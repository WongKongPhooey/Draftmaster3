using UnityEngine;

/// <summary>
/// Derives engine RPM and the current gear from road speed, runs sequential shift logic, and exposes a
/// torque-based acceleration multiplier. This is a SOUND/FEEL layer: it does not move the car itself. The
/// motion driver (SplineDriver) reads <see cref="AccelMultiplier"/> to shape acceleration, and EngineAudio
/// reads <see cref="Rpm"/>/<see cref="Load01"/> to drive a multi-sample engine note.
///
/// Speed comes from any <see cref="IVehicleSpeedReadout"/> on the same GameObject (SplineDriver implements it).
/// Gear/RPM data comes from <see cref="vehicleInfo"/> — auto-pulled from a SplineDriver if not assigned.
/// </summary>
public class EngineGearbox : MonoBehaviour
{
    [Tooltip("Gear/RPM data. If left empty, pulled from a SplineDriver on this GameObject.")]
    public VehicleInfo vehicleInfo;

    [Tooltip("Engine RPM is smoothed toward its target by this time constant (s). Lower = snappier throttle response.")]
    [Range(0f, 0.5f)] public float rpmSmoothing = 0.06f;

    // ---- Outputs (read by SplineDriver + EngineAudio) ----
    /// <summary>Current (smoothed) engine RPM.</summary>
    public float Rpm { get; private set; }
    /// <summary>RPM mapped to 0..1 across idle..redline.</summary>
    public float Rpm01 { get; private set; }
    /// <summary>Zero-based current gear index.</summary>
    public int Gear { get; private set; }
    /// <summary>Human gear number (1..N), 0 if no gearbox data.</summary>
    public int GearNumber => HasGears ? Gear + 1 : 0;
    /// <summary>True for the brief drive interruption during a shift.</summary>
    public bool IsShifting => _shiftTimer > 0f;
    /// <summary>Engine load 0..1: 1 under power, ~0 coasting/engine-braking. Drives audio brightness/volume.</summary>
    public float Load01 { get; private set; }
    /// <summary>Acceleration multiplier the motion driver applies: torque-curve shape × shift drive-cut.</summary>
    public float AccelMultiplier { get; private set; } = 1f;
    /// <summary>+1 just upshifted, -1 just downshifted, 0 otherwise. Cleared after one frame — poll for SFX.</summary>
    public int ShiftEvent { get; private set; }
    /// <summary>Is anything actually driving this car right now. False for a parked car — nobody is in it,
    /// the engine is off, and EngineAudio holds its voice rather than idling at whatever speed a switched-off
    /// brain still reports.</summary>
    public bool Running { get; private set; }

    bool HasGears => vehicleInfo != null && vehicleInfo.gearRatios != null && vehicleInfo.gearRatios.Length > 0;

    IVehicleSpeedReadout _speedSource;
    float _shiftTimer;
    float _prevSpeedMps;
    float _rpmTarget;
    float _resolveCooldown;

    void Awake()
    {
        _speedSource = ResolveSpeedSource();
        if (vehicleInfo == null)
        {
            // Pull the car's data from whichever motion brain it has (AI spline car or dynamic player car).
            var sd = GetComponent<SplineDriver>();
            if (sd != null) vehicleInfo = sd.vehicleInfo;
            var pvc = GetComponent<PlayerVehicleController>();
            if (vehicleInfo == null && pvc != null) vehicleInfo = pvc.vehicleInfo;
        }
        Gear = 0;
        Rpm = vehicleInfo != null ? vehicleInfo.idleRpm : 1000f;
        _rpmTarget = Rpm;
        AccelMultiplier = 1f;
        Load01 = 0f;
        Running = false;
    }

    // Pick the speed source that's actually driving the car: the ENABLED IVehicleSpeedReadout. A player car carries
    // both a PlayerVehicleController and a SplineDriver (its broadcast/AI brain) — only one is enabled at a time.
    // A disabled SplineDriver still reports its constant cruise speed, which would pin the engine note at full RPM.
    //
    // NOTHING enabled means nobody is driving this car, and that is answered with null rather than with a
    // disabled component's stale readout. A parked car is parked: the scene opens with the player's car sat
    // in its box with both brains switched off, and taking the switched-off one's cruise speed as gospel is
    // what had a career load open on a car screaming at full revs in an empty pit lane.
    IVehicleSpeedReadout ResolveSpeedSource()
    {
        var comps = GetComponents<MonoBehaviour>();
        for (int i = 0; i < comps.Length; i++)
            if (comps[i] is IVehicleSpeedReadout r && comps[i].isActiveAndEnabled) return r;
        return null;
    }

    void FixedUpdate()
    {
        ShiftEvent = 0;

        float dt = Time.fixedDeltaTime;

        // Re-resolve when the live driver changes (driving ↔ broadcast/crew-chief swaps which readout is enabled).
        // A car nobody is driving asks again on a timer rather than every physics step: GetComponents allocates,
        // and a paddock full of parked cars would pay for that 50 times a second each to be told "still parked".
        if (_speedSource is MonoBehaviour mb && !mb.isActiveAndEnabled) _speedSource = ResolveSpeedSource();
        else if (_speedSource == null)
        {
            _resolveCooldown -= dt;
            if (_resolveCooldown <= 0f)
            {
                _resolveCooldown = 0.25f;
                _speedSource = ResolveSpeedSource();
            }
        }

        Running = _speedSource != null;

        if (!HasGears || _speedSource == null)
        {
            AccelMultiplier = 1f;
            // Parked: let the revs fall away so whoever climbs in next starts from a cold engine rather than
            // from the note this car was making when its driver got out.
            if (_speedSource == null)
            {
                Load01 = 0f;
                _prevSpeedMps = 0f;
                if (vehicleInfo != null)
                {
                    Rpm = Mathf.Lerp(Rpm, vehicleInfo.idleRpm, 1f - Mathf.Exp(-dt / 0.3f));
                    Rpm01 = Mathf.InverseLerp(vehicleInfo.idleRpm, vehicleInfo.maxRpm, Rpm);
                }
            }
            return;
        }

        float speedMps = Mathf.Max(0f, _speedSource.SpeedMps);

        // Engine load: are we gaining speed (on power) or coasting/braking? Drives audio + lets a closed
        // throttle relax the torque shaping. Smoothed so it doesn't flicker frame to frame.
        float accelMps2 = dt > 0f ? (speedMps - _prevSpeedMps) / dt : 0f;
        _prevSpeedMps = speedMps;
        // 1 = on power, 0 = coasting/braking. Steady cruise (accel~0) sits mid as a light-throttle blend;
        // gaining speed pushes to the on bank, losing it to the off bank.
        float loadTarget = Mathf.Clamp01(0.5f + accelMps2 * 1.2f);
        Load01 = Mathf.Lerp(Load01, loadTarget, 1f - Mathf.Exp(-dt / 0.12f));

        // Tick down any in-progress shift before evaluating a new one.
        if (_shiftTimer > 0f) _shiftTimer -= dt;

        // RPM the engine would turn at this speed in the candidate gear.
        float rpmInGear(int g) => RpmFor(speedMps, g);

        // Sequential shift logic — only when not mid-shift, so a single change completes cleanly.
        if (_shiftTimer <= 0f)
        {
            float rpmNow = rpmInGear(Gear);
            int last = vehicleInfo.gearRatios.Length - 1;
            if (Gear < last && rpmNow > vehicleInfo.shiftUpRpm)
            {
                Gear++;
                _shiftTimer = vehicleInfo.shiftTime;
                ShiftEvent = 1;
            }
            else if (Gear > 0 && rpmNow < vehicleInfo.shiftDownRpm)
            {
                Gear--;
                _shiftTimer = vehicleInfo.shiftTime;
                ShiftEvent = -1;
            }
        }

        // Target RPM for the (possibly new) gear, floored at idle.
        _rpmTarget = Mathf.Max(vehicleInfo.idleRpm, rpmInGear(Gear));
        // During a shift the clutch is out — let revs fall toward idle for the audible blip/drop.
        if (IsShifting) _rpmTarget = Mathf.Lerp(_rpmTarget, vehicleInfo.idleRpm, 0.6f);

        float k = rpmSmoothing > 0f ? 1f - Mathf.Exp(-dt / rpmSmoothing) : 1f;
        Rpm = Mathf.Lerp(Rpm, _rpmTarget, k);
        Rpm01 = Mathf.InverseLerp(vehicleInfo.idleRpm, vehicleInfo.maxRpm, Rpm);

        // Acceleration shaping: normalized torque curve × drive-cut while the box is between gears.
        float torque = vehicleInfo.torqueCurve != null && vehicleInfo.torqueCurve.length > 0
            ? Mathf.Max(0f, vehicleInfo.torqueCurve.Evaluate(Rpm01))
            : 1f;
        AccelMultiplier = IsShifting ? 0.05f : torque;
    }

    /// <summary>Engine RPM the drivetrain turns at the given road speed in the given gear.</summary>
    float RpmFor(float speedMps, int gear)
    {
        if (!HasGears || vehicleInfo.wheelRadius <= 0f) return vehicleInfo != null ? vehicleInfo.idleRpm : 0f;
        gear = Mathf.Clamp(gear, 0, vehicleInfo.gearRatios.Length - 1);
        float wheelRevPerMin = (speedMps / (2f * Mathf.PI * vehicleInfo.wheelRadius)) * 60f;
        return wheelRevPerMin * vehicleInfo.finalDriveRatio * vehicleInfo.gearRatios[gear];
    }
}
