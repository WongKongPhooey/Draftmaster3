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
    public float minFollowDistance = 5f;
    public float sidewaysRange = 12f;
    public float sidewaysWidth = 3.5f;
    [Tooltip("Distance ahead (m) scanned to pick outside-of-turn passing side.")]
    public float cornerScanDistance = 90f;
    [Tooltip("Lateral half-width (m) within which a follower benefits from slipstream.")]
    public float draftingLateralWidth = 2.5f;

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
        float desiredTactical = 0f;
        float speedCap = float.MaxValue;
        float speedBoost = 0f;
        bool wantOvertake = false;
        float overtakeDir = 0f;

        if (RaceField.TryGetAhead(_spline, lookAheadRange, out var ahead, out float aheadGap))
        {
            float aheadSpeed = ahead.CurrentMph;
            float mySpeed = _spline.CurrentMph;
            float aheadLat = ahead.LateralOnTrack;

            float closingRange = Mathf.Lerp(overtakeClosingRange * 0.7f, overtakeClosingRange * 1.4f, aggression01);
            // Compare against what we COULD do (profile pace), not current speed — once we've matched the
            // leader's speed the current-speed delta is zero and a train forms that never breaks.
            float myPotential = Mathf.Max(mySpeed, _spline.DesiredMph);
            if (aheadGap < closingRange && aheadSpeed < myPotential - 2f && _cooldownTimer <= 0f)
            {
                wantOvertake = true;
                // Prefer outside of upcoming turn (more room, safer arc). Aggressive drivers can dive inside.
                int turnSign = _spline.NextTurnSign(cornerScanDistance);
                if (turnSign != 0)
                {
                    float outsideDir = turnSign; // positive turn (left) → outside is positive lateral (right of travel)
                    float insideDir = -outsideDir;
                    overtakeDir = aggression01 > 0.75f ? insideDir : outsideDir;
                }
                else
                {
                    overtakeDir = aheadLat >= 0f ? -1f : 1f;
                }
            }

            float safeGap = Mathf.Lerp(14f, 6f, aggression01);
            if (aheadGap < safeGap)
            {
                float blend = Mathf.Clamp01((safeGap - aheadGap) / Mathf.Max(safeGap - minFollowDistance, 0.1f));
                speedCap = Mathf.Lerp(mySpeed, aheadSpeed, blend);
            }
            if (aheadGap < minFollowDistance) speedCap = Mathf.Min(speedCap, aheadSpeed * 0.85f);

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

    static float LongitudinalGap(SplineDriver self, SplineDriver other)
    {
        float trackLen = self.TrackLength;
        float g = other.DistanceOnTrack - self.DistanceOnTrack;
        if (g > trackLen * 0.5f) g -= trackLen;
        else if (g < -trackLen * 0.5f) g += trackLen;
        return g;
    }
}
