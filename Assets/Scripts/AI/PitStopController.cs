using UnityEngine;

// AI pit strategy: once the tyres are worn past a threshold, the car peels into the pit lane at the entry node,
// drives to its box, holds for a service stop (fresh tyres + repaired bodywork), then rejoins. The racing brain
// is parked while on pit road so it doesn't try to race down the lane.
[RequireComponent(typeof(SplineDriver))]
public class PitStopController : MonoBehaviour
{
    [Tooltip("Pit when average tyre wear exceeds this (0..1). A small random spread is added per car so the field doesn't all stop together.")]
    [Range(0.2f, 0.95f)] public float wearThreshold = 0.6f;
    [Tooltip("Seconds stationary in the box being serviced.")]
    public float serviceSeconds = 3.5f;
    [Tooltip("Where the service box sits along the pit lane (0..1).")]
    [Range(0.1f, 0.9f)] public float serviceFrac = 0.5f;
    [Tooltip("Commit to the pit lane when within this distance (m) of the pit-entry node.")]
    public float pitEntryWindow = 25f;
    [Tooltip("Reset bodywork damage during the stop too.")]
    public bool repairDamage = true;
    [Tooltip("Refuel to full during the stop.")]
    public bool refuel = true;

    [Header("Forced pit (demo)")]
    [Tooltip("Force a single pit stop after green even if the tyres aren't worn, so stops are visible in a short race.")]
    public bool forcedPit = true;
    [Tooltip("Seconds after the green flag before this car is forced to pit.")]
    public float forcedPitDelay = 30f;
    [Tooltip("Extra random seconds added on top of the delay, per car, so the field doesn't pit together.")]
    public float forcedPitSpread = 25f;

    enum State { Racing, HeadingToPit, Servicing, Leaving }
    State _state = State.Racing;

    SplineDriver _spline;
    AIRacingBehaviour _ai;
    TireModel _tires;
    VehicleDamage _bodywork;
    FuelTank _fuel;
    float _serviceTimer;
    float _threshold;
    float _greenTime = -1f;
    float _forcedThreshold;
    bool _forcedDone;

    public bool IsPitting => _state != State.Racing;

    void Awake() { _spline = GetComponent<SplineDriver>(); _ai = GetComponent<AIRacingBehaviour>(); }

    void Start()
    {
        _threshold = Mathf.Clamp01(wearThreshold + Random.Range(-0.06f, 0.06f));
        _forcedThreshold = forcedPitDelay + Random.Range(0f, Mathf.Max(0f, forcedPitSpread));
    }

    // Tyres / bodywork / fuel are created by other components (PlayerVehicleController adds TireModel in its own
    // Start), so the order they appear relative to our Start is undefined. Grab them lazily so we never latch null.
    void AcquireRefs()
    {
        if (_tires == null) _tires = GetComponent<TireModel>();
        if (_bodywork == null) _bodywork = GetComponentInChildren<VehicleDamage>();
        if (_fuel == null) _fuel = GetComponent<FuelTank>();
    }

    void FixedUpdate()
    {
        if (!RaceStart.IsGreen || _spline == null || _spline.TrackLength <= 0f) return;
        var tb = _spline.track;
        if (tb == null || tb.track == null) return;

        AcquireRefs();
        if (_greenTime < 0f) _greenTime = Time.time;   // first green frame this car saw

        switch (_state)
        {
            case State.Racing:
                if (WantsPit()) { _state = State.HeadingToPit; _forcedDone = true; }
                break;

            case State.HeadingToPit:
                if (!_spline.IsOnPit)
                {
                    // Commit to the lane as we reach the entry node so the merge stays continuous.
                    float d = _spline.DistanceOnTrack;
                    float gap = tb.track.pitEntryDistance - d;
                    if (gap < 0f) gap += _spline.TrackLength;
                    if (gap < pitEntryWindow) _spline.usePitLane = true;
                }
                else
                {
                    if (_ai != null) _ai.enabled = false;     // park the racing brain on pit road
                    _spline.tacticalLateralOffset = 0f;
                    // Stop in this car's ASSIGNED box (= its grid position), not a generic fraction of the lane,
                    // so every car services in its own marked box. Falls back to serviceFrac if unconfigured.
                    float targetFrac = serviceFrac;
                    if (PitLane.Configured && _spline.PitLength > 0f)
                        targetFrac = PitLane.BoxDistance(_spline.qualifyingPosition, _spline.PitLength) / _spline.PitLength;
                    if (_spline.PitProgress01 >= targetFrac)
                    {
                        _spline.pitStopHold = true;
                        _serviceTimer = serviceSeconds;
                        PitCrewRegistry.ForBox(_spline.qualifyingPosition)?.BeginService(transform);
                        RecordPit();
                        _state = State.Servicing;
                    }
                }
                break;

            case State.Servicing:
                _serviceTimer -= Time.fixedDeltaTime;
                if (_serviceTimer <= 0f)
                {
                    if (_tires != null) _tires.PitReset();
                    if (repairDamage && _bodywork != null) _bodywork.RepairFull();
                    if (refuel && _fuel != null) _fuel.FillFull();
                    PitCrewRegistry.ForBox(_spline.qualifyingPosition)?.EndService();
                    _spline.pitStopHold = false;
                    _state = State.Leaving;
                }
                break;

            case State.Leaving:
                if (!_spline.IsOnPit) // SplineDriver auto-exits the pit; we're back on track
                {
                    if (_ai != null) _ai.enabled = true;
                    _state = State.Racing;
                }
                break;
        }
    }

    void RecordPit()
    {
        var hist = GetComponent<PitHistory>();
        if (hist == null) hist = gameObject.AddComponent<PitHistory>();
        int lap = RacePositionTracker.Instance != null ? RacePositionTracker.Instance.LapOf(transform) : 0;
        hist.Record(lap);
    }

    bool WantsPit()
    {
        if (_spline.IsOnPit) return false;

        // Demo guarantee: pit once a set time into the race even on fresh tyres, so stops are always visible.
        if (forcedPit && !_forcedDone && _greenTime >= 0f && Time.time - _greenTime >= _forcedThreshold)
            return true;

        // Strategy: pit when the tyres are worn past this car's threshold.
        if (_tires == null) return false;
        float avgWear = 0.5f * (_tires.FrontWear + _tires.RearWear);
        return avgWear >= _threshold;
    }
}
