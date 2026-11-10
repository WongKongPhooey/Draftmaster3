using System.Collections.Generic;
using Draftmaster.Tracks;
using UnityEngine;

// Puts the oval lane AI (Docs/Oval-Lanes.md, stages 1-3) into live races. Under green on an oval, every AI car on
// the main track is given one of the two lanes - its train, its spacing and its passing by lane change then run as
// in the sim. Off the green (pre-grid, the formation lap, a full-course caution) the lanes it gave are taken back,
// so the formation brain lines the field up two-wide and single file exactly as before, and the next green hands
// them out again from wherever the cars are.
//
// Which lane: the one the car is nearest. Cars alongside each other at the green (a rolling start's two-wide rows)
// are split so the one further toward the infield takes the inside lane and the other the outside, never both into
// one lane. A car alongside one that already has a lane takes the other.
//
// No snap: the lane replaces the car's line (and its formation offsets) at once, so the car is handed its lane with
// lateralOffset set to where it already is relative to the lane line, and that offset is eased off at the racing
// brain's sideways rate (AIRacingBehaviour eases lateralOffset to 0 under green; a practice stint car, which it
// leaves alone, is eased here).
//
// Not on road courses (SplineDriver ignores ovalLane there anyway) and not on the plate tracks, which keep their
// three-wide pack AI. Cars a test or a scene set a lane on by hand are left alone. Self-bootstraps: no scene wiring.
// Pause menu > "Lane AI" turns it off for an A/B (PlayerPrefs OvalLaneAI, default on).
[DefaultExecutionOrder(-60)]   // before SplineDriver's FixedUpdate, so a lane given this step is laid this step
public class OvalLaneDirector : MonoBehaviour
{
    public static OvalLaneDirector Instance { get; private set; }

    const string PrefKey = "OvalLaneAI";
    static int _enabled = -1;   // cached pref: -1 unread

    // On/off, surfaced in the pause menu. Persisted; applies live (off hands every lane back at once).
    public static bool Enabled
    {
        get
        {
            if (_enabled < 0) _enabled = PlayerPrefs.GetInt(PrefKey, 1);
            return _enabled == 1;
        }
        set
        {
            _enabled = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefKey, _enabled);
        }
    }

    // Whether lanes can be raced on the track the field is on right now (the pause menu offers the toggle then).
    public static bool Applies => Instance != null && Instance._lanesFor != null && !TrackConditions.AiPackRacing;

    [Tooltip("Cars alongside each other (m along the track, centre to centre) are split across the two lanes rather than both given the nearer one.")]
    public float alongsideMetres = 6f;
    [Tooltip("Across the track (m, centre to centre) within which two cars count as alongside.")]
    public float alongsideLateral = 4.5f;
    [Tooltip("Most cars given a lane in one physics step. Each one re-lays its line and speed plan; spreading a 40-car green over a few steps keeps that off one frame on a phone.")]
    public int maxAssignPerStep = 8;
    [Tooltip("Most sideways offset (m) a car is handed its lane with; further out than this and it starts this far from the lane line.")]
    public float maxHandoverOffset = 3.5f;
    [Tooltip("Sideways rate (m/s) a practice stint car's handover offset is eased off at (AIRacingBehaviour.maxLateralSpeed).")]
    public float handoverEaseRate = 1.6f;

    // The cars this director gave a lane to (and so may take it back from).
    readonly HashSet<SplineDriver> _given = new();
    readonly List<SplineDriver> _release = new();
    // Practice stint cars still easing their handover offset (the racing brain leaves lateralOffset alone for them).
    readonly HashSet<SplineDriver> _easing = new();

    // The track the lanes below were built for, and the lanes themselves on its centreline samples.
    TrackBuilder _lanesFor;
    TrackBuilder _checked;
    float[] _sampleDistance, _inside, _outside;
    float _lapLength, _insideSide;
    SplineDriver _safetyCarDriver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("OvalLaneDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<OvalLaneDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void FixedUpdate()
    {
        var drivers = RaceField.Drivers;
        TrackBuilder track = null;
        for (int i = 0; i < drivers.Count && track == null; i++)
            if (drivers[i] != null && drivers[i].isActiveAndEnabled) track = drivers[i].track;
        RefreshLanes(track);

        _given.RemoveWhere(d => d == null);
        EaseHandovers();
        bool live = Enabled && RaceStart.IsGreen && _lanesFor != null && !TrackConditions.AiPackRacing;
        if (!live)
        {
            ReleaseAll();
            return;
        }

        int budget = Mathf.Max(1, maxAssignPerStep);
        for (int i = 0; i < drivers.Count && budget > 0; i++)
        {
            var d = drivers[i];
            if (!Eligible(d) || d.ovalLane != OvalLanes.Lane.None) continue;
            var lane = Choose(d, drivers);
            if (lane == OvalLanes.Lane.None) continue;
            d.ovalLane = lane;
            d.RelayLaneNow();   // lay the lane now, to hand it over from where the car is relative to its line
            // Off the line it is meant to be on now: its start lane while it holds one (SplineDriver.startLanes).
            float off = d.CarLateral - d.LaneTargetLateralAt(d.CentreDistanceOnTrack);
            d.lateralOffset = Mathf.Clamp(off, -maxHandoverOffset, maxHandoverOffset);
            _given.Add(d);
            if (d.TryGetComponent<PracticeAIStint>(out _)) _easing.Add(d);
            budget--;
        }
    }

    void EaseHandovers()
    {
        if (_easing.Count == 0) return;
        _release.Clear();
        float step = handoverEaseRate * Time.fixedDeltaTime;
        foreach (var d in _easing)
        {
            if (d == null) { _release.Add(d); continue; }
            if (d.IsOnPit) continue;
            d.lateralOffset = Mathf.MoveTowards(d.lateralOffset, 0f, step);
            if (Mathf.Abs(d.lateralOffset) < 0.001f) _release.Add(d);
        }
        for (int i = 0; i < _release.Count; i++) _easing.Remove(_release[i]);
    }

    // Hands back every lane this director gave, once the car is not mid lane change (a move lasts ~3 s and is
    // left to land: dropping the lane mid-blend would snap its line sideways).
    void ReleaseAll()
    {
        if (_given.Count == 0) return;
        _release.Clear();
        foreach (var d in _given)
            if (d == null || !d.LaneChanging) _release.Add(d);
        for (int i = 0; i < _release.Count; i++)
        {
            var d = _release[i];
            _given.Remove(d);
            _easing.Remove(d);
            if (d != null) d.ovalLane = OvalLanes.Lane.None;
        }
    }

    bool Eligible(SplineDriver d)
    {
        if (d == null || !d.isActiveAndEnabled || d.track != _lanesFor || d.IsOnPit || d.IsHeldStill) return false;
        if (d.TrackLength <= 0f || Mathf.Abs(d.TrackLength - _lapLength) > 1f) return false;
        if (d == _safetyCarDriver) return false;
        if (_safetyCarDriver == null)
        {
            // TryGetComponent: a failed GetComponent allocates its null-error message in the editor, and this runs
            // for every eligible car every step while no safety car has been found (~0.5 KB each).
            if (d.TryGetComponent<SafetyCar>(out _)) { _safetyCarDriver = d; return false; }
        }
        return true;
    }

    OvalLanes.Lane Choose(SplineDriver self, IReadOnlyList<SplineDriver> drivers)
    {
        // Off the pace lap's start lanes: low takes the inside lane, high the outside, and the car keeps to its start
        // lane on top of it until up to speed.
        if (self.StartLaneSide < 0) return OvalLanes.Lane.Inside;
        if (self.StartLaneSide > 0) return OvalLanes.Lane.Outside;
        float myD = self.CentreDistanceOnTrack, myLat = self.CarLateral;
        float myIn = myLat * _insideSide;   // how far toward the infield
        bool besideInner = false, besideOuter = false;
        OvalLanes.Lane taken = OvalLanes.Lane.None;
        for (int i = 0; i < drivers.Count; i++)
        {
            var o = drivers[i];
            if (o == null || o == self || !o.isActiveAndEnabled || o.IsOnPit || o == _safetyCarDriver) continue;
            float along = Mathf.Abs(Mathf.Repeat(o.CentreDistanceOnTrack - myD + _lapLength * 0.5f, _lapLength) - _lapLength * 0.5f);
            if (along > alongsideMetres || Mathf.Abs(o.CarLateral - myLat) > alongsideLateral) continue;
            if (o.LaneFollowing && (o.ovalLane == OvalLanes.Lane.Inside || o.ovalLane == OvalLanes.Lane.Outside)) taken = o.ovalLane;
            else if (o.CarLateral * _insideSide > myIn) besideInner = true;
            else besideOuter = true;
        }
        if (taken == OvalLanes.Lane.Inside) return OvalLanes.Lane.Outside;
        if (taken == OvalLanes.Lane.Outside) return OvalLanes.Lane.Inside;
        if (besideInner != besideOuter) return besideInner ? OvalLanes.Lane.Outside : OvalLanes.Lane.Inside;
        // Single file (idealWhenSingleFile): the ideal line. A car alongside one on the ideal is split from it on
        // the brain's next step (SplineDriver.StepPairLanes), as any pair drawing level is.
        if (self.idealWhenSingleFile && self.lanePassing) return OvalLanes.Lane.Ideal;

        float inLat = LaneAt(_inside, myD), outLat = LaneAt(_outside, myD);
        return Mathf.Abs(myLat - inLat) <= Mathf.Abs(myLat - outLat) ? OvalLanes.Lane.Inside : OvalLanes.Lane.Outside;
    }

    void RefreshLanes(TrackBuilder track)
    {
        if (track == _checked) return;
        _checked = track;
        _lanesFor = null;
        _given.Clear();
        _easing.Clear();
        _safetyCarDriver = null;
        if (track == null) return;
        var samples = track.SampleCenterline();
        if (!track.TryGetOvalLanes(samples, out _inside, out _outside)) return;
        int n = samples.Count;
        _sampleDistance = new float[n];
        var tangents = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            _sampleDistance[i] = samples[i].distance;
            tangents[i] = samples[i].tangent;
        }
        _lapLength = _sampleDistance[n - 1];
        _insideSide = OvalLanes.InsideSide(tangents);
        _lanesFor = track;
    }

    float LaneAt(float[] lane, float distance)
    {
        int n = _sampleDistance.Length;
        distance = Mathf.Repeat(distance, _lapLength);
        int hi = System.Array.BinarySearch(_sampleDistance, distance);
        if (hi >= 0) return lane[hi];
        hi = ~hi;
        if (hi <= 0) return lane[0];
        if (hi >= n) return lane[n - 1];
        int lo = hi - 1;
        float span = _sampleDistance[hi] - _sampleDistance[lo];
        return span > 0f ? Mathf.Lerp(lane[lo], lane[hi], (distance - _sampleDistance[lo]) / span) : lane[lo];
    }
}
