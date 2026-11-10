using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// AI pit strategy: once the tyres are worn past a threshold, the car peels into the pit lane at the entry node,
// drives to its box, holds for a service stop (fresh tyres + repaired bodywork), then rejoins. The racing brain
// is parked while on pit road so it doesn't try to race down the lane. Under a full-course caution pit road stays
// open, and a damaged car comes in for repairs (cautionRepairDamage).
//
// When the race is over (RaceDirector.TookChequer) the car runs its cool-down lap, then comes in the same way
// and parks in its box for good — no service, no rejoin.
[RequireComponent(typeof(SplineDriver))]
public class PitStopController : MonoBehaviour
{
    [Tooltip("Pit when average tyre wear exceeds this (0..1). A small random spread is added per car so the field doesn't all stop together.")]
    [Range(0.2f, 0.95f)] public float wearThreshold = 0.6f;
    [Tooltip("Shortest time (s) stationary in the box being serviced. The car also waits for its crew to change all four tyres — the right-hand pair, then the left-hand pair after they run round the car — so a stop runs to whichever is longer.")]
    public float serviceSeconds = 6f;
    [Tooltip("Longest (s) past serviceSeconds the car waits for the crew to finish the tyres, so a crew that cannot finish never keeps the car in the box for good.")]
    public float crewWaitSeconds = 6f;
    [Tooltip("Where the service box sits along the pit lane (0..1).")]
    [Range(0.1f, 0.9f)] public float serviceFrac = 0.5f;
    [Tooltip("Commit to the pit lane when within this distance (m) of the pit-entry node.")]
    public float pitEntryWindow = 25f;
    [Tooltip("Braking rate (m/s^2) for the run-in to pit road: the car is capped at the speed it can still slow to the pit limit from by the entry node.")]
    public float pitApproachDecel = 7f;
    [Tooltip("How far (m) before the entry node the car starts drifting across to the pit lane's side of the track.")]
    public float pitApproachLeadM = 200f;
    [Tooltip("Gentlest path angle (deg) the car takes between the pit-lane centerline (the driving line) and the wall-side box lane, in to its box and back out. Cars drive the centerline and only cut across at their own box, so a car being serviced never blocks the lane. The move is an S-curve over the distance the car rolls (Draftmaster.Sim.PitBoxPath), as the formation pull-out is, so the body points the same way at any speed and squares up on the strip and on the line; it is tightened past this angle only as far as clearing the cars in the neighbouring boxes needs.")]
    [Range(2f, 30f)] public float boxPathAngleDeg = 12f;
    [Tooltip("Shortest run (m) over which the car moves between the centerline and the box lane, for a ladder packed too tight to fit the curve between the cars either side.")]
    public float boxPathMinLength = 8f;
    [Tooltip("Margin (m) on top of a car's width that the car must be across from the car in the next box while it is alongside it, going in and coming out.")]
    public float boxPathClearance = 0.5f;
    [Tooltip("Braking rate (m/s^2) used to size the approach to the box. The car is capped at the speed it can still stop from, so it arrives AT its own box instead of sailing through it into the next car's.")]
    public float boxApproachDecel = 5f;
    [Tooltip("Speed (mph) the car keeps creeping at over the last few metres into the box.")]
    public float boxCrawlMph = 5f;
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

    [Header("Two-lane pit road")]
    [Tooltip("Pit road runs as two lanes side by side (Draftmaster.Sim.PitLanes) where it is wide enough: lane 0 beside the boxes, lane 1 the far side. A car coming in alongside another takes the far lane and moves across once there is room; a car leaving its box pulls out into the far lane, clear of cars still braking down the box-side lane for their boxes. Off = everyone drives the middle, as before.")]
    public bool twoLanePitRoad = true;
    [Tooltip("Steepest path angle (deg) moving across from one pit-road lane to the other - an S-curve over the distance rolled, like the cut into the box.")]
    [Range(2f, 20f)] public float laneChangeAngleDeg = 8f;
    [Tooltip("A pit-road lane counts as taken by a car in it from this far (m) behind me...")]
    public float laneCheckBehindM = 10f;
    [Tooltip("...to this far (m) ahead.")]
    public float laneCheckAheadM = 12f;
    [Tooltip("A car that came onto pit road in the far lane stays there for at least this far (m) before moving across to the box side, so a car that came in alongside it is on pit road too, and seen, before it does.")]
    public float laneSettleM = 30f;
    [Tooltip("Cutting across the box-side lane from the far lane into my box, I first drop back behind a car in that lane alongside me: this much (mph) slower than it.")]
    public float laneYieldMph = 5f;

    [Header("Forced pit (demo)")]
    [Tooltip("Force a single pit stop after green even if the tyres aren't worn, so stops are visible in a short race.")]
    public bool forcedPit = true;
    [Tooltip("Seconds after the green flag before this car is forced to pit.")]
    public float forcedPitDelay = 30f;
    [Tooltip("Extra random seconds added on top of the delay, per car, so the field doesn't pit together.")]
    public float forcedPitSpread = 25f;
    [Tooltip("A race shorter than this many laps never forces a stop — on fresh tyres nobody pits in a sprint, and handing the lead to whoever stays out is no race.")]
    public int forcedPitMinRaceLaps = 10;

    [Header("Under caution")]
    [Tooltip("Under a full-course caution (RaceStart.Caution) a car with at least this much bodywork damage (VehicleDamage.DamageLevel, 0..1 — a full-severity hit adds ~0.18) comes in for repairs.")]
    [Range(0f, 1f)] public float cautionRepairDamage = 0.02f;
    [Tooltip("Under a full-course caution a car comes in for tyres once they are this share of the way to its green-flag wear threshold — a stop costs little behind the safety car.")]
    [Range(0f, 1f)] public float cautionWearShare = 0.5f;

    [Header("After the chequer")]
    [Tooltip("Once this car has taken the chequered flag, run a cool-down lap and then park in its pit box.")]
    public bool parkAfterRace = true;
    [Tooltip("Share of a lap (0..1) the car must run after the chequer before it may take the pit entry, so a lane that opens just past the line costs a full cool-down lap rather than none.")]
    [Range(0f, 1f)] public float coolDownLapFrac = 0.5f;

    const float MphToMps = 1f / 2.237f;

    enum State { Racing, HeadingToPit, Servicing, Leaving, Parked }
    State _state = State.Racing;

    SplineDriver _spline;
    AIRacingBehaviour _ai;
    TireModel _tires;
    VehicleDamage _bodywork;
    FuelTank _fuel;
    float _serviceElapsed;
    PitCrewBox _crew;   // the crew working this stop; the car waits on their tyres
    float _threshold;
    float _greenTime = -1f;
    float _forcedThreshold;
    bool _forcedDone;

    // Post-race: the flag has fallen for this car; metres run since; coming in to park rather than to service.
    bool _raceOver;
    float _coolDownMetres;
    float _prevDist = -1f;
    bool _parking;

    // The move across between the driving line and the box strip (PitBoxPath), latched when it starts: where it
    // started from, the pit-lane distance it started at, and how long it is. In: centerline -> strip, out: back.
    bool _cutting, _pullingOut, _pulledOut;
    float _pathFrom, _pathStartD, _pathLength;
    const float BoxPathEndMargin = 1f;   // the cut-in is on the strip this far (m) before the box's stop line

    // Two-lane pit road: the lane this car is driving (-1 = not chosen yet), and the move across to it, latched like
    // the box cut (from, to, the pit-lane distance it started at, its length).
    int _lane = -1;
    bool _laneMoving;
    float _laneFrom, _laneTo, _laneStartD, _laneLength, _laneEntryD;
    static readonly List<PitLanes.Car> s_pitCars = new List<PitLanes.Car>();
    VehicleCollision _collision;

    // Which box this car stops in. The field uses the box its grid slot owns; the player's car, brought in by
    // the AI after the race (RaceEndPitIn), uses the box the player was given.
    [HideInInspector] public int boxOverride = -1;
    int BoxIndex => boxOverride >= 0 ? boxOverride : _spline.qualifyingPosition;

    public bool IsPitting => _state != State.Racing;
    // The pit-road lane this car is driving down (PitLanes.BoxSide / FarSide), or -1 while it is not in one - in its
    // box, cutting in or pulling out, or not on pit road at all. Other cars judge a -1 by where it is across the lane.
    public int PitLaneIndex =>
        (_state == State.HeadingToPit && _spline != null && _spline.IsOnPit && !_cutting) ||
        (_state == State.Leaving && _spline != null && _spline.IsOnPit && !_spline.pitStopHold && !_pullingOut)
            ? _lane : -1;
    // Stopped in its box after the race, and staying there.
    public bool IsParkedAfterRace => _state == State.Parked;

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
        if (!RaceStart.IsRunning || _spline == null || _spline.TrackLength <= 0f) return;
        var tb = _spline.track;
        if (tb == null || tb.track == null) return;

        AcquireRefs();
        if (_greenTime < 0f) _greenTime = Time.time;   // first green frame this car saw
        TrackCoolDown();

        switch (_state)
        {
            case State.Racing:
                if (_raceOver)
                {
                    // The cool-down lap is the run to the pit entry; only a box to park in makes it worth taking.
                    if (PitLane.Configured && _coolDownMetres >= coolDownLapFrac * _spline.TrackLength)
                    {
                        _parking = true;
                        _state = State.HeadingToPit;
                    }
                }
                else if (WantsPit()) { _state = State.HeadingToPit; _forcedDone = true; }
                break;

            case State.HeadingToPit:
                if (!_spline.IsOnPit)
                {
                    _cutting = false;
                    _lane = -1;
                    _laneMoving = false;
                    // Commit to the lane as we reach the entry node so the merge stays continuous.
                    float d = _spline.DistanceOnTrack;
                    float gap = tb.track.PitEntryDistanceOnLap - d;
                    if (gap < 0f) gap += _spline.TrackLength;
                    if (gap < pitEntryWindow) _spline.usePitLane = true;
                    _spline.ApproachPitEntry(pitApproachDecel, pitApproachLeadM);
                }
                else
                {
                    if (_ai != null) _ai.enabled = false;     // park the racing brain on pit road
                    // The racing brain's offset becomes the car's pit-road lateral rather than vanishing: zeroed, a
                    // car coming in beside another jumped onto the middle of pit road, on top of it.
                    if (Mathf.Abs(_spline.tacticalLateralOffset) > 1e-4f)
                    {
                        _spline.lateralOffset += _spline.tacticalLateralOffset;
                        _laneMoving = false;
                    }
                    _spline.tacticalLateralOffset = 0f;
                    // Stop in this car's ASSIGNED box (= its grid position), not a generic fraction of the lane,
                    // so every car services in its own marked box. Falls back to serviceFrac if unconfigured.
                    float targetFrac = serviceFrac;
                    float boxDist = _spline.PitLength * serviceFrac;
                    if (PitLane.Configured && _spline.PitLength > 0f)
                    {
                        boxDist = _spline.PitParkDistanceFor(PitLane.BoxDistance(BoxIndex, _spline.PitLength));
                        targetFrac = boxDist / _spline.PitLength;
                    }
                    float remaining = boxDist - _spline.PitProgress01 * _spline.PitLength;

                    // Call the box before arriving: the sign man walks out and puts the board down over
                    // where the nose will stop, which he cannot do if he only hears about it on arrival.
                    // Not when coming in to park: there is no stop to board, and he'd stand there until it timed out.
                    if (remaining < signCallM && !_parking)
                        PitCrewRegistry.ForBox(BoxIndex)?.SignalApproach(transform);

                    // Drive a pit-road lane (the middle on a one-lane road), clear of cars being serviced on the strip,
                    // and cut across to the wall-side box lane only inside the last stretch before this car's own box —
                    // same flow as PracticeAIStint. Servicing ON the centerline blocked the whole lane and the
                    // cars behind (which have no racing brain down here) drove straight into the stop.
                    // The cut is an S-curve over the distance rolled (PitBoxPath), finished on the strip just short
                    // of the stop line, so the car arrives square in its box. A fixed lateral rate in time — what
                    // this used to do — was most of the move in the last few metres at a crawl under the braking
                    // ramp, and swung the body across the box and back.
                    float myD = _spline.PitProgress01 * _spline.PitLength;

                    // Two lanes: come in on the box side unless a car is already there beside me, and move across
                    // to it from the far lane as soon as there is room - it is the lane the box is turned into from.
                    bool twoLanes = TwoLanes;
                    bool yieldToBoxLane = false;
                    if (_lane < 0)
                    {
                        _laneEntryD = myD;
                        _lane = twoLanes ? PitLanes.EntryLane(myD, _spline.lateralOffset, PitCars(), PitWidth, PitLane.ParkLateral,
                                                              laneCheckBehindM, laneCheckAheadM)
                                         : PitLanes.BoxSide;
                    }
                    if (twoLanes && _lane == PitLanes.FarSide && !_cutting
                        && PitLanes.BoxSideOpen(myD, myD - _laneEntryD, PitCars(), PitWidth, PitLane.ParkLateral, laneCheckBehindM, laneCheckAheadM, laneSettleM))
                        _lane = PitLanes.BoxSide;
                    if (!_cutting) SteerToLane(myD);

                    if (!_cutting && Mathf.Abs(PitLane.ParkLateral - _spline.lateralOffset) > 0.01f)
                    {
                        float len = BoxPathLength(PitLane.ParkLateral - _spline.lateralOffset, BoxPathEndMargin);
                        // From the far lane the cut crosses the box-side lane: not across a car in it beside me. Drop
                        // back behind it first (below) - unless the box is right here, when the car has to go in.
                        bool crossing = twoLanes && _lane == PitLanes.FarSide;
                        if (crossing && remaining < len + BoxPathEndMargin && remaining > 3f
                            && BoxLaneCarBeside(myD, out _))
                            yieldToBoxLane = true;
                        else if (remaining < len + BoxPathEndMargin)
                        {
                            _cutting = true;
                            _laneMoving = false;
                            _pathFrom = _spline.lateralOffset;
                            _pathStartD = myD;
                            // Came onto the lane already inside the window (a box right by the entry): fit it in.
                            _pathLength = Mathf.Max(0.5f, Mathf.Min(len, remaining - BoxPathEndMargin));
                        }
                    }
                    if (_cutting)
                        _spline.lateralOffset = Draftmaster.Sim.PitBoxPath.Lateral(_pathFrom, PitLane.ParkLateral, myD - _pathStartD, _pathLength);

                    // Only queue behind traffic between here and this car's own box — a car parked in a box
                    // further down the lane is not an obstacle, it is someone else's stop.
                    PitLaneFollowCap(remaining + 2f);
                    // Brake FOR the box. Without this the stop was triggered on arrival at full pit speed and
                    // the car needed another ~30 m to halt — landing it two or three boxes down, on top of
                    // whoever owned them (including the player's).
                    float approachMph = Mathf.Sqrt(2f * Mathf.Max(0.5f, boxApproachDecel) * Mathf.Max(0f, remaining - 1f)) / MphToMps;
                    _spline.aiMaxSpeedMph = Mathf.Min(_spline.aiMaxSpeedMph, Mathf.Max(boxCrawlMph, approachMph));
                    if (yieldToBoxLane && BoxLaneCarBeside(myD, out float besideMph))
                        _spline.aiMaxSpeedMph = Mathf.Min(_spline.aiMaxSpeedMph, Mathf.Max(boxCrawlMph, besideMph - laneYieldMph));
                    // Hard stop line at the box: SplineDriver pins the car the instant it arrives, so a late
                    // frame can't carry it past. Cleared again on the way out.
                    _spline.pitParkDistance = boxDist;

                    if (_spline.pitStopHold || remaining <= 0.5f || _spline.PitProgress01 >= targetFrac)
                    {
                        _spline.pitStopHold = true;
                        _cutting = false;
                        if (_parking)
                        {
                            _state = State.Parked;
                            break;
                        }
                        _serviceElapsed = 0f;
                        _crew = PitCrewRegistry.ForBox(BoxIndex);
                        if (_crew != null) _crew.BeginService(transform);
                        RecordPit();
                        _state = State.Servicing;
                    }
                }
                break;

            case State.Servicing:
                // Fuel and the stop's floor take serviceSeconds; the tyres take as long as the crew need for
                // the right side, the run round the car and the left side. Both, before the car goes.
                _serviceElapsed += Time.fixedDeltaTime;
                if (PitCrewBox.StopComplete(_crew, _serviceElapsed, serviceSeconds, crewWaitSeconds))
                {
                    if (_tires != null) _tires.PitReset();
                    if (repairDamage && _bodywork != null) _bodywork.RepairFull();
                    if (refuel && _fuel != null) _fuel.FillFull();
                    if (_crew != null) _crew.EndService();
                    _crew = null;
                    // The race ended while the car was in its box: it stays there.
                    _state = _raceOver ? State.Parked : State.Leaving; // pitStopHold stays on until the lane behind is clear
                }
                break;

            case State.Parked:
                // Pinned on the stop line in the box. Once it has actually stopped, hard-park it as the practice
                // field parks, so a dynamic-AI car's motion model is re-seeded and doesn't creep out of the box.
                _spline.pitStopHold = true;
                float stoppedMps = _spline.externalMotionController ? _spline.externalActualSpeedMps : _spline.speed;
                if (!_spline.parkedHold && stoppedMps < 0.3f) _spline.parkedHold = true;
                break;

            case State.Leaving:
                if (_spline.pitStopHold)
                {
                    // Yield: stay in the box until no moving pit-lane car is bearing down from behind,
                    // so the pull-out merges into a gap instead of across someone's nose.
                    // The stop line has to be released first or SplineDriver re-pins the car every frame.
                    _pullingOut = false;
                    _pulledOut = false;
                    if (PitExitClear())
                    {
                        _spline.pitParkDistance = -1f;
                        _spline.aiMaxSpeedMph = 200f;
                        _spline.pitStopHold = false;
                    }
                }
                else if (_spline.IsOnPit)
                {
                    // Off the box strip back onto the centerline for the run to the exit — the same S-curve over
                    // distance rolled as the formation pull-out, fitted to clear the car in the next box down the
                    // lane and to be on the line before the exit. A fixed lateral rate in time swung the car
                    // through 50 degrees and more as it crawled out of the box.
                    float myD = _spline.PitProgress01 * _spline.PitLength;
                    // Out into the far lane (two-lane pit road), across the box-side lane the cars coming in are
                    // braking down for their boxes; the middle of a one-lane road.
                    _lane = PitLanes.FarSide;
                    float outLat = LaneLat(_lane);
                    if (!_pullingOut && !_pulledOut && Mathf.Abs(_spline.lateralOffset - outLat) > 0.01f)
                    {
                        _pullingOut = true;
                        _pathFrom = _spline.lateralOffset;
                        _pathStartD = myD;
                        // SplineDriver zeroes lateralOffset at the exit and eases what is left as a merge bias,
                        // which is a sideways slide of its own, so the curve is done before then.
                        float room = (_spline.PitLength * _spline.pitExitThreshold - myD) * 0.8f;
                        _pathLength = BoxPathLength(outLat - _pathFrom, 0f, Mathf.Max(room, boxPathMinLength));
                    }
                    if (_pullingOut)
                    {
                        _spline.lateralOffset = PitBoxPath.Lateral(_pathFrom, outLat, myD - _pathStartD, _pathLength);
                        if (myD - _pathStartD >= _pathLength) { _pullingOut = false; _pulledOut = true; }
                    }
                    PitLaneFollowCap();
                }
                else // SplineDriver auto-exits the pit; we're back on track
                {
                    _spline.pitParkDistance = -1f;   // never carry a stop line back onto the track
                    _pullingOut = false;
                    _pulledOut = false;
                    _lane = -1;
                    _laneMoving = false;
                    if (_ai != null) _ai.enabled = true;
                    _state = State.Racing;
                }
                break;
        }
    }

    // Watch for this car taking the chequer, then count the metres it runs on the main track after it — the
    // cool-down lap. Measured off the lap-distance wrap, so the line going past doesn't count as minus a lap.
    void TrackCoolDown()
    {
        if (!parkAfterRace) return;
        if (!_raceOver)
        {
            if (!RaceDirector.TookChequer(transform)) return;
            _raceOver = true;
            _coolDownMetres = 0f;
            _prevDist = -1f;
            // Already on the way in for a stop: make it the park.
            if (_state == State.HeadingToPit) _parking = true;
        }

        if (_spline.IsOnPit) { _prevDist = -1f; return; }
        float d = _spline.DistanceOnTrack;
        if (_prevDist >= 0f)
        {
            float step = d - _prevDist;
            float half = _spline.TrackLength * 0.5f;
            if (step < -half) step += _spline.TrackLength;
            else if (step > half) step -= _spline.TrackLength;
            if (step > 0f) _coolDownMetres += step;
        }
        _prevDist = d;
    }

    // Bring this car straight in to park, with no cool-down lap of its own to run: the player's car, which the
    // player drove round the cool-down lap before the AI took it (RaceEndPitIn).
    public void ParkNow()
    {
        _raceOver = true;
        _coolDownMetres = float.MaxValue;
        _parking = true;
        _spline.parkedHold = false;
        _spline.pitStopHold = false;
        _spline.pitParkDistance = -1f;
        _cutting = _pullingOut = _pulledOut = _laneMoving = false;
        _lane = -1;
        _state = State.HeadingToPit;   // on track it heads for the entry; on pit road it rolls on to the box
    }

    // Put this car in its box this instant, parked for good: the race has been ended from the results screen
    // and whoever was still running a cool-down lap or rolling down pit road is simply there. No-op without
    // a box ladder to park in.
    public void ParkInBoxNow()
    {
        if (_spline == null || !PitLane.Configured) return;
        if (_state == State.Servicing && _crew != null) _crew.EndService();
        _crew = null;
        if (_ai != null) _ai.enabled = false;
        _raceOver = true;
        _parking = true;
        _state = State.Parked;
        _spline.SnapToPitBox(PitLane.BoxDistance(BoxIndex, _spline.PitLength), PitLane.ParkLateral);
    }

    // Length (m) of the move between the driving line and the box strip (PitBoxPath), from this car's footprint and
    // the box ladder. `maxLength` > 0 caps it (the lane left to the exit).
    float BoxPathLength(float delta, float endMargin, float maxLength = 0f)
    {
        if (_collision == null) _collision = GetComponent<VehicleCollision>();
        Vector2 half = _collision != null ? _collision.halfExtents : new Vector2(1.0f, 2.4f);
        float spacing = PitLane.Configured ? PitLane.Spacing : 0f;
        return Draftmaster.Sim.PitBoxPath.Length(delta, spacing, 2f * half.y, 2f * half.x + boxPathClearance,
                                                 boxPathAngleDeg, boxPathMinLength, maxLength, endMargin);
    }

    // ---- Two-lane pit road ------------------------------------------------------------------------------------

    float PitWidth => _spline != null && _spline.track != null ? _spline.track.PitDrivingWidth : 0f;
    bool TwoLanes => twoLanePitRoad && PitLanes.TwoLanes(PitWidth, PitLane.ParkLateral);
    float LaneLat(int lane) => twoLanePitRoad ? PitLanes.Lateral(lane, PitWidth, PitLane.ParkLateral) : 0f;

    // Every other car on pit road: where it is down the lane and across it, and the lane it says it is in.
    List<PitLanes.Car> PitCars()
    {
        s_pitCars.Clear();
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || !d.IsOnPit || d.PitLength <= 0f) continue;
            var psc = d.GetComponent<PitStopController>();
            s_pitCars.Add(new PitLanes.Car
            {
                distance = d.PitProgress01 * d.PitLength,
                lateral = d.lateralOffset,
                mph = d.CurrentMph,
                lane = psc != null && psc.isActiveAndEnabled ? psc.PitLaneIndex : -1,
            });
        }
        return s_pitCars;
    }

    // A car in the box-side lane beside me - from a car length behind to a little ahead - that a cut across the lane
    // into my box would run into. `mph`: how fast it is going.
    bool BoxLaneCarBeside(float myD, out float mph)
    {
        mph = 0f;
        var cars = PitCars();
        float best = float.MaxValue;
        for (int i = 0; i < cars.Count; i++)
        {
            var c = cars[i];
            float g = c.distance - myD;
            if (g < -6f || g > 8f) continue;
            if (PitLanes.LaneOf(c, PitWidth, PitLane.ParkLateral) != PitLanes.BoxSide) continue;
            if (Mathf.Abs(g) < best) { best = Mathf.Abs(g); mph = c.mph; }
        }
        return best < float.MaxValue;
    }

    // Over to the lane this car is driving, as an S-curve over the distance rolled (PitBoxPath), latched when it
    // starts and started again if the lane changes on the way.
    void SteerToLane(float myD)
    {
        float target = LaneLat(_lane < 0 ? PitLanes.BoxSide : _lane);
        if (_laneMoving && Mathf.Abs(target - _laneTo) > 0.01f) _laneMoving = false;
        if (!_laneMoving)
        {
            if (Mathf.Abs(target - _spline.lateralOffset) < 0.02f) { _spline.lateralOffset = target; return; }
            _laneMoving = true;
            _laneFrom = _spline.lateralOffset;
            _laneTo = target;
            _laneStartD = myD;
            _laneLength = Mathf.Max(boxPathMinLength, FormationLanes.PullOutLength(Mathf.Abs(target - _laneFrom), laneChangeAngleDeg, 0f, 0f));
        }
        float run = myD - _laneStartD;
        _spline.lateralOffset = PitBoxPath.Lateral(_laneFrom, _laneTo, run, _laneLength);
        if (run >= _laneLength) _laneMoving = false;
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

        // A race with a known length: nobody comes in on the last lap — the stop costs more than it can win back.
        var director = RaceDirector.Instance;
        bool knownRace = director != null && director.isActiveAndEnabled && director.raceLaps > 0;
        if (knownRace && RaceDirector.Progress01 >= 1f - 1f / director.raceLaps) return false;

        // Demo guarantee: pit once a set time into the race even on fresh tyres, so stops are always visible —
        // but not in a race too short for a stop to make sense.
        bool forceAllowed = forcedPit && (!knownRace || director.raceLaps >= forcedPitMinRaceLaps);
        if (forceAllowed && !_forcedDone && _greenTime >= 0f && Time.time - _greenTime >= _forcedThreshold)
            return true;

        // Under caution: come in to have damage repaired, and for tyres well before they would be due under green.
        bool caution = RaceStart.IsCaution;
        if (caution && _bodywork != null && _bodywork.DamageLevel >= cautionRepairDamage) return true;

        // Strategy: pit when the tyres are worn past this car's threshold.
        if (_tires == null) return false;
        float avgWear = 0.5f * (_tires.FrontWear + _tires.RearWear);
        return avgWear >= (caution ? _threshold * cautionWearShare : _threshold);
    }
}
