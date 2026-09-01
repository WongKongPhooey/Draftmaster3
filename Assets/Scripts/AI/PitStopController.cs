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
    [Tooltip("Lateral speed (m/s) for moving between the pit-lane centerline (the driving line) and the wall-side box lane. Cars drive the centerline and only cut across at their own box, so a car being serviced never blocks the lane.")]
    public float laneChangeRate = 3f;
    [Tooltip("Braking rate (m/s^2) used to size the approach to the box. The car is capped at the speed it can still stop from, so it arrives AT its own box instead of sailing through it into the next car's.")]
    public float boxApproachDecel = 5f;
    [Tooltip("Speed (mph) the car keeps creeping at over the last few metres into the box.")]
    public float boxCrawlMph = 5f;
    [Tooltip("Shortest run-in (m) over which the car may cut from the centerline to the box lane. The real window grows with speed so the car is ON the box lane before it stops.")]
    public float minCutWindow = 12f;
    [Tooltip("Tell the crew the car is coming this far (m) up the lane from the box, so their sign man is out with the board down before it arrives — the board is what marks the stop.")]
    public float signCallM = 35f;
    [Tooltip("After service, hold in the box while another moving pit-lane car is within this many metres behind the box — pull out into a gap, not into traffic.")]
    public float exitYieldBehindM = 14f;
    [Tooltip("Pit-lane ACC: stop this far (m) behind a stationary car in the lane ahead.")]
    public float pitFollowStopGap = 6f;
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

    const float MphToMps = 1f / 2.237f;

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
                    float gap = tb.track.PitEntryDistanceOnLap - d;
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
                    float boxDist = _spline.PitLength * serviceFrac;
                    if (PitLane.Configured && _spline.PitLength > 0f)
                    {
                        boxDist = PitLane.BoxDistance(_spline.qualifyingPosition, _spline.PitLength);
                        targetFrac = boxDist / _spline.PitLength;
                    }
                    float remaining = boxDist - _spline.PitProgress01 * _spline.PitLength;

                    // Call the box before arriving: the sign man walks out and puts the board down over
                    // where the nose will stop, which he cannot do if he only hears about it on arrival.
                    if (remaining < signCallM)
                        PitCrewRegistry.ForBox(_spline.qualifyingPosition)?.SignalApproach(transform);

                    // Drive the centerline down the lane (clear of cars being serviced on the strip) and cut
                    // across to the wall-side box lane only inside the last stretch before this car's own box —
                    // same flow as PracticeAIStint. Servicing ON the centerline blocked the whole lane and the
                    // cars behind (which have no racing brain down here) drove straight into the stop.
                    // The window is sized from the lateral still to cover: the box lane sits ~8 m off the
                    // centerline, so the old fixed 10 m run-in only bought ~1 m of it at pit speed and the car
                    // stopped in the middle of the lane instead of in its box. Under the braking ramp below the
                    // time left to the box is sqrt(2d/a), so d = (latRemain/rate)^2 * a/2 covers the move; the
                    // 1.5 margin keeps it honest without driving half the strip through everyone else's boxes.
                    float latRemain = Mathf.Abs(PitLane.ParkLateral - _spline.lateralOffset);
                    float latTime = latRemain / Mathf.Max(0.1f, laneChangeRate);
                    float cutWindow = Mathf.Max(minCutWindow, 0.75f * Mathf.Max(0.5f, boxApproachDecel) * latTime * latTime);
                    if (remaining < cutWindow)
                        _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, PitLane.ParkLateral, laneChangeRate * Time.fixedDeltaTime);

                    // Only queue behind traffic between here and this car's own box — a car parked in a box
                    // further down the lane is not an obstacle, it is someone else's stop.
                    PitLaneFollowCap(remaining + 2f);
                    // Brake FOR the box. Without this the stop was triggered on arrival at full pit speed and
                    // the car needed another ~30 m to halt — landing it two or three boxes down, on top of
                    // whoever owned them (including the player's).
                    float approachMph = Mathf.Sqrt(2f * Mathf.Max(0.5f, boxApproachDecel) * Mathf.Max(0f, remaining - 1f)) / MphToMps;
                    _spline.aiMaxSpeedMph = Mathf.Min(_spline.aiMaxSpeedMph, Mathf.Max(boxCrawlMph, approachMph));
                    // Hard stop line at the box: SplineDriver pins the car the instant it arrives, so a late
                    // frame can't carry it past. Cleared again on the way out.
                    _spline.pitParkDistance = boxDist;

                    if (_spline.pitStopHold || remaining <= 0.5f || _spline.PitProgress01 >= targetFrac)
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
                    _state = State.Leaving; // pitStopHold stays on until the lane behind is clear
                }
                break;

            case State.Leaving:
                if (_spline.pitStopHold)
                {
                    // Yield: stay in the box until no moving pit-lane car is bearing down from behind,
                    // so the pull-out merges into a gap instead of across someone's nose.
                    // The stop line has to be released first or SplineDriver re-pins the car every frame.
                    if (PitExitClear())
                    {
                        _spline.pitParkDistance = -1f;
                        _spline.aiMaxSpeedMph = 200f;
                        _spline.pitStopHold = false;
                    }
                }
                else if (_spline.IsOnPit)
                {
                    // Ease off the box strip back onto the centerline for the run to the exit.
                    _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, 0f, laneChangeRate * Time.fixedDeltaTime);
                    PitLaneFollowCap();
                }
                else // SplineDriver auto-exits the pit; we're back on track
                {
                    _spline.pitParkDistance = -1f;   // never carry a stop line back onto the track
                    if (_ai != null) _ai.enabled = true;
                    _state = State.Racing;
                }
                break;
        }
    }

    // Pit-lane ACC: nothing else paces a car down here (the racing brain is parked), so without this a car
    // drives the lane's speed profile straight into whoever is stopped or queuing ahead. Owns aiMaxSpeedMph
    // while on the pit lane — AIRacingBehaviour is disabled here and re-takes it every frame once racing.
    // horizon = how far ahead an obstacle still matters (m). On the run to the box that is the distance
    // left to the box: cars parked beyond it are in their own boxes, not in the way.
    void PitLaneFollowCap(float horizon = float.MaxValue)
    {
        if (!_spline.IsOnPit || _spline.PitLength <= 0f) return;
        float myD = _spline.PitProgress01 * _spline.PitLength;
        float myLat = _spline.LateralOnTrack;

        float bestGap = float.MaxValue;
        float bestMph = 0f;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || !d.IsOnPit || d.PitLength <= 0f) continue;
            float g = d.PitProgress01 * d.PitLength - myD;
            if (g <= 0.5f || g > Mathf.Min(30f, horizon)) continue;         // level with me, or past my box
            if (Mathf.Abs(d.LateralOnTrack - myLat) > 2.2f) continue;      // parked on the strip, not in my lane
            if (g < bestGap) { bestGap = g; bestMph = d.CurrentMph; }
        }

        // No foe → release the cap (200 lets SplineDriver's own pit pace govern). A Min() ratchet here would
        // latch a one-off slow-down for the rest of the lane.
        float cap = 200f;
        if (bestGap < float.MaxValue)
            cap = Mathf.Max(0f, bestMph + Mathf.Max(0f, bestGap - pitFollowStopGap) * 2f);
        _spline.aiMaxSpeedMph = cap;
    }

    // A moving pit-lane car within exitYieldBehindM behind the box (or level with it) blocks the pull-out.
    // Stationary neighbours servicing in adjacent boxes don't count.
    bool PitExitClear()
    {
        if (!_spline.IsOnPit || _spline.PitLength <= 0f) return true;
        float myD = _spline.PitProgress01 * _spline.PitLength;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || !d.IsOnPit || d.PitLength <= 0f) continue;
            if (d.CurrentMph < 2f) continue; // parked/servicing — not traffic
            float rel = myD - d.PitProgress01 * d.PitLength; // + = they're behind me
            if (rel > -3f && rel < exitYieldBehindM) return false;
        }
        return true;
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
