using UnityEngine;
using Draftmaster.Sim;

[RequireComponent(typeof(SplineDriver))]
public class AIRacingBehaviour : MonoBehaviour
{
    [Header("Driver Personality (0 = cautious, 1 = aggressive)")]
    [Range(0f, 1f)] public float aggression01 = 0.5f;
    [Tooltip("0 = wildly inconsistent, 1 = metronome. Drives mistake frequency.")]
    [Range(0f, 1f)] public float consistency01 = 0.8f;
    [Tooltip("Per-second probability of a small mistake at consistency=0. Decays toward zero as consistency rises.")]
    public float mistakeProbabilityPerSecond = 0.06f;
    [Tooltip("Length of a single mistake event (sec).")]
    public float mistakeDurationSeconds = 0.7f;
    [Tooltip("Pace multiplier applied during a mistake (e.g. 0.85 = 15% slower).")]
    public float mistakePaceFactor = 0.85f;
    [Tooltip("Extra lateral wobble (m) during a mistake.")]
    public float mistakeWobble = 1.2f;

    [Header("Race Craft")]
    [Tooltip("Let the race phase shape this driver: settle in over the opening laps (wider gaps, fewer lunges) and throw everything at it over the closing ones. Needs a RaceDirector for the race distance; practice/qualifying/multiplayer run the neutral mid-race envelope.")]
    public bool phaseAwareRacecraft = true;
    [Tooltip("Blue flags: a car a lap or more down eases offline and lifts slightly to let the leaders through instead of racing them.")]
    public bool respectBlueFlags = true;
    [Tooltip("Range behind (m) at which a car on a lap we haven't reached starts being let past.")]
    public float blueFlagRange = 45f;
    [Tooltip("Lateral offset (m) held while getting out of a lapper's way.")]
    public float blueFlagOffset = 2.6f;
    [Tooltip("Pace multiplier at full yield — a small lift so the pass actually completes. 1 = don't lift at all.")]
    [Range(0.6f, 1f)] public float blueFlagLift = 0.94f;
    [Tooltip("Range behind (m) over which a pursuer piles pressure on and makes a mistake likelier. Worn tyres raise the odds too.")]
    public float pressureRange = 18f;

    [Header("Detection Ranges (m)")]
    public float lookAheadRange = 70f;
    public float overtakeClosingRange = 25f;
    [Tooltip("Extra overtake-initiation range (m) per mph of closing speed — start easing offline earlier when reeling a slower car in fast, so the AI arrives alongside instead of catching its gearbox then darting.")]
    public float ttcRangePerMph = 0.6f;
    public float minFollowDistance = 5f;
    public float sidewaysRange = 12f;
    public float sidewaysWidth = 3.5f;
    [Tooltip("Distance ahead (m) scanned to pick outside-of-turn passing side.")]
    public float cornerScanDistance = 90f;

    [Header("Rear-end Avoidance")]
    [Tooltip("Range (m) scanned for a slower/stopped car ahead to brake for. Must exceed the braking distance from racing speed, so keep it well above lookAheadRange.")]
    public float brakeScanRange = 130f;
    [Tooltip("Time headway (s) to the car ahead — the gap held even at matched speed scales with our speed. Short enough to sit in the car ahead's draft; the closing-speed braking term still keeps a car from running into a slower one.")]
    public float followHeadwaySeconds = 0.3f;
    [Tooltip("Braking rate (m/s²) assumed when sizing the safe following gap. Lower = brake earlier / more margin.")]
    public float followDecelMps2 = 11f;
    [Tooltip("Gap (m) at which we back off BELOW the car ahead's speed so we don't tap it.")]
    public float hardFollowGap = 6f;
    [Tooltip("Lateral separation (m) below which a car ahead fully blocks our corridor — the follow cap applies at full strength. A car is ~2 m wide, so below this they would be bumper to bumper.")]
    public float corridorOverlapWidth = 1.6f;
    [Tooltip("Lateral separation (m) beyond which a car ahead no longer caps our speed at all — we're clear to drive past. The cap fades between the two widths. This is what lets a committed overtake actually PASS a slow or wrecked car instead of matching its speed until it fully stops, and what lets a car in the next lane run alongside instead of being held to the speed of the one beside it (it was 3.4 m - a whole lane on an 11 m superspeedway - which strung every pack out single file).")]
    public float corridorClearWidth = 2.4f;
    [Tooltip("The clear width where the AI is NOT racing in lanes (road courses): a pass there runs on an out-in-out line through corners, and 0.4 m to spare touched - Watkins Glen's pack sim went from 4 contacts to 20 at 2.4 m.")]
    public float corridorClearWidthNoLanes = 3.4f;

    [Header("Local Yellows")]
    [Tooltip("Lift through the yellow zone before a car stopped on the road (CautionWatch), and don't pass anyone there but the stopped car.")]
    public bool respectYellows = true;
    [Tooltip("The yellow zone starts this far (m) up the road from the stopped car — room to brake from full speed to the yellow pace before reaching it.")]
    public float yellowZoneBeforeMetres = 200f;
    [Tooltip("...and runs this far (m) past it, so nobody floors it alongside the scene.")]
    public float yellowZoneAfterMetres = 40f;
    [Tooltip("Share of the pace the car would carry at that point of the lap it holds through the zone.")]
    [Range(0.3f, 1f)] public float yellowPaceFactor = 0.6f;
    [Tooltip("Never slower than this (mph) through a yellow zone — a lift, not a crawl.")]
    public float yellowFloorMph = 40f;
    [Tooltip("Under yellow only a car slower than this (mph) — the stopped one — is driven round.")]
    public float yellowPassBelowMph = 20f;
    [Tooltip("Centre-to-centre lateral (m) kept from a stopped car while driving round it (a car is ~2 m wide).")]
    public float stoppedPassClearance = 3.5f;
    [Tooltip("Lateral speed (m/s) used to step out round a stopped car — quicker than a racing line change.")]
    public float stoppedPassLateralSpeed = 3f;
    [Tooltip("Right up against a stopped car and not yet out round it, still roll at this (mph) while steering out. A dead stop there never ends: a car only moves sideways while it is rolling, so it waited forever, became a stopped car itself, and the queue behind it grew all race.")]
    public float stoppedCreepMph = 5f;
    [Tooltip("Inside the yellow zone, start lining up on the wide side of the stopped car this far (m) short of it — whatever cars are in between.")]
    public float stoppedLineUpMetres = 150f;

    // True while this car is inside a yellow zone (for HUDs, telemetry and tests).
    public bool UnderYellow { get; private set; }

    [Header("Rolling Start")]
    [Tooltip("Seconds after the green flag during which the follow-distance speed cap is eased in, so the whole field launches together (a rolling start) instead of accordioning out from the leader. The hard nose-to-tail cap still applies, so cars can't pile in. 0 = off (cars hold full racing gaps from the instant of green).")]
    public float launchWindowSeconds = 3f;

    [Header("Lanes")]
    [Tooltip("Race in lanes: the road between the AI's bounds is split into lanes at least laneSpacing apart, a pass moves into the lane beside the car being passed (only when it is clear), a car alongside holds its lane, and a car only drops back to the ideal line when that lane is clear. NR2003's minrace/maxrace. Off = the old fixed overtakeLineOffset step.")]
    public bool useLanes = true;
    // Lanes on for this car AND for this kind of track (TrackConditions.AiLanes: ovals, not road courses).
    bool Lanes => useLanes && TrackConditions.AiLanes;

    [Tooltip("Least distance (m) between lane centres: a car's width and the gap a driver leaves beside another.")]
    public float laneSpacing = 2.4f;
    [Tooltip("A lane counts as taken by a car this far (m) behind us or ahead of us, plus a second of closing speed ahead.")]
    public float laneClearBehind = 7f;
    public float laneClearAhead = 6f;
    [Tooltip("A pass is held until the car being passed is this far (m) behind us - clear by a length - or abandoned once it pulls this far ahead, or after passMaxSeconds.")]
    public float passClearMargin = 2.5f;
    public float passAbandonGap = 45f;
    public float passMaxSeconds = 10f;

    [Header("Pack Racing (superspeedways - TrackConditions.AiPackRacing)")]
    [Tooltip("Centre-to-centre gap (m) held behind the car ahead in a line: a car is 4.8 m long, so this is a bumper's width - bump-drafting distance.")]
    public float packFollowGap = 6.2f;
    [Tooltip("How hard (m/s²) a car is willing to shed closing speed on the car ahead in its line: the closing speed it carries is the speed it could lose at this rate over the room left. Gentle - nobody stands on the brakes in a pack.")]
    public float packCloseDecel = 1.2f;
    [Tooltip("Seconds between lane decisions. A pack changes shape over seconds, not frames.")]
    public float packDecisionInterval = 0.4f;
    [Tooltip("A neighbouring lane is worth moving into when the car that would be ahead of us in it is this much quicker (mph) than the car ahead of us in ours.")]
    public float packLaneSwitchMph = 1.2f;
    [Tooltip("Pulling out into an empty lane needs a run: closing on the car ahead by at least this (mph).")]
    public float packRunMph = 2f;
    [Tooltip("Chance per decision that a car right on the bumper of the car ahead, in its tow, pulls out to start a new lane anyway - at aggression 1. Scales down with aggression.")]
    public float packPullOutChance = 0.08f;
    [Tooltip("Chance a car on the bumper goes with the car ahead when it changes lane (the line follows its leader), at aggression 0. Aggressive drivers stay put and take the gap a little more often.")]
    public float packFollowLeaderChance = 0.85f;
    [Tooltip("A lane with nobody in it this far (m) ahead is empty: a car alone in it tucks into a line beside it if one is near.")]
    public float packEmptyLaneMetres = 60f;
    [Tooltip("Lateral speed (m/s) and acceleration (m/s²) of a lane change in a pack. A move takes about a second: drawn out over two and a half, the car pulling out spent all of it half across its pusher's nose, slowing as it left the draft.")]
    public float packLateralSpeed = 3.2f;
    public float packLateralAccel = 7f;

    [Header("Manoeuvre Strength")]
    [Tooltip("Lateral offset (m) committed during an overtake.")]
    public float overtakeLineOffset = 3f;
    [Tooltip("Max lateral target offset (m) from side-by-side repulsion.")]
    public float sidewaysMaxPush = 2.5f;
    [Tooltip("Lateral offset (m) when defending an inside line from a faster pursuer.")]
    public float defendLineOffset = 2.5f;
    [Tooltip("Range behind (m) to consider a pursuer threatening enough to defend.")]
    public float defendDetectRange = 35f;
    [Tooltip("Contact threshold: cars within this lateral distance (m) and sidewaysRange long are treated as touching. Triggers harder push + speed scrub.")]
    public float contactLateralWidth = 1.1f;
    [Tooltip("Extra lateral push (m) applied during contact.")]
    public float contactPush = 1.5f;
    [Tooltip("Speed scrub (mph) per second during contact.")]
    public float contactSpeedScrub = 8f;
    [Tooltip("Contact scrub never drags the car below this speed (mph). Stops side-by-side pairs grinding to a halt.")]
    public float contactScrubFloorMph = 30f;

    [Header("Rivalry / Payback")]
    [Tooltip("Allow this driver to deliberately wreck a rival when their relationship has fallen below DriverRelationships.PaybackThreshold (NASCAR Thunder style).")]
    public bool enablePayback = true;
    [Tooltip("Seconds between grudge scans for a nearby hated rival.")]
    public float paybackScanInterval = 1.5f;
    [Tooltip("Longitudinal range (m) within which a rival can be targeted.")]
    public float paybackRange = 22f;
    [Tooltip("Seconds a payback move is held: steering into the rival, follow caps ignored.")]
    public float paybackDurationSeconds = 2.2f;
    [Tooltip("Per-rival cooldown (s) after an attempt, so a feud produces occasional lunges rather than constant grinding.")]
    public float paybackCooldownSeconds = 45f;
    [Tooltip("Chance per scan of launching payback right at the threshold. Grows as the relationship rots further below it, and with aggression.")]
    [Range(0f, 1f)] public float paybackBaseChance = 0.2f;
    [Tooltip("Extra speed (mph) requested while ramming, so the hit actually lands.")]
    public float paybackSpeedBoost = 6f;
    [Tooltip("Seconds of continuous drafting behind the same car per +1 relationship — working together builds trust (0 = off).")]
    public float draftBondSeconds = 8f;

    [Header("Stuck Recovery")]
    [Tooltip("Below this speed (mph) while the profile wants much more, the car counts as stalled.")]
    public float stallSpeedMph = 15f;
    [Tooltip("Seconds of continuous stall before recovery kicks in.")]
    public float stallTriggerSeconds = 1.5f;
    [Tooltip("Recovery duration: follow-caps are ignored and the car commits to a side to drive around the blockage.")]
    public float stallRecoverySeconds = 2.5f;

    [Header("Smoothness")]
    [Tooltip("Max lateral speed (m/s) the AI uses when changing line. Lower = smoother, like a real steering rate limit.")]
    public float maxLateralSpeed = 1.6f;
    [Tooltip("Lateral acceleration (m/s²) used to start and finish a line change. A move eases in, carries maxLateralSpeed, and eases out onto its target instead of starting and stopping dead - which is what read as twitchy, and what upset a car on the limit.")]
    public float maxLateralAccel = 4f;
    [Tooltip("Longitudinal window (m, centre to centre) inside which a car counts as ALONGSIDE: the side-by-side push and the contact response only act on a car this close. About a car length; a car further ahead or behind is a car to follow or pass, not one to steer away from.")]
    public float alongsideLength = 5.2f;
    [Tooltip("Dead zone (m). Tactical changes smaller than this aren't acted on.")]
    public float tacticalDeadzone = 0.25f;
    [Tooltip("Once an overtake direction is committed, hold it for at least this many seconds before reconsidering. Prevents flip-flop.")]
    public float commitHoldSeconds = 1.5f;
    [Tooltip("Once the AI returns to neutral line, wait this long before initiating another manoeuvre.")]
    public float manoeuvreCooldown = 0.6f;

    const float MphToMps = 1f / 2.237f;

    SplineDriver _spline;
    float _smoothedTactical;
    float _tacticalVelocity;
    // The pass under way, in lanes: who is being passed, which lane we are passing in, and for how long.
    SplineDriver _passCar;
    int _passLane = -1;
    float _passTimer;
    float _commitTimer;
    float _commitDir;
    float _cooldownTimer;
    bool _wasManoeuvring;
    bool _passAroundStopped;   // the committed pass is round a stopped car at _passTargetLat
    float _passTargetLat;
    float _mistakeTimer;
    float _mistakeWobbleDir;
    float _basePaceMultiplier = 1f;
    float _phaseAggression = 0.5f;
    float _stallTimer;
    float _recoveryTimer;
    float _recoveryDir;
    PracticeAIStint _stint;   // practice stints manage lateralOffset themselves — don't fight them
    // Pack racing: the lane this car is in or committed to, whether it is still moving there, the decision clock.
    int _packLane = -1;
    bool _packMoving;
    float _packDecisionTimer;
    SplineDriver _leaderMoveSeen;   // the leader whose current lane change we have already decided about
    int _leaderMoveLane = -1;
    PlayerVehicleController _pvc;
    public int PackLane => _packLane;
    public bool PackMoving => _packMoving;
    // Every racing brain by its SplineDriver, so a car can see where the others have committed to go.
    static readonly System.Collections.Generic.Dictionary<SplineDriver, AIRacingBehaviour> _byDriver =
        new System.Collections.Generic.Dictionary<SplineDriver, AIRacingBehaviour>();
    // Pack racing on for this car: a superspeedway, in lanes.
    bool Pack => TrackConditions.AiPackRacing && Lanes;

    // Payback state: the rival currently being lunged at (an AI spline OR the free-driven player),
    // the active-move timer, and per-rival cooldown gates keyed by driver name.
    float _paybackScanTimer;
    float _paybackTimer;
    SplineDriver _paybackRivalSpline;
    PlayerVehicleController _paybackRivalObstacle;
    readonly System.Collections.Generic.Dictionary<string, float> _paybackNextAllowed = new();
    DriverLabel _label;
    float _draftBondTimer;
    string _draftPartnerName;

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        if (_spline != null) _basePaceMultiplier = _spline.paceMultiplier;
        _stint = GetComponent<PracticeAIStint>();
        _pvc = GetComponent<PlayerVehicleController>();
        if (_spline != null) _byDriver[_spline] = this;
    }

    void OnDestroy()
    {
        if (_spline != null && _byDriver.TryGetValue(_spline, out var me) && me == this) _byDriver.Remove(_spline);
    }

    // Where another car is headed across the road: the lane it has committed to in a pack, else where it is.
    float HeadedLateral(SplineDriver other)
    {
        if (Pack && _byDriver.TryGetValue(other, out var b) && b != null && b._packMoving && b._packLane >= 0)
            return LaneLateral(b._packLane);
        return other.LateralOnTrack;
    }

    public void SetBasePace(float baseMul) { _basePaceMultiplier = baseMul; }

    void FixedUpdate()
    {
        if (_spline == null || _spline.TrackLength <= 0f) return;
        // Racing tactics stay off until the green flag. During PreGrid/Formation, FormationController
        // owns the AI's speed cap and line so the field forms up behind the safety car.
        if (!RaceStart.IsGreen) return;

        float dt = Time.fixedDeltaTime;

        // Race phase. A field that races lap 1 exactly the way it races the last lap reads as a machine, so
        // the drivers settle in over the opening laps - wider gaps, fewer lunges - and throw everything at
        // it over the closing ones. RaceDirector reports -1 wherever the race distance isn't known
        // (practice, qualifying, multiplayer); RaceCraft turns that into the neutral mid-race envelope
        // rather than an eternal opening lap. Everything downstream reads _phaseAggression, not the
        // driver's flat aggression stat.
        float raceProgress = phaseAwareRacecraft
            ? RaceCraft.NormaliseProgress(RaceDirector.Progress01)
            : RaceCraft.NeutralProgress;
        _phaseAggression = RaceCraft.PhaseAggression(aggression01, raceProgress);
        float followMargin = RaceCraft.PhaseFollowMargin(raceProgress);

        // The grid-row stagger (lateralOffset) is only a parked-field look. Once racing, ease it off so cars
        // settle onto their real racing line instead of holding a permanent ±2 m offset for the whole race.
        // Not while on the pit lane (box-lane parking uses it) and not in practice (the stint controller owns it).
        if (_stint == null && !_spline.IsOnPit && Mathf.Abs(_spline.lateralOffset) > 0.001f)
            _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, 0f, maxLateralSpeed * dt);

        // Rolling start: for launchWindowSeconds after green, ease the follow-distance cap in from 0→1 so the
        // field accelerates together off the line (like the released player) instead of the cap pinning each
        // follower to the car ahead and the green crawling back through the pack from the leader. 1 = full cap.
        float sinceGreen = RaceStart.SecondsSinceGreen;
        float launchCapBlend = (launchWindowSeconds > 0f && sinceGreen >= 0f && sinceGreen < launchWindowSeconds)
            ? Mathf.Clamp01(sinceGreen / launchWindowSeconds) : 1f;

        // Local yellow: a car stopped on the road up ahead (CautionWatch). Through its zone we lift well off the
        // pace, take no tow and pass nobody — except the stopped car itself, which still has to be driven round.
        var caution = CautionWatch.Instance;
        float incidentGap = 0f;
        Transform incidentCar = null;
        bool underYellow = respectYellows && !_spline.IsOnPit && caution != null
            && caution.YellowFor(transform, _spline.DistanceOnTrack, _spline.TrackLength,
                                 yellowZoneBeforeMetres, yellowZoneAfterMetres, out incidentGap, out incidentCar);
        UnderYellow = underYellow;

        // Rivalry: run down an active payback move; otherwise scan for a nearby hated rival on an interval.
        if (enablePayback && !_spline.IsOnPit)
        {
            if (_paybackTimer > 0f)
            {
                _paybackTimer -= dt;
                if (_paybackTimer <= 0f) { _paybackRivalSpline = null; _paybackRivalObstacle = null; }
            }
            else
            {
                _paybackScanTimer -= dt;
                if (_paybackScanTimer <= 0f)
                {
                    _paybackScanTimer = paybackScanInterval;
                    ScanForPayback();
                }
            }
        }

        float desiredTactical = 0f;
        float speedCap = float.MaxValue;
        float speedBoost = 0f;
        bool wantOvertake = false;
        float overtakeDir = 0f;

        // Detection range must cover the distance we need to brake from our CURRENT speed, otherwise a stopped
        // car is spotted too late to avoid — a fixed brakeScanRange is far too short at racing speed (that's
        // what let the field plough into stationary cars).
        float myMpsNow = _spline.CurrentMph * MphToMps;
        // How close this track's racing runs (TrackTuning.draftFollowScale): nose to tail on a superspeedway.
        float headway = followHeadwaySeconds * TrackConditions.AiFollowScale;
        float dynStopDist = minFollowDistance + myMpsNow * headway
                            + (myMpsNow * myMpsNow) / (2f * Mathf.Max(followDecelMps2, 1f)) + 20f;
        float scanDist = Mathf.Max(lookAheadRange, brakeScanRange, dynStopDist);

        if (RaceField.TryGetAhead(_spline, scanDist, out var ahead, out float aheadGap))
        {
            float aheadSpeed = ahead.CurrentMph;
            float mySpeed = _spline.CurrentMph;
            float aheadLat = ahead.LateralOnTrack;

            float closingRange = Mathf.Lerp(overtakeClosingRange * 0.7f, overtakeClosingRange * 1.4f, _phaseAggression);
            // Compare against what we COULD do (profile pace), not current speed — once we've matched the
            // leader's speed the current-speed delta is zero and a train forms that never breaks.
            float myPotential = Mathf.Max(mySpeed, _spline.DesiredMph);
            // Closing-rate widening: catching a much-slower car fast → begin the move from further back.
            float closingMph = Mathf.Max(0f, myPotential - aheadSpeed);
            float initiateRange = closingRange + closingMph * ttcRangePerMph;
            // Never shorter than the gap the follow cap below holds us at. It used to be: the cap parked a
            // quicker car ~40 m back (a 0.7 s headway at Watkins Glen speeds) while a pass only started inside
            // ~25 m, so it sat there all race — the pack sim counted zero passes in six laps.
            initiateRange = Mathf.Max(initiateRange, (minFollowDistance + myMpsNow * headway) * 1.2f + 6f);
            // Quicker than them means quicker than what THEY could do here too. Down a straight both cars want
            // far more than either can reach, so "my target beats their speed" was true for every follower on
            // every straight. Target against target is the fair comparison (the draft boost is part of ours, so a
            // tow is what makes the difference on a straight) — plus a car crawling along, spun or wrecked, which
            // is fair game whatever it could do.
            bool crawling = aheadSpeed < Mathf.Max(mySpeed, _spline.DesiredMph) * 0.7f;
            // Flat out, DesiredMph is far above anything reachable, and off a rolling start every car ahead read as
            // crawling against it - the whole pack tried to drive round itself. Crawling is against what we're doing.
            if (Pack) crawling = aheadSpeed < mySpeed * 0.7f;
            bool quicker = _spline.DesiredMph > ahead.DesiredMph + 1.5f || crawling;
            // Under yellow everyone ahead is lifting too, so "crawling" alone would pass the whole queue. Only
            // the car that is actually stopped, or nearly, gets driven round.
            if (underYellow) quicker = aheadSpeed < yellowPassBelowMph;
            // In a pack, passing is lane flow (PackLanes below); this is only for driving round a crawling car.
            if (aheadGap < initiateRange && quicker && _cooldownTimer <= 0f && (!Pack || crawling || underYellow))
            {
                bool stoppedCar = aheadSpeed < yellowPassBelowMph;
                // A stopped car is driven round on the wide side of the road — the side away from where it sits —
                // not on whichever side the next corner favours.
                overtakeDir = stoppedCar ? AroundSide(aheadLat) : ChooseOvertakeSide(aheadLat);
                // In lanes, a pass goes into the lane beside the car ahead - on the chosen side if it is clear,
                // the other if not - and not at all if neither is: tuck in behind and take the tow instead.
                if (!stoppedCar && Lanes && LaneGeometry(out _, out _, out _))
                {
                    int lane = ChoosePassLane(aheadLat, overtakeDir != 0f ? overtakeDir : (aheadLat >= _spline.LateralOnTrack ? -1f : 1f));
                    overtakeDir = lane < 0 ? 0f : Mathf.Sign(LaneLateral(lane) - _spline.LateralOnTrack + 1e-3f);
                    if (lane >= 0) { _passCar = ahead; _passLane = lane; _passTimer = 0f; }
                }
                wantOvertake = overtakeDir != 0f; // 0 = both sides blocked → don't dive into traffic, just tuck in
                if (wantOvertake) SetPassTarget(stoppedCar, aheadLat);
            }
        }

        // Rear-end avoidance: keep a speed-scaled gap and shed our closing speed EARLY so we never plough
        // into a slower / stopped car. reqGap = fixed buffer + time-headway + the distance to bleed our
        // closing speed at followDecelMps2. (The old fixed 6–14 m gap was far too short at racing speeds —
        // that's what caused the start-line pileups.) Aggressive drivers tuck in a touch tighter.
        // The cap keys off the nearest car IN OUR LATERAL CORRIDOR and fades with lateral separation —
        // capping off the nearest-ahead regardless of lateral froze the whole field behind any slow or
        // wrecked car (a committed overtake could never build the speed to actually drive past it).
        if (TryGetAheadInCorridor(scanDist, out var blocker, out float blockGap, out float overlap01))
        {
            float blockerMph = blocker.CurrentMph;
            float mySpeedMps = _spline.CurrentMph * MphToMps;
            float closingMps = Mathf.Max(0f, mySpeedMps - blockerMph * MphToMps);
            float brakeDist = (closingMps * closingMps) / (2f * Mathf.Max(followDecelMps2, 1f));
            float reqGap = (minFollowDistance + mySpeedMps * headway + brakeDist)
                           * Mathf.Lerp(1.15f, 0.85f, _phaseAggression) * followMargin;
            bool packFollow = Pack && !underYellow && blockerMph > _spline.CurrentMph * 0.75f;
            if (packFollow)
            {
                // Bump drafting: close up to a bumper's width behind the car ahead and sit there.
                float packCap = Mathf.Lerp(_spline.DesiredMph, PackFollowCap(blockerMph, blockGap), launchCapBlend);
                speedCap = Mathf.Min(speedCap, Mathf.Lerp(_spline.DesiredMph, packCap, overlap01));
            }
            else if (blockGap < reqGap)
            {
                // Ease from the blocker's speed (at reqGap) down to a touch under it (at hardFollowGap).
                float close01 = Mathf.InverseLerp(reqGap, hardFollowGap, blockGap);
                float followCap = Mathf.Lerp(blockerMph, Mathf.Max(0f, blockerMph - 6f), close01);
                // Rolling start: blend from our own race pace toward the follow cap over the launch window.
                followCap = Mathf.Lerp(_spline.DesiredMph, followCap, launchCapBlend);
                // Laterally clearing the blocker fades the cap back to race pace — drive past, don't shadow it.
                followCap = Mathf.Lerp(_spline.DesiredMph, followCap, overlap01);
                speedCap = Mathf.Min(speedCap, followCap);
            }
            if (blockGap < hardFollowGap && !packFollow)
                speedCap = Mathf.Min(speedCap, Mathf.Lerp(_spline.DesiredMph, blockerMph * 0.6f, overlap01));
        }

        // Human player car(s) — driven free with SplineDriver off, so absent from RaceField and invisible to the
        // checks above. Treat the nearest one ahead exactly like a slow/stopped car: shed closing speed early and,
        // if it's much slower, commit to a side to go around instead of rear-ending it.
        var obstacles = RaceObstacles.All;
        for (int oi = 0; oi < obstacles.Count; oi++)
        {
            var p = obstacles[oi];
            if (p == null || p.ObstacleTrack == null || p.ObstacleTrack != _spline.track) continue;
            float len = _spline.TrackLength;
            if (len <= 0f) continue;
            float pg = p.TrackDistance - _spline.DistanceOnTrack;
            if (pg <= 0f) pg += len;
            if (pg <= 0f || pg > scanDist) continue;

            float pSpeedMph = p.SpeedMph;
            // Same lateral-corridor fade as the AI-vs-AI cap: a player car we're already clear of
            // shouldn't pin our speed while we drive past it.
            float pOverlap = CorridorOverlap01(Mathf.Abs(p.TrackLateral - _spline.LateralOnTrack));
            float pClosingMps = Mathf.Max(0f, myMpsNow - pSpeedMph * MphToMps);
            float pBrakeDist = (pClosingMps * pClosingMps) / (2f * Mathf.Max(followDecelMps2, 1f));
            float pReqGap = (minFollowDistance + myMpsNow * headway + pBrakeDist)
                            * Mathf.Lerp(1.15f, 0.85f, _phaseAggression) * followMargin;
            bool pPack = Pack && !underYellow && pSpeedMph > _spline.CurrentMph * 0.75f;
            if (pPack)
            {
                if (pOverlap > 0f)
                {
                    float pCap = Mathf.Lerp(_spline.DesiredMph, PackFollowCap(pSpeedMph, pg), launchCapBlend);
                    speedCap = Mathf.Min(speedCap, Mathf.Lerp(_spline.DesiredMph, pCap, pOverlap));
                }
            }
            else if (pg < pReqGap && pOverlap > 0f)
            {
                float close01 = Mathf.InverseLerp(pReqGap, hardFollowGap, pg);
                float pCap = Mathf.Lerp(pSpeedMph, Mathf.Max(0f, pSpeedMph - 6f), close01);
                pCap = Mathf.Lerp(_spline.DesiredMph, pCap, launchCapBlend); // rolling-start ease-in
                pCap = Mathf.Lerp(_spline.DesiredMph, pCap, pOverlap);
                speedCap = Mathf.Min(speedCap, pCap);
            }
            if (pg < hardFollowGap && pOverlap > 0f && !pPack)
                speedCap = Mathf.Min(speedCap, Mathf.Lerp(_spline.DesiredMph, pSpeedMph * 0.6f, pOverlap));

            // Go around a much-slower / stopped player when a side is clear of other cars.
            float myPotential = Mathf.Max(_spline.CurrentMph, _spline.DesiredMph);
            if (!wantOvertake && pSpeedMph < myPotential - 2f && _cooldownTimer <= 0f
                && (!underYellow || pSpeedMph < yellowPassBelowMph))
            {
                bool stoppedCar = pSpeedMph < yellowPassBelowMph;
                float side = stoppedCar ? AroundSide(p.TrackLateral)
                                        : (p.TrackLateral >= _spline.LateralOnTrack ? -1f : 1f);
                if (side != 0f && !SideOccupied(side)) { overtakeDir = side; wantOvertake = true; }
                else if (!stoppedCar && !SideOccupied(-side)) { overtakeDir = -side; wantOvertake = true; }
                if (wantOvertake) SetPassTarget(stoppedCar, p.TrackLateral);
            }
        }

        // Slipstream: shared DraftAero geometry — the exact field the physics reads. The boost raises the brain's
        // TARGET speed; the dynamic model separately gains real drag reduction (raised ceiling + accel), so the
        // ask and the capability stay matched. Covers AI and human tow sources alike, and ticks the draft bond
        // with whoever is punching the hole in the air.
        var vinfo = _spline.vehicleInfo;
        if (vinfo != null && !_spline.IsOnPit)
        {
            DraftAero.Compute(_spline.track, _spline.TrackLength, _spline.DistanceOnTrack, _spline.LateralOnTrack,
                _spline.CurrentMph, gameObject, vinfo, out float towFactor, out var towSource, out _);
            if (towFactor > 0f)
            {
                speedBoost = Mathf.Max(speedBoost, vinfo.draftingMaxBonus * towFactor * TrackConditions.DraftScale);
                if (towSource != null) AccumulateDraftBond(DriverRelationships.NameOf(towSource), dt);
            }
        }

        // Under yellow we know exactly where the stopped car is, so we don't wait to see it. The avoidance above
        // only ever looks at the nearest car ahead, and with another car between us and the wreck it was only
        // spotted when that car swerved out of the way — at 80-100 mph, 15 m short of it: the pack sim had cars
        // driving straight through it. So every car in the zone lines up early on the wide side of it, and if it
        // still isn't clear of it in time, brakes to a stop short of it.
        float incidentStopCap = float.MaxValue;
        if (underYellow && incidentCar != null && incidentGap > 0f && incidentGap <= stoppedLineUpMetres
            && TryIncidentLateral(incidentCar, out float incidentLat))
        {
            float side = incidentLat < 0f ? 1f : -1f;
            wantOvertake = true;
            overtakeDir = side;
            SetPassTarget(true, incidentLat);

            float clear = Mathf.Abs(_spline.LateralOnTrack - incidentLat);
            if (clear < stoppedPassClearance - 0.8f)
            {
                // Not out of its way yet: never arrive faster than we could stop short of it — but always keep a
                // creep, or a car stopped behind it could never edge out round it (a car only moves sideways while
                // it's rolling). Holding 0 inside the last 6 m is what turned one wreck into a stopped queue.
                float room = Mathf.Max(0f, incidentGap - 6f);
                incidentStopCap = Mathf.Max(room > 0f ? 8f : stoppedCreepMph,
                                            Mathf.Sqrt(2f * followDecelMps2 * 0.6f * room) / MphToMps);
            }
        }

        // A pass already under way when the yellow comes out is abandoned: tuck back in behind. Not the way round
        // the stopped car itself — once alongside it, it is no longer "ahead", and dropping the commitment there
        // would swing us straight back into it.
        if (underYellow && !wantOvertake && !_passAroundStopped) _commitTimer = 0f;

        // A pass in lanes is held for as long as the pass takes - until the car is behind us by a length, or has
        // pulled away, or it has gone on too long - rather than for a fixed second and a half that could run out
        // with the two cars door to door.
        bool lanePass = false;
        if (_passCar != null && _passLane >= 0 && !underYellow)
        {
            _passTimer += dt;
            float g = LongitudinalGap(_spline, _passCar);   // + = they are still ahead
            bool done = g < -(alongsideLength + passClearMargin) || g > passAbandonGap || _passTimer > passMaxSeconds
                        || !_passCar.isActiveAndEnabled || _passCar.IsOnPit;
            if (done) { _passCar = null; _passLane = -1; _commitTimer = 0f; }   // no fixed-offset tail after it
            else lanePass = true;
        }
        else { _passCar = null; _passLane = -1; }

        // Commitment: once we pick a passing side, hold it. Prevents weave.
        if (lanePass)
        {
            wantOvertake = true;
            overtakeDir = Mathf.Sign(LaneLateral(_passLane) - _spline.LateralOnTrack + 1e-3f);
            _commitTimer = commitHoldSeconds;
            _commitDir = overtakeDir;
        }
        else if (wantOvertake)
        {
            _commitTimer = commitHoldSeconds;
            _commitDir = overtakeDir;
        }
        else if (_commitTimer > 0f)
        {
            _commitTimer -= dt;
            if (_commitTimer > 0f)
            {
                wantOvertake = true;
                overtakeDir = _commitDir;
            }
        }

        if (!wantOvertake) _passAroundStopped = false;
        if (wantOvertake && lanePass && !_passAroundStopped)
        {
            // Into the passing lane, wherever it is across the road here.
            desiredTactical = LaneLateral(_passLane) - _spline.UntacticalLateral;
        }
        else if (wantOvertake)
        {
            desiredTactical = overtakeDir * overtakeLineOffset;
            // Round a stopped car, a fixed step off our own line isn't enough: our line often runs along the same
            // edge it has stopped on, and 3 m off it left barely a car's width between the two — the pack sim had
            // the field scraping past it. Aim for a lateral clear of the car itself.
            if (_passAroundStopped)
            {
                float needed = _passTargetLat + overtakeDir * stoppedPassClearance - _spline.UntacticalLateral;
                if (needed * overtakeDir > overtakeLineOffset) desiredTactical = needed;
            }
        }

        // Pack racing: the lane is everything. Which lane to be in is decided by how the lanes are flowing (PackLanes);
        // once a car moves it goes all the way - nobody backs out of a lane change in a pack.
        if (Pack && !wantOvertake && !underYellow && !_spline.IsOnPit && PackLanes(dt, out float packLat))
            desiredTactical = packLat - _spline.UntacticalLateral;
        else if (Pack) { _packLane = -1; _packMoving = false; }

        // Holding a lane. A car with another alongside stays in the lane it is in - it does not drift back to the
        // ideal line across the other car - and a car only drops back to the ideal line once that lane is clear.
        if (!Pack && !wantOvertake && Lanes && !_spline.IsOnPit && LaneGeometry(out _, out _, out _))
        {
            float here = _spline.LateralOnTrack;
            float ideal = _spline.UntacticalLateral;
            bool alongside = AnyAlongside();
            if (alongside || (Mathf.Abs(ideal - here) > laneSpacing * 0.5f && !LaneClear(ideal)))
                desiredTactical = LaneLateral(NearestLane(here)) - ideal;
        }

        // Defending: if a faster pursuer is close behind during the approach to a turn, shift to the inside.
        if (!Pack && (_spline.CurrentPhase == SplineDriver.CornerPhase.Approach || _spline.CurrentPhase == SplineDriver.CornerPhase.Entry))
        {
            if (RaceField.TryGetBehind(_spline, defendDetectRange, out var pursuer, out float behindGap))
            {
                float behindSpeed = pursuer.CurrentMph;
                if (behindSpeed > _spline.CurrentMph + 1f && _cooldownTimer <= 0f)
                {
                    int turnSign = _spline.NextTurnSign(cornerScanDistance);
                    if (turnSign != 0)
                    {
                        float insideDir = -turnSign; // inside of turn = opposite of outside
                        float strength = Mathf.Clamp01((defendDetectRange - behindGap) / defendDetectRange);
                        float block = insideDir * defendLineOffset * strength;
                        // In lanes, only a block into a lane that's free - never across a car alongside.
                        if (!Lanes || LaneClear(_spline.LateralOnTrack + block)) desiredTactical += block;
                    }
                }
            }
        }

        // Blue flags. A car a lap down that races the leaders is the single most immersion-breaking thing
        // an AI field does, so lapped traffic gets out of the way instead: ease off the line the lapper is
        // already using and lift just enough that the pass actually completes. This outranks whatever
        // overtake or defence we were about to make - you don't defend against a car that isn't racing you
        // - while stuck recovery and a payback lunge below still override it, and the side-by-side
        // repulsion beneath still applies, so yielding never steers into somebody.
        float blueFlagLiftFactor = 1f;
        if (respectBlueFlags && !_spline.IsOnPit
            && TryGetLapperBehind(out float lapperLat, out float lapperGap))
        {
            float yieldStrength = RaceCraft.YieldStrength01(lapperGap, blueFlagRange);
            desiredTactical = RaceCraft.YieldDirection(_spline.LateralOnTrack, lapperLat)
                              * blueFlagOffset * yieldStrength;
            blueFlagLiftFactor = RaceCraft.YieldSpeedFactor(yieldStrength, blueFlagLift);
            _commitTimer = 0f;   // don't let an overtake commitment resume the moment we're past
        }

        // Side-by-side repulsion + contact response.
        float repulse = 0f;
        float contactScrub = 0f;
        // How much this track's racing shies away from a car alongside (TrackTuning.sideAwareness): fully on a road
        // course, hardly at all in a superspeedway pack, where three wide is how it's done and holding your lane is
        // the courtesy. In a pack contact is leaned on too, not backed out of.
        float sideShy = TrackConditions.AiSideAwareness;
        float contactShy = Pack ? sideShy : 1f;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline) continue;
            if (PaybackActive && other == _paybackRivalSpline) continue; // no self-preservation vs the target
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float longGap = LongitudinalGap(_spline, other);
            // Only a car actually alongside. This used to take anything within sidewaysRange (12 m) ahead or
            // behind, so a car drafting nose to tail in the same lane was "in contact" with the one in front:
            // pushed sideways and scrubbed of speed for being in the tow. The field could never pack up.
            // Where the AI doesn't race in lanes (road courses) the wider window stays: through an out-in-out
            // corner it is what keeps a car off the one diagonally ahead of it.
            // In a pack, alongside means really overlapping: a car bump drafting 5 m behind is nose to tail, not door to
            // door - counted as side contact it was scrubbed, and scrubbed the car ahead, from 190 to 150 mph in half a lap.
            float window = Pack ? alongsideLength * 0.75f : Lanes ? Mathf.Min(sidewaysRange, alongsideLength) : sidewaysRange;
            if (Mathf.Abs(longGap) > window) continue;
            float latGap = _spline.LateralOnTrack - other.LateralOnTrack;
            float absLat = Mathf.Abs(latGap);
            float dir = latGap >= 0f ? 1f : -1f;
            if (absLat < contactLateralWidth)
            {
                // Contact: stronger push + speed scrub.
                float overlap = (contactLateralWidth - absLat) / contactLateralWidth;
                repulse += dir * (sidewaysMaxPush + contactPush) * overlap * contactShy;
                contactScrub += contactSpeedScrub * overlap * contactShy;
            }
            else
            {
                float threshold = sidewaysWidth * 0.6f;
                if (absLat >= threshold) continue;
                float push = (threshold - absLat) / threshold;
                repulse += dir * push * sidewaysMaxPush * sideShy;
            }
        }
        desiredTactical += Mathf.Clamp(repulse, -(sidewaysMaxPush + contactPush), sidewaysMaxPush + contactPush);

        if (contactScrub > 0f)
        {
            float scrubMph = contactScrub * dt;
            // Floor stops the ratchet: without it two cars in sustained contact cap each other to zero.
            speedCap = Mathf.Min(speedCap, Mathf.Max(_spline.CurrentMph - scrubMph, contactScrubFloorMph));
        }

        // Stuck recovery: stalled well below profile pace → ignore follow-caps and commit around the blockage.
        bool stalled = !_spline.usePitLane && _spline.CurrentMph < stallSpeedMph && _spline.DesiredMph > stallSpeedMph + 10f;
        _stallTimer = stalled ? _stallTimer + dt : 0f;
        if (_stallTimer >= stallTriggerSeconds && _recoveryTimer <= 0f)
        {
            _recoveryTimer = stallRecoverySeconds;
            // Pick the side away from whoever is blocking; fall back to drifting toward centerline.
            if (RaceField.TryGetAhead(_spline, minFollowDistance * 2f, out var stallBlocker, out _))
            {
                // Held up by a car that has stopped: out round it on the wide side, clear of it (see below).
                bool stoppedAhead = stallBlocker.CurrentMph < yellowPassBelowMph;
                _recoveryDir = stoppedAhead
                    ? (stallBlocker.LateralOnTrack < 0f ? 1f : -1f)
                    : (stallBlocker.LateralOnTrack >= _spline.LateralOnTrack ? -1f : 1f);
                SetPassTarget(stoppedAhead, stallBlocker.LateralOnTrack);
            }
            else
                _recoveryDir = _spline.LateralOnTrack >= 0f ? -1f : 1f;
            _stallTimer = 0f;
        }
        if (_recoveryTimer > 0f)
        {
            _recoveryTimer -= dt;
            speedCap = float.MaxValue;
            desiredTactical = _recoveryDir * overtakeLineOffset;
            if (_passAroundStopped)
            {
                float needed = _passTargetLat + _recoveryDir * stoppedPassClearance - _spline.UntacticalLateral;
                if (needed * _recoveryDir > overtakeLineOffset) desiredTactical = needed;
            }
            _commitTimer = Mathf.Max(_commitTimer, commitHoldSeconds);
            _commitDir = _recoveryDir;
        }

        // Payback: steer INTO the rival and drop self-preservation for the duration. Repulsion against
        // the target is skipped above, follow caps are discarded, and a small boost is requested when the
        // rival is ahead so the hit lands. VehicleCollision does the actual damage; the resulting contact
        // sours the relationship further via DriverRelationships.ReportContact.
        if (PaybackActive && _recoveryTimer <= 0f)
        {
            if (TryGetRivalTrackPose(out float rivalLat, out float rivalGap) && Mathf.Abs(rivalGap) <= paybackRange)
            {
                desiredTactical = _smoothedTactical + (rivalLat - _spline.LateralOnTrack);
                speedCap = float.MaxValue;
                if (rivalGap > 0.5f) speedBoost = Mathf.Max(speedBoost, paybackSpeedBoost);
                _commitTimer = 0f; // a lunge overrides any overtake commitment
            }
            else
            {
                _paybackTimer = 0f; // rival gone (pitted, wrecked clear, out of range) — stand down
                _paybackRivalSpline = null;
                _paybackRivalObstacle = null;
            }
        }

        // Converge on the desired offset like a driver turning the wheel: accelerate into the move, carry no more
        // than maxLateralSpeed, and slow so as to arrive on the target rather than at full rate. Dead-zone
        // prevents twitching near target. Driving round a stopped car is an avoidance move - quicker throughout.
        float diff = desiredTactical - _smoothedTactical;
        if (Mathf.Abs(diff) < tacticalDeadzone) diff = 0f;
        float lateralSpeed = _passAroundStopped ? Mathf.Max(maxLateralSpeed, stoppedPassLateralSpeed) : maxLateralSpeed;
        float lateralAccel = Mathf.Max(0.1f, _passAroundStopped ? maxLateralAccel * 3f : maxLateralAccel);
        if (Pack && _packMoving)
        {
            lateralSpeed = Mathf.Max(lateralSpeed, packLateralSpeed);
            lateralAccel = Mathf.Max(lateralAccel, packLateralAccel);
        }
        float wantVelocity = Mathf.Sign(diff) * Mathf.Min(lateralSpeed, Mathf.Sqrt(2f * lateralAccel * Mathf.Abs(diff)));
        _tacticalVelocity = Mathf.MoveTowards(_tacticalVelocity, wantVelocity, lateralAccel * dt);
        float move = _tacticalVelocity * dt;
        // Never step past the target: arrive and stop.
        if (diff != 0f && Mathf.Sign(move) == Mathf.Sign(diff) && Mathf.Abs(move) > Mathf.Abs(diff))
        {
            move = diff;
            _tacticalVelocity = 0f;
        }
        _smoothedTactical += move;

        // Manoeuvre cooldown, started once as we settle back to neutral after a move. It used to be re-armed on
        // every neutral frame, so it sat at manoeuvreCooldown forever and no overtake could ever begin — the
        // whole field ran nose to tail and nobody passed.
        bool settled = Mathf.Abs(_smoothedTactical) < tacticalDeadzone && Mathf.Abs(desiredTactical) < tacticalDeadzone;
        if (settled && _wasManoeuvring) _cooldownTimer = manoeuvreCooldown;
        _wasManoeuvring = !settled;
        if (_cooldownTimer > 0f) _cooldownTimer -= dt;

        // Tyre grip decides two things - the pace the car can carry and how likely its driver is to drop
        // it - so it is looked up once and used for both.
        float grip = CurrentGrip();

        // Mistake roll. No longer a flat per-second dice throw: a driver with a rival filling the mirrors on
        // worn tyres is far likelier to make an error than the same driver alone on fresh rubber, and even a
        // metronome cracks eventually. The weighting lives in RaceCraft so it can be unit-tested in EditMode.
        // An active mistake adds wobble + a pace dip, as before.
        if (_mistakeTimer > 0f)
        {
            _mistakeTimer -= dt;
            // In a pack a slip is a twitch, not a lurch: three wide, a metre's wander is the wall or the car beside.
            _smoothedTactical += _mistakeWobbleDir * mistakeWobble * (Pack ? 0.25f : 1f) * dt;
        }
        else if (mistakeProbabilityPerSecond > 0f)
        {
            float perSecond = RaceCraft.MistakeChancePerSecond(
                mistakeProbabilityPerSecond, consistency01, PressureFromBehind(), 1f - grip);
            if (Random.value < perSecond * dt)
            {
                _mistakeTimer = mistakeDurationSeconds;
                _mistakeWobbleDir = Random.value < 0.5f ? -1f : 1f;
            }
        }

        float effectivePace = _basePaceMultiplier * TrackConditions.AiPaceMultiplier * grip * blueFlagLiftFactor;
        if (_mistakeTimer > 0f) effectivePace *= mistakePaceFactor;
        _spline.paceMultiplier = effectivePace;

        // Yellow zone: a clear lift off the pace we'd carry here, applied last so nothing above (a recovery, a
        // payback lunge, a tow) can race through the scene. DesiredMph is the profile speed before any cap, so
        // this never ratchets itself down; last step's tow is taken back out of it.
        if (underYellow)
        {
            float cleanPace = Mathf.Max(0f, _spline.DesiredMph - _spline.aiSpeedBoostMph);
            speedCap = Mathf.Min(speedCap, RaceCraft.YellowSpeedCap(cleanPace, yellowPaceFactor, yellowFloorMph));
            speedCap = Mathf.Min(speedCap, incidentStopCap);
            speedBoost = 0f;
        }

        _spline.tacticalLateralOffset = _smoothedTactical;
        _spline.aiMaxSpeedMph = speedCap;
        _spline.aiSpeedBoostMph = speedBoost;
    }

    // ---- Pack racing ----

    // The speed (mph) to hold behind a car in our line: close on it no faster than we could shed at packCloseDecel
    // over the room left to a bumper's width behind it, then sit there - a touch under its speed if we're inside it.
    float PackFollowCap(float aheadMph, float gap)
    {
        float room = gap - packFollowGap;
        if (room > 0f) return aheadMph + Mathf.Sqrt(2f * packCloseDecel * room) / MphToMps;
        // Inside a bumper's width: ease back a little, never far under it - asking for 5 mph under the car ahead
        // turned into a brake stab the next car had to match, and so on down the line. Touching is bump drafting.
        return Mathf.Max(0f, aheadMph + Mathf.Max(room * 0.6f, -2.5f));
    }

    // Which lane to race in, as a lateral. A car stays in its lane unless:
    //   * the car ahead of it in the line moves to another lane - most follow it (that is how lines stay together,
    //     and how a run takes its pusher with it);
    //   * the lane beside is moving faster and there's a hole to drop into;
    //   * it has a run on the car ahead (or, on the bumper in the tow, the nerve) and the lane beside is empty -
    //     it pulls out to make a new lane;
    //   * its own lane is empty ahead and a line runs beside it: tuck in and take the draft.
    // Once it decides, it goes - the move is held until the car is in the lane.
    bool PackLanes(float dt, out float lateral)
    {
        lateral = _spline.LateralOnTrack;
        if (!LaneGeometry(out _, out _, out int count)) return false;
        float here = _spline.LateralOnTrack;
        int cur = NearestLane(here);
        if (_packLane < 0 || _packLane >= count) { _packLane = cur; _packMoving = false; }

        if (_packMoving)
        {
            if (Mathf.Abs(LaneLateral(_packLane) - here) < 0.35f) _packMoving = false;
            lateral = LaneLateral(_packLane);
            return true;
        }
        _packLane = cur;   // settled: whatever lane we are in is our lane

        float myMph = _spline.CurrentMph;
        var lead = AheadInLane(cur, packEmptyLaneMetres, out float leadGap);
        float leadMph = lead != null ? lead.CurrentMph : float.MaxValue;

        // 1. Our leader is changing lane: decide at once whether to go with it - every step, not on the decision
        // clock, because a pusher that waited the better part of half a second was still on the bumper as the car
        // pulled across its nose. One roll per lane change.
        if (lead != null && leadGap < packFollowGap * 2.2f
            && _byDriver.TryGetValue(lead, out var lb) && lb != null
            && lb._packMoving && lb._packLane >= 0 && lb._packLane < count && lb._packLane != cur
            && (lead != _leaderMoveSeen || lb._packLane != _leaderMoveLane))
        {
            _leaderMoveSeen = lead;
            _leaderMoveLane = lb._packLane;
            if (Random.value < Mathf.Lerp(packFollowLeaderChance, packFollowLeaderChance * 0.6f, _phaseAggression)
                && PackLaneClear(lb._packLane))
                return MoveTo(lb._packLane, out lateral);
        }

        _packDecisionTimer -= dt;
        if (_packDecisionTimer > 0f) { lateral = LaneLateral(_packLane); return true; }
        _packDecisionTimer = packDecisionInterval * Random.Range(0.75f, 1.25f);

        // 2. A quicker lane beside, 3. a new lane with a run, or 4. tuck into a line.
        int best = -1;
        float bestScore = 0f;
        float tow = _pvc != null ? _pvc.TowFactor : 0f;
        bool haveRun = lead != null && leadGap < 30f && myMph - leadMph > packRunMph;
        bool nerve = lead != null && leadGap < packFollowGap * 1.6f && tow > 0.5f
                     && Random.value < packPullOutChance * _phaseAggression;
        for (int dir = -1; dir <= 1; dir += 2)
        {
            int lane = cur + dir;
            if (lane < 0 || lane >= count) continue;
            var adj = AheadInLane(lane, packEmptyLaneMetres, out float adjGap);
            float score;
            if (adj != null)
            {
                if (lead == null) score = adjGap < 35f ? 1f : 0f;                                  // 4: join the line beside
                else score = adj.CurrentMph - leadMph - packLaneSwitchMph;                          // 2: the faster lane
                if (lead != null && adjGap < leadGap - 2f) score -= 0.5f;                           //    not to sit further back
            }
            else score = (haveRun || nerve) ? 0.5f + Mathf.Max(0f, myMph - leadMph) * 0.1f : 0f;    // 3: make a lane
            if (score > bestScore && PackLaneClear(lane)) { best = lane; bestScore = score; }
        }
        if (best >= 0) return MoveTo(best, out lateral);

        lateral = LaneLateral(_packLane);
        return true;
    }

    bool MoveTo(int lane, out float lateral)
    {
        _packLane = lane;
        _packMoving = true;
        lateral = LaneLateral(lane);
        return true;
    }

    // The nearest car ahead of us in this lane, within range.
    SplineDriver AheadInLane(int lane, float range, out float gap)
    {
        gap = float.MaxValue;
        SplineDriver best = null;
        float laneLat = LaneLateral(lane);
        float half = LaneSpacing * 0.5f;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float lg = LongitudinalGap(_spline, other);
            if (lg <= 0.5f || lg > range || lg >= gap) continue;
            if (Mathf.Abs(other.LateralOnTrack - laneLat) > half) continue;
            gap = lg;
            best = other;
        }
        return best;
    }

    // Room to drop into a lane here: nobody in it from a length behind (more if they're closing) to a length ahead
    // (more if we're closing). Tighter than LaneClear - a pack fills every hole - but it never cuts across a car.
    bool PackLaneClear(int lane)
    {
        float laneLat = LaneLateral(lane);
        float half = LaneSpacing * 0.8f;
        // Pulling out of a tow costs the tow: judge the gap behind on the speed we'll have once out in the air, or
        // we drop in front of a car that's still in a tow and quicker than we're about to be.
        float tow = _pvc != null ? _pvc.TowFactor : 0f;
        var vi = _spline.vehicleInfo;
        float towLoss = vi != null ? vi.draftingTopSpeedGain * TrackConditions.DraftScale * tow : 0f;
        float myMps = _spline.CurrentMph * MphToMps;
        float myOutMps = myMps * (1f - towLoss);
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            // In the lane, or committed to moving into it (two cars diving for one hole from either side).
            if (Mathf.Abs(other.LateralOnTrack - laneLat) >= half && Mathf.Abs(HeadedLateral(other) - laneLat) >= half) continue;
            float otherMps = other.CurrentMph * MphToMps;
            float lg = LongitudinalGap(_spline, other);
            float closingBehind = otherMps - myOutMps;   // + = the car behind would close on us
            float closingAhead = myMps - otherMps;      // + = we would close on the car ahead
            if (lg > -(6f + Mathf.Max(0f, closingBehind) * 2f) && lg < 6f + Mathf.Max(0f, closingAhead) * 2f) return false;
        }
        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack == null || p.ObstacleTrack != _spline.track) continue;
            if (Mathf.Abs(p.TrackLateral - laneLat) >= half) continue;
            float closing = p.SpeedMph * MphToMps - myMps;
            float lg = SignedGapTo(p.TrackDistance);
            if (lg > -(6f + Mathf.Max(0f, closing) * 1.2f) && lg < 6f + Mathf.Max(0f, -closing) * 1.2f) return false;
        }
        return true;
    }

    // Which side to drive round a stopped car: the wide side of the road, away from where it sits. 0 when a car
    // is already there — wait behind rather than squeeze through the narrow side.
    float AroundSide(float obstacleLat)
    {
        float pick = obstacleLat < 0f ? 1f : -1f;
        return SideOccupied(pick) ? 0f : pick;
    }

    // Where a stopped car sits across the road: an AI car's brain, or the human car's own projection.
    bool TryIncidentLateral(Transform car, out float lateral)
    {
        lateral = 0f;
        var sd = car.GetComponent<SplineDriver>();
        if (sd != null && sd.enabled) { lateral = sd.LateralOnTrack; return true; }
        var pvc = car.GetComponent<PlayerVehicleController>();
        if (pvc != null && pvc.ObstacleTrack == _spline.track) { lateral = pvc.TrackLateral; return true; }
        return false;
    }

    // Remember what the pass is round. A pass round a moving car keeps the usual fixed offset; round a stopped
    // one the offset is sized off the car itself (see where desiredTactical is set).
    void SetPassTarget(bool stoppedCar, float targetLat)
    {
        _passAroundStopped = stoppedCar;
        if (stoppedCar) _passTargetLat = targetLat;
    }

    // Pick which side to pass on: outside of an upcoming turn (safer arc), else the roomier side away from the
    // car ahead. Never commit to a side another car already occupies — try the other, and bail if both are blocked.
    float ChooseOvertakeSide(float aheadLat)
    {
        int turnSign = _spline.NextTurnSign(cornerScanDistance);
        float pick;
        if (turnSign != 0)
        {
            float outsideDir = turnSign;   // outside of the turn — more room, better exit
            float insideDir = -outsideDir; // the dive
            pick = _phaseAggression > 0.75f ? insideDir : outsideDir;
        }
        else
        {
            float roomBias = 0f;
            if (_spline.GetLateralRoom(out float leftRoom, out float rightRoom))
                roomBias = rightRoom - leftRoom; // >0 → more room on the right (+lateral)
            float awayFromAhead = aheadLat >= 0f ? -1f : 1f; // car ahead sits right → go left
            pick = (roomBias * 0.4f + awayFromAhead) >= 0f ? 1f : -1f;
        }

        if (SideOccupied(pick)) pick = -pick;     // someone's there — try the other side
        if (SideOccupied(pick)) return 0f;        // boxed in both sides — abort the pass
        return pick;
    }

    // ---- Lanes ----

    // The lanes across the road here: from the AI's left bound to its right, evenly spaced and at least laneSpacing
    // apart. Daytona's 11 m (bounds ±3.9 m) is four lanes; a narrow road course is two or three.
    float LaneSpacing => TrackConditions.AiLaneSpacing > 0f ? TrackConditions.AiLaneSpacing : laneSpacing;

    bool LaneGeometry(out float lo, out float hi, out int count)
    {
        count = 0;
        float spacing = LaneSpacing;
        if (!_spline.GetLateralBounds(out lo, out hi) || hi - lo < spacing) return false;
        // A superspeedway pack races in fixed grooves, the same distance off the centreline all the way round.
        // Fitted between the bounds instead, the lanes moved with the road's shape through the tri-oval and every
        // car in them was carried across the track without meaning to go anywhere (Talladega: five lanes, pile-ups
        // against the outside one).
        if (Pack && TrackConditions.AiPackLanes >= 2)
        {
            count = TrackConditions.AiPackLanes;
            hi = (count - 1) * 0.5f * spacing;
            lo = -hi;
            return true;
        }
        count = Mathf.FloorToInt((hi - lo) / Mathf.Max(0.5f, spacing)) + 1;
        return count >= 2;
    }

    float LaneLateral(int lane)
    {
        if (!LaneGeometry(out float lo, out float hi, out int count)) return _spline.LateralOnTrack;
        lane = Mathf.Clamp(lane, 0, count - 1);
        float lat = Mathf.Lerp(lo, hi, lane / (float)(count - 1));
        // A fixed groove still has to be on the road here.
        if (Pack && TrackConditions.AiPackLanes >= 2 && _spline.GetLateralBounds(out float bLo, out float bHi))
            lat = Mathf.Clamp(lat, bLo, bHi);
        return lat;
    }

    int NearestLane(float lateral)
    {
        if (!LaneGeometry(out float lo, out float hi, out int count)) return 0;
        return Mathf.Clamp(Mathf.RoundToInt((lateral - lo) / (hi - lo) * (count - 1)), 0, count - 1);
    }

    // Is a lane free for us to drive into: nobody in it from laneClearBehind behind us to laneClearAhead (plus a
    // second of closing speed) ahead? The human car counts too.
    bool LaneClear(float laneLat)
    {
        float closing = Mathf.Max(0f, _spline.CurrentMph) * MphToMps * 0.1f;
        float halfLane = laneSpacing * 0.75f;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float lg = LongitudinalGap(_spline, other);
            if (lg < -laneClearBehind || lg > laneClearAhead + closing) continue;
            if (Mathf.Abs(other.LateralOnTrack - laneLat) < halfLane) return false;
        }
        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack == null || p.ObstacleTrack != _spline.track) continue;
            float lg = p.TrackDistance - _spline.DistanceOnTrack;
            float len = _spline.TrackLength;
            if (lg > len * 0.5f) lg -= len; else if (lg < -len * 0.5f) lg += len;
            if (lg < -laneClearBehind || lg > laneClearAhead + closing) continue;
            if (Mathf.Abs(p.TrackLateral - laneLat) < halfLane) return false;
        }
        return true;
    }

    // Anyone overlapping us alongside, either side?
    bool AnyAlongside()
    {
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            if (Mathf.Abs(LongitudinalGap(_spline, other)) < alongsideLength
                && Mathf.Abs(other.LateralOnTrack - _spline.LateralOnTrack) < laneSpacing * 2f)
                return true;
        }
        return false;
    }

    // The lane to pass in: next to the car ahead on the preferred side, else the other side; -1 if neither is
    // clear (or exists). The lane we are already in counts only if it isn't the car ahead's.
    int ChoosePassLane(float aheadLat, float preferDir)
    {
        if (!LaneGeometry(out _, out _, out int count)) return -1;
        int theirs = NearestLane(aheadLat);
        float dir = preferDir >= 0f ? 1f : -1f;
        for (int attempt = 0; attempt < 2; attempt++, dir = -dir)
        {
            int lane = theirs + (int)dir;
            if (lane < 0 || lane >= count) continue;
            if (LaneClear(LaneLateral(lane))) return lane;
        }
        return -1;
    }

    // Is another car alongside or just ahead on the given side, so passing there would clip it?
    bool SideOccupied(float dir)
    {
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float lg = LongitudinalGap(_spline, other); // + = ahead of me
            if (lg < -3f || lg > overtakeClosingRange) continue; // only cars alongside / just ahead
            float latGap = other.LateralOnTrack - _spline.LateralOnTrack;
            if (Mathf.Sign(latGap) == Mathf.Sign(dir) && Mathf.Abs(latGap) < sidewaysWidth) return true;
        }
        return false;
    }

    static float LongitudinalGap(SplineDriver self, SplineDriver other)
    {
        float trackLen = self.TrackLength;
        float g = other.DistanceOnTrack - self.DistanceOnTrack;
        if (g > trackLen * 0.5f) g -= trackLen;
        else if (g < -trackLen * 0.5f) g += trackLen;
        return g;
    }

    // 1 = the other car sits square in our lateral path, 0 = fully clear, fading in between.
    float CorridorOverlap01(float lateralSeparation)
    {
        float overlap = Lanes ? corridorOverlapWidth : Mathf.Max(corridorOverlapWidth, 2f);
        float clear = Lanes ? corridorClearWidth : corridorClearWidthNoLanes;
        return 1f - Mathf.Clamp01((lateralSeparation - overlap) / Mathf.Max(clear - overlap, 0.1f));
    }

    // Nearest car ahead that overlaps MY lateral corridor — the one we'd actually hit. RaceField.TryGetAhead
    // returns the nearest-ahead regardless of lateral, which is right for "who do I try to pass" but wrong
    // for "who do I brake for": a wreck sitting offline would cap the whole field's speed until it stopped.
    bool TryGetAheadInCorridor(float scanDist, out SplineDriver blocker, out float gap, out float overlap01)
    {
        blocker = null; gap = 0f; overlap01 = 0f;
        float best = float.MaxValue;
        float myLat = _spline.LateralOnTrack;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float lg = LongitudinalGap(_spline, other);
            if (lg <= 0f || lg > scanDist || lg >= best) continue;
            float ov = CorridorOverlap01(Mathf.Abs(other.LateralOnTrack - myLat));
            // In a pack, a car committed to moving into our lane is followed from the moment it commits - it will be
            // there before we could shed the speed if we waited for it to arrive.
            // And while we are moving lanes ourselves, the car ahead in the lane we're moving into is followed already.
            if (Pack)
            {
                ov = Mathf.Max(ov, CorridorOverlap01(Mathf.Abs(HeadedLateral(other) - myLat)));
                if (_packMoving && _packLane >= 0)
                    ov = Mathf.Max(ov, CorridorOverlap01(Mathf.Abs(other.LateralOnTrack - LaneLateral(_packLane))));
            }
            if (ov <= 0f) continue;
            best = lg;
            blocker = other;
            gap = lg;
            overlap01 = ov;
        }
        return blocker != null;
    }

    // ---- Race phase, pressure and blue flags ----

    // Overall tyre grip as a multiplier on pace. TireModel is the full thermal/wear model, TireState the
    // older simple one; a car carrying neither is on fresh rubber.
    float CurrentGrip()
    {
        var tireModel = GetComponent<TireModel>();
        if (tireModel != null) return tireModel.OverallGrip;
        var tire = GetComponent<TireState>();
        return tire != null ? tire.GripMultiplier : 1f;
    }

    // How hard somebody is leaning on us, 0..1 - the nearest car behind within pressureRange, whoever it
    // is. A human on the bumper has to count for as much as an AI one, so the free-driven player cars are
    // scanned too; they are obstacles rather than RaceField entries.
    float PressureFromBehind()
    {
        if (pressureRange <= 0f || _spline.IsOnPit) return 0f;

        float nearest = float.MaxValue;
        if (RaceField.TryGetBehind(_spline, pressureRange, out _, out float aiGap)) nearest = aiGap;

        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack == null || p.ObstacleTrack != _spline.track) continue;
            float gap = -SignedGapTo(p.TrackDistance);   // + = behind us
            if (gap > 0f && gap < nearest) nearest = gap;
        }

        return nearest == float.MaxValue ? 0f : RaceCraft.Pressure01(nearest, pressureRange);
    }

    // The nearest car behind that is genuinely further round the race than we are and close enough to be
    // let past. Laps come from RacePositionTracker, which counts line crossings for every car tagged
    // Vehicle - AI and human alike - so being lapped by the player waves the same flag as being lapped by
    // an AI. No tracker (practice, qualifying, a scene without one) means nobody is a lap down: we simply
    // never yield, which is the right answer for a session that isn't scored on laps.
    bool TryGetLapperBehind(out float lapperLat, out float gapBehind)
    {
        lapperLat = 0f;
        gapBehind = 0f;
        if (blueFlagRange <= 0f) return false;

        var rt = RacePositionTracker.Instance;
        if (rt == null) return false;
        int myLap = rt.LapOf(transform);

        float best = float.MaxValue;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float gap = -LongitudinalGap(_spline, other);   // + = behind us
            if (gap >= best) continue;
            // Range first, so the lap lookup below only runs for the handful of cars close enough to
            // matter. ShouldYield re-tests it; this is only here to keep the common case a compare.
            if (gap <= 0f || gap > blueFlagRange) continue;
            if (!RaceCraft.ShouldYield(myLap, rt.LapOf(other.transform), gap, blueFlagRange)) continue;
            best = gap;
            lapperLat = other.LateralOnTrack;
            gapBehind = gap;
        }

        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack == null || p.ObstacleTrack != _spline.track) continue;
            float gap = -SignedGapTo(p.TrackDistance);
            if (gap >= best || gap <= 0f || gap > blueFlagRange) continue;
            if (!RaceCraft.ShouldYield(myLap, rt.LapOf(p.transform), gap, blueFlagRange)) continue;
            best = gap;
            lapperLat = p.TrackLateral;
            gapBehind = gap;
        }

        return best < float.MaxValue;
    }

    // ---- Rivalry / payback ----

    bool PaybackActive => _paybackTimer > 0f && (_paybackRivalSpline != null || _paybackRivalObstacle != null);

    // Continuous clean drafting behind the same partner trickles the relationship upward: +1 per
    // draftBondSeconds. Switching partners resets the clock.
    void AccumulateDraftBond(string partner, float dt)
    {
        if (draftBondSeconds <= 0f || string.IsNullOrEmpty(partner)) return;
        if (partner != _draftPartnerName)
        {
            _draftPartnerName = partner;
            _draftBondTimer = 0f;
        }
        _draftBondTimer += dt;
        if (_draftBondTimer >= draftBondSeconds)
        {
            _draftBondTimer = 0f;
            DriverRelationships.Modify(MyName(), partner, 1f);
        }
    }

    string MyName()
    {
        if (_label == null) _label = GetComponent<DriverLabel>();
        return _label != null && !string.IsNullOrEmpty(_label.driverName) ? _label.driverName : gameObject.name;
    }

    // Look for anyone nearby this driver hates enough to wreck: AI splines first, then the free-driven
    // player (an obstacle, not a RaceField entry). One target max; first qualifying roll wins.
    void ScanForPayback()
    {
        string myName = MyName();

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline || other.IsOnPit) continue;
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            if (Mathf.Abs(LongitudinalGap(_spline, other)) > paybackRange) continue;
            if (TryLaunchPayback(myName, DriverRelationships.NameOf(other.gameObject)))
            {
                _paybackRivalSpline = other;
                _paybackRivalObstacle = null;
                return;
            }
        }

        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.ObstacleTrack != _spline.track) continue;
            if (Mathf.Abs(SignedGapTo(p.TrackDistance)) > paybackRange) continue;
            if (TryLaunchPayback(myName, DriverRelationships.NameOf(p.gameObject)))
            {
                _paybackRivalObstacle = p;
                _paybackRivalSpline = null;
                return;
            }
        }
    }

    bool TryLaunchPayback(string myName, string otherName)
    {
        if (string.IsNullOrEmpty(otherName) || otherName == myName) return false;
        float rel = DriverRelationships.Get(myName, otherName);
        if (rel > DriverRelationships.PaybackThreshold) return false;
        if (_paybackNextAllowed.TryGetValue(otherName, out float next) && Time.time < next) return false;

        // Right at the threshold a cautious driver almost never snaps; deep in the red an aggressive one
        // lunges most scans.
        float depth01 = Mathf.Clamp01((DriverRelationships.PaybackThreshold - rel) / 40f);
        float chance = paybackBaseChance * (0.5f + _phaseAggression) * (0.6f + 0.8f * depth01);
        if (Random.value > chance) return false;

        _paybackNextAllowed[otherName] = Time.time + paybackCooldownSeconds;
        _paybackTimer = paybackDurationSeconds;
        DriverRelationships.DeclarePayback(myName, otherName);
        return true;
    }

    // Rival's lateral position + signed longitudinal gap (+ = rival ahead). False once they're untrackable.
    bool TryGetRivalTrackPose(out float lat, out float gap)
    {
        lat = 0f;
        gap = 0f;
        if (_paybackRivalSpline != null)
        {
            if (_paybackRivalSpline.IsOnPit || !_paybackRivalSpline.isActiveAndEnabled) return false;
            lat = _paybackRivalSpline.LateralOnTrack;
            gap = LongitudinalGap(_spline, _paybackRivalSpline);
            return true;
        }
        if (_paybackRivalObstacle != null)
        {
            if (_paybackRivalObstacle.ObstacleTrack != _spline.track || !_paybackRivalObstacle.isActiveAndEnabled) return false;
            lat = _paybackRivalObstacle.TrackLateral;
            gap = SignedGapTo(_paybackRivalObstacle.TrackDistance);
            return true;
        }
        return false;
    }

    float SignedGapTo(float otherDist)
    {
        float len = _spline.TrackLength;
        float g = otherDist - _spline.DistanceOnTrack;
        if (g > len * 0.5f) g -= len;
        else if (g < -len * 0.5f) g += len;
        return g;
    }
}
