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
    public float targetGap = 9f;
    [Tooltip("Speed correction (mph) applied per metre of gap error. Higher = closes/opens gaps faster, but too high makes the train string-UNSTABLE (a wobble amplifies down the field into a pile-up).")]
    public float gapGainMphPerMetre = 2.5f;
    [Tooltip("Gap error (m) ignored before the car reacts — a deadband so a hair-trigger correction doesn't ripple back through the field as a phantom stop-and-go jam.")]
    public float gapDeadbandM = 1.5f;
    [Tooltip("Brake (mph) per mph of CLOSING speed on the car ahead. This relative-velocity damping is what makes the train string-STABLE: a car eases off the moment it's catching the car ahead (before the gap collapses), so a disturbance up front dies out down the line instead of amplifying into a pile-up.")]
    public float relVelDampMph = 0.85f;
    [Tooltip("Max rate (mph/sec) the formation speed cap may FALL. Braking is rate-limited so a touch or a slow car ahead produces a gentle, bounded slow-down instead of a hard stab that ripples back through the pack as a pile-up. Accelerating up is not limited.")]
    public float maxBrakeMphPerSec = 14f;
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

    [Header("Two-wide formation")]
    [Tooltip("Pack into two columns this far (m) either side of centre — half the lateral gap between the two cars in a row. The field runs double-file the WHOLE formation lap, so it's paired up ready for the start. Must comfortably exceed weaveAmplitude + half a car width: the columns and the weave are ACTIVE AT THE SAME TIME, and anti-phase weave eats 2*weaveAmplitude of the pair gap.")]
    public float columnHalfOffset = 2.2f;
    [Tooltip("Longitudinal gap (m) each row holds behind the row ahead while closed up. Small = tight rows, but too small and a wobble closes it to a touch.")]
    public float rowGap = 8f;
    [Tooltip("Through a turn, scale the column offset by this (0..1) so the pair eases toward centre but STAYS paired — never collapses to single file (which is what made the field look single-file on a road course whose close-up zone contains corners).")]
    [Range(0f, 1f)] public float cornerColumnScale = 0.6f;

    [Header("Collision avoidance")]
    [Tooltip("Range ahead (m) scanned for the nearest car DIRECTLY IN MY PATH (lateral overlap) — the car I'd actually rear-end. Watched regardless of the formation reference; the real anti-collision net.")]
    public float avoidScanRange = 26f;
    [Tooltip("Lateral overlap (m) that counts as 'in my path' — roughly a car width plus margin, so a car drifting between the two columns is caught before it converges. Kept below the column separation (2*columnHalfOffset) so a clean two-wide partner isn't falsely braked for.")]
    public float avoidLateralGate = 2.2f;
    [Tooltip("Speed-dependent following cushion: keep at least currentSpeed * this (seconds) of gap. Inside it, the car avoids (slips alongside or slows).")]
    public float avoidHeadwaySec = 0.55f;
    [Tooltip("Absolute floor (m) the speed-dependent cushion never drops below, so cars keep a gap even at crawl.")]
    public float avoidMinGap = 5f;
    [Tooltip("Lateral offset (m) from a car-in-front to sit cleanly ALONGSIDE it when slipping out to pass — about one car width so the boxes clear.")]
    public float alongsideClear = 2.4f;
    [Tooltip("Braking authority (mph/sec) granted to a car actively avoiding a contact — overrides the (possibly weak) decel curve so a boxed-in car can actually slow in time. Firm but not instant.")]
    public float avoidHardDecelMphPerSec = 30f;
    [Tooltip("Longitudinal window (m) within which another car counts as 'beside' me (so I won't slip into its side).")]
    public float besideLongM = 5.5f;
    [Tooltip("Lateral window (m) on a side within which a car counts as 'beside' me, blocking a slip to that side.")]
    public float besideLatM = 2.6f;

    [Header("Corner caution")]
    [Tooltip("Distance ahead (m) scanned for a turn. Inside this, the car drops the catch-up boost and the weave so it can hold the racing line through the corner.")]
    public float cornerLookahead = 55f;

    [Header("Pit-out settle")]
    [Tooltip("After rejoining the main track from the pit, hold this gentle speed (mph) with no weave for a moment so the car eases onto the racing line instead of being snapped across by the merge.")]
    public float pitOutMph = 45f;
    [Tooltip("How long (s) the pit-out settle lasts after leaving the pit lane.")]
    public float pitOutSettleSeconds = 3f;
    [Tooltip("How fast (m/s of lateral) a car filing out for the formation lap pulls off the grey parked-box strip onto the pit lane's driving line. The grid spawn parks cars at the box-lane lateral; without this they ride that offset single-file down the wall for the whole lane.")]
    public float pitPullOutLateralRate = 1.5f;

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

    const float MphToMps = 1f / 2.237f;

    SplineDriver _spline;
    PlayerVehicleController _pvc;
    SplineInputDriver _input;
    float _lateral;     // current applied tacticalLateralOffset (slew-limited)
    float _weaveEnv;    // 0..1 envelope so the weave ramps in gently
    bool _wasPit;
    float _pitOutTimer;
    float _prevCap;     // last frame's speed cap, for rate-limiting how fast braking can ramp the cap down
    bool _hasPrevCap;
    float _savedLineFactor; // the car's own racing line, parked during formation and restored at green
    bool _lineFactorSaved;

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

        // The pair columns are offsets from each car's OWN racing line, and per-driver lines (lineFactor,
        // skewed by AIDriverBinding aggression) all converge at an apex — offset or not, two different base
        // paths can cross there and put a pair in contact. Run the whole field on the SAME line (ideal) for
        // the formation lap so the pair separation is exactly the column difference everywhere.
        if (phase == RaceStart.Phase.Formation && _spline != null && !_lineFactorSaved)
        {
            _savedLineFactor = _spline.lineFactor;
            _spline.lineFactor = 0f;
            _lineFactorSaved = true;
        }

        // Leaving the formation lap (green or pregrid): drop any weave/lateral the formation applied —
        // AIRacingBehaviour owns the lateral line once racing.
        if (phase != RaceStart.Phase.Formation)
        {
            _lateral = 0f;
            _weaveEnv = 0f;
            _hasPrevCap = false; // drop the brake-rate-limit history so the green launch isn't held back by it
            if (_spline != null)
            {
                _spline.tacticalLateralOffset = 0f;
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
        if (RaceStart.Current != RaceStart.Phase.Formation) return;

        float dt = Time.fixedDeltaTime;

        // While still filing out of the pit lane, let SplineDriver's pit crawl handle pace — but pull the car
        // off the parked box lane onto the pit CENTERLINE once it's rolling. Only the formation pull-out does
        // this here: practice stints manage their own box-lane lateral in both directions (PracticeAIStint).
        if (_spline.usePitLane)
        {
            _wasPit = true;
            if (_spline.CurrentMph > 3f)
                _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, 0f, pitPullOutLateralRate * dt);
            return;
        }
        // Just rejoined the main track — start the pit-out settle so the merge doesn't snap the car off line.
        if (_wasPit) { _wasPit = false; _pitOutTimer = pitOutSettleSeconds; _weaveEnv = 0f; }
        bool settling = _pitOutTimer > 0f;
        if (settling) _pitOutTimer -= dt;

        float cruise = FormationDirector.Instance != null ? FormationDirector.Instance.cruiseMph : cruiseMph;
        bool closingUp = FormationDirector.Instance != null && FormationDirector.Instance.FieldClosingUp;

        // Corner awareness: in or approaching a turn, hold the racing line — no catch-up overspeed, no weave.
        var phase = _spline.CurrentPhase;
        bool inTurn = phase == SplineDriver.CornerPhase.Entry || phase == SplineDriver.CornerPhase.Apex ||
                      phase == SplineDriver.CornerPhase.Exit || phase == SplineDriver.CornerPhase.Approach;
        bool corner = inTurn || _spline.NextTurnSign(cornerLookahead) != 0;

        // --- Speed law: pace off the NEAREST car ahead in my corridor — any AI, the safety car, OR the free-driven
        //     player — never off a grid bookkeeping reference past them. The old code stationed behind a fixed
        //     grid-row-ahead, so a car would charge a far reference and rear-end the one ACTUALLY in front (and had
        //     no idea what to do when the player got out of line). Qualifying order / columns are now cosmetic
        //     LATERAL only; the gaps are held by following whoever is genuinely ahead of me.
        float cap;
        float floorMph = minCapMph;
        float avoidTarget = float.NaN; // NaN = no lateral avoidance this frame
        bool emergencyBrake = false;

        if (settling)
            cap = pitOutMph; // ease onto the racing line after the pit merge before resuming pace
        else
        {
            // Free to close the train up on an open straight; held to cruise in a corner or the close-up bunch.
            cap = (corner || closingUp) ? cruise : cruise + catchUpBonusMph;
            float wantGap = closingUp ? rowGap : targetGap;

            if (TryNearestAheadInPath(avoidScanRange, avoidLateralGate,
                    out float foeMph, out float foeLat, out float foeGap, out bool foeIsPaceCar))
            {
                float myMps = _spline.CurrentMph * MphToMps;
                float safeGap = Mathf.Max(avoidMinGap, myMps * avoidHeadwaySec);

                // Station off the nearest car ahead EVERY frame (ACC: match its pace, trimmed by gap error + closing
                // rate), so closing speed never builds in the first place. Held to at least the speed-dependent cushion.
                float follow = FollowCapRaw(foeMph, foeGap, Mathf.Max(wantGap, safeGap));
                if (foeIsPaceCar) follow = Mathf.Min(follow, cruise); // pace the safety car, but NEVER chase its peel-away
                cap = Mathf.Min(cap, follow);

                // Inside the cushion and still closing: act. Slip ALONGSIDE on an open side if there's room (a real
                // pace-lap move that opens the gap with nobody braking); otherwise hold station with FULL braking
                // authority (bypass the gentle rate-limit + guarantee the decel) so the slow actually lands in time.
                if (foeGap < safeGap && _spline.CurrentMph - foeMph > -1f)
                {
                    int side = foeIsPaceCar ? 0 : ChooseAvoidSide();
                    if (side != 0)
                    {
                        avoidTarget = foeLat + side * alongsideClear;
                        cap = Mathf.Min(cap, foeMph + 1f); // match its pace while sliding out so we don't tag it mid-move
                    }
                    else
                    {
                        emergencyBrake = true;
                        if (foeMph < blockStoppedMph) floorMph = 0f; // foe stopped and I can't pass → allow a full stop
                    }
                }
            }
        }

        cap = Mathf.Clamp(cap, floorMph, cruise + catchUpBonusMph);

        // Rate-limit braking: the cap may rise freely but only FALL at maxBrakeMphPerSec, so normal station-keeping
        // bleeds speed off gently (no stab-and-amplify pile-up wave). EXEMPT when actively avoiding a contact
        // (emergencyBrake): there the firm slow MUST land. _hasPrevCap guards the first frame.
        if (_hasPrevCap && !emergencyBrake) cap = Mathf.Max(cap, _prevCap - maxBrakeMphPerSec * dt);
        _prevCap = cap;
        _hasPrevCap = true;

        _spline.aiMaxSpeedMph = cap;
        _spline.aiMinDecelMphPerSec = emergencyBrake ? avoidHardDecelMphPerSec : 0f; // brake authority for the avoidance
        _spline.paceMultiplier = formationPace;
        _spline.aiSpeedBoostMph = 0f;

        // --- Lateral: avoidance wins; otherwise hold the two-wide column the WHOLE lap (double file), with a gentle
        //     tyre-warming weave layered on early straights that fades out near the line so the rows sit steady for
        //     the start. Through turns the column eases toward centre (cornerColumnScale) but never collapses to
        //     single file — that collapse is what made the field look single-file approaching the green.
        bool weaveOk = !settling && !corner && !closingUp && float.IsNaN(avoidTarget);
        _weaveEnv = Mathf.MoveTowards(_weaveEnv, weaveOk ? 1f : 0f, dt / Mathf.Max(weaveRampSeconds, 0.01f));

        float lateralTarget;
        float slew;
        if (!float.IsNaN(avoidTarget))
        {
            lateralTarget = avoidTarget;
            slew = avoidSlewPerSec;
        }
        else
        {
            // Two columns by grid parity: even slots left, odd slots right.
            int parity = ((_spline.qualifyingPosition % 2) + 2) % 2;
            float column = (parity == 0 ? -columnHalfOffset : columnHalfOffset);
            if (corner) column *= cornerColumnScale;
            float ph = _spline.qualifyingPosition * weavePhasePerSlot;
            float weave = Mathf.Sin(Time.time * (2f * Mathf.PI * weaveHz) + ph) * weaveAmplitude * _weaveEnv;
            lateralTarget = column + weave;
            slew = weaveSlewPerSec;
        }
        _lateral = Mathf.MoveTowards(_lateral, lateralTarget, slew * dt);
        _spline.tacticalLateralOffset = _lateral;
    }

    // Linear ACC follower: target the car-ahead's speed, trimmed by gap error (with a deadband) and damped by the
    // closing rate. The closing-rate term is what makes the train string-STABLE — a wobble up front dies out down
    // the line instead of amplifying into a stop-and-go pile-up.
    float FollowCapRaw(float aheadMph, float gap, float wantGap)
    {
        float gapErr = gap - wantGap;
        if (Mathf.Abs(gapErr) <= gapDeadbandM) gapErr = 0f;
        else gapErr -= Mathf.Sign(gapErr) * gapDeadbandM;
        float closingMph = _spline.CurrentMph - aheadMph; // + = we're catching the car ahead
        return aheadMph + gapGainMphPerMetre * gapErr - relVelDampMph * closingMph;
    }

    // Nearest thing ahead within range that overlaps my path laterally (|lateral diff| <= latGate ≈ a car width) —
    // the car I'd actually rear-end. Scans BOTH the AI field (RaceField) AND the free-driven player (RaceObstacles),
    // so a player who drops back, stops, or gets sideways in front of me is paced off and avoided just like an AI.
    // A thing level with me (gap ≈ 0) or further to the side is skipped. isPaceCar flags the safety car so the
    // caller can pace it without chasing its close-up peel-away.
    bool TryNearestAheadInPath(float range, float latGate,
        out float aheadMph, out float aheadLat, out float gap, out bool isPaceCar)
    {
        aheadMph = 0f;
        aheadLat = 0f;
        gap = 0f;
        isPaceCar = false;
        float len = _spline.TrackLength;
        if (len <= 0f) return false;
        float myDist = _spline.DistanceOnTrack;
        float myLat = _spline.LateralOnTrack;
        float bestGap = float.MaxValue;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || d.IsOnPit) continue;
            if (Mathf.Abs(d.TrackLength - len) > 0.5f) continue;
            float g = d.DistanceOnTrack - myDist;
            if (g <= 0f) g += len;
            if (g <= 0.2f || g > range) continue;                         // level with me, or out of range
            if (Mathf.Abs(d.LateralOnTrack - myLat) > latGate) continue;   // off to the side — not in my path
            if (g < bestGap)
            {
                bestGap = g;
                aheadMph = d.CurrentMph;
                aheadLat = d.LateralOnTrack;
                isPaceCar = d.qualifyingPosition == FormationOrder.SafetyCarGrid;
            }
        }

        var obstacles = RaceObstacles.All;
        for (int oi = 0; oi < obstacles.Count; oi++)
        {
            var p = obstacles[oi];
            if (p == null || p.ObstacleTrack != _spline.track) continue;
            float g = p.TrackDistance - myDist;
            if (g <= 0f) g += len;
            if (g <= 0.2f || g > range) continue;
            if (Mathf.Abs(p.TrackLateral - myLat) > latGate) continue;
            if (g < bestGap)
            {
                bestGap = g;
                aheadMph = p.SpeedMph;
                aheadLat = p.TrackLateral;
                isPaceCar = false;
            }
        }

        if (bestGap == float.MaxValue) return false;
        gap = bestGap;
        return true;
    }

    // Choose a side to slip alongside the car in front: +1 = right, -1 = left, 0 = boxed in. A side is open only if
    // there's track room beyond me on it AND no car already sitting beside me there. Prefers the roomier side.
    int ChooseAvoidSide()
    {
        bool hasRoom = _spline.GetLateralRoom(out float leftRoom, out float rightRoom);
        bool leftOpen = (!hasRoom || leftRoom > alongsideClear) && !CarBeside(-1);
        bool rightOpen = (!hasRoom || rightRoom > alongsideClear) && !CarBeside(1);
        if (leftOpen && rightOpen) return rightRoom >= leftRoom ? 1 : -1;
        if (rightOpen) return 1;
        if (leftOpen) return -1;
        return 0;
    }

    // Is another car alongside me on the given side (+1 right, -1 left)? Within a short longitudinal window and a
    // lateral window on that side — so I don't slip sideways into a car that's already there.
    bool CarBeside(int side)
    {
        float len = _spline.TrackLength;
        if (len <= 0f) return false;
        float myDist = _spline.DistanceOnTrack;
        float myLat = _spline.LateralOnTrack;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || d.IsOnPit) continue;
            if (Mathf.Abs(d.TrackLength - len) > 0.5f) continue;
            float g = d.DistanceOnTrack - myDist;
            if (g > len * 0.5f) g -= len; else if (g < -len * 0.5f) g += len;
            if (Mathf.Abs(g) > besideLongM) continue;                  // not alongside longitudinally
            float dl = d.LateralOnTrack - myLat;
            if (side * dl > 0f && Mathf.Abs(dl) < besideLatM) return true; // a car close on that side
        }
        return false;
    }
}
