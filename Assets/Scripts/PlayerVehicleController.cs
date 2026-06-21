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
    [Range(-0.3f, 0.3f)] public float understeerBias = 0.10f;
    [Tooltip("Yaw-rate damping (1/s). Resists spinning — settles snap spins and tames brake-induced rotation. Higher = more stable/heavier feel. 0 = none.")]
    public float yawDamping = 2.5f;
    [Tooltip("Extra yaw damping under braking (1/s), scaled by brake input. Specifically counters spin-on-the-brakes from rear unloading.")]
    public float brakeYawDamping = 2f;
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

    [Header("Surface / Grass")]
    [Tooltip("TrackBuilder used to tell on-track from grass. Auto-found at Start if left empty.")]
    public TrackBuilder track;
    [Tooltip("Lateral grip multiplier when off the track surface. Low = slippery grass that lets the car break away.")]
    [Range(0.1f, 1f)] public float grassGrip = 0.4f;
    [Tooltip("Engine traction multiplier on grass (wheels struggle to put power down).")]
    [Range(0.1f, 1f)] public float grassPower = 0.55f;
    [Tooltip("Extra deceleration (m/s²) from rolling resistance on grass, at and above grassDragRampSpeed.")]
    public float grassDrag = 6f;
    [Tooltip("Speed (m/s) over which grass rolling resistance reaches full strength. Below it, drag fades to 0 so the car can crawl off a standstill.")]
    public float grassDragRampSpeed = 3f;

    [Header("Runoff Surfaces")]
    [Tooltip("Lateral grip on a Gravel runoff. Low = slides; gravel mainly bogs the car down via drag.")]
    [Range(0.1f, 1f)] public float gravelGrip = 0.45f;
    [Tooltip("Engine traction on Gravel (wheels dig in / spin).")]
    [Range(0.1f, 1f)] public float gravelPower = 0.4f;
    [Tooltip("Extra deceleration (m/s²) from a Gravel trap. High = sinks in and stops fast.")]
    public float gravelDrag = 14f;
    [Tooltip("Lateral grip on a Tarmac runoff — near track grip, so paved run-offs are safe.")]
    [Range(0.1f, 1f)] public float tarmacRunoffGrip = 0.95f;

    [Header("Damage → Handling")]
    [Tooltip("Grip lost at full bodywork damage (both axles).")]
    [Range(0f, 0.6f)] public float damageGripLoss = 0.22f;
    [Tooltip("Extra deceleration (m/s²) at full damage — bent panels drag.")]
    public float damageDragAdd = 3f;
    [Tooltip("Fraction of top speed lost at full damage.")]
    [Range(0f, 0.5f)] public float damageTopSpeedLoss = 0.12f;
    [Tooltip("Steering pull (radians) at full damage with full side bias — a battered car wanders toward the hit side.")]
    public float damageSteerPull = 0.05f;

    [Header("Reverse")]
    [Tooltip("Hold brake with no throttle once nearly stopped to reverse. Max reverse speed (m/s).")]
    public float reverseMaxSpeed = 5f;
    [Tooltip("Reverse acceleration (m/s²).")]
    public float reverseAccel = 4f;
    [Tooltip("Forward speed (m/s) below which holding the brake engages reverse.")]
    public float reverseEngageSpeed = 0.4f;

    [Header("Wheelspin / Donuts")]
    [Tooltip("Enable low-speed wheelspin/donuts. Disabled for AI so they launch cleanly instead of spinning off the line.")]
    public bool enableWheelspin = true;
    [Tooltip("Below this forward speed (m/s), heavy throttle lights up the rear tyres (1st-gear wheelspin).")]
    public float wheelspinSpeed = 9f;
    [Tooltip("Throttle needed before the rear breaks traction.")]
    [Range(0f, 1f)] public float wheelspinThrottle = 0.6f;
    [Tooltip("Donut rotation rate at full spin + full steering lock (deg/sec).")]
    public float wheelspinYawRate = 150f;
    [Tooltip("How quickly donut yaw spins up/down (deg/sec²).")]
    public float wheelspinYawAccel = 360f;
    [Tooltip("Forward accel lost while the wheels are spinning (keeps the car slow so a donut can be held).")]
    [Range(0f, 0.95f)] public float wheelspinAccelLoss = 0.6f;
    [Tooltip("Rear lateral grip multiplier while spinning (lower = looser, easier to rotate).")]
    [Range(0.1f, 1f)] public float wheelspinRearGrip = 0.45f;

    [Header("Tyre Trails")]
    [Tooltip("Leave faint tyre trails while on grass.")]
    public bool grassTrails = true;
    [Tooltip("Trail tint (alpha = faintness).")]
    public Color trailColor = new Color(0.18f, 0.14f, 0.10f, 0.5f);
    public float trailWidth = 0.35f;
    [Tooltip("Seconds a trail segment lingers before fading out.")]
    public float trailTime = 4f;
    [Tooltip("Lateral distance between the two rear tyre trails (m).")]
    public float trailTrackWidth = 1.6f;
    [Tooltip("Sorting order for trails. Above the track surface, below the car.")]
    public int trailSortingOrder = 1;
    [Tooltip("Optional trail material. A faint unlit sprite material is created if left empty.")]
    public Material trailMaterial;

    public VehicleInfo vehicleInfo;

    public float Mass => vehicleInfo != null ? vehicleInfo.mass : 1500f;
    public float SpeedMps => Mathf.Sqrt(_vx * _vx + _vy * _vy);
    public float SpeedMph => SpeedMps * 2.237f;
    // Body-slip angle (overall slide), degrees.
    public float SlipAngleDeg => Mathf.Atan2(_vy, Mathf.Max(Mathf.Abs(_vx), 0.01f)) * Mathf.Rad2Deg;
    public float YawRateDeg => _r * Mathf.Rad2Deg;
    public float HeadingDeg => _headingDeg; // world heading of the nose (0 = +X), for AI input providers
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
    bool _onGrass;          // car is off the track surface this step
    float _lastAy;          // last lateral accel (m/s²), for tyre load-transfer split
    TireModel _tires;       // 4-tyre wear+temperature model (when enableWear)
    VehicleDamage _bodywork; // accumulated bodywork damage → handling penalties

    [HideInInspector] public bool externalInput; // true when an AI controller feeds inputs via SetInput
    // Soft forward-speed cap (m/s). Infinity = no cap. Set by FormationDirector to hold the player to
    // pace-car speed during a caution/formation lap. Caps forward speed only — reverse and slides are untouched.
    [HideInInspector] public float speedGovernorMps = Mathf.Infinity;
    float _inSteer, _inThrottle, _inBrake;        // last externally-supplied inputs
    bool _wasEmitting;      // trail emit state last step (for streak-free re-enable)
    TrailRenderer _trailL, _trailR; // rear tyre trails (grass)

    void Start()
    {
        if (startReference != null)
        {
            transform.position = startReference.position;
            transform.rotation = startReference.rotation;
        }
        _headingDeg = transform.eulerAngles.z + (spriteFacesUp ? 90f : 0f) - angleOffsetDeg;
        RecomputeGeometry();

        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        if (grassTrails) CreateTrails();

        if (enableWear)
        {
            _tires = GetComponent<TireModel>();
            if (_tires == null) _tires = gameObject.AddComponent<TireModel>();
            _tires.Configure(vehicleInfo);
        }

        _bodywork = GetComponentInChildren<VehicleDamage>();
    }

    void CreateTrails()
    {
        _trailL = MakeTrail("TyreTrailL");
        _trailR = MakeTrail("TyreTrailR");
    }

    // Feed driving inputs from an external controller (AI). Each is -1..1 / 0..1; only used when externalInput is true.
    public void SetInput(float steer, float throttle, float brake)
    {
        _inSteer = Mathf.Clamp(steer, -1f, 1f);
        _inThrottle = Mathf.Clamp01(throttle);
        _inBrake = Mathf.Clamp01(brake);
    }

    // Place the car and align the dynamic state to a heading (used at AI spawn so it starts on the grid/pit,
    // and when handing back from a kinematic formation lap — pass the current forward speed so the rolling
    // start doesn't lurch from a standstill).
    public void SeedPose(Vector2 worldPos, float headingDeg, float forwardSpeedMps = 0f)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        _headingDeg = headingDeg;
        _vx = forwardSpeedMps;
        _vy = _r = 0f;
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? _headingDeg - 90f : _headingDeg) + angleOffsetDeg);
    }

    TrailRenderer MakeTrail(string name)
    {
        var go = new GameObject(name);
        // Parented to the scene root, not the (scaled) car, so width/position stay in world metres.
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = trailTime;
        tr.startWidth = trailWidth;
        tr.endWidth = trailWidth;
        tr.numCapVertices = 2;
        tr.minVertexDistance = 0.1f;
        tr.autodestruct = false;
        tr.emitting = false;
        tr.sortingOrder = trailSortingOrder;
        tr.material = trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default"));
        tr.startColor = trailColor;
        tr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        return tr;
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

        // --- Inputs: external (AI) when driven, otherwise gamepad first, keyboard overrides if pressed.
        float steerIn, throttleIn, brakeIn;
        if (externalInput)
        {
            steerIn = _inSteer; throttleIn = _inThrottle; brakeIn = _inBrake;
        }
        else
        {
            steerIn = 0f; throttleIn = 0f; brakeIn = 0f;
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
        }
        steerIn = Mathf.Clamp(steerIn, -1f, 1f);
        if (Mathf.Abs(steerIn) < steerDeadzone) steerIn = 0f;
        steerIn = Mathf.Sign(steerIn) * Mathf.Pow(Mathf.Abs(steerIn), steerExpo);

        // Steering → front-wheel angle, rate-limited and speed-scaled.
        float speedFraction = Mathf.Clamp01(SpeedMph / Mathf.Max(steerDecaySpeedMph, 1f));
        float speedAuthority = Mathf.Lerp(1f, highSpeedSteerScale, speedFraction);
        float desiredSteer = -steerIn * vehicleInfo.maxSteeringAngle * speedAuthority;
        _steerDeg = Mathf.MoveTowards(_steerDeg, desiredSteer, vehicleInfo.steeringRate * dt);
        // Bodywork damage spoils handling: a steering pull toward the battered side, plus grip/drag/top-speed below.
        float dmg = _bodywork != null ? _bodywork.DamageLevel : 0f;
        float delta = _steerDeg * Mathf.Deg2Rad;
        if (dmg > 0f && _bodywork != null) delta += _bodywork.DamageBiasX * dmg * damageSteerPull;

        // --- Surface + wheelspin state for this update.
        // On the track (or a tarmac runoff) the car has full grip. Off it, sample the runoff field: grass is
        // slippery, gravel bogs the car down. Unclassified off-track terrain defaults to grass.
        bool onTrackSurface = track == null || track.IsOnSurface(transform.position, out _);
        float surfGrip = 1f, surfPower = 1f, surfDrag = 0f;
        bool looseSurface = false;
        if (!onTrackSurface)
        {
            var surf = TrackEnvironment.SurfaceType.Grass;
            if (SurfaceField.TryGetSurface(transform.position, out var queried)) surf = queried;
            switch (surf)
            {
                case TrackEnvironment.SurfaceType.TarmacRunoff:
                    surfGrip = tarmacRunoffGrip; surfPower = 1f; surfDrag = 0f; looseSurface = false;
                    break;
                case TrackEnvironment.SurfaceType.Gravel:
                    surfGrip = gravelGrip; surfPower = gravelPower; surfDrag = gravelDrag; looseSurface = true;
                    break;
                default: // Grass (and unclassified off-track)
                    surfGrip = grassGrip; surfPower = grassPower; surfDrag = grassDrag; looseSurface = true;
                    break;
            }
        }
        _onGrass = looseSurface; // drives tyre-trail emission below

        float speedNow = SpeedMps;
        float wheelspin = 0f;
        if (enableWheelspin && throttleIn > wheelspinThrottle && speedNow < wheelspinSpeed)
            wheelspin = Mathf.Clamp01(
                ((throttleIn - wheelspinThrottle) / Mathf.Max(1f - wheelspinThrottle, 0.01f))
                * (1f - speedNow / Mathf.Max(wheelspinSpeed, 0.01f)));

        // --- Longitudinal command (engine/brake along the nose), evaluated from forward speed.
        float topMps = (vehicleInfo.topSpeed / 2.237f) * (1f - dmg * damageTopSpeedLoss);
        float accel = SampleAccel(_vx) * TrackConditions.PowerMultiplier * throttleIn;

        // Reverse: with no throttle, holding the brake once nearly stopped drives the car slowly backward.
        // Only engages within (-reverseMaxSpeed, reverseEngageSpeed) so a fast backward slide from a spin still
        // BRAKES (and keeps its momentum) instead of being snapped to the reverse cap.
        bool reversing = brakeIn > 0.05f && throttleIn < 0.05f && _vx < reverseEngageSpeed && _vx > -reverseMaxSpeed;

        float decel = reversing ? 0f : SampleDecel(_vx) * brakeIn; // brake suppressed while reversing (would fight it)
        if (throttleIn < 0.05f && brakeIn < 0.05f) decel += coastDecel;
        float reverseDrive = reversing ? -reverseAccel * brakeIn : 0f;

        // Surface power/drag (no-ops on the track: surfPower=1, surfDrag=0).
        accel *= surfPower;
        reverseDrive *= surfPower;
        // Rolling resistance ramps in with speed — zero at a standstill so the car can always crawl back off.
        if (surfDrag > 0f) decel += surfDrag * Mathf.Clamp01(speedNow / grassDragRampSpeed);
        if (dmg > 0f) decel += dmg * damageDragAdd;                  // bent bodywork drags
        accel *= (1f - wheelspinAccelLoss * wheelspin);              // spinning wheels put down less power
        float axCmd = accel + reverseDrive - decel; // commanded longitudinal accel (m/s²)

        // Available grip per axle: μ (proxied by maxLateralG) × track × tyre (wear+temperature), biased for balance.
        float trackGrip = TrackConditions.GripMultiplier;
        float tyreGripF = (enableWear && _tires != null) ? _tires.AxleGripFront : 1f;
        float tyreGripR = (enableWear && _tires != null) ? _tires.AxleGripRear : 1f;
        float dmgGrip = 1f - dmg * damageGripLoss;
        float muF = vehicleInfo.maxLateralG * trackGrip * tyreGripF * dmgGrip * (1f - understeerBias);
        float muR = vehicleInfo.maxLateralG * trackGrip * tyreGripR * dmgGrip * (1f + understeerBias);

        // Off-track surface cuts both axles; lit-up rears lose extra grip so the car rotates.
        muF *= surfGrip;
        muR *= surfGrip * Mathf.Lerp(1f, wheelspinRearGrip, wheelspin);

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
                _lastAy = ay;
                // Yaw damper opposes rotation (stabilises oversteer); extra damping under braking kills spin-on-the-brakes.
                float yawDamp = yawDamping + brakeYawDamping * brakeIn;
                float dr = (_a * fyF - _b * fyR) / _iz - yawDamp * _r; // yaw accel from front/rear moment + damping
                _vy += ay * h;
                _r += dr * h;
                // Velocity-vector rotation (Coriolis), paired with the -_vx*_r term in ay so it conserves speed:
                // this is what carries momentum through a spin (incl. into reverse). Only valid here where the
                // counter-term exists — doing it in the low-speed branch (where _vy is killed kinematically) pumped
                // _vx and launched the car after a spin.
                _vx += _vy * _r * h;
            }
            else
            {
                // Low-speed: kinematic yaw from geometry, bleed lateral velocity (no meaningful slip at crawl).
                float rKin = (_vx / (_a + _b)) * Mathf.Tan(delta);
                if (wheelspin > 0f)
                {
                    // Rear tyres lit up: pivot toward steering lock so a donut can be held from near standstill.
                    float lockFrac = Mathf.Clamp(_steerDeg / Mathf.Max(vehicleInfo.maxSteeringAngle, 1f), -1f, 1f);
                    float targetR = rKin + lockFrac * wheelspinYawRate * Mathf.Deg2Rad * wheelspin;
                    _r = Mathf.MoveTowards(_r, targetR, wheelspinYawAccel * Mathf.Deg2Rad * h);
                    _vy = Mathf.MoveTowards(_vy, 0f, 4f * h); // looser so the tail steps out
                }
                else
                {
                    _r = Mathf.MoveTowards(_r, rKin, 8f * h);
                    _vy = Mathf.MoveTowards(_vy, 0f, 12f * h);
                }
                _alphaF = 0f; _alphaR = 0f;
            }

            // Engine/brake along the nose. Engine drives forward only; brake + coast oppose the current travel
            // direction without pushing the car through zero into reverse (so backward momentum from a spin is kept,
            // not deleted and then "shot forward" under throttle). The velocity-vector rotation lives in the tyre
            // branch above.
            _vx += accel * h;
            _vx += reverseDrive * h;
            float resist = decel * h;
            if (_vx > 0f) _vx = Mathf.Max(0f, _vx - resist);
            else if (_vx < 0f) _vx = Mathf.Min(0f, _vx + resist);
            _vx = Mathf.Clamp(_vx, -topMps, topMps);
            if (reversing) _vx = Mathf.Max(_vx, -reverseMaxSpeed); // cap input-driven reverse, not spin momentum
            if (_vx > speedGovernorMps) _vx = speedGovernorMps;    // formation/caution pace cap (forward only)

            // Integrate heading by yaw rate.
            _headingDeg += _r * Mathf.Rad2Deg * h;
        }

        // --- Tyre wear + temperature update (worked/hot tyres fade and shift the balance).
        if (enableWear && _tires != null && SpeedMph > 1f)
        {
            float latNorm = Mathf.Clamp(_lastAy / Mathf.Max(vehicleInfo.maxLateralG * G, 0.1f), -1f, 1f);
            _tires.Tick(dt, (wearAccumF / subSteps) * wearRateScale, (wearAccumR / subSteps) * wearRateScale, SpeedMps, latNorm);
            wearFront = _tires.FrontWear; // mirror for telemetry / existing readouts
            wearRear = _tires.RearWear;
        }

        // --- Integrate world position from body velocity rotated into world space.
        float hr = _headingDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(hr), sin = Mathf.Sin(hr);
        Vector2 worldVel = new Vector2(_vx * cos - _vy * sin, _vx * sin + _vy * cos);
        Vector2 newPos = (Vector2)transform.position + worldVel * dt;
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? _headingDeg - 90f : _headingDeg) + angleOffsetDeg);

        UpdateTrails();
    }

    // Park the two trail emitters over the rear tyres (world metres) and emit only while sliding on grass.
    void UpdateTrails()
    {
        if (!grassTrails || _trailL == null) return;
        float hr = _headingDeg * Mathf.Deg2Rad;
        Vector2 fwd = new Vector2(Mathf.Cos(hr), Mathf.Sin(hr));
        Vector2 nrm = new Vector2(fwd.y, -fwd.x); // right of travel
        Vector2 rear = (Vector2)transform.position - fwd * _b;
        float z = transform.position.z;

        Vector2 lp = rear + nrm * (trailTrackWidth * 0.5f);
        Vector2 rp = rear - nrm * (trailTrackWidth * 0.5f);
        bool emit = _onGrass && SpeedMps > 0.5f;

        if (emit && !_wasEmitting)
        {
            // Snap to the tyre positions and wipe history so re-enabling doesn't draw a streak from the last spot.
            _trailL.transform.position = new Vector3(lp.x, lp.y, z);
            _trailR.transform.position = new Vector3(rp.x, rp.y, z);
            _trailL.Clear();
            _trailR.Clear();
        }

        _trailL.transform.position = new Vector3(lp.x, lp.y, z);
        _trailR.transform.position = new Vector3(rp.x, rp.y, z);
        _trailL.emitting = emit;
        _trailR.emitting = emit;
        _wasEmitting = emit;
    }

    void OnDestroy()
    {
        if (_trailL != null) Destroy(_trailL.gameObject);
        if (_trailR != null) Destroy(_trailR.gameObject);
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

    // Car-vs-car impact. The velocity change is supplied by VehicleCollision (a proper 2-body impulse), so we
    // just add it — no wall-style reflection. This is what makes a fast car shunt a stationary one forward and
    // shed its own speed, instead of bouncing straight back off a car that never moves.
    public void ApplyCarImpact(Vector2 worldMtv, Vector2 contactPoint, Vector2 worldDeltaV, float severity)
    {
        transform.position += new Vector3(worldMtv.x, worldMtv.y, 0f); // depenetrate

        // Add the world-space velocity change in the body frame (body = Rᵀ·world).
        float hr = _headingDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(hr), sin = Mathf.Sin(hr);
        _vx += worldDeltaV.x * cos + worldDeltaV.y * sin;
        _vy += -worldDeltaV.x * sin + worldDeltaV.y * cos;

        // Off-centre hit imparts yaw spin: lever arm × impulse (impulse = mass × Δv) / yaw inertia.
        Vector2 rArm = contactPoint - (Vector2)transform.position;
        float dvMag = worldDeltaV.magnitude;
        if (dvMag > 1e-5f)
        {
            Vector2 n = worldDeltaV / dvMag;
            float impulse = dvMag * Mass;
            float crossZ = rArm.x * n.y - rArm.y * n.x;
            float deltaOmega = (crossZ * impulse / Mathf.Max(_iz, 1f)) * spinSensitivity;
            float cap = maxImpactSpinDeg * Mathf.Deg2Rad;
            _r += Mathf.Clamp(deltaOmega, -cap, cap);
        }
    }

    public void PitResetTyres() { wearFront = 0f; wearRear = 0f; if (_tires != null) _tires.PitReset(); }

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
