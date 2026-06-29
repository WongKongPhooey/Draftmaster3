using UnityEngine;

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
    [Tooltip("Lateral half-width (m) within which a follower benefits from slipstream.")]
    public float draftingLateralWidth = 2.5f;

    [Header("Rear-end Avoidance")]
    [Tooltip("Range (m) scanned for a slower/stopped car ahead to brake for. Must exceed the braking distance from racing speed, so keep it well above lookAheadRange.")]
    public float brakeScanRange = 130f;
    [Tooltip("Time headway (s) to the car ahead — the gap held even at matched speed scales with our speed.")]
    public float followHeadwaySeconds = 0.7f;
    [Tooltip("Braking rate (m/s²) assumed when sizing the safe following gap. Lower = brake earlier / more margin.")]
    public float followDecelMps2 = 11f;
    [Tooltip("Gap (m) at which we back off BELOW the car ahead's speed so we don't tap it.")]
    public float hardFollowGap = 6f;

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

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        if (_spline != null) _basePaceMultiplier = _spline.paceMultiplier;
    }

    public void SetBasePace(float baseMul) { _basePaceMultiplier = baseMul; }

    void FixedUpdate()
    {
        if (_spline == null || _spline.TrackLength <= 0f) return;
        // Racing tactics stay off until the green flag. During PreGrid/Formation, FormationController
        // owns the AI's speed cap and line so the field forms up behind the safety car.
        if (!RaceStart.IsGreen) return;

        float dt = Time.fixedDeltaTime;

        // Rolling start: for launchWindowSeconds after green, ease the follow-distance cap in from 0→1 so the
        // field accelerates together off the line (like the released player) instead of the cap pinning each
        // follower to the car ahead and the green crawling back through the pack from the leader. 1 = full cap.
        float sinceGreen = RaceStart.SecondsSinceGreen;
        float launchCapBlend = (launchWindowSeconds > 0f && sinceGreen >= 0f && sinceGreen < launchWindowSeconds)
            ? Mathf.Clamp01(sinceGreen / launchWindowSeconds) : 1f;

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

            // Rear-end avoidance: keep a speed-scaled gap and shed our closing speed EARLY so we never plough
            // into a slower / stopped car. reqGap = fixed buffer + time-headway + the distance to bleed our
            // closing speed at followDecelMps2. (The old fixed 6–14 m gap was far too short at racing speeds —
            // that's what caused the start-line pileups.) Aggressive drivers tuck in a touch tighter.
            float mySpeedMps = mySpeed * MphToMps;
            float closingMps = Mathf.Max(0f, mySpeedMps - aheadSpeed * MphToMps);
            float brakeDist = (closingMps * closingMps) / (2f * Mathf.Max(followDecelMps2, 1f));
            float reqGap = (minFollowDistance + mySpeedMps * followHeadwaySeconds + brakeDist)
                           * Mathf.Lerp(1.15f, 0.85f, aggression01);
            if (aheadGap < reqGap)
            {
                // Ease from the leader's speed (at reqGap) down to a touch under it (at hardFollowGap).
                float close01 = Mathf.InverseLerp(reqGap, hardFollowGap, aheadGap);
                float followCap = Mathf.Lerp(aheadSpeed, Mathf.Max(0f, aheadSpeed - 6f), close01);
                // Rolling start: blend from our own race pace toward the follow cap over the launch window.
                followCap = Mathf.Lerp(_spline.DesiredMph, followCap, launchCapBlend);
                speedCap = Mathf.Min(speedCap, followCap);
            }
            if (aheadGap < hardFollowGap) speedCap = Mathf.Min(speedCap, aheadSpeed * 0.6f);

            // Drafting: close, aligned, above min speed. Linear falloff with gap. Tighter alignment = more bonus.
            var vi = _spline.vehicleInfo;
            if (vi != null && mySpeed >= vi.draftingMinSpeed && aheadGap <= vi.draftingMaxGap)
            {
                float lateralDelta = Mathf.Abs(_spline.LateralOnTrack - aheadLat);
                if (lateralDelta <= draftingLateralWidth)
                {
                    float gapFrac = 1f - (aheadGap / Mathf.Max(vi.draftingMaxGap, 0.1f));
                    float latFrac = 1f - (lateralDelta / Mathf.Max(draftingLateralWidth, 0.1f));
                    speedBoost = vi.draftingMaxBonus * Mathf.Clamp01(gapFrac) * Mathf.Clamp01(latFrac);
                }
            }
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
            float pClosingMps = Mathf.Max(0f, myMpsNow - pSpeedMph * MphToMps);
            float pBrakeDist = (pClosingMps * pClosingMps) / (2f * Mathf.Max(followDecelMps2, 1f));
            float pReqGap = (minFollowDistance + myMpsNow * followHeadwaySeconds + pBrakeDist)
                            * Mathf.Lerp(1.15f, 0.85f, aggression01);
            if (pg < pReqGap)
            {
                float close01 = Mathf.InverseLerp(pReqGap, hardFollowGap, pg);
                float pCap = Mathf.Lerp(pSpeedMph, Mathf.Max(0f, pSpeedMph - 6f), close01);
                pCap = Mathf.Lerp(_spline.DesiredMph, pCap, launchCapBlend); // rolling-start ease-in
                speedCap = Mathf.Min(speedCap, pCap);
            }
            if (pg < hardFollowGap) speedCap = Mathf.Min(speedCap, pSpeedMph * 0.6f);

            // Go around a much-slower / stopped player when a side is clear of other cars.
            float myPotential = Mathf.Max(_spline.CurrentMph, _spline.DesiredMph);
            if (!wantOvertake && pSpeedMph < myPotential - 2f && _cooldownTimer <= 0f)
            {
                float side = p.TrackLateral >= _spline.LateralOnTrack ? -1f : 1f;
                if (!SideOccupied(side)) { overtakeDir = side; wantOvertake = true; }
                else if (!SideOccupied(-side)) { overtakeDir = -side; wantOvertake = true; }
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
            if (RaceField.TryGetAhead(_spline, minFollowDistance * 2f, out var blocker, out _))
                _recoveryDir = blocker.LateralOnTrack >= _spline.LateralOnTrack ? -1f : 1f;
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

        float effectivePace = _basePaceMultiplier;
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
}
