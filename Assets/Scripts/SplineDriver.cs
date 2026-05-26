using System.Collections.Generic;
using UnityEngine;

public class SplineDriver : MonoBehaviour
{
    public TrackBuilder track;
    public VehicleInfo vehicleInfo;
    [Tooltip("Scales the target speed at every point on track. <1 slows the car down, >1 speeds it up. Driver stats (qualifying/consistency) feed this.")]
    [Range(0.5f, 1.2f)]
    public float paceMultiplier = 1f;
    [Tooltip("Racing-line variant: -1 = leftmost line, 0 = ideal, +1 = rightmost line. Anything in between blends.")]
    [Range(-1f, 1f)]
    public float lineFactor = 0f;
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

    [Header("Cornering Feel")]
    [Tooltip("How strongly the car leans into turns. 0 = rigid, ~8 = subtle, 20+ = drifty arcade.")]
    public float leanIntoTurns = 10f;
    [Tooltip("Smoothing for the lean angle. Lower = snappier, higher = floatier. 0 disables smoothing.")]
    [Range(0f, 0.95f)]
    public float leanSmoothing = 0.8f;

    [Header("Anticipation")]
    [Tooltip("How far ahead (metres) to look for slower corners that require pre-braking.")]
    public float brakingLookahead = 400f;
    [Tooltip("If true, this driver prints its precomputed per-segment target speeds to the console on Rebuild. Useful for diagnosing why a car isn't braking for corners.")]
    public bool logSegmentSpeeds = false;

    public float CurrentTargetMph { get; private set; }

    [Header("Debug Live (read-only)")]
    [SerializeField] float _dbgCurrentMph;
    [SerializeField] float _dbgTargetMph;
    [SerializeField] int _dbgSegIdx;
    [SerializeField] float _dbgDistance;
    [SerializeField] float _dbgAccelMps2;
    [SerializeField] float _dbgDecelMps2;
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
    float _mainLength;
    float _pitLength;
    float _distance;
    bool _onPit;
    float _prevHeading;
    bool _hasPrevHeading;
    float _currentLean;
    float _currentMph;

    void Awake()
    {
        var vl = GetComponent<VehicleLogic>();
        if (vl != null) vl.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void Start()
    {
        Rebuild();
        _distance = startDistance;
        _currentMph = speed * MpsToMph;
        _onPit = usePitLane;
        Place();
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

        if (logSegmentSpeeds)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[SplineDriver] {name} segments (mainLength={_mainLength:F1}m, vehicle={(vehicleInfo != null ? vehicleInfo.name : "<null>")}, topSpeed={(vehicleInfo != null ? vehicleInfo.topSpeed : 0)}):");
            for (int i = 0; i < segs.Length; i++)
            {
                var s = segs[i];
                float radius = (s.type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(s.angle) > 1e-3f)
                    ? s.length / (Mathf.Abs(s.angle) * Mathf.Deg2Rad) : 0f;
                sb.AppendLine($"  [{i}] {s.type} len={s.length:F1}m ang={s.angle:F1}° bank={s.banking:F1}° r={radius:F0}m maxOverride={s.maxSpeed} → target={_segmentTargetMph[i]:F0}mph");
            }
            Debug.Log(sb.ToString());
        }
    }

    float ComputeTargetSpeedForSegment(TrackInfoV2.TrackSegment seg)
    {
        float topMph = vehicleInfo != null ? vehicleInfo.topSpeed : 200f;
        if (seg.maxSpeed > 0) return Mathf.Min(topMph, seg.maxSpeed);
        if (seg.type == TrackInfoV2.SegmentType.Straight || Mathf.Approximately(seg.angle, 0f)) return topMph;

        float radius = seg.length / Mathf.Max(Mathf.Abs(seg.angle) * Mathf.Deg2Rad, 1e-4f);
        float baseMph = (vehicleInfo != null && vehicleInfo.corneringSpeedCurve != null && vehicleInfo.corneringSpeedCurve.length > 0)
            ? vehicleInfo.corneringSpeedCurve.Evaluate(radius)
            : fallbackCornerMph;
        float bankingMph = (vehicleInfo != null) ? seg.banking * vehicleInfo.bankingMphPerDegree : 0f;
        return Mathf.Clamp(baseMph + bankingMph, 5f, topMph);
    }

    void FixedUpdate()
    {
        if (_mainSamples == null || _mainSamples.Count < 2) return;

        if (usePitLane != _onPit)
        {
            _onPit = usePitLane;
            _distance = 0f;
        }

        float length = _onPit ? _pitLength : _mainLength;
        if (length <= 0f) return;

        if (vehicleInfo != null && _segmentTargetMph != null && !_onPit)
        {
            float targetMph = ComputeEffectiveTargetMph(_distance) * paceMultiplier;
            CurrentTargetMph = targetMph;
            UpdateSpeedToward(targetMph);
            speed = _currentMph * MphToMps;
            _dbgCurrentMph = _currentMph;
            _dbgTargetMph = targetMph;
            _dbgSegIdx = SegmentIndexAt(_distance);
            _dbgDistance = _distance;
            _dbgAccelMps2 = SampleAccel(_currentMph);
            _dbgDecelMps2 = SampleDecel(_currentMph);
        }
        else
        {
            Debug.LogWarning($"[SplineDriver] {name} sim gated off — vehicleInfo={(vehicleInfo != null)}, segmentTargets={(_segmentTargetMph != null)}, onPit={_onPit}, trackInfo={(track != null && track.track != null)}", this);
            enabled = false;
        }

        _distance += speed * Time.fixedDeltaTime;
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

    float ComputeEffectiveTargetMph(float distance)
    {
        int curIdx = SegmentIndexAt(distance);
        float target = _segmentTargetMph[curIdx];

        float decel = SampleDecel(_currentMph);
        if (decel < 0.1f) decel = 0.1f;
        float currentMps = _currentMph * MphToMps;

        int n = _segmentTargetMph.Length;
        for (int k = 1; k <= n; k++)
        {
            int idx = (curIdx + k) % n;
            float distToStart = _segmentStartDistance[idx] - distance;
            if (distToStart < 0f) distToStart += _mainLength;
            if (distToStart > brakingLookahead) break;

            float segMph = _segmentTargetMph[idx];
            if (segMph >= _currentMph) continue;

            float segMps = segMph * MphToMps;
            float brakeDist = (currentMps * currentMps - segMps * segMps) / (2f * decel);
            if (brakeDist >= distToStart) target = Mathf.Min(target, segMph);
        }

        return target;
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

        float lineLateral = (!_onPit && _anchors != null && track.track != null) ? track.track.GetLateralAt(_distance, lineFactor, _anchors) : 0f;
        Vector2 right = new Vector2(sample.tangent.y, -sample.tangent.x);
        Vector2 finalPos = sample.position + right * (lateralOffset + lineLateral);
        Vector3 worldPos = track != null ? track.transform.TransformPoint(new Vector3(finalPos.x, finalPos.y, 0)) : new Vector3(finalPos.x, finalPos.y, 0);
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg;
        float leanTarget = _hasPrevHeading ? Mathf.DeltaAngle(_prevHeading, angleDeg) * leanIntoTurns : 0f;
        _currentLean = Mathf.Lerp(leanTarget, _currentLean, leanSmoothing);
        _prevHeading = angleDeg;
        _hasPrevHeading = true;
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? angleDeg - 90f : angleDeg) + angleOffsetDeg + _currentLean);
    }
}
