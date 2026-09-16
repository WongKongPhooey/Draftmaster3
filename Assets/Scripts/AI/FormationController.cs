using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;

// Drives an AI car during the FORMATION phase: tucks it into a two-wide train behind the safety car and weaves
// it gently to mimic warming the tyres. Dormant in every other phase — AIRacingBehaviour owns the car once the
// race goes green.
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
    [Tooltip("Lowest speed cap (mph) the station-keeping asks for — a floor so a car never crawls to a halt mid-formation. The safety law ignores it: a car will stop behind a stopped car.")]
    public float minCapMph = 12f;
    [Tooltip("Least distance ahead (m) scanned for cars. The planner scans further at speed — far enough to stop gently for a stopped car.")]
    public float avoidScanRange = 26f;
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

    [Header("Seeing and avoiding the cars around")]
    [Tooltip("Station keeping, the never-hit safety law, lane widths and the swerve-alongside rules. Defaults are the values the pace-lap tests (PackAvoidanceTests) were run with.")]
    public PackSettings pack = new PackSettings();
    [Tooltip("Braking authority (mph/sec) granted when the safety law has to act — overrides the (possibly weak) decel curve so an emergency slow actually lands. Also what every AI car is assumed able to brake at, so it must be the same for the whole field.")]
    public float avoidHardDecelMphPerSec = 30f;
    [Tooltip("The human driver is assumed able to stop this many times harder than an AI car (a spin, a wall, a stamp on the brakes), so the AI leaves them more room.")]
    public float humanBrakeFactor = 1.5f;
    [Tooltip("A free car further than this (m) across from the pit centreline is on the track, not in the pit lane.")]
    public float pitLaneHalfWidth = 7f;

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
    readonly PackPlanner _planner = new PackPlanner();
    readonly List<PackCar> _cars = new List<PackCar>();
    float _lateral;     // current applied tacticalLateralOffset (slew-limited)
    float _weaveEnv;    // 0..1 envelope so the weave ramps in gently
    bool _wasPit;
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
        OnPhaseChanged(RaceStart.Current);
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (_spline != null && s_bySpline.TryGetValue(_spline, out var fc) && fc == this) s_bySpline.Remove(_spline);
        RaceStart.PhaseChanged -= OnPhaseChanged;
        HasPlan = false;
    }

    // The dynamic bicycle model + pure-pursuit steering is twitchy at parade speeds and spins cars off the
    // line. For the formation lap we drive KINEMATICALLY instead — SplineDriver glues the car to the racing
    // line (exactly like the safety car). At green we hand back to the dynamic model for racing, re-seeding it
    // with the car's current pose + speed for a smooth rolling start.
    void OnPhaseChanged(RaceStart.Phase phase)
    {
        // Lazy-fetch: GridSpawner may add this component before the dynamic-model components exist.
        if (_pvc == null) _pvc = GetComponent<PlayerVehicleController>();
        if (_input == null) _input = GetComponent<SplineInputDriver>();

        // The collider can be sized after this component was added, so read it again now.
        ResolveExtents(gameObject, out _halfLength, out _halfWidth, refresh: true);

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
            _planner.Reset(); // no brake-rate history or swerve latch carried into the green
            HasPlan = false;
            if (_spline != null)
            {
                _spline.tacticalLateralOffset = 0f;
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
        if (RaceStart.Current != RaceStart.Phase.Formation) return;

        float dt = Time.fixedDeltaTime;
        _planner.Settings = pack;
        float cruise = FormationDirector.Instance != null ? FormationDirector.Instance.cruiseMph : cruiseMph;
        bool closingUp = FormationDirector.Instance != null && FormationDirector.Instance.FieldClosingUp;

        // Filing out of the pit lane: pull off the parked box strip onto the pit centreline once rolling, and keep
        // station off whatever is in front down the lane — including the cars that have already rejoined the
        // track just past the exit, which is exactly where the queue behind them used to pile in. Only the
        // formation pull-out eases the box lateral here: practice stints manage their own box-lane lateral in
        // both directions (PracticeAIStint).
        if (_spline.usePitLane)
        {
            _wasPit = true;
            DbgOnPit = true;
            DbgSettling = false;
            HasPlan = false;
            if (_spline.CurrentMph > 3f)
                _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, 0f, pitPullOutLateralRate * dt);
            if (_spline.IsOnPit) PitLaneStep(dt, cruise);
            return;
        }
        DbgOnPit = false;
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
        // it), otherwise free to close the train up on an open straight and held to cruise in a corner or the
        // close-up bunch.
        float baseCap = settling ? pitOutMph : ((corner || closingUp) ? cruise : cruise + catchUpBonusMph);

        // Lateral intent: the two-wide column the WHOLE lap (double file), with a gentle tyre-warming weave on early
        // straights that fades out near the line so the rows sit steady for the start. Through turns the column
        // eases toward centre (cornerColumnScale) but never collapses to single file.
        bool weaveOk = !settling && !corner && !closingUp && !_planner.Swerving;
        _weaveEnv = FormationLanes.StepEnvelope(_weaveEnv, weaveOk, dt, weaveRampSeconds, weaveFadeOutSeconds);
        int slot = _spline.qualifyingPosition;
        float column = FormationLanes.Column(slot, columnHalfOffset, corner, cornerColumnScale);
        float weave = FormationLanes.Weave(Time.time, slot, weaveAmplitude, weaveHz, weavePhasePerSlot, _weaveEnv);

        float len = _spline.TrackLength;
        float scanAhead = _planner.ScanAheadM(_spline.CurrentMph, Mathf.Max(avoidScanRange, paceCarGap + 12f));
        GatherTrack(len, scanAhead, _planner.ScanBehindM);

        float merge = _spline.MergeLateralBias;
        var self = SelfState(merge);
        _spline.GetLateralBounds(out self.TrackLo, out self.TrackHi);
        var intent = new PackIntent
        {
            BaseCapMph = baseCap,
            MaxCapMph = cruise + catchUpBonusMph,
            FloorMph = minCapMph,
            WantGap = closingUp ? rowGap : targetGap,
            PaceCarGap = paceCarGap,
            CruiseMph = cruise,
            ColumnTactical = column + weave,
            ColumnSlew = weaveSlewPerSec,
            // Never slip sideways mid-merge — that lateral snap is exactly what the settle exists to prevent.
            AllowSwerve = !settling,
        };
        var cmd = _planner.Step(self, intent, _cars, dt);
        Apply(cmd, dt);

        // Where I'm heading across the track, for the cars around me — without the merge bias, which is easing out.
        PlannedTrackLateral = _planner.PlannedLateral(self.Lateral - self.Tactical, self.Lateral) - merge;
        HasPlan = true;
        DbgSettling = settling;
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

    void Apply(PackCommand cmd, float dt)
    {
        _spline.aiMaxSpeedMph = cmd.CapMph;
        _spline.aiMinDecelMphPerSec = cmd.MinDecelMphPerSec; // brake authority only while the safety law acts
        _spline.paceMultiplier = formationPace;
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
            IsPaceCar = d.qualifyingPosition == FormationOrder.SafetyCarGrid,
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
