using UnityEngine;
using UnityEngine.InputSystem;

// Pit-lane speed limiter for the HUMAN-driven car. The AI already respect TrackInfoV2.pitSpeedLimit inside
// SplineDriver; nothing held the player to it, so a player could blast down the pit lane at 180.
//
// Behaviour (demo spec): the limiter AUTO-ENGAGES the moment the car is on the pit lane before the exit line,
// and caps forward speed there. It is a toggle, not a cage — the driver can switch it off (L / gamepad north)
// and speed, which lights a warning and records a violation for whatever wants to punish it later. Crossing
// the pit exit line releases the cap for good until the car comes back down the lane.
//
// The cap is written to PlayerVehicleController.pitLimiterMps, which is separate from speedGovernorMps so the
// formation-lap governor and this can't clobber one another.
[RequireComponent(typeof(PlayerVehicleController))]
public class PitLimiter : MonoBehaviour
{
    [Tooltip("Car this governs. Auto-found on this GameObject.")]
    public PlayerVehicleController car;
    [Tooltip("Track whose pit lane defines the limit and the exit line. Auto-found at Start.")]
    public TrackBuilder track;

    [Header("Limit")]
    [Tooltip("Speed limit in mph. 0 = read TrackInfoV2.pitSpeedLimit from the track.")]
    public float speedLimitMphOverride = 0f;
    [Tooltip("How far past the limit (mph) the driver may run with the limiter OFF before the warning shows and a violation is logged.")]
    public float violationToleranceMph = 2f;

    [Header("Engagement")]
    [Tooltip("Arm the limiter automatically on entering the pit lane. Off = the driver must arm it by hand.")]
    public bool autoEngage = true;
    [Tooltip("Let the driver toggle the limiter off/on while in the pit lane.")]
    public bool driverCanToggle = true;
    [Tooltip("Keyboard key that toggles the limiter.")]
    public Key toggleKey = Key.L;
    [Tooltip("Metres back from the exit line the car must be before the limiter re-arms — stops it flickering back on while straddling the line.")]
    public float reArmMargin = 10f;

    // --- Live state, read by the HUD.
    public bool Armed { get; private set; }              // limiter engaged, cap applied
    public bool InPitZone { get; private set; }          // on the pit lane, before the exit line
    public bool Speeding { get; private set; }           // in the zone, limiter off, over the limit
    public float LimitMph { get; private set; }          // limit in force
    public float MetresToExitLine { get; private set; }  // + = still short of the line
    public int Violations { get; private set; }          // times the driver crossed the limit with it off

    bool _wasSpeeding;
    bool _togglePrev;
    bool _wasInZone;

    void Awake()
    {
        if (car == null) car = GetComponent<PlayerVehicleController>();
    }

    void Start()
    {
        if (track == null) track = car != null ? car.track : null;
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
    }

    void OnDisable()
    {
        Release(); // never leave a disabled limiter holding the car at 60
    }

    void Update()
    {
        if (driverCanToggle && InPitZone) PollToggle();
    }

    void FixedUpdate()
    {
        if (car == null || track == null || track.track == null || !track.track.hasPitLane) { Release(); return; }

        // An AI brain driving this car (broadcast mode / team switch) obeys the pit limit itself inside
        // SplineDriver — a second cap on top would fight its speed profile.
        if (car.externalInput) { Release(); return; }

        LimitMph = speedLimitMphOverride > 0f ? speedLimitMphOverride : Mathf.Max(1, track.track.pitSpeedLimit);

        bool onPit = track.IsOnPitSurface(transform.position);
        float exitLine = track.track.PitExitLineDistance;
        float pitDistance = onPit ? track.NearestPitDistance(transform.position) : 0f;
        MetresToExitLine = onPit ? exitLine - pitDistance : 0f;

        // Hysteresis: leaving the zone needs only to cross the line, but coming back needs a clear margin,
        // so a car sitting on the line doesn't strobe the limiter on and off.
        InPitZone = onPit && (_wasInZone ? pitDistance < exitLine : pitDistance < exitLine - reArmMargin);

        if (InPitZone && !_wasInZone && autoEngage) Armed = true;
        if (!InPitZone) Armed = false;
        _wasInZone = InPitZone;

        car.pitLimiterMps = Armed ? LimitMph / 2.237f : Mathf.Infinity;

        Speeding = InPitZone && !Armed && car.SpeedMph > LimitMph + violationToleranceMph;
        if (Speeding && !_wasSpeeding) Violations++;
        _wasSpeeding = Speeding;
    }

    // Engage/disengage by hand. Public so a HUD button or a tutorial beat can drive it too.
    public void SetArmed(bool armed)
    {
        Armed = armed && InPitZone;
        if (car != null) car.pitLimiterMps = Armed ? LimitMph / 2.237f : Mathf.Infinity;
    }

    void Release()
    {
        Armed = false;
        InPitZone = false;
        Speeding = false;
        _wasInZone = false;
        if (car != null) car.pitLimiterMps = Mathf.Infinity;
    }

    void PollToggle()
    {
        bool held = false;
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].isPressed) held = true;
        var gp = Gamepad.current;
        if (gp != null && gp.buttonNorth.isPressed) held = true;

        if (held && !_togglePrev) SetArmed(!Armed);
        _togglePrev = held;
    }
}
