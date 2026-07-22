using UnityEngine;

// AI input provider. Turns SplineDriver's commanded path into steer/throttle/brake for the SHARED dynamic model
// (PlayerVehicleController) — so AI drive with the exact tyre model and feel as the player. SplineDriver stays
// the "brain" (racing line, speed profile, AIRacingBehaviour, drafting); this only translates its targets into
// pure-pursuit steering + speed-error throttle/brake. Replaces the old kinematic BicycleDynamics for spawned cars.
//
// Runs before PlayerVehicleController (order 0) so inputs are set for the same physics step.
[RequireComponent(typeof(SplineDriver), typeof(PlayerVehicleController))]
[DefaultExecutionOrder(-50)]
public class SplineInputDriver : MonoBehaviour
{
    [Tooltip("Steering response: heading error (deg) is scaled by this, then clamped to the steering limit and a low-speed ramp. Higher = sharper correction (twitchier).")]
    public float steerGain = 1.5f;
    [Tooltip("Throttle/brake per m/s of speed error (produces a 0..1 input). Higher = snappier speed tracking. Too low makes the AI feel gutless: throttle sags as they close on the target speed, so they cruise 1-2 m/s under it and never use full power.")]
    public float speedGain = 2f;
    [Tooltip("Below this speed (m/s) steering authority ramps down to avoid low-speed wobble / spin.")]
    public float lowSpeedCutoff = 6f;
    [Tooltip("Derivative damping (s) on the heading error: counters how fast the error is CHANGING, killing the high-frequency tail wiggle of the P-only chase (visible at pace-lap speeds where the weave caps the lookahead short). Acts on the error rate, not raw yaw rate, so steady-state cornering — constant error, constant yaw — is untouched.")]
    public float steerDamping = 0.08f;

    [Header("Lookahead")]
    [Tooltip("Seconds of travel ahead to aim the steering at. The pure-pursuit target sits speed*this metres up the racing line; bigger = smoother but lazier turn-in.")]
    public float lookaheadTime = 0.45f;
    [Tooltip("Minimum lookahead distance (m), used at low speed and on the tightest turns.")]
    public float lookaheadMin = 4f;
    [Tooltip("Maximum lookahead distance (m), capping it on the straights.")]
    public float lookaheadMax = 22f;
    [Tooltip("On a turn, the lookahead is capped to this fraction of the turn radius — so tight corners get a short, sharp aim point (turn in hard) while gradual ones keep a long smooth one. Lower = sharper turn-in.")]
    public float radiusLookaheadFactor = 0.55f;
    [Tooltip("Distance ahead (m) scanned for the tightest turn radius that caps the lookahead.")]
    public float curvatureScan = 35f;

    [Header("Grip Guard")]
    [Tooltip("Fraction of the LIVE lateral-grip limit the speed governor lets the car use before capping the commanded speed. The baked speed profile can't see live tyre wear or grip-slider changes mid-race; this can. >1 disables the safety net in practice.")]
    [Range(0.5f, 1.2f)] public float gripUtilization = 0.95f;
    [Tooltip("Seconds of travel scanned ahead for the tightest radius feeding the grip governor — enough anticipation to lift before the corner, short enough not to crawl whole straights.")]
    public float gripScanTime = 0.7f;

    [Header("Slide Catch")]
    [Tooltip("Body slip angle (deg) where the slide response starts: throttle tapers and steering starts aiming the VELOCITY vector at the path (countersteer) instead of winding more lock onto a nose that's already pointing the wrong way.")]
    public float slideStartDeg = 6f;
    [Tooltip("Body slip angle (deg) of a full slide: throttle fully cut, steering fully course-based.")]
    public float slideFullDeg = 16f;

    [Header("Recovery")]
    [Tooltip("Heading error (deg) beyond which the car counts as spun/lost and enters recovery: creep speed, full steering authority, short aim point until realigned. Stops a spun car doing full-throttle donuts at its old target speed.")]
    public float recoveryEnterDeg = 60f;
    [Tooltip("Heading error (deg) below which recovery ends (hysteresis so it doesn't flicker).")]
    public float recoveryExitDeg = 25f;
    [Tooltip("Speed cap (m/s) while recovering.")]
    public float recoverySpeedMps = 8f;

    SplineDriver _spline;
    PlayerVehicleController _car;
    TireModel _tireModel;
    TireState _tireState;
    bool _seeded;
    bool _recovering;
    float _prevHeadingError;
    bool _hasPrevError;

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        _car = GetComponent<PlayerVehicleController>();
    }

    void OnEnable()
    {
        // SplineDriver stops writing the transform (the dynamic model owns it); PlayerVehicleController takes AI inputs.
        if (_spline != null) _spline.externalMotionController = true;
        if (_car != null) _car.externalInput = true;
        // Re-seed whenever re-enabled (e.g. handing back from a kinematic formation lap) so the car picks up
        // its current spline pose + speed instead of driving on from a stale internal state.
        _seeded = false;
        _recovering = false;
        _hasPrevError = false;
    }

    void FixedUpdate()
    {
        if (_spline == null || _car == null || _spline.track == null || _spline.vehicleInfo == null) return;

        // Don't seed until SplineDriver has actually placed the car (TrackLength>0 means Rebuild has run).
        if (_spline.TrackLength <= 0f) return;

        Vector3 seedWorld = _spline.track.transform.TransformPoint(
            new Vector3(_spline.CommandedLocalPos.x, _spline.CommandedLocalPos.y, 0f));

        // Place the car on the grid/pit and align heading before handing over to the dynamic model.
        // While the car is held still, keep re-seeding: SplineDriver.Rebuild can populate the spline
        // before its Start computes the real pit-box pose, so a one-shot seed can latch the wrong (origin)
        // pose on a frozen car. Re-seeding every held frame pins the car to its box once Start runs, and
        // SeedPose zeroes the model's velocity so a parked car can't creep or be shoved out of its box by
        // a neighbour. This tracks IsHeldStill, not the race phase: practice parks cars with parkedHold
        // while the phase is already Green, so a PreGrid-only test let the whole pit lane roll.
        if (!_seeded || _spline.IsHeldStill)
        {
            // Tyre components are added by other Start()s after our Awake — keep re-resolving while held.
            if (_tireModel == null) _tireModel = GetComponent<TireModel>();
            if (_tireState == null) _tireState = GetComponent<TireState>();
            _car.SeedPose(new Vector2(seedWorld.x, seedWorld.y), _spline.CommandedHeadingDeg, _spline.CommandedSpeedMps);
            _seeded = true;
            return;
        }

        float speed = _car.SpeedMps;

        // --- Slide state: body slip angle is how far the velocity vector points away from the nose. Past
        // slideStartDeg the car is sliding — winding on more lock or more throttle only makes it worse.
        float slipDeg = _car.SlipAngleDeg;
        float slide01 = Mathf.InverseLerp(slideStartDeg, Mathf.Max(slideFullDeg, slideStartDeg + 0.1f), Mathf.Abs(slipDeg));

        // --- Steering: pure-pursuit toward a point AHEAD on the racing line. Lookahead scales with speed, then
        // is capped by the tightest upcoming turn radius so the car aims short into tight corners (sharp turn-in,
        // no running wide) while keeping a long smooth aim on straights and gradual sweepers.
        float lookahead = Mathf.Clamp(speed * lookaheadTime, lookaheadMin, lookaheadMax);
        float radius = _spline.CurvatureRadiusAhead(curvatureScan);
        if (radius < float.MaxValue)
            lookahead = Mathf.Min(lookahead, Mathf.Max(lookaheadMin, radius * radiusLookaheadFactor));
        if (_recovering) lookahead = lookaheadMin; // aim close: get back ON the line, not down the road
        Vector2 aheadLocal = _spline.PathPointAhead(lookahead);
        Vector3 targetWorld = _spline.track.transform.TransformPoint(new Vector3(aheadLocal.x, aheadLocal.y, 0f));
        Vector2 toTarget = (Vector2)targetWorld - (Vector2)transform.position;
        float steerInput = 0f;
        float noseErrorDeg = 0f;
        if (toTarget.sqrMagnitude > 1e-4f)
        {
            float bearingDeg = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            noseErrorDeg = Mathf.DeltaAngle(_car.HeadingDeg, bearingDeg);

            // Recovery hysteresis: a spun car (nose way off the path direction) creeps and steers hard until
            // realigned, instead of chasing its old commanded speed while pointing across the track.
            if (!_recovering && Mathf.Abs(noseErrorDeg) > recoveryEnterDeg) _recovering = true;
            else if (_recovering && Mathf.Abs(noseErrorDeg) < recoveryExitDeg) _recovering = false;

            // Steering reference blends from the nose to the COURSE (nose + slip) as a slide builds: at full
            // slide the controller aims the velocity vector at the path — natural countersteer — rather than
            // adding lock to a nose the tyres have already let go of.
            float refHeadingDeg = _car.HeadingDeg + slipDeg * slide01;
            float headingError = Mathf.DeltaAngle(refHeadingDeg, bearingDeg);
            float authority = Mathf.Clamp01(speed / Mathf.Max(lowSpeedCutoff, 0.1f));
            // The low-speed ramp exists to stop wobble at crawl in normal running; a recovering car NEEDS lock
            // from near-standstill (the kinematic low-speed branch is stable), so keep most of the authority.
            if (_recovering) authority = Mathf.Max(authority, 0.6f);
            float maxSteer = Mathf.Max(_spline.vehicleInfo.maxSteeringAngle, 1f);
            // PD: the derivative term opposes the error's rate of change. Clamped so a one-frame target jump
            // (lane rejoin, recovery toggle) can't spike the steering; skipped on the first sample after a seed.
            float errorRate = 0f;
            if (_hasPrevError && Time.fixedDeltaTime > 0f)
                errorRate = Mathf.Clamp(Mathf.DeltaAngle(_prevHeadingError, headingError) / Time.fixedDeltaTime, -180f, 180f);
            _prevHeadingError = headingError;
            _hasPrevError = true;
            float steerAngleDeg = Mathf.Clamp(headingError * steerGain + errorRate * steerDamping, -maxSteer, maxSteer) * authority;
            // PlayerVehicleController maps desiredSteer = -steerIn * maxSteeringAngle, so invert to request this angle.
            steerInput = Mathf.Clamp(-steerAngleDeg / maxSteer, -1f, 1f);
        }

        // --- Speed: throttle when under the commanded speed, brake when over.
        float commandedMps = _spline.CommandedSpeedMps;

        // Grip governor: never command more speed than the LIVE friction circle can hold on the tightest radius
        // coming up within the scan window. The baked profile used spawn-time grip — tyre wear, the grip slider,
        // and pace-stretched targets can all push it past what the physics can actually corner at; this cap is
        // what keeps the car under its limit instead of understeering off the road.
        if (!_spline.IsOnPit)
        {
            float aLatMax = LiveLateralAccelLimitMps2();
            float gripRadius = _spline.CurvatureRadiusAhead(Mathf.Max(8f, speed * gripScanTime));
            if (aLatMax > 0.1f && gripRadius < float.MaxValue)
            {
                float vGrip = Mathf.Sqrt(aLatMax * gripUtilization * gripRadius);
                if (commandedMps > vGrip) commandedMps = vGrip;
            }
        }

        if (_recovering) commandedMps = Mathf.Min(commandedMps, recoverySpeedMps);

        float speedError = commandedMps - speed;
        // A slide cuts throttle (power past saturated rears just rotates the car further) and softens the brake
        // (forward weight transfer unloads the rear mid-slide) — the tyres get their lateral budget back to catch it.
        float throttle = Mathf.Clamp01(speedError * speedGain) * (1f - slide01);
        float brake = Mathf.Clamp01(-speedError * speedGain) * (1f - 0.5f * slide01);

        _car.SetInput(steerInput, throttle, brake);

        // Feed actual speed back so the brain advances its path point with the real car (prevents the commanded
        // point running away when the car is slowed by contact/understeer).
        _spline.externalActualSpeedMps = speed;
    }

    // Lateral-accel ceiling (m/s²) the dynamic model can generate RIGHT NOW: maxLateralG × the same grip
    // multipliers PlayerVehicleController folds into its friction circle (global+AI track conditions, live tyre state).
    float LiveLateralAccelLimitMps2()
    {
        var vi = _spline.vehicleInfo;
        if (vi == null || vi.maxLateralG <= 0.01f) return 0f;
        float grip = TrackConditions.AiEffective;
        if (_tireModel != null) grip *= _tireModel.OverallGrip;
        else if (_tireState != null) grip *= _tireState.GripMultiplier;
        return vi.maxLateralG * Mathf.Max(0.05f, grip) * 9.81f;
    }
}
