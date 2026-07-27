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
    [Tooltip("Time headway (s) to the car ahead — the gap held even at matched speed scales with our speed.")]
    public float followHeadwaySeconds = 0.7f;
    [Tooltip("Braking rate (m/s²) assumed when sizing the safe following gap. Lower = brake earlier / more margin.")]
    public float followDecelMps2 = 11f;
    [Tooltip("Gap (m) at which we back off BELOW the car ahead's speed so we don't tap it.")]
    public float hardFollowGap = 6f;
    [Tooltip("Lateral separation (m) below which a car ahead fully blocks our corridor (≈ a car width) — the follow cap applies at full strength.")]
    public float corridorOverlapWidth = 2f;
    [Tooltip("Lateral separation (m) beyond which a car ahead no longer caps our speed at all — we're clear to drive past. The cap fades between the two widths. This is what lets a committed overtake actually PASS a slow or wrecked car instead of matching its speed until it fully stops.")]
    public float corridorClearWidth = 3.4f;

    [Header("Rolling Start")]
    [Tooltip("Seconds after the green flag during which the follow-distance speed cap is eased in, so the whole field launches together (a rolling start) instead of accordioning out from the leader. The hard nose-to-tail cap still applies, so cars can't pile in. 0 = off (cars hold full racing gaps from the instant of green).")]
    public float launchWindowSeconds = 3f;

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
    [Tooltip("Dead zone (m). Tactical changes smaller than this aren't acted on.")]
    public float tacticalDeadzone = 0.25f;
    [Tooltip("Once an overtake direction is committed, hold it for at least this many seconds before reconsidering. Prevents flip-flop.")]
    public float commitHoldSeconds = 1.5f;
    [Tooltip("Once the AI returns to neutral line, wait this long before initiating another manoeuvre.")]
    public float manoeuvreCooldown = 0.6f;

    const float MphToMps = 1f / 2.237f;

    SplineDriver _spline;
    float _smoothedTactical;
    float _commitTimer;
    float _commitDir;
    float _cooldownTimer;
    float _mistakeTimer;
    float _mistakeWobbleDir;
    float _basePaceMultiplier = 1f;
    float _stallTimer;
    float _recoveryTimer;
    float _recoveryDir;
    PracticeAIStint _stint;   // practice stints manage lateralOffset themselves — don't fight them

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
    }

    public void SetBasePace(float baseMul) { _basePaceMultiplier = baseMul; }

    void FixedUpdate()
    {
        if (_spline == null || _spline.TrackLength <= 0f) return;
        // Racing tactics stay off until the green flag. During PreGrid/Formation, FormationController
        // owns the AI's speed cap and line so the field forms up behind the safety car.
        if (!RaceStart.IsGreen) return;

        float dt = Time.fixedDeltaTime;

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
        float dynStopDist = minFollowDistance + myMpsNow * followHeadwaySeconds
                            + (myMpsNow * myMpsNow) / (2f * Mathf.Max(followDecelMps2, 1f)) + 20f;
        float scanDist = Mathf.Max(lookAheadRange, brakeScanRange, dynStopDist);

        if (RaceField.TryGetAhead(_spline, scanDist, out var ahead, out float aheadGap))
        {
            float aheadSpeed = ahead.CurrentMph;
            float mySpeed = _spline.CurrentMph;
            float aheadLat = ahead.LateralOnTrack;

            float closingRange = Mathf.Lerp(overtakeClosingRange * 0.7f, overtakeClosingRange * 1.4f, aggression01);
            // Compare against what we COULD do (profile pace), not current speed — once we've matched the
            // leader's speed the current-speed delta is zero and a train forms that never breaks.
            float myPotential = Mathf.Max(mySpeed, _spline.DesiredMph);
            // Closing-rate widening: catching a much-slower car fast → begin the move from further back.
            float closingMph = Mathf.Max(0f, myPotential - aheadSpeed);
            float initiateRange = closingRange + closingMph * ttcRangePerMph;
            if (aheadGap < initiateRange && aheadSpeed < myPotential - 2f && _cooldownTimer <= 0f)
            {
                overtakeDir = ChooseOvertakeSide(aheadLat);
                wantOvertake = overtakeDir != 0f; // 0 = both sides blocked → don't dive into traffic, just tuck in
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
            float reqGap = (minFollowDistance + mySpeedMps * followHeadwaySeconds + brakeDist)
                           * Mathf.Lerp(1.15f, 0.85f, aggression01);
            if (blockGap < reqGap)
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
            if (blockGap < hardFollowGap)
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
            float pReqGap = (minFollowDistance + myMpsNow * followHeadwaySeconds + pBrakeDist)
                            * Mathf.Lerp(1.15f, 0.85f, aggression01);
            if (pg < pReqGap && pOverlap > 0f)
            {
                float close01 = Mathf.InverseLerp(pReqGap, hardFollowGap, pg);
                float pCap = Mathf.Lerp(pSpeedMph, Mathf.Max(0f, pSpeedMph - 6f), close01);
                pCap = Mathf.Lerp(_spline.DesiredMph, pCap, launchCapBlend); // rolling-start ease-in
                pCap = Mathf.Lerp(_spline.DesiredMph, pCap, pOverlap);
                speedCap = Mathf.Min(speedCap, pCap);
            }
            if (pg < hardFollowGap && pOverlap > 0f)
                speedCap = Mathf.Min(speedCap, Mathf.Lerp(_spline.DesiredMph, pSpeedMph * 0.6f, pOverlap));

            // Go around a much-slower / stopped player when a side is clear of other cars.
            float myPotential = Mathf.Max(_spline.CurrentMph, _spline.DesiredMph);
            if (!wantOvertake && pSpeedMph < myPotential - 2f && _cooldownTimer <= 0f)
            {
                float side = p.TrackLateral >= _spline.LateralOnTrack ? -1f : 1f;
                if (!SideOccupied(side)) { overtakeDir = side; wantOvertake = true; }
                else if (!SideOccupied(-side)) { overtakeDir = -side; wantOvertake = true; }
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
                speedBoost = Mathf.Max(speedBoost, vinfo.draftingMaxBonus * towFactor);
                if (towSource != null) AccumulateDraftBond(DriverRelationships.NameOf(towSource), dt);
            }
        }

        // Commitment: once we pick a passing side, hold it. Prevents weave.
        if (wantOvertake)
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

        if (wantOvertake) desiredTactical = overtakeDir * overtakeLineOffset;

        // Defending: if a faster pursuer is close behind during the approach to a turn, shift to the inside.
        if (_spline.CurrentPhase == SplineDriver.CornerPhase.Approach || _spline.CurrentPhase == SplineDriver.CornerPhase.Entry)
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
                        desiredTactical += insideDir * defendLineOffset * strength;
                    }
                }
            }
        }

        // Side-by-side repulsion + contact response.
        float repulse = 0f;
        float contactScrub = 0f;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var other = drivers[i];
            if (other == null || other == _spline) continue;
            if (PaybackActive && other == _paybackRivalSpline) continue; // no self-preservation vs the target
            if (System.Math.Abs(other.TrackLength - _spline.TrackLength) > 0.5f) continue;
            float longGap = LongitudinalGap(_spline, other);
            if (Mathf.Abs(longGap) > sidewaysRange) continue;
            float latGap = _spline.LateralOnTrack - other.LateralOnTrack;
            float absLat = Mathf.Abs(latGap);
            float dir = latGap >= 0f ? 1f : -1f;
            if (absLat < contactLateralWidth)
            {
                // Contact: stronger push + speed scrub.
                float overlap = (contactLateralWidth - absLat) / contactLateralWidth;
                repulse += dir * (sidewaysMaxPush + contactPush) * overlap;
                contactScrub += contactSpeedScrub * overlap;
            }
            else
            {
                float threshold = sidewaysWidth * 0.6f;
                if (absLat >= threshold) continue;
                float push = (threshold - absLat) / threshold;
                repulse += dir * push * sidewaysMaxPush;
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
                _recoveryDir = stallBlocker.LateralOnTrack >= _spline.LateralOnTrack ? -1f : 1f;
            else
                _recoveryDir = _spline.LateralOnTrack >= 0f ? -1f : 1f;
            _stallTimer = 0f;
        }
        if (_recoveryTimer > 0f)
        {
            _recoveryTimer -= dt;
            speedCap = float.MaxValue;
            desiredTactical = _recoveryDir * overtakeLineOffset;
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

        // Slew-rate-limited convergence toward desired offset. Dead-zone prevents twitching near target.
        float diff = desiredTactical - _smoothedTactical;
        if (Mathf.Abs(diff) < tacticalDeadzone) diff = 0f;
        float step = maxLateralSpeed * dt;
        _smoothedTactical += Mathf.Clamp(diff, -step, step);

        // Manoeuvre cooldown once we settle near zero.
        if (Mathf.Abs(_smoothedTactical) < tacticalDeadzone && Mathf.Abs(desiredTactical) < tacticalDeadzone)
        {
            if (_cooldownTimer < manoeuvreCooldown) _cooldownTimer = manoeuvreCooldown;
        }
        if (_cooldownTimer > 0f) _cooldownTimer -= dt;

        // Mistake roll: probability scales with (1 - consistency). Active mistake adds wobble + pace dip.
        if (_mistakeTimer > 0f)
        {
            _mistakeTimer -= dt;
            _smoothedTactical += _mistakeWobbleDir * mistakeWobble * dt;
        }
        else if (mistakeProbabilityPerSecond > 0f)
        {
            float perTickP = mistakeProbabilityPerSecond * (1f - consistency01) * dt;
            if (Random.value < perTickP)
            {
                _mistakeTimer = mistakeDurationSeconds;
                _mistakeWobbleDir = Random.value < 0.5f ? -1f : 1f;
            }
        }

        float effectivePace = _basePaceMultiplier * TrackConditions.AiPaceMultiplier;
        if (_mistakeTimer > 0f) effectivePace *= mistakePaceFactor;
        var tireModel = GetComponent<TireModel>();
        if (tireModel != null) effectivePace *= tireModel.OverallGrip;
        else { var tire = GetComponent<TireState>(); if (tire != null) effectivePace *= tire.GripMultiplier; }
        _spline.paceMultiplier = effectivePace;

        _spline.tacticalLateralOffset = _smoothedTactical;
        _spline.aiMaxSpeedMph = speedCap;
        _spline.aiSpeedBoostMph = speedBoost;
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
            pick = aggression01 > 0.75f ? insideDir : outsideDir;
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
        => 1f - Mathf.Clamp01((lateralSeparation - corridorOverlapWidth)
                              / Mathf.Max(corridorClearWidth - corridorOverlapWidth, 0.1f));

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
            if (ov <= 0f) continue;
            best = lg;
            blocker = other;
            gap = lg;
            overlap01 = ov;
        }
        return blocker != null;
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
        float chance = paybackBaseChance * (0.5f + aggression01) * (0.6f + 0.8f * depth01);
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
