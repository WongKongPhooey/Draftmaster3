using UnityEngine;

// Kinematic bicycle model with axle-level lateral-G saturation. Opt-in.
// When enabled, takes over transform writes from SplineDriver. SplineDriver still produces commanded
// position / heading / speed; this component tracks those targets with steering + throttle inputs
// integrated through bicycle kinematics. Realistic understeer kicks in when target lateral G exceeds grip.
//
// Place after SplineDriver in script execution order so commanded values are fresh.
[RequireComponent(typeof(SplineDriver))]
[DefaultExecutionOrder(200)]
public class BicycleDynamics : MonoBehaviour
{
    [Header("Controller Gains")]
    [Tooltip("Lookahead distance (m) along path used by the pure-pursuit steering controller. Higher = smoother but lazier.")]
    public float lookaheadDistance = 6f;
    [Tooltip("How aggressively throttle/brake closes speed error per second. Higher = snappier.")]
    public float speedGain = 4f;
    [Tooltip("Heading-error softener; reduces steering when going slow to avoid spin.")]
    public float lowSpeedCutoff = 6f;

    [Header("Grip Saturation")]
    [Tooltip("Multiplier on the car's maxLateralG used to decide when the model starts to wash out (1.0 = exactly at sim limit).")]
    public float saturationHeadroom = 1f;

    [Tooltip("If true, this controller writes transform; SplineDriver kinematic writes are suppressed.")]
    public bool active = true;

    SplineDriver _spline;
    float _speedMps;
    float _headingDeg;
    float _steeringDeg;
    bool _initialised;

    void Awake() { _spline = GetComponent<SplineDriver>(); }
    void OnEnable() { _spline.externalMotionController = active; }
    void OnDisable() { if (_spline != null) _spline.externalMotionController = false; }

    void FixedUpdate()
    {
        if (!active || _spline == null || _spline.vehicleInfo == null) return;
        if (_spline.track == null) return;

        if (!_initialised)
        {
            _headingDeg = _spline.CommandedHeadingDeg;
            _speedMps = _spline.CommandedSpeedMps;
            transform.position = WorldFromLocal(_spline.CommandedLocalPos);
            transform.rotation = HeadingRotation(_headingDeg);
            _initialised = true;
            return;
        }

        var vi = _spline.vehicleInfo;
        float dt = Time.fixedDeltaTime;

        // Steering: pure-pursuit. Convert current world pos to track-local then look ahead along commanded path.
        Vector2 currentLocal = LocalFromWorld(transform.position);
        Vector2 targetLocal = _spline.CommandedLocalPos;
        Vector2 toTarget = targetLocal - currentLocal;
        float targetHeadingRad = Mathf.Atan2(toTarget.y, toTarget.x);
        float targetHeadingDeg = targetHeadingRad * Mathf.Rad2Deg;

        float headingError = Mathf.DeltaAngle(_headingDeg, targetHeadingDeg);
        float desiredSteer = headingError * Mathf.Clamp01(_speedMps / Mathf.Max(lowSpeedCutoff, 0.1f));
        desiredSteer = Mathf.Clamp(desiredSteer, -vi.maxSteeringAngle, vi.maxSteeringAngle);

        // Rate-limit steering input.
        float steerStep = vi.steeringRate * dt;
        _steeringDeg = Mathf.MoveTowards(_steeringDeg, desiredSteer, steerStep);

        // Throttle/brake from commanded speed error.
        float speedError = _spline.CommandedSpeedMps - _speedMps;
        float accelInput = Mathf.Clamp(speedError * speedGain, -50f, 50f);
        float accelCap = SampleAccel(vi, _speedMps) * TrackConditions.EffectivePower;
        float decelCap = SampleDecel(vi, _speedMps);
        float longitudinalA = accelInput >= 0f ? Mathf.Min(accelInput, accelCap) : Mathf.Max(accelInput, -decelCap);

        // Yaw from bicycle kinematics. ω = v/L · tan(δ).
        float steerRad = _steeringDeg * Mathf.Deg2Rad;
        float yawRateRad = (_speedMps / Mathf.Max(vi.wheelbase, 0.1f)) * Mathf.Tan(steerRad);

        // Grip saturation: cap lateral acceleration. If required lateral > available, scale yawRate down and bleed speed.
        var tire = GetComponent<TireState>();
        float gripMul = TrackConditions.AiEffective * (tire != null ? tire.GripMultiplier : 1f);
        float maxLatMps2 = vi.maxLateralG * gripMul * 9.81f * saturationHeadroom;
        float requiredLatMps2 = Mathf.Abs(yawRateRad * _speedMps);
        if (requiredLatMps2 > maxLatMps2 && _speedMps > 0.5f)
        {
            float ratio = maxLatMps2 / requiredLatMps2;
            yawRateRad *= ratio;
            // Understeer cost: bleed a fraction of overdriven speed as scrub.
            float scrub = (1f - ratio) * 4f * dt;
            longitudinalA -= scrub * 9.81f;
        }

        _speedMps = Mathf.Max(0f, _speedMps + longitudinalA * dt);
        _headingDeg += yawRateRad * Mathf.Rad2Deg * dt;

        float headingRad = _headingDeg * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
        currentLocal += forward * (_speedMps * dt);

        transform.position = WorldFromLocal(currentLocal);
        transform.rotation = HeadingRotation(_headingDeg + (_spline.spriteFacesUp ? -90f : 0f) + _spline.angleOffsetDeg);

        _spline.speed = _speedMps;
    }

    Vector3 WorldFromLocal(Vector2 local)
    {
        if (_spline.track == null) return new Vector3(local.x, local.y, transform.position.z);
        var w = _spline.track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
        return new Vector3(w.x, w.y, transform.position.z);
    }

    Vector2 LocalFromWorld(Vector3 world)
    {
        if (_spline.track == null) return new Vector2(world.x, world.y);
        var l = _spline.track.transform.InverseTransformPoint(world);
        return new Vector2(l.x, l.y);
    }

    static Quaternion HeadingRotation(float deg) => Quaternion.Euler(0f, 0f, deg);

    static float SampleAccel(VehicleInfo vi, float speedMps)
    {
        float mph = speedMps * 2.237f;
        if (vi.accelerationCurve != null && vi.accelerationCurve.length > 0) return Mathf.Max(0f, vi.accelerationCurve.Evaluate(mph));
        return 5f;
    }

    static float SampleDecel(VehicleInfo vi, float speedMps)
    {
        float mph = speedMps * 2.237f;
        if (vi.decelerationCurve != null && vi.decelerationCurve.length > 0) return Mathf.Max(0.1f, vi.decelerationCurve.Evaluate(mph));
        return 10f;
    }
}
