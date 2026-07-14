using System.Collections.Generic;
using UnityEngine;

public class SplineDriver : MonoBehaviour, IVehicleSpeedReadout, ICollisionResponder, IFormationMember
{
    public float SpeedMps => speed;

    Vector2 _lastWorldRight = Vector2.right;
    Vector2 _lastWorldForward = Vector2.right;
    float _collisionLateral;
    float _collisionLongitudinal;
    bool _contactHold;

    public float Mass => vehicleInfo != null ? vehicleInfo.mass : 1500f;

    public void ApplyContact(Vector2 worldMtv, Vector2 contactPoint, float severity)
    {
        // Spline cars stay glued to the path; the lever arm can't spin them freely. contactPoint is ignored here.
        // Persist the push as offsets along BOTH spline axes (transform is rebuilt from the spline each frame).
        // The longitudinal offset is what unsticks cars overlapping nose-to-tail — the old code dropped it entirely.
        _collisionLateral = Mathf.Clamp(_collisionLateral + Vector2.Dot(worldMtv, _lastWorldRight), -8f, 8f);
        _collisionLongitudinal = Mathf.Clamp(_collisionLongitudinal + Vector2.Dot(worldMtv, _lastWorldForward), -8f, 8f);
        _contactHold = true; // hold the offsets through the next Place (skip one decay step) so contact separates

        // Scrub speed only by how head-on the hit is. Glancing scrapes barely slow; square-on loses more. Kept GENTLE
        // (contactSpeedScrub) — a hard scrub on a nose-to-tail tap in a tight pack made the tapped car lurch slow,
        // the car behind brake to match, and the wave amplified into a pile-up. A soft touch barely bleeds speed.
        Vector2 n = worldMtv.sqrMagnitude > 1e-6f ? worldMtv.normalized : Vector2.zero;
        float headOn = Mathf.Abs(Vector2.Dot(_lastWorldForward, n)); // 0 = parallel glance, 1 = square
        _currentMph *= Mathf.Clamp01(1f - severity * headOn * contactSpeedScrub);
    }

    // Spline cars are glued to the path, so momentum transfer doesn't apply — just separate and scrub speed
    // the same way a barrier contact does. (During racing the AI run the dynamic model, which gets the full
    // 2-body impulse via PlayerVehicleController.ApplyCarImpact instead.)
    public void ApplyCarImpact(Vector2 worldMtv, Vector2 contactPoint, Vector2 worldDeltaV, float severity)
        => ApplyContact(worldMtv, contactPoint, severity);

    public TrackBuilder track;
    public VehicleInfo vehicleInfo;
    [Tooltip("Scales the target speed at every point on track. <1 slows the car down, >1 speeds it up. Driver stats (qualifying/consistency) feed this.")]
    [Range(0.5f, 1.2f)]
    public float paceMultiplier = 1f;
    [Tooltip("Racing-line variant: -1 = leftmost line, 0 = ideal, +1 = rightmost line. Anything in between blends. Used as seed for the smoothed line.")]
    [Range(-1f, 1f)]
    public float lineFactor = 0f;

    [Header("Racing Line Smoothing")]
    [Tooltip("How many Gauss-Seidel passes to relax the line toward minimum curvature. 0 = follow authored ideal exactly (rigid). 30-80 = realistic smoothed line.")]
    [Range(0, 200)]
    public int smoothingIterations = 60;
    [Tooltip("Per-pass relaxation factor. 0 = no movement, 1 = full averaging. ~0.3 is stable.")]
    [Range(0f, 1f)]
    public float smoothingRelaxation = 0.3f;
    [Tooltip("Current speed in metres per second. Driven by the simulation when vehicleInfo is assigned; otherwise treated as a constant.")]
    public float speed = 40f;
    [Tooltip("Distance along the spline to spawn at, in metres.")]
    public float startDistance = 0f;
    [Tooltip("Lateral offset from centerline, in metres. Applied on top of any racing-line offset.")]
    public float lateralOffset = 0f;
    [Tooltip("Loop back to the start when the lap completes.")]
    public bool loop = true;
    [Tooltip("Sprite faces +Y by default. Set false if your sprite faces +X.")]
    public bool spriteFacesUp = true;
    [Tooltip("Extra rotation applied to the sprite, in degrees.")]
    public float angleOffsetDeg = 0f;
    [Tooltip("Drive on the pit lane spline instead of the main spline.")]
    public bool usePitLane = false;

    [Header("Pit Grid Spawn")]
    [Tooltip("If true, spawn car in pit lane at qualifyingPosition's pit box.")]
    public bool spawnInPit = false;
    [Tooltip("Grid index. 0 = pole (closest to pit exit). Used to place car in pit box.")]
    public int qualifyingPosition = 0;
    [Tooltip("Distance (m) between pit boxes along pit lane.")]
    public float pitBoxSpacing = 12f;
    [Tooltip("Distance (m) from end of pit lane to the pole-sitter's pit box.")]
    public float pitBoxExitGap = 20f;
    [Tooltip("How much of the pit-lane length the cars actually need to traverse before merging back onto track (sets the auto-exit threshold).")]
    [Range(0.5f, 1f)] public float pitExitThreshold = 0.98f;
    [Tooltip("When on the pit lane and past pitExitThreshold, hop back onto the main spline. Disable for a safety car that drives INTO the pit to park.")]
    public bool autoPitExit = true;
    [Tooltip("On an AI takeover (EngageFromCurrentPose), the car engages on the PIT lane instead of the racing line if it's within this many metres of the pit centerline. Stops a handover in the pit driving through the pit wall onto the track.")]
    public float pitEngageLateralMax = 6f;
    [Tooltip("After rejoining the main spline at pit exit, how fast (m/s) the car eases from its pit-exit line onto the racing line. Stops the merge being an instant sideways pop.")]
    public float pitMergeEaseSpeed = 2.5f;
    [Tooltip("How fast (deg/sec) the car's heading eases from its pit-exit facing onto the main-spline tangent. The pit exit isn't geometrically locked to the main spline, so without this the heading SNAPS on rejoin (a visible rotation pop). Lower = longer, smoother turn-in.")]
    public float pitMergeHeadingEaseDegPerSec = 25f;

    [Header("Formation Start")]
    [Tooltip("Hold this car perfectly still (parked in its box / at pit exit) while RaceStart.Current is PreGrid. Released automatically once the phase advances to Formation.")]
    public bool freezeUntilFormation = false;
    [Tooltip("While on the pit lane, hold the car stopped (a service stop). Set by PitStopController at the pit box.")]
    public bool pitStopHold = false;
    [Tooltip("Distance (m) along the pit lane to hard-park at (e.g. the safety car parking near the entrance). -1 = no park; the car drives the pit normally. Once reached the car is pinned here and stopped, regardless of decel/vehicleInfo.")]
    public float pitParkDistance = -1f;
    [Tooltip("Hard-park pin, independent of the race phase (practice cars waiting in their box). Zero speed, transform pinned to the commanded point until cleared.")]
    public bool parkedHold = false;

    public bool IsOnPit => _onPit;
    public float PitProgress01 => (_onPit && _pitLength > 0f) ? Mathf.Clamp01(_distance / _pitLength) : 0f;
    public float PitLength => _pitLength;

    [Header("Cornering Feel")]
    [Tooltip("Generous temporal smoothing (0..0.97) applied to the racing-line lateral AND the turn-in yaw, so the car flows through corner entry/exit instead of the rear axle snapping at segment boundaries. Higher = smoother but slightly rounds/lags the authored line. Per-FixedUpdate Lerp weight on the OLD value.")]
    [Range(0f, 0.97f)] public float cornerSmoothing = 0.88f;
    [Tooltip("Lean angle (deg) per metre/sec of lateral motion. Positive offset rate = moving right = leans right. Negate to flip.")]
    public float leanIntoTurns = 4f;
    [Tooltip("Smoothing for the lean angle. Lower = snappier, higher = floatier. 0 disables smoothing.")]
    [Range(0f, 0.95f)]
    public float leanSmoothing = 0.8f;
    [Tooltip("Hard cap on the lean angle (deg). Without it, a fast lateral move (chicane, weave, line change) blows the lean up to 20°+ and the car renders crabbed — pointing diagonally instead of along its direction of travel. Keep small (a few degrees) so cornering still reads as a subtle lean.")]
    [Range(0f, 20f)]
    public float maxLeanDeg = 5f;
    [Tooltip("Fraction of speed bled off on a perfectly square contact (scaled by severity and how head-on the hit is). Keep small for the parade — a hard scrub on a nose-to-tail tap makes the field concertina into a pile-up.")]
    [Range(0f, 1f)] public float contactSpeedScrub = 0.12f;
    const float rearAxleToCenter = -2.4f;

    public float CurrentMph => _currentMph;
    /// What the speed profile wants here, before AI follow-caps. Lets behaviours ask "could I be going faster?"
    public float DesiredMph { get; private set; }
    public float DistanceOnTrack => _mainLength > 0f ? ((_distance % _mainLength) + _mainLength) % _mainLength : _distance;
    public float LateralOnTrack => _prevLateral;
    public float TrackLength => _mainLength;

    // IFormationMember — lets the formation lap line cars up in grid order (the safety car overrides its grid via
    // qualifyingPosition = FormationOrder.SafetyCarGrid so it leads).
    public int GridPosition => qualifyingPosition;
    float IFormationMember.TrackDistance => DistanceOnTrack;
    float IFormationMember.TrackLateral => LateralOnTrack;
    public float SpeedMph => _currentMph;
    public bool FormationActive => isActiveAndEnabled && _mainLength > 0f;

    // Commanded path state (consumed by BicycleDynamics).
    public Vector2 CommandedLocalPos { get; private set; }
    public float CommandedHeadingDeg { get; private set; }
    public float CommandedSpeedMps { get; private set; }
    public bool externalMotionController = false; // true when BicycleDynamics owns the transform
    [HideInInspector] public float externalActualSpeedMps; // actual car speed fed back by the input provider
    public int CurrentSegmentIndex() => _segmentStartDistance != null ? SegmentIndexAt(_distance) : -1;

    public enum CornerPhase { Straight, Approach, Entry, Apex, Exit, PostExit }
    public CornerPhase CurrentPhase { get; private set; }

    [Header("AI Inputs (driven by AIRacingBehaviour)")]
    [Tooltip("Additive lateral offset applied each frame. Used by AI for overtaking / side-repulsion.")]
    public float tacticalLateralOffset = 0f;
    [Tooltip("Hard speed cap (mph) layered on top of the speed profile. float.MaxValue = no cap.")]
    public float aiMaxSpeedMph = float.MaxValue;
    [Tooltip("Additive speed bonus (mph). Used for drafting/slipstream.")]
    public float aiSpeedBoostMph = 0f;
    [Tooltip("When > 0, GUARANTEES at least this braking rate (mph/sec) regardless of the (possibly weak) decel curve. Set per-frame by the formation/avoidance AI so an emergency slow can actually land. 0 = use the curve.")]
    [HideInInspector] public float aiMinDecelMphPerSec = 0f;
    [Tooltip("Default deceleration used when the vehicle's decel curve is unauthored, in m/s².")]
    public float fallbackDecel = 10f;
    [Tooltip("Default acceleration used when the vehicle's accel curve is unauthored, in m/s².")]
    public float fallbackAccel = 5f;
    [Tooltip("Default flat-corner speed used when the vehicle's cornering curve is unauthored, in mph.")]
    public float fallbackCornerMph = 110f;
    [Tooltip("Scales corner target speeds. Targets are computed from the driven line's real curvature and the same grip the physics uses, so 1.0 = the theoretical limit; keep slightly below 1 for margin, >1 commands past the grip ceiling (physics saturation caps what actually happens).")]
    [Range(0.6f, 2f)] public float cornerSpeedScale = 0.95f;

    const float MphToMps = 1f / 2.237f;
    const float MpsToMph = 2.237f;

    List<TrackBuilder.Sample> _mainSamples;
    List<TrackBuilder.Sample> _pitSamples;
    List<TrackInfoV2.RacingLineAnchor> _anchors;
    float[] _segmentTargetMph;
    float[] _segmentStartDistance;
    float[] _speedProfile;
    float[] _curvatureProfile;  // |curvature| (1/m) of the DRIVEN line (centerline + smoothed lateral), per main sample
    float _bakedALatMaxMps2;    // lateral-accel ceiling (m/s²) the profile was baked against
    float[] _lateralProfile;
    float[] _leftBoundProfile;
    float[] _rightBoundProfile;
    float _mainLength;
    float _pitLength;
    float _distance;
    bool _onPit;
    float _prevLateral;
    bool _hasPrevLateral;
    float _currentLean;
    float _currentMph;
    float _lineLatSmoothed;   // low-passed racing-line lateral, so the rear axle doesn't snap at segment boundaries
    bool _hasLineSmoothed;    // false until the first smoothed sample / after a lane change, so it seeds from raw
    float _pathYawSmoothed;   // low-passed turn-in yaw (deg), so the body doesn't snap pointing onto its path
    float _mergeLatBias; // lateral carried across the pit→main merge, eased out so the rejoin isn't a sideways pop
    float _mergeHeadingBias; // heading (deg) carried across the merge, eased out so the rejoin isn't a rotation snap

    EngineGearbox _gearbox; // optional: shapes acceleration (gear torque + shift drive-cut) and feeds engine audio

    void Awake()
    {
        var vl = GetComponent<VehicleLogic>();
        if (vl != null) vl.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        _gearbox = GetComponent<EngineGearbox>();
    }

    void OnEnable() { RaceField.Register(this); FormationOrder.Register(this); }
    void OnDisable() { RaceField.Unregister(this); FormationOrder.Unregister(this); }

    bool _startSeeded; // set by EngageFromCurrentPose so Start() doesn't clobber the seeded distance

    // Take over a free-driven car (e.g. the player's) without a teleport: rebuild the spline data, latch onto the
    // nearest point to the car's current world position, and continue at its current speed. Enable the component
    // first, then call this. Used by DriveModeController when handing the player's car to the AI.
    public void EngageFromCurrentPose(float startMph)
    {
        if (_mainSamples == null || _mainSamples.Count < 2) Rebuild();
        externalMotionController = false; // kinematic: this component owns the transform

        // Latch onto whichever lane the car is physically ON. Taking over a car that's in the pit lane and forcing
        // it onto the main racing line would drive it straight through the pit wall (the crash). Use an ABSOLUTE
        // test — within pitEngageLateralMax of the pit centerline — rather than "nearer spline wins": the pit lane
        // runs alongside the main straight, so the main line just across the wall is often geometrically closer than
        // the pit centerline, which made the comparison pick the track and crash. Same test as PlayerPitService.
        // RejoinSplineContinuous then continues along the chosen lane; the normal auto-exit carries a pit-lane car
        // back onto the track at the pit-exit node.
        bool onPit = false;
        if (track != null && _pitSamples != null && _pitSamples.Count >= 2 && _pitLength > 0f)
        {
            Vector2 local = track.transform.InverseTransformPoint(transform.position);
            Vector2 pitPos = track.SamplePitAt(track.NearestPitDistance(transform.position), _pitSamples).position;
            // A car parked on the box lane (outside the pit ribbon) still counts as on the pit.
            float engageMax = track.HasPitBoxLane ? Mathf.Max(pitEngageLateralMax, track.PitBoxLaneOuterLateral + 0.5f) : pitEngageLateralMax;
            onPit = Vector2.Distance(local, pitPos) < engageMax;
        }
        usePitLane = onPit;
        _onPit = onPit;
        RejoinSplineContinuous(transform.position);
        float cap = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        _currentMph = Mathf.Clamp(startMph, 0f, cap);
        speed = _currentMph * MphToMps;
        _startSeeded = true;
    }

    void Start()
    {
        // Re-resolve the gearbox: GridSpawner adds it AFTER this SplineDriver, so the Awake fetch missed it.
        // Start runs after the whole spawn loop, so it exists now. (Prefab-built cars already had it in Awake.)
        if (_gearbox == null) _gearbox = GetComponent<EngineGearbox>();

        if (_startSeeded) return; // already engaged via EngageFromCurrentPose; keep its seeded distance
        Rebuild();
        if (spawnInPit && _pitLength > 0f)
        {
            usePitLane = true;
            _onPit = true;
            _distance = ComputePitBoxDistance(qualifyingPosition);
        }
        else
        {
            _distance = startDistance;
            _onPit = usePitLane;
        }
        // Pit-box race start is a standing start. The speed field only seeds constant-speed test rigs.
        _currentMph = spawnInPit ? 0f : speed * MpsToMph;
        float capMph = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        _currentMph = Mathf.Clamp(_currentMph, 0f, capMph);
        Place();
    }

    float ComputePitBoxDistance(int idx)
    {
        float d = _pitLength - pitBoxExitGap - idx * pitBoxSpacing;
        return Mathf.Max(0f, d);
    }

    // Place the car at startDistance immediately and write the transform, independent of Start() ordering.
    // The networked spawner calls this before NetworkObject.Spawn() so the car captures a valid on-track pose:
    // otherwise the AI input driver's first physics step (which runs before Start) seeds it at the origin.
    public void PlaceAtStartDistance()
    {
        if (track == null) return;
        if (_mainSamples == null || _mainSamples.Count < 2) Rebuild();

        if (spawnInPit && _pitLength > 0f)
        {
            usePitLane = true;
            _onPit = true;
            _distance = ComputePitBoxDistance(qualifyingPosition);
        }
        else
        {
            _distance = startDistance;
            _onPit = usePitLane;
        }
        _currentMph = spawnInPit ? 0f : speed * MpsToMph;
        Place(); // sets CommandedLocalPos/Heading/Speed; skips the transform under externalMotionController

        // Write the transform too (Place skips it when an external model owns motion), so the car physically
        // sits on its start spot and NetworkTransform replicates that pose to clients.
        Vector3 wp = track.transform.TransformPoint(new Vector3(CommandedLocalPos.x, CommandedLocalPos.y, 0f));
        transform.position = new Vector3(wp.x, wp.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? CommandedHeadingDeg - 90f : CommandedHeadingDeg) + angleOffsetDeg);
    }

    public void Rebuild()
    {
        if (track == null) return;
        _mainSamples = track.SampleCenterline();
        _mainLength = _mainSamples.Count > 0 ? _mainSamples[_mainSamples.Count - 1].distance : 0f;
        _pitSamples = track.SamplePitCenterline();
        _pitLength = _pitSamples.Count > 0 ? _pitSamples[_pitSamples.Count - 1].distance : 0f;
        _anchors = track.track != null ? track.track.BuildRacingLineAnchors() : null;
        PrecomputeSegmentSpeeds();
        // Lateral (the smoothed racing line) must exist before the speed profile: corner targets are computed
        // from the curvature of the line the car actually drives, not the raw centerline segments.
        BuildLateralProfile();
        BuildCurvatureProfile();
        BuildSpeedProfile();
    }

    void BuildLateralProfile()
    {
        if (_mainSamples == null || _mainSamples.Count == 0 || track == null || track.track == null || _anchors == null)
        {
            _lateralProfile = null;
            _leftBoundProfile = null;
            _rightBoundProfile = null;
            return;
        }

        int n = _mainSamples.Count;
        _lateralProfile = new float[n];
        _leftBoundProfile = new float[n];
        _rightBoundProfile = new float[n];

        for (int i = 0; i < n; i++)
        {
            float d = _mainSamples[i].distance;
            _lateralProfile[i] = track.track.GetLateralAt(d, lineFactor, _anchors, _mainLength);
            _leftBoundProfile[i] = track.track.GetLateralAt(d, -1f, _anchors, _mainLength);
            _rightBoundProfile[i] = track.track.GetLateralAt(d, +1f, _anchors, _mainLength);
        }

        // Min-curvature relaxation: each pass nudges every point toward the average of its neighbours, clamped to bounds.
        var tmp = new float[n];
        for (int p = 0; p < smoothingIterations; p++)
        {
            for (int i = 0; i < n; i++)
            {
                int prev = i == 0 ? (loop ? n - 1 : 0) : i - 1;
                int next = i == n - 1 ? (loop ? 0 : n - 1) : i + 1;
                float avg = 0.5f * (_lateralProfile[prev] + _lateralProfile[next]);
                float relaxed = Mathf.Lerp(_lateralProfile[i], avg, smoothingRelaxation);
                float lo = Mathf.Min(_leftBoundProfile[i], _rightBoundProfile[i]);
                float hi = Mathf.Max(_leftBoundProfile[i], _rightBoundProfile[i]);
                tmp[i] = Mathf.Clamp(relaxed, lo, hi);
            }
            (tmp, _lateralProfile) = (_lateralProfile, tmp);
        }
    }

    void BoundsAt(float distance, out float lo, out float hi)
    {
        lo = -100f; hi = 100f;
        if (_leftBoundProfile == null || _rightBoundProfile == null || _mainSamples == null) return;
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _leftBoundProfile.Length;
        int idxLo = 0;
        for (int i = 0; i < n; i++)
        {
            if (_mainSamples[i].distance <= distance) idxLo = i;
            else break;
        }
        int idxHi = (idxLo + 1) % n;
        float dLo = _mainSamples[idxLo].distance;
        float dHi = _mainSamples[idxHi].distance;
        if (idxHi <= idxLo) dHi += _mainLength;
        float denom = dHi - dLo;
        float t = denom > 0f ? Mathf.Clamp01((distance - dLo) / denom) : 0f;
        float left = Mathf.Lerp(_leftBoundProfile[idxLo], _leftBoundProfile[idxHi], t);
        float right = Mathf.Lerp(_rightBoundProfile[idxLo], _rightBoundProfile[idxHi], t);
        lo = Mathf.Min(left, right);
        hi = Mathf.Max(left, right);
    }

    float LateralAt(float distance)
    {
        if (_lateralProfile == null || _lateralProfile.Length == 0 || _mainSamples == null) return 0f;
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _lateralProfile.Length;
        int lo = 0;
        for (int i = 0; i < n; i++)
        {
            if (_mainSamples[i].distance <= distance) lo = i;
            else break;
        }
        int hi = (lo + 1) % n;
        float dLo = _mainSamples[lo].distance;
        float dHi = _mainSamples[hi].distance;
        if (hi <= lo) dHi += _mainLength;
        float denom = dHi - dLo;
        float t = denom > 0f ? Mathf.Clamp01((distance - dLo) / denom) : 0f;
        return Mathf.Lerp(_lateralProfile[lo], _lateralProfile[hi], t);
    }

    // paceMultiplier folded into the current speed profile. Targets are baked at this pace so the
    // relaxation passes bake matching (longer) braking distances; the live command applies only the
    // residual ratio between rebuilds. Above 1 the whole longitudinal envelope stretches with it —
    // without that, the pace knob never reached the straights (accel tapers to zero at the authored
    // top speed and both brain and car hard-clamp there, so only corner speeds ever scaled).
    float _profilePace = 1f;
    float _nextProfileRebuildTime;
    float ProfileStretch => Mathf.Max(1f, _profilePace);

    void PrecomputeSegmentSpeeds()
    {
        if (track == null || track.track == null || track.track.segments == null)
        {
            _segmentTargetMph = null;
            _segmentStartDistance = null;
            return;
        }

        _profilePace = Mathf.Max(0.1f, paceMultiplier);
        var segs = track.track.segments;
        _segmentTargetMph = new float[segs.Length];
        _segmentStartDistance = new float[segs.Length];
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            _segmentStartDistance[i] = cum;
            _segmentTargetMph[i] = ComputeTargetSpeedForSegment(segs[i]) * _profilePace;
            cum += segs[i].length;
        }

    }

    // Curvature (1/m) of the ACTUAL driven line — centerline + smoothed racing-line lateral — per main sample.
    // Segment radius alone lies about corner speed: the smoothed line opens the radius at the apex (that's the
    // point of a racing line) and the lateral swing through transitions adds curvature the segments never see.
    // Speed targets and braking distances are only physics-true against the line the car really drives.
    void BuildCurvatureProfile()
    {
        if (_mainSamples == null || _mainSamples.Count < 3) { _curvatureProfile = null; return; }
        int n = _mainSamples.Count;
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var s = _mainSamples[i];
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            float lat = (_lateralProfile != null && _lateralProfile.Length == n) ? _lateralProfile[i] : 0f;
            pts[i] = s.position + right * lat;
        }

        _curvatureProfile = new float[n];
        for (int i = 0; i < n; i++)
        {
            // Segment joins can emit near-coincident samples; step outward until the neighbours are far enough
            // apart for the three-point (Menger) estimate to be stable.
            int prev = StepDistinct(pts, i, -1, 0.5f);
            int next = StepDistinct(pts, i, +1, 0.5f);
            _curvatureProfile[i] = (prev == i || next == i || prev == next)
                ? 0f
                : MengerCurvature(pts[prev], pts[i], pts[next]);
        }

        // Light box smoothing: keeps apex curvature honest while killing sample-to-sample jitter.
        var tmp = new float[n];
        for (int p = 0; p < 2; p++)
        {
            for (int i = 0; i < n; i++)
            {
                int prev = i == 0 ? (loop ? n - 1 : 0) : i - 1;
                int next = i == n - 1 ? (loop ? 0 : n - 1) : i + 1;
                tmp[i] = (_curvatureProfile[prev] + _curvatureProfile[i] + _curvatureProfile[next]) / 3f;
            }
            (tmp, _curvatureProfile) = (_curvatureProfile, tmp);
        }
    }

    int StepDistinct(Vector2[] pts, int from, int dir, float minDist)
    {
        int n = pts.Length;
        int idx = from;
        for (int k = 0; k < 8; k++)
        {
            int cand = idx + dir;
            if (loop) cand = (cand + n) % n;
            else if (cand < 0 || cand >= n) return idx;
            idx = cand;
            if (Vector2.Distance(pts[from], pts[idx]) >= minDist) return idx;
        }
        return idx;
    }

    static float MengerCurvature(Vector2 a, Vector2 b, Vector2 c)
    {
        float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
        float denom = ab * bc * ca;
        if (denom < 1e-6f) return 0f;
        float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        return 2f * Mathf.Abs(cross) / denom;
    }

    // Lateral-accel ceiling (m/s²) matching what the dynamic model can actually generate: maxLateralG × the same
    // grip multipliers PlayerVehicleController folds into its friction circle (global+AI conditions, tyre state).
    float GripLateralAccelMps2()
    {
        float gripMul = TrackConditions.AiEffective;
        var tireModel = GetComponent<TireModel>();
        if (tireModel != null) gripMul *= tireModel.OverallGrip;
        else { var tire = GetComponent<TireState>(); if (tire != null) gripMul *= tire.GripMultiplier; }
        float g = (vehicleInfo != null && vehicleInfo.maxLateralG > 0.01f) ? vehicleInfo.maxLateralG : 1.8f;
        return g * Mathf.Max(0.05f, gripMul) * 9.81f;
    }

    void BuildSpeedProfile()
    {
        if (_mainSamples == null || _mainSamples.Count == 0 || _segmentTargetMph == null)
        {
            _speedProfile = null;
            return;
        }

        int n = _mainSamples.Count;
        _speedProfile = new float[n];
        _bakedALatMaxMps2 = GripLateralAccelMps2();
        for (int i = 0; i < n; i++)
        {
            int segIdx = SegmentIndexAt(_mainSamples[i].distance);
            float kappa = _curvatureProfile != null ? _curvatureProfile[i] : 0f;
            // Curvature-true target where the line actually bends; the old segment target (top speed or the
            // authored cap) elsewhere and as the fallback when no curvature data exists.
            _speedProfile[i] = kappa > 1e-4f
                ? ComputeTargetSpeedForCurvature(kappa, segIdx) * _profilePace
                : _segmentTargetMph[segIdx];
        }

        // Two wrap-aware passes per direction so values settle across the loop seam.
        int passes = loop ? 2 : 1;
        for (int p = 0; p < passes; p++)
        {
            for (int i = 1; i < n; i++) ApplyAccelLimit(i, i - 1);
            if (loop) ApplyAccelLimit(0, n - 1);
        }
        for (int p = 0; p < passes; p++)
        {
            for (int i = n - 2; i >= 0; i--) ApplyBrakeLimit(i, i + 1);
            if (loop) ApplyBrakeLimit(n - 1, 0);
        }
    }

    // Corner target from the driven line's real radius: v = √(r · a_lat). Same μ the physics friction circle
    // uses, so with cornerSpeedScale ≤ 1 the car is guaranteed lateral headroom instead of being commanded into
    // saturation (which is what strews understeering cars across the track when grip/speed tuning changes).
    float ComputeTargetSpeedForCurvature(float kappa, int segIdx)
    {
        float topMph = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        float radius = 1f / kappa;

        float baseMph;
        if (vehicleInfo != null && vehicleInfo.corneringSpeedCurve != null && vehicleInfo.corneringSpeedCurve.length > 0)
        {
            // The curve encodes corner speed by radius at nominal (1.0) grip; corner speed scales with √μ.
            baseMph = vehicleInfo.corneringSpeedCurve.Evaluate(radius)
                      * Mathf.Sqrt(Mathf.Max(TrackConditions.AiEffective, 0.05f));
        }
        else
        {
            baseMph = Mathf.Sqrt(radius * _bakedALatMaxMps2) * MpsToMph;
        }

        float bankingMph = 0f;
        float capMph = topMph;
        var segs = (track != null && track.track != null) ? track.track.segments : null;
        if (segs != null && segIdx >= 0 && segIdx < segs.Length)
        {
            var seg = segs[segIdx];
            if (vehicleInfo != null) bankingMph = seg.banking * vehicleInfo.bankingMphPerDegree;
            if (seg.maxSpeed > 0) capMph = Mathf.Min(capMph, seg.maxSpeed);
        }
        return Mathf.Clamp((baseMph + bankingMph) * cornerSpeedScale, 5f, capMph);
    }

    // Friction circle: longitudinal authority shrinks with the lateral load already spent at this point of the
    // line — a_long = a_max · √(1 − (a_lat/a_latMax)²). This is what pushes braking zones back up the straight
    // (brake BEFORE turn-in) instead of assuming full brake force mid-corner, where the real car's rear lets go.
    float FrictionCircleHeadroom(int idx, float vMps)
    {
        if (_curvatureProfile == null || idx >= _curvatureProfile.Length || _bakedALatMaxMps2 <= 0.01f) return 1f;
        float frac = vMps * vMps * _curvatureProfile[idx] / _bakedALatMaxMps2;
        // Floor keeps the relaxation passes progressing even where targets sit at the lateral limit.
        return Mathf.Max(0.15f, Mathf.Sqrt(Mathf.Clamp01(1f - frac * frac)));
    }

    void ApplyAccelLimit(int i, int prev)
    {
        float d = _mainSamples[i].distance - _mainSamples[prev].distance;
        if (d < 0f) d += _mainLength;
        if (d <= 0f) return;
        float vPrev = _speedProfile[prev] * MphToMps;
        float a = SampleAccel(_speedProfile[prev] / ProfileStretch) * ProfileStretch;
        a *= FrictionCircleHeadroom(prev, vPrev);
        float vMaxMps = Mathf.Sqrt(vPrev * vPrev + 2f * a * d);
        float vMaxMph = vMaxMps * MpsToMph;
        if (vMaxMph < _speedProfile[i]) _speedProfile[i] = vMaxMph;
    }

    void ApplyBrakeLimit(int i, int next)
    {
        float d = _mainSamples[next].distance - _mainSamples[i].distance;
        if (d < 0f) d += _mainLength;
        if (d <= 0f) return;
        float vNext = _speedProfile[next] * MphToMps;
        float decel = SampleDecel(_speedProfile[i] / ProfileStretch) * ProfileStretch;
        decel *= FrictionCircleHeadroom(i, _speedProfile[i] * MphToMps);
        float vMaxMps = Mathf.Sqrt(vNext * vNext + 2f * decel * d);
        float vMaxMph = vMaxMps * MpsToMph;
        if (vMaxMph < _speedProfile[i]) _speedProfile[i] = vMaxMph;
    }

    float ProfileAt(float distance)
    {
        if (_speedProfile == null || _speedProfile.Length == 0 || _mainSamples == null) return 0f;
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _speedProfile.Length;
        int lo = 0;
        for (int i = 0; i < n; i++)
        {
            if (_mainSamples[i].distance <= distance) lo = i;
            else break;
        }
        int hi = (lo + 1) % n;
        float dLo = _mainSamples[lo].distance;
        float dHi = _mainSamples[hi].distance;
        if (hi <= lo) dHi += _mainLength;
        float denom = dHi - dLo;
        float t = denom > 0f ? Mathf.Clamp01((distance - dLo) / denom) : 0f;
        return Mathf.Lerp(_speedProfile[lo], _speedProfile[hi], t);
    }

    float ComputeTargetSpeedForSegment(TrackInfoV2.TrackSegment seg)
    {
        float topMph = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        if (seg.maxSpeed > 0) return Mathf.Min(topMph, seg.maxSpeed);
        if (seg.type == TrackInfoV2.SegmentType.Straight || Mathf.Approximately(seg.angle, 0f)) return topMph;

        float radius = seg.length / Mathf.Max(Mathf.Abs(seg.angle) * Mathf.Deg2Rad, 1e-4f);
        float baseMph;
        if (vehicleInfo != null && vehicleInfo.corneringSpeedCurve != null && vehicleInfo.corneringSpeedCurve.length > 0)
        {
            // The curve encodes corner speed at nominal (1.0) grip. Corner speed scales with √μ, so fold the
            // AI's effective grip in (global boost × AI-only bonus) — otherwise the AI corner at raw curve
            // speeds while the physics enjoy the multiplied grip and the targets undersell what the car can do.
            baseMph = vehicleInfo.corneringSpeedCurve.Evaluate(radius)
                      * Mathf.Sqrt(Mathf.Max(TrackConditions.AiEffective, 0.05f));
        }
        else if (vehicleInfo != null && vehicleInfo.maxLateralG > 0.01f)
        {
            // v = sqrt(r * g * mu_effective). mu_effective = base mu × track conditions (incl. AI bonus) × tire wear grip.
            float gripMul = TrackConditions.AiEffective;
            var tireModel = GetComponent<TireModel>();
            if (tireModel != null) gripMul *= tireModel.OverallGrip;
            else { var tire = GetComponent<TireState>(); if (tire != null) gripMul *= tire.GripMultiplier; }
            float aLatMps2 = vehicleInfo.maxLateralG * Mathf.Max(0.05f, gripMul) * 9.81f;
            float vMps = Mathf.Sqrt(radius * aLatMps2);
            baseMph = vMps * MpsToMph;
        }
        else
        {
            baseMph = fallbackCornerMph;
        }
        float bankingMph = (vehicleInfo != null) ? seg.banking * vehicleInfo.bankingMphPerDegree : 0f;
        return Mathf.Clamp((baseMph + bankingMph) * cornerSpeedScale, 5f, topMph);
    }

    void UpdateCornerPhase()
    {
        if (track == null || track.track == null || track.track.segments == null || _segmentStartDistance == null)
        {
            CurrentPhase = CornerPhase.Straight;
            return;
        }
        var segs = track.track.segments;
        float d = DistanceOnTrack;
        int idx = SegmentIndexAt(d);
        if (idx < 0 || idx >= segs.Length) { CurrentPhase = CornerPhase.Straight; return; }
        var seg = segs[idx];
        if (seg.type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(seg.angle) > 0.5f)
        {
            float into = d - _segmentStartDistance[idx];
            if (into < 0f) into += _mainLength;
            float t = seg.length > 0.01f ? into / seg.length : 0f;
            if (t < 0.3f) CurrentPhase = CornerPhase.Entry;
            else if (t < 0.6f) CurrentPhase = CornerPhase.Apex;
            else CurrentPhase = CornerPhase.Exit;
            return;
        }

        // Straight: classify by proximity of next turn / recent exit.
        float distToNextTurn = float.MaxValue;
        for (int k = 1; k <= segs.Length; k++)
        {
            int n = (idx + k) % segs.Length;
            if (segs[n].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[n].angle) > 0.5f)
            {
                float gap = _segmentStartDistance[n] - d;
                if (gap < 0f) gap += _mainLength;
                distToNextTurn = gap;
                break;
            }
        }
        float prevExitDist = float.MaxValue;
        for (int k = 1; k <= segs.Length; k++)
        {
            int p = (idx - k + segs.Length) % segs.Length;
            if (segs[p].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[p].angle) > 0.5f)
            {
                float endP = _segmentStartDistance[p] + segs[p].length;
                float gap = d - endP;
                if (gap < 0f) gap += _mainLength;
                prevExitDist = gap;
                break;
            }
        }
        if (distToNextTurn < 60f) CurrentPhase = CornerPhase.Approach;
        else if (prevExitDist < 30f) CurrentPhase = CornerPhase.PostExit;
        else CurrentPhase = CornerPhase.Straight;
    }

    public int NextTurnSign(float scanDistance)
    {
        if (track == null || track.track == null || track.track.segments == null) return 0;
        if (_segmentStartDistance == null || _mainLength <= 0f) return 0;
        var segs = track.track.segments;
        float d = DistanceOnTrack;
        int curIdx = SegmentIndexAt(d);
        for (int k = 0; k < segs.Length; k++)
        {
            int idx = (curIdx + k) % segs.Length;
            float segStart = _segmentStartDistance[idx];
            float gap = segStart - d;
            if (gap < 0f) gap += _mainLength;
            if (gap > scanDistance) break;
            if (segs[idx].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[idx].angle) > 0.5f)
                return segs[idx].angle > 0f ? 1 : -1;
        }
        return 0;
    }

    void FixedUpdate()
    {
        if (_mainSamples == null || _mainSamples.Count < 2) return;

        // Pre-grid hold: sit parked in the pit box / at the pit exit until the formation lap begins.
        // Still Place() so the pose (and the commanded point feeding any dynamic model) tracks the box,
        // but advance nothing and command zero speed so dynamic-AI cars hold station too.
        if ((freezeUntilFormation && RaceStart.Current == RaceStart.Phase.PreGrid) || parkedHold)
        {
            speed = 0f;
            _currentMph = 0f;
            Place();
            // Pin the body to the box even under a dynamic motion controller. The controller's one-shot
            // seed can latch the wrong pose (its FixedUpdate may run before this Start places the car), and
            // a frozen car has no speed to recover. Writing the transform here guarantees it sits on its box;
            // a zero-velocity PlayerVehicleController preserves it, so they agree regardless of update order.
            if (externalMotionController && track != null)
            {
                Vector3 wp = track.transform.TransformPoint(new Vector3(CommandedLocalPos.x, CommandedLocalPos.y, 0f));
                transform.position = new Vector3(wp.x, wp.y, transform.position.z);
                transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? CommandedHeadingDeg - 90f : CommandedHeadingDeg) + angleOffsetDeg);
            }
            return;
        }

        if (usePitLane != _onPit)
        {
            // Lane switch — if entering pit, reset tires (service stop). Out-of-pit doesn't touch wear.
            if (usePitLane)
            {
                var tire = GetComponent<TireState>();
                if (tire != null) tire.PitReset();
            }
            _onPit = usePitLane;
            // Land on the new lane at the point nearest the car's current position (no teleport to lane start),
            // carrying its current lateral as a bias that eases out — same continuous merge as the pit exit.
            RejoinSplineContinuous(transform.position);
        }

        float length = _onPit ? _pitLength : _mainLength;
        if (length <= 0f) return;

        if (vehicleInfo != null)
        {
            float targetMph;
            if (_onPit)
            {
                float pitLimit = track != null && track.track != null ? track.track.pitSpeedLimit : 50f;
                // aiMaxSpeedMph applies on the lane too: PitStopController's pit ACC queues a car behind
                // slower/stopped lane traffic through it (every other writer leaves it at/above the limit).
                targetMph = pitStopHold ? 0f : Mathf.Min(pitLimit, aiMaxSpeedMph); // hold = a service stop at the box
            }
            else if (_speedProfile != null)
            {
                // The profile is baked at _profilePace; between (rate-limited) rebuilds the residual
                // ratio keeps the response live, so formation ramps and slider drags act immediately.
                float paceNow = Mathf.Max(0.1f, paceMultiplier);
                float ratio = paceNow / _profilePace;
                if ((ratio > 1.05f || ratio < 0.95f) && Time.time >= _nextProfileRebuildTime)
                {
                    _nextProfileRebuildTime = Time.time + 2f;
                    PrecomputeSegmentSpeeds();
                    BuildSpeedProfile();
                    ratio = 1f;
                }
                targetMph = ProfileAt(_distance) * ratio + aiSpeedBoostMph;
                DesiredMph = targetMph;
                if (aiMaxSpeedMph < targetMph) targetMph = aiMaxSpeedMph;
            }
            else
            {
                targetMph = 0f;
                DesiredMph = 0f;
            }
            UpdateSpeedToward(targetMph);
            speed = _currentMph * MphToMps;
        }

        UpdateCornerPhase();

        // Advance along the spline by the ACTUAL car speed when an external dynamic model drives it, so the
        // commanded path point stays with the car (a stalled/contacted car doesn't get left behind by its brain).
        // CommandedSpeedMps still carries the brain's DESIRED speed (set from `speed` in Place) for the provider.
        float advanceSpeed = externalMotionController ? externalActualSpeedMps : speed;
        _distance += advanceSpeed * Time.fixedDeltaTime;

        // Hard-park target on the pit lane (safety car parking near the entrance). Deterministic: pin the car the
        // instant it reaches the target distance, independent of braking distance or whether vehicleInfo exists —
        // otherwise a kinematic pace car with no decel curve rolls to the pit-spline end and parks out by the exit.
        if (_onPit && pitParkDistance >= 0f && _distance >= pitParkDistance)
        {
            _distance = pitParkDistance;
            _currentMph = 0f;
            speed = 0f;
            pitStopHold = true;
            Place();
            return;
        }

        if (autoPitExit && _onPit && _pitLength > 0f && _distance >= _pitLength * pitExitThreshold)
        {
            // Rejoin the main spline WITHOUT a positional jump. The authored pitExitDistance node need not line
            // up with the physical end of the pit lane, so teleporting there warps the car forward (and the
            // lateral discontinuity spikes the lean into a phantom 360). Instead continue from the main-spline
            // point nearest the car's current position, and carry its current lateral as a bias that eases out.
            _onPit = false;
            usePitLane = false;
            // Any pit-only lateral (the wall-side box lane a pit start parks on) must not leak onto the
            // track. Zeroed BEFORE the rejoin so the merge bias absorbs it and eases it out continuously.
            lateralOffset = 0f;
            length = _mainLength;
            RejoinSplineContinuous(transform.position);
        }

        if (loop && !_onPit)
        {
            while (_distance >= length) _distance -= length;
            while (_distance < 0f) _distance += length;
        }
        else
        {
            _distance = Mathf.Clamp(_distance, 0f, length);
        }
        Place();
    }

    void UpdateSpeedToward(float targetMph)
    {
        // Pace above 1 stretches the longitudinal envelope — accel/decel curves sampled at the
        // pace-normalised speed and scaled up, top-speed ceiling raised to match. Sub-1 pace keeps
        // the stock envelope (formation laps just chase lower targets with normal urgency).
        float stretch = Mathf.Max(1f, paceMultiplier);
        float topMph = (vehicleInfo != null ? vehicleInfo.topSpeed : 200f) * stretch;
        if (_currentMph < targetMph)
        {
            float accel = SampleAccel(_currentMph / stretch) * stretch;
            // Gearbox shapes the live accel (gear torque curve + brief drive-cut on a shift). It averages ~1
            // so overall pace is preserved; the static speed profile is left untouched (built without it).
            if (_gearbox != null) accel *= _gearbox.AccelMultiplier;
            _currentMph += accel * MpsToMph * Time.fixedDeltaTime;
            if (_currentMph > targetMph) _currentMph = targetMph;
        }
        else if (_currentMph > targetMph)
        {
            float decelMphPerSec = SampleDecel(_currentMph / stretch) * stretch * MpsToMph;
            // Avoidance can demand a firmer brake than the authored decel curve (which may be too weak to stop a
            // closing car before contact). aiMinDecelMphPerSec is the floor on braking authority for that frame.
            if (aiMinDecelMphPerSec > 0f) decelMphPerSec = Mathf.Max(decelMphPerSec, aiMinDecelMphPerSec);
            _currentMph -= decelMphPerSec * Time.fixedDeltaTime;
            if (_currentMph < targetMph) _currentMph = targetMph;
        }
        // aiSpeedBoostMph is aero (slipstream) — it legitimately carries a car past its stock flat-out
        // speed, so the ceiling rises with it. Without this the boost is a dead letter on the straights.
        _currentMph = Mathf.Clamp(_currentMph, 0f, topMph + Mathf.Max(0f, aiSpeedBoostMph));
    }

    float SampleAccel(float mph)
    {
        if (vehicleInfo == null || vehicleInfo.accelerationCurve == null || vehicleInfo.accelerationCurve.length == 0)
            return fallbackAccel;
        return Mathf.Max(0f, vehicleInfo.accelerationCurve.Evaluate(mph));
    }

    float SampleDecel(float mph)
    {
        if (vehicleInfo == null || vehicleInfo.decelerationCurve == null || vehicleInfo.decelerationCurve.length == 0)
            return fallbackDecel;
        return Mathf.Max(0.1f, vehicleInfo.decelerationCurve.Evaluate(mph));
    }

    int SegmentIndexAt(float distance)
    {
        if (_mainLength > 0f) distance = ((distance % _mainLength) + _mainLength) % _mainLength;
        int n = _segmentStartDistance.Length;
        int idx = 0;
        for (int i = 0; i < n; i++)
        {
            if (_segmentStartDistance[i] <= distance) idx = i;
            else break;
        }
        return idx;
    }

    // Lateral room (m) from the car's current line to each track edge. +lateral is right of travel, so
    // rightRoom is space to move right, leftRoom space to move left. Lets the AI pick the roomier passing side.
    public bool GetLateralRoom(out float leftRoom, out float rightRoom)
    {
        leftRoom = rightRoom = 0f;
        if (_leftBoundProfile == null || _rightBoundProfile == null || !_hasPrevLateral) return false;
        BoundsAt(_distance, out float lo, out float hi);
        float cur = _prevLateral;
        rightRoom = Mathf.Max(0f, hi - cur);
        leftRoom = Mathf.Max(0f, cur - lo);
        return true;
    }

    // Smallest turn radius (m) on the path within scanDistance ahead, including the current position.
    // float.MaxValue when the road ahead is straight. The AI input driver uses this to shorten its steering
    // lookahead on tight corners (aim short = turn in hard), keep it long on gradual ones (smooth), and to
    // cap its commanded speed to the live grip limit. Prefers the DRIVEN-line curvature profile (the radius
    // the car actually has to negotiate); falls back to raw segment radii when no profile exists.
    public float CurvatureRadiusAhead(float scanDistance)
    {
        if (!_onPit && _curvatureProfile != null && _mainSamples != null && _mainLength > 0f)
        {
            int n = _curvatureProfile.Length;
            float start = DistanceOnTrack;
            // Samples are distance-ordered: find the first at/after the car, then walk only the scan window.
            int i0 = 0;
            for (int i = 0; i < n; i++)
            {
                if (_mainSamples[i].distance <= start) i0 = i;
                else break;
            }
            float maxK = 0f;
            for (int k = 0; k < n; k++)
            {
                int i = (i0 + k) % n;
                float ahead = _mainSamples[i].distance - start;
                if (ahead < 0f) ahead += _mainLength;
                if (k > 0 && ahead > scanDistance) break;
                if (_curvatureProfile[i] > maxK) maxK = _curvatureProfile[i];
            }
            return maxK > 1e-4f ? 1f / maxK : float.MaxValue;
        }

        if (track == null || track.track == null || track.track.segments == null) return float.MaxValue;
        if (_segmentStartDistance == null || _mainLength <= 0f) return float.MaxValue;

        var segs = track.track.segments;
        float d = DistanceOnTrack;
        int curIdx = SegmentIndexAt(d);
        float minR = float.MaxValue;
        for (int k = 0; k < segs.Length; k++)
        {
            int idx = (curIdx + k) % segs.Length;
            if (k > 0)
            {
                float gap = _segmentStartDistance[idx] - d;
                if (gap < 0f) gap += _mainLength;
                if (gap > scanDistance) break;
            }
            var s = segs[idx];
            if (s.type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(s.angle) > 0.5f)
            {
                float r = s.length / Mathf.Max(Mathf.Abs(s.angle) * Mathf.Deg2Rad, 1e-4f);
                if (r < minR) minR = r;
            }
        }
        return minR;
    }

    // Land on the current lane (_onPit must already be set) at the point nearest worldNow — no teleport to the
    // lane start. Carries the car's current lateral as a bias that eases onto the lane line, and clears the lean
    // so the switch can't spike a phantom 360. Used by both the pit-entry and pit-exit transitions.
    void RejoinSplineContinuous(Vector3 worldNow)
    {
        if (track == null || _mainSamples == null || _mainSamples.Count < 2) { _distance = 0f; return; }
        Vector2 localNow = track.transform.InverseTransformPoint(worldNow);

        // worldNow is the car CENTRE, but _distance indexes the REAR-AXLE sample point — Place() builds the centre as
        // rearAxle + forward*rearAxleToCenter. Snapping _distance to the centre's nearest point makes the very next
        // Place() shove the centre back by |rearAxleToCenter| (~2.4m): the small backward jump seen on the pit-exit
        // merge. Resolve the rear-axle point from the arriving heading and snap _distance to THAT instead.
        Vector2 rearAxleLocal = localNow;
        if (_hasPrevLateral)
        {
            float hr = CommandedHeadingDeg * Mathf.Deg2Rad;
            rearAxleLocal = localNow - new Vector2(Mathf.Cos(hr), Mathf.Sin(hr)) * rearAxleToCenter;
        }
        Vector3 rearAxleWorld = track.transform.TransformPoint(new Vector3(rearAxleLocal.x, rearAxleLocal.y, 0f));

        TrackBuilder.Sample s;
        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            _distance = track.NearestPitDistance(rearAxleWorld);
            s = track.SamplePitAt(_distance, _pitSamples);
        }
        else
        {
            _distance = track.NearestCenterlineDistance(rearAxleWorld);
            s = track.SampleAt(_distance, _mainSamples);
        }

        float actualLat = Vector2.Dot(rearAxleLocal - s.position, s.normal);
        float baseLat = lateralOffset + tacticalLateralOffset + (_onPit ? 0f : LateralAt(_distance));
        _mergeLatBias = Mathf.Clamp(actualLat - baseLat, -20f, 20f);

        // Heading bias: the new lane's tangent vs the facing the car arrives with. CommandedHeadingDeg is in the
        // same track-local frame as the sample tangent (both atan2 of a local-space direction), so the delta is
        // the kink at the join. Eased to 0 in Place() so the car turns smoothly onto the new tangent instead of
        // snapping. Only when we have a valid arriving pose (_hasPrevLateral); a cold engage has no facing to keep.
        if (_hasPrevLateral)
        {
            float newHeadingDeg = Mathf.Atan2(s.tangent.y, s.tangent.x) * Mathf.Rad2Deg;
            _mergeHeadingBias = Mathf.Clamp(Mathf.DeltaAngle(newHeadingDeg, CommandedHeadingDeg), -90f, 90f);
        }
        else _mergeHeadingBias = 0f;

        _hasPrevLateral = false;
        _hasLineSmoothed = false; // reseed the line low-pass from the new lane's raw offset (no stale drag)
        _currentLean = 0f;
    }

    // Path point on the current spline a given distance AHEAD of the car, in track-local space. The AI input
    // driver steers toward this (pure-pursuit lookahead): aiming at the car's own position (CommandedLocalPos)
    // gives it nothing to track and makes it wander, so a lookahead point is what lets it hold the racing line.
    public Vector2 PathPointAhead(float aheadMeters)
    {
        if (_mainSamples == null || _mainSamples.Count < 2) return CommandedLocalPos;

        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            float dp = Mathf.Clamp(_distance + aheadMeters, 0f, _pitLength);
            var sp = track.SamplePitAt(dp, _pitSamples);
            Vector2 rp = new Vector2(sp.tangent.y, -sp.tangent.x);
            return sp.position + rp * (lateralOffset + tacticalLateralOffset);
        }

        float d = _distance + aheadMeters;
        if (_mainLength > 0f) d = ((d % _mainLength) + _mainLength) % _mainLength;
        var s = track.SampleAt(d, _mainSamples);
        Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
        float lat = LateralAt(d) + lateralOffset + tacticalLateralOffset;
        if (_leftBoundProfile != null && _rightBoundProfile != null)
        {
            BoundsAt(d, out float lo, out float hi);
            lat = Mathf.Clamp(lat, lo, hi);
        }
        return s.position + right * lat;
    }

    void Place()
    {
        // Decay collision offsets toward 0, but hold them one step after a fresh contact so cars actually separate.
        if (_contactHold) _contactHold = false;
        else
        {
            _collisionLateral = Mathf.MoveTowards(_collisionLateral, 0f, 6f * Time.fixedDeltaTime);
            _collisionLongitudinal = Mathf.MoveTowards(_collisionLongitudinal, 0f, 6f * Time.fixedDeltaTime);
        }
        // Ease the pit-merge lateral + heading biases out so the car drifts from its pit-exit line/facing onto the
        // racing line and tangent — a continuous turn-in instead of a sideways pop or a rotation snap.
        _mergeLatBias = Mathf.MoveTowards(_mergeLatBias, 0f, pitMergeEaseSpeed * Time.fixedDeltaTime);
        _mergeHeadingBias = Mathf.MoveTowards(_mergeHeadingBias, 0f, pitMergeHeadingEaseDegPerSec * Time.fixedDeltaTime);

        float placeDistance = _distance + _collisionLongitudinal;

        TrackBuilder.Sample sample;
        float length;
        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            sample = track.SamplePitAt(placeDistance, _pitSamples);
            length = _pitLength;
        }
        else
        {
            sample = track.SampleAt(placeDistance, _mainSamples);
            length = _mainLength;
        }

        // Racing-line lateral, temporally smoothed. The authored line steps between entry/apex/exit anchors, so the
        // raw value kinks at segment boundaries — the rear axle (built straight off this offset) snaps into and out
        // of turns. A generous low-pass rounds those kinks so the car flows through the corner. Reset on a lane
        // change (RejoinSplineContinuous clears _hasLineSmoothed) so it doesn't drag a stale value across the merge.
        float rawLineLateral = !_onPit ? LateralAt(placeDistance) : 0f;
        _lineLatSmoothed = _hasLineSmoothed ? Mathf.Lerp(rawLineLateral, _lineLatSmoothed, cornerSmoothing) : rawLineLateral;
        _hasLineSmoothed = true;
        float lineLateral = _lineLatSmoothed;
        float baseLateral = lateralOffset + tacticalLateralOffset + lineLateral + _collisionLateral;
        if (!_onPit && _leftBoundProfile != null && _rightBoundProfile != null)
        {
            BoundsAt(placeDistance, out float boundLo, out float boundHi);
            baseLateral = Mathf.Clamp(baseLateral, boundLo, boundHi);
        }
        // The pit-merge bias carries the car from its REAL pit-exit position onto the racing line. It must NOT be
        // bounds-clamped, or the car snaps (warps) onto the track edge the instant it leaves the pit lane — the
        // pit lane sits laterally outside the track, so clamping deletes the offset. Added after the clamp and
        // eased to zero (pitMergeEaseSpeed) so the car slides smoothly onto the line instead of teleporting.
        float totalLateral = baseLateral + _mergeLatBias;
        Vector2 right = new Vector2(sample.tangent.y, -sample.tangent.x);
        if (track != null)
        {
            _lastWorldRight = ((Vector2)track.transform.TransformVector(new Vector3(right.x, right.y, 0f))).normalized;
            _lastWorldForward = ((Vector2)track.transform.TransformVector(new Vector3(sample.tangent.x, sample.tangent.y, 0f))).normalized;
        }
        else { _lastWorldRight = right; _lastWorldForward = sample.tangent; }
        Vector2 rearAxle = sample.position + right * totalLateral;
        float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg;
        float lateralRate = (_hasPrevLateral && Time.fixedDeltaTime > 0f) ? (totalLateral - _prevLateral) / Time.fixedDeltaTime : 0f;
        // Geometric turn-in: on an offset racing line the car's true direction of travel is the centreline tangent
        // rotated by atan2(lateral velocity, forward velocity). Through a chicane the offset swings hard, so this
        // yaw is large and MUST come from the real kinematics. The old code faked turn-in with `lean` alone, which
        // is clamped to a few degrees (maxLeanDeg) — so the car under-rotated and rendered crabbed (pointing down
        // the centreline, not down its actual path). Speed-normalised so the angle is right at any pace; the floor
        // on forward speed guards against an atan2 blow-up at a standstill (where lateralRate is ~0 anyway).
        float forwardMps = Mathf.Max(Mathf.Abs(speed), 0.5f);
        float pathYawRaw = Mathf.Atan2(-lateralRate, forwardMps) * Mathf.Rad2Deg;
        // Smooth the turn-in yaw too (same generous filter), so the body doesn't snap as the car points onto its
        // path at corner entry/exit. Cold first frame takes the raw angle so there's no start-up swing.
        _pathYawSmoothed = _hasPrevLateral ? Mathf.Lerp(pathYawRaw, _pathYawSmoothed, cornerSmoothing) : pathYawRaw;
        // `lean` is now only a SMALL cosmetic bank layered on top of the correct heading (subtle lean into the
        // turn), clamped tight — the big turn-in comes from the smoothed path yaw above, not from this.
        float leanTarget = -lateralRate * leanIntoTurns;
        _currentLean = Mathf.Lerp(leanTarget, _currentLean, leanSmoothing);
        _currentLean = Mathf.Clamp(_currentLean, -maxLeanDeg, maxLeanDeg);
        _prevLateral = totalLateral;
        _hasPrevLateral = true;
        float carHeadingDeg = angleDeg + _pathYawSmoothed + _currentLean + _mergeHeadingBias;
        float carHeadingRad = carHeadingDeg * Mathf.Deg2Rad;
        Vector2 carForward = new Vector2(Mathf.Cos(carHeadingRad), Mathf.Sin(carHeadingRad));
        Vector2 finalPos = rearAxle + carForward * rearAxleToCenter;

        CommandedLocalPos = finalPos;
        CommandedHeadingDeg = carHeadingDeg;
        CommandedSpeedMps = speed;

        if (externalMotionController) return;

        Vector3 worldPos = track != null ? track.transform.TransformPoint(new Vector3(finalPos.x, finalPos.y, 0)) : new Vector3(finalPos.x, finalPos.y, 0);
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? carHeadingDeg - 90f : carHeadingDeg) + angleOffsetDeg);
    }
}
