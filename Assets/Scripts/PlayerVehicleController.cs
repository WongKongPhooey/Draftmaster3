using UnityEngine;
using UnityEngine.InputSystem;

// Free-driven player car. Bicycle kinematics with grip-cap, driven directly by gamepad / keyboard inputs.
// No spline/path coupling. Implements IVehicleSpeedReadout so SpeedometerUI can read its speed.
public class PlayerVehicleController : MonoBehaviour, IVehicleSpeedReadout, ICollisionResponder
{
    [Header("Collision Response")]
    [Tooltip("Bounce-back along the wall normal. 0 = no bounce (slide), 0.3 = lively armco kick.")]
    [Range(0f, 0.8f)] public float restitution = 0.25f;
    [Tooltip("Speed retained tangent to the wall on a scrape. 1 = no loss, 0.9 = slight scrub.")]
    [Range(0.5f, 1f)] public float tangentialRetention = 0.94f;
    [Tooltip("How fast the car's heading bends toward the deflected direction on contact (deg/sec). Lower = spongier, more inertia.")]
    public float contactTurnRate = 220f;

    float _contactHeadingTarget;
    float _contactTimer;

    public void ApplyContact(Vector2 worldMtv, float severity)
    {
        transform.position += new Vector3(worldMtv.x, worldMtv.y, 0f);

        Vector2 n = worldMtv.sqrMagnitude > 1e-6f ? worldMtv.normalized : Vector2.zero;
        if (n == Vector2.zero) return;

        float hRad = _headingDeg * Mathf.Deg2Rad;
        Vector2 vel = new Vector2(Mathf.Cos(hRad), Mathf.Sin(hRad)) * _speedMps;

        float vn = Vector2.Dot(vel, n);
        if (vn < 0f)
        {
            Vector2 normalComp = vn * n;
            Vector2 tangentComp = vel - normalComp;
            Vector2 deflected = tangentComp * tangentialRetention - normalComp * restitution;
            _speedMps = deflected.magnitude; // speed change immediate (energy lost in impact)
            if (deflected.sqrMagnitude > 0.01f)
            {
                // Heading bends toward deflected dir over time, not instantly — gives flex/inertia.
                _contactHeadingTarget = Mathf.Atan2(deflected.y, deflected.x) * Mathf.Rad2Deg;
                _contactTimer = 0.25f;
            }
        }
    }

    public VehicleInfo vehicleInfo;

    [Header("Spawn")]
    [Tooltip("Optional transform whose world position+rotation seeds the car at Start.")]
    public Transform startReference;

    [Header("Visual")]
    [Tooltip("If the sprite faces +Y by default, the renderer is rotated by -90° relative to heading.")]
    public bool spriteFacesUp = false;
    public float angleOffsetDeg = 180f;

    [Header("Physics Tuning")]
    [Tooltip("Coasting deceleration (m/s²) when no throttle and no brake (engine + aero drag).")]
    public float coastDecel = 1.5f;
    [Tooltip("Cornering scrub. m/s² lost per unit of grip overload per second. Low values (~0.6) reward correct braking, high values punish hard.")]
    public float scrubCoefficient = 0.6f;
    [Tooltip("Only scrub when lateral demand exceeds this fraction of grip. 1.0 = scrub from the limit; 0.85 = small underdrive cushion.")]
    public float scrubThreshold = 1.0f;
    [Tooltip("Steering authority reduction at high speed. 1 = full at all speeds, lower numbers tighten high-speed steering.")]
    [Range(0.2f, 1f)] public float highSpeedSteerScale = 0.55f;
    [Tooltip("Speed (mph) at which steering authority has decayed to highSpeedSteerScale.")]
    public float steerDecaySpeedMph = 180f;

    public float SpeedMps => _speedMps;
    public float SpeedMph => _speedMps * 2.237f;

    float _speedMps;
    float _headingDeg;
    float _steerDeg;

    void Start()
    {
        if (startReference != null)
        {
            transform.position = startReference.position;
            transform.rotation = startReference.rotation;
        }
        _headingDeg = transform.eulerAngles.z + (spriteFacesUp ? 90f : 0f) - angleOffsetDeg;
    }

    void FixedUpdate()
    {
        if (vehicleInfo == null) return;
        float dt = Time.fixedDeltaTime;

        // Inputs: gamepad first, keyboard overrides if pressed.
        float steerIn = 0f, throttleIn = 0f, brakeIn = 0f;
        var gp = Gamepad.current;
        if (gp != null)
        {
            steerIn = gp.leftStick.x.ReadValue();
            throttleIn = gp.rightTrigger.ReadValue();
            brakeIn = gp.leftTrigger.ReadValue();
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerIn = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerIn = +1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttleIn = 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brakeIn = 1f;
        }
        steerIn = Mathf.Clamp(steerIn, -1f, 1f);

        // Steering input maps to front wheel angle, then rate-limited and speed-scaled.
        float speedFraction = Mathf.Clamp01(SpeedMph / Mathf.Max(steerDecaySpeedMph, 1f));
        float speedAuthority = Mathf.Lerp(1f, highSpeedSteerScale, speedFraction);
        float desiredSteer = -steerIn * vehicleInfo.maxSteeringAngle * speedAuthority;
        _steerDeg = Mathf.MoveTowards(_steerDeg, desiredSteer, vehicleInfo.steeringRate * dt);

        // Longitudinal force.
        float accelMps2 = SampleAccel(_speedMps) * TrackConditions.PowerMultiplier * throttleIn;
        float decelMps2 = SampleDecel(_speedMps) * brakeIn;
        if (throttleIn < 0.05f && brakeIn < 0.05f) decelMps2 += coastDecel;
        float topMps = vehicleInfo.topSpeed / 2.237f;
        _speedMps = Mathf.Clamp(_speedMps + (accelMps2 - decelMps2) * dt, 0f, topMps);

        // Bicycle yaw + grip saturation.
        float steerRad = _steerDeg * Mathf.Deg2Rad;
        float yawRateRad = (_speedMps / Mathf.Max(vehicleInfo.wheelbase, 0.1f)) * Mathf.Tan(steerRad);

        var tire = GetComponent<TireState>();
        float gripMul = TrackConditions.GripMultiplier * (tire != null ? tire.GripMultiplier : 1f);
        float maxLatMps2 = vehicleInfo.maxLateralG * gripMul * 9.81f;
        float requiredLatMps2 = Mathf.Abs(yawRateRad * _speedMps);
        float scrubLimit = maxLatMps2 * scrubThreshold;
        if (requiredLatMps2 > scrubLimit && _speedMps > 0.5f)
        {
            float ratio = maxLatMps2 / Mathf.Max(requiredLatMps2, 0.01f);
            yawRateRad *= ratio;
            float overload = (requiredLatMps2 - scrubLimit) / Mathf.Max(scrubLimit, 0.01f);
            _speedMps -= overload * scrubCoefficient * 9.81f * dt;
            if (_speedMps < 0f) _speedMps = 0f;
        }

        _headingDeg += yawRateRad * Mathf.Rad2Deg * dt;

        // Contact heading bend: rate-limited rotation toward the deflected direction (spongy, not instant).
        if (_contactTimer > 0f)
        {
            _headingDeg = Mathf.MoveTowardsAngle(_headingDeg, _contactHeadingTarget, contactTurnRate * dt);
            _contactTimer -= dt;
        }

        float headRad = _headingDeg * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(headRad), Mathf.Sin(headRad));
        Vector2 newPos = (Vector2)transform.position + forward * (_speedMps * dt);
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? _headingDeg - 90f : _headingDeg) + angleOffsetDeg);
    }

    float SampleAccel(float speedMps)
    {
        float mph = speedMps * 2.237f;
        if (vehicleInfo.accelerationCurve != null && vehicleInfo.accelerationCurve.length > 0)
            return Mathf.Max(0f, vehicleInfo.accelerationCurve.Evaluate(mph));
        return 5f;
    }

    float SampleDecel(float speedMps)
    {
        float mph = speedMps * 2.237f;
        if (vehicleInfo.decelerationCurve != null && vehicleInfo.decelerationCurve.length > 0)
            return Mathf.Max(0.1f, vehicleInfo.decelerationCurve.Evaluate(mph));
        return 10f;
    }
}
