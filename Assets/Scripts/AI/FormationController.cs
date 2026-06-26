using UnityEngine;

// Drives an AI car during the FORMATION phase: tucks it into a single-file train behind the safety
// car (or the car ahead) and weaves it gently to mimic warming the tyres. Also slows for and steers
// around a stopped/crashed car ahead so the field doesn't pile in. Dormant in every other phase —
// AIRacingBehaviour owns the car once the race goes green.
//
// Works purely through SplineDriver's public knobs (aiMaxSpeedMph / tacticalLateralOffset /
// paceMultiplier), so the car's path, corner speeds and dynamic motion are unchanged.
[RequireComponent(typeof(SplineDriver))]
public class FormationController : MonoBehaviour
{
    [Tooltip("Cruise pace of the train (mph). Falls back to this when no FormationDirector is present.")]
    public float cruiseMph = 60f;
    [Tooltip("How far behind the car ahead this car tries to sit (m).")]
    public float targetGap = 7f;
    [Tooltip("Speed correction (mph) applied per metre of gap error. Higher = closes/opens gaps faster.")]
    public float gapGainMphPerMetre = 4f;
    [Tooltip("Most this car may exceed cruise pace by while catching the train up (mph). Only used on straights. Kept low so the field doesn't string out far ahead of the pace.")]
    public float catchUpBonusMph = 9f;
    [Tooltip("Lowest speed cap (mph) — a floor so a car never crawls to a halt mid-formation (lifted when blocked).")]
    public float minCapMph = 12f;
    [Tooltip("How far ahead (m) to look for the car to follow.")]
    public float lookAheadRange = 90f;
    [Tooltip("Pace multiplier on the SplineDriver corner-speed profile during formation. <1 gives the dynamic model grip margin so it holds the line through turns. Barely affects straights (capped to cruise).")]
    [Range(0.6f, 1f)] public float formationPace = 0.9f;

    [Tooltip("This car has no dynamic model (cheap kinematic AI): stay on the kinematic SplineDriver through the green flag instead of handing back to PlayerVehicleController. Set by the spawner for a kinematic field.")]
    public bool kinematic;

    [Header("Corner caution")]
    [Tooltip("Distance ahead (m) scanned for a turn. Inside this, the car drops the catch-up boost and the weave so it can hold the racing line through the corner.")]
    public float cornerLookahead = 55f;

    [Header("Pit-out settle")]
    [Tooltip("After rejoining the main track from the pit, hold this gentle speed (mph) with no weave for a moment so the car eases onto the racing line instead of being snapped across by the merge.")]
    public float pitOutMph = 45f;
    [Tooltip("How long (s) the pit-out settle lasts after leaving the pit lane.")]
    public float pitOutSettleSeconds = 3f;

    [Header("Blockage avoidance (stopped/crashed car ahead)")]
    [Tooltip("Range ahead (m) scanned for a slow/stopped car to avoid.")]
    public float blockScanRange = 24f;
    [Tooltip("A car ahead this much slower than us (mph), or below blockStoppedMph, counts as a blockage.")]
    public float blockSpeedDeltaMph = 18f;
    [Tooltip("A car ahead below this speed (mph) always counts as a blockage even if we're slow too.")]
    public float blockStoppedMph = 14f;
    [Tooltip("Gap (m) at which we should be fully stopped behind a blockage.")]
    public float blockStopGap = 6f;
    [Tooltip("Lateral offset (m) used to steer around a blockage.")]
    public float avoidPush = 3.5f;
    [Tooltip("How fast the avoidance offset moves (m/s). Higher than the weave slew so the car actually dodges.")]
    public float avoidSlewPerSec = 4.5f;

    [Header("Tyre-warming weave")]
    [Tooltip("Lateral weave amplitude (m) on straights. Suppressed in/near turns and at pit-out.")]
    public float weaveAmplitude = 0.85f;
    [Tooltip("Weave frequency (Hz). Low = slow gentle sway the dynamic model can follow.")]
    public float weaveHz = 0.18f;
    [Tooltip("Phase offset (radians) added per grid slot, so the train snakes instead of weaving in lockstep.")]
    public float weavePhasePerSlot = 0.8f;
    [Tooltip("How fast the weave offset is allowed to change (m/s). Lower = smoother, less likely to snap the car off line.")]
    public float weaveSlewPerSec = 1.5f;
    [Tooltip("Seconds over which the weave fades back in after a pit-out settle or corner, so it never snaps on.")]
    public float weaveRampSeconds = 3f;

    const float MphToMps = 1f / 2.237f;

    SplineDriver _spline;
    PlayerVehicleController _pvc;
    SplineInputDriver _input;
    float _lateral;     // current applied tacticalLateralOffset (slew-limited)
    float _weaveEnv;    // 0..1 envelope so the weave ramps in gently
    bool _wasPit;
    float _pitOutTimer;

    void Awake() => _spline = GetComponent<SplineDriver>();

    void OnEnable()
    {
        RaceStart.PhaseChanged += OnPhaseChanged;
        OnPhaseChanged(RaceStart.Current);
    }

    void OnDisable() => RaceStart.PhaseChanged -= OnPhaseChanged;

    // The dynamic bicycle model + pure-pursuit steering is twitchy at parade speeds and spins cars off the
    // line. For the formation lap we drive KINEMATICALLY instead — SplineDriver glues the car to the racing
    // line (exactly like the safety car), so it physically cannot crash. At green we hand back to the dynamic
    // model for racing, re-seeding it with the car's current pose + speed for a smooth rolling start.
    void OnPhaseChanged(RaceStart.Phase phase)
    {
        // Lazy-fetch: GridSpawner may add this component before the dynamic-model components exist.
        if (_pvc == null) _pvc = GetComponent<PlayerVehicleController>();
        if (_input == null) _input = GetComponent<SplineInputDriver>();

        // Kinematic while forming up; a kinematic car (no dynamic model) also stays kinematic for racing.
        bool driveKinematic = phase == RaceStart.Phase.Formation || kinematic;
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
            if (_pvc != null) _pvc.enabled = true;
            if (_input != null) _input.enabled = true;
            else if (_spline != null) _spline.externalMotionController = true;
        }

        // Leaving the formation lap (green or pregrid): drop any weave/lateral the formation applied —
        // AIRacingBehaviour owns the lateral line once racing.
        if (phase != RaceStart.Phase.Formation)
        {
            _lateral = 0f;
            _weaveEnv = 0f;
            if (_spline != null) _spline.tacticalLateralOffset = 0f;
        }
    }

    void FixedUpdate()
    {
        if (_spline == null || _spline.TrackLength <= 0f) return;
        if (RaceStart.Current != RaceStart.Phase.Formation) return;

        float dt = Time.fixedDeltaTime;

        // While still filing out of the pit lane, let SplineDriver's pit crawl handle pace/line.
        if (_spline.usePitLane) { _wasPit = true; return; }
        // Just rejoined the main track — start the pit-out settle so the merge doesn't snap the car off line.
        if (_wasPit) { _wasPit = false; _pitOutTimer = pitOutSettleSeconds; _weaveEnv = 0f; }
        bool settling = _pitOutTimer > 0f;
        if (settling) _pitOutTimer -= dt;

        float cruise = FormationDirector.Instance != null ? FormationDirector.Instance.cruiseMph : cruiseMph;

        // Corner awareness: in or approaching a turn, hold the racing line — no catch-up overspeed, no weave.
        var phase = _spline.CurrentPhase;
        bool inTurn = phase == SplineDriver.CornerPhase.Entry || phase == SplineDriver.CornerPhase.Apex ||
                      phase == SplineDriver.CornerPhase.Exit || phase == SplineDriver.CornerPhase.Approach;
        bool corner = inTurn || _spline.NextTurnSign(cornerLookahead) != 0;

        // --- Base speed cap: pit-out settle, else catch-up / station-keeping.
        float cap;
        if (settling)
            cap = pitOutMph; // ease onto the racing line after the pit merge before resuming pace
        else
        {
            // Station behind the car directly ahead in GRID ORDER (an AI, the player, or the safety car) rather
            // than the nearest car ahead — that's what forms the field up in qualifying order and holds the
            // player's slot open. The gap is SIGNED: if our man slips behind us (e.g. the player drops back) the
            // negative gap drops our cap so we wait for them instead of charging off to "catch" a phantom ahead.
            var memberAhead = FormationOrder.MemberAhead(_spline.qualifyingPosition);
            if (memberAhead != null)
            {
                float len = _spline.TrackLength;
                float gap = memberAhead.TrackDistance - _spline.DistanceOnTrack;
                if (gap > len * 0.5f) gap -= len;
                else if (gap < -len * 0.5f) gap += len;
                cap = cruise + gapGainMphPerMetre * (gap - targetGap);
            }
            else
                cap = cruise + catchUpBonusMph; // nobody ahead in the order yet — close the train up (straights only)
            if (corner) cap = Mathf.Min(cap, cruise); // never carry the catch-up boost into a turn
        }

        // --- Blockage: a stopped/crashed (or much slower) car close ahead. Slow hard (allow a full stop) and
        //     steer to the open side to go around it. Overrides the weave and the minimum-speed floor.
        float floorMph = minCapMph;
        float avoidTarget = float.NaN; // NaN = no avoidance this frame
        if (RaceField.TryGetAhead(_spline, blockScanRange, out var ahead, out float bgap))
        {
            float aheadMph = ahead.CurrentMph;
            bool blocked = aheadMph < _spline.CurrentMph - blockSpeedDeltaMph || aheadMph < blockStoppedMph;
            if (blocked)
            {
                // Ramp the allowed speed from the blocker's speed down to 0 as we close to blockStopGap.
                float closeT = Mathf.InverseLerp(blockScanRange, blockStopGap, bgap); // 0 far → 1 at stop gap
                float allowedMph = Mathf.Lerp(aheadMph, 0f, Mathf.Clamp01(closeT));
                cap = Mathf.Min(cap, allowedMph);
                floorMph = 0f; // let it stop behind the wreck instead of creeping in

                // Steer toward the side with more room — opposite of where the blocker sits laterally.
                float side = ahead.LateralOnTrack >= _spline.LateralOnTrack ? -1f : 1f;
                float strength = Mathf.Clamp01((blockScanRange - bgap) / Mathf.Max(blockScanRange, 0.1f));
                avoidTarget = side * avoidPush * strength;
            }
        }

        // The free-driven player isn't in RaceField, so the block above can't see it. Treat the player as a
        // blockage too: slow (allow a full stop) and steer to the open side so the forming field doesn't pile
        // into a player who has dropped back or stopped.
        var obstacles = RaceObstacles.All;
        for (int oi = 0; oi < obstacles.Count; oi++)
        {
            var p = obstacles[oi];
            if (p == null || p.ObstacleTrack == null || p.ObstacleTrack != _spline.track) continue;
            float len = _spline.TrackLength;
            if (len <= 0f) continue;
            float pg = p.TrackDistance - _spline.DistanceOnTrack;
            if (pg <= 0f) pg += len;
            if (pg <= 0f || pg > blockScanRange) continue;

            float pMph = p.SpeedMph;
            bool pBlocked = pMph < _spline.CurrentMph - blockSpeedDeltaMph || pMph < blockStoppedMph;
            if (!pBlocked) continue;

            float pCloseT = Mathf.InverseLerp(blockScanRange, blockStopGap, pg);
            cap = Mathf.Min(cap, Mathf.Lerp(pMph, 0f, Mathf.Clamp01(pCloseT)));
            floorMph = 0f;
            float pSide = p.TrackLateral >= _spline.LateralOnTrack ? -1f : 1f;
            float pStrength = Mathf.Clamp01((blockScanRange - pg) / Mathf.Max(blockScanRange, 0.1f));
            avoidTarget = pSide * avoidPush * pStrength;
        }

        cap = Mathf.Clamp(cap, floorMph, cruise + catchUpBonusMph);

        _spline.aiMaxSpeedMph = cap;
        _spline.paceMultiplier = formationPace;
        _spline.aiSpeedBoostMph = 0f;

        // --- Lateral: avoidance wins; otherwise a slow weave on straights that ramps in gently.
        if (settling || corner) _weaveEnv = 0f;
        else _weaveEnv = Mathf.MoveTowards(_weaveEnv, 1f, dt / Mathf.Max(weaveRampSeconds, 0.01f));

        float lateralTarget;
        float slew;
        if (!float.IsNaN(avoidTarget))
        {
            lateralTarget = avoidTarget;
            slew = avoidSlewPerSec;
        }
        else
        {
            float ph = _spline.qualifyingPosition * weavePhasePerSlot;
            lateralTarget = (corner || settling)
                ? 0f
                : Mathf.Sin(Time.time * (2f * Mathf.PI * weaveHz) + ph) * weaveAmplitude * _weaveEnv;
            slew = weaveSlewPerSec;
        }
        _lateral = Mathf.MoveTowards(_lateral, lateralTarget, slew * dt);
        _spline.tacticalLateralOffset = _lateral;
    }
}
