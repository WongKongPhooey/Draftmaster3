using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The player's car on Auto (AIHandover.ToAI: the racing AI on the car's own dynamic model) coming up on ONE crashed car
// sat on the road, under green, with CautionWatch ticking on the sim's clock - the local yellow, the wreck list, and the
// racing brain's own way round a stopped car. Reported 2026-11-20: "approaches a stationary crashed car, slows right
// down but still hits it, when it could have just driven around it."
//
// The crashed car is a live one: a dynamic-model car (PlayerVehicleController + SplineDriver driving it externally)
// whose engine is gone - no throttle, on the brakes, as AIRetirement leaves a broken car - spun to a standstill
// somewhere across the road, its brain still running. Its brain's plan (SplineDriver.LateralOnTrack) is not where its
// body is (CarLateral / BodyLateralExtent): a wreck that has slid off its line still plans to be on it.
//
// Before the fix (2026-11-20) Daytona hit it in 2 of 5 scenarios, at 14 and 21 mph, having braked from 110 mph to ~10:
// AIHandover.ToAI mid-race left the brain thinking the green had just fallen, so in a pack it held the lateral it was
// handed over on while under race speed (AIRacingBehaviour.RestartSettling) - and a car braking for a wreck is always
// under race speed. It only started out round the wreck ~20 m short, at a crawl. Now a handover under green is not a
// start (AIRacingBehaviour.JoinRunningRace), and a real restart's settle never holds a car behind a stopped one
// (asRestart). WatkinsGlen was clean before and after. Tried and dropped: judging the stop-short cap's "clear of it" by
// the car bodies rather than the brains' plans - not needed by any case here, and in WreckEscapeGreenSimTests it traded
// the two wreck contacts for two more car-to-car ones.
//
// Each scenario: the wreck at one place and lateral across the road, the Auto car 350 m short of it at race pace, run
// until it is past or 40 s. No physics, so contacts are geometric (car boxes 4.8 x 2.0 m from the transforms) and a car
// that drives into the wreck goes through it - live, that is the crash. Everything is by reflection: the runtime types
// live in Assembly-CSharp, which a test assembly can't reference.
public class AutoStoppedCarSimTests
{
    const float HalfLength = 2.4f, HalfWidth = 1f;
    const float StartBack = 350f;   // the usual: Auto has the car well before the wreck
    readonly List<GameObject> _spawned = new List<GameObject>();
    readonly List<Component> _enabled = new List<Component>();
    GameObject _package;

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
        Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        Runtime("CautionWatch").GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "" });
    }

    void Clear()
    {
        foreach (var c in _enabled) if (c != null) Call(c, "OnDisable");
        _enabled.Clear();
        foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        Runtime("CautionWatch").GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
    }

    // asRestart: the brain sees its first green step as a start / restart (as every car does off a caution) rather than
    // a mid-race handover - in a pack it holds the formation it lined up in until up to speed, but never past a wreck.
    // startBack: how far short of the wreck Auto takes the car (at race pace). 120 m at Daytona is under a second and a
    // half: braking and steering at once, where the stop short of it is what keeps the car off it.
    [TestCase("WatkinsGlen", false, StartBack)]
    [TestCase("WatkinsGlen", false, 120f)]
    [TestCase("Daytona", false, StartBack)]
    [TestCase("Daytona", true, StartBack)]
    [TestCase("Daytona", false, 120f)]
    public void AnAutoCarDrivesRoundOneStoppedCar(string trackId, bool asRestart, float startBack)
    {
        UnityEngine.Random.InitState(5);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { trackId });
        var raceStart = Runtime("RaceStart");
        raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
        var greenTime = raceStart.GetField("<GreenTime>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(greenTime, "RaceStart.GreenTime is gone.");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"track package missing for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
        Physics2D.SyncTransforms();
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);

        var trackType = Runtime("TrackBuilder");
        var splineType = Runtime("SplineDriver");
        var track = (Component)_package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        // The road, read off a probe brain: where the straightest stretch and the tightest corner are, and the racing line.
        var probeGo = new GameObject("Probe");
        _spawned.Add(probeGo);
        var probe = NewSpline(probeGo, track, vehicleInfo);
        Call(probe, "Awake");
        Call(probe, "Start");
        var samples = (System.Collections.IList)splineType.GetField("_mainSamples", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(probe);
        float lap = (float)splineType.GetProperty("TrackLength").GetValue(probe);
        int n = samples.Count;
        var sType = samples[0].GetType();
        var sPos = new Vector2[n]; var sTan = new Vector2[n]; var sWidth = new float[n]; var sDist = new float[n];
        for (int i = 0; i < n; i++)
        {
            sPos[i] = (Vector2)sType.GetField("position").GetValue(samples[i]);
            sTan[i] = (Vector2)sType.GetField("tangent").GetValue(samples[i]);
            sWidth[i] = (float)sType.GetField("width").GetValue(samples[i]);
            sDist[i] = (float)sType.GetField("distance").GetValue(samples[i]);
        }
        var lateralAt = splineType.GetMethod("LateralAt", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
        Assert.IsNotNull(lateralAt, "SplineDriver.LateralAt(float) is gone.");
        int Index(float d)
        {
            d = Mathf.Repeat(d, lap);
            int lo = 0, hi = n - 1;
            while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (sDist[mid] <= d) lo = mid; else hi = mid - 1; }
            return lo;
        }
        float Turn(float d, float back, float on)
        {
            float turn = 0f;
            Vector2 prev = sTan[Index(d - back)];
            for (float a = -back + 10f; a <= on; a += 10f) { Vector2 tn = sTan[Index(d + a)]; turn += Vector2.Angle(prev, tn); prev = tn; }
            return turn;
        }
        float straightD = 0f, cornerD = 0f, best = float.MaxValue, worst = -1f;
        for (float d = 0f; d < lap; d += 10f)
        {
            float s = Turn(d, 200f, 40f);
            if (s < best) { best = s; straightD = d; }
            float c = Turn(d, 20f, 20f);
            if (c > worst) { worst = c; cornerD = d; }
        }
        float lineStraight = (float)lateralAt.Invoke(probe, new object[] { straightD });
        float lineCorner = (float)lateralAt.Invoke(probe, new object[] { cornerD });

        // The scenarios: where the wreck is, where across the road its body sits, how it is turned.
        var scenarios = new List<(string name, float d, float lat, float yaw)>
        {
            ("straight, on the line",            straightD, lineStraight, 35f),
            ("straight, half on the line",       straightD, lineStraight + 1.6f * Mathf.Sign(-lineStraight + 1e-3f), 80f),
            ("straight, mid-road",               straightD, 0f, 150f),
            ("corner, on the line",              cornerD, lineCorner, 60f),
            ("corner, half on the line",         cornerD, lineCorner + 1.6f * Mathf.Sign(-lineCorner + 1e-3f), 100f),
        };

        var report = new StringBuilder($"[AutoStoppedCarSim] {trackId}{(asRestart ? " restart" : "")}, Auto {startBack:0} m short (lap {lap:0} m)");
        var failures = new List<string>();
        foreach (var sc in scenarios)
        {
            int wi = Index(sc.d);
            float half = sWidth[wi] * 0.5f;
            float lat = Mathf.Clamp(sc.lat, -half + 1.2f, half - 1.2f);
            string result = RunScenario(trackId, track, vehicleInfo, lap, sc.d, lat, sc.yaw, sPos[wi], sTan[wi], sWidth[wi],
                                        greenTime, asRestart, startBack, out bool hit, out float hitMph, out bool passed, out string trace);
            report.Append($"\n  {sc.name} (wreck at {sc.d:0} m, lat {lat:0.0} of +-{half:0.0}, yaw {sc.yaw:0}): {result}");
            if (hit && hitMph >= 3f) { failures.Add($"{sc.name}: hit the wreck at {hitMph:0} mph"); report.Append(trace); }
            else if (!passed) { failures.Add($"{sc.name}: never got past"); report.Append(trace); }
        }
        Debug.Log(report.ToString());
        Assert.IsEmpty(failures, string.Join("; ", failures) + "\n" + report);
    }

    string RunScenario(string trackId, Component track, UnityEngine.Object vehicleInfo, float lap, float wreckD, float wreckLat,
                       float wreckYaw, Vector2 wPos, Vector2 wTan, float width, FieldInfo greenTime, bool asRestart, float startBack,
                       out bool hit, out float hitMph, out bool passed, out string traceOut)
    {
        Clear();
        var splineType = Runtime("SplineDriver");
        var racingType = Runtime("AIRacingBehaviour");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        var watchGo = new GameObject("CautionWatch");
        _spawned.Add(watchGo);
        var watch = watchGo.AddComponent(Runtime("CautionWatch"));
        Call(watch, "Awake");
        float clock = 1000f;

        // The crashed car: a field car, built as GridSpawner builds one, spun to a stop across the road.
        var wGo = new GameObject("Wreck");
        _spawned.Add(wGo);
        var wSpline = NewSpline(wGo, track, vehicleInfo);
        Set(wSpline, "cornerSpeedScale", 0.95f);
        Set(wSpline, "externalMotionController", true);
        var wRacing = wGo.AddComponent(racingType);
        Set(wRacing, "mistakeProbabilityPerSecond", 0f);
        var wPvc = NewPvc(wGo, track, vehicleInfo, 15f);
        Call(wSpline, "Awake");
        Call(wSpline, "OnEnable");
        _enabled.Add(wSpline);
        Call(wRacing, "Awake");
        Call(wPvc, "Start");
        Call(wSpline, "Start");
        Set(wSpline, "startDistance", Mathf.Repeat(wreckD - 15f, lap));
        splineType.GetMethod("PlaceAtStartDistance").Invoke(wSpline, null);
        // Its brain has been driving its line up to here.
        for (int k = 0; k < 10; k++) Call(wSpline, "FixedUpdate");
        Vector2 right = new Vector2(wTan.y, -wTan.x);
        Vector2 local = wPos + right * wreckLat;
        Vector3 world = track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
        Vector2 tWorld = track.transform.TransformVector(new Vector3(wTan.x, wTan.y, 0f));
        float roadDeg = Mathf.Atan2(tWorld.y, tWorld.x) * Mathf.Rad2Deg;
        pvcType.GetMethod("SeedPose").Invoke(wPvc, new object[] { (Vector2)world, roadDeg + wreckYaw, 0f });
        Set(wSpline, "aiMaxSpeedMph", 0f);
        Set(wSpline, "externalActualSpeedMps", 0f);
        Call(wPvc, "OnEnable");
        _enabled.Add(wPvc);
        Action wRacingStep = Step(wRacing), wPvcStep = Step(wPvc), wSplineStep = Step(wSpline);
        var setInput = pvcType.GetMethod("SetInput");
        var speedMps = pvcType.GetProperty("SpeedMps");

        // The player's car, the human's (spline off, the model under the human's hand), then handed to Auto.
        var go = new GameObject("Player");
        _spawned.Add(go);
        var spline = NewSpline(go, track, vehicleInfo);
        var pvc = NewPvc(go, track, vehicleInfo, 13f);   // the player's car keeps its own (RaceScene's PlayerCar)
        Set(pvc, "externalInput", false);
        Set(pvc, "enableWheelspin", true);
        Set(pvc, "damageImpairsHandling", true);
        Call(spline, "Awake");
        Call(pvc, "Start");
        Call(spline, "Start");
        float startD = Mathf.Repeat(wreckD - startBack, lap);
        Set(spline, "startDistance", startD);
        splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
        float paceMph = (float)splineType.GetMethod("RacePaceMphAt", new[] { typeof(float) }).Invoke(spline, new object[] { startD });
        float heading = (float)splineType.GetMethod("MainTangentWorldDeg").Invoke(spline, new object[] { startD });
        pvcType.GetMethod("SeedPose").Invoke(pvc, new object[] { (Vector2)go.transform.position, heading, Mathf.Min(paceMph, 180f) * 0.85f / 2.237f });
        ((Behaviour)spline).enabled = false;
        Call(pvc, "OnEnable");
        // DriveModeController.EnterAuto: GridSpawner's corner scale, then AIHandover.ToAI with GridSpawner's gains.
        Set(spline, "cornerSpeedScale", 0.95f);
        bool taken = (bool)Runtime("AIHandover").GetMethod("ToAI").Invoke(null, new object[] { go, 1.5f, 2f });
        Assert.IsTrue(taken, "AIHandover.ToAI refused the player's car.");
        Call(pvc, "OnDisable");
        Call(spline, "OnEnable");
        _enabled.Add(spline);
        var racing = go.GetComponent(racingType);
        Call(racing, "Awake");
        Set(racing, "mistakeProbabilityPerSecond", 0f);
        if (asRestart) Set(racing, "_joinedRunning", false);
        var input = go.GetComponent(inputType);
        Call(input, "Awake");
        Call(input, "OnEnable");
        Call(pvc, "OnEnable");
        _enabled.Add(pvc);
        Action[] steps = { Step(racing), Step(input), Step(pvc), Step(spline) };

        var centreProp = splineType.GetProperty("CentreDistanceOnTrack");
        var latProp = splineType.GetProperty("CarLateral");
        var planProp = splineType.GetProperty("LateralOnTrack");
        var capField = splineType.GetField("aiMaxSpeedMph");
        var trace = new StringBuilder("\n    approach:");
        float toGo = startBack + HalfLength * 2f + 3f;
        float prevD = Mathf.Repeat((float)centreProp.GetValue(spline), lap);
        float dt = Time.fixedDeltaTime, t = 0f, minMph = float.MaxValue, minGap = float.MaxValue;
        hit = false; hitMph = 0f; passed = false;
        bool touching = false;
        int maxSteps = Mathf.RoundToInt(40f / dt);
        for (int step = 0; step < maxSteps; step++)
        {
            t = (step + 1) * dt;
            greenTime.SetValue(null, Time.time - 60f - t);
            clock += dt;
            Runtime("CautionWatch").GetMethod("Tick").Invoke(watch, new object[] { clock });
            foreach (var s in steps) s();
            // The wreck: brain running, engine gone - no throttle, on the brakes (AIRetirement's broken car).
            Set(wSpline, "aiMaxSpeedMph", 0f);
            wRacingStep();
            setInput.Invoke(wPvc, new object[] { 0f, 0f, 1f });
            wPvcStep();
            Set(wSpline, "externalActualSpeedMps", (float)speedMps.GetValue(wPvc));
            wSplineStep();

            float d = Mathf.Repeat((float)centreProp.GetValue(spline), lap);
            float delta = Mathf.Repeat(d - prevD + lap * 0.5f, lap) - lap * 0.5f;
            prevD = d;
            toGo -= delta;
            float mph = (float)speedMps.GetValue(pvc) * 2.237f;
            Vector2 pa = go.transform.position, pb = wGo.transform.position;
            if (toGo < startBack * 0.4f) minMph = Mathf.Min(minMph, mph);
            float gap = (pa - pb).magnitude;
            minGap = Mathf.Min(minGap, gap);
            bool overlap = gap < 6f && Overlap(pa, Facing(go.transform), pb, Facing(wGo.transform));
            if (overlap && !touching) { hit = true; hitMph = Mathf.Max(hitMph, mph); }
            touching = overlap;
            if (step % 10 == 0 && toGo < 140f && toGo > -5f)
            {
                object R(string f) => racing.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racing);
                trace.Append($"\n    {toGo:0} m {mph:0} mph cap {Mathf.Min(999f, (float)capField.GetValue(spline)):0} car {(float)latProp.GetValue(spline):0.0} " +
                             $"plan {(float)planProp.GetValue(spline):0.0} tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(spline):0.0} | " +
                             $"wreck car {(float)latProp.GetValue(wSpline):0.0} plan {(float)planProp.GetValue(wSpline):0.0} | " +
                             $"around {R("_passAroundStopped")} at {(float)R("_passTargetLat"):0.0}+{(float)R("_passClearance"):0.0} " +
                             $"commit {(float)R("_commitTimer"):0.0} dir {(float)R("_commitDir"):0} rec {(float)R("_recoveryTimer"):0.0} " +
                             $"yellow {racing.GetType().GetProperty("UnderYellow")?.GetValue(racing)}" + (overlap ? " HIT" : ""));
            }
            if (toGo <= 0f) { passed = true; break; }
        }
        traceOut = trace.ToString();
        float wMoved = Vector2.Distance(wGo.transform.position, world);
        return (passed ? $"past in {t:0.0} s" : $"NOT past, {toGo:0} m short") +
               $", slowest {minMph:0} mph, nearest {minGap:0.0} m centre to centre" +
               (hit ? $", HIT at {hitMph:0} mph" : ", no contact") + $" (wreck moved {wMoved:0.0} m)";
    }

    Component NewSpline(GameObject go, Component track, UnityEngine.Object vehicleInfo)
    {
        var s = go.AddComponent(Runtime("SplineDriver"));
        Set(s, "track", track);
        Set(s, "vehicleInfo", vehicleInfo);
        Set(s, "loop", true);
        Set(s, "spawnInPit", false);
        Set(s, "spriteFacesUp", false);
        Set(s, "angleOffsetDeg", 180f);
        Set(s, "staggeredLaunch", false);
        return s;
    }

    Component NewPvc(GameObject go, Component track, UnityEngine.Object vehicleInfo, float corneringStiffness)
    {
        var pvc = go.AddComponent(Runtime("PlayerVehicleController"));
        Set(pvc, "vehicleInfo", vehicleInfo);
        Set(pvc, "track", track);
        Set(pvc, "spriteFacesUp", false);
        Set(pvc, "angleOffsetDeg", 180f);
        Set(pvc, "grassTrails", false);
        Set(pvc, "enableWheelspin", false);
        Set(pvc, "externalInput", true);
        Set(pvc, "damageImpairsHandling", false);
        Set(pvc, "surfaceSpray", false);
        Set(pvc, "impactDebris", false);
        Set(pvc, "corneringStiffness", corneringStiffness);
        return pvc;
    }

    static Vector2 Facing(Transform tf)
    {
        float deg = (tf.eulerAngles.z - 180f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(deg), Mathf.Sin(deg));
    }

    static bool Overlap(Vector2 pa, Vector2 fa, Vector2 pb, Vector2 fb)
    {
        Vector2 sa = new Vector2(-fa.y, fa.x), sb = new Vector2(-fb.y, fb.x);
        Vector2 d = pb - pa;
        foreach (var axis in new[] { fa, sa, fb, sb })
        {
            float ra = HalfLength * Mathf.Abs(Vector2.Dot(fa, axis)) + HalfWidth * Mathf.Abs(Vector2.Dot(sa, axis));
            float rb = HalfLength * Mathf.Abs(Vector2.Dot(fb, axis)) + HalfWidth * Mathf.Abs(Vector2.Dot(sb, axis));
            if (Mathf.Abs(Vector2.Dot(d, axis)) > ra + rb) return false;
        }
        return true;
    }

    static Action Step(Component c)
    {
        var mi = c.GetType().GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(mi, $"{c.GetType().Name}.FixedUpdate is gone.");
        return (Action)Delegate.CreateDelegate(typeof(Action), c, mi);
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static void Call(object target, string method)
    {
        var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        mi?.Invoke(target, null);
    }
}
