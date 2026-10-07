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
        public int laps;
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
        $"{r.weavesPerCarLap:0.00} weaves/car/lap\n";

    // The superspeedway baseline: a full Cup field at Daytona across several rosters. One line per seed and the
    // totals, so a change to the AI can be judged against it rather than against one lucky draw.
    [Explicit("Diagnostic: a 40-car pack at Daytona, three rosters - the superspeedway baseline.")]
    [Test, Timeout(1800000)]
    public void DaytonaBaseline() => Baseline("Daytona", new[] { 1, 2, 3 }, 40, 6);

    [Explicit("Diagnostic: the same baseline at Talladega.")]
    [Test, Timeout(1800000)]
    public void TalladegaBaseline() => Baseline("Talladega", new[] { 1, 2, 3 }, 40, 6);

    [Explicit("Diagnostic: one Daytona roster with every off and the finishing order logged.")]
    [Test, Timeout(1800000)]
    public void Daytona() => Log("daytona seed 1", Run("Daytona", new Settings { cars = 40, laps = 6, seed = 1 }));

    [Explicit("Diagnostic: one Daytona roster, the last two seconds before each off, step by step.")]
    [Test, Timeout(1800000)]
    public void DaytonaTraceOffs() => Log("daytona trace offs", Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, traceOffs = true }));

    [Explicit("Diagnostic: one Daytona roster, what each close-following car is deciding, twice a second.")]
    [Test, Timeout(1800000)]
    public void DaytonaTraceAttacks() => Log("daytona trace attacks", Run("Daytona", new Settings { cars = 40, laps = 3, seed = 1, traceLines = 120 }));

    void Baseline(string trackId, int[] seeds, int cars, int laps)
    {
        var sb = new StringBuilder($"[PackSim] {trackId} baseline, {cars} cars, {laps} laps\n");
        float passes = 0, contacts = 0, offs = 0, w2 = 0, w3 = 0, rms = 0, weaves = 0, tow = 0;
        foreach (int seed in seeds)
        {
            var r = Run(trackId, new Settings { cars = cars, laps = laps, seed = seed });
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
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject($"Car{i:D2}");
            _cars.Add(go);

            // A spread of ratings like the real roster (qualifying 8..20 of 20), shuffled against grid order so
            // the quick cars start behind slow ones and have to pass.
            float q01 = Mathf.Lerp(0.4f, 1f, ((i * 5) % n) / (float)(n - 1));
            float cons01 = Mathf.Lerp(0.65f, 0.95f, ((i * 7) % n) / (float)(n - 1));
            float agg01 = Mathf.Lerp(0.3f, 0.9f, ((i * 3) % n) / (float)(n - 1));
            quals[i] = q01;

            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", _vehicleCopy);
            Set(spline, "cornerSpeedScale", 0.95f);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", 35f);
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
            if (set.followHeadway.HasValue) Set(racing, "followHeadwaySeconds", set.followHeadway.Value);
            Set(racing, "respectYellows", set.respectYellows);

            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(input, "Awake");
            Call(input, "OnEnable");
            Call(racing, "Awake");
            racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { basePace });
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
        int maxSteps = Mathf.RoundToInt((set.laps + 1) * 150f / dt);
        var tacField = splineType.GetField("tacticalLateralOffset");
        int shapeSamples = 0, wide2 = 0, wide3 = 0;
        double aloneSq = 0; int aloneSamples = 0; float aloneMax = 0f;
        var prevTac = new float[n];
        var tacTrend = new float[n];   // sign of the last real move of the intended offset
        int weaves = 0;
        float t = 0f;

        for (int step = 0; step < maxSteps; step++)
        {
            for (int i = 0; i < n; i++)
            {
                if (stopped && i == 0) splineType.GetField("aiMaxSpeedMph").SetValue(splines[0], 0f);
                else racingStep.Invoke(racers[i], null);
                inputStep.Invoke(inputs[i], null);
                pvcStep.Invoke(pvcs[i], null);
                splineStep.Invoke(splines[i], null);
            }
            t += dt;

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
                if (!on && wasOn[i] && lapsDone[i] >= 1)
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

            // A close car's decision-making, twice a second: what it is chasing, what it may do, where it is.
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
            if (leaderLaps >= 2 && step % 5 == 0)
            {
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
            if (lapsDone[0] >= 1)
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
                    if (touching.Add(key)) { r.contacts++; if (stopD >= 0f && i == 0) r.stoppedHits++; }
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
        r.wide2Share = shapeSamples > 0 ? wide2 / (float)shapeSamples : 0f;
        r.wide3Share = shapeSamples > 0 ? wide3 / (float)shapeSamples : 0f;
        r.aloneLineRms = aloneSamples > 0 ? (float)System.Math.Sqrt(aloneSq / aloneSamples) : 0f;
        r.aloneLineMax = aloneMax;
        int racedLaps = 0;
        for (int i = 0; i < n; i++) racedLaps += Mathf.Max(0, lapsDone[i] - 1);
        r.weavesPerCarLap = racedLaps > 0 ? weaves / (float)racedLaps : 0f;

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
