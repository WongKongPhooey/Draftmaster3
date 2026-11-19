using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// A pile-up right across the road under GREEN, with the live race's dynamic AI coming up behind it (AIRacingBehaviour
// -> SplineInputDriver -> PlayerVehicleController, built and stepped by hand as OvalPassingTests builds its field).
// No caution is thrown here (there is no FullCourseCaution / CautionWatch in the sim), so this is the racing brain's
// own way round a stopped car: the pass round it, and the stuck recovery once the field has stalled behind it - with
// the grass (AIRacingBehaviour.grassToAvoid, SplineDriver.allowGrass) as the last resort when the wreck fills the road.
//
// There is no physics here, so a car that drives into a wreck car goes through it: contacts are counted from the car
// boxes. Live, each of those is a crash or a car wedged against the wreck - stopped and stuck. 2026-11-19, before the
// fixes in AIRacingBehaviour / SplineDriver.PathPointAhead: every car got past but through it - 10 wreck contacts, the
// leader at 87 mph (it held its offset off a racing line that crossed the road, and read the turned wreck cars by
// their noses); the second car, its one way round taken by the first, turned the other way into the rest of the pile.
// With them, before the wreck itself stopped counting as a car alongside, the field stopped behind it for good (the
// reported bug). Now: 6/6 past in 17 s, 2 wreck contacts at ~11 mph (cars inching out from behind it).
//
// The caution side of the same thing (FormationController.escapeWrecks) is
// FormationLapSimTests.APileUpAcrossTheRoadIsDrivenRoundUnderACaution.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class WreckEscapeGreenSimTests
{
    const float HalfLength = 2.4f, HalfWidth = 1.0f;   // car box, as the other sims
    const int TraceCar = 1;   // whose approach to the wreck the report traces
    readonly List<GameObject> _spawned = new List<GameObject>();
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
        // Out of RaceField first: OnEnable was called by hand, so destroying in edit mode never runs OnDisable.
        var splineType = Runtime("SplineDriver");
        foreach (var go in _spawned)
            if (go != null && go.TryGetComponent(splineType, out var sd)) Call(sd, "OnDisable");
        foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
    }

    // grass = AIRacingBehaviour.grassToAvoid. Off is a diagnostic: the field held behind the wreck.
    [TestCase("WatkinsGlen", true)]
    [TestCase("WatkinsGlen", false, Explicit = true, Reason = "Diagnostic: the same with grassToAvoid off")]
    public void APileUpAcrossTheRoadIsDrivenRoundUnderGreen(string trackId, bool grass)
    {
        var raceStart = Runtime("RaceStart");
        raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        raceStart.GetMethod("GreenFlagNow").Invoke(null, null);

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
        var racingType = Runtime("AIRacingBehaviour");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");
        var bandType = Runtime("GrassEscapeBand");
        var track = (Component)_package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        // The wreck cars: kinematic SplineDrivers, nothing driving them (as AIRetirement leaves a wrecked car).
        Component NewWreckCar(int k)
        {
            var go = new GameObject($"Wreck{k}");
            _spawned.Add(go);
            var s = go.AddComponent(splineType);
            Set(s, "track", track);
            Set(s, "vehicleInfo", vehicleInfo);
            Set(s, "loop", true);
            Set(s, "spawnInPit", false);
            Set(s, "spriteFacesUp", false);
            Set(s, "angleOffsetDeg", 180f);
            Set(s, "externalMotionController", false);
            Set(s, "speed", 0f);
            Set(s, "aiMaxSpeedMph", 0f);
            Call(s, "Awake");
            Call(s, "OnEnable");
            Call(s, "Start");
            return s;
        }
        var probe = NewWreckCar(0);
        var samples = (System.Collections.IList)splineType.GetField("_mainSamples", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(probe);
        Assert.IsNotNull(samples);
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
        var band = bandType.GetMethod("For").Invoke(null, new object[] { track, samples, (float)splineType.GetField("grassEscapeMetres").GetValue(probe) });
        Assert.IsNotNull(band, "no grass band");
        var bandLo = (float[])bandType.GetField("Lo").GetValue(band);
        var bandHi = (float[])bandType.GetField("Hi").GetValue(band);

        // Where: the straightest stretch (160 m back, 40 m on) with room for a car on the grass on at least one side
        // the whole way past - so the wreck is met at speed, on a straight, and there IS a way round, off the road.
        int Index(float d)
        {
            d = Mathf.Repeat(d, lap);
            int lo = 0, hi = n - 1;
            while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (sDist[mid] <= d) lo = mid; else hi = mid - 1; }
            return lo;
        }
        float wreckD = -1f, bestTurn = float.MaxValue;
        for (float d = 0f; d < lap; d += 10f)
        {
            float turn = 0f;
            Vector2 prev = sTan[Index(d - 160f)];
            for (float a = -150f; a <= 40f; a += 10f)
            {
                Vector2 tn = sTan[Index(d + a)];
                turn += Vector2.Angle(prev, tn);
                prev = tn;
            }
            float roomL = float.MaxValue, roomR = float.MaxValue;
            for (float a = -6f; a <= 20f; a += 2f)
            {
                int i = Index(d + a);
                roomL = Mathf.Min(roomL, -bandLo[i] - sWidth[i] * 0.5f);
                roomR = Mathf.Min(roomR, bandHi[i] - sWidth[i] * 0.5f);
            }
            if (Mathf.Max(roomL, roomR) < 5f) continue;
            if (turn < bestTurn) { bestTurn = turn; wreckD = d; }
        }
        Assert.GreaterOrEqual(wreckD, 0f, $"{trackId}: no straight with grass beside it");

        // The wreck: cars stopped side by side from edge to edge, 3 m apart (1 m of daylight - no car fits), ragged
        // and turned across the road.
        int wi = Index(wreckD);
        Vector2 wPos = sPos[wi], wTan = sTan[wi];
        float width = sWidth[wi];
        Vector2 right = new Vector2(wTan.y, -wTan.x);
        int wreckCount = Mathf.CeilToInt((width - 2f) / 3f) + 1;
        var wreck = new List<Component>();
        var wreckLats = new List<float>();
        for (int k = 0; k < wreckCount; k++)
        {
            var s = k == 0 ? probe : NewWreckCar(k);
            float lat = Mathf.Min(-width * 0.5f + 1f + 3f * k, width * 0.5f - 1f);
            wreckLats.Add(lat);
            Vector2 local = wPos + wTan * ((k % 2) * 1.5f) + right * lat;
            var tf = s.transform;
            Vector3 world = track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
            tf.position = new Vector3(world.x, world.y, tf.position.z);
            Vector2 tWorld = track.transform.TransformVector(new Vector3(wTan.x, wTan.y, 0f));
            float facing = Mathf.Atan2(tWorld.y, tWorld.x) * Mathf.Rad2Deg + (k % 2 == 0 ? 20f : -25f);
            tf.rotation = Quaternion.Euler(0f, 0f, facing + 180f);
            splineType.GetMethod("EngageFromCurrentPose").Invoke(s, new object[] { 0f });
            Set(s, "aiMaxSpeedMph", 0f);
            wreck.Add(s);
        }

        // The field: dynamic AI single file, 30 m apart, the leader 320 m short of the wreck, at 30 m/s.
        const int FieldCars = 6;
        var field = new List<Component>();
        var racers = new List<Component>();
        var pvcs = new List<Component>();
        var steps = new List<Action>();
        for (int i = 0; i < FieldCars; i++)
        {
            var go = new GameObject($"AI{i}");
            _spawned.Add(go);
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "cornerSpeedScale", 0.95f);
            Set(spline, "externalMotionController", true);
            Set(spline, "loop", true);
            Set(spline, "spawnInPit", false);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", 30f);
            Set(spline, "staggeredLaunch", false);
            var racing = go.AddComponent(racingType);
            Set(racing, "mistakeProbabilityPerSecond", 0f);
            Set(racing, "grassToAvoid", grass);
            var pvc = go.AddComponent(pvcType);
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
            Set(pvc, "corneringStiffness", 15f);   // GridSpawner.aiCorneringStiffness
            var input = go.AddComponent(inputType);
            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(racing, "Awake");
            Call(input, "Awake");
            Call(pvc, "Start");
            Call(spline, "Start");
            Set(spline, "startDistance", Mathf.Repeat(wreckD - 320f - i * 30f, lap));
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
            Call(input, "OnEnable");
            field.Add(spline); racers.Add(racing); pvcs.Add(pvc);
            steps.Add(Step(racing)); steps.Add(Step(input)); steps.Add(Step(pvc)); steps.Add(Step(spline));
        }
        foreach (var w in wreck) steps.Add(Step(w));

        var centreProp = splineType.GetProperty("CentreDistanceOnTrack");
        var latProp = splineType.GetProperty("CarLateral");
        var mphProp = splineType.GetProperty("CurrentMph");
        var allowGrassField = splineType.GetField("allowGrass");
        var speedMps = pvcType.GetProperty("SpeedMps");
        var greenTime = raceStart.GetField("<GreenTime>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(greenTime, "RaceStart.GreenTime is gone.");

        // How far each car has to go to be past the wreck (the last of it is 1.5 m up the road), unwrapped.
        var toGo = new float[FieldCars];
        var prevD = new float[FieldCars];
        for (int i = 0; i < FieldCars; i++)
        {
            prevD[i] = Mathf.Repeat((float)centreProp.GetValue(field[i]), lap);
            toGo[i] = Mathf.Repeat(wreckD - prevD[i], lap) + 1.5f + HalfLength * 2f + 2f;
        }

        var all = new List<Component>(field);
        all.AddRange(wreck);
        int m = all.Count;
        var touching = new HashSet<long>();
        var carHits = new List<string>();
        var hardCarHits = new List<string>();
        var wreckHits = new List<string>();
        var passedAt = new Dictionary<int, float>();
        int grassSteps = 0;
        float maxOut = 0f, maxWreckHitMph = 0f;
        var trace = new System.Text.StringBuilder();
        var wreckStart = new float[wreck.Count];
        for (int k = 0; k < wreck.Count; k++) wreckStart[k] = (float)centreProp.GetValue(wreck[k]);
        var capField = splineType.GetField("aiMaxSpeedMph");
        float dt = Time.fixedDeltaTime, t = 0f;
        int maxSteps = Mathf.RoundToInt(120f / dt);
        for (int step = 0; step < maxSteps && passedAt.Count < FieldCars; step++)
        {
            t = (step + 1) * dt;
            greenTime.SetValue(null, Time.time - 30f - t);   // well past the start
            foreach (var w in wreck) Set(w, "aiMaxSpeedMph", 0f);
            foreach (var s in steps) s();

            for (int i = 0; i < FieldCars; i++)
            {
                float d = Mathf.Repeat((float)centreProp.GetValue(field[i]), lap);
                float delta = Mathf.Repeat(d - prevD[i] + lap * 0.5f, lap) - lap * 0.5f;
                prevD[i] = d;
                if (toGo[i] > 0f)
                {
                    toGo[i] -= delta;
                    if (toGo[i] <= 0f) passedAt[i] = t;
                }
                if ((bool)allowGrassField.GetValue(field[i])) grassSteps++;
                if (i == TraceCar && step % 10 == 0 && toGo[i] < 90f && toGo[i] > 0f)
                {
                    object R(string f) => racingType.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(racers[TraceCar]);
                    var bArgs = new object[] { 0f, true, false, 0f, 0f };
                    splineType.GetMethod("GetBoundsAhead", new[] { typeof(float), typeof(bool), typeof(bool), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }).Invoke(field[TraceCar], bArgs);
                    trace.Append($"\n    {toGo[i]:0} m {(float)speedMps.GetValue(pvcs[i]) * 2.237f:0} mph cap {Mathf.Min(999f, (float)capField.GetValue(field[i])):0} " +
                                 $"car {(float)latProp.GetValue(field[i]):0.0} path {(float)splineType.GetProperty("LateralOnTrack").GetValue(field[i]):0.0} " +
                                 $"tac {(float)splineType.GetField("tacticalLateralOffset").GetValue(field[i]):0.0} " +
                                 $"runOff {splineType.GetField("allowRunOff").GetValue(field[i])} bounds {(float)bArgs[3]:0.0}..{(float)bArgs[4]:0.0} " +
                                 $"around {R("_passAroundStopped")} at {(float)R("_passTargetLat"):0.0}+{(float)R("_passClearance"):0.0} " +
                                 $"commit {(float)R("_commitTimer"):0.0} dir {(float)R("_commitDir"):0} rec {(float)R("_recoveryTimer"):0.0}");
                }
                float halfW = sWidth[Index(d)] * 0.5f;
                maxOut = Mathf.Max(maxOut, Mathf.Abs((float)latProp.GetValue(field[i])) - halfW);
            }

            // Contacts: car boxes in track space, from the transforms (a dynamic car is where its body is).
            for (int a = 0; a < m; a++)
            for (int b = a + 1; b < m; b++)
            {
                bool aw = a >= FieldCars, bw = b >= FieldCars;
                if (aw && bw) continue;
                Vector2 pa = all[a].transform.position, pb = all[b].transform.position;
                if ((pa - pb).sqrMagnitude > 64f) { touching.Remove((long)a * 1000 + b); continue; }
                long key = (long)a * 1000 + b;
                if (!Overlap(pa, Facing(all[a].transform), pb, Facing(all[b].transform))) { touching.Remove(key); continue; }
                if (!touching.Add(key)) continue;
                float va = a < FieldCars ? (float)speedMps.GetValue(pvcs[a]) * 2.237f : 0f;
                float vb = b < FieldCars ? (float)speedMps.GetValue(pvcs[b]) * 2.237f : 0f;
                string what = $"t{t:0.0} {all[a].name} / {all[b].name} ({va:0} / {vb:0} mph)";
                if (aw || bw) { wreckHits.Add(what); maxWreckHitMph = Mathf.Max(maxWreckHitMph, Mathf.Max(va, vb)); }
                else
                {
                    carHits.Add(what);
                    if (Mathf.Abs(va - vb) >= 10f) hardCarHits.Add(what);
                }
            }
        }

        var stuck = new List<string>();
        for (int i = 0; i < FieldCars; i++)
            if (!passedAt.ContainsKey(i))
                stuck.Add($"{field[i].name} {toGo[i]:0} m short at {(float)mphProp.GetValue(field[i]):0} mph, lat {(float)latProp.GetValue(field[i]):0.0}");
        var times = new List<string>();
        foreach (var kv in passedAt) times.Add($"{field[kv.Key].name} t{kv.Value:0}");
        string summary = $"[GreenPileUpSim] {trackId} grass={grass}: {wreckCount} cars across {width:0.0} m at {wreckD:0} m " +
                         $"(lats {string.Join(", ", wreckLats.ConvertAll(l => l.ToString("0.0")))}); " +
                         $"{passedAt.Count}/{FieldCars} past in {t:0} s, furthest out {maxOut:0.0} m past the edge, " +
                         $"{grassSteps * dt:0.0} car-s with the grass open; {carHits.Count} contacts between running cars " +
                         $"({hardCarHits.Count} closing at 10 mph+), {wreckHits.Count} with the wreck" +
                         $"\n  passed: {string.Join(", ", times)}\n  stuck: {string.Join("; ", stuck)}" +
                         $"\n  car contacts: {string.Join("; ", carHits)}\n  wreck contacts: {string.Join("; ", wreckHits)}";
        var wreckEnd = new List<string>();
        for (int k = 0; k < wreck.Count; k++)
            wreckEnd.Add($"{wreck[k].name} {wreckStart[k]:0.0}->{(float)centreProp.GetValue(wreck[k]):0.0} m {(float)mphProp.GetValue(wreck[k]):0} mph lat {(float)splineType.GetProperty("LateralOnTrack").GetValue(wreck[k]):0.0}");
        summary += "\n  wreck: " + string.Join("; ", wreckEnd) + $"\n  AI{TraceCar} approach:" + trace;
        Debug.Log(summary);
        if (!grass) return;   // diagnostic only: Watkins Glen's paved run-off may be enough on its own
        Assert.AreEqual(FieldCars, passedAt.Count, "every car should get round the wreck. " + summary);
        Assert.Greater(maxOut, 0f, "nobody went off the road to get round a wreck that fills it. " + summary);
        // Nudging a wreck car while inching out from behind it is one thing; driving into it is a crash.
        Assert.Less(maxWreckHitMph, 25f, "a car drove into the wreck. " + summary);
    }

    // World heading of a car's nose (SplineDriver.BodyLateralExtent's convention: sprite faces left, offset 180).
    static Vector2 Facing(Transform tf)
    {
        float deg = (tf.eulerAngles.z - 180f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(deg), Mathf.Sin(deg));
    }

    // Two car boxes overlap (separating axis test).
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
        var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        mi?.Invoke(target, null);
    }
}
