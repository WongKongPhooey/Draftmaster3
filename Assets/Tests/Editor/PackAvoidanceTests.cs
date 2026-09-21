using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The pace-lap field kept crashing into itself, and the only place that could be seen was Play Mode — which
// does not tick in an unfocused editor. So the brain every formation car runs (PackPlanner) is driven here
// through a small kinematic world instead: a straight road, cars that accelerate and brake the way
// SplineDriver moves a kinematic car (decel curve, plus whatever brake authority the planner demands), one
// frame of latency between seeing and acting, and a box-overlap check between every pair of cars every frame.
//
// A scenario passes only if no two boxes ever touch.
public class PackAvoidanceTests
{
    const float Dt = 0.02f;
    const float HalfLength = 2.4f;          // GridSpawner.collisionHalfExtents
    const float HalfWidth = 1f;
    const float CurveDecelMphPerSec = 22.4f; // Cup24 decel curve, ~10 m/s² at formation speeds
    const float EmergencyMphPerSec = 30f;    // FormationController.avoidHardDecelMphPerSec
    const float AccelMphPerSec = 8f;
    const float Cruise = 60f;
    const float CatchUp = 9f;
    const float Column = 2.2f;               // FormationController.columnHalfOffset
    const float RowGap = 15f;

    sealed class Car
    {
        public int Id;
        public float Dist;
        public float Lat;
        public float Tactical;
        public float Mph;
        public float Column;
        public int Slot = -1;            // >= 0: the lateral intent is the formation's column + weave for this slot
        public float WeaveEnv;
        public float HalfLength = PackAvoidanceTests.HalfLength;
        public float HalfWidth = PackAvoidanceTests.HalfWidth;
        public PackPlanner Planner;      // null = scripted
        public PackCommand Cmd;
        public PackCommand Pending;
        public bool HasCmd;
        public bool PaceCar;

        public float ScriptMph;
        public float ScriptDecel = EmergencyMphPerSec;
        public float ScriptLatTarget;
        public float ScriptLatSlew;

        public int SwerveCount;
        public bool WasSwerving;
        public bool EverBraked;
    }

    sealed class World
    {
        public readonly List<Car> Cars = new List<Car>();
        public float TrackLo = -7f;
        public float TrackHi = 7f;
        public float Time;
        public bool AllowSwerve = true;
        public bool Corner;              // formation cars pull their columns in and stop weaving
        public bool ClosingUp;           // formation cars pack into rows and stop weaving
        public string FirstContact;
        // Set these to log one car's view every half second (and every swerve change) while tuning a scenario.
        public System.Text.StringBuilder Trace;
        public int TraceId = -1;
        readonly List<PackCar> _buf = new List<PackCar>();
        int _nextId = 1;

        public Car AddAI(float dist, float column, float mph)
        {
            var c = new Car { Id = _nextId++, Dist = dist, Lat = column, Tactical = column, Column = column, Mph = mph,
                              Planner = new PackPlanner() };
            Cars.Add(c);
            return c;
        }

        public Car AddScripted(float dist, float lat, float mph, bool paceCar = false)
        {
            var c = new Car { Id = _nextId++, Dist = dist, Lat = lat, Mph = mph, ScriptMph = mph,
                              ScriptLatTarget = lat, PaceCar = paceCar };
            Cars.Add(c);
            return c;
        }

        public void Run(float seconds, System.Action<World> each = null)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                each?.Invoke(this);
                Step();
            }
        }

        void Step()
        {
            // 1. Every AI plans off the same snapshot.
            foreach (var me in Cars)
            {
                if (me.Planner == null) continue;
                _buf.Clear();
                float ahead = me.Planner.ScanAheadM(me.Mph, 38f);
                float behind = me.Planner.ScanBehindM;
                foreach (var o in Cars)
                {
                    if (o == me) continue;
                    float gap = o.Dist - me.Dist;
                    if (gap > ahead || gap < -behind) continue;
                    _buf.Add(new PackCar
                    {
                        Id = o.Id,
                        Gap = gap,
                        Lateral = o.Lat,
                        PlannedLateral = o.Planner != null
                            ? o.Planner.PlannedLateral(o.Lat - o.Tactical, o.Lat)
                            : o.ScriptLatTarget,
                        SpeedMph = o.Mph,
                        HalfLength = o.HalfLength,
                        HalfWidth = o.HalfWidth,
                        BrakeMphPerSec = o.Planner != null ? EmergencyMphPerSec : o.ScriptDecel,
                        IsPaceCar = o.PaceCar,
                    });
                }
                var self = new PackSelf
                {
                    SpeedMph = me.Mph, Lateral = me.Lat, Tactical = me.Tactical,
                    HalfLength = HalfLength, HalfWidth = HalfWidth,
                    BrakeMphPerSec = Mathf.Max(CurveDecelMphPerSec, EmergencyMphPerSec),
                    TrackLo = TrackLo, TrackHi = TrackHi,
                };
                float column = me.Column;
                if (me.Slot >= 0)
                {
                    // The same lateral intent FormationController builds.
                    bool weaveOk = !Corner && !ClosingUp && !me.Planner.Swerving;
                    me.WeaveEnv = FormationLanes.StepEnvelope(me.WeaveEnv, weaveOk, Dt, 3f, 0.75f);
                    column = FormationLanes.Column(me.Slot, Column, Corner, 0.6f)
                             + FormationLanes.Weave(Time, me.Slot, 0.45f, 0.18f, 0.8f, me.WeaveEnv);
                }
                bool calm = Corner || ClosingUp;
                var intent = new PackIntent
                {
                    BaseCapMph = calm ? Cruise : Cruise + CatchUp, MaxCapMph = Cruise + CatchUp, FloorMph = 12f,
                    WantGap = ClosingUp ? 8f : 9f, PaceCarGap = 26f, CruiseMph = Cruise,
                    ColumnTactical = column, ColumnSlew = 1.5f, AllowSwerve = AllowSwerve,
                };
                me.Pending = me.Planner.Step(self, intent, _buf, Dt);

                if (Trace != null && (me.Id == TraceId) && (me.Planner.Swerving != me.WasSwerving || Mathf.RoundToInt(Time / Dt) % 10 == 0))
                    Trace.AppendLine($"t={Time:0.00} id={me.Id} d={me.Dist:0.0} lat={me.Lat:0.00} v={me.Mph:0.0} mode={me.Pending.Mode} cap={me.Pending.CapMph:0.0} swerving={me.Planner.Swerving} foe={me.Planner.SwerveTargetId} side={me.Planner.SwerveSide} tgt={me.Pending.TacticalTarget:0.00} lead={me.Pending.HasLeader} clr={me.Pending.Clearance:0.0}");
                if (me.Planner.Swerving && !me.WasSwerving) me.SwerveCount++;
                me.WasSwerving = me.Planner.Swerving;
                if (me.Pending.MinDecelMphPerSec > 0f) me.EverBraked = true;
            }

            // 2. Move. An AI acts on LAST frame's command: one frame of latency, the worst the update order gives.
            foreach (var c in Cars)
            {
                if (c.Planner != null)
                {
                    if (c.HasCmd)
                    {
                        float target = c.Cmd.CapMph;
                        if (c.Mph < target) c.Mph = Mathf.Min(target, c.Mph + AccelMphPerSec * Dt);
                        else
                        {
                            float decel = CurveDecelMphPerSec;
                            if (c.Cmd.MinDecelMphPerSec > 0f) decel = Mathf.Max(decel, c.Cmd.MinDecelMphPerSec);
                            c.Mph = Mathf.Max(target, c.Mph - decel * Dt);
                        }
                        c.Tactical = Mathf.MoveTowards(c.Tactical, c.Cmd.TacticalTarget, c.Cmd.Slew * Dt);
                    }
                    c.Cmd = c.Pending;
                    c.HasCmd = true;
                    c.Lat = Mathf.Clamp(c.Tactical, TrackLo, TrackHi); // racing line at 0 on this straight
                }
                else
                {
                    if (c.Mph > c.ScriptMph) c.Mph = Mathf.Max(c.ScriptMph, c.Mph - c.ScriptDecel * Dt);
                    else c.Mph = Mathf.Min(c.ScriptMph, c.Mph + AccelMphPerSec * Dt);
                    c.Lat = Mathf.MoveTowards(c.Lat, c.ScriptLatTarget, c.ScriptLatSlew * Dt);
                }
                c.Dist += c.Mph * PackAvoidance.MphToMps * Dt;
            }
            Time += Dt;

            // 3. Contact.
            if (FirstContact != null) return;
            for (int i = 0; i < Cars.Count; i++)
            for (int j = i + 1; j < Cars.Count; j++)
            {
                var a = Cars[i];
                var b = Cars[j];
                if (Mathf.Abs(a.Dist - b.Dist) < a.HalfLength + b.HalfLength &&
                    Mathf.Abs(a.Lat - b.Lat) < a.HalfWidth + b.HalfWidth)
                {
                    FirstContact = $"t={Time:0.00}s car {a.Id} ({a.Dist:0.0}m, lat {a.Lat:0.00}, {a.Mph:0.0}mph) " +
                                   $"touched car {b.Id} ({b.Dist:0.0}m, lat {b.Lat:0.00}, {b.Mph:0.0}mph)";
                    return;
                }
            }
        }
    }

    static List<Car> TwoWideTrain(World w, float frontDist, int rows)
    {
        var list = new List<Car>();
        for (int r = 0; r < rows; r++)
        {
            list.Add(w.AddAI(frontDist - r * RowGap, -Column, Cruise));
            list.Add(w.AddAI(frontDist - r * RowGap - 0.5f, Column, Cruise));
        }
        return list;
    }

    // A grid in formation order: slot 0 on the left of the front row, 1 on the right, and so on.
    static List<Car> FormationField(World w, float frontDist, int cars)
    {
        var list = new List<Car>();
        for (int slot = 0; slot < cars; slot++)
        {
            float column = FormationLanes.Column(slot, Column, false, 1f);
            var c = w.AddAI(frontDist - (slot / 2) * RowGap - (slot % 2) * 0.5f, column, Cruise);
            c.Slot = slot;
            list.Add(c);
        }
        return list;
    }

    // ---------------------------------------------------------------- the maths

    [Test]
    public void APairFittedIntoTheRoadKeepsItsSpacing()
    {
        // The racing line hugs the right edge at an apex. Offsetting each car from the line on its own put the
        // right-hand car past the edge, SplineDriver clamped it back, and it landed on its partner.
        const float lo = -4f, hi = 4f, line = 3.5f, column = 1.76f;
        float right = line + FormationLanes.FitColumn(column, line, lo, hi);
        float left = line + FormationLanes.FitColumn(-column, line, lo, hi);
        Assert.AreEqual(2f * column, right - left, 1e-4f, "the pair lost its spacing");
        Assert.LessOrEqual(right, hi + 1e-4f);
        Assert.GreaterOrEqual(left, lo - 1e-4f);

        // Plenty of road: the columns sit on the line exactly as before.
        Assert.AreEqual(column, FormationLanes.FitColumn(column, 0f, lo, hi), 1e-4f);
        // No bounds known (pit lane): untouched.
        Assert.AreEqual(column, FormationLanes.FitColumn(column, 3.9f, float.NegativeInfinity, float.PositiveInfinity), 1e-4f);
    }

    [Test]
    public void SafeSpeedAndSafeClearanceAreInverses()
    {
        const float b = 13.4f;
        foreach (float v in new[] { 5f, 20f, 27f, 40f })
        foreach (float vL in new[] { 0f, 10f, 27f })
        {
            float clear = PackAvoidance.SafeClearance(v, vL, b, b, 0.12f);
            if (clear <= 0f) continue;
            Assert.AreEqual(v, PackAvoidance.SafeSpeedMps(clear, vL, b, b, 0.12f), 0.01f, $"v={v} vL={vL}");
        }
    }

    [Test]
    public void BehindAStoppedCarTheSafeSpeedIsTheOneYouCanStopFrom()
    {
        // 60 mph, 13.4 m/s², 0.12 s: 3.2 m of reaction plus 26.8 m of braking.
        float v = 60f * PackAvoidance.MphToMps;
        float stop = PackAvoidance.StoppingDistance(v, 13.4f, 0.12f);
        Assert.AreEqual(30.0f, stop, 0.2f);
        Assert.AreEqual(v, PackAvoidance.SafeSpeedMps(stop, 0f, 13.4f, 13.4f, 0.12f), 0.01f);
        Assert.AreEqual(0f, PackAvoidance.SafeSpeedMps(-1f, 0f, 13.4f, 13.4f, 0.12f));
    }

    [Test]
    public void TwoCarsAtTheSamePaceOnlyNeedTheReactionGap()
    {
        float v = 27f;
        Assert.AreEqual(v * 0.12f, PackAvoidance.SafeClearance(v, v, 13.4f, 13.4f, 0.12f), 1e-3f);
        // A car ahead that can stop harder than I can needs more room.
        Assert.Greater(PackAvoidance.SafeClearance(v, v, 13.4f, 20f, 0.12f), v * 0.12f + 5f);
    }

    [Test]
    public void GapsWrapRoundTheLap()
    {
        Assert.AreEqual(10f, PackAvoidance.SignedGap(5f, 995f, 1000f), 1e-3f);
        Assert.AreEqual(-10f, PackAvoidance.SignedGap(995f, 5f, 1000f), 1e-3f);
        Assert.AreEqual(30f, PackAvoidance.SignedGap(130f, 100f, 1000f), 1e-3f);
    }

    [Test]
    public void ACarSatSidewaysBlocksTheRoad()
    {
        PackAvoidance.Footprint(2.4f, 1f, 90f, out float along, out float across);
        Assert.AreEqual(1f, along, 1e-3f);
        Assert.AreEqual(2.4f, across, 1e-3f);
        PackAvoidance.Footprint(2.4f, 1f, 0f, out along, out across);
        Assert.AreEqual(2.4f, along, 1e-3f);
        Assert.AreEqual(1f, across, 1e-3f);
    }

    [Test]
    public void ALaneWithACarAlongsideIsBlockedAndAnEmptyOneIsNot()
    {
        var planner = new PackPlanner();
        var me = new PackSelf { SpeedMph = 60f, Lateral = 0f, HalfLength = HalfLength, HalfWidth = HalfWidth,
                                TrackLo = -7f, TrackHi = 7f };
        var cars = new List<PackCar>
        {
            new PackCar { Id = 1, Gap = 1f, Lateral = 3f, PlannedLateral = 3f, SpeedMph = 60f,
                          HalfLength = HalfLength, HalfWidth = HalfWidth },
        };
        Assert.IsTrue(planner.LaneBlocked(me, 2.6f, cars, PackPlanner.NoCar), "a car alongside on the right");
        Assert.IsFalse(planner.LaneBlocked(me, -2.6f, cars, PackPlanner.NoCar), "nothing on the left");
        // Far enough back and no quicker than me: not in the way.
        cars[0] = new PackCar { Id = 1, Gap = -20f, Lateral = 3f, PlannedLateral = 3f, SpeedMph = 60f,
                                HalfLength = HalfLength, HalfWidth = HalfWidth };
        Assert.IsFalse(planner.LaneBlocked(me, 2.6f, cars, PackPlanner.NoCar));
        // ...unless it's coming up fast.
        cars[0] = new PackCar { Id = 1, Gap = -20f, Lateral = 3f, PlannedLateral = 3f, SpeedMph = 95f,
                                HalfLength = HalfLength, HalfWidth = HalfWidth };
        Assert.IsTrue(planner.LaneBlocked(me, 2.6f, cars, PackPlanner.NoCar));
    }

    // ---------------------------------------------------------------- the field

    [Test]
    public void AHealthyTwoWideTrainKeepsStationWithoutEverEmergencyBraking()
    {
        var w = new World();
        var pace = w.AddScripted(26f + 2f * HalfLength + 400f, 0f, Cruise, paceCar: true);
        pace.ScriptDecel = 10f;
        var field = TwoWideTrain(w, 400f, 12);

        w.Run(30f, world =>
        {
            // The pace car eases off for a corner and picks up again.
            pace.ScriptMph = world.Time > 5f && world.Time < 10f ? 50f : Cruise;
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
        foreach (var c in field)
        {
            Assert.AreEqual(0, c.SwerveCount, $"car {c.Id} swerved in a healthy train");
            Assert.IsFalse(c.EverBraked, $"car {c.Id} needed the emergency brake in a healthy train");
        }
    }

    [Test]
    public void TheFrontRowStoppingDeadDoesNotPileUpTheTrain()
    {
        var w = new World();
        var left = w.AddScripted(400f, -Column, Cruise);
        var right = w.AddScripted(399.5f, Column, Cruise);
        TwoWideTrain(w, 400f - RowGap, 10);

        w.Run(40f, world =>
        {
            if (world.Time > 5f) { left.ScriptMph = 0f; right.ScriptMph = 0f; }
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
    }

    [Test]
    public void ASingleFileTrainDrivesRoundAStoppedCarAndBackOntoItsLine()
    {
        var w = new World();
        var wreck = w.AddScripted(400f, 0f, 0f);
        var field = new List<Car>();
        for (int i = 0; i < 8; i++) field.Add(w.AddAI(200f - i * RowGap, 0f, Cruise));

        w.Run(60f);

        Assert.IsNull(w.FirstContact, w.FirstContact);
        foreach (var c in field)
        {
            Assert.Greater(c.Dist, wreck.Dist + 20f, $"car {c.Id} never got past the wreck");
            Assert.Less(Mathf.Abs(c.Lat), 0.3f, $"car {c.Id} did not settle back onto its line");
            Assert.LessOrEqual(c.SwerveCount, 2, $"car {c.Id} dithered in and out of the swerve");
        }
    }

    [Test]
    public void AStoppedCarInOneColumnIsPassedWithoutHittingTheOtherColumn()
    {
        var w = new World();
        w.AddScripted(400f, -Column, 0f);
        TwoWideTrain(w, 300f, 8);

        w.Run(60f);

        Assert.IsNull(w.FirstContact, w.FirstContact);
    }

    [Test]
    public void WithNowhereToGoTheCarStopsShortInsteadOfHittingIt()
    {
        var w = new World { TrackLo = -1.5f, TrackHi = 1.5f };
        var wreck = w.AddScripted(300f, 0f, 0f);
        var car = w.AddAI(200f, 0f, Cruise);

        w.Run(20f);

        Assert.IsNull(w.FirstContact, w.FirstContact);
        Assert.AreEqual(0, car.SwerveCount);
        Assert.Less(car.Mph, 0.5f);
        float clear = wreck.Dist - car.Dist - 2f * HalfLength;
        Assert.Greater(clear, 0.5f);
        Assert.Less(clear, 12f, "it should roll up behind the wreck, not stop miles short");
    }

    [Test]
    public void ACarBoxedInBySideTrafficBrakesRatherThanSwervingIntoIt()
    {
        var w = new World();
        // A stopped car ahead in the middle lane, and a lane of traffic either side matching my pace.
        w.AddScripted(250f, 0f, 0f);
        var me = w.AddAI(150f, 0f, 40f);
        var leftEscort = w.AddScripted(150.5f, -2.6f, 40f);
        var rightEscort = w.AddScripted(149.5f, 2.6f, 40f);
        leftEscort.ScriptDecel = rightEscort.ScriptDecel = 22f;

        w.Run(15f, world =>
        {
            // The escorts stay alongside me the whole way, so neither side ever opens.
            leftEscort.ScriptMph = me.Mph;
            rightEscort.ScriptMph = me.Mph;
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
        Assert.AreEqual(0, me.SwerveCount);
        Assert.Less(me.Mph, 0.5f);
    }

    [Test]
    public void AFastCarJoiningBehindASlowTrainBrakesInTime()
    {
        foreach (float gap in new[] { 12f, 25f })
        {
            var w = new World { AllowSwerve = false }; // pit-out settle: no swerving
            w.AddScripted(100f, 0f, 25f);
            w.AddAI(100f - gap, 0f, 50f);
            w.Run(15f);
            Assert.IsNull(w.FirstContact, $"gap {gap}: {w.FirstContact}");
        }
    }

    [Test]
    public void ACarCuttingAcrossInFrontIsSeenBeforeItArrives()
    {
        var w = new World();
        var field = new List<Car>();
        for (int i = 0; i < 5; i++) field.Add(w.AddAI(300f - i * RowGap, 0f, Cruise));
        // Level-ish with the third car, one lane over, then it cuts across and brakes.
        var intruder = w.AddScripted(300f - 2f * RowGap + 8f, 4.4f, Cruise);
        intruder.ScriptLatSlew = 2f;
        intruder.ScriptDecel = 20f;

        w.Run(25f, world =>
        {
            if (world.Time > 3f) { intruder.ScriptLatTarget = 0f; intruder.ScriptMph = 45f; }
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
    }

    [Test]
    public void APileUpOfStoppedCarsIsEventuallyDrivenRound()
    {
        var w = new World();
        var a = w.AddScripted(400f, 0f, 0f);
        w.AddScripted(400f - 2f * HalfLength - 1f, 0f, 0f); // nose to tail with the first
        var field = new List<Car>();
        for (int i = 0; i < 4; i++) field.Add(w.AddAI(250f - i * RowGap, 0f, Cruise));

        w.Run(80f);

        Assert.IsNull(w.FirstContact, w.FirstContact);
        foreach (var c in field) Assert.Greater(c.Dist, a.Dist + 20f, $"car {c.Id} is still waiting");
    }

    [Test]
    public void ACarInTheQueueWaitsForTheCarInFrontToGoRoundFirst()
    {
        // Both already sat behind the wreck, nose to tail. The one at the front goes round; the one behind it
        // must not pull out while the front one is still in the lane.
        var w = new World();
        var wreck = w.AddScripted(400f, 0f, 0f);
        var first = w.AddAI(wreck.Dist - 2f * HalfLength - 5f, 0f, 0f);
        var second = w.AddAI(first.Dist - 2f * HalfLength - 5f, 0f, 0f);

        bool secondLeftEarly = false;
        w.Run(30f, world =>
        {
            if (Mathf.Abs(first.Lat) < 0.5f && first.Dist < wreck.Dist && second.Planner.Swerving)
                secondLeftEarly = true;
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
        Assert.IsFalse(secondLeftEarly, "the second car filed past the queue instead of waiting its turn");
        Assert.Greater(first.Dist, wreck.Dist + 20f, "the front car never went round");
        Assert.Greater(second.Dist, wreck.Dist + 20f, "the second car never went round");
    }

    [Test]
    public void TheContactCheckCatchesARearEnd()
    {
        // The control: a car that isn't looking drives straight into a stopped one, and the world notices.
        var w = new World();
        w.AddScripted(100f, 0f, 0f);
        w.AddScripted(50f, 0.5f, 40f);
        w.Run(10f);
        Assert.IsNotNull(w.FirstContact);
    }

    [Test]
    public void AFullFieldFormationLapWithCornersAndWeaveStaysClean()
    {
        // 43 cars two-wide behind the pace car. Every 20 seconds a turn: the pace car slows for it, the columns
        // pull in and the weave stops. The last stretch is the close-up to the green.
        var w = new World();
        var pace = w.AddScripted(600f + 26f + 2f * HalfLength, 0f, Cruise, paceCar: true);
        pace.ScriptDecel = 10f;
        var field = FormationField(w, 600f, 43);

        w.Run(120f, world =>
        {
            float cycle = world.Time % 20f;
            world.Corner = world.Time < 100f && cycle > 12f && cycle < 18f;
            world.ClosingUp = world.Time >= 100f;
            pace.ScriptMph = world.Corner ? 45f : Cruise;
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
        foreach (var c in field)
        {
            Assert.AreEqual(0, c.SwerveCount, $"car {c.Id} swerved on a clean formation lap");
            Assert.IsFalse(c.EverBraked, $"car {c.Id} needed the emergency brake on a clean formation lap");
        }
    }

    [Test]
    public void ErraticLeadersNeverGetHitFromBehind()
    {
        // Both column leaders do whatever they like with the throttle and brakes (never harder than an AI can
        // brake) for two minutes. Nobody behind them may touch anybody.
        var w = new World();
        var left = w.AddScripted(400f, -Column, Cruise);
        var right = w.AddScripted(399.5f, Column, Cruise);
        TwoWideTrain(w, 400f - RowGap, 10);
        var rng = new System.Random(20260916);
        float nextLeft = 0f, nextRight = 0f;

        w.Run(120f, world =>
        {
            if (world.Time >= nextLeft)
            {
                left.ScriptMph = (float)rng.NextDouble() * 70f;
                nextLeft = world.Time + 1f + (float)rng.NextDouble() * 2f;
            }
            if (world.Time >= nextRight)
            {
                right.ScriptMph = (float)rng.NextDouble() * 70f;
                nextRight = world.Time + 1f + (float)rng.NextDouble() * 2f;
            }
        });

        Assert.IsNull(w.FirstContact, w.FirstContact);
    }

    [Test]
    public void TheHumanIsGivenRoomToStopHarderThanAnAICan()
    {
        // The player can stamp on the brakes harder than the AI assume of each other; the AI are told so
        // (FormationController.humanBrakeFactor) and must still not hit them.
        var w = new World();
        var human = w.AddScripted(300f, 0f, Cruise);
        human.ScriptDecel = EmergencyMphPerSec * 1.5f;
        for (int i = 0; i < 5; i++) w.AddAI(300f - (i + 1) * RowGap, 0f, Cruise);

        w.Run(30f, world => { if (world.Time > 8f) human.ScriptMph = 0f; });

        Assert.IsNull(w.FirstContact, w.FirstContact);
    }

    [Test]
    public void ACarSpunAcrossBothColumnsIsAvoided()
    {
        // Sat sideways across the middle of the road: 4.8 m of car across, 2 m along.
        var w = new World();
        var spun = w.AddScripted(500f, -0.5f, 0f);
        spun.HalfLength = HalfWidth;
        spun.HalfWidth = HalfLength;
        TwoWideTrain(w, 350f, 8);

        w.Run(60f);

        Assert.IsNull(w.FirstContact, w.FirstContact);
    }

    [Test]
    public void ZZTraceErratic()
    {
        var w = new World();
        var left = w.AddScripted(400f, -Column, Cruise);
        var right = w.AddScripted(399.5f, Column, Cruise);
        TwoWideTrain(w, 400f - RowGap, 10);
        var rng = new System.Random(20260916);
        float nextLeft = 0f, nextRight = 0f;
        var sb = new System.Text.StringBuilder();
        w.Trace = sb;
        w.TraceId = 4;
        w.Run(6.4f, world =>
        {
            if (world.Time >= nextLeft)
            {
                left.ScriptMph = (float)rng.NextDouble() * 70f;
                nextLeft = world.Time + 1f + (float)rng.NextDouble() * 2f;
            }
            if (world.Time >= nextRight)
            {
                right.ScriptMph = (float)rng.NextDouble() * 70f;
                nextRight = world.Time + 1f + (float)rng.NextDouble() * 2f;
            }
            if (Mathf.RoundToInt(world.Time / Dt) % 10 == 0)
                sb.AppendLine($"   t={world.Time:0.00} L d={left.Dist:0.0} v={left.Mph:0.0}->{left.ScriptMph:0.0}  R d={right.Dist:0.0} v={right.Mph:0.0}->{right.ScriptMph:0.0}");
        });
        sb.AppendLine(w.FirstContact ?? "no contact");
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/pack_trace.txt"), sb.ToString());
    }

    [Test]
    public void ZZStressErratic()
    {
        var sb = new System.Text.StringBuilder();
        int fails = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var w = new World();
            var left = w.AddScripted(400f, -Column, Cruise);
            var right = w.AddScripted(399.5f, Column, Cruise);
            TwoWideTrain(w, 400f - RowGap, 10);
            var rng = new System.Random(seed);
            float nextLeft = 0f, nextRight = 0f;
            w.Run(120f, world =>
            {
                if (world.Time >= nextLeft) { left.ScriptMph = (float)rng.NextDouble() * 70f; nextLeft = world.Time + 1f + (float)rng.NextDouble() * 2f; }
                if (world.Time >= nextRight) { right.ScriptMph = (float)rng.NextDouble() * 70f; nextRight = world.Time + 1f + (float)rng.NextDouble() * 2f; }
            });
            if (w.FirstContact != null) { fails++; sb.AppendLine($"seed {seed}: {w.FirstContact}"); }
        }
        // Full formation with random corner timing and pace-car dips.
        for (int seed = 1; seed <= 20; seed++)
        {
            var w = new World();
            var pace = w.AddScripted(600f + 26f + 2f * HalfLength, 0f, Cruise, paceCar: true);
            pace.ScriptDecel = 10f + seed % 3 * 5f;
            var field = FormationField(w, 600f, 43);
            var rng = new System.Random(seed);
            float next = 0f; bool corner = false;
            w.Run(150f, world =>
            {
                if (world.Time >= next) { corner = !corner; next = world.Time + 2f + (float)rng.NextDouble() * 8f; pace.ScriptMph = corner ? 30f + (float)rng.NextDouble() * 25f : Cruise; }
                world.Corner = corner && world.Time < 130f;
                world.ClosingUp = world.Time >= 130f;
                if (world.ClosingUp) pace.ScriptMph = Cruise;
            });
            int swerves = 0, brakes = 0;
            foreach (var c in field) { swerves += c.SwerveCount; if (c.EverBraked) brakes++; }
            if (w.FirstContact != null || swerves > 0 || brakes > 0) { fails++; sb.AppendLine($"formation seed {seed}: {w.FirstContact ?? "clean"} swerves={swerves} braked={brakes}"); }
        }
        sb.AppendLine($"fails={fails}");
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/pack_stress.txt"), sb.ToString());
    }
}
