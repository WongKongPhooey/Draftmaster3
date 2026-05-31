using System.Collections.Generic;
using UnityEngine;

public class SplineDriver : MonoBehaviour, IVehicleSpeedReadout, ICollisionResponder
{
    public float SpeedMps => speed;

    Vector2 _lastWorldRight = Vector2.right;
    float _collisionLateral;

    public void ApplyContact(Vector2 worldMtv, float severity)
    {
        // Project the push onto the car's lateral axis; transform is rebuilt from spline each frame,
        // so persist the correction as a decaying lateral offset rather than moving transform directly.
        float lateralDelta = Vector2.Dot(worldMtv, _lastWorldRight);
        _collisionLateral += lateralDelta;
        _collisionLateral = Mathf.Clamp(_collisionLateral, -8f, 8f);
        _currentMph *= Mathf.Clamp01(1f - severity * 0.4f);
    }

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

    [Header("Cornering Feel")]
    [Tooltip("Lean angle (deg) per metre/sec of lateral motion. Positive offset rate = moving right = leans right. Negate to flip.")]
    public float leanIntoTurns = 4f;
    [Tooltip("Smoothing for the lean angle. Lower = snappier, higher = floatier. 0 disables smoothing.")]
    [Range(0f, 0.95f)]
    public float leanSmoothing = 0.8f;
    const float rearAxleToCenter = -2.4f;

    public float CurrentMph => _currentMph;
    public float DistanceOnTrack => _mainLength > 0f ? ((_distance % _mainLength) + _mainLength) % _mainLength : _distance;
    public float LateralOnTrack => _prevLateral;
    public float TrackLength => _mainLength;

    // Commanded path state (consumed by BicycleDynamics).
    public Vector2 CommandedLocalPos { get; private set; }
    public float CommandedHeadingDeg { get; private set; }
    public float CommandedSpeedMps { get; private set; }
    public bool externalMotionController = false; // true when BicycleDynamics owns the transform
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
    [Tooltip("Default deceleration used when the vehicle's decel curve is unauthored, in m/s².")]
    public float fallbackDecel = 10f;
    [Tooltip("Default acceleration used when the vehicle's accel curve is unauthored, in m/s².")]
    public float fallbackAccel = 5f;
    [Tooltip("Default flat-corner speed used when the vehicle's cornering curve is unauthored, in mph.")]
    public float fallbackCornerMph = 110f;

    const float MphToMps = 1f / 2.237f;
    const float MpsToMph = 2.237f;

    List<TrackBuilder.Sample> _mainSamples;
    List<TrackBuilder.Sample> _pitSamples;
    List<TrackInfoV2.RacingLineAnchor> _anchors;
    float[] _segmentTargetMph;
    float[] _segmentStartDistance;
    float[] _speedProfile;
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

    void Awake()
    {
        var vl = GetComponent<VehicleLogic>();
        if (vl != null) vl.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void OnEnable() { RaceField.Register(this); }
    void OnDisable() { RaceField.Unregister(this); }

    void Start()
    {
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
        _currentMph = speed * MpsToMph;
        Place();
    }

    float ComputePitBoxDistance(int idx)
    {
        float d = _pitLength - pitBoxExitGap - idx * pitBoxSpacing;
        return Mathf.Max(0f, d);
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
        BuildSpeedProfile();
        BuildLateralProfile();
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
            _lateralProfile[i] = track.track.GetLateralAt(d, lineFactor, _anchors);
            _leftBoundProfile[i] = track.track.GetLateralAt(d, -1f, _anchors);
            _rightBoundProfile[i] = track.track.GetLateralAt(d, +1f, _anchors);
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

    void PrecomputeSegmentSpeeds()
    {
        if (track == null || track.track == null || track.track.segments == null)
        {
            _segmentTargetMph = null;
            _segmentStartDistance = null;
            return;
        }

        var segs = track.track.segments;
        _segmentTargetMph = new float[segs.Length];
        _segmentStartDistance = new float[segs.Length];
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            _segmentStartDistance[i] = cum;
            _segmentTargetMph[i] = ComputeTargetSpeedForSegment(segs[i]);
            cum += segs[i].length;
        }

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
        for (int i = 0; i < n; i++)
        {
            int segIdx = SegmentIndexAt(_mainSamples[i].distance);
            _speedProfile[i] = _segmentTargetMph[segIdx];
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

    void ApplyAccelLimit(int i, int prev)
    {
        float d = _mainSamples[i].distance - _mainSamples[prev].distance;
        if (d < 0f) d += _mainLength;
        if (d <= 0f) return;
        float vPrev = _speedProfile[prev] * MphToMps;
        float a = SampleAccel(_speedProfile[prev]);
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
        float decel = SampleDecel(_speedProfile[i]);
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
            baseMph = vehicleInfo.corneringSpeedCurve.Evaluate(radius);
        }
        else if (vehicleInfo != null && vehicleInfo.maxLateralG > 0.01f)
        {
            // v = sqrt(r * g * mu_effective). mu_effective = base mu × track conditions × tire wear grip.
            float gripMul = TrackConditions.GripMultiplier;
            var tire = GetComponent<TireState>();
            if (tire != null) gripMul *= tire.GripMultiplier;
            float aLatMps2 = vehicleInfo.maxLateralG * Mathf.Max(0.05f, gripMul) * 9.81f;
            float vMps = Mathf.Sqrt(radius * aLatMps2);
            baseMph = vMps * MpsToMph;
        }
        else
        {
            baseMph = fallbackCornerMph;
        }
        float bankingMph = (vehicleInfo != null) ? seg.banking * vehicleInfo.bankingMphPerDegree : 0f;
        return Mathf.Clamp(baseMph + bankingMph, 5f, topMph);
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

        if (usePitLane != _onPit)
        {
            // Lane switch — if entering pit, reset tires (service stop). Out-of-pit doesn't touch wear.
            if (usePitLane)
            {
                var tire = GetComponent<TireState>();
                if (tire != null) tire.PitReset();
            }
            _onPit = usePitLane;
            _distance = 0f;
        }

        float length = _onPit ? _pitLength : _mainLength;
        if (length <= 0f) return;

        if (vehicleInfo != null)
        {
            float targetMph;
            if (_onPit)
            {
                targetMph = track != null && track.track != null ? track.track.pitSpeedLimit : 50f;
            }
            else if (_speedProfile != null)
            {
                targetMph = ProfileAt(_distance) * paceMultiplier + aiSpeedBoostMph;
                if (aiMaxSpeedMph < targetMph) targetMph = aiMaxSpeedMph;
            }
            else
            {
                targetMph = 0f;
            }
            UpdateSpeedToward(targetMph);
            speed = _currentMph * MphToMps;
        }

        UpdateCornerPhase();

        _distance += speed * Time.fixedDeltaTime;

        if (_onPit && _pitLength > 0f && _distance >= _pitLength * pitExitThreshold)
        {
            // Hop from pit spline back to main spline at pit exit node.
            float overshoot = _distance - _pitLength;
            _onPit = false;
            usePitLane = false;
            _distance = (track != null && track.track != null ? track.track.pitExitDistance : 0f) + Mathf.Max(0f, overshoot);
            length = _mainLength;
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
        float topMph = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        if (_currentMph < targetMph)
        {
            float accel = SampleAccel(_currentMph);
            _currentMph += accel * MpsToMph * Time.fixedDeltaTime;
            if (_currentMph > targetMph) _currentMph = targetMph;
        }
        else if (_currentMph > targetMph)
        {
            float decel = SampleDecel(_currentMph);
            _currentMph -= decel * MpsToMph * Time.fixedDeltaTime;
            if (_currentMph < targetMph) _currentMph = targetMph;
        }
        _currentMph = Mathf.Clamp(_currentMph, 0f, topMph);
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

    void Place()
    {
        TrackBuilder.Sample sample;
        float length;
        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            sample = track.SamplePitAt(_distance, _pitSamples);
            length = _pitLength;
        }
        else
        {
            sample = track.SampleAt(_distance, _mainSamples);
            length = _mainLength;
        }

        _collisionLateral = Mathf.MoveTowards(_collisionLateral, 0f, 6f * Time.fixedDeltaTime);
        float lineLateral = !_onPit ? LateralAt(_distance) : 0f;
        float totalLateral = lateralOffset + tacticalLateralOffset + lineLateral + _collisionLateral;
        if (!_onPit && _leftBoundProfile != null && _rightBoundProfile != null)
        {
            BoundsAt(_distance, out float boundLo, out float boundHi);
            totalLateral = Mathf.Clamp(totalLateral, boundLo, boundHi);
        }
        Vector2 right = new Vector2(sample.tangent.y, -sample.tangent.x);
        if (track != null) _lastWorldRight = ((Vector2)track.transform.TransformVector(new Vector3(right.x, right.y, 0f))).normalized;
        else _lastWorldRight = right;
        Vector2 rearAxle = sample.position + right * totalLateral;
        float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg;
        float lateralRate = (_hasPrevLateral && Time.fixedDeltaTime > 0f) ? (totalLateral - _prevLateral) / Time.fixedDeltaTime : 0f;
        float leanTarget = -lateralRate * leanIntoTurns;
        _currentLean = Mathf.Lerp(leanTarget, _currentLean, leanSmoothing);
        _prevLateral = totalLateral;
        _hasPrevLateral = true;
        float carHeadingDeg = angleDeg + _currentLean;
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
