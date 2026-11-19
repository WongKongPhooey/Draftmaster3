using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// Drives an AI car during the FORMATION phase: tucks it into a two-wide train behind the safety car and weaves
// it gently to mimic warming the tyres. Dormant in every other phase — AIRacingBehaviour owns the car once the
// race goes green.
//
// Also drives it under a full-course CAUTION (FullCourseCaution): the same train behind the safety car, but single
// file — the field is in running order by then, not grid order, so grid-slot columns, row partners and slot
// anchors mean nothing — with no weave. Once the restart order is settled (pit road shut for good, nobody left on it)
// every car has a slot in a two-wide restart line-up (CautionLineup.Slot), and from then on the slot does what the grid
// slot does on the formation lap: columns, row partners drawn level, the slot anchor, the start lanes - the restart is
// lined up exactly as the start is. A car coming in for a stop under the caution belongs to its PitStopController on
// pit road.
//
// Works purely through SplineDriver's public knobs (aiMaxSpeedMph / aiMinDecelMphPerSec / tacticalLateralOffset /
// paceMultiplier), so the car's path, corner speeds and dynamic motion are unchanged.
//
// Seeing and avoiding the cars around it is PackPlanner's job (Draftmaster.Sim, EditMode-tested): this component
// only gathers what is around the car — AI, the safety car and the free-driven player, on the track or down the
// pit lane — into that planner's track frame, and applies what it decides. The planner holds station off whatever
// is genuinely in the car's lane (bumper to bumper, not nose to nose), never runs faster than it could stop from,
// and drives round a stopped or suddenly slowed car when a side is open.
[RequireComponent(typeof(SplineDriver))]
public class FormationController : MonoBehaviour
{
    [Tooltip("Cruise pace of the train (mph). Falls back to this when no FormationDirector is present.")]
    public float cruiseMph = 60f;
    [Tooltip("How far behind the car ahead this car tries to sit (m, centre to centre). The planner never sits closer than its safety clearance, whatever this says.")]
    public float targetGap = 9f;
    [Tooltip("How far behind the SAFETY CAR the leader tries to sit (m). Deliberately much larger than targetGap: " +
             "the pace car runs its own speed profile, so it sheds speed for corners on its own schedule and the " +
             "leader stationed a race-gap behind it has to stab the brakes to keep station. That stab is the " +
             "disturbance the whole train then amplifies. A long gap turns the same speed change into a slow " +
             "gap-closure the follow law can absorb without braking at all.")]
    public float paceCarGap = 26f;
    [Tooltip("Most this car may exceed cruise pace by while catching the train up (mph). Only used on straights. Kept low so the field doesn't string out far ahead of the pace.")]
    public float catchUpBonusMph = 9f;
    [Tooltip("Most this car may exceed cruise pace by (mph) when the car ahead of it has got well away — reached at " +
             "catchUpFarGap, eased in from catchUpNearGap. A human on pole drives off first and runs straight up to " +
             "the safety car; held to the small catch-up bonus the train behind never got back to them. Never " +
             "applies off the pace car itself, which the leader is meant to sit a long way back from.")]
    public float farCatchUpBonusMph = 30f;
    [Tooltip("Centre-to-centre gap (m) to the nearest car ahead at which the far catch-up starts to ease in.")]
    public float catchUpNearGap = 25f;
    [Tooltip("Centre-to-centre gap (m) to the nearest car ahead at which the full far catch-up applies.")]
    public float catchUpFarGap = 100f;
    [Tooltip("Share of the far catch-up a car may still use in or near a turn, or in the close-up. The corner speed profile still caps it.")]
    [Range(0f, 1f)] public float cornerCatchUpScale = 0.5f;
    [Tooltip("Lowest speed cap (mph) the station-keeping asks for — a floor so a car never crawls to a halt mid-formation. The safety law ignores it: a car will stop behind a stopped car.")]
    public float minCapMph = 12f;
    [Tooltip("Least distance ahead (m) scanned for cars. The planner scans further at speed — far enough to stop gently for a stopped car.")]
    public float avoidScanRange = 26f;
    [Tooltip("Pace multiplier on the SplineDriver corner-speed profile during formation. <1 gives the dynamic model grip margin so it holds the line through turns. Barely affects straights (capped to cruise).")]
    [Range(0.6f, 1f)] public float formationPace = 0.9f;

    [Tooltip("This car has no dynamic model (cheap kinematic AI): stay on the kinematic SplineDriver through the green flag instead of handing back to PlayerVehicleController. Set by the spawner for a kinematic field.")]
    public bool kinematic;

    [Tooltip("Grid slot that picks this car's column (even = left, odd = right). -1 = the SplineDriver's qualifyingPosition. Set for the player's own car when the AI drives it, whose SplineDriver never had a grid slot.")]
    public int columnSlot = -1;

    // The player's own car, driven by the AI for a while (TakeOver). Only here to form it up: it lets go of the
    // car at the green, or when the human takes the wheel back, and puts every knob it touched back as it was.
    bool _takeover;
    float _takeoverPace = 1f;
    static bool s_addingTakeover;

    [Header("Two-wide formation")]
    [Tooltip("Pack into two columns this far (m) either side of centre — half the lateral gap between the two cars in a row. The field runs double-file the WHOLE formation lap, so it's paired up ready for the start. Must comfortably exceed weaveAmplitude + half a car width: the columns and the weave are ACTIVE AT THE SAME TIME, and anti-phase weave eats 2*weaveAmplitude of the pair gap.")]
    public float columnHalfOffset = 2.2f;
    [Tooltip("Keep the two columns this far (m) inside the AI's road limits (which are where a car's CENTRE may go, 1.4 m in from the edge). The column is placed off the racing line, and where the line hugs the inside - Texas's Turn 4 kink, just before the line - the inside car was squeezed onto the limit with 0.4 m of tarmac beside it; the dynamic cars run 1-1.5 m wide of their target there under braking, so at the green the inside row went onto the grass. Gives way before the pair's spacing on a narrow road. Ovals only: on a road course (Watkins Glen, 14 m) it pinned the pair to the middle as the line swung near the start and the rows touched at the green.")]
    public float columnEdgeMarginM = 1.5f;
    [Tooltip("Longitudinal gap (m, centre to centre) each row asks for behind the row ahead while closed up. 6 m is " +
             "nose to tail - about a metre of daylight between bumpers, as a real field stacks up for the green. The " +
             "close-up safety law (closeUpReactionSec, closeUpStandstillClearM, closeUpStationBufferM) has the final " +
             "say: at the 50 mph close-up pace it holds rows ~6.4 m apart. At 8 m the field still went green with " +
             "3.5 m between bumpers.")]
    public float rowGap = 6f;
    [Tooltip("Through a turn, scale the column offset by this (0..1) so the pair eases toward centre but STAYS paired — never collapses to single file (which is what made the field look single-file on a road course whose close-up zone contains corners). The pair sits 2 x columnHalfOffset x this apart in a turn: keep that over ~3.2 m. Two cars 2.6 m apart clip each other as soon as one draws half alongside with a few degrees of yaw, which a tight road-course corner always gives them. The pair is fitted inside the road (FormationLanes.FitColumn), so a wider spacing no longer pushes the outside car off it.")]
    [Range(0f, 1f)] public float cornerColumnScale = 0.8f;

    [Header("Close-up before the green")]
    [Tooltip("In the close-up — from the moment the pace car speeds up to peel off, to the green — how much faster " +
             "(mph) than the front row (FormationDirector.closeUpLeaderMph) a car behind it may run, so the gaps " +
             "actually close: everything behind gains on the front row until station keeping (rowGap, " +
             "closeUpHeadwaySec) stops it. In or near a turn it is scaled by cornerCatchUpScale. Keep it under " +
             "pack.BlockSpeedDeltaMph, or a car closing on the back of the stacked-up rows takes them for a " +
             "blockage and swerves alongside instead of tucking in.")]
    public float closeUpBonusMph = 16f;
    [Tooltip("Time headway (s) the station keeping holds in the close-up, in place of pack.HeadwaySec. At formation " +
             "pace the normal 0.55 s is ~15 m centre to centre, which is a strung-out train, not rows. Kept under " +
             "rowGap / close-up pace (0.2 s = 4.5 m at 50 mph) so it never sets the gap.")]
    public float closeUpHeadwaySec = 0.2f;
    [Tooltip("Clearance (m) the station keeping leaves above what the safety law needs in the close-up, in place of " +
             "pack.StationBufferM. The safety law itself is unchanged.")]
    public float closeUpStationBufferM = 0.25f;
    [Tooltip("Station keeping's dead band (m) in the close-up, in place of pack.GapDeadbandM. A car closing from " +
             "behind stops gaining at the FAR edge of the band, so with the normal 1.5 m every row sat 1.5 m further " +
             "back than its station — Daytona's rows settled at ~10 m centre to centre.")]
    public float closeUpGapDeadbandM = 0.25f;
    [Tooltip("The safety law's reaction allowance (s) in the close-up, in place of pack.ReactionSec. Behind the " +
             "safety car the field is kinematic and answers its speed command the next step; the reaction distance " +
             "(this x speed) is the biggest part of the row gap at close-up pace. 0.04 s = two physics steps of " +
             "latency (the scan reads the car ahead's last step). The human's car keeps its own extra room " +
             "(humanBrakeFactor).")]
    public float closeUpReactionSec = 0.04f;
    [Tooltip("The safety law's bumper-to-bumper clearance after a full stop (m) in the close-up, in place of " +
             "pack.StandstillClearM. With every car rolling at the same pace and able to brake as hard as the one " +
             "ahead, this is pure margin on top of the row gap; 1 m of it was a metre of daylight between every row.")]
    public float closeUpStandstillClearM = 0.5f;
    [Tooltip("In the close-up, most a car may run over the front row's pace (mph) to reach its own place in the " +
             "stacked-up field — its row's worth of settled gaps behind the front (FormationLanes.CloseUpCatchUpMph). " +
             "Stacking up only off the car ahead closed the field one row at a time, and the back of it was still " +
             "strung out at the green (Darlington 18 m rows, a 383 m field). On a road course: full on straights, half " +
             "through a sweeping turn, none into a tight one; on an oval half only in a tight turn. 0 = off.")]
    public float closeUpCatchUpMaxMph = 35f;
    [Tooltip("Close-up catch-up (mph) per metre a car is short of its place in the field. Linear, so neighbours differ " +
             "in pace by this times the gap between them alone and every gap closes at once (0.5 = the gap halves in " +
             "about 3 s). A car's closing speed on the one directly ahead falls with the gap, so it arrives at the " +
             "train's pace rather than as a much faster car the planner would take for a blockage.")]
    public float closeUpCatchUpMphPerMetre = 0.8f;
    [Tooltip("Metres short of its place in the close-up at which the catch-up has run out; closeUpBonusMph and the " +
             "station keeping take the car the rest of the way.")]
    public float closeUpArriveMarginM = 1f;
    [Tooltip("In the close-up, pace (mph) added per metre this car is behind its row partner (the other car of its " +
             "grid row, in the other column) — or taken off per metre it is ahead — so the pair draws level side by " +
             "side. Station keeping still has the final say.")]
    public float pairAlignMphPerMetre = 0.5f;
    [Tooltip("NR2003's pace laps: every car is anchored to its own slot behind the safety car (paceCarGap + its row x " +
             "targetGap), not just to the car ahead of it, so gaps can't pile up down a long field and a car well short " +
             "of its slot closes on it hard. Extra mph per metre short of the slot (capped at farCatchUpBonusMph), and " +
             "off per metre past it (capped at slotAnchorMaxEaseMph). 0 = off.")]
    public float slotAnchorMphPerMetre = 0.4f;
    public float slotAnchorMaxEaseMph = 4f;
    [Tooltip("Most the slot anchor may add (mph). Half of it through a banked oval turn and none into a tight corner - closing on a slot into one ran cars into each other at Watkins Glen.")]
    public float slotAnchorMaxMph = 15f;
    [Tooltip("Most the row-pair alignment adds or takes off (mph).")]
    public float pairAlignMaxMph = 5f;
    [Tooltip("Row partners further apart than this (m) are not lined up yet — one is still filing out — and are left alone.")]
    public float pairAlignRangeM = 40f;

    [Header("Seeing and avoiding the cars around")]
    [Tooltip("Station keeping, the never-hit safety law, lane widths and the swerve-alongside rules. Defaults are the values the pace-lap tests (PackAvoidanceTests) were run with.")]
    public PackSettings pack = new PackSettings();
    [Tooltip("Braking authority (mph/sec) granted when the safety law has to act — overrides the (possibly weak) decel curve so an emergency slow actually lands. Also what every AI car is assumed able to brake at, so it must be the same for the whole field.")]
    public float avoidHardDecelMphPerSec = 30f;
    [Tooltip("The human driver is assumed able to stop this many times harder than an AI car (a spin, a wall, a stamp on the brakes), so the AI leaves them more room.")]
    public float humanBrakeFactor = 1.5f;
    [Tooltip("A free car further than this (m) across from the pit centreline is on the track, not in the pit lane.")]
    public float pitLaneHalfWidth = 7f;

    [Header("Closing up under a full-course caution")]
    [Tooltip("When the caution comes out a car drives on round at this share of its racing pace (its speed plan, corners and all) until it reaches the train behind the safety car. It used to be held at once to cruise plus a crawl allowance, and the whole field stood on the brakes at the flag.")]
    [Range(0.5f, 1f)] public float cautionApproachPace = CautionRules.ApproachPace;
    [Tooltip("The lift (m/s^2) from racing pace down to cautionApproachPace as the flag comes out, and the most the caution's own speed cap may fall by per second. The pack planner still brakes harder for a car right in front.")]
    public float cautionEaseDecelMps2 = CautionRules.ApproachDecelMps2;
    [Tooltip("Under a caution a car keeps station at the approach's short headway and gentle braking (CautionApproachPack) until its caution cap is down within this (mph) of cruise - at the train - and at the formation pack's spacing after, which leaves room in the train for a car still alongside to fall in.")]
    public float cautionSingleFileMph = 15f;
    [Tooltip("Falling in single file under a caution: with a car in another lane, level or up to this far (m) clear ahead of me, I drop back behind it. At least the pack planner's SideWindowAheadM (6): the planner only moves me across once the car ahead in the lane I am moving into is that far clear, and a car parked 3 m back from it ran the whole caution in the outside lane.")]
    public float cautionFallInClearM = 7f;
    [Tooltip("...running this much (mph) slower than it until its lane opens up.")]
    public float cautionFallInMph = 6f;
    [Tooltip("Under a caution a car closing on the one ahead runs no faster than it could shed its closing speed at this rate (m/s^2) by the time it is cautionArriveMarginM behind its station — so it arrives at that car's pace, never as a much faster car the planner would take for a blockage and pass. Off the car ahead's ACTUAL speed: one still running the approach pace up the road holds nobody back. Nobody passes under the caution.")]
    public float cautionCloseDecelMps2 = 2f;
    [Tooltip("Metres beyond the station gap (targetGap off a car, paceCarGap off the safety car) by which a car closing up under a caution is down to the speed of the one ahead.")]
    public float cautionArriveMarginM = 16f;

    // Caution approach state: the racing pace the car had when the flag came out (-1 = not under a caution), the pace
    // its speed plan runs at now (eased down from that), and the caution's own speed cap (mph, eased).
    float _racePace = -1f, _cautionPaceNow = 1f, _cautionCapMph = float.PositiveInfinity;

    [Header("Caught up in the crash (caution)")]
    [Tooltip("A car caught up in the crash goes to the tail of the restart order. While a car due ahead of it is this close behind it (m), it slows and moves down to the inside to be passed.")]
    public float cautionYieldLookBehindM = 250f;
    [Tooltip("How far under cruise (mph) a caught car runs while it lets the field by.")]
    public float cautionYieldSlowMph = 15f;
    [Tooltip("How far in from the inside edge of the road (m, to the car's centre) a caught car runs while it is being passed.")]
    public float cautionYieldEdgeInsetM = 1.6f;
    [Tooltip("Centre-to-centre lateral (m) a car passing a caught car keeps from it - it goes by on the outside.")]
    public float cautionPassClearM = 3.8f;
    [Tooltip("How far ahead (m) a car looks for a caught car moving over to let it by.")]
    public float cautionPassScanM = 70f;
    [Tooltip("Sideways rate (m/s) of the move down to the inside, and of the passing car's move out round it.")]
    public float cautionYieldSlewPerSec = 2.5f;

    [Header("Round a wreck that blocks the road (caution)")]
    [Tooltip("Under a caution, a car stopped behind a wreck with no way round on the road drives round it on the grass or across the infield (SplineDriver.allowGrass, out to the nearest wall). The pack planner only ever swerves round the one car ahead, within the road: behind a pile-up across the track the field stopped and never moved again.")]
    public bool escapeWrecks = true;
    [Tooltip("Slower than this (mph), with a stopped car in my lane up ahead, I am stuck behind it.")]
    public float escapeStuckMph = 6f;
    [Tooltip("Seconds stuck before going round - the planner's own swerve, on the road, gets the first go.")]
    public float escapeAfterSeconds = 2f;
    [Tooltip("Daylight (m) kept from each stopped car gone round.")]
    public float escapeClearM = 1.2f;
    [Tooltip("Speed (mph) while still getting out from behind a stopped car - a crawl, steering out.")]
    public float escapeCreepMph = 5f;
    [Tooltip("Fastest (mph) while going round the wreck, and while still out on the grass afterwards.")]
    public float escapeMaxMph = 20f;
    [Tooltip("Sideways rate (m/s) of the move out round the wreck.")]
    public float escapeSlewPerSec = 3f;
    [Tooltip("Longest (s) one way round is kept before looking again.")]
    public float escapeMaxSeconds = 25f;

    [Header("Corner caution")]
    [Tooltip("Distance ahead (m) scanned for a turn. Inside this, the car drops the catch-up boost and the weave so it can hold the racing line through the corner.")]
    public float cornerLookahead = 55f;

    [Header("Pit-out settle")]
    [Tooltip("After rejoining the main track from the pit, hold this gentle speed (mph) with no weave for a moment so the car eases onto the racing line instead of being snapped across by the merge.")]
    public float pitOutMph = 45f;
    [Tooltip("How long (s) the pit-out settle lasts after leaving the pit lane.")]
    public float pitOutSettleSeconds = 3f;
    [Tooltip("Steepest angle (deg) a car filing out for the formation lap takes off the grey parked-box strip onto the pit lane's driving line. The grid spawn parks cars at the box-lane lateral; without the pull-out they ride that offset single-file down the wall for the whole lane. The lateral follows an S-curve over the distance the car rolls (FormationLanes.PullOutLateral), so the body points gently across and back - a fixed lateral rate in time swung it ~48 degrees at a crawl and the rear flailed.")]
    [Range(2f, 30f)] public float pitPullOutAngleDeg = 8f;
    [Tooltip("Shortest pull-out (m), for a box so near the pit exit that the angle above would not fit before the merge.")]
    public float pitPullOutMinLength = 8f;

    [Header("Tyre-warming weave")]
    [Tooltip("Lateral weave amplitude (m) on straights. Suppressed in/near turns and at pit-out. Layered on TOP of the two-wide column offset with a per-slot phase, so anti-phase neighbours close their pair gap by up to twice this — keep it well under columnHalfOffset minus a car width.")]
    public float weaveAmplitude = 0.45f;
    [Tooltip("Weave frequency (Hz). Low = slow gentle sway the dynamic model can follow.")]
    public float weaveHz = 0.18f;
    [Tooltip("Phase offset (radians) added per grid slot, so the train snakes instead of weaving in lockstep.")]
    public float weavePhasePerSlot = 0.8f;
    [Tooltip("How fast the weave offset is allowed to change (m/s). Lower = smoother, less likely to snap the car off line.")]
    public float weaveSlewPerSec = 1.5f;
    [Tooltip("Seconds over which the weave fades back in after a pit-out settle or corner, so it never snaps on.")]
    public float weaveRampSeconds = 3f;
    [Tooltip("Seconds over which the weave fades OUT for a corner, a swerve or the close-up. Quick on purpose: the columns pull in for a turn at the same moment, and anti-phase weave on narrowed columns closes a pair to a touch.")]
    public float weaveFadeOutSeconds = 0.75f;

    const float DefaultHalfLength = 2.4f; // GridSpawner.collisionHalfExtents
    const float DefaultHalfWidth = 1f;
    const float PitLaneOpenCap = 200f;    // SplineDriver clamps a pit-lane car to the pit limit anyway

    SplineDriver _spline;
    PlayerVehicleController _pvc;
    SplineInputDriver _input;
    PitStopController _pit;
    readonly PackPlanner _planner = new PackPlanner();
    SplineDriver _passSc;      // under a caution, the safety car while it is out picking up a leader that isn't me
    PackSettings _closeUpPack; // pack with the close-up headway and buffer
    PackSettings _cautionPack; // the close-up pack with the caution's gentle comfort braking
    float _prevLine;           // last step's racing-line lateral, for how fast the line is swinging across the road
    bool _hasPrevLine;
    int _ovalTrack = -1;   // -1 unknown, 0 road course, 1 oval: columnEdgeMarginM applies on ovals only
    const float MaxLineRate = 10f; // m/s; a jump faster than this is a seam (the pit merge), not the line moving
    readonly List<PackCar> _cars = new List<PackCar>();
    readonly List<WreckEscape.Car> _escapeCars = new List<WreckEscape.Car>();
    readonly List<PackCar> _movingCars = new List<PackCar>();
    readonly Dictionary<int, SplineDriver> _stoppedById = new Dictionary<int, SplineDriver>();
    float _blockedTimer;    // seconds stuck behind a stopped car (escapeWrecks)
    int _escapeSide;        // +1 / -1 while going round a wreck; 0 not
    float _escapeTarget;    // lateral (m) to go round it at
    float _escapeToGo;      // metres still to run to be past it
    float _escapeTimer;
    float _lateral;     // current applied tacticalLateralOffset (slew-limited)
    float _weaveEnv;    // 0..1 envelope so the weave ramps in gently
    bool _wasPit;
    bool _pullingOut;      // filing out of the pit box: lateralOffset follows the pull-out S-curve
    float _pullOutFrom;    // box-strip lateral the pull-out started from
    float _pullOutTravel;  // metres rolled since
    float _pullOutLength;  // metres the pull-out takes
    float _pitOutTimer;
    float _halfLength = DefaultHalfLength;
    float _halfWidth = DefaultHalfWidth;
    float _savedLineFactor; // the car's own racing line, parked during formation and restored at green
    bool _lineFactorSaved;

    // Every formation car, and a lookup from its SplineDriver, so a car can read where its neighbours are
    // heading across the track (a car sliding into my lane is in my lane before it gets there).
    public static readonly List<FormationController> Active = new();
    static readonly Dictionary<SplineDriver, FormationController> s_bySpline = new();
    static readonly Dictionary<int, Vector2> s_extents = new(); // GameObject id → (half-length, half-width)

    public SplineDriver Spline => _spline;
    // Where this car is heading across the track (main-track lateral, m), and whether that is known yet.
    public float PlannedTrackLateral { get; private set; }
    public bool HasPlan { get; private set; }

    // --- Diagnostics (read by FormationDiagnostics, the F8 overlay). Last frame's view of the planner, so a pace
    //     lap can be watched car-by-car instead of inferred from the wreckage.
    public float DbgGap { get; private set; }            // bumper-to-bumper clearance to the car followed; -1 = nothing
    public float DbgSafeClearance { get; private set; }  // under this the safety law is braking
    public float DbgStationGap { get; private set; }     // centre-to-centre gap the station keeping holds
    public float DbgClosingMph { get; private set; }     // + = catching the car ahead
    public float DbgCap { get; private set; }            // commanded speed cap after all limits
    public PackMode DbgMode { get; private set; }
    public bool DbgSettling { get; private set; }
    public bool DbgOnPit { get; private set; }
    public bool DbgPaceCarAhead { get; private set; }

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        ResolveExtents(gameObject, out _halfLength, out _halfWidth);
    }

    void OnEnable()
    {
        Active.Add(this);
        if (_spline != null) s_bySpline[_spline] = this;
        RaceStart.PhaseChanged += OnPhaseChanged;
        // TakeOver adds this component and only then marks it as the player's car; applying the phase in
        // between would briefly wake the human's PlayerVehicleController under the AI.
        if (!s_addingTakeover) OnPhaseChanged(RaceStart.Current);
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (_spline != null && s_bySpline.TryGetValue(_spline, out var fc) && fc == this) s_bySpline.Remove(_spline);
        RaceStart.PhaseChanged -= OnPhaseChanged;
        HasPlan = false;
        if (_takeover) ReleaseTakeover();
    }

    // The human's car handed to the AI (Auto, crew chief). A bare SplineDriver just drives its racing speed
    // profile — no cap, no idea anything is in front of it — so on the formation lap it ran at race pace straight
    // up the back of the train. Give it the same formation brain as every other car.
    //
    // `dynamicModel`: the car was handed over on its dynamic model with AIRacingBehaviour on it (AIHandover), as
    // every field car is. Then this stays on it for the whole time the AI has it, like on any field car — kinematic
    // behind the safety car, dynamic again at the green with the column handed to the racing brain, back in the
    // train for a caution. Without one the car is kinematic only, and this lets go of it at the green and returns
    // null once the race is green (the formation has nothing to do then).
    public static FormationController TakeOver(SplineDriver spline, int gridSlot, bool dynamicModel = false)
    {
        if (spline == null || (!dynamicModel && RaceStart.IsGreen)) return null;
        var fc = spline.GetComponent<FormationController>();
        if (fc == null)
        {
            s_addingTakeover = true;
            try { fc = spline.gameObject.AddComponent<FormationController>(); }
            finally { s_addingTakeover = false; }
        }
        fc.enabled = false; // re-enable below so OnEnable re-applies the current phase with the fields set
        fc.kinematic = !dynamicModel; // kinematic: the AI drives it on its SplineDriver; never wake its PlayerVehicleController
        fc.columnSlot = gridSlot;
        fc._takeover = true;
        fc._takeoverPace = spline.paceMultiplier;
        fc.enabled = true;
        return fc;
    }

    // The human takes the wheel back: stop steering the car and hand its SplineDriver back untouched.
    public static void HandBack(SplineDriver spline)
    {
        if (spline == null) return;
        var fc = spline.GetComponent<FormationController>();
        if (fc != null && fc._takeover) fc.enabled = false;
    }

    void ReleaseTakeover()
    {
        _takeover = false;
        _lateral = 0f;
        _weaveEnv = 0f;
        _planner.Reset();
        if (_spline == null) return;
        _spline.aiMaxSpeedMph = float.MaxValue;
        _spline.aiMinDecelMphPerSec = 0f;
        _spline.aiSpeedBoostMph = 0f;
        _spline.tacticalLateralOffset = 0f;
        _spline.paceMultiplier = _takeoverPace;
        if (_lineFactorSaved)
        {
            _spline.lineFactor = _savedLineFactor;
            _lineFactorSaved = false;
        }
    }

    // The dynamic bicycle model + pure-pursuit steering is twitchy at parade speeds and spins cars off the
    // line. For the formation lap we drive KINEMATICALLY instead — SplineDriver glues the car to the racing
    // line (exactly like the safety car). At green we hand back to the dynamic model for racing, re-seeding it
    // with the car's current pose + speed for a smooth rolling start.
    void OnPhaseChanged(RaceStart.Phase phase)
    {
        // The player's car the AI was only minding for the formation, kinematically: let go of it at the green, the
        // same moment AIRacingBehaviour picks up every other car. One on its dynamic model goes on like the rest.
        if (_takeover && kinematic && phase == RaceStart.Phase.Green)
        {
            enabled = false; // OnDisable releases every knob
            return;
        }

        // Lazy-fetch: GridSpawner may add this component before the dynamic-model components exist.
        if (_pvc == null) _pvc = GetComponent<PlayerVehicleController>();
        if (_input == null) _input = GetComponent<SplineInputDriver>();
        if (_pit == null) _pit = GetComponent<PitStopController>();

        // The collider can be sized after this component was added, so read it again now.
        ResolveExtents(gameObject, out _halfLength, out _halfWidth, refresh: true);

        bool underSafetyCar = phase == RaceStart.Phase.Formation || phase == RaceStart.Phase.Caution;

        // Out of a caution, the next one starts again from whatever racing pace the car has then.
        if (phase != RaceStart.Phase.Caution) _racePace = -1f;

        // A caution comes out with the car racing on its dynamic model, wherever that has put it (sliding, on the
        // apron, a lane over from its brain's point). Latch the spline onto where the car actually IS before it
        // takes the transform, or the switch teleports it back onto the brain's pose. The racing brain's lateral
        // carries on as the planner's starting tactical so nothing snaps across the road either.
        if (phase == RaceStart.Phase.Caution && _spline != null && _spline.isActiveAndEnabled && _spline.externalMotionController)
        {
            _lateral = _spline.tacticalLateralOffset;
            _weaveEnv = 0f;
            _planner.Reset();
            _spline.EngageFromCurrentPose(_pvc != null ? _pvc.SpeedMph : _spline.CurrentMph);
        }

        // Kinematic behind the safety car; a kinematic car (no dynamic model) also stays kinematic for racing.
        bool driveKinematic = underSafetyCar || kinematic;
        if (driveKinematic)
        {
            if (_input != null) _input.enabled = false;
            if (_pvc != null) _pvc.enabled = false;
            if (_spline != null) _spline.externalMotionController = false; // SplineDriver writes the transform
        }
        else
        {
            // Dynamic again (PreGrid frozen-hold, or Green racing). Re-enabling SplineInputDriver re-asserts
            // externalMotionController and re-seeds the dynamic model from the current spline pose + speed.
            //
            // ...but only on its own next FixedUpdate, and the model is switched on now. Until then it still holds the
            // speed and heading it had when it was last switched off - for a car handed over to Auto while rolling
            // (somewhere round the formation lap) or one that went into a caution at racing speed, a car doing 50 mph
            // pointing the way the road went a lap ago. CautionWatch read exactly that as a car spinning (moving, 30+
            // degrees off the road), held it as a wreck for wreckHoldSeconds, and at the green the cars behind the
            // player's Auto car stopped short of it and went for the grass at 30 mph (phone 0.8.24, Daytona, 20 cars
            // wrecked). So the model is seeded here, from the pose the spline has been placing the car at, before
            // anything can read it. Only coming back from kinematic (the model was off): a car whose model was running
            // keeps its own state.
            if (_pvc != null && !_pvc.enabled && _spline != null && _spline.TrackLength > 0f && _spline.track != null
                && !_spline.externalMotionController)
            {
                Vector3 seed = _spline.track.transform.TransformPoint(
                    new Vector3(_spline.CommandedLocalPos.x, _spline.CommandedLocalPos.y, 0f));
                _pvc.SeedPose(new Vector2(seed.x, seed.y), _spline.CommandedHeadingDeg, _spline.CommandedSpeedMps);
            }
            if (_pvc != null) _pvc.enabled = true;
            if (_input != null) _input.enabled = true;
            else if (_spline != null) _spline.externalMotionController = true;
        }

        // The pair columns are offsets from each car's OWN racing line, and per-driver lines (lineFactor,
        // skewed by AIDriverBinding aggression) all converge at an apex — offset or not, two different base
        // paths can cross there and put a pair in contact. Run the whole field on the SAME line (ideal) for
        // the formation lap so the pair separation is exactly the column difference everywhere.
        if (underSafetyCar && _spline != null && !_lineFactorSaved)
        {
            _savedLineFactor = _spline.lineFactor;
            _spline.lineFactor = 0f;
            _lineFactorSaved = true;
        }

        // Leaving the formation lap or a caution (green or pregrid): drop any weave/lateral the formation applied —
        // AIRacingBehaviour owns the lateral line once racing.
        if (!underSafetyCar)
        {
            _lateral = 0f;
            _weaveEnv = 0f;
            _planner.Reset(); // no brake-rate history or swerve latch carried into the green
            HasPlan = false;
            if (_spline != null)
            {
                // At the green the column offset is handed to the racing brain, which eases it toward its own
                // line. Zeroed here, the car was re-seeded onto its brain's pose in the same step - the whole
                // double-file field jumped onto one line, on top of each other.
                var racer = phase == RaceStart.Phase.Green ? GetComponent<AIRacingBehaviour>() : null;
                if (racer != null && racer.enabled) racer.TakeLateral(_spline.tacticalLateralOffset);
                else _spline.tacticalLateralOffset = 0f;
                _spline.aiMinDecelMphPerSec = 0f;
                if (_lineFactorSaved)
                {
                    // The dynamic model steers over to the restored line via pure pursuit — no lateral snap.
                    _spline.lineFactor = _savedLineFactor;
                    _lineFactorSaved = false;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (_spline == null || _spline.TrackLength <= 0f) return;
        if (!RaceStart.UnderSafetyCar) return;
        bool caution = RaceStart.IsCaution;
        if (!caution || _spline.usePitLane) { ClearWreckEscape(); _spline.allowGrass = false; }
        // Under a caution, lining up two-wide for the restart: this car has a slot in the restart line-up, which stands in
        // for its grid slot from here on (MySlot), and it runs the formation lap's rules rather than the caution's single
        // file. Single file nose to tail, the cars went green 6 m apart in one lane: inside the racing brain's
        // hardFollowGap, which caps a car to 60% of the speed of the one in front - a car slowing hard on the green and
        // the car behind into it.
        bool doubled = caution && MySlot() >= 0;
        // Under a caution the safety car comes out of the pits and crawls round on the inside until the restart leader is
        // up behind it (SafetyCar.PickingUp). Till then it is not the car to keep station off: every car but the leader
        // drives past it on the outside.
        var scNow = caution && FormationDirector.Instance != null ? FormationDirector.Instance.SafetyCarInstance : null;
        _passSc = scNow != null && scNow.PassableBy(transform) ? scNow.Driver : null;

        float dt = Time.fixedDeltaTime;

        // In for a stop under the caution: on pit road the PitStopController owns the car — its box approach, the
        // cut across to the box strip and its own lane ACC. Taking the lane over from it here would ease the car
        // back off the strip and lift its braking for the box. Out on the track it is still part of the train.
        if (caution && _spline.usePitLane && _pit != null && _pit.isActiveAndEnabled && _pit.IsPitting)
        {
            _wasPit = true;
            _hasPrevLine = false;
            DbgOnPit = true;
            HasPlan = false;
            _lateral = _spline.tacticalLateralOffset;
            return;
        }

        float cruise = FormationDirector.Instance != null ? FormationDirector.Instance.cruiseMph : cruiseMph;
        bool closingUp = FormationDirector.Instance != null && FormationDirector.Instance.FieldClosingUp;
        _planner.Settings = closingUp && !_spline.usePitLane ? CloseUpPack()
                          : caution && !doubled && !_spline.usePitLane && _cautionCapMph > cruise + cautionSingleFileMph
                              ? CautionApproachPack()   // still driving on round to the train
                              : pack;                   // in the train: room between cars to fall in single file

        // Filing out of the pit lane: pull off the parked box strip onto the pit centreline once rolling, and keep
        // station off whatever is in front down the lane — including the cars that have already rejoined the
        // track just past the exit, which is exactly where the queue behind them used to pile in. Only the
        // formation pull-out eases the box lateral here: practice stints manage their own box-lane lateral in
        // both directions (PracticeAIStint).
        if (_spline.usePitLane)
        {
            _wasPit = true;
            _hasPrevLine = false;
            DbgOnPit = true;
            DbgSettling = false;
            HasPlan = false;
            StepPullOut(dt);
            if (_spline.IsOnPit) PitLaneStep(dt, cruise);
            return;
        }
        DbgOnPit = false;
        _pullingOut = false;
        // Just rejoined the main track — start the pit-out settle so the merge doesn't snap the car off line.
        if (_wasPit) { _wasPit = false; _pitOutTimer = pitOutSettleSeconds; _weaveEnv = 0f; }
        bool settling = _pitOutTimer > 0f;
        if (settling) _pitOutTimer -= dt;

        // Corner awareness: in or approaching a turn, hold the racing line — no catch-up overspeed, no weave.
        var phase = _spline.CurrentPhase;
        bool inTurn = phase == SplineDriver.CornerPhase.Entry || phase == SplineDriver.CornerPhase.Apex ||
                      phase == SplineDriver.CornerPhase.Exit || phase == SplineDriver.CornerPhase.Approach;
        bool corner = inTurn || _spline.NextTurnSign(cornerLookahead) != 0;

        // Base pace: gentle while settling after the pit merge (a ceiling — the planner still follows underneath
        // it), otherwise free to close the train up on an open straight and held to cruise in a corner.
        // A car the train ahead has got away from (the human on pole, typically) may run harder to close back up:
        // eased in by how far ahead the next car is, so a car already in the train is unaffected.
        // In the close-up the front row eases down to the director's close-up pace and everything behind it runs a
        // little faster, so the field concertinas up into rows behind it; each car also draws level with its row
        // partner (PairAlign).
        float far = FarCatchUp(out bool leading, out float aheadGap, out bool aheadIsPace,
                               out float frontGap, out int carsAhead, out float aheadMph);
        float farBonus = farCatchUpBonusMph * far;
        // Under caution the field is in running order, not grid order - until it has its restart slots.
        float anchor = caution && !doubled ? 0f : SlotAnchorMph();
        // Under caution: drive on round at most of racing pace (Apply runs the speed plan at _cautionPaceNow), and come
        // down onto the car in front (or the safety car) only as the road to it runs out - off that car's own speed, so a
        // car still running the approach pace up the road holds nobody back. In the close-up its rules take over.
        bool cautionApproach = caution && !doubled && !closingUp && !settling;
        if (caution)
        {
            if (_racePace < 0f)
            {
                _racePace = Mathf.Max(0.1f, _spline.paceMultiplier);
                _cautionPaceNow = _racePace;
                _cautionCapMph = float.PositiveInfinity;
                // Carry on from the lane the car is racing in (a kinematic field car's racing brain moved it; this
                // controller's own lateral is whatever the last formation left), or Apply snaps it onto the line.
                _lateral = _spline.tacticalLateralOffset;
                // Nor carry the planner's last formation-lap cap into racing speed: its comfort braking would start
                // from that and stamp the car down to it.
                _planner.Reset(_spline.CurrentMph);
            }
            // Ease the pace down: the speed falls ~ cautionEaseDecelMps2 (speed scales with pace).
            float v = Mathf.Max(_spline.CurrentMph * PackAvoidance.MphToMps, 10f);
            _cautionPaceNow = Mathf.MoveTowards(_cautionPaceNow, _racePace * cautionApproachPace,
                                                cautionEaseDecelMps2 * _cautionPaceNow / v * dt);
        }
        float cautionBonus = 0f;
        if (cautionApproach)
        {
            float capMph = aheadGap < float.MaxValue
                ? CautionRules.ApproachCapMps(aheadGap, (aheadIsPace ? paceCarGap : targetGap) + cautionArriveMarginM,
                                              aheadMph * PackAvoidance.MphToMps, cautionCloseDecelMps2) * PackAvoidance.MpsToMph
                : 999f;
            capMph = Mathf.Max(cruise, capMph);
            // Never a stamp on the brakes from the caution itself: the cap falls no faster than the ease, starting
            // from the car's own speed.
            if (float.IsPositiveInfinity(_cautionCapMph)) _cautionCapMph = Mathf.Max(capMph, _spline.CurrentMph);
            _cautionCapMph = CautionRules.EaseDown(_cautionCapMph * PackAvoidance.MphToMps, capMph * PackAvoidance.MphToMps,
                                                   cautionEaseDecelMps2, dt) * PackAvoidance.MpsToMph;
            cautionBonus = Mathf.Max(0f, _cautionCapMph - cruise);
        }
        else if (!caution) _cautionCapMph = float.PositiveInfinity;
        float baseCap;
        if (settling) baseCap = pitOutMph;
        else if (cautionApproach)
            // Kinematic behind the safety car, so the speed plan (at the approach pace) is the corner limit.
            baseCap = cruise + cautionBonus;
        else if (closingUp)
        {
            float frontRow = FormationDirector.Instance.CloseUpLeaderMph;
            float align = caution && !doubled ? 0f : PairAlign();
            float bonus = leading ? 0f : closeUpBonusMph * (corner ? cornerCatchUpScale : 1f);
            // Short of my place in the stacked-up field (rows ahead x a settled row's gap, behind the front): close
            // on it, every car at once rather than row after row. By the road's own curvature, as the slot anchor.
            if (!leading && closeUpCatchUpMaxMph > 0f)
            {
                var ps = CloseUpPack();
                float spacing = FormationLanes.CloseUpSpacingM(frontRow, rowGap, ps.HeadwaySec, ps.MinGap,
                    2f * DefaultHalfLength, ps.StandstillClearM, ps.StationBufferM, ps.ReactionSec);
                float catchUp = FormationLanes.CloseUpCatchUpMph(frontGap, carsAhead, caution && !doubled ? 1 : 2, spacing,
                    closeUpArriveMarginM, closeUpCatchUpMphPerMetre, closeUpCatchUpMaxMph);
                // On an oval the field is kinematic and its corner speed profile caps it, so a sweeping banked turn
                // takes the whole of it — Darlington's close-up runs through Turns 3 and 4.
                float r = _spline.CurvatureRadiusAhead(50f);
                float share = OnOval() ? (r > 150f ? 1f : 0.5f)
                                       : (r > 400f ? 1f : r > 150f ? 0.5f : 0f);
                bonus = Mathf.Max(bonus, catchUp * share);
            }
            baseCap = frontRow + bonus + (corner ? Mathf.Min(0f, align) : align);
        }
        else
        {
            baseCap = corner ? cruise + farBonus * cornerCatchUpScale
                             : cruise + Mathf.Lerp(catchUpBonusMph, Mathf.Max(catchUpBonusMph, farCatchUpBonusMph), far);
            // Short of my slot behind the safety car: close on it (harder on a straight). Past it: ease off a touch.
            if (anchor > 0f)
            {
                // By the road's own curvature, not the corner phase (judged at race speed it calls almost the whole
                // of a formation lap a corner): full on the straights, half through the turns.
                float r = _spline.CurvatureRadiusAhead(50f);
                float share = r > 400f ? 1f : r > 150f ? 0.5f : 0f;
                baseCap = Mathf.Max(baseCap, cruise + anchor * share);
            }
            else if (anchor < 0f) baseCap = Mathf.Min(baseCap, cruise + anchor);
        }

        // Lateral intent: the two-wide column the WHOLE lap (double file), with a gentle tyre-warming weave on early
        // straights that fades out near the line so the rows sit steady for the start. Through turns the column
        // eases toward centre (cornerColumnScale) but never collapses to single file.
        // Under caution: single file on the line, no weave - two-wide on the columns once lined up for the restart.
        bool weaveOk = !caution && !settling && !corner && !closingUp && !_planner.Swerving;
        _weaveEnv = FormationLanes.StepEnvelope(_weaveEnv, weaveOk, dt, weaveRampSeconds, weaveFadeOutSeconds);
        int slot = MySlot();
        float column = caution && !doubled ? 0f : FormationLanes.Column(slot, columnHalfOffset, corner, cornerColumnScale);

        // Round a wreck on the grass (escapeWrecks): the bounds take the grass in while going round and until back on
        // the road.
        _spline.allowGrass = caution && escapeWrecks && (_escapeSide != 0 || OutOnTheGrass());
        float boundLo = float.NegativeInfinity, boundHi = float.PositiveInfinity;
        _spline.GetLateralBounds(out boundLo, out boundHi);

        // Under a caution, a car caught up in the crash drops to the tail: with a car due ahead of it coming up behind,
        // it slows and moves down to the inside, and the cars due ahead go by on the outside of it. Nobody else passes.
        bool yielding = false, makingWay = false;
        if (caution && !closingUp && !settling)
        {
            float infield = _spline.InfieldSide;
            yielding = CautionLineup.ShouldYield(transform, _spline.CentreDistanceOnTrack, _spline.TrackLength,
                                                 _spline.track, cautionYieldLookBehindM);
            if (yielding)
            {
                // Down to the let-by pace at the caution's gentle lift, not all at once from racing speed.
                float yieldCap = Mathf.Max(minCapMph, cruise - cautionYieldSlowMph);
                yieldCap = Mathf.Max(yieldCap, _spline.CurrentMph - cautionEaseDecelMps2 * PackAvoidance.MpsToMph * dt);
                baseCap = Mathf.Min(baseCap, yieldCap);
                if (infield != 0f && !float.IsInfinity(boundLo) && !float.IsInfinity(boundHi))
                {
                    float yieldLat = infield > 0f ? boundHi - cautionYieldEdgeInsetM : boundLo + cautionYieldEdgeInsetM;
                    column = yieldLat - _spline.UntacticalLateral;
                    makingWay = true;
                }
            }
            else if (infield != 0f)
            {
                // Round a caught car moving over, or the safety car crawling on the inside: whichever needs me further
                // out.
                float yLat = 0f;
                bool passCaught = !CautionLineup.IsCaught(transform) && YieldingAhead(out yLat);
                bool passSc = SafetyCarToPass(out float scLat);
                if (passSc && (!passCaught || (infield > 0f ? scLat < yLat : scLat > yLat))) yLat = scLat;
                if (passCaught || passSc)
                {
                    float lineNow = _spline.UntacticalLateral;
                    float passLat = yLat - infield * cautionPassClearM;
                    float want = infield > 0f ? Mathf.Min(lineNow, passLat) : Mathf.Max(lineNow, passLat);
                    column = want - lineNow;
                    makingWay = true;
                }
            }
        }
        CautionLineup.SetYielding(transform, yielding);
        if (_ovalTrack < 0)
        {
            var trackInfo = _spline.track != null ? _spline.track.track : null;
            _ovalTrack = trackInfo != null && !Draftmaster.Tracks.TrackDimensions.IsRoadCourse(trackInfo.name) ? 1 : 0;
        }
        // Not while still merging off pit road (outside the limits altogether): there the margin only moves the
        // target under cars still nosing out of their boxes (Darlington's pit exit).
        float carLat = _spline.LateralOnTrack;
        bool onTheRoad = carLat >= boundLo && carLat <= boundHi;
        float edgeMargin = _ovalTrack == 1 && onTheRoad ? columnEdgeMarginM : 0f;
        // Making way under a caution is one car moving over to a spot, not half of a two-wide pair: fitted as a pair it
        // was slid across with its imaginary partner, and a car going by the safety car on the outside ran to the wall.
        float fitted = makingWay
            ? FormationLanes.FitOffset(column, _spline.UntacticalLateral, boundLo, boundHi, edgeMargin)
            : FormationLanes.FitColumn(column, _spline.UntacticalLateral, boundLo, boundHi, edgeMargin);
        // Squeezed against the road edge, the column offset (relative to the racing line) has to keep up with the line
        // swinging across a corner — at the gentle weave slew the inside car lagged into its partner alongside. Only
        // in the close-up, where the rows are drawn level side by side; the staggered train before it never needed it.
        bool squeezed = Mathf.Abs(fitted - column) > 0.05f;
        // Start lanes (lane-AI ovals): the two columns run the two low start lines instead - a fixed height above the
        // inside edge and well apart, not offsets either side of the racing line, which swings up the banking through
        // every turn and the trioval. The column toward the infield takes the low line. The car keeps to it after the
        // green until up to speed (SplineDriver.startLanes).
        int startSide = 0;
        if ((!caution || doubled) && !settling && !makingWay && !_spline.usePitLane && column != 0f && _spline.startLanes
            && OvalLaneDirector.Applies && OvalLaneDirector.Enabled)
        {
            float infield = _spline.InfieldSide;
            int side = infield == 0f ? 0 : column * infield > 0f ? -1 : 1;
            float at = _spline.CentreDistanceOnTrack;
            if (side != 0 && _spline.TryStartLaneLateralAt(-1, at, out float lowLat) && _spline.TryStartLaneLateralAt(1, at, out float highLat))
            {
                startSide = side;
                // The pair fitted inside the car-centre limits as the columns are (FitColumn): the limits pinch in
                // under the road's width in places (Daytona's pit exit), and clamped there the low car was pushed up
                // into its partner.
                float mid = 0.5f * (lowLat + highLat), half = 0.5f * Mathf.Abs(highLat - lowLat);
                float mine = (side < 0 ? lowLat : highLat) - mid;
                // No columnEdgeMarginM: the start lanes keep their own room from the edges.
                float fit = FormationLanes.FitColumn(mine >= 0f ? half : -half, mid, boundLo, boundHi);
                fitted = mid + fit - _spline.UntacticalLateral;
            }
        }
        _spline.SetStartLane(startSide);
        column = fitted;
        float line = _spline.UntacticalLateral;
        float lineRate = _hasPrevLine ? Mathf.Min(Mathf.Abs(line - _prevLine) / Mathf.Max(dt, 1e-4f), MaxLineRate) : 0f;
        _prevLine = line;
        _hasPrevLine = true;
        float weave = FormationLanes.Weave(Time.time, slot, weaveAmplitude, weaveHz, weavePhasePerSlot, _weaveEnv);

        float len = _spline.TrackLength;
        float scanAhead = _planner.ScanAheadM(_spline.CurrentMph, Mathf.Max(avoidScanRange, paceCarGap + 12f));
        GatherTrack(len, scanAhead, _planner.ScanBehindM);

        float merge = _spline.MergeLateralBias;
        var self = SelfState(merge);
        self.TrackLo = boundLo;
        self.TrackHi = boundHi;

        // Falling in single file under the caution: a car in another lane, level or a little ahead, keeps its place
        // and I drop back behind it - a few mph slower, at the caution's lift - so a hole opens in its lane for me to
        // move into. The planner only waits for the lane to clear, and two cars that both waited ran on side by side
        // the whole caution, the lanes drifting past each other. (All at once at the flag, a two- and three-wide pack
        // collapsing onto one line stood on the brakes; this sorts it out a car at a time at a gentle lift.)
        // Not a car going by one caught up in the crash, which is moving over to let it past.
        bool singleFiling = caution && !doubled && !settling && !yielding && !makingWay;
        if (singleFiling)
        {
            for (int i = 0; i < _cars.Count; i++)
            {
                var c = _cars[i];
                if (c.IsPaceCar || c.Gap <= 0f || c.Gap > self.HalfLength + c.HalfLength + cautionFallInClearM) continue;
                if (_passSc != null && c.Id == _passSc.GetInstanceID()) continue;   // going by it, not in behind it
                if (Mathf.Abs(c.Lateral - self.Lateral) < self.HalfWidth + c.HalfWidth) continue; // in my lane: followed
                float behind = Mathf.Max(minCapMph, c.SpeedMph - cautionFallInMph);
                behind = Mathf.Max(behind, _spline.CurrentMph - cautionEaseDecelMps2 * PackAvoidance.MpsToMph * dt);
                baseCap = Mathf.Min(baseCap, behind);
            }
        }
        var intent = new PackIntent
        {
            BaseCapMph = baseCap,
            MaxCapMph = yielding || cautionApproach ? baseCap
                      : Mathf.Max(baseCap, cruise + Mathf.Max(catchUpBonusMph, Mathf.Max(farBonus, Mathf.Max(anchor, cautionBonus)))),
            FloorMph = minCapMph,
            WantGap = closingUp ? rowGap : targetGap,
            PaceCarGap = paceCarGap,
            // Keeping station off the pace car is held to cruise so nobody chases its peel-away — but a car with
            // the human out of its lane up ahead follows the pace car too, and held to cruise it could never close
            // on a human already running cruise behind it. The far catch-up lifts that ceiling for that car only.
            // Under caution the safety car can be a long way up the road from the car now leading the train (the
            // leader pitted): the caution catch-up lifts it the same way.
            CruiseMph = yielding || cautionApproach ? baseCap : cruise + Mathf.Max(farBonus, cautionBonus),
            ColumnTactical = column + weave,
            // On a start lane the column is the gap from the racing line to a fixed line: it must keep up with the
            // racing line swinging across the road as the squeezed pair's does.
            ColumnSlew = (squeezed && closingUp) || startSide != 0 ? Mathf.Max(weaveSlewPerSec, pack.SwerveSlewPerSec) + lineRate
                       : makingWay ? Mathf.Max(weaveSlewPerSec, cautionYieldSlewPerSec) : weaveSlewPerSec,
            // Never slip sideways mid-merge — that lateral snap is exactly what the settle exists to prevent.
            AllowSwerve = !settling,
            // In the close-up too: the rows are stacked tight on purpose, and a car closing on a moving one brakes
            // into its row rather than pulling out alongside it into the other column.
            // Under caution too: nobody passes a moving car behind the safety car — a car that comes out of the pits
            // behind another stays behind it. A stopped or crawling car (a blockage) is still driven round.
            NoUrgentSwerve = corner || closingUp || caution,
        };
        var cmd = _planner.Step(self, intent, _cars, dt);
        if (caution && escapeWrecks) StepWreckEscape(self, ref cmd, dt);
        else ClearWreckEscape();
        Apply(cmd, dt);

        // Where I'm heading across the track, for the cars around me — without the merge bias, which is easing out.
        PlannedTrackLateral = _planner.PlannedLateral(self.Lateral - self.Tactical, self.Lateral) - merge;
        HasPlan = true;
        DbgSettling = settling;
    }

    // Off the box strip onto the pit lane's driving line, by distance rolled rather than by time (see
    // pitPullOutAngleDeg): a car held in the queue stays where it is, pointing where it was.
    void StepPullOut(float dt)
    {
        if (!_pullingOut)
        {
            if (Mathf.Abs(_spline.lateralOffset) < 0.01f) return;
            _pullingOut = true;
            _pullOutFrom = _spline.lateralOffset;
            _pullOutTravel = 0f;
            // On the line before the merge: SplineDriver zeroes lateralOffset at the pit exit and eases whatever is
            // left out as a merge bias, which is a sideways slide of its own.
            float room = _spline.IsOnPit && _spline.PitLength > 0f
                ? (_spline.PitLength * _spline.pitExitThreshold - _spline.DistanceOnTrack) * 0.8f
                : 0f;
            _pullOutLength = FormationLanes.PullOutLength(_pullOutFrom, pitPullOutAngleDeg, room, pitPullOutMinLength);
        }
        _pullOutTravel += Mathf.Max(0f, _spline.CurrentMph) / 2.237f * dt;
        _spline.lateralOffset = FormationLanes.PullOutLateral(_pullOutFrom, _pullOutTravel, _pullOutLength);
        if (_pullOutTravel >= _pullOutLength) _pullingOut = false;
    }

    // 0..1: how far away the nearest car ahead on the track is, from catchUpNearGap (0) to catchUpFarGap (1). Looks
    // round the whole lap, not just the planner's scan, because a car that has got away is out of scan range by
    // definition. 0 when the nearest car ahead is the pace car (the leader holds a long gap to it on purpose) or
    // there is nobody ahead at all. A car more than half a lap "ahead" is really the back of the field behind: once
    // the pace car has pitted the leader would otherwise chase its own tail.
    // `leading`: the front row — nothing ahead but the pace car (or nothing), not counting my own row partner beside
    // me, whom PairAlign lines me up with instead of the close-up bonus running me past them.
    // `aheadGap` / `aheadIsPace`: the road (m) to the nearest car ahead and whether it is the safety car;
    // float.MaxValue with nobody. Under a caution, while the safety car is still out on the track, the whole lap is
    // looked round: the safety car is always in front of the leader then, so the tail of the field can never be
    // mistaken for the car ahead, and a car that pitted can come out more than half a lap behind the train.
    // `frontGap` / `carsAhead`: the road (m) to the front-most car of the field ahead of me (not the safety car) and
    // how many cars there are between it and me, it included — for the close-up's place in the stacked-up field.
    float FarCatchUp(out bool leading, out float aheadGap, out bool aheadIsPace, out float frontGap, out int carsAhead,
                     out float aheadMph)
    {
        float len = _spline.TrackLength;
        float myC = _spline.CentreDistanceOnTrack;
        // No row partners under caution until the field doubles up for the restart (MySlot is -1 till then).
        int partner = MySlot() >= 0 ? MySlot() ^ 1 : int.MinValue;
        bool scOut = RaceStart.IsCaution && FormationDirector.Instance != null && !FormationDirector.Instance.FieldClosingUp;
        float reach = scOut ? len : len * 0.5f;
        float best = float.MaxValue, bestOther = float.MaxValue, bestMph = 0f;
        bool bestIsPace = false, bestOtherIsPace = false;
        float front = 0f;
        int count = 0;

        // Under a caution a caught car moving over to let me by is not the car I close up on - the one beyond it is.
        bool passCaught = RaceStart.IsCaution && !CautionLineup.IsCaught(transform);
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || d.IsOnPit || !d.isActiveAndEnabled) continue;
            if (Mathf.Abs(d.TrackLength - len) > 0.5f) continue;
            if (passCaught && CautionLineup.IsYielding(d.transform)) continue;
            bool pace = d.qualifyingPosition == FormationOrder.SafetyCarGrid;
            if (pace && d == _passSc) continue;   // picking up the leader: driven past, not closed up on
            Consider(Mathf.Repeat(d.CentreDistanceOnTrack - myC, len), pace, !pace && SlotOf(d) == partner, d.CurrentMph);
        }
        var humans = RaceObstacles.All;
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || p.ObstacleTrack != _spline.track) continue;
            if (passCaught && CautionLineup.IsYielding(p.transform)) continue;
            int pSlot = HumanSlot(p);
            Consider(Mathf.Repeat(p.TrackDistance - myC, len), false, pSlot >= 0 && pSlot == partner, p.SpeedMph);
        }

        leading = bestOther == float.MaxValue || bestOtherIsPace;
        aheadGap = best;
        aheadIsPace = bestIsPace;
        aheadMph = bestMph;
        frontGap = count > 0 ? front : float.MaxValue;
        carsAhead = count;
        if (best == float.MaxValue || bestIsPace) return 0f;
        return Mathf.Clamp01((best - catchUpNearGap) / Mathf.Max(1f, catchUpFarGap - catchUpNearGap));

        void Consider(float gap, bool pace, bool isPartner, float mph)
        {
            if (gap <= 0f || gap >= reach) return;
            if (gap < best) { best = gap; bestIsPace = pace; bestMph = mph; }
            if (!isPartner && gap < bestOther) { bestOther = gap; bestOtherIsPace = pace; }
            if (!pace) { count++; if (gap > front) front = gap; }
        }
    }

    // This car's slot: its grid slot on the formation lap; under a caution its slot in the two-wide restart line-up, or
    // -1 while the field still runs single file (CautionLineup.Slot).
    int MySlot() => RaceStart.IsCaution ? CautionLineup.Slot(transform)
                  : columnSlot >= 0 ? columnSlot : _spline.qualifyingPosition;

    // The free-driven human's slot, the same way.
    static int HumanSlot(PlayerVehicleController p) => RaceStart.IsCaution ? CautionLineup.Slot(p.transform) : p.GridPosition;

    // The nearest caught car moving over to be passed, from alongside to cautionPassScanM up the road: where it is
    // across the track.
    bool YieldingAhead(out float lateral)
    {
        lateral = 0f;
        float len = _spline.TrackLength;
        float myC = _spline.CentreDistanceOnTrack;
        float best = float.MaxValue;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || d.IsOnPit || !d.isActiveAndEnabled || !CautionLineup.IsYielding(d.transform)) continue;
            if (Mathf.Abs(d.TrackLength - len) > 0.5f) continue;
            float g = PackAvoidance.SignedGap(d.CentreDistanceOnTrack, myC, len);
            if (g < -8f || g > cautionPassScanM || Mathf.Abs(g) >= best) continue;
            best = Mathf.Abs(g);
            lateral = d.LateralOnTrack;
        }
        var humans = RaceObstacles.All;
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || p.ObstacleTrack != _spline.track || !CautionLineup.IsYielding(p.transform)) continue;
            float g = PackAvoidance.SignedGap(p.TrackDistance, myC, len);
            if (g < -8f || g > cautionPassScanM || Mathf.Abs(g) >= best) continue;
            best = Mathf.Abs(g);
            lateral = p.TrackLateral;
        }
        return best < float.MaxValue;
    }

    // The safety car out picking up the leader (_passSc), on the track from just alongside to as far up the road as the
    // planner looks: where it is across the track, to go by on the outside of it. Looked for as far ahead as the planner
    // scans - the field comes up to it at most of racing pace, and started round it only at cautionPassScanM it was
    // already braking for it in its lane.
    bool SafetyCarToPass(out float lateral)
    {
        lateral = 0f;
        var d = _passSc;
        if (d == null || !d.isActiveAndEnabled || d.IsOnPit) return false;
        float len = _spline.TrackLength;
        if (Mathf.Abs(d.TrackLength - len) > 0.5f) return false;
        float g = PackAvoidance.SignedGap(d.CentreDistanceOnTrack, _spline.CentreDistanceOnTrack, len);
        float scan = Mathf.Max(cautionPassScanM, _planner.ScanAheadM(_spline.CurrentMph, Mathf.Max(avoidScanRange, paceCarGap + 12f)));
        if (g < -8f || g > scan) return false;
        lateral = d.LateralOnTrack;
        return true;
    }

    // This car's own track is an oval (its tuning races in lanes) - read from the track itself, once.
    bool OnOval()
    {
        if (_ovalKnown) return _oval;
        var info = _spline.track != null ? _spline.track.track : null;
        if (info == null) return false;
        _oval = TrackProfile.ForTrack(info.name).raceInLanes;
        _ovalKnown = true;
        return _oval;
    }
    bool _oval, _ovalKnown;

    // mph to add (+, short of my slot) or take off (-, past it): my slot is paceCarGap + my row x targetGap behind the
    // safety car. Zero with no safety car on the track, or for a slot the anchor doesn't know.
    float SlotAnchorMph()
    {
        int slot = MySlot();
        // Ovals only for now: at Watkins Glen even the straights-only anchor ran three pairs together on the formation.
        if (slotAnchorMphPerMetre <= 0f || slot < 0 || !OnOval()) return 0f;
        float len = _spline.TrackLength;
        float myC = _spline.CentreDistanceOnTrack;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d.qualifyingPosition != FormationOrder.SafetyCarGrid || d.IsOnPit || !d.isActiveAndEnabled) continue;
            if (Mathf.Abs(d.TrackLength - len) > 0.5f) return 0f;
            float behind = Mathf.Repeat(d.CentreDistanceOnTrack - myC, len);
            if (behind > len * 0.5f) return 0f;   // the safety car is behind me: not lined up behind it yet
            float error = behind - (paceCarGap + (slot / 2) * targetGap);
            return error > 0f ? Mathf.Min(error * slotAnchorMphPerMetre, slotAnchorMaxMph)
                              : Mathf.Max(error * slotAnchorMphPerMetre, -slotAnchorMaxEaseMph);
        }
        return 0f;
    }

    static int SlotOf(SplineDriver d) =>
        RaceStart.IsCaution ? CautionLineup.Slot(d.transform)
        : s_bySpline.TryGetValue(d, out var fc) && fc != null && fc.columnSlot >= 0 ? fc.columnSlot : d.qualifyingPosition;

    // mph to add (+) or take off (-) to draw level with my row partner: grid slots 2k and 2k+1 share a row. Zero with
    // no partner on the track, or one too far away to be lining up with yet.
    float PairAlign()
    {
        int slot = MySlot();
        if (slot < 0) return 0f;
        int partnerSlot = slot ^ 1;
        float len = _spline.TrackLength;
        float myC = _spline.CentreDistanceOnTrack;
        for (int i = 0; i < Active.Count; i++)
        {
            var fc = Active[i];
            if (fc == null || fc == this || fc._spline == null || !fc.isActiveAndEnabled) continue;
            if (fc.MySlot() != partnerSlot) continue;
            if (fc._spline.usePitLane || Mathf.Abs(fc._spline.TrackLength - len) > 0.5f) return 0f;
            return Align(PackAvoidance.SignedGap(fc._spline.CentreDistanceOnTrack, myC, len));
        }
        var humans = RaceObstacles.All; // the human's own grid slot, beside an AI car
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || HumanSlot(p) != partnerSlot || p.ObstacleTrack != _spline.track) continue;
            return Align(PackAvoidance.SignedGap(p.TrackDistance, myC, len));
        }
        return 0f;

        float Align(float ahead) => Mathf.Abs(ahead) > pairAlignRangeM
            ? 0f
            : Mathf.Clamp(ahead * pairAlignMphPerMetre, -pairAlignMaxMph, pairAlignMaxMph);
    }

    // The pack settings with the close-up's tighter station keeping: a copy made once, so the serialized settings are untouched.
    PackSettings CloseUpPack()
    {
        if (_closeUpPack != null) return _closeUpPack;
        _closeUpPack = JsonUtility.FromJson<PackSettings>(JsonUtility.ToJson(pack));
        _closeUpPack.HeadwaySec = Mathf.Min(pack.HeadwaySec, closeUpHeadwaySec);
        _closeUpPack.StationBufferM = Mathf.Min(pack.StationBufferM, closeUpStationBufferM);
        _closeUpPack.GapDeadbandM = Mathf.Min(pack.GapDeadbandM, closeUpGapDeadbandM);
        _closeUpPack.ReactionSec = Mathf.Min(pack.ReactionSec, closeUpReactionSec);
        _closeUpPack.StandstillClearM = Mathf.Min(pack.StandstillClearM, closeUpStandstillClearM);
        return _closeUpPack;
    }

    // Under a caution, driving on round to the train: the field comes out of racing nose to tail, and the formation's
    // 0.55 s headway (40 m at 170 mph) had every car in a draft line braking at the comfort limit to open the gap -
    // the whole pack stood on the brakes at the flag. Station off the car in front at the close-up's short headway,
    // and comfort braking no firmer than the caution's gentle lift. The safety law is untouched.
    PackSettings CautionApproachPack()
    {
        if (_cautionPack != null) return _cautionPack;
        _cautionPack = JsonUtility.FromJson<PackSettings>(JsonUtility.ToJson(CloseUpPack()));
        _cautionPack.MaxBrakeMphPerSec = Mathf.Min(pack.MaxBrakeMphPerSec, cautionEaseDecelMps2 * PackAvoidance.MpsToMph);
        // Behind the safety car every car is kinematic and answers its speed command the same step: no reaction
        // distance to keep. At 170 mph even the close-up's 0.04 s was 3 m, and a draft line running a metre apart
        // stood on the brakes the instant the flag came out to open it.
        _cautionPack.ReactionSec = 0f;
        return _cautionPack;
    }

    // Down the pit lane: station keeping and the safety law only. No swerving, no columns.
    void PitLaneStep(float dt, float cruise)
    {
        float scanAhead = _planner.ScanAheadM(_spline.CurrentMph, Mathf.Max(avoidScanRange, targetGap + 12f));
        GatherPitLane(scanAhead, _planner.ScanBehindM);

        var self = SelfState(0f);
        var intent = new PackIntent
        {
            BaseCapMph = PitLaneOpenCap,
            MaxCapMph = PitLaneOpenCap,
            FloorMph = 0f,
            WantGap = targetGap,
            PaceCarGap = paceCarGap,
            CruiseMph = cruise,
            ColumnTactical = _lateral,
            ColumnSlew = weaveSlewPerSec,
            AllowSwerve = false,
        };
        var cmd = _planner.Step(self, intent, _cars, dt);
        Apply(cmd, dt);
    }

    PackSelf SelfState(float drift) => new PackSelf
    {
        SpeedMph = _spline.CurrentMph,
        Lateral = _spline.LateralOnTrack,
        Tactical = _lateral,
        HalfLength = _halfLength,
        HalfWidth = _halfWidth,
        BrakeMphPerSec = BrakeAuthority(_spline),
        TrackLo = float.NegativeInfinity,
        TrackHi = float.PositiveInfinity,
        Drift = drift,
    };

    // Stuck behind a stopped car with no way round on the road (escapeWrecks): after escapeAfterSeconds, find the way
    // round the whole pile-up - on the road if there is one, else on the grass - and drive it at a crawl until out from
    // behind, then at escapeMaxMph until past the last stopped car. The planner's lateral is overridden for that, and its
    // speed too where it is only braking for the stopped cars being gone round; a moving car in my way still has it
    // brake.
    void StepWreckEscape(in PackSelf self, ref PackCommand cmd, float dt)
    {
        var es = WreckEscape.Settings.Default;
        es.ClearM = escapeClearM;
        es.StoppedMph = pack != null ? pack.StoppedDeadMph : es.StoppedMph;
        var me = new WreckEscape.Self { Lateral = self.Lateral, HalfWidth = self.HalfWidth, HalfLength = self.HalfLength };
        // Stopped AI cars by id, to read where their bodies really lie across the road (a wreck is spun and sideways).
        _stoppedById.Clear();
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d != null && d != _spline && !d.IsOnPit && d.CurrentMph < es.StoppedMph) _stoppedById[d.GetInstanceID()] = d;
        }
        _escapeCars.Clear();
        for (int i = 0; i < _cars.Count; i++)
        {
            var c = _cars[i];
            // Each car where the planner sees it - from where it is to where it is heading - and a stopped one's body
            // as it really lies too.
            float lo = c.LaneLo, hi = c.LaneHi;
            if (!c.IsPaceCar && _stoppedById.TryGetValue(c.Id, out var sd)
                && sd.BodyLateralExtent(c.HalfLength, c.HalfWidth, out float bodyLo, out float bodyHi))
            {
                lo = Mathf.Min(lo, bodyLo);
                hi = Mathf.Max(hi, bodyHi);
            }
            // The safety car is never part of a wreck, but it is in the way like any moving car.
            _escapeCars.Add(new WreckEscape.Car
            {
                Gap = c.Gap, Lateral = 0.5f * (lo + hi), HalfWidth = 0.5f * (hi - lo), HalfLength = c.HalfLength,
                SpeedMph = c.IsPaceCar ? Mathf.Max(c.SpeedMph, es.StoppedMph + 1f) : c.SpeedMph,
            });
        }

        // Done going round but still out on the grass: slowly, until back on the road.
        if (_escapeSide == 0 && _spline.allowGrass) cmd.CapMph = Mathf.Min(cmd.CapMph, escapeMaxMph);

        if (_escapeSide == 0)
        {
            int blocker = self.SpeedMph < escapeStuckMph ? WreckEscape.StoppedBlocker(me, _escapeCars, es) : -1;
            _blockedTimer = blocker >= 0 ? _blockedTimer + dt : 0f;
            if (blocker < 0 || _blockedTimer < escapeAfterSeconds) return;

            // The road first; the grass only when the wreck fills it. Limits held from here to past the pile-up, and
            // measured from the car's centre (the brain's distance is a little ahead of it).
            float from = -SplineDriver.PathPointAheadOfCentre;
            float to = _escapeCars[blocker].Gap + es.LookPastM - SplineDriver.PathPointAheadOfCentre;
            int side = 0;
            float target = 0f, past = 0f;
            if (_spline.GetBoundsOver(from, to, false, false, out float lo, out float hi))
                side = WreckEscape.FindWayRound(me, _escapeCars, blocker, lo, hi, es, out target, out past);
            if (side == 0 && _spline.GetBoundsOver(from, to, true, true, out lo, out hi))
                side = WreckEscape.FindWayRound(me, _escapeCars, blocker, lo, hi, es, out target, out past);
            if (side == 0) return;   // nowhere yet: look again next step
            _escapeSide = side;
            _escapeTarget = target;
            _escapeToGo = past;
            _escapeTimer = 0f;
            _blockedTimer = 0f;
            _spline.allowGrass = true;
        }

        _escapeTimer += dt;
        _escapeToGo -= Mathf.Max(0f, self.SpeedMph) / 2.237f * dt;
        if (_escapeToGo <= 0f || _escapeTimer > escapeMaxSeconds) { ClearWreckEscape(); return; }

        // Across to the spot - unless a moving car (another going round, the safety car) is in, or about to be in, the
        // lane I would move into: then hold my line and let the brakes do it, as the planner would.
        _movingCars.Clear();
        for (int i = 0; i < _cars.Count; i++)
            if (_cars[i].IsPaceCar || _cars[i].SpeedMph >= es.StoppedMph) _movingCars.Add(_cars[i]);
        bool laneShut = _planner.LaneBlocked(self, _escapeTarget, _movingCars, PackPlanner.NoCar);
        // Off the line the car's own lateral is laid on - not off where it is now, which a placed car only reaches as fast
        // as it rolls (SplineDriver path angle): measured from there, the lag fed back and ran the target out to the wall.
        cmd.TacticalTarget = laneShut ? self.Tactical : _escapeTarget - _spline.UntacticalLateral;
        cmd.Slew = Mathf.Max(cmd.Slew, escapeSlewPerSec);

        // What is in my way: stopped cars my box (a little wider) still overlaps, and moving cars in the strip from
        // here to the spot.
        float boxLo = self.Lateral - self.HalfWidth - 0.15f, boxHi = self.Lateral + self.HalfWidth + 0.15f;
        float stripLo = Mathf.Min(self.Lateral, _escapeTarget) - self.HalfWidth, stripHi = Mathf.Max(self.Lateral, _escapeTarget) + self.HalfWidth;
        float stoppedClear = float.MaxValue;
        bool movingInWay = false;
        float followCap = float.MaxValue;
        for (int i = 0; i < _escapeCars.Count; i++)
        {
            var c = _escapeCars[i];
            if (c.Gap <= 0f) continue;
            float clear = c.Gap - self.HalfLength - c.HalfLength;
            if (clear > 30f) continue;
            float cLo = c.Lateral - c.HalfWidth, cHi = c.Lateral + c.HalfWidth;
            if (c.SpeedMph < es.StoppedMph)
            {
                if (cHi > boxLo && cLo < boxHi) stoppedClear = Mathf.Min(stoppedClear, clear);
            }
            else if (cHi > stripLo && cLo < stripHi)
            {
                movingInWay = true;
                // Another car going round the same way, or traffic: never run up its back - its speed, plus a little
                // for every metre of room past 2.
                followCap = Mathf.Min(followCap, c.SpeedMph + Mathf.Max(0f, clear - 2f));
            }
        }
        float cap = Mathf.Min(escapeMaxMph, followCap);
        if (stoppedClear < float.MaxValue)
        {
            // Still getting out from behind a stopped car: a crawl, steering out (SplineDriver.grassEscapePathAngleDeg
            // lets a placed car get across steeply). Never a dead stop - a car that cannot roll cannot steer out, and
            // would sit there for good; the last tenths are at walking pace.
            float room = Mathf.Max(0f, stoppedClear - 0.15f);
            cap = Mathf.Min(cap, Mathf.Max(1f, escapeCreepMph * Mathf.Clamp01(room) + room * 2f));
        }
        // Nothing moving in my way: whatever the planner says, up to the escape pace (it is braking for the very cars
        // being gone round). Something moving in the way, or my lane over shut: its cap stands.
        cmd.CapMph = movingInWay || laneShut ? Mathf.Min(cmd.CapMph, cap) : cap;
    }

    void ClearWreckEscape()
    {
        _escapeSide = 0;
        _blockedTimer = 0f;
        _escapeTimer = 0f;
    }

    // Back from going round a wreck but still out on the grass: it stays open (and slow) until the car is on the road.
    bool OutOnTheGrass()
    {
        if (!_spline.allowGrass || !_spline.GetBoundsAhead(0f, true, false, out float lo, out float hi)) return false;
        float lat = _spline.LateralOnTrack;
        return lat < lo - 0.2f || lat > hi + 0.2f;
    }

    void Apply(PackCommand cmd, float dt)
    {
        _spline.aiMaxSpeedMph = cmd.CapMph;
        _spline.aiMinDecelMphPerSec = cmd.MinDecelMphPerSec; // brake authority only while the safety law acts
        // Under a caution, until the field is closing up for the restart, the speed plan runs at the eased approach
        // pace (most of the car's racing pace) rather than the formation lap's.
        _spline.paceMultiplier = RaceStart.IsCaution && _racePace > 0f && !(FormationDirector.Instance != null && FormationDirector.Instance.FieldClosingUp)
            ? _cautionPaceNow : formationPace;
        _spline.aiSpeedBoostMph = 0f;
        _lateral = Mathf.MoveTowards(_lateral, cmd.TacticalTarget, cmd.Slew * dt);
        _spline.tacticalLateralOffset = _lateral;

        DbgGap = cmd.HasLeader ? cmd.Clearance : -1f;
        DbgSafeClearance = cmd.SafeClearance;
        DbgStationGap = cmd.StationGap;
        DbgClosingMph = cmd.ClosingMph;
        DbgPaceCarAhead = cmd.LeaderIsPaceCar;
        DbgCap = cmd.CapMph;
        DbgMode = cmd.Mode;
    }

    // The hardest an AI car can be expected to brake: its curve, or the emergency authority the safety law grants.
    float BrakeAuthority(SplineDriver d) => Mathf.Max(avoidHardDecelMphPerSec, d.BrakingMphPerSecAt(d.CurrentMph));

    // Every car near me on the main track, in my frame: AI and the safety car from RaceField, the free-driven
    // player from RaceObstacles. Gaps are between car CENTRES — the planner takes the car lengths off.
    void GatherTrack(float len, float ahead, float behind)
    {
        _cars.Clear();
        float myC = _spline.CentreDistanceOnTrack;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || d.IsOnPit || !d.isActiveAndEnabled) continue;
            if (Mathf.Abs(d.TrackLength - len) > 0.5f) continue;
            float gap = PackAvoidance.SignedGap(d.CentreDistanceOnTrack, myC, len);
            if (gap > ahead || gap < -behind) continue;
            _cars.Add(AiCar(d, gap, d.LateralOnTrack, PlannedLateralOf(d)));
        }

        var humans = RaceObstacles.All;
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || p.ObstacleTrack != _spline.track) continue;
            float gap = PackAvoidance.SignedGap(p.TrackDistance, myC, len);
            if (gap > ahead || gap < -behind) continue;

            // A spun car blocks far more of the road than one pointing down it, and only its motion ALONG the
            // road counts as pace.
            ResolveExtents(p.gameObject, out float hl, out float hw);
            float roadDeg = _spline.MainTangentWorldDeg(p.TrackDistance);
            float yaw = Mathf.DeltaAngle(roadDeg, p.HeadingDeg);
            PackAvoidance.Footprint(hl, hw, yaw, out float along, out float across);
            float alongMph = p.SpeedMph * Mathf.Cos((yaw + p.SlipAngleDeg) * Mathf.Deg2Rad);
            _cars.Add(new PackCar
            {
                Id = p.GetInstanceID(),
                Gap = gap,
                Lateral = p.TrackLateral,
                PlannedLateral = p.TrackLateral,
                SpeedMph = Mathf.Max(0f, alongMph),
                HalfLength = along,
                HalfWidth = across,
                BrakeMphPerSec = avoidHardDecelMphPerSec * Mathf.Max(1f, humanBrakeFactor),
                IsPaceCar = false,
            });
        }
    }

    // Down the pit lane: the cars in the lane with me, the cars that have just rejoined the track past the exit
    // (placed on my path as if the lane carried straight on), and the player if they are in the lane.
    void GatherPitLane(float ahead, float behind)
    {
        _cars.Clear();
        float myC = _spline.CentreDistanceOnPit;
        float myLat = _spline.LateralOnTrack;
        float len = _spline.TrackLength;
        bool exitKnown = _spline.TryGetPitExit(out float exitMainPath, out float exitMainLat, out float exitPitPath);
        float a = SplineDriver.PathPointAheadOfCentre;
        float toExit = exitPitPath - (myC + a); // metres of lane left before I'm handed to the main spline
        float exitCentreMain = exitMainPath - a;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || !d.isActiveAndEnabled) continue;
            if (d.IsOnPit)
            {
                if (Mathf.Abs(d.PitLength - _spline.PitLength) > 0.5f) continue;
                float gap = d.CentreDistanceOnPit - myC;
                if (gap > ahead || gap < -behind) continue;
                _cars.Add(AiCar(d, gap, d.LateralOnTrack, d.LateralOnTrack));
            }
            else if (exitKnown && Mathf.Abs(d.TrackLength - len) <= 0.5f)
            {
                float past = PackAvoidance.SignedGap(d.CentreDistanceOnTrack, exitCentreMain, len);
                if (past < 0f) continue;                    // not on the road out of this pit exit
                float gap = toExit + past;
                if (gap > ahead || gap < -behind) continue;
                // It left along the same lane and eases onto its line at the merge rate, so near the exit it is
                // still in my lane; further on only if it has not drifted far from where the lane comes out.
                float drifted = Mathf.Abs(d.LateralOnTrack - exitMainLat);
                float gate = _halfWidth + DefaultHalfWidth + pack.LaneMarginM + 1f + 0.15f * past;
                if (drifted > gate) continue;
                _cars.Add(AiCar(d, gap, myLat, myLat));
            }
        }

        var humans = RaceObstacles.All;
        for (int i = 0; i < humans.Count; i++)
        {
            var p = humans[i];
            if (p == null || !p.isActiveAndEnabled || p.ObstacleTrack != _spline.track) continue;
            float reach = ahead + 10f;
            if ((p.transform.position - transform.position).sqrMagnitude > reach * reach) continue;
            if (!_spline.ProjectOntoPit(p.transform.position, out float pd, out float plat)) continue;
            if (Mathf.Abs(plat) > pitLaneHalfWidth) continue; // over the wall, on the track
            float gap = pd - myC;
            if (gap > ahead || gap < -behind) continue;
            ResolveExtents(p.gameObject, out float hl, out float hw);
            _cars.Add(new PackCar
            {
                Id = p.GetInstanceID(),
                Gap = gap,
                Lateral = plat,
                PlannedLateral = plat,
                SpeedMph = Mathf.Max(0f, p.SpeedMph),
                HalfLength = hl,
                HalfWidth = Mathf.Max(hw, hl * 0.5f), // no road tangent to judge its yaw against here: be generous
                BrakeMphPerSec = avoidHardDecelMphPerSec * Mathf.Max(1f, humanBrakeFactor),
                IsPaceCar = false,
            });
        }
    }

    PackCar AiCar(SplineDriver d, float gap, float lateral, float planned)
    {
        ResolveExtents(d.gameObject, out float hl, out float hw);
        return new PackCar
        {
            Id = d.GetInstanceID(),
            Gap = gap,
            Lateral = lateral,
            PlannedLateral = planned,
            SpeedMph = d.CurrentMph,
            HalfLength = hl,
            HalfWidth = hw,
            BrakeMphPerSec = BrakeAuthority(d),
            // The safety car out picking up the leader is just a slow car to drive round, in its own lane.
            IsPaceCar = d.qualifyingPosition == FormationOrder.SafetyCarGrid && d != _passSc,
        };
    }

    static float PlannedLateralOf(SplineDriver d)
    {
        if (s_bySpline.TryGetValue(d, out var fc) && fc != null && fc.isActiveAndEnabled && fc.HasPlan)
            return fc.PlannedTrackLateral;
        return d.LateralOnTrack;
    }

    // A car's collision box in metres (half-length, half-width), read once from its VehicleCollision.
    static void ResolveExtents(GameObject go, out float halfLength, out float halfWidth, bool refresh = false)
    {
        int id = go.GetInstanceID();
        if (!refresh && s_extents.TryGetValue(id, out var e))
        {
            halfLength = e.x;
            halfWidth = e.y;
            return;
        }
        halfLength = DefaultHalfLength;
        halfWidth = DefaultHalfWidth;
        var vc = go.GetComponent<VehicleCollision>();
        if (vc != null)
        {
            Vector3 scale = go.transform.lossyScale;
            halfLength = vc.halfExtents.y * Mathf.Abs(scale.x);
            halfWidth = vc.halfExtents.x * Mathf.Abs(scale.y);
        }
        s_extents[id] = new Vector2(halfLength, halfWidth);
    }
}
