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

        Vector3 targetWorld = _spline.track.transform.TransformPoint(
            new Vector3(_spline.CommandedLocalPos.x, _spline.CommandedLocalPos.y, 0f));

        // Place the car on the grid/pit and align heading before handing over to the dynamic model.
        // While the field is held PreGrid, keep re-seeding: SplineDriver.Rebuild can populate the spline
        // before its Start computes the real pit-box pose, so a one-shot seed can latch the wrong (origin)
        // pose on a frozen car. Re-seeding every PreGrid frame pins the car to its box once Start runs.
        if (!_seeded || RaceStart.Current == RaceStart.Phase.PreGrid)
        {
            _car.SeedPose(new Vector2(targetWorld.x, targetWorld.y), _spline.CommandedHeadingDeg, _spline.CommandedSpeedMps);
            _seeded = true;
            return;
        }

        float speed = _car.SpeedMps;

        // --- Steering: pure-pursuit toward the commanded path point.
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
