using UnityEngine;
using Draftmaster.Sim;

// Gives the race camera some weight. Bolted onto the camera alongside CameraFollow, which ticks it and adds
// the results on top of its own follow position — so this never fights the follow, the pit-lane zoom, or
// whatever else is pointing the camera that frame.
//
// Two effects:
//   Lean  — the camera drops back under braking, pushes ahead under power and slides toward the inside of a
//           corner, with a touch of roll. All of it is smoothed, so it reads as the view settling rather than
//           snapping about. Driven by the car's real accelerations (PlayerVehicleController telemetry), or by
//           differentiating the followed transform when the car is kinematic (a broadcast AI, a net puppet).
//   Shake — contacts add to a 0..1 trauma budget that decays on its own; the camera rattles by trauma² and
//           takes a directional kick away from whatever it hit. A wall scrape trickles trauma in, so grinding
//           down the barrier rumbles without ever reaching full-shunt violence.
//
// Only cars get the treatment: an on-foot target (the pit walk, the paddock) has none of these components and
// is followed dead straight, as before.
[DisallowMultipleComponent]
public class DrivingCameraFeel : MonoBehaviour
{
    [Header("Lean")]
    [Tooltip("Master switch for the braking / steering / throttle lean.")]
    public bool enableLean = true;
    [Tooltip("Metres the camera pushes ahead of the nose at full acceleration (and drops back at full braking).")]
    public float longitudinalLean = 2.5f;
    [Tooltip("Metres the camera slides toward the inside of a corner at full lateral load.")]
    public float lateralLean = 2f;
    [Tooltip("Degrees of camera roll at full lateral load. Negative rolls the other way.")]
    public float maxRollDeg = 2f;
    [Tooltip("Acceleration (in g) that counts as 'full'. Higher = the lean saves itself for the big moments.")]
    public float referenceG = 1.1f;
    [Tooltip("How fast the lean chases the car, in e-folds per second. Low = languid, high = twitchy.")]
    public float leanResponse = 5f;
    [Tooltip("Speed (mph) below which the lean fades out, so a parked or pitting car sits square.")]
    public float leanFadeInMph = 12f;
    [Tooltip("Metres the lean is allowed to reach in total. Stops a big slide walking the car off screen.")]
    public float maxLeanMetres = 4f;

    [Header("Impact shake")]
    [Tooltip("Master switch for impact shake.")]
    public bool enableShake = true;
    [Tooltip("Contact severity (0..1) below which nothing shakes. Keeps grid jostling quiet.")]
    [Range(0f, 1f)] public float impactMinSeverity = 0.06f;
    [Tooltip("Trauma added by a full-severity impact. Over 1 means the biggest hits saturate instantly.")]
    public float impactTrauma = 1.3f;
    [Tooltip("Shake from car-vs-car contact, relative to hitting a barrier.")]
    [Range(0f, 1f)] public float carContactScale = 0.7f;
    [Tooltip("Trauma per second while scraping a barrier at full scrape speed.")]
    public float scrapeTraumaPerSecond = 0.9f;
    [Tooltip("Scrape speed (m/s) at which the rumble is at full strength.")]
    public float scrapeFullSpeed = 20f;
    [Tooltip("Trauma bled off per second. Higher = shorter, snappier shakes.")]
    public float traumaDecay = 1.7f;
    [Tooltip("Metres of rattle at full trauma.")]
    public float shakeMetres = 1.2f;
    [Tooltip("Degrees of roll rattle at full trauma.")]
    public float shakeRollDeg = 1.6f;
    [Tooltip("Rattle frequency, Hz-ish. Higher = buzzier.")]
    public float shakeFrequency = 26f;
    [Tooltip("Metres the camera is punched away from a full-severity impact before springing back.")]
    public float kickMetres = 1.6f;
    [Tooltip("How fast the directional punch springs back, in e-folds per second.")]
    public float kickDecay = 7f;

    // What CameraFollow adds on top of its follow position / rotation this frame.
    public Vector3 PositionOffset { get; private set; }
    public float RollDegrees { get; private set; }
    // 0..1 shake budget, exposed for debug readouts.
    public float Trauma => _trauma;

    Transform _target;
    PlayerVehicleController _pvc;
    VehicleCollision _collision;
    bool _isCar;

    Vector2 _lean;      // body-frame lean, metres (x = along the nose, y = body-left)
    float _roll;        // smoothed lean roll, degrees
    float _trauma;
    Vector2 _kick;      // decaying directional punch, world space
    float _pendingImpact, _pendingScrape;
    int _seed;

    // Kinematic-target fallback: differentiate the transform when there is no controller to ask.
    Vector2 _prevPos;
    float _prevSpeed, _prevHeadingDeg, _fallbackAx, _fallbackAy, _fallbackSpeed, _fallbackHeadingDeg;
    bool _hasPrev;

    void Awake() => _seed = Random.Range(1, 9973);

    void OnEnable()
    {
        if (_collision != null) { _collision.Contacted -= OnContact; _collision.Contacted += OnContact; }
    }

    void OnDisable()
    {
        if (_collision != null) _collision.Contacted -= OnContact;
        PositionOffset = Vector3.zero;
        RollDegrees = 0f;
        ResetState();
    }

    void OnDestroy()
    {
        if (_collision != null) _collision.Contacted -= OnContact;
    }

    // Driven by CameraFollow so the ordering is deterministic (a second LateUpdate would race it).
    public void Tick(Transform target, float dt)
    {
        if (target != _target) Bind(target);
        if (!_isCar || dt <= 0f)
        {
            PositionOffset = Vector3.zero;
            RollDegrees = 0f;
            return;
        }

        ReadMotion(dt, out float speedMph, out float headingDeg, out float ax, out float ay);

        Vector2 forward = new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        Vector2 left = new Vector2(-forward.y, forward.x);

        // --- Lean.
        Vector2 leanTarget = Vector2.zero;
        float rollTarget = 0f;
        if (enableLean)
        {
            float fade = CameraFeel.LeanFade(speedMph, leanFadeInMph);
            leanTarget = CameraFeel.LeanOffset(ax, ay, referenceG, longitudinalLean, lateralLean) * fade;
            rollTarget = CameraFeel.RollDegrees(ay, referenceG, maxRollDeg) * fade;
        }
        _lean.x = CameraFeel.Approach(_lean.x, leanTarget.x, leanResponse, dt);
        _lean.y = CameraFeel.Approach(_lean.y, leanTarget.y, leanResponse, dt);
        _roll = CameraFeel.Approach(_roll, rollTarget, leanResponse, dt);
        _lean = Vector2.ClampMagnitude(_lean, Mathf.Max(0f, maxLeanMetres));

        Vector2 world = forward * _lean.x + left * _lean.y;

        // --- Shake.
        float shakeRoll = 0f;
        if (enableShake)
        {
            _trauma = CameraFeel.DecayTrauma(_trauma, traumaDecay, dt);
            if (_pendingImpact >= impactMinSeverity) _trauma = CameraFeel.AddTrauma(_trauma, _pendingImpact * impactTrauma);
            if (_pendingScrape > 0f) _trauma = CameraFeel.AddTrauma(_trauma, _pendingScrape * scrapeTraumaPerSecond * dt);

            world += CameraFeel.ShakeOffset(_trauma, shakeMetres, Time.time, shakeFrequency, _seed);
            shakeRoll = CameraFeel.ShakeRoll(_trauma, shakeRollDeg, Time.time, shakeFrequency, _seed);

            _kick = Vector2.Lerp(_kick, Vector2.zero, 1f - Mathf.Exp(-Mathf.Max(0.01f, kickDecay) * dt));
            world += _kick;
        }
        else
        {
            _trauma = 0f;
            _kick = Vector2.zero;
        }
        _pendingImpact = 0f;
        _pendingScrape = 0f;

        PositionOffset = new Vector3(world.x, world.y, 0f);
        RollDegrees = _roll + shakeRoll;
    }

    // Hit something: worldNormal points from the car toward whatever it hit, severity is 0..1.
    public void AddImpact(float severity, Vector2 worldNormal)
    {
        severity = Mathf.Clamp01(severity);
        if (severity < impactMinSeverity) return;
        _pendingImpact = Mathf.Max(_pendingImpact, severity);
        // Punch away from the obstacle, the way the car itself is thrown, then spring back.
        if (worldNormal.sqrMagnitude > 1e-6f)
        {
            _kick -= worldNormal.normalized * (severity * kickMetres);
            _kick = Vector2.ClampMagnitude(_kick, Mathf.Max(0f, kickMetres));
        }
    }

    void OnContact(VehicleCollision.ContactEvent c)
    {
        if (!enableShake) return;
        AddImpact(Mathf.Clamp01(c.severity) * (c.otherIsCar ? carContactScale : 1f), c.normal);
        if (scrapeFullSpeed > 0f)
            _pendingScrape = Mathf.Max(_pendingScrape, Mathf.Clamp01(c.scrapeSpeed / scrapeFullSpeed));
    }

    void Bind(Transform target)
    {
        if (_collision != null) _collision.Contacted -= OnContact;
        _target = target;
        _pvc = null;
        _collision = null;
        _isCar = false;
        ResetState();
        if (target == null) return;

        _pvc = target.GetComponent<PlayerVehicleController>();
        _collision = target.GetComponent<VehicleCollision>();
        // A SplineDriver-only car is kinematic, but it is still a car — differentiate its transform instead.
        _isCar = _pvc != null || _collision != null || target.GetComponent<SplineDriver>() != null;
        if (_collision != null && isActiveAndEnabled) _collision.Contacted += OnContact;
    }

    void ResetState()
    {
        _lean = Vector2.zero;
        _roll = 0f;
        _trauma = 0f;
        _kick = Vector2.zero;
        _pendingImpact = 0f;
        _pendingScrape = 0f;
        _hasPrev = false;
        _fallbackAx = _fallbackAy = _fallbackSpeed = 0f;
    }

    void ReadMotion(float dt, out float speedMph, out float headingDeg, out float ax, out float ay)
    {
        if (_pvc != null)
        {
            speedMph = _pvc.SpeedMph;
            headingDeg = _pvc.HeadingDeg;
            ax = _pvc.LongitudinalAccel;
            ay = _pvc.LateralAccel;
            _hasPrev = false; // so a later fall back to differentiation cannot use a stale sample
            return;
        }

        // No controller to ask: recover speed, heading and both accelerations from the transform. Raw
        // differences are noisy at frame rate, so everything is low-passed before it reaches the lean.
        Vector2 pos = _target.position;
        if (!_hasPrev)
        {
            _prevPos = pos;
            _prevSpeed = 0f;
            _prevHeadingDeg = _fallbackHeadingDeg = _target.eulerAngles.z;
            _hasPrev = true;
        }

        Vector2 vel = (pos - _prevPos) / dt;
        float speed = vel.magnitude;
        if (speed > 0.2f) _fallbackHeadingDeg = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        _prevPos = pos;

        float rawAx = (speed - _prevSpeed) / dt;
        float yawRate = Mathf.DeltaAngle(_prevHeadingDeg, _fallbackHeadingDeg) * Mathf.Deg2Rad / dt;
        _prevSpeed = speed;
        _prevHeadingDeg = _fallbackHeadingDeg;

        _fallbackSpeed = CameraFeel.Approach(_fallbackSpeed, speed, 10f, dt);
        _fallbackAx = CameraFeel.Approach(_fallbackAx, rawAx, 6f, dt);
        _fallbackAy = CameraFeel.Approach(_fallbackAy, speed * yawRate, 6f, dt);

        speedMph = _fallbackSpeed * 2.237f;
        headingDeg = _fallbackHeadingDeg;
        ax = _fallbackAx;
        ay = _fallbackAy;
    }
}
