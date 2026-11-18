using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// An AI car's way out of the race.
//
// Two things end it (AIRetirementRule). Bent past breakdownDamage, the car stops working: the engine is gone, the
// throttle is cut and the driver rolls it to a stop on the brakes, still steering along their line. And a car that
// sits stopped on the circuit too long — broken down, beached, wedged against the wall, facing the wrong way — is
// retired: lifted off the circuit and parked on the far side of the infield wall, with room checked for it there.
// Where a stretch has no infield wall it is parked well out in the infield instead, clear of every part of the road.
//
// The move is never seen. Once the car is due to go its spot is picked and held for it, but the car stays where it
// stopped (still on the circuit, still raising its yellow) until neither it nor the spot is inside any camera's
// frame, and only then jumps. A fixed view that never looks away (a grandstand seat, a camera following this very
// car) gets forceMoveAfterSeconds before the car goes anyway, so it cannot hold a caution out for ever. A stuck car
// that gets going again while it waits is back in the race.
//
// A retired car is out of the race. Its brain, inputs and collision response are switched off, it is pinned where
// it was put (ParkedCarPin), and CautionWatch skips it — a stopped car behind the wall is not an incident, so it
// stops raising local yellows and no longer holds a full-course caution out. RaceDirector stops waiting for it at
// the chequer; it is classified laps down like any car that did not finish.
//
// Race only, and only while the race is running (green or caution): a car held on the grid, queued in the pit
// lane or parked after the flag is not stuck. Practice and qualifying keep their own stint control. Never the
// human's car — the player has the tow (StrandedTow).
//
// Added by GridSpawner to every dynamic-model AI car. Runs between SplineInputDriver (-50), whose inputs it
// overrides once the car has broken down, and PlayerVehicleController (0), which consumes them.
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public class AIRetirement : MonoBehaviour
{
    [Header("Breakdown")]
    [Tooltip("Bodywork damage (VehicleDamage.DamageLevel, 0..1 — a full-severity hit adds ~0.18) at which the car " +
             "stops working. 0 = cars never break down.")]
    [Range(0f, 1f)] public float breakdownDamage = 0.6f;
    [Tooltip("Brake (0..1) a broken-down car rolls to a stop on. Gentle: it is coasting off, not stopping for a wreck.")]
    [Range(0f, 1f)] public float breakdownBrake = 0.3f;

    [Header("Retirement")]
    [Tooltip("At or below this speed (mph) the car counts as stopped.")]
    public float stoppedBelowMph = 4f;
    [Tooltip("Seconds a broken-down car sits stopped before it is retired.")]
    public float brokenRetireSeconds = 4f;
    [Tooltip("Seconds a car that could still drive has to sit stopped on the circuit before it is retired as stuck.")]
    public float stuckRetireSeconds = 15f;
    [Tooltip("A car that is not broken and has another car this close (m) ahead of it is queued, not stuck.")]
    public float queuedBehindMetres = 15f;

    [Header("Parking spot")]
    [Tooltip("How far (m) into the infield from the road's edge to look for the infield wall.")]
    public float barrierSearchMetres = 60f;
    [Tooltip("Gap (m) between the infield wall and the parked car's centre.")]
    public float behindBarrierMetres = 4f;
    [Tooltip("Where there is no infield wall: how far (m) into the infield from the road's edge to park.")]
    public float noBarrierMetres = 35f;
    [Tooltip("Least clearance (m) between the parked car and the edge of any part of the road.")]
    public float roadClearanceMetres = 4f;
    [Tooltip("Least distance (m) between two retired cars.")]
    public float spacingMetres = 8f;
    [Tooltip("Step (m) along the track between spots tried, and how far either way from where the car stopped.")]
    public float alongStepMetres = 12f;
    public float alongMaxMetres = 240f;

    [Header("Out of shot")]
    [Tooltip("Extra clearance (m) beyond a camera's frame that both the car and its parking spot need before the " +
             "car is moved: covers the camera's travel between this physics step and the next render.")]
    public float outOfShotMargin = 10f;
    [Tooltip("Seconds a car due to retire waits on the circuit for the camera to look away before it is moved " +
             "anyway. A fixed view (a grandstand seat, the camera following this car) would otherwise keep it " +
             "there, and its caution out, for ever. 0 = wait for ever.")]
    public float forceMoveAfterSeconds = 90f;

    static readonly HashSet<Transform> _retired = new();
    static readonly HashSet<Transform> _retiring = new();
    static Camera[] _cameras;
    static readonly List<Vector2> _takenSpots = new();
    static TrackBuilder _samplesFor;
    static List<TrackBuilder.Sample> _samples;
    static List<Vector2> _centres;
    static List<float> _halfWidths;

    // Whether this car has retired from the race.
    public static bool IsRetired(Transform car) => car != null && _retired.Contains(car);
    // Retired, or due to and only waiting for the camera to look away before it is moved off the circuit. Either
    // way it will not finish. A waiting car is still on the road, so CautionWatch reads IsRetired, not this.
    public static bool IsOutOfRace(Transform car) => car != null && (_retired.Contains(car) || _retiring.Contains(car));

    public bool BrokenDown { get; private set; }
    public bool Retired { get; private set; }

    SplineDriver _spline;
    SplineInputDriver _input;
    PlayerVehicleController _car;
    VehicleDamage _bodywork;
    float _stoppedSince = -1f;

    // Due to retire, waiting out of shot (see the header): where it will go, and since when.
    bool _waiting;
    Vector2 _waitSpot;
    float _waitHeading;
    bool _waitBehindWall;
    float _waitingSince;
    int _layerMask;     // layers the car is drawn on; a camera that culls them all cannot see it

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _retired.Clear();
        _retiring.Clear();
        _takenSpots.Clear();
        _cameras = null;
        _samplesFor = null;
        _samples = null;
        _centres = null;
        _halfWidths = null;
    }

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        _input = GetComponent<SplineInputDriver>();
        _car = GetComponent<PlayerVehicleController>();
    }

    void OnDestroy()
    {
        _retired.Remove(transform);
        _retiring.Remove(transform);
    }

    void FixedUpdate()
    {
        if (Retired || _spline == null || _car == null) return;
        if (_input == null) _input = GetComponent<SplineInputDriver>();

        // Engine gone: no throttle, roll to a stop. Steering still follows the driver's line. Latched — a broken
        // car does not start driving again because the flag fell or a caution came out.
        if (BrokenDown && _input != null && _input.enabled && _car.externalInput)
            _car.SetInput(_input.LastSteer, 0f, breakdownBrake);

        if (_waiting) { TickWaiting(); return; }

        if (!InPlay()) { _stoppedSince = -1f; return; }

        if (_bodywork == null) _bodywork = GetComponentInChildren<VehicleDamage>();

        // A car in the pit lane is about to be repaired, not broken down; it breaks down where it is out on track,
        // on its own dynamic model — not while a caution's formation queue is driving it kinematically.
        if (!BrokenDown && !_spline.IsOnPit && _bodywork != null && _input != null && _input.enabled
            && AIRetirementRule.BrokenDown(_bodywork.DamageLevel, breakdownDamage))
            BreakDown();

        float now = Time.fixedTime;
        if (!StoppedOnCircuit()) { _stoppedSince = -1f; return; }
        if (_stoppedSince < 0f) _stoppedSince = now;
        if (AIRetirementRule.Retire(now - _stoppedSince, BrokenDown, brokenRetireSeconds, stuckRetireSeconds)
            && !BeginRetire())
            _stoppedSince = now;   // nowhere to put it: wait out the timer again before the next search
    }

    // The race is on, this is an AI car in it, and it has not taken the flag.
    bool InPlay()
    {
        if (!RaceStart.IsRunning || RaceWeekend.IsPracticeLike) return false;
        if (!_car.isActiveAndEnabled || PlayerVehicleController.IsHuman(_car) || IsTrackedAsPlayer()) return false;
        return !RaceDirector.TookChequer(transform);
    }

    // The player's own car can carry an AI brain (broadcast cut, crew chief, a team switch) — it is still theirs.
    bool IsTrackedAsPlayer()
    {
        var rt = RacePositionTracker.Instance;
        if (rt == null) return false;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
            if (order[i] != null && order[i].tf == transform) return order[i].isPlayer;
        return false;
    }

    bool StoppedOnCircuit()
    {
        if (_car.SpeedMph > stoppedBelowMph) return false;
        if (_spline.IsHeldStill) return false;
        // On pit road a car is waiting for its stop. Not a broken one: BreakDown switched its pit stop off, so
        // nobody is coming to fix it, and left there it is a roadblock the caution queue waits behind for ever.
        if (!BrokenDown && _spline.IsOnPit) return false;
        if (!BrokenDown && _spline.track != null && _spline.track.IsOnPitSurface(transform.position)) return false;
        // Under formation control (the caution's kinematic queue) the car is being driven for it.
        if (_input == null || !_input.enabled) return false;
        // Sat behind a car that has stopped is a queue, not a stuck car — unless this one is broken.
        if (!BrokenDown && queuedBehindMetres > 0f && RaceField.TryGetAhead(_spline, queuedBehindMetres, out _, out _))
            return false;
        return true;
    }

    void BreakDown()
    {
        BrokenDown = true;
        // A car with no engine is not coming in for tyres: the stop would leave it rolling down pit road.
        var pits = GetComponent<PitStopController>();
        if (pits != null) pits.enabled = false;
        // Nor is it joining a caution's queue: the caution its own stop brings out would otherwise take it over
        // kinematically and drive it round behind the safety car instead of leaving it to be retired.
        var formation = GetComponent<FormationController>();
        if (formation != null) formation.enabled = false;
        Debug.Log($"[AIRetire] {name} broke down (damage {_bodywork.DamageLevel:0.00}) at {_spline.DistanceOnTrack:0} m");
    }

    // Due out of the race: pick and hold a spot over the infield wall, then wait for the camera to look away
    // before moving there (TickWaiting) — straight away, most of the time, as nobody is looking.
    // Returns false, leaving the car racing (and flagged), when there is nowhere off the road to put it.
    bool BeginRetire()
    {
        // Room checked first. Should nothing pass (a scene where something solid covers the whole infield), the
        // spot only has to be clear of the road. And if not even that, the car stays where it is, still in the race
        // and still raising its yellow: a retired car raises none, so it must never be left on the road.
        if (!FindParkingSpot(true, out Vector2 spot, out float headingDeg, out bool behindWall)
            && !FindParkingSpot(false, out spot, out headingDeg, out behindWall))
        {
            Debug.LogWarning($"[AIRetire] {name}: no spot off the road to retire to; left racing");
            return false;
        }

        _takenSpots.Add(spot);   // held while it waits, so no other car is parked on it
        _retiring.Add(transform);
        _waiting = true;
        _waitSpot = spot;
        _waitHeading = headingDeg;
        _waitBehindWall = behindWall;
        _waitingSince = Time.fixedTime;
        TickWaiting();
        if (_waiting)
            Debug.Log($"[AIRetire] {name} retiring ({(BrokenDown ? "broken down" : "stuck")}) — in shot, " +
                      "moves once the camera looks away");
        return true;
    }

    // Waiting to be moved: go the moment neither the car nor its spot is on screen.
    void TickWaiting()
    {
        // Taken over by the player (a team switch into it): theirs to get going again, not ours to tidy away. And a
        // car that was only stuck, and has got going again or been taken over by a caution's queue (which drives it
        // kinematically, input off), is back in the race.
        if (PlayerVehicleController.IsHuman(_car) || IsTrackedAsPlayer()
            || (!BrokenDown && (_car.SpeedMph > stoppedBelowMph || _input == null || !_input.enabled)))
        {
            _waiting = false;
            _retiring.Remove(transform);
            _takenSpots.Remove(_waitSpot);
            _stoppedSince = -1f;
            return;
        }

        float radius = CarSize().magnitude * 0.5f;
        bool carSeen = InShot(transform.position, radius);
        bool spotSeen = InShot(new Vector3(_waitSpot.x, _waitSpot.y, transform.position.z), radius);
        if (!AIRetirementRule.MoveOutOfSight(carSeen, spotSeen, Time.fixedTime - _waitingSince, forceMoveAfterSeconds))
            return;

        _waiting = false;
        _retiring.Remove(transform);
        Retire(_waitSpot, _waitHeading, _waitBehindWall, carSeen || spotSeen);
    }

    // Whether any camera drawing to the screen has this circle in its frame. No camera (a headless sim): nobody.
    bool InShot(Vector3 point, float radius)
    {
        int count = Camera.allCamerasCount;
        if (count == 0) return false;
        if (_cameras == null || _cameras.Length < count) _cameras = new Camera[count];
        count = Camera.GetAllCameras(_cameras);
        if (_layerMask == 0) _layerMask = DrawnLayers();

        for (int i = 0; i < count; i++)
        {
            var cam = _cameras[i];
            if (cam == null || cam.targetTexture != null || cam.cameraType != CameraType.Game) continue;
            if ((cam.cullingMask & _layerMask) == 0) continue;   // a UI-only camera
            Vector3 at = cam.transform.position;
            if (!AIRetirementRule.OutOfShot(point, radius, at, HalfHeight(cam, Mathf.Abs(point.z - at.z)),
                                            cam.aspect, outOfShotMargin))
                return true;
        }
        return false;
    }

    // Half the frame's height (m) as it is drawn, `depth` metres in front of the camera. The race camera's
    // orthographic size is opened out only while it renders — the swing's view-ahead (DrivingCameraFeel.ViewZoom)
    // and an upright phone's (DriveOrientationController) — so both are put back on here.
    static float HalfHeight(Camera cam, float depth)
    {
        if (!cam.orthographic)
            return depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float size = cam.orthographicSize;
        var feel = cam.GetComponent<DrivingCameraFeel>();
        if (feel != null && feel.isActiveAndEnabled) size *= Mathf.Max(1f, feel.ViewZoom);
        size *= Draftmaster.Controls.DriveOrientation.PortraitZoom(cam.pixelWidth, cam.pixelHeight);
        return size;
    }

    int DrawnLayers()
    {
        int mask = 1 << gameObject.layer;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) mask |= 1 << r.gameObject.layer;
        return mask;
    }

    // Out of the race: off the circuit, over the infield wall, switched off and pinned there.
    void Retire(Vector2 spot, float headingDeg, bool behindWall, bool forced)
    {
        Retired = true;
        _retired.Add(transform);

        // Everything that would drive, steer or react to contact goes off first; a live controller would spend the
        // frame fighting the move.
        // FormationController first: it switches the car's controls back on at the next green.
        var formation = GetComponent<FormationController>();
        if (formation != null) formation.enabled = false;
        var ai = GetComponent<AIRacingBehaviour>();
        if (ai != null) ai.enabled = false;
        var pits = GetComponent<PitStopController>();
        if (pits != null) pits.enabled = false;
        if (_input != null) _input.enabled = false;
        _spline.enabled = false;   // leaves RaceField and FormationOrder: nobody races, queues or drafts on it
        var col = GetComponent<VehicleCollision>();
        if (col != null) col.enabled = false;

        _car.SetInput(0f, 0f, 1f);
        _car.externalInput = false;
        _car.SeedPose(spot, headingDeg);
        _car.enabled = false;

        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = transform.position;
            body.rotation = transform.eulerAngles.z;
        }
        ParkedCarPin.Hold(_car);

        // Moved, not driven: the jump must not be read as progress or a line crossing.
        RacePositionTracker.NoteTeleport(transform);
        if (LapTimingManager.Instance != null) LapTimingManager.Instance.AbandonLap(transform);

        Debug.Log($"[AIRetire] {name} retired ({(BrokenDown ? "broken down" : "stuck")}) — " +
                  (behindWall ? "parked behind the infield wall" : "parked in the infield, no wall") +
                  (forced ? $" (still in shot after {forceMoveAfterSeconds:0} s; moved anyway)" : ""));
    }

    // A spot on the infield side of the track near where the car stopped: just over the infield wall if there is
    // one within barrierSearchMetres, else noBarrierMetres into the infield. Room is checked — clear of every part
    // of the road and the pit lane, of anything solid, and of other retired cars — trying spots further along the
    // track either way until one fits.
    // `strict` off skips the solid-object and other-car checks, keeping only the road and pit-lane clearance.
    bool FindParkingSpot(bool strict, out Vector2 spot, out float headingDeg, out bool behindWall)
    {
        spot = default;
        headingDeg = 0f;
        behindWall = false;
        var track = _spline.track;
        if (track == null || track.track == null || !EnsureSamples(track)) return false;

        var tf = track.transform;
        Vector2 local = tf.InverseTransformPoint(transform.position);
        int near = AIRetirementRule.NearestIndex(local, _centres);
        if (near < 0) return false;
        float here = _samples[near].distance;
        float lapLength = _samples[_samples.Count - 1].distance;
        if (lapLength <= 0f) return false;
        bool turnsLeft = track.track.LapTurnsLeft();

        var size = CarSize();
        foreach (float along in AIRetirementRule.AlongOffsets(alongStepMetres, alongMaxMetres))
        {
            var s = track.SampleAt(Mathf.Repeat(here + along, lapLength), _samples);
            Vector2 inLocal = turnsLeft ? -s.normal : s.normal;   // sample normal is the right of travel
            Vector2 inWorld = ((Vector2)tf.TransformDirection(new Vector3(inLocal.x, inLocal.y, 0f))).normalized;
            Vector2 tangentWorld = ((Vector2)tf.TransformDirection(new Vector3(s.tangent.x, s.tangent.y, 0f))).normalized;
            Vector2 edgeWorld = tf.TransformPoint(new Vector3(s.position.x + inLocal.x * s.width * 0.5f,
                                                              s.position.y + inLocal.y * s.width * 0.5f, 0f));
            float heading = Mathf.Atan2(tangentWorld.y, tangentWorld.x) * Mathf.Rad2Deg;

            bool wall = FindInfieldWall(edgeWorld, inWorld, out Vector2 wallPoint);
            Vector2 from = wall ? wallPoint : edgeWorld;
            float first = wall ? behindBarrierMetres + size.y * 0.5f : noBarrierMetres;
            for (int k = 0; k < 4; k++)
            {
                Vector2 p = from + inWorld * (first + k * 5f);
                if (!HasRoom(track, p, heading, size, strict)) continue;
                spot = p;
                headingDeg = heading;
                behindWall = wall;
                return true;
            }
        }
        return false;
    }

    // The nearest barrier straight into the infield from the road's edge.
    bool FindInfieldWall(Vector2 edgeWorld, Vector2 inWorld, out Vector2 point)
    {
        point = default;
        var hits = Physics2D.RaycastAll(edgeWorld - inWorld, inWorld, barrierSearchMetres + 1f);
        for (int i = 0; i < hits.Length; i++)   // sorted nearest first
        {
            var c = hits[i].collider;
            if (c == null || c.isTrigger || !IsBarrier(c.transform)) continue;
            point = hits[i].point;
            return true;
        }
        return false;
    }

    // Track walls are TrackEnvironmentBuilder's "Barrier_*" pieces under its "Barriers" root.
    static bool IsBarrier(Transform t)
    {
        for (int depth = 0; t != null && depth < 3; depth++, t = t.parent)
            if (t.name.StartsWith("Barrier")) return true;
        return false;
    }

    bool HasRoom(TrackBuilder track, Vector2 p, float headingDeg, Vector2 size, bool strict)
    {
        if (strict && !AIRetirementRule.ClearOfOthers(p, _takenSpots, spacingMetres)) return false;

        // Every corner clear of the road (all of it — a road course's infield has more circuit in it) and the pits.
        var tf = track.transform;
        float rad = headingDeg * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 side = new Vector2(-fwd.y, fwd.x);
        for (int cx = -1; cx <= 1; cx += 2)
        for (int cy = -1; cy <= 1; cy += 2)
        {
            Vector2 corner = p + fwd * (cx * size.x * 0.5f) + side * (cy * size.y * 0.5f);
            Vector2 cornerLocal = tf.InverseTransformPoint(corner);
            if (!AIRetirementRule.ClearOfRoad(cornerLocal, _centres, _halfWidths, roadClearanceMetres)) return false;
            if (track.IsOnPitSurface(new Vector3(corner.x, corner.y, transform.position.z))) return false;
        }

        if (!strict) return true;

        // Nothing solid there: walls, buildings, a car already parked.
        var overlaps = Physics2D.OverlapBoxAll(p, size + Vector2.one * 2f, headingDeg);
        for (int i = 0; i < overlaps.Length; i++)
        {
            var c = overlaps[i];
            if (c == null || c.isTrigger || c.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    // Length (x) by width (y), metres.
    Vector2 CarSize()
    {
        var col = GetComponent<VehicleCollision>();
        Vector2 half = col != null ? col.halfExtents : new Vector2(1f, 2.4f);
        return new Vector2(half.y * 2f, half.x * 2f);
    }

    static bool EnsureSamples(TrackBuilder track)
    {
        if (_samplesFor == track && _samples != null && _samples.Count >= 2) return true;
        _samples = track.SampleCenterline();
        _samplesFor = track;
        if (_samples == null || _samples.Count < 2) return false;
        _centres = new List<Vector2>(_samples.Count);
        _halfWidths = new List<float>(_samples.Count);
        for (int i = 0; i < _samples.Count; i++)
        {
            _centres.Add(_samples[i].position);
            _halfWidths.Add(_samples[i].width * 0.5f);
        }
        return true;
    }
}
