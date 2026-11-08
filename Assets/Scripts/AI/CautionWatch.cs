using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// Local yellow flags: watches the whole circuit for a car that has stopped — a spin that never got going
// again, a wreck, anything parked on the racing surface — and flags the stretch of road before it.
//
// The player's flag also goes out the instant a car up the road hits a barrier (VehicleCollision.AnyBarrierHit),
// for wallHitFlagSeconds after the hit. Waiting for the wreck to come to rest left the flag showing only once
// the player was already on top of it.
//
// Two readers. The HUD's yellow flag asks whether there is an incident just up the road from the player
// (CautionAhead). The AI ask whether they are inside any incident's yellow zone (YellowFor), and lift and hold
// station through it (AIRacingBehaviour).
//
// The field is read off RacePositionTracker rather than RaceField where there is one, for the same reason the
// tracker itself does it: on a client the AI are network puppets with their brains disabled, so they are not
// in RaceField, but the tracker still has them with a live progress and speed. With no tracker (a headless
// sim), RaceField is read instead. Cars in the pit lane are skipped — a car sitting on its jacks is stationary
// and is not a hazard on the track. So are retired cars (AIRetirement): out of the race and parked off the circuit.
//
// Distance is measured along the centerline, not as the crow flies: a car stopped on the far side of a
// short oval is metres away in world space and most of a lap away on the road.
public class CautionWatch : MonoBehaviour
{
    public static CautionWatch Instance { get; private set; }

    [Tooltip("Look this far (m) up the road from the player for a stopped car or a wall hit (the HUD flag). Several seconds at racing speed, so the flag is up before the scene is.")]
    public float lookAheadMetres = 400f;
    [Tooltip("At or below this speed (mph) a car counts as stopped.")]
    public float stoppedMph = 8f;
    [Tooltip("A car must be that slow for this long (s) before it raises a flag — a car being passed at the exit of a slow hairpin is not an incident.")]
    public float stoppedForSeconds = 0.75f;
    [Tooltip("Once raised, hold the flag at least this long (s), so it doesn't strobe as the player drives past the stopped car.")]
    public float holdSeconds = 1.5f;
    [Tooltip("A car up the road that hits a barrier at least this hard (closing speed, m/s) flags a yellow for the player at once. Below it is a brush along the wall.")]
    public float wallHitMinClosingMps = 3f;
    [Tooltip("How long (s) after its last barrier hit a car keeps the player's flag out. If it stops, the stopped-car rule has taken over by then; if it drives on, the flag drops.")]
    public float wallHitFlagSeconds = 4f;

    [Header("Wrecks")]
    [Tooltip("A car pointing this far (deg) off the road while it is moving is wrecking - spun, sideways, going round. The field steers clear of it the way it does a stopped car, only with more room (AIRacingBehaviour.wreckPassClearance).")]
    public float wreckYawDeg = 30f;
    [Tooltip("Slower than this (m/s) a car's heading says nothing - it may be pointing anywhere as it gets going again.")]
    public float wreckMinSpeedMps = 4f;
    [Tooltip("A car that hit a barrier at least wallHitMinClosingMps hard counts as wrecking for this long (s) after the hit.")]
    public float wreckHitSeconds = 2f;
    [Tooltip("Once wrecking, a car stays flagged this long (s) after it last looked it, so a spin does not strobe on and off as it swings through straight.")]
    public float wreckHoldSeconds = 1.5f;

    public struct Incident
    {
        public Transform car;      // the stopped car — never flags a yellow for itself
        public float distance;     // along the lap (m)
    }

    // Every stopped car on the road right now.
    public IReadOnlyList<Incident> Incidents => _incidents;
    // Every car that hit a barrier within wallHitFlagSeconds and is still out on the road. HUD flag only —
    // the AI lift for stopped cars, not for every car that glances off the wall.
    public IReadOnlyList<Incident> WallHits => _wallHits;

    // Every car wrecking right now (wreckYawDeg, a fresh wall hit, the AI's own spin recovery, stopped on the road) -
    // the stopped incidents above included. What the field steers round; the yellow lift still only reads Incidents.
    public IReadOnlyList<Incident> Wrecks => _wrecks;

    // Whether `car` is wrecking right now (see Wrecks).
    public bool IsWrecking(Transform car) => car != null && _wrecking.Contains(car);

    // Seconds since `car` was last seen wrecking; +infinity if it never has been (this race).
    public float SecondsSinceWreck(Transform car) =>
        car != null && _lastWreckAt.TryGetValue(car, out float at) ? _now - at : float.PositiveInfinity;

    // True while there is a stopped car within lookAheadMetres up the road from the player.
    public bool CautionAhead { get; private set; }
    // Distance (m) along the track to the nearest stopped car ahead; 0 when nothing is flagged.
    public float DistanceToIncident { get; private set; }

    readonly Dictionary<Transform, float> _slowSince = new();
    readonly List<Incident> _incidents = new();
    readonly Dictionary<Transform, float> _wallHitAt = new();
    readonly List<Incident> _wallHits = new();
    readonly List<Incident> _wrecks = new();
    readonly HashSet<Transform> _wrecking = new();
    readonly Dictionary<Transform, float> _lastWreckAt = new();
    readonly Dictionary<Transform, PlayerVehicleController> _pvcOf = new();
    readonly Dictionary<Transform, SplineInputDriver> _inputOf = new();
    float _now;
    float _holdUntil;
    float _lapLength;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("CautionWatch");
        DontDestroyOnLoad(go);
        go.AddComponent<CautionWatch>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable() => VehicleCollision.AnyBarrierHit += OnBarrierHit;
    void OnDisable() => VehicleCollision.AnyBarrierHit -= OnBarrierHit;

    void OnBarrierHit(Transform car, float closingMps) => ReportBarrierHit(car, closingMps, Time.time);

    // A car touched a barrier. `now` is on the same clock as Tick.
    public void ReportBarrierHit(Transform car, float closingMps, float now)
    {
        if (car == null || closingMps < wallHitMinClosingMps) return;
        _wallHitAt[car] = now;
    }

    void Update() => Tick(Time.time);

    // Is `self`, at myDist along a lap of lapLength, inside the yellow zone of a stopped car other than itself?
    // gap = metres to that incident (+ ahead, - already past). The zone is `before` metres up to the car and
    // `after` metres beyond it.
    public bool YellowFor(Transform self, float myDist, float lapLength, float before, float after, out float gap)
        => YellowFor(self, myDist, lapLength, before, after, out gap, out _);

    // ...and which car it is: the nearest incident still AHEAD if there is one (the thing to drive round), else
    // the one just passed.
    public bool YellowFor(Transform self, float myDist, float lapLength, float before, float after,
                          out float gap, out Transform incidentCar)
        => Nearest(_incidents, self, myDist, lapLength, before, after, out gap, out incidentCar);

    // The same zone test against every wrecking car (Wrecks, the stopped ones included): the car to steer round.
    public bool WreckFor(Transform self, float myDist, float lapLength, float before, float after,
                         out float gap, out Transform wreckCar)
        => Nearest(_wrecks, self, myDist, lapLength, before, after, out gap, out wreckCar);

    static bool Nearest(List<Incident> list, Transform self, float myDist, float lapLength, float before, float after,
                        out float gap, out Transform incidentCar)
    {
        gap = 0f;
        incidentCar = null;
        bool found = false;
        float best = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            var inc = list[i];
            if (inc.car == null || inc.car == self) continue;
            if (!RaceCraft.InYellowZone(myDist, inc.distance, lapLength, before, after, out float g)) continue;
            float rank = g >= 0f ? g : 10000f - g;   // anything ahead outranks anything behind
            if (rank < best) { best = rank; gap = g; incidentCar = inc.car; found = true; }
        }
        return found;
    }

    // One look at the field. `now` is the clock the stopped-for timer runs on (Time.time in a session; the
    // sim's own clock in a headless test, where Time.time never moves).
    public void Tick(float now)
    {
        _incidents.Clear();
        _wallHits.Clear();
        _wrecks.Clear();
        _now = now;
        var tracker = RacePositionTracker.Instance;
        if (tracker != null && tracker.TrackLength > 0f) ScanTracker(tracker, now);
        else ScanRaceField(now);
        PruneDead();
        UpdatePlayerFlag(tracker, now);
    }

    void ScanTracker(RacePositionTracker tracker, float now)
    {
        float len = tracker.TrackLength;
        _lapLength = len;
        float stoppedMps = stoppedMph / 2.237f;
        var order = tracker.Order;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;
            // Retired (AIRetirement): parked behind the infield wall, out of the race and no hazard to anybody.
            if (AIRetirement.IsRetired(e.tf)) { _slowSince.Remove(e.tf); _wallHitAt.Remove(e.tf); continue; }
            // trackDistance, not progress: the AI test their own DistanceOnTrack (from segment[0]) against these,
            // and progress is counted from the start/finish line. At Watkins Glen that put every wreck 250 m
            // behind where it was — the field lifted for a yellow a quarter-kilometre short of the scene, then
            // drove into it at racing speed.
            bool inPits = InPits(tracker, e);
            bool stopped = e.speedMps <= stoppedMps && !inPits;
            var here = new Incident { car = e.tf, distance = Mathf.Repeat(e.trackDistance, len) };
            bool stoppedLong = StoppedLongEnough(e.tf, stopped, now);
            if (stoppedLong) _incidents.Add(here);
            bool hitRecently = _wallHitAt.TryGetValue(e.tf, out float hitAt) && now - hitAt <= wallHitFlagSeconds;
            if (hitRecently && !inPits) _wallHits.Add(here);
            NoteWreck(here, e.spline, inPits, stoppedLong, hitRecently && now - hitAt <= wreckHitSeconds, now);
        }
    }

    // Wrecking this step or within wreckHoldSeconds: into Wrecks.
    void NoteWreck(Incident here, SplineDriver spline, bool inPits, bool stopped, bool hitHard, float now)
    {
        var tf = here.car;
        if (!inPits && Wrecking(tf, spline, here.distance, stopped, hitHard)) _lastWreckAt[tf] = now;
        if (!inPits && _lastWreckAt.TryGetValue(tf, out float wreckAt) && now - wreckAt <= wreckHoldSeconds)
        {
            _wrecks.Add(here);
            _wrecking.Add(tf);
        }
        else _wrecking.Remove(tf);
    }

    // Whether this car looks like it is crashing this step. Only while racing (the green, or a caution - a crash is
    // still happening when the flag comes out): a grid sat still for the start is not a wreck.
    bool Wrecking(Transform tf, SplineDriver spline, float trackDistance, bool stopped, bool hitHard)
    {
        if (!RaceStart.IsRunning || AIRetirement.IsRetired(tf)) return false;
        if (hitHard || stopped) return true;
        if (!_inputOf.TryGetValue(tf, out var input)) _inputOf[tf] = input = tf.GetComponent<SplineInputDriver>();
        if (input != null && input.isActiveAndEnabled && input.IsRecovering) return true;
        if (!_pvcOf.TryGetValue(tf, out var pvc)) _pvcOf[tf] = pvc = tf.GetComponent<PlayerVehicleController>();
        // The dynamic model's heading: a kinematic car (PVC off) sits wherever its spline puts it, on the road.
        if (pvc == null || !pvc.enabled || pvc.SpeedMps < wreckMinSpeedMps) return false;
        var road = RoadReference(spline);
        if (road == null) return false;
        float yaw = Mathf.DeltaAngle(road.MainTangentWorldDeg(trackDistance), pvc.HeadingDeg);
        return Mathf.Abs(yaw) > wreckYawDeg;
    }

    // Any spline on the track to read the road direction off: the car's own, else the first in the field.
    static SplineDriver RoadReference(SplineDriver own)
    {
        if (own != null && own.TrackLength > 0f) return own;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
            if (drivers[i] != null && drivers[i].TrackLength > 0f) return drivers[i];
        return null;
    }

    void ScanRaceField(float now)
    {
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d.TrackLength <= 0f) continue;
            if (AIRetirement.IsRetired(d.transform)) { _slowSince.Remove(d.transform); continue; }
            _lapLength = d.TrackLength;
            bool stopped = d.CurrentMph <= stoppedMph && !d.IsOnPit;
            var here = new Incident { car = d.transform, distance = d.DistanceOnTrack };
            bool stoppedLong = StoppedLongEnough(d.transform, stopped, now);
            if (stoppedLong) _incidents.Add(here);
            bool hitHard = _wallHitAt.TryGetValue(d.transform, out float hitAt) && now - hitAt <= wreckHitSeconds;
            NoteWreck(here, d, d.IsOnPit, stoppedLong, hitHard, now);
        }
    }

    bool StoppedLongEnough(Transform car, bool stopped, float now)
    {
        if (!stopped) { _slowSince.Remove(car); return false; }
        if (!_slowSince.TryGetValue(car, out float since)) { _slowSince[car] = now; since = now; }
        return now - since >= stoppedForSeconds;
    }

    // The HUD's flag: an incident within lookAheadMetres up the road from the local player's car.
    void UpdatePlayerFlag(RacePositionTracker tracker, float now)
    {
        float nearest = float.MaxValue;
        if (tracker != null && _lapLength > 0f)
        {
            var order = tracker.Order;
            RacePositionTracker.Entry me = null;
            for (int i = 0; i < order.Count; i++) if (order[i] != null && order[i].isPlayer) { me = order[i]; break; }
            if (me != null && me.tf != null)
            {
                float myDist = Mathf.Repeat(me.trackDistance, _lapLength);
                for (int i = 0; i < _incidents.Count; i++)
                {
                    if (_incidents[i].car == me.tf) continue;
                    // Gap up the road, wrapped — a car just over the start/finish line is ahead of a player
                    // who has not reached it yet, not a lap away.
                    float gap = Mathf.Repeat(_incidents[i].distance - myDist, _lapLength);
                    if (gap > 0f && gap <= lookAheadMetres && gap < nearest) nearest = gap;
                }
                for (int i = 0; i < _wallHits.Count; i++)
                {
                    var hit = _wallHits[i];
                    if (hit.car == me.tf || !_wallHitAt.TryGetValue(hit.car, out float hitAt)) continue;
                    float gap = Mathf.Repeat(hit.distance - myDist, _lapLength);
                    if (RaceCraft.WallHitFlagsYellow(gap, lookAheadMetres, now - hitAt, wallHitFlagSeconds) && gap < nearest)
                        nearest = gap;
                }
            }
        }

        if (nearest < float.MaxValue)
        {
            CautionAhead = true;
            DistanceToIncident = nearest;
            _holdUntil = now + holdSeconds;
        }
        else if (now >= _holdUntil)
        {
            CautionAhead = false;
            DistanceToIncident = 0f;
        }
    }

    static bool InPits(RacePositionTracker tracker, RacePositionTracker.Entry e)
    {
        if (e.spline != null && e.spline.enabled) return e.spline.IsOnPit;
        return tracker.track != null && tracker.track.IsOnPitSurface(e.tf.position);
    }

    // Destroyed transforms compare == null but still hash as keys.
    // Wall hits older than the flag window go too.
    void PruneDead()
    {
        List<Transform> dead = null;
        foreach (var kv in _slowSince)
            if (kv.Key == null) (dead ??= new List<Transform>()).Add(kv.Key);
        if (dead != null) for (int i = 0; i < dead.Count; i++) _slowSince.Remove(dead[i]);

        dead?.Clear();
        foreach (var kv in _wallHitAt)
            if (kv.Key == null || _now - kv.Value > wallHitFlagSeconds) (dead ??= new List<Transform>()).Add(kv.Key);
        if (dead != null) for (int i = 0; i < dead.Count; i++) _wallHitAt.Remove(dead[i]);

        dead?.Clear();
        foreach (var kv in _lastWreckAt)
            if (kv.Key == null) (dead ??= new List<Transform>()).Add(kv.Key);
        if (dead != null)
            for (int i = 0; i < dead.Count; i++)
            {
                _lastWreckAt.Remove(dead[i]);
                _pvcOf.Remove(dead[i]);
                _inputOf.Remove(dead[i]);
            }
        _wrecking.RemoveWhere(t => t == null);
    }
}
