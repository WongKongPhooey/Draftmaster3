using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// AI cars leaving pit road onto an oval's apron (SplineDriver's apron exit, ovals of a mile or more) under green, with the
// racing brain (AIRacingBehaviour) on top as PitStopController hands it back at the exit. Reported 2026-11-25: "when the
// AI leave the pits and go around the apron pit exit, sometimes they crawl along at about 30 mph".
//
// One car alone, and two leaving side by side (one per lane of a two-lane pit road), each on its own dynamic model. The
// run is ~12 s from the exit - the turns the apron carries them through. No physics, so nothing pushes the cars apart:
// contact is geometric (4.8 x 2.0 m boxes from the transforms). Everything by reflection: the runtime types live in
// Assembly-CSharp, which a test assembly can't reference.
public class ApronPitExitPaceSimTests
{
    const float HalfLength = 2.4f, HalfWidth = 1f;
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
        foreach (var c in _enabled) if (c != null) Call(c, "OnDisable");
        _enabled.Clear();
        foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        Runtime("CautionWatch").GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "" });
    }

    class Car
    {
        public GameObject go;
        public Component spline, racing, pvc, input;
        public Action racingStep, inputStep, pvcStep, splineStep;
        public bool off;           // off pit road, racing brain handed the car
        public float offAt = -1f;  // sim time it left pit road
        public float exitMph;
        public float slowestMph = float.MaxValue;
        public string slowestWhy = "";
    }

    [TestCase("Daytona", 1)]
    [TestCase("Daytona", 2)]
    [TestCase("FortWorth", 1)]
    [TestCase("FortWorth", 2)]
    public void CarsLeavingOntoTheApron_KeepRacePace(string trackId, int cars)
    {
        UnityEngine.Random.InitState(7);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { trackId });
        var raceStart = Runtime("RaceStart");
        raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        raceStart.GetMethod("GreenFlagNow").Invoke(null, null);
        var greenTime = raceStart.GetField("<GreenTime>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"track package missing for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
        Physics2D.SyncTransforms();
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);

        var track = (Component)_package.GetComponentInChildren(Runtime("TrackBuilder"), true);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");
        var splineType = Runtime("SplineDriver");
        var racingType = Runtime("AIRacingBehaviour");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        var watchGo = new GameObject("CautionWatch");
        _spawned.Add(watchGo);
        var watch = watchGo.AddComponent(Runtime("CautionWatch"));
        Call(watch, "Awake");
        float clock = 1000f;

        // Pit road's lanes: a quarter of its driving width either side of the middle (Draftmaster.Sim.PitLanes).
        float pitWidth = (float)track.GetType().GetProperty("PitDrivingWidth").GetValue(track);
        float laneLat = pitWidth * 0.25f;

        var list = new List<Car>();
        for (int i = 0; i < cars; i++)
        {
            var c = new Car { go = new GameObject($"Car{i}") };
            _spawned.Add(c.go);
            c.spline = c.go.AddComponent(splineType);
            Set(c.spline, "track", track);
            Set(c.spline, "vehicleInfo", vehicleInfo);
            Set(c.spline, "loop", true);
            Set(c.spline, "spawnInPit", true);
            Set(c.spline, "qualifyingPosition", 4);
            Set(c.spline, "spriteFacesUp", false);
            Set(c.spline, "angleOffsetDeg", 180f);
            Set(c.spline, "staggeredLaunch", false);
            Set(c.spline, "cornerSpeedScale", 0.95f);
            Set(c.spline, "externalMotionController", true);
            c.racing = c.go.AddComponent(racingType);
            Set(c.racing, "mistakeProbabilityPerSecond", 0f);
            c.pvc = c.go.AddComponent(pvcType);
            Set(c.pvc, "vehicleInfo", vehicleInfo);
            Set(c.pvc, "track", track);
            Set(c.pvc, "spriteFacesUp", false);
            Set(c.pvc, "angleOffsetDeg", 180f);
            Set(c.pvc, "grassTrails", false);
            Set(c.pvc, "enableWheelspin", false);
            Set(c.pvc, "externalInput", true);
            Set(c.pvc, "damageImpairsHandling", false);
            Set(c.pvc, "surfaceSpray", false);
            Set(c.pvc, "impactDebris", false);
            Set(c.pvc, "corneringStiffness", 15f);
            c.input = c.go.AddComponent(inputType);
            Call(c.spline, "Awake");
            Call(c.input, "Awake");
            Call(c.input, "OnEnable");
            Call(c.pvc, "Start");
            Call(c.spline, "OnEnable");
            _enabled.Add(c.spline);
            Call(c.racing, "Awake");
            Call(c.spline, "Start");
            // Side by side: one per lane of pit road (a lone car down the middle).
            Set(c.spline, "lateralOffset", cars == 1 ? 0f : (i == 0 ? -laneLat : laneLat));
            c.racingStep = Step(c.racing); c.inputStep = Step(c.input); c.pvcStep = Step(c.pvc); c.splineStep = Step(c.spline);
            list.Add(c);
        }

        var isOnPit = splineType.GetProperty("IsOnPit");
        var capField = splineType.GetField("aiMaxSpeedMph");
        var latProp = splineType.GetProperty("CarLateral");
        var grassField = splineType.GetField("allowGrass");
        var runOffField = splineType.GetField("allowRunOff");
        var speedMps = pvcType.GetProperty("SpeedMps");
        object R(Component c, string f) => c.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);

        var report = new StringBuilder($"[ApronPitExit] {trackId}, {cars} car(s), pit lanes +-{laneLat:0.0}");
        float dt = Time.fixedDeltaTime;
        bool touching = false;
        int contacts = 0;
        for (int step = 0; step < Mathf.RoundToInt(80f / dt); step++)
        {
            float t = step * dt;
            greenTime.SetValue(null, Time.time - 60f - t);
            clock += dt;
            Runtime("CautionWatch").GetMethod("Tick").Invoke(watch, new object[] { clock });
            bool allDone = true;
            foreach (var c in list)
            {
                bool onPit = (bool)isOnPit.GetValue(c.spline);
                float mph = (float)speedMps.GetValue(c.pvc) * 2.237f;
                if (!c.off && !onPit)
                {
                    // PitStopController / PracticeAIStint hand the car to the racing brain as it comes off pit road.
                    c.off = true;
                    c.offAt = t;
                    c.exitMph = mph;
                    Call(c.racing, "OnEnable");
                    _enabled.Add(c.racing);
                    report.Append($"\n  {c.go.name} off pit road at {t:0.0} s, {mph:0} mph, lat {(float)latProp.GetValue(c.spline):0.0}");
                }
                if (c.off) c.racingStep();
                c.inputStep();
                c.pvcStep();
                c.splineStep();

                if (c.off)
                {
                    float since = t - c.offAt;
                    if (since <= 12f) allDone = false;
                    string why = $"cap {Mathf.Min(999f, (float)capField.GetValue(c.spline)):0} grass {grassField.GetValue(c.spline)}/{R(c.racing, "_offPavement")} " +
                                 $"runoff {runOffField.GetValue(c.spline)} rec {(float)R(c.racing, "_recoveryTimer"):0.0} lat {(float)latProp.GetValue(c.spline):0.0}";
                    if (since > 3f && since <= 12f && mph < c.slowestMph) { c.slowestMph = mph; c.slowestWhy = $"{since:0.0} s out: {why}"; }
                    if (step % 25 == 0 && since <= 12f) report.Append($"\n  {c.go.name} +{since,4:0.0}s {mph,4:0} mph {why}");
                }
                else allDone = false;
            }
            if (list.Count == 2)
            {
                Vector2 pa = list[0].go.transform.position, pb = list[1].go.transform.position;
                bool overlap = (pa - pb).magnitude < 6f && Overlap(pa, Facing(list[0].go.transform), pb, Facing(list[1].go.transform));
                if (overlap && !touching) contacts++;
                touching = overlap;
            }
            if (allDone) break;
        }

        var failures = new List<string>();
        foreach (var c in list)
        {
            report.Append($"\n  {c.go.name}: exit {c.exitMph:0} mph, slowest 3-12 s out {c.slowestMph:0} mph ({c.slowestWhy})");
            if (!c.off) failures.Add($"{c.go.name} never left pit road");
            else if (c.slowestMph < 60f) failures.Add($"{c.go.name} crawled at {c.slowestMph:0} mph ({c.slowestWhy})");
        }
        report.Append($"\n  car-to-car contacts: {contacts}");
        Debug.Log(report.ToString());
        Assert.IsEmpty(failures, string.Join("; ", failures) + "\n" + report);
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
