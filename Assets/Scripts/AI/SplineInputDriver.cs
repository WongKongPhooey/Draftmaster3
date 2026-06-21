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
    [Tooltip("Throttle/brake per m/s of speed error (produces a 0..1 input). Higher = snappier speed tracking.")]
    public float speedGain = 0.5f;
    [Tooltip("Below this speed (m/s) steering authority ramps down to avoid low-speed wobble / spin.")]
    public float lowSpeedCutoff = 6f;

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

    SplineDriver _spline;
    PlayerVehicleController _car;
    bool _seeded;

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
    }

    void FixedUpdate()
    {
        if (_spline == null || _car == null || _spline.track == null || _spline.vehicleInfo == null) return;

        // Don't seed until SplineDriver has actually placed the car (TrackLength>0 means Rebuild has run).
        if (_spline.TrackLength <= 0f) return;

        Vector3 seedWorld = _spline.track.transform.TransformPoint(
            new Vector3(_spline.CommandedLocalPos.x, _spline.CommandedLocalPos.y, 0f));

        // Place the car on the grid/pit and align heading before handing over to the dynamic model.
        // While the field is held PreGrid, keep re-seeding: SplineDriver.Rebuild can populate the spline
        // before its Start computes the real pit-box pose, so a one-shot seed can latch the wrong (origin)
        // pose on a frozen car. Re-seeding every PreGrid frame pins the car to its box once Start runs.
        if (!_seeded || RaceStart.Current == RaceStart.Phase.PreGrid)
        {
            _car.SeedPose(new Vector2(seedWorld.x, seedWorld.y), _spline.CommandedHeadingDeg, _spline.CommandedSpeedMps);
            _seeded = true;
            return;
        }

        float speed = _car.SpeedMps;

        // --- Steering: pure-pursuit toward a point AHEAD on the racing line. Lookahead scales with speed, then
        // is capped by the tightest upcoming turn radius so the car aims short into tight corners (sharp turn-in,
        // no running wide) while keeping a long smooth aim on straights and gradual sweepers.
        float lookahead = Mathf.Clamp(speed * lookaheadTime, lookaheadMin, lookaheadMax);
        float radius = _spline.CurvatureRadiusAhead(curvatureScan);
        if (radius < float.MaxValue)
            lookahead = Mathf.Min(lookahead, Mathf.Max(lookaheadMin, radius * radiusLookaheadFactor));
        Vector2 aheadLocal = _spline.PathPointAhead(lookahead);
        Vector3 targetWorld = _spline.track.transform.TransformPoint(new Vector3(aheadLocal.x, aheadLocal.y, 0f));
        Vector2 toTarget = (Vector2)targetWorld - (Vector2)transform.position;
        float steerInput = 0f;
        if (toTarget.sqrMagnitude > 1e-4f)
        {
            float bearingDeg = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float headingError = Mathf.DeltaAngle(_car.HeadingDeg, bearingDeg);
            float authority = Mathf.Clamp01(speed / Mathf.Max(lowSpeedCutoff, 0.1f));
            float maxSteer = Mathf.Max(_spline.vehicleInfo.maxSteeringAngle, 1f);
            float steerAngleDeg = Mathf.Clamp(headingError * steerGain, -maxSteer, maxSteer) * authority;
            // PlayerVehicleController maps desiredSteer = -steerIn * maxSteeringAngle, so invert to request this angle.
            steerInput = Mathf.Clamp(-steerAngleDeg / maxSteer, -1f, 1f);
        }

        // --- Speed: throttle when under the commanded speed, brake when over.
        float speedError = _spline.CommandedSpeedMps - speed;
        float throttle = Mathf.Clamp01(speedError * speedGain);
        float brake = Mathf.Clamp01(-speedError * speedGain);

        _car.SetInput(steerInput, throttle, brake);

        // Feed actual speed back so the brain advances its path point with the real car (prevents the commanded
        // point running away when the car is slowed by contact/understeer).
        _spline.externalActualSpeedMps = speed;
    }
}
