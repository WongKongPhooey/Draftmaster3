using UnityEngine;
using UnityEngine.InputSystem;

// Free-driven player car. DYNAMIC bicycle model: separate front/rear tyres with slip angles and load-dependent
// grip, longitudinal weight transfer, and per-axle wear. This is what produces real understeer (front lets go
// first → car runs wide) and oversteer (rear lets go first → car rotates / slides). Impacts inject yaw rate and
// the tyres damp it out, so a knock means a natural, recoverable slide rather than a scripted snap.
// Implements IVehicleSpeedReadout so SpeedometerUI can read speed, ICollisionResponder for VehicleCollision.
public class PlayerVehicleController : MonoBehaviour, IVehicleSpeedReadout, ICollisionResponder
{
    [Header("Collision Response")]
    [Tooltip("Bounce-back along the wall normal. 0 = no bounce (slide), 0.3 = lively armco kick.")]
    [Range(0f, 0.8f)] public float restitution = 0.25f;
    [Tooltip("Speed retained tangent to the wall on a scrape. 1 = no loss, 0.9 = slight scrub.")]
    [Range(0.5f, 1f)] public float tangentialRetention = 0.94f;
    [Tooltip("How much yaw spin an off-centre hit imparts. 0 = none (slide only), 1 = realistic, >1 = arcadey.")]
    [Range(0f, 2f)] public float spinSensitivity = 1f;
    [Tooltip("Max yaw rate a single impact can add (deg/sec). Caps how violent a spin-out gets.")]
    public float maxImpactSpinDeg = 360f;

    [Header("Vehicle Geometry / Mass")]
    [Tooltip("Fraction of static weight on the FRONT axle. >0.5 = nose-heavy (more front grip, tends to understeer).")]
    [Range(0.4f, 0.65f)] public float frontWeightBias = 0.54f;
    [Tooltip("Centre-of-gravity height (m). Higher = more weight transfer under accel/brake, more pitch-sensitive balance.")]
    public float cgHeight = 0.5f;
    [Tooltip("Yaw inertia scaler. Iz = mass * a * b * this. <1 = darty/nervous rotation, >1 = lazy/stable.")]
    [Range(0.4f, 2f)] public float yawInertiaFactor = 1.0f;

    [Header("Tyre Model")]
    [Tooltip("Cornering stiffness per unit vertical load (1/rad). How sharply slip angle builds lateral force. ~10-14 typical. Higher = sharper turn-in, twitchier.")]
    public float corneringStiffness = 11f;
    [Tooltip("Static handling balance. + = more understeer (rear grippier than front, safe/stable). - = oversteer (loose). 0 = neutral.")]
    [Range(-0.3f, 0.3f)] public float understeerBias = 0.04f;
    [Tooltip("Below this forward speed (m/s) steering is kinematic (geometry only) to avoid divide-by-near-zero jitter at crawl.")]
    public float lowSpeedKinematic = 2.0f;
    [Tooltip("Physics sub-steps per FixedUpdate. More = stiffer/stabler tyres without oscillation. 4 is plenty.")]
    [Range(1, 8)] public int subSteps = 4;

    [Header("Tyre Wear")]
    [Tooltip("Enable per-axle wear. Worked tyres lose grip toward VehicleInfo.tireMinGrip and shift the balance.")]
    public bool enableWear = true;
    [Tooltip("Wear gained per second at full lateral load, multiplied onto VehicleInfo.tireWearRate.")]
    public float wearRateScale = 1f;
    [Range(0f, 1f)] public float wearFront;   // exposed for HUD / telemetry
    [Range(0f, 1f)] public float wearRear;

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
    [Tooltip("Steering authority reduction at high speed. 1 = full at all speeds, lower tightens high-speed steering.")]
    [Range(0.2f, 1f)] public float highSpeedSteerScale = 0.55f;
    [Tooltip("Speed (mph) at which steering authority has decayed to highSpeedSteerScale.")]
    public float steerDecaySpeedMph = 180f;
    [Tooltip("Stick input below this magnitude is ignored — kills drift-induced steer.")]
    [Range(0f, 0.4f)] public float steerDeadzone = 0.12f;
    [Tooltip("Steering response curve. 1 = linear, higher = softer near centre (small tilt -> small steer).")]
    [Range(1f, 3f)] public float steerExpo = 1.8f;

    public VehicleInfo vehicleInfo;

    public float Mass => vehicleInfo != null ? vehicleInfo.mass : 1500f;
    public float SpeedMps => Mathf.Sqrt(_vx * _vx + _vy * _vy);
    public float SpeedMph => SpeedMps * 2.237f;
    // Body-slip angle (overall slide), degrees.
    public float SlipAngleDeg => Mathf.Atan2(_vy, Mathf.Max(Mathf.Abs(_vx), 0.01f)) * Mathf.Rad2Deg;
    public float YawRateDeg => _r * Mathf.Rad2Deg;
    // Per-axle slip angles (deg) and handling balance: + = understeer (front sliding more), - = oversteer (rear more).
    public float SlipFrontDeg => _alphaF * Mathf.Rad2Deg;
    public float SlipRearDeg => _alphaR * Mathf.Rad2Deg;
    public float HandlingBalanceDeg => (Mathf.Abs(_alphaF) - Mathf.Abs(_alphaR)) * Mathf.Rad2Deg;

    const float G = 9.81f;

    float _vx;          // forward velocity, body frame (m/s)
    float _vy;          // lateral velocity, body frame (m/s, +left)
    float _r;           // yaw rate (rad/s, + = CCW)
    float _headingDeg;  // world heading of the nose
    float _steerDeg;    // current front-wheel angle

    float _a, _b, _iz;  // CoM→front axle, CoM→rear axle (m), yaw inertia (kg·m²)
    float _alphaF, _alphaR; // last front/rear slip angles (rad) for telemetry

    void Start()
    {
        if (startReference != null)
        {
            transform.position = startReference.position;
            transform.rotation = startReference.rotation;
        }
        _headingDeg = transform.eulerAngles.z + (spriteFacesUp ? 90f : 0f) - angleOffsetDeg;
        RecomputeGeometry();
    }

    void RecomputeGeometry()
    {
        float L = vehicleInfo != null ? Mathf.Max(vehicleInfo.wheelbase, 0.5f) : 2.8f;
        // Front-heavy → CoM nearer the front → a (CoM→front) smaller than b (CoM→rear).
        _a = L * (1f - frontWeightBias);
        _b = L * frontWeightBias;
        _iz = Mass * _a * _b * Mathf.Max(yawInertiaFactor, 0.1f);
    }

    void FixedUpdate()
    {
        if (vehicleInfo == null) return;
        RecomputeGeometry();
        float dt = Time.fixedDeltaTime;

        // --- Inputs: gamepad first, keyboard overrides if pressed.
        float steerIn = 0f, throttleIn = 0f, brakeIn = 0f;
        var gp = Gamepad.current;
        if (gp != null)
        {
            steerIn = gp.leftStick.ReadValue().x;
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
        if (Mathf.Abs(steerIn) < steerDeadzone) steerIn = 0f;
        steerIn = Mathf.Sign(steerIn) * Mathf.Pow(Mathf.Abs(steerIn), steerExpo);

        // Steering → front-wheel angle, rate-limited and speed-scaled.
        float speedFraction = Mathf.Clamp01(SpeedMph / Mathf.Max(steerDecaySpeedMph, 1f));
        float speedAuthority = Mathf.Lerp(1f, highSpeedSteerScale, speedFraction);
        float desiredSteer = -steerIn * vehicleInfo.maxSteeringAngle * speedAuthority;
        _steerDeg = Mathf.MoveTowards(_steerDeg, desiredSteer, vehicleInfo.steeringRate * dt);
        float delta = _steerDeg * Mathf.Deg2Rad;

        // --- Longitudinal command (engine/brake along the nose), evaluated from forward speed.
        float topMps = vehicleInfo.topSpeed / 2.237f;
        float accel = SampleAccel(_vx) * TrackConditions.PowerMultiplier * throttleIn;
        float decel = SampleDecel(_vx) * brakeIn;
        if (throttleIn < 0.05f && brakeIn < 0.05f) decel += coastDecel;
        float axCmd = accel - decel; // commanded longitudinal accel (m/s²)

        // Available grip per axle: μ (proxied by maxLateralG) × track × wear, biased front/rear for balance.
        float trackGrip = TrackConditions.GripMultiplier;
        float minGrip = vehicleInfo.tireMinGrip;
        float wearGripF = enableWear ? Mathf.Lerp(1f, minGrip, wearFront) : 1f;
        float wearGripR = enableWear ? Mathf.Lerp(1f, minGrip, wearRear) : 1f;
        float muF = vehicleInfo.maxLateralG * trackGrip * wearGripF * (1f - understeerBias);
        float muR = vehicleInfo.maxLateralG * trackGrip * wearGripR * (1f + understeerBias);

        float m = Mass;
        float h = dt / Mathf.Max(subSteps, 1);
        float wearAccumF = 0f, wearAccumR = 0f;

        for (int s = 0; s < subSteps; s++)
        {
            // Longitudinal weight transfer: braking (axCmd<0) loads the front, throttle loads the rear.
            float staticFzF = m * G * frontWeightBias;
            float staticFzR = m * G * (1f - frontWeightBias);
            float dFz = m * axCmd * cgHeight / (_a + _b);
            float fzF = Mathf.Max(staticFzF - dFz, 0f);
            float fzR = Mathf.Max(staticFzR + dFz, 0f);

            float fyF, fyR;
            if (_vx > lowSpeedKinematic)
            {
                // Slip angles: difference between where each axle points and where it's actually travelling.
                float alphaF = Mathf.Atan2(_vy + _a * _r, _vx) - delta;
                float alphaR = Mathf.Atan2(_vy - _b * _r, _vx);
                _alphaF = alphaF; _alphaR = alphaR;

                // Linear tyre with friction-circle clamp. The axle that hits its μ·Fz ceiling first lets go first.
                float peakF = muF * fzF;
                float peakR = muR * fzR;
                fyF = Mathf.Clamp(-corneringStiffness * fzF * alphaF, -peakF, peakF);
                fyR = Mathf.Clamp(-corneringStiffness * fzR * alphaR, -peakR, peakR);

                // Tyre work for wear: how close each axle runs to its limit.
                wearAccumF += Mathf.Abs(fyF) / Mathf.Max(peakF, 1f);
                wearAccumR += Mathf.Abs(fyR) / Mathf.Max(peakR, 1f);

                // Equations of motion (body frame).
                float ay = (fyF + fyR) / m - _vx * _r;          // lateral accel (tyres + centripetal term)
                float dr = (_a * fyF - _b * fyR) / _iz;          // yaw accel from front/rear moment
                _vy += ay * h;
                _r += dr * h;
            }
            else
            {
                // Low-speed: kinematic yaw from geometry, bleed lateral velocity (no meaningful slip at crawl).
                float rKin = (_vx / (_a + _b)) * Mathf.Tan(delta);
                _r = Mathf.MoveTowards(_r, rKin, 8f * h);
                _vy = Mathf.MoveTowards(_vy, 0f, 12f * h);
                _alphaF = 0f; _alphaR = 0f;
            }

            // Forward: engine/brake + the centripetal coupling. Clamp to [0, top].
            _vx += (axCmd + _vy * _r) * h;
            _vx = Mathf.Clamp(_vx, 0f, topMps);

            // Integrate heading by yaw rate.
            _headingDeg += _r * Mathf.Rad2Deg * h;
        }

        // --- Per-axle wear accumulation (worked tyres fade and shift the balance).
        if (enableWear && SpeedMph > 1f)
        {
            float rate = vehicleInfo.tireWearRate * wearRateScale;
            wearFront = Mathf.Clamp01(wearFront + (wearAccumF / subSteps) * rate * dt);
            wearRear = Mathf.Clamp01(wearRear + (wearAccumR / subSteps) * rate * dt);
        }

        // --- Integrate world position from body velocity rotated into world space.
        float hr = _headingDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(hr), sin = Mathf.Sin(hr);
        Vector2 worldVel = new Vector2(_vx * cos - _vy * sin, _vx * sin + _vy * cos);
        Vector2 newPos = (Vector2)transform.position + worldVel * dt;
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? _headingDeg - 90f : _headingDeg) + angleOffsetDeg);
    }

    public void ApplyContact(Vector2 worldMtv, Vector2 contactPoint, float severity)
    {
        // Depenetrate.
        transform.position += new Vector3(worldMtv.x, worldMtv.y, 0f);

        Vector2 n = worldMtv.sqrMagnitude > 1e-6f ? worldMtv.normalized : Vector2.zero;
        if (n == Vector2.zero) return;

        // Current velocity in world space.
        float hr = _headingDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(hr), sin = Mathf.Sin(hr);
        Vector2 vel = new Vector2(_vx * cos - _vy * sin, _vx * sin + _vy * cos);

        float vn = Vector2.Dot(vel, n);
        if (vn < 0f) // moving into the surface — reflect momentum.
        {
            Vector2 normalComp = vn * n;
            Vector2 tangentComp = vel - normalComp;
            vel = tangentComp * tangentialRetention - normalComp * restitution;

            // Convert reflected world velocity back to the body frame.
            _vx = vel.x * cos + vel.y * sin;
            _vy = -vel.x * sin + vel.y * cos;

            // Off-centre hit → yaw torque. Lever arm × normal impulse, divided by yaw inertia → Δ yaw rate.
            Vector2 rArm = contactPoint - (Vector2)transform.position;
            float impulse = (1f + restitution) * (-vn) * Mass; // N·s along the normal
            float crossZ = rArm.x * n.y - rArm.y * n.x;
            float deltaOmega = (crossZ * impulse / Mathf.Max(_iz, 1f)) * spinSensitivity; // rad/s
            float cap = maxImpactSpinDeg * Mathf.Deg2Rad;
            _r += Mathf.Clamp(deltaOmega, -cap, cap);
        }
    }

    public void PitResetTyres() { wearFront = 0f; wearRear = 0f; }

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
