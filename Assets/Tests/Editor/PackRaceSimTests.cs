using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// A pack of AI cars racing each other on a real track, with no play mode.
//
// AILapSimTests drives one car; FormationLapSimTests runs the kinematic pace lap. Neither says anything about
// racing: whether a field spread by driver skill stays together, drafts, and passes. This builds a pack the
// way GridSpawner builds a dynamic-AI race (SplineDriver brain + PlayerVehicleController physics +
// SplineInputDriver + AIRacingBehaviour, a driver's ratings applied the way AIDriverBinding applies them) on a
// real track package, starts it rolling single file under green, and steps every car by hand. What comes out
// is how the field races: lap-time spread, the gaps cars run at, how long they spend in a tow, how many places
// change hands, and how often they touch or leave the road.
//
// Cars don't collide (no physics step runs in edit mode) — overlaps are counted as contact instead.
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class PackRaceSimTests
{
    const float HalfLength = 2.4f;
    const float HalfWidth = 1f;

    GameObject _package;
    readonly List<GameObject> _cars = new List<GameObject>();
    UnityEngine.Object _vehicleCopy;

    static Type Runtime(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        Assert.Fail($"{name} is missing from Assembly-CSharp.");
        return null;
    }

    [TearDown]
    public void Despawn()
    {
        foreach (var c in _cars)
        {
            if (c == null) continue;
            var s = c.GetComponent(Runtime("SplineDriver"));
            if (s != null) Call(s, "OnDisable");
            var p = c.GetComponent(Runtime("PlayerVehicleController"));
            if (p != null) Call(p, "OnDisable");
            UnityEngine.Object.DestroyImmediate(c);
        }
        _cars.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        if (_vehicleCopy != null) UnityEngine.Object.DestroyImmediate(_vehicleCopy);
        _vehicleCopy = null;
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "" });
    }

    public class Settings
    {
        public int cars = 12;
        public int laps = 6;
        public bool skillCornering = true;      // AIDriverBinding's cornerCommitment spread; false = everyone at 1
        public float? followHeadway;            // AIRacingBehaviour.followHeadwaySeconds override
        public float? draftMaxGap, draftMinSpeed, draftMaxBonus, draftTowAccel, draftTopSpeedGain;
        public int traceLines;                  // > 0: log that many close-following lines (see Run)
        public float stopCarAt = -1f;           // >= 0: car 0 stops dead once it reaches this distance on lap 2
        public bool respectYellows = true;
        public int traceYellowLines;            // > 0: log cars closing on the stopped car
        public Dictionary<string, object> input; // SplineInputDriver field overrides
        public int seed = 7;                     // roster shuffle and ratings
        public bool traceOffs;                   // log each off's last two seconds, step by step
        public float? draftScale, followScale;   // TrackConditions.DraftScale / AiFollowScale overrides
        public int columns = 1;                  // 2 = a double-file rolling start, as the race starts
        public Dictionary<string, object> racing; // AIRacingBehaviour field overrides
        public int traceCar = -1;                 // >= 0: log that car's controls every 0.2 s on its second lap
        public int traceHard;                     // > 0: log up to that many hard contacts, both cars' state
        public bool fastestAtBack;                // the quickest-rated car starts last; its charge is logged lap by lap
        public float? driverGripSpread;
        public float? soloStrength;
        public int traceDrops;
        public float initialWear;
        public int stepTraceCar = -1;             // >= 0: log this car every physics step from traceFrom for traceSeconds
        public float traceFrom = 60f, traceSeconds = 6f;                 // every tyre starts this worn (0..1)                    // > 0: log that many cars falling off the back of the car ahead (gap past 25 m)               // one car alone with this rating (all three stats), for clean-air pace           // AIRatings.DriverGrip spread override (0 = every driver the same grip)
        public bool traceCharger;                 // with fastestAtBack: log the charger against the car ahead once a second
        public float? sideAwareness;              // TrackConditions.AiSideAwareness override
        public float traceSlowMph;                // > 0: when a car first drops below this (after 25 s), log its last 2 s
        // Rolling start (startSeconds > 0): the field double file as the formation hands it over at the green - the
        // leader startLineMetres short of the start/finish line, rows rowGap apart, the two columns columnLateral either
        // side of the racing line as a tactical offset (FormationController -> AIRacingBehaviour.TakeLateral) - run for
        // startSeconds and measured from the first step, not from car 0's second lap.
        public float startSeconds;
        public float startLineMetres = 40f, rowGap = 15f, columnLateral = 2.2f, startMph = 55f;
    }

    static float Get(Component c, string prop) => (float)c.GetType().GetProperty(prop).GetValue(c);

    [Explicit("Diagnostic: why a quicker car behind doesn't pass — the chaser's state while close.")]
    [Test]
    public void WatkinsGlenTraceAttacks() => Log("trace", Run("WatkinsGlen", new Settings { laps = 3, traceLines = 150 }));

    public class Result
    {
        public readonly List<string> lines = new List<string>();
        public int passes;            // running-order swaps after the first lap
        public int contacts;
        public int offs;
        public float medianGap, towShare, under20Share;
        public float fastestLap = float.MaxValue, slowestBest;
        // Stopped-car runs: the others' speed over the stretch before where it stopped, with and without the
        // car there, and how they behaved passing it.
        public float stoppedAt = -1f, cleanMph, yellowMph;
        public int cleanSamples, yellowSamples, yellowPasses, stoppedHits;
        // Pack shape. Car-time with one / two or more cars overlapping it alongside (2-wide / 3-wide+), how far a
        // car with nobody near it strays from its own planned line, and how often a car's intended sideways
        // offset reverses (a weave) per car per lap.
        public float wide2Share, wide3Share, aloneLineRms, aloneLineMax, weavesPerCarLap;
        // Holding formation. 3-wide runs: how long a car stays in a row of three (s, mean), and the share of 3-wide time
        // spent in runs of 3 s or more. Lane hold: how long a car keeps to one lateral (within half a groove, 1.3 m)
        // before changing, changes per car per lap, and its wander round that lateral while holding it (rms, m).
        // Pack: the field's length leader to last (median, m) and the share of cars in the lead pack (consecutive
        // gaps under 25 m from the leader).
        public float wide3RunMean, wide3Sustained, laneHoldMean, laneChangesPerCarLap, laneJitterRms;
        public int laneChanges, weaveCount;
        public float fieldLengthMedian, leadPackShare;
        // Stuck: episodes of a car running within 25 m directly behind one whose best lap is 0.3 s+ slower, for 10 s
        // or more (count), and the longest such stretch (s). Charge: the quickest car's position at each of its laps.
        // Stability: rms body slip (deg) and steering reversals per car per second - a car that looks loose.
        public float slipRms, steerReversalsPerSec;
        public int stuckEpisodes;
        public float stuckLongest;
        public readonly List<int> charge = new List<int>();
        public int laps;
        // Throttle: share of car-time flat out (>= 0.98), lifts (flat -> under 0.9) per car per lap, share of
        // car-time on the brake, and the share of following gaps that are nose to tail (< 7 m centre to centre,
        // same lane) - bump-drafting distance.
        public float fullThrottleShare, liftsPerCarLap, brakeShare, noseToTailShare;
        public float minMph = float.MaxValue, meanMph;
        // Contacts split by kind: nose to tail (same lane) vs side to side, and hard ones - closing faster than
        // VehicleCollision's no-damage speed (3 m/s), the ones that bend metal and start wrecks.
        public int rearContacts, sideContacts, hardContacts;
        // Rolling start: distinct cars that fell below 30 mph (a stack-up), the spread of the field across the road
        // (rms lateral off the centreline, m), the share of braking car-time spent right of the centreline, and how far
        // the field got (mean and last car, m) in the time.
        public int slowCars;
        public float lateralRms, brakingRightShare, meanProgress, lastProgress;
    }

    [Explicit("Diagnostic: a pack of AI racing at Watkins Glen as the game now sets it up.")]
    [Test]
    public void WatkinsGlen() => Log("current", Run("WatkinsGlen", new Settings()));

    [Explicit("Diagnostic: step-by-step lead-up to each off in the pack, yaw damping on.")]
    [Test]
    public void WatkinsGlenTraceOffs() => Log("trace offs", Run("WatkinsGlen", new Settings { seed = 2, traceOffs = true }));

    [Explicit("Diagnostic: the pack with the steering's yaw damping off and on, across rosters.")]
    [TestCase(false, 1)] [TestCase(true, 1)]
    [TestCase(false, 2)] [TestCase(true, 2)]
    [TestCase(false, 3)] [TestCase(true, 3)]
    [TestCase(false, 7)] [TestCase(true, 7)]
    [TestCase(false, 11)] [TestCase(true, 11)]
    [TestCase(false, 12)] [TestCase(true, 12)]
    [TestCase(false, 13)] [TestCase(true, 13)]
    [TestCase(false, 14)] [TestCase(true, 14)]
    [TestCase(false, 15)] [TestCase(true, 15)]
    [TestCase(false, 16)] [TestCase(true, 16)]
    public void WatkinsGlenYawDamping(bool damping, int seed) =>
        Log($"yaw damping {(damping ? "on" : "off")} seed {seed}", Run("WatkinsGlen", new Settings
        {
            input = damping ? null : new Dictionary<string, object> { { "yawRateGain", 0f } },
            seed = seed,
        }));

    [Explicit("Diagnostic: the pack with the slip-limit countersteer and wide-of-line lift off and on, across rosters.")]
    [TestCase(false, 1)] [TestCase(true, 1)]
    [TestCase(false, 2)] [TestCase(true, 2)]
    [TestCase(false, 3)] [TestCase(true, 3)]
    [TestCase(false, 7)] [TestCase(true, 7)]
    [TestCase(false, 11)] [TestCase(true, 11)]
    [TestCase(false, 12)] [TestCase(true, 12)]
    [TestCase(false, 13)] [TestCase(true, 13)]
    [TestCase(false, 14)] [TestCase(true, 14)]
    [TestCase(false, 15)] [TestCase(true, 15)]
    [TestCase(false, 16)] [TestCase(true, 16)]
    public void WatkinsGlenSlipLimit(bool on, int seed) =>
        Log($"slip limit {(on ? "on" : "off")} seed {seed}", Run("WatkinsGlen", new Settings
        {
            input = on ? null : new Dictionary<string, object> { { "slipLimitGain", 0f }, { "wideLiftEndMetres", 0f } },
            seed = seed,
        }));

    [Explicit("Diagnostic: the pack with a candidate draft, before writing it to the vehicle asset.")]
    [TestCase(30f, 80f, 10f, 2.2f, 0.08f)]
    [TestCase(35f, 70f, 12f, 2.8f, 0.10f)]
    public void WatkinsGlenDraft(float gap, float minMph, float bonus, float towAccel, float topGain) =>
        Log($"draft {gap} m / {minMph} mph / +{bonus} mph / {towAccel} m/s2 / {topGain:P0}",
            Run("WatkinsGlen", new Settings { draftMaxGap = gap, draftMinSpeed = minMph, draftMaxBonus = bonus,
                                              draftTowAccel = towAccel, draftTopSpeedGain = topGain }));

    [Explicit("Diagnostic: the pack before and after — no cornering spread, old follow gap, old draft.")]
    [Test]
    public void WatkinsGlenCompare()
    {
        var sb = new StringBuilder();
        sb.Append(Summary("old: no skill cornering, 0.7 s headway, 18 m / 130 mph draft",
            Run("WatkinsGlen", new Settings { skillCornering = false, followHeadway = 0.7f, draftMaxGap = 18f, draftMinSpeed = 130f,
                                              draftMaxBonus = 7f, draftTowAccel = 1.6f, draftTopSpeedGain = 0.06f })));
        Despawn();
        sb.Append(Summary("new racecraft, old draft", Run("WatkinsGlen", new Settings { draftMaxGap = 18f,
                                              draftMinSpeed = 130f, draftMaxBonus = 7f, draftTowAccel = 1.6f, draftTopSpeedGain = 0.06f })));
        Despawn();
        sb.Append(Summary("current (Cup24 asset draft)", Run("WatkinsGlen", new Settings())));
        Debug.Log("[PackSim] compare\n" + sb);
    }

    [Explicit("Diagnostic: a car stops dead on the run to Turn 1 at Watkins Glen; how the field treats the yellow.")]
    [Test]
    public void WatkinsGlenYellow()
    {
        var r = Run("WatkinsGlen", new Settings { laps = 4, stopCarAt = 250f });
        Debug.Log($"[PackSim] yellow: car stopped at {r.stoppedAt:0} m. Field over the 130 m before it: " +
                  $"{r.cleanMph:0.0} mph clean ({r.cleanSamples} samples) vs {r.yellowMph:0.0} mph under yellow ({r.yellowSamples}); " +
                  $"{r.yellowPasses} passes under yellow, {r.stoppedHits} hits on the stopped car\n" + Summary("", r));
        Assert.GreaterOrEqual(r.stoppedAt, 0f, "the car never stopped");
        Assert.Greater(r.yellowSamples, 0, "nobody drove through the yellow zone");
        Assert.Less(r.yellowMph, r.cleanMph * 0.8f, "the field didn't lift through the yellow");
    }

    [Explicit("Diagnostic: the stopped-car run with yellows off, and a trace of cars closing on the stopped car.")]
    [Test]
    public void WatkinsGlenYellowCompare()
    {
        var off = Run("WatkinsGlen", new Settings { laps = 4, stopCarAt = 250f, respectYellows = false });
        string a = $"yellows OFF: {off.cleanMph:0.0} -> {off.yellowMph:0.0} mph, {off.stoppedHits} hits on the stopped car, {off.contacts} contacts";
        Despawn();
        var on = Run("WatkinsGlen", new Settings { laps = 4, stopCarAt = 250f, traceYellowLines = 120 });
        Debug.Log($"[PackSim] yellow compare\n{a}\nyellows ON: {on.cleanMph:0.0} -> {on.yellowMph:0.0} mph, {on.stoppedHits} hits on the stopped car, {on.contacts} contacts\n" +
                  string.Join("\n", on.lines));
    }

    static void Log(string title, Result r) => Debug.Log("[PackSim] " + Summary(title, r) + string.Join("\n", r.lines));

    static string Summary(string title, Result r) =>
        $"{title}: {r.passes} passes, {r.contacts} contacts, {r.offs} offs, median gap {r.medianGap:0.0} m, " +
        $"{r.under20Share:P0} of the time within 20 m, {r.towShare:P0} in a tow, best laps {r.fastestLap:0.00}..{r.slowestBest:0.00} s, " +
        $"2-wide {r.wide2Share:P1}, 3-wide+ {r.wide3Share:P1}, alone off-line rms {r.aloneLineRms:0.00} m (max {r.aloneLineMax:0.0}), " +
        $"{r.weavesPerCarLap:0.00} weaves/car/lap, flat out {r.fullThrottleShare:P1}, {r.liftsPerCarLap:0.00} lifts/car/lap, " +
        $"braking {r.brakeShare:P1}, nose-to-tail {r.noseToTailShare:P0}, speed {r.minMph:0}..mean {r.meanMph:0} mph, " +
        $"contacts rear {r.rearContacts} / side {r.sideContacts} / HARD {r.hardContacts}\n" +
        $"    holding: 3-wide runs {r.wide3RunMean:0.0} s ({r.wide3Sustained:P0} in runs 3 s+), lane hold {r.laneHoldMean:0.0} s, " +
        $"{r.laneChangesPerCarLap:0.00} lane changes/car/lap, wander {r.laneJitterRms:0.00} m, field {r.fieldLengthMedian:0} m long, " +
        $"{r.leadPackShare:P0} in the lead pack, slip rms {r.slipRms:0.0} deg, {r.steerReversalsPerSec:0.00} steer reversals/s, stuck 10 s+ {r.stuckEpisodes} (longest {r.stuckLongest:0} s)" +
        (r.charge.Count > 0 ? ", charge P" + string.Join(" P", r.charge) : "") + "\n";

    // The rolling start at Watkins Glen: a full road-course field double file at the green, through Turn 1 and the
    // esses. A field that merges onto the racing line stacks up on one side of the road braking for Turn 1 (the
    // player drove past twenty cars on the empty side); this measures how much of the road the field uses, how
    // many cars stack up below 30 mph, how far it gets in the time, and how often cars touch.
    [Explicit("Diagnostic: a double-file rolling start at Watkins Glen into Turn 1, five rosters.")]
    [Test, Timeout(1800000)]
    public void WatkinsGlenStart() => StartBaseline("WatkinsGlen", new[] { 1, 2, 3, 4, 5 }, 24, null);

    [Explicit("Diagnostic: one Watkins Glen rolling start with the field across the road logged every second.")]
    [Test, Timeout(1800000)]
    public void WatkinsGlenStartTrace() =>
        Log("WG start trace seed 1", Run("WatkinsGlen", new Settings { cars = 24, seed = 1, startSeconds = 30f, traceHard = 25 }));

    // Racing laps at one track of each kind after a change to how the AI uses the road: passes, contacts and offs
    // should hold or improve, not just the start.
    [Explicit("Diagnostic: four laps of racing at a road course, a speedway, a short track and a plate track, two rosters.")]
    [Test, Timeout(3600000)]
    public void RacingRegression()
    {
        var sb = new StringBuilder("[PackSim] racing regression, 16 cars, 4 laps\n");
        foreach (var id in new[] { "WatkinsGlen", "Charlotte", "Martinsville", "Talladega" })
        foreach (int seed in new[] { 1, 2 })
        {
            var r = Run(id, new Settings { cars = 16, laps = 4, seed = seed, columns = 2 });
            sb.Append(Summary($"  {id} seed {seed}", r));
            Despawn();
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: four laps of Watkins Glen racing, four rosters - passes against contacts.")]
    [Test, Timeout(3600000)]
    public void WatkinsGlenRacing()
    {
        var sb = new StringBuilder("[PackSim] WG racing, 16 cars, 4 laps");
        int p = 0, c = 0, h = 0, o = 0;
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            var r = Run("WatkinsGlen", new Settings { cars = 16, laps = 4, seed = seed, columns = 2, traceHard = 8 });
            foreach (var l in r.lines) if (l.Contains("HARD")) sb.Append(System.Environment.NewLine).Append(l);
            p += r.passes; c += r.contacts; h += r.hardContacts; o += r.offs;
            Despawn();
        }
        sb.Append($"\n  total: {p} passes, {c} contacts, HARD {h}, {o} offs");
        Debug.Log(sb.ToString());
    }

    // NR2003's lateral field (AIRacingBehaviour.useLateralField) against the pack's lane rules, on a superspeedway.
    [Explicit("Diagnostic: the lateral field on against off - a 40-car pack, three rosters.")]
    [Test, Timeout(7200000)]
    public void LateralFieldCompare([Values("Daytona", "Talladega")] string trackId) => FieldRuns(trackId, false, true);

    [Explicit("Diagnostic: the lateral field alone - a 40-car pack, three rosters.")]
    [Test, Timeout(7200000)]
    public void LateralFieldOnly([Values("Daytona", "Talladega")] string trackId) => FieldRuns(trackId, true);

    [Explicit("Diagnostic: the lateral field at Daytona, one roster, hard contacts traced.")]
    [Test, Timeout(1800000)]
    public void LateralFieldTrace() =>
        Log("field trace", Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, columns = 2, traceHard = 30,
                                                     racing = new Dictionary<string, object> { { "useLateralField", true } } }));

    // The racing the AI is judged on. A superspeedway: a pack that holds together, holds its lanes and holds three
    // wide for long stretches. A road course: on the line, clean passes, and a fast car from the back carving
    // through - 1-2 places a lap until it is in the top 20 - rather than parking behind slower cars.
    [Explicit("Diagnostic: the racing goals - Daytona lanes vs field (pack, lanes, 3-wide), Watkins Glen charge from the back.")]
    [Test, Timeout(7200000)]
    public void GoalsBaseline()
    {
        var sb = new StringBuilder("[PackSim] goals baseline\n");
        foreach (bool field in new[] { false, true })
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var r = Run("Daytona", new Settings { cars = 40, laps = 6, seed = seed, columns = 2,
                    racing = new Dictionary<string, object> { { "useLateralField", field } } });
                sb.Append(Summary($"  Daytona {(field ? "FIELD" : "LANES")} seed {seed}", r));
                Despawn();
            }
        foreach (int seed in new[] { 1, 2 })
        {
            var r = Run("WatkinsGlen", new Settings { cars = 24, laps = 8, seed = seed, columns = 2, fastestAtBack = true });
            sb.Append(Summary($"  WG charge seed {seed}", r));
            Despawn();
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: the fastest car from the back at Watkins Glen, traced once a second.")]
    [Test, Timeout(1800000)]
    public void WatkinsGlenChargeTrace() =>
        Log("WG charge trace", Run("WatkinsGlen", new Settings { cars = 24, laps = 4, seed = 1, columns = 2, fastestAtBack = true, traceCharger = true }));

    [Explicit("Diagnostic: road-course follow headway against the charge from the back - Watkins Glen, two rosters each.")]
    [Test, Timeout(7200000)]
    public void WatkinsGlenChargeHeadway()
    {
        var sb = new StringBuilder("[PackSim] WG charge headway\n");
        foreach (float hw in new[] { 0.3f, 0.15f, 0.08f })
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("WatkinsGlen", new Settings { cars = 24, laps = 8, seed = seed, columns = 2, fastestAtBack = true, followHeadway = hw });
                sb.Append($"  headway {hw} seed {seed}: {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}, " +
                          $"median gap {r.medianGap:0.0} m, stuck 10 s+ {r.stuckEpisodes}, charge P{string.Join(" P", r.charge)}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    // Driver grip (NR2003's strength -> traction circle) against the charge from the back at Watkins Glen, and the
    // plate's pace spread against the pack holding together at Daytona.
    [Explicit("Diagnostic: driver grip spread at Watkins Glen (charge) and plate pace spread at Daytona (pack).")]
    [Test, Timeout(7200000)]
    public void SkillSpreadSweep()
    {
        var sb = new StringBuilder("[PackSim] skill spread sweep\n");
        foreach (float g in new[] { 0f, 0.03f, 0.05f })
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("WatkinsGlen", new Settings { cars = 24, laps = 8, seed = seed, columns = 2, fastestAtBack = true, driverGripSpread = g });
                sb.Append($"  WG grip spread {g} seed {seed}: {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}, " +
                          $"best laps {r.fastestLap:0.00}..{r.slowestBest:0.00} s, stuck 10 s+ {r.stuckEpisodes}, charge P{string.Join(" P", r.charge)}\n");
                Despawn();
            }
        foreach (float ps in new[] { 1f, 0.2f, 0f })
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("Daytona", new Settings { cars = 40, laps = 6, seed = seed, columns = 2,
                    racing = new Dictionary<string, object> { { "useLateralField", true }, { "platePaceSpread", ps } } });
                sb.Append($"  Daytona plate spread {ps} seed {seed}: field {r.fieldLengthMedian:0} m, lead pack {r.leadPackShare:P0}, " +
                          $"3-wide {r.wide3Share:P0} (runs {r.wide3RunMean:0.0} s), lane changes {r.laneChangesPerCarLap:0.00}/car/lap, " +
                          $"{r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: one car alone at Watkins Glen - clean-air pace by rating, with and without driver grip.")]
    [Test, Timeout(1800000)]
    public void WatkinsGlenSoloPace()
    {
        var sb = new StringBuilder("[PackSim] WG solo pace\n");
        foreach (float g in new[] { 0f, 0.05f })
            foreach (float s in new[] { 0f, 0.5f, 1f })
            {
                var r = Run("WatkinsGlen", new Settings { cars = 1, laps = 3, seed = 1, soloStrength = s, driverGripSpread = g });
                sb.Append($"  grip spread {g} strength {s}: best lap {r.fastestLap:0.00} s, {r.offs} offs\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: pass settings against the charge from the back at Watkins Glen, two rosters each.")]
    [Test, Timeout(7200000)]
    public void WatkinsGlenChargePassing()
    {
        var sb = new StringBuilder("[PackSim] WG charge passing\n");
        var variants = new (string name, Dictionary<string, object> racing)[]
        {
            ("current", null),
            ("offset 5", new Dictionary<string, object> { { "overtakeLineOffset", 5f } }),
            ("offset 5 commit 3 clear 2.4", new Dictionary<string, object>
                { { "overtakeLineOffset", 5f }, { "commitHoldSeconds", 3f }, { "corridorClearWidthNoLanes", 2.4f } }),
            ("watched field", new Dictionary<string, object>
                { { "overtakeLineOffset", 5f }, { "commitHoldSeconds", 3f }, { "corridorClearWidthNoLanes", 2.4f }, { "followHeadwaySeconds", 0.2f } }),
        };
        foreach (var (name, racing) in variants)
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("WatkinsGlen", new Settings { cars = 24, laps = 8, seed = seed, columns = 2, fastestAtBack = true, racing = racing });
                sb.Append($"  {name} seed {seed}: {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}, " +
                          $"stuck 10 s+ {r.stuckEpisodes}, charge P{string.Join(" P", r.charge)}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: cars falling off the pack at Daytona with the lateral field, traced.")]
    [Test, Timeout(1800000)]
    public void DaytonaDropTrace() =>
        Log("daytona drops", Run("Daytona", new Settings { cars = 40, laps = 4, seed = 1, columns = 2, traceDrops = 40,
            racing = new Dictionary<string, object> { { "useLateralField", true } } }));

    [Explicit("Diagnostic: the lateral field on a road course against the current AI - the Watkins Glen charge from the back.")]
    [Test, Timeout(7200000)]
    public void WatkinsGlenChargeField()
    {
        var sb = new StringBuilder("[PackSim] WG charge field\n");
        var variants = new (string name, Dictionary<string, object> racing)[]
        {
            ("current", null),
            ("field offline 1", new Dictionary<string, object> { { "useLateralField", true }, { "fieldEverywhere", true } }),
            ("field offline 0.5", new Dictionary<string, object> { { "useLateralField", true }, { "fieldEverywhere", true }, { "fieldOffLineCost", 0.5f } }),
        };
        foreach (var (name, racing) in variants)
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("WatkinsGlen", new Settings { cars = 24, laps = 8, seed = seed, columns = 2, fastestAtBack = true, racing = racing });
                sb.Append($"  {name} seed {seed}: {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}, " +
                          $"alone off-line {r.aloneLineRms:0.00} m, stuck 10 s+ {r.stuckEpisodes}, charge P{string.Join(" P", r.charge)}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: draft range against the pack holding together at Daytona - field and lanes.")]
    [Test, Timeout(7200000)]
    public void DaytonaDraftRange()
    {
        var sb = new StringBuilder("[PackSim] daytona draft range\n");
        foreach (var (gap, field) in new[] { (35f, true), (70f, true), (120f, true), (70f, false) })
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("Daytona", new Settings { cars = 40, laps = 6, seed = seed, columns = 2, draftMaxGap = gap,
                    racing = new Dictionary<string, object> { { "useLateralField", field } } });
                sb.Append($"  {(field ? "field" : "lanes")} draft {gap} m seed {seed}: field {r.fieldLengthMedian:0} m, lead pack {r.leadPackShare:P0}, " +
                          $"3-wide {r.wide3Share:P0} (runs {r.wide3RunMean:0.0} s), lane changes {r.laneChangesPerCarLap:0.00}/car/lap, " +
                          $"lap {r.fastestLap:0.0}s, {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: the superspeedway draft range (VehicleInfo range x TrackTuning.draftRangeScale 2) - Daytona three rosters, Talladega.")]
    [Test, Timeout(7200000)]
    public void SuperspeedwayDraftRangeConfirm()
    {
        var sb = new StringBuilder("[PackSim] superspeedway draft range confirm\n");
        foreach (var (track, carGap) in new[] { ("Daytona", 35f), ("Daytona", 45f), ("Daytona", 60f), ("Talladega", 35f) })
            foreach (int seed in new[] { 1, 2, 3 })
            {
                if (track == "Talladega" && seed == 3) continue;
                var r = Run(track, new Settings { cars = 40, laps = 6, seed = seed, columns = 2, draftMaxGap = carGap });
                sb.Append($"  {track} range {carGap * 2f:0} m seed {seed}: lead pack {r.leadPackShare:P0}, field {r.fieldLengthMedian:0} m, " +
                          $"3-wide {r.wide3Share:P0} (runs {r.wide3RunMean:0.0} s, {r.wide3Sustained:P0} 3 s+), lane changes {r.laneChangesPerCarLap:0.00}/car/lap, " +
                          $"hold {r.laneHoldMean:0.0} s, lap {r.fastestLap:0.0}s, {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: fresh against worn tyres, one car alone - Watkins Glen and Daytona.")]
    [Test, Timeout(1800000)]
    public void TyreFalloff()
    {
        var sb = new StringBuilder("[PackSim] tyre falloff\n");
        foreach (var track in new[] { "WatkinsGlen", "Daytona" })
            foreach (float w in new[] { 0f, 0.8f })
            {
                var r = Run(track, new Settings { cars = 1, laps = 3, seed = 1, soloStrength = 0.5f, initialWear = w });
                var tm = _cars[0].GetComponent(Runtime("TireModel"));
                float endWear = tm != null ? ((float[])tm.GetType().GetField("wear").GetValue(tm))[0] : -1f;
                sb.Append($"  {track} start wear {w}: best lap {r.fastestLap:0.00} s, front-left wear after 3 laps {endWear:0.000}, {r.offs} offs\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: Talladega's draft range and lane count against holding a pack - two rosters each.")]
    [Test, Timeout(7200000)]
    public void TalladegaPack()
    {
        var sb = new StringBuilder("[PackSim] talladega pack\n");
        foreach (float carGap in new[] { 13.5f, 35f })   // x the superspeedway's 2.6: 35 m (the car's own) and 90 m
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("Talladega", new Settings { cars = 40, laps = 6, seed = seed, columns = 2, draftMaxGap = carGap });
                sb.Append($"  range {carGap * 2.6f:0} m seed {seed}: lead pack {r.leadPackShare:P0}, field {r.fieldLengthMedian:0} m, " +
                          $"3-wide {r.wide3Share:P0} (runs {r.wide3RunMean:0.0} s), lane changes {r.laneChangesPerCarLap:0.00}/car/lap, " +
                          $"hold {r.laneHoldMean:0.0} s, wander {r.laneJitterRms:0.00} m, {r.passes} passes, {r.contacts} contacts, HARD {r.hardContacts}\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: steering lookahead against how loose the cars look - Daytona pack and Watkins Glen racing.")]
    [Test, Timeout(7200000)]
    public void SteeringStability()
    {
        var sb = new StringBuilder("[PackSim] steering stability\n");
        foreach (float la in new[] { 22f, 35f, 50f })
        {
            var input = new Dictionary<string, object> { { "lookaheadMax", la } };
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("Daytona", new Settings { cars = 40, laps = 4, seed = seed, columns = 2, input = input });
                sb.Append($"  Daytona lookahead {la} seed {seed}: slip rms {r.slipRms:0.00} deg, {r.steerReversalsPerSec:0.00} steer rev/s, " +
                          $"wander {r.laneJitterRms:0.00} m, lead pack {r.leadPackShare:P0}, 3-wide {r.wide3Share:P0}, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}\n");
                Despawn();
            }
            var w = Run("WatkinsGlen", new Settings { cars = 16, laps = 4, seed = 1, columns = 2, input = input });
            sb.Append($"  WG lookahead {la}: slip rms {w.slipRms:0.00} deg, {w.steerReversalsPerSec:0.00} steer rev/s, alone off-line {w.aloneLineRms:0.00} m, " +
                      $"best {w.fastestLap:0.00} s, {w.contacts} contacts, HARD {w.hardContacts}, offs {w.offs}\n");
            Despawn();
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: one mid-pack car at Daytona logged every physics step for six seconds - is it loose?")]
    [Test, Timeout(1800000)]
    public void DaytonaStepTrace() =>
        Log("daytona step trace", Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, columns = 2, stepTraceCar = 20, traceFrom = 70f, traceSeconds = 8f }));

    [Explicit("Diagnostic: a Daytona restart - lane changes and weaving in the first 20 s, settling on against off.")]
    [Test, Timeout(3600000)]
    public void DaytonaRestart()
    {
        var sb = new StringBuilder("[PackSim] daytona restart\n");
        foreach (float settle in new[] { 0f, 8f })
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var r = Run("Daytona", new Settings { cars = 40, seed = seed, startSeconds = 20f, startMph = 60f,
                    racing = new Dictionary<string, object> { { "restartSettleMaxSeconds", settle } } });
                sb.Append($"  settle {settle} s seed {seed}: lane changes {r.laneChanges / 40f:0.00}/car, weaves {r.weaveCount / 40f:0.00}/car, " +
                          $"wander {r.laneJitterRms:0.00} m, {r.contacts} contacts, HARD {r.hardContacts}, offs {r.offs}, last car {r.lastProgress:0} m\n");
                Despawn();
            }
        Debug.Log(sb.ToString());
    }

    // How eager the field is to move: stickiness (m of progress a change must win) and move cost (m per m across).
    [Explicit("Diagnostic: lateral field eagerness sweep at Daytona, three rosters each.")]
    [Test, Timeout(7200000)]
    public void LateralFieldEagerness()
    {
        var sb = new StringBuilder("[PackSim] lateral field eagerness Daytona\n");
        foreach (var (stick, move) in new[] { (1f, 0.1f), (3f, 0.5f), (6f, 1f) })
        {
            var racing = new Dictionary<string, object>
                { { "useLateralField", true }, { "fieldStickiness", stick }, { "fieldMoveCost", move } };
            int contacts = 0, hard = 0, passes = 0;
            float w3 = 0, nose = 0, weaves = 0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var r = Run("Daytona", new Settings { cars = 40, laps = 6, seed = seed, columns = 2, racing = racing });
                contacts += r.contacts; hard += r.hardContacts; passes += r.passes;
                w3 += r.wide3Share; nose += r.noseToTailShare; weaves += r.weavesPerCarLap;
                Despawn();
            }
            sb.Append($"  stick {stick} move {move}: {passes / 3f:0} passes, {contacts / 3f:0} contacts, HARD {hard / 3f:0.0}, " +
                      $"3-wide+ {w3 / 3:P0}, nose-to-tail {nose / 3:P0}, {weaves / 3:0.00} weaves/car/lap\n");
        }
        Debug.Log(sb.ToString());
    }

    void FieldRuns(string trackId, params bool[] variants)
    {
        var sb = new StringBuilder($"[PackSim] lateral field compare {trackId}\n");
        foreach (bool on in variants)
        {
            var racing = new Dictionary<string, object> { { "useLateralField", on } };
            int passes = 0, contacts = 0, hard = 0, offs = 0;
            float w2 = 0, w3 = 0, nose = 0, flat = 0;
            sb.Append(on ? "FIELD\n" : "LANES (current)\n");
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var r = Run(trackId, new Settings { cars = 40, laps = 6, seed = seed, columns = 2, racing = racing });
                sb.Append(Summary($"  seed {seed}", r));
                passes += r.passes; contacts += r.contacts; hard += r.hardContacts; offs += r.offs;
                w2 += r.wide2Share; w3 += r.wide3Share; nose += r.noseToTailShare; flat += r.fullThrottleShare;
                Despawn();
            }
            sb.Append($"  MEAN: {passes / 3f:0} passes, {contacts / 3f:0} contacts, HARD {hard / 3f:0.0}, {offs / 3f:0.0} offs, " +
                      $"2-wide {w2 / 3:P0}, 3-wide+ {w3 / 3:P0}, nose-to-tail {nose / 3:P0}, flat out {flat / 3:P0}\n");
        }
        Debug.Log(sb.ToString());
    }

    void StartBaseline(string trackId, int[] seeds, int cars, Dictionary<string, object> racing)
    {
        var sb = new StringBuilder($"[PackSim] {trackId} rolling start, {cars} cars\n");
        float contacts = 0, hard = 0, offs = 0, slow = 0, rms = 0, right = 0, mean = 0, last = 0;
        foreach (int seed in seeds)
        {
            var r = Run(trackId, new Settings { cars = cars, seed = seed, startSeconds = 30f, racing = racing });
            sb.Append($"  seed {seed}: {r.contacts} contacts (HARD {r.hardContacts}, rear {r.rearContacts}, side {r.sideContacts}), " +
                      $"{r.offs} offs, {r.slowCars} cars under 30 mph, across the road rms {r.lateralRms:0.00} m, " +
                      $"braking right of centre {r.brakingRightShare:P0}, progress mean {r.meanProgress:0} m / last car {r.lastProgress:0} m\n");
            contacts += r.contacts; hard += r.hardContacts; offs += r.offs; slow += r.slowCars;
            rms += r.lateralRms; right += r.brakingRightShare; mean += r.meanProgress; last += r.lastProgress;
            Despawn();
        }
        int k = seeds.Length;
        sb.Append($"  MEAN: {contacts / k:0.0} contacts, HARD {hard / k:0.0}, {offs / k:0.0} offs, {slow / k:0.0} cars under 30 mph, " +
                  $"rms {rms / k:0.00} m, braking right {right / k:P0}, progress {mean / k:0} m / last {last / k:0} m\n");
        Debug.Log(sb.ToString());
    }

    // The superspeedway baseline: a full Cup field at Daytona across several rosters. One line per seed and the
    // totals, so a change to the AI can be judged against it rather than against one lucky draw.
    [Explicit("Diagnostic: a 40-car pack at Daytona, three rosters - the superspeedway baseline.")]
    [Test, Timeout(1800000)]
    public void DaytonaBaseline() => Baseline("Daytona", new[] { 1, 2, 3 }, 40, 6);

    [Explicit("Diagnostic: the same baseline at Talladega.")]
    [Test, Timeout(1800000)]
    public void TalladegaBaseline() => Baseline("Talladega", new[] { 1, 2, 3 }, 40, 6);

    [Explicit("Diagnostic: one car alone at Daytona, and a short 40-car run - is the lift into the turns the car or the pack?")]
    [Test, Timeout(1800000)]
    public void DaytonaThrottle()
    {
        var solo = Run("Daytona", new Settings { cars = 2, laps = 3, seed = 1 });
        var conditions = Runtime("TrackConditions");
        string a = $"plate {conditions.GetField("PlateMph").GetValue(null)} mph, track grip x{conditions.GetField("TrackGripScale").GetValue(null)}, " +
                   $"flat out {conditions.GetField("AiFlatOut").GetValue(null)}, pack {conditions.GetField("AiPackRacing").GetValue(null)}\n" + Summary("pair", solo)
                   + string.Join("\n", solo.lines) + "\n";
        Despawn();
        var pack = Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, columns = 2, traceHard = 40 });
        Debug.Log("[PackSim] daytona throttle\n" + a + Summary("pack", pack) + string.Join("\n", pack.lines));
    }

    // One of each kind of track with its own racing setup, against the old one-size side-by-side caution (1) on the
    // ovals that now race with less of it.
    [Explicit("Diagnostic: a 20-car pack at one track of each kind - superspeedway, speedway, short track, road course.")]
    [Test, Timeout(3600000)]
    public void TrackKinds()
    {
        var sb = new StringBuilder("[PackSim] track kinds, 20 cars, 4 laps\n");
        foreach (var (id, side) in new (string, float?)[] { ("Talladega", null), ("Charlotte", null), ("Charlotte", 1f),
                                                            ("Martinsville", null), ("Martinsville", 1f), ("WatkinsGlen", null) })
        {
            var r = Run(id, new Settings { cars = 20, laps = 4, seed = 1, columns = 2, sideAwareness = side });
            sb.Append(Summary($"  {id}{(side.HasValue ? $" (side awareness {side})" : "")}", r));
            Despawn();
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: a short pack at a plate track with every hard contact traced, and the lane layout.")]
    [TestCase("Talladega")]
    [TestCase("Daytona")]
    public void PlateTrack(string trackId)
    {
        var r = Run(trackId, new Settings { cars = 40, laps = 2, seed = 1, columns = 2, traceHard = 10, traceSlowMph = 150f });
        var conditions = Runtime("TrackConditions");
        Debug.Log($"[PackSim] plate track {trackId}: plate {conditions.GetField("PlateMph").GetValue(null)} mph, grip x{conditions.GetField("TrackGripScale").GetValue(null)}\n"
                  + Summary("", r) + string.Join("\n", r.lines));
    }

    [Explicit("Diagnostic: two cars at Daytona, the second one's controls traced over a lap.")]
    [Test, Timeout(1800000)]
    public void DaytonaPair()
    {
        var r = Run("Daytona", new Settings { cars = 2, laps = 3, seed = 1, traceCar = 1 });
        Debug.Log("[PackSim] daytona pair\n" + Summary("pair", r) + string.Join("\n", r.lines));
    }

    [Explicit("Diagnostic: one Daytona roster with every off and the finishing order logged.")]
    [Test, Timeout(1800000)]
    public void Daytona() => Log("daytona seed 1", Run("Daytona", new Settings { cars = 40, laps = 6, seed = 1 }));

    [Explicit("Diagnostic: one Daytona roster, the last two seconds before each off, step by step.")]
    [Test, Timeout(1800000)]
    public void DaytonaTraceOffs() => Log("daytona trace offs", Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, traceOffs = true }));

    [Explicit("Diagnostic: one Daytona roster, what each close-following car is deciding, twice a second.")]
    [Test, Timeout(1800000)]
    public void DaytonaTraceAttacks() => Log("daytona trace attacks", Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, traceLines = 120 }));

    // Draft strength x following distance at Daytona, two rosters each, four laps: which combination packs the
    // field up without the field running into itself.
    [Explicit("Diagnostic: Daytona draft-strength x follow-distance sweep.")]
    [Test, Timeout(3600000)]
    public void DaytonaDraftSweep()
    {
        var sb = new StringBuilder("[PackSim] Daytona draft sweep (draftScale, followScale)\n");
        foreach (var combo in new[] { (1f, 1f), (1f, 0.35f), (1f, 0.6f), (1.65f, 1f), (1.3f, 0.6f) })
        {
            foreach (int seed in new[] { 1, 2 })
            {
                var r = Run("Daytona", new Settings { cars = 40, laps = 4, seed = seed, draftScale = combo.Item1, followScale = combo.Item2 });
                sb.Append(Summary($"  draft {combo.Item1:0.00} follow {combo.Item2:0.00} seed {seed}", r));
                Despawn();
            }
        }
        Debug.Log(sb.ToString());
    }

    void Baseline(string trackId, int[] seeds, int cars, int laps)
    {
        var sb = new StringBuilder($"[PackSim] {trackId} baseline, {cars} cars, {laps} laps\n");
        float passes = 0, contacts = 0, offs = 0, w2 = 0, w3 = 0, rms = 0, weaves = 0, tow = 0;
        foreach (int seed in seeds)
        {
            var r = Run(trackId, new Settings { cars = cars, laps = laps, seed = seed, columns = 2 });
            sb.Append(Summary($"  seed {seed}", r));
            passes += r.passes; contacts += r.contacts; offs += r.offs;
            w2 += r.wide2Share; w3 += r.wide3Share; rms += r.aloneLineRms; weaves += r.weavesPerCarLap; tow += r.towShare;
            Despawn();
        }
        int k = seeds.Length;
        sb.Append($"  MEAN: {passes / k:0.0} passes, {contacts / k:0.0} contacts, {offs / k:0.0} offs, {tow / k:P0} in a tow, " +
                  $"2-wide {w2 / k:P1}, 3-wide+ {w3 / k:P1}, alone off-line rms {rms / k:0.00} m, {weaves / k:0.00} weaves/car/lap\n");
        Debug.Log(sb.ToString());
    }

    Result Run(string trackId, Settings set)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"no track package for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        var trackType = Runtime("TrackBuilder");
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
        var track = _package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");

        // The track's calibrated AI strength, as TrackPackage applies it on load.
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { trackId });
        var conditions = Runtime("TrackConditions");
        if (set.draftScale.HasValue) conditions.GetField("DraftScale").SetValue(null, set.draftScale.Value);
        if (set.followScale.HasValue) conditions.GetField("AiFollowScale").SetValue(null, set.followScale.Value);
        if (set.sideAwareness.HasValue) conditions.GetField("AiSideAwareness").SetValue(null, set.sideAwareness.Value);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);   // green

        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");
        _vehicleCopy = UnityEngine.Object.Instantiate(vehicleInfo);   // draft overrides never touch the asset
        if (set.draftMaxGap.HasValue) Set(_vehicleCopy, "draftingMaxGap", set.draftMaxGap.Value);
        if (set.draftMinSpeed.HasValue) Set(_vehicleCopy, "draftingMinSpeed", set.draftMinSpeed.Value);
        if (set.draftMaxBonus.HasValue) Set(_vehicleCopy, "draftingMaxBonus", set.draftMaxBonus.Value);
        if (set.draftTowAccel.HasValue) Set(_vehicleCopy, "draftingTowAccel", set.draftTowAccel.Value);
        if (set.draftTopSpeedGain.HasValue) Set(_vehicleCopy, "draftingTopSpeedGain", set.draftTopSpeedGain.Value);

        var splineType = Runtime("SplineDriver");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");
        var racingType = Runtime("AIRacingBehaviour");

        UnityEngine.Random.InitState(set.seed);
        int n = set.cars;
        var splines = new Component[n];
        var pvcs = new Component[n];
        var inputs = new Component[n];
        var racers = new Component[n];
        var quals = new float[n];
        // A spread of ratings like the real roster (qualifying 8..20 of 20), shuffled against grid order so the quick
        // cars start behind slow ones and have to pass. The last index starts at the back in every grid layout.
        var qs = new float[n]; var cs = new float[n]; var ags = new float[n];
        for (int i = 0; i < n; i++)
        {
            float k = n > 1 ? 1f / (n - 1) : 0f;
            qs[i] = set.soloStrength ?? Mathf.Lerp(0.4f, 1f, ((i * 5) % n) * k);
            cs[i] = set.soloStrength ?? Mathf.Lerp(0.65f, 0.95f, ((i * 7) % n) * k);
            ags[i] = Mathf.Lerp(0.3f, 0.9f, ((i * 3) % n) * k);
        }
        int charger = -1;
        if (set.fastestAtBack)
        {
            int f = 0;
            for (int i = 1; i < n; i++) if (qs[i] > qs[f]) f = i;
            charger = n - 1;
            (qs[f], qs[charger]) = (qs[charger], qs[f]);
            (cs[f], cs[charger]) = (cs[charger], cs[f]);
            (ags[f], ags[charger]) = (ags[charger], ags[f]);
        }
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject($"Car{i:D2}");
            _cars.Add(go);

            float q01 = qs[i], cons01 = cs[i], agg01 = ags[i];
            quals[i] = q01;

            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", _vehicleCopy);
            Set(spline, "cornerSpeedScale", 0.95f);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", set.startSeconds > 0f ? set.startMph / 2.237f : 35f);   // m/s
            if (set.startSeconds > 0f)
            {
                var info = track.GetType().GetField("track").GetValue(track);
                float sf = (float)info.GetType().GetField("startFinishDistance").GetValue(info);
                int row = i / 2;   // car 0 on pole this time: the start is about who is where, not about passing
                float d0 = sf - set.startLineMetres - row * set.rowGap;
                Assert.GreaterOrEqual(d0, 0f, "grid runs back past spline 0 - not handled");
                Set(spline, "startDistance", d0);
            }
            else if (set.columns >= 2)
            {
                // Double file: rows 10 m apart, the two columns a lane apart either side of the line.
                int row = (n - 1 - i) / 2;
                Set(spline, "startDistance", 60f + row * 10f);
                Set(spline, "lateralOffset", (i % 2 == 0 ? -1f : 1f) * 1.35f);
            }
            else
                Set(spline, "startDistance", 60f + (n - 1 - i) * 14f);   // single file, 14 m apart, car 0 at the back
            Set(spline, "externalMotionController", true);

            var pvc = go.AddComponent(pvcType);
            Set(pvc, "vehicleInfo", _vehicleCopy);
            Set(pvc, "track", track);
            Set(pvc, "spriteFacesUp", false);
            Set(pvc, "angleOffsetDeg", 180f);
            Set(pvc, "grassTrails", false);
            Set(pvc, "enableWheelspin", false);
            Set(pvc, "externalInput", true);
            Set(pvc, "damageImpairsHandling", false);
            Set(pvc, "surfaceSpray", false);
            Set(pvc, "impactDebris", false);

            var input = go.AddComponent(inputType);
            if (set.input != null) foreach (var kv in set.input) Set(input, kv.Key, kv.Value);
            var racing = go.AddComponent(racingType);

            // What AIDriverBinding.Apply does with a driver's ratings: the event roll (AIRatings.ForEvent, with the
            // track-type aptitude equal to raw speed - no specialists in a test roster), then the same helpers.
            const int statMax = 20;
            var ratings = AIRatings.ForEvent(Mathf.RoundToInt(q01 * statMax), Mathf.RoundToInt(q01 * statMax),
                                             Mathf.RoundToInt(cons01 * statMax), Mathf.RoundToInt(agg01 * statMax), statMax,
                                             UnityEngine.Random.value, UnityEngine.Random.value);
            Set(spline, "lineFactor", AIRatings.LineFactor(ratings.aggression01));
            Set(racing, "aggression01", ratings.aggression01);
            Set(racing, "consistency01", ratings.consistency01);
            float basePace = AIRatings.BasePace(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
            Set(spline, "paceMultiplier", basePace);
            float commit = AIRatings.CornerCommitment(ratings.strength01, ratings.consistency01, UnityEngine.Random.value);
            Set(spline, "cornerCommitment", set.skillCornering ? commit : 1f);
            Set(spline, "driverGrip", AIRatings.DriverGrip(ratings.strength01, set.driverGripSpread ?? AIRatings.DriverGripSpread));
            if (set.followHeadway.HasValue) Set(racing, "followHeadwaySeconds", set.followHeadway.Value);
            if (set.racing != null) foreach (var kv in set.racing) Set(racing, kv.Key, kv.Value);
            Set(racing, "respectYellows", set.respectYellows);

            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(input, "Awake");
            Call(input, "OnEnable");
            Call(racing, "Awake");
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { basePace });
            if (set.startSeconds > 0f)
                racingType.GetMethod("TakeLateral").Invoke(racing, new object[] { (i % 2 == 0 ? -1f : 1f) * set.columnLateral });
            Call(pvc, "OnEnable");   // finds its SplineDriver brain: the draft reads the track pose from it
            Call(pvc, "Start");
            Call(spline, "Start");

            splines[i] = spline; pvcs[i] = pvc; inputs[i] = input; racers[i] = racing;
        }

        var lengthProp = splineType.GetProperty("TrackLength");
        var distProp = splineType.GetProperty("DistanceOnTrack");
        var towProp = pvcType.GetProperty("TowFactor");
        var onLegal = Runtime("LapTimingManager").GetMethod("OnLegalSurface", BindingFlags.Public | BindingFlags.Static);
        var racingStep = racingType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        float len = (float)lengthProp.GetValue(splines[0]);
        Assert.Greater(len, 100f, "The AI brain never built its spline.");

        var r = new Result();
        float dt = Time.fixedDeltaTime;
        var lapsDone = new int[n];
        var lastD = new float[n];
        var lapStart = new float[n];
        var best = new float[n];
        var wasOn = new bool[n];
        var history = new Queue<string>[n];
        var slowLogged = new bool[n];
        for (int i = 0; i < n; i++) history[i] = new Queue<string>();
        var pathAhead = splineType.GetMethod("PathPointAhead");
        for (int i = 0; i < n; i++) { lastD[i] = (float)distProp.GetValue(splines[i]); lapStart[i] = -1f; best[i] = float.MaxValue; wasOn[i] = true; }
        var progress = new float[n];
        var order = new int[n];
        int[] prevOrder = null;
        var gaps = new List<float>();
        int towSamples = 0, towed = 0;
        var touching = new HashSet<int>();
        Component watch = null;
        var watchType = Runtime("CautionWatch");
        var tickWatch = watchType.GetMethod("Tick");
        if (set.stopCarAt >= 0f)
        {
            var wgo = new GameObject("CautionWatch");
            _cars.Add(wgo);   // torn down with the cars
            watch = wgo.AddComponent(watchType);
            Call(watch, "Awake");
        }
        bool stopped = false;
        float stopD = -1f;
        var cleanSpeeds = new List<float>();   // (distance, mph) of the others before the stop
        var cleanDist = new List<float>();
        var mphProp = splineType.GetProperty("CurrentMph");
        var underYellowProp = racingType.GetProperty("UnderYellow");
        int maxSteps = set.startSeconds > 0f ? Mathf.RoundToInt(set.startSeconds / dt) : Mathf.RoundToInt((set.laps + 1) * 150f / dt);
        bool fromStart = set.startSeconds > 0f;
        var wasSlow = new bool[n];
        var startD = new float[n];
        for (int i = 0; i < n; i++) startD[i] = (float)distProp.GetValue(splines[i]);
        double latSq = 0; int latSamples = 0, brakingSamples = 0, brakingRight = 0;
        var tacField = splineType.GetField("tacticalLateralOffset");
        int shapeSamples = 0, wide2 = 0, wide3 = 0;
        double aloneSq = 0; int aloneSamples = 0; float aloneMax = 0f;
        var prevTac = new float[n];
        var tacTrend = new float[n];   // sign of the last real move of the intended offset
        int weaves = 0;
        var wideStart = new float[n]; for (int i = 0; i < n; i++) wideStart[i] = -1f;
        var wideRuns = new List<float>();
        var anchorLat = new float[n]; var anchorT = new float[n]; var anchored = new bool[n];
        var holds = new List<float>();
        int laneChanges = 0; double jitterSq = 0; int jitterSamples = 0;
        var fieldLengths = new List<float>(); int packSamples = 0, packCars = 0;
        var behind = new List<(float t, int i, int j)>();   // who ran close behind whom, sampled
        int chargerLaps = 0;
        var dropped = new bool[n];
        double slipSq = 0; int slipSamples = 0, steerRev = 0; var steerTrend = new float[n]; var prevSteer = new float[n];
        float t = 0f;
        var thrProp = inputType.GetProperty("LastThrottle");
        var brkProp = inputType.GetProperty("LastBrake");
        var latProp = splineType.GetProperty("LateralOnTrack");
        var wasFlat = new bool[n];
        int thrSamples = 0, flatSamples = 0, brakeSamples = 0, lifts = 0, followSamples = 0, n2t = 0;
        double mphSum = 0;

        for (int step = 0; step < maxSteps; step++)
        {
            if (step == 0 && set.initialWear > 0f)
                for (int i = 0; i < n; i++)
                {
                    var tm = _cars[i].GetComponent(Runtime("TireModel"));
                    if (tm == null) continue;
                    var w = (float[])tm.GetType().GetField("wear").GetValue(tm);
                    for (int k = 0; k < w.Length; k++) w[k] = set.initialWear;
                }
            for (int i = 0; i < n; i++)
            {
                if (stopped && i == 0) splineType.GetField("aiMaxSpeedMph").SetValue(splines[0], 0f);
                else racingStep.Invoke(racers[i], null);
                inputStep.Invoke(inputs[i], null);
                pvcStep.Invoke(pvcs[i], null);
                splineStep.Invoke(splines[i], null);
            }
            t += dt;
            if (set.stepTraceCar >= 0 && set.stepTraceCar < n && t >= set.traceFrom && t < set.traceFrom + set.traceSeconds)
            {
                int c = set.stepTraceCar;
                r.lines.Add($"  S t{t:0.00} d{(float)distProp.GetValue(splines[c]):0} {splineType.GetProperty("CurrentPhase").GetValue(splines[c])} " +
                            $"mph {Get(pvcs[c], "SpeedMph"):0.0} slip {Get(pvcs[c], "SlipAngleDeg"):0.00} yaw {Get(pvcs[c], "YawRateDeg"):0.0} " +
                            $"steer {Get(inputs[c], "LastSteer"):0.000} nose {Get(inputs[c], "LastNoseErrorDeg"):0.00} " +
                            $"lat {Get(splines[c], "LateralOnTrack"):0.00} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(splines[c]):0.00} th {Get(inputs[c], "LastThrottle"):0.00}");
            }

            if (watch != null)
            {
                tickWatch.Invoke(watch, new object[] { t });
                float d0 = (float)distProp.GetValue(splines[0]);
                if (!stopped && lapsDone[0] >= 2 && d0 >= set.stopCarAt && d0 < set.stopCarAt + 50f)
                {
                    stopped = true;
                    splineType.GetField("aiSpeedBoostMph").SetValue(splines[0], 0f);
                }
                if (stopped && stopD < 0f && (float)mphProp.GetValue(splines[0]) < 1f) stopD = d0;

                // Everyone else's speed over the stretch leading up to where car 0 comes to rest: before it
                // stops (the clean baseline, sampled over set.stopCarAt..+250 so it covers wherever it ends
                // up) and once it's parked there.
                for (int i = 1; i < n; i++)
                {
                    float d = (float)distProp.GetValue(splines[i]);
                    float v = (float)mphProp.GetValue(splines[i]);
                    if (!stopped && lapsDone[i] >= 1 && d >= set.stopCarAt - 200f && d <= set.stopCarAt + 250f)
                    { cleanDist.Add(d); cleanSpeeds.Add(v); }
                    if (stopD >= 0f && d >= stopD - 150f && d <= stopD - 20f)
                    {
                        r.yellowMph += v; r.yellowSamples++;
                    }
                    if (stopD >= 0f && step % 5 == 0 && d >= stopD - 60f && d <= stopD + 8f && r.lines.Count < set.traceYellowLines)
                        r.lines.Add($"  t{t:0.0} Car{i:D2} to stop {stopD - d:0.0} m lat {Get(splines[i], "LateralOnTrack"):0.0} (stopped lat {Get(splines[0], "LateralOnTrack"):0.0}) " +
                            $"{v:0.0} mph cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(splines[i])):0.0} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(splines[i]):0.0} " +
                            $"commit {(float)racingType.GetField("_commitTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0}/{(float)racingType.GetField("_commitDir", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0} " +
                            $"rec {(float)racingType.GetField("_recoveryTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0} yellow {(bool)underYellowProp.GetValue(racers[i])}");
                }
            }

            if (set.traceCar >= 0 && set.traceCar < n && lapsDone[set.traceCar] == 2 && step % 10 == 0)
            {
                int c = set.traceCar;
                r.lines.Add($"  d{(float)distProp.GetValue(splines[c]):0} car {Get(pvcs[c], "SpeedMps") * 2.237f:0.0} brain {Get(splines[c], "CurrentMph"):0.0} " +
                    $"target {Get(splines[c], "TargetMph"):0.0} desired {Get(splines[c], "DesiredMph"):0.0} cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(splines[c])):0.0} " +
                    $"cmd {Get(inputs[c], "LastCommandedMps") * 2.237f:0.0} gripcap {Mathf.Min(999f, Get(inputs[c], "LastGripCapMps") * 2.237f):0.0} " +
                    $"th {Get(inputs[c], "LastThrottle"):0.00} br {Get(inputs[c], "LastBrake"):0.00} slip {Get(pvcs[c], "SlipAngleDeg"):0.0} " +
                    $"lat {Get(splines[c], "LateralOnTrack"):0.0} tow {(float)towProp.GetValue(pvcs[c]):0.00} push {Get(pvcs[c], "PushFactor"):0.00} bank {Get(pvcs[c], "CurrentBankDeg"):0}");
            }

            int leaderLaps = 0;
            for (int i = 0; i < n; i++)
            {
                float d = (float)distProp.GetValue(splines[i]);
                if (d < lastD[i] - len * 0.5f)
                {
                    if (lapStart[i] >= 0f) best[i] = Mathf.Min(best[i], t - lapStart[i]);
                    lapStart[i] = t;
                    lapsDone[i]++;
                }
                lastD[i] = d;
                progress[i] = lapsDone[i] * len + d;
                leaderLaps = Mathf.Max(leaderLaps, lapsDone[i]);

                bool on = (bool)onLegal.Invoke(null, new object[] { track, _cars[i].transform.position, 1f });
                if (set.traceSlowMph > 0f && step % 3 == 0)
                {
                    history[i].Enqueue($"    t{t:0.00} d{d:0.0} car {Get(pvcs[i], "SpeedMps") * 2.237f:0.0} brain {Get(splines[i], "CurrentMph"):0.0} " +
                        $"target {Get(splines[i], "TargetMph"):0.0} cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(splines[i])):0.0} " +
                        $"lat {Get(splines[i], "LateralOnTrack"):0.00} tac {(float)tacField.GetValue(splines[i]):0.00} lane {racingType.GetProperty("PackLane").GetValue(racers[i])}{((bool)racingType.GetProperty("PackMoving").GetValue(racers[i]) ? "->" : "")} " +
                        $"commit {(float)racingType.GetField("_commitTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0} rec {(float)racingType.GetField("_recoveryTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0} " +
                        $"mist {(float)racingType.GetField("_mistakeTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0} " +
                        $"th {Get(inputs[i], "LastThrottle"):0.00} br {Get(inputs[i], "LastBrake"):0.00} slip {Get(pvcs[i], "SlipAngleDeg"):0.0} nose {Get(inputs[i], "LastNoseErrorDeg"):0.0} tow {(float)towProp.GetValue(pvcs[i]):0.00}");
                    while (history[i].Count > 25) history[i].Dequeue();
                    float vSlow = Get(pvcs[i], "SpeedMps") * 2.237f;
                    if (t > (fromStart ? 2f : 25f) && vSlow < set.traceSlowMph && !slowLogged[i] && r.lines.Count < 300)
                    {
                        slowLogged[i] = true;
                        r.lines.Add($"  Car{i:D2} slow, lead-up:");
                        r.lines.AddRange(history[i]);
                    }
                }
                if (set.traceOffs && step % 3 == 0)
                {
                    // Where the car sits against its own planned line (m, + = left of it).
                    var tr = ((Component)track).transform;
                    Vector2 p0 = (Vector2)pathAhead.Invoke(splines[i], new object[] { 0f });
                    Vector2 p1 = (Vector2)pathAhead.Invoke(splines[i], new object[] { 3f });
                    Vector2 w0 = tr.TransformPoint(p0), w1 = tr.TransformPoint(p1);
                    Vector2 tg = (w1 - w0).normalized;
                    float off = Vector2.Dot((Vector2)_cars[i].transform.position - w0, new Vector2(-tg.y, tg.x));
                    history[i].Enqueue($"    t{t:0.00} d{d:0.0} {Get(splines[i], "CurrentMph"):0.0} mph lat {Get(splines[i], "LateralOnTrack"):0.0} " +
                        $"tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(splines[i]):0.00} carOff {off:0.00} " +
                        $"st {Get(inputs[i], "LastSteer"):0.00} th {Get(inputs[i], "LastThrottle"):0.00} br {Get(inputs[i], "LastBrake"):0.00} " +
                        $"yaw {Get(pvcs[i], "YawRateDeg"):0.0} slip {Get(pvcs[i], "SlipAngleDeg"):0.0} tow {(float)towProp.GetValue(pvcs[i]):0.00}{(on ? "" : " OFF")}");
                    while (history[i].Count > 40) history[i].Dequeue();
                }
                if (!on && wasOn[i] && lapsDone[i] >= 1 && set.traceOffs && r.lines.Count < 400)
                {
                    r.lines.Add($"  Car{i:D2} off, lead-up:");
                    r.lines.AddRange(history[i]);
                }
                if (!on && wasOn[i] && (lapsDone[i] >= 1 || fromStart))
                {
                    r.offs++;
                    if (set.traceLines == 0 && set.traceYellowLines == 0 && r.lines.Count < 60)
                        r.lines.Add($"  off: t{t:0.0} Car{i:D2} lap {lapsDone[i]} d{d:0} lat {Get(splines[i], "LateralOnTrack"):0.0} " +
                                    $"tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(splines[i]):0.0} " +
                                    $"slip {Get(pvcs[i], "SlipAngleDeg"):0.0} {Get(splines[i], "CurrentMph"):0} mph");
                }
                wasOn[i] = on;
            }
            if (leaderLaps > set.laps) break;

            if (fromStart && step % 5 == 0)
            {
                var hist = new int[5];
                float minV = float.MaxValue, sumV = 0f;
                for (int i = 0; i < n; i++)
                {
                    float lat = (float)latProp.GetValue(splines[i]);
                    float v = (float)mphProp.GetValue(splines[i]);
                    latSq += lat * lat; latSamples++;
                    if ((float)brkProp.GetValue(inputs[i]) > 0.05f) { brakingSamples++; if (lat > 0f) brakingRight++; }
                    if (t > 2f && v < 30f && !wasSlow[i]) { wasSlow[i] = true; r.slowCars++; }
                    hist[lat < -3f ? 0 : lat < -1f ? 1 : lat < 1f ? 2 : lat < 3f ? 3 : 4]++;
                    minV = Mathf.Min(minV, v); sumV += v;
                }
                if (step % 50 == 0)
                    r.lines.Add($"  t{t:0.0} leader d{(float)distProp.GetValue(splines[order[0]]):0} across (L<-3|-1|0|1|>3R) " +
                                $"{hist[0]} {hist[1]} {hist[2]} {hist[3]} {hist[4]}  speed min {minV:0} mean {sumV / n:0} mph, contacts {r.contacts} hard {r.hardContacts}");
            }

            // A close car's decision-making, twice a second: what it is chasing, what it may do, where it is.
            if (set.traceCharger && charger >= 0 && step % 50 == 0 && r.lines.Count < 400)
            {
                int i = charger;
                float ahead = float.MaxValue; int aj = -1;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    float g = progress[j] - progress[i];
                    if (g > 0f && g < ahead) { ahead = g; aj = j; }
                }
                if (aj >= 0)
                {
                    var si = splines[i]; var sj = splines[aj];
                    var nb = BindingFlags.Instance | BindingFlags.NonPublic;
                    r.lines.Add($"  CH t{t:0} d{(float)distProp.GetValue(si):0} phase {splineType.GetProperty("CurrentPhase").GetValue(si)} -> Car{aj:D2}(q{quals[aj]:0.00}) gap {ahead:0.0} | " +
                        $"me {Get(si, "CurrentMph"):0} want {Get(si, "DesiredMph"):0} cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(si)):0} " +
                        $"lat {Get(si, "LateralOnTrack"):0.0} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(si):0.0} " +
                        $"commit {(float)racingType.GetField("_commitTimer", nb).GetValue(racers[i]):0.0}/{(float)racingType.GetField("_commitDir", nb).GetValue(racers[i]):0} " +
                        $"cool {(float)racingType.GetField("_cooldownTimer", nb).GetValue(racers[i]):0.0} | " +
                        $"them {Get(sj, "CurrentMph"):0} want {Get(sj, "DesiredMph"):0} lat {Get(sj, "LateralOnTrack"):0.0}");
                }
            }
            if (set.traceLines > 0 && leaderLaps >= 2 && step % 25 == 0 && r.lines.Count < set.traceLines)
            {
                for (int i = 0; i < n; i++)
                {
                    float ahead = float.MaxValue; int aj = -1;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        float g = progress[j] - progress[i];
                        if (g > 0f && g < ahead) { ahead = g; aj = j; }
                    }
                    if (aj < 0 || ahead > 35f) continue;
                    var si = splines[i]; var sj = splines[aj];
                    r.lines.Add($"  t{t:0.0} Car{i:D2}(q{quals[i]:0.00}) d{(float)distProp.GetValue(si):0} -> Car{aj:D2}(q{quals[aj]:0.00}) gap {ahead:0.0} m | " +
                        $"me {Get(si, "CurrentMph"):0.0} want {Get(si, "DesiredMph"):0.0} cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(si)):0.0} " +
                        $"boost {(float)splineType.GetField("aiSpeedBoostMph").GetValue(si):0.0} lat {Get(si, "LateralOnTrack"):0.0} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(si):0.0} " +
                        $"commit {(float)racingType.GetField("_commitTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0}/{(float)racingType.GetField("_commitDir", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0} " +
                        $"cool {(float)racingType.GetField("_cooldownTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[i]):0.0} tow {(float)towProp.GetValue(pvcs[i]):0.00} | " +
                        $"them {Get(sj, "CurrentMph"):0.0} lat {Get(sj, "LateralOnTrack"):0.0} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(sj):0.0}");
                    if (r.lines.Count >= set.traceLines) break;
                }
            }

            // Running order; a swap between consecutive samples after the opening lap is a pass.
            if (step % 25 == 0)
            {
                for (int i = 0; i < n; i++) order[i] = i;
                Array.Sort(order, (a, b) => progress[b].CompareTo(progress[a]));
                if (leaderLaps >= 2 && prevOrder != null)
                {
                    var prevRank = new int[n];
                    for (int k = 0; k < n; k++) prevRank[prevOrder[k]] = k;
                    for (int k = 0; k < n; k++) if (prevRank[order[k]] > k) r.passes += prevRank[order[k]] - k;
                    // Under yellow: one running car passing another (the stopped car doesn't count).
                    if (stopD >= 0f)
                        for (int a = 1; a < n; a++)
                        for (int b = 1; b < n; b++)
                        {
                            if (a == b || !(bool)underYellowProp.GetValue(racers[a])) continue;
                            int ra = Array.IndexOf(order, a), rb = Array.IndexOf(order, b);
                            if (prevRank[a] > prevRank[b] && ra < rb) r.yellowPasses++;
                        }
                }
                prevOrder = (int[])order.Clone();

                if (leaderLaps >= 2)
                {
                    for (int k = 1; k < n; k++) gaps.Add(progress[order[k - 1]] - progress[order[k]]);
                    for (int i = 0; i < n; i++)
                    {
                        towSamples++;
                        if ((float)towProp.GetValue(pvcs[i]) > 0.05f) towed++;
                    }
                }
            }

            // Pack shape, ten times a second once racing.
            if ((leaderLaps >= 2 || set.startSeconds > 0f) && step % 5 == 0)
            {
                var ord = new int[n];
                for (int k = 0; k < n; k++) ord[k] = k;
                Array.Sort(ord, (a, b) => progress[b].CompareTo(progress[a]));
                fieldLengths.Add(progress[ord[0]] - progress[ord[n - 1]]);
                int inPack = 1;
                while (inPack < n && progress[ord[inPack - 1]] - progress[ord[inPack]] < 25f) inPack++;
                packSamples++; packCars += inPack;
                for (int k = 1; k < n; k++)
                {
                    int me = ord[k], them = ord[k - 1];
                    float gapAhead = progress[them] - progress[me];
                    if (gapAhead < 25f) { behind.Add((t, me, them)); dropped[me] = false; continue; }
                    if (dropped[me] || set.traceDrops <= 0 || r.lines.Count >= set.traceDrops) continue;
                    dropped[me] = true;
                    var sm = splines[me]; var st = splines[them];
                    var nb = BindingFlags.Instance | BindingFlags.NonPublic;
                    r.lines.Add($"  DROP t{t:0.0} P{k + 1} Car{me:D2}(q{quals[me]:0.00}) {gapAhead:0.0} m off Car{them:D2}(q{quals[them]:0.00}) | " +
                        $"me {Get(sm, "CurrentMph"):0.0} want {Get(sm, "DesiredMph"):0.0} target {Get(sm, "TargetMph"):0.0} cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(sm)):0} " +
                        $"boost {(float)splineType.GetField("aiSpeedBoostMph").GetValue(sm):0.0} th {Get(inputs[me], "LastThrottle"):0.00} br {Get(inputs[me], "LastBrake"):0.00} " +
                        $"tow {(float)towProp.GetValue(pvcs[me]):0.00} lat {Get(sm, "LateralOnTrack"):0.0} aim {(float)racingType.GetField("_fieldTarget", nb).GetValue(racers[me]):0.0} | " +
                        $"them {Get(st, "CurrentMph"):0.0} lat {Get(st, "LateralOnTrack"):0.0} tow {(float)towProp.GetValue(pvcs[them]):0.00}");
                }
                if (charger >= 0 && lapsDone[charger] > chargerLaps)
                {
                    chargerLaps = lapsDone[charger];
                    r.charge.Add(Array.IndexOf(ord, charger) + 1);
                }
                for (int i = 0; i < n; i++)
                {
                    int alongside = 0;
                    float nearest = float.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        float g = Mathf.Abs(progress[j] - progress[i]);
                        if (g < 2f * HalfLength) alongside++;
                        nearest = Mathf.Min(nearest, g);
                    }
                    shapeSamples++;
                    if (alongside == 1) wide2++;
                    else if (alongside >= 2) wide3++;

                    // Stability: body slip and the steering wheel going back and forth.
                    float slipNow = Get(inputs[i], "LastSlipDeg");
                    slipSq += slipNow * slipNow; slipSamples++;
                    float steerNow = Get(inputs[i], "LastSteer");
                    float ds = steerNow - prevSteer[i];
                    if (Mathf.Abs(ds) > 0.03f)
                    {
                        float sd = Mathf.Sign(ds);
                        if (steerTrend[i] != 0f && sd != steerTrend[i]) steerRev++;
                        steerTrend[i] = sd; prevSteer[i] = steerNow;
                    }

                    // 3-wide runs.
                    if (alongside >= 2) { if (wideStart[i] < 0f) wideStart[i] = t; }
                    else if (wideStart[i] >= 0f) { wideRuns.Add(t - wideStart[i]); wideStart[i] = -1f; }

                    // Lane hold: a change is a move of more than half a groove off the lateral being held.
                    float lh = (float)latProp.GetValue(splines[i]);
                    if (!anchored[i]) { anchored[i] = true; anchorLat[i] = lh; anchorT[i] = t; }
                    else if (Mathf.Abs(lh - anchorLat[i]) > 1.3f)
                    {
                        holds.Add(t - anchorT[i]); laneChanges++;
                        anchorLat[i] = lh; anchorT[i] = t;
                    }
                    else
                    {
                        anchorLat[i] = Mathf.Lerp(anchorLat[i], lh, 0.05f);   // follow a slow drift of the groove itself
                        jitterSq += (lh - anchorLat[i]) * (lh - anchorLat[i]); jitterSamples++;
                    }

                    // Alone: nobody within 30 m either way. How far off its own planned path the car is running.
                    if (nearest > 30f)
                    {
                        var tr = ((Component)track).transform;
                        Vector2 w0 = tr.TransformPoint((Vector2)pathAhead.Invoke(splines[i], new object[] { 0f }));
                        Vector2 w1 = tr.TransformPoint((Vector2)pathAhead.Invoke(splines[i], new object[] { 3f }));
                        Vector2 tg = (w1 - w0).normalized;
                        float off = Mathf.Abs(Vector2.Dot((Vector2)_cars[i].transform.position - w0, new Vector2(-tg.y, tg.x)));
                        aloneSq += off * off; aloneSamples++; aloneMax = Mathf.Max(aloneMax, off);
                    }

                    float th = (float)thrProp.GetValue(inputs[i]);
                    float br = (float)brkProp.GetValue(inputs[i]);
                    float vNow = (float)mphProp.GetValue(splines[i]);
                    thrSamples++;
                    mphSum += vNow;
                    r.minMph = Mathf.Min(r.minMph, vNow);
                    if (th >= 0.98f) flatSamples++;
                    if (br > 0.02f) brakeSamples++;
                    if (wasFlat[i] && th < 0.9f) lifts++;
                    if (th >= 0.98f) wasFlat[i] = true; else if (th < 0.9f) wasFlat[i] = false;
                    // Nose to tail: the nearest car ahead in the same lane.
                    float aheadSame = float.MaxValue;
                    float myLat = (float)latProp.GetValue(splines[i]);
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        float g = progress[j] - progress[i];
                        if (g <= 0f || g >= aheadSame) continue;
                        if (Mathf.Abs((float)latProp.GetValue(splines[j]) - myLat) < 1.4f) aheadSame = g;
                    }
                    if (aheadSame < 40f) { followSamples++; if (aheadSame < 7f) n2t++; }

                    // A weave: the intended offset moving one way by more than 0.15 m, then the other.
                    float tac = (float)tacField.GetValue(splines[i]);
                    float move = tac - prevTac[i];
                    if (Mathf.Abs(move) > 0.15f)
                    {
                        float dir = Mathf.Sign(move);
                        if (tacTrend[i] != 0f && dir != tacTrend[i]) weaves++;
                        tacTrend[i] = dir;
                        prevTac[i] = tac;
                    }
                }
            }

            // Contact: rectangles overlapping.
            if (lapsDone[0] >= 1 || fromStart)
            {
                for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 pa = _cars[i].transform.position, pb = _cars[j].transform.position;
                    if ((pa - pb).sqrMagnitude > 64f) continue;
                    float ha = (_cars[i].transform.eulerAngles.z - 180f) * Mathf.Deg2Rad;
                    float hb = (_cars[j].transform.eulerAngles.z - 180f) * Mathf.Deg2Rad;
                    int key = i * 1000 + j;
                    bool touch = Penetration(pa, new Vector2(Mathf.Cos(ha), Mathf.Sin(ha)), pb, new Vector2(Mathf.Cos(hb), Mathf.Sin(hb))) > 0f;
                    if (!touch) { touching.Remove(key); continue; }
                    if (touching.Add(key))
                    {
                        r.contacts++;
                        if (stopD >= 0f && i == 0) r.stoppedHits++;
                        float latA = (float)latProp.GetValue(splines[i]), latB = (float)latProp.GetValue(splines[j]);
                        if (Mathf.Abs(latA - latB) < 1.3f) r.rearContacts++; else r.sideContacts++;
                        Vector2 va = Get(pvcs[i], "SpeedMps") * new Vector2(Mathf.Cos(Get(pvcs[i], "HeadingDeg") * Mathf.Deg2Rad), Mathf.Sin(Get(pvcs[i], "HeadingDeg") * Mathf.Deg2Rad));
                        Vector2 vb = Get(pvcs[j], "SpeedMps") * new Vector2(Mathf.Cos(Get(pvcs[j], "HeadingDeg") * Mathf.Deg2Rad), Mathf.Sin(Get(pvcs[j], "HeadingDeg") * Mathf.Deg2Rad));
                        Vector2 nrm = (pb - pa).normalized;
                        float closingHit = Vector2.Dot(va - vb, nrm);
                        if (closingHit > 3f)
                        {
                            r.hardContacts++;
                            if (r.lines.Count < set.traceHard)
                            {
                                string State(int c) =>
                                    $"Car{c:D2} {Get(pvcs[c], "SpeedMps") * 2.237f:0.0} mph lat {Get(splines[c], "LateralOnTrack"):0.00} tac {(float)tacField.GetValue(splines[c]):0.00} " +
                                    $"lane {racingType.GetProperty("PackLane").GetValue(racers[c])}{((bool)racingType.GetProperty("PackMoving").GetValue(racers[c]) ? "->" : "")} " +
                                    $"th {Get(inputs[c], "LastThrottle"):0.00} br {Get(inputs[c], "LastBrake"):0.00} cap {Mathf.Min(999f, (float)splineType.GetField("aiMaxSpeedMph").GetValue(splines[c])):0} " +
                                    $"slip {Get(pvcs[c], "SlipAngleDeg"):0.0} tow {(float)towProp.GetValue(pvcs[c]):0.00}";
                                r.lines.Add($"  HARD t{t:0.0} d{(float)distProp.GetValue(splines[i]):0} closing {closingHit:0.0} m/s, gap {progress[j] - progress[i]:0.0} m | {State(i)} | {State(j)}");
                            }
                        }
                    }
                }
            }
        }

        if (stopD >= 0f)
        {
            r.stoppedAt = stopD;
            for (int k = 0; k < cleanDist.Count; k++)
                if (cleanDist[k] >= stopD - 150f && cleanDist[k] <= stopD - 20f) { r.cleanMph += cleanSpeeds[k]; r.cleanSamples++; }
            if (r.cleanSamples > 0) r.cleanMph /= r.cleanSamples;
            if (r.yellowSamples > 0) r.yellowMph /= r.yellowSamples;
        }

        r.laps = set.laps;
        if (fromStart)
        {
            r.lateralRms = latSamples > 0 ? (float)System.Math.Sqrt(latSq / latSamples) : 0f;
            r.brakingRightShare = brakingSamples > 0 ? brakingRight / (float)brakingSamples : 0f;
            float sum = 0f, last = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float covered = lapsDone[i] * len + (float)distProp.GetValue(splines[i]) - startD[i];
                sum += covered; last = Mathf.Min(last, covered);
            }
            r.meanProgress = sum / n; r.lastProgress = last;
        }
        r.wide2Share = shapeSamples > 0 ? wide2 / (float)shapeSamples : 0f;
        r.wide3Share = shapeSamples > 0 ? wide3 / (float)shapeSamples : 0f;
        r.aloneLineRms = aloneSamples > 0 ? (float)System.Math.Sqrt(aloneSq / aloneSamples) : 0f;
        r.aloneLineMax = aloneMax;
        int racedLaps = 0;
        for (int i = 0; i < n; i++) racedLaps += Mathf.Max(0, lapsDone[i] - 1);
        r.weavesPerCarLap = racedLaps > 0 ? weaves / (float)racedLaps : 0f;
        r.slipRms = slipSamples > 0 ? (float)System.Math.Sqrt(slipSq / slipSamples) : 0f;
        r.steerReversalsPerSec = slipSamples > 0 ? steerRev / (slipSamples * 0.1f) : 0f;   // samples are 10 Hz per car
        for (int i = 0; i < n; i++) if (wideStart[i] >= 0f) wideRuns.Add(t - wideStart[i]);
        float wideTotal = 0f, wideLong = 0f;
        foreach (var w in wideRuns) { wideTotal += w; if (w >= 3f) wideLong += w; }
        r.wide3RunMean = wideRuns.Count > 0 ? wideTotal / wideRuns.Count : 0f;
        r.wide3Sustained = wideTotal > 0f ? wideLong / wideTotal : 0f;
        for (int i = 0; i < n; i++) if (anchored[i]) holds.Add(t - anchorT[i]);
        float holdSum = 0f; foreach (var hd in holds) holdSum += hd;
        r.laneHoldMean = holds.Count > 0 ? holdSum / holds.Count : 0f;
        r.laneChangesPerCarLap = racedLaps > 0 ? laneChanges / (float)racedLaps : 0f;
        r.laneChanges = laneChanges; r.weaveCount = weaves;
        r.laneJitterRms = jitterSamples > 0 ? (float)System.Math.Sqrt(jitterSq / jitterSamples) : 0f;
        fieldLengths.Sort();
        r.fieldLengthMedian = fieldLengths.Count > 0 ? fieldLengths[fieldLengths.Count / 2] : 0f;
        r.leadPackShare = packSamples > 0 ? packCars / (float)(packSamples * n) : 0f;
        {
            // Stuck: continuous stretches of i close behind j where j is the slower car over the run.
            var runStart = new Dictionary<long, float>(); var last = new Dictionary<long, float>();
            foreach (var (st, i, j) in behind)
            {
                if (!(best[i] < best[j] - 0.3f)) continue;
                long key = i * 1000L + j;
                if (last.TryGetValue(key, out float lt) && st - lt < 0.25f) { last[key] = st; continue; }
                if (last.TryGetValue(key, out lt)) Close(key, lt);
                runStart[key] = st; last[key] = st;
            }
            foreach (var kv in last) Close(kv.Key, kv.Value);
            void Close(long key, float end)
            {
                float len = end - runStart[key];
                if (len >= 10f) r.stuckEpisodes++;
                r.stuckLongest = Mathf.Max(r.stuckLongest, len);
            }
        }
        r.fullThrottleShare = thrSamples > 0 ? flatSamples / (float)thrSamples : 0f;
        r.brakeShare = thrSamples > 0 ? brakeSamples / (float)thrSamples : 0f;
        r.liftsPerCarLap = racedLaps > 0 ? lifts / (float)racedLaps : 0f;
        r.noseToTailShare = followSamples > 0 ? n2t / (float)followSamples : 0f;
        r.meanMph = thrSamples > 0 ? (float)(mphSum / thrSamples) : 0f;

        gaps.Sort();
        r.medianGap = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0f;
        int under = 0;
        foreach (var g in gaps) if (g < 20f) under++;
        r.under20Share = gaps.Count > 0 ? under / (float)gaps.Count : 0f;
        r.towShare = towSamples > 0 ? towed / (float)towSamples : 0f;
        for (int i = 0; i < n; i++)
        {
            if (best[i] < float.MaxValue)
            {
                r.fastestLap = Mathf.Min(r.fastestLap, best[i]);
                r.slowestBest = Mathf.Max(r.slowestBest, best[i]);
            }
        }
        for (int k = 0; k < n; k++)
        {
            int i = order[k];
            r.lines.Add($"  P{k + 1,2} Car{i:D2} qual {quals[i]:0.00} commit {(float)splineType.GetField("cornerCommitment").GetValue(splines[i]):0.000} best {best[i]:0.00} s laps {lapsDone[i]}");
        }
        return r;
    }

    static float Penetration(Vector2 pa, Vector2 fa, Vector2 pb, Vector2 fb)
    {
        Vector2 ra = new Vector2(-fa.y, fa.x), rb = new Vector2(-fb.y, fb.x);
        Vector2 d = pb - pa;
        float min = float.MaxValue;
        foreach (var axis in new[] { fa, ra, fb, rb })
        {
            float projA = HalfLength * Mathf.Abs(Vector2.Dot(fa, axis)) + HalfWidth * Mathf.Abs(Vector2.Dot(ra, axis));
            float projB = HalfLength * Mathf.Abs(Vector2.Dot(fb, axis)) + HalfWidth * Mathf.Abs(Vector2.Dot(rb, axis));
            float o = projA + projB - Mathf.Abs(Vector2.Dot(d, axis));
            if (o < min) min = o;
        }
        return min;
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(target, null);
    }
}
