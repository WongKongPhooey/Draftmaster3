using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The AI driving a real track, with no play mode.
//
// Play mode only ticks while the editor has focus, so nothing about how the AI actually drive can be checked
// over MCP — every controller change used to mean a human playtest. This builds one AI car exactly the way
// GridSpawner builds a practice car (SplineDriver brain, PlayerVehicleController physics, SplineInputDriver
// between them) on the real Watkins Glen package, and steps the three of them by hand the way a physics frame
// would. What comes out is lap times and a list of every moment the car left the surface or spun, by track
// position — the same things AIIncidentRecorder logs in a real session.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class AILapSimTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/WatkinsGlen.prefab";

    GameObject _package;
    readonly List<GameObject> _cars = new List<GameObject>();

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

    [SetUp]
    public void SpawnTrack() => SpawnTrack(PackagePath);

    void SpawnTrack(string packagePath)
    {
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(packagePath);
        Assert.IsNotNull(prefab, $"track package missing at {packagePath}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        // Runoff/kerb polygons, so leaving the road puts the car on grass the way it does in a session.
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
    }

    [TearDown]
    public void Despawn()
    {
        foreach (var c in _cars) if (c != null) UnityEngine.Object.DestroyImmediate(c);
        _cars.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
    }

    public struct Incident
    {
        public string type;
        public int lap;
        public float distance, speed, slip;
    }

    public class Result
    {
        public readonly List<float> lapTimes = new List<float>();
        public readonly List<Incident> incidents = new List<Incident>();
        public float maxSlip;
        public float maxBrainGap;   // metres between the brain's track distance and where the car really is
        public readonly StringBuilder trace = new StringBuilder();
    }

    [Test]
    [Explicit("Diagnostic: step-by-step trace through one stretch of track, car lateral against the line and the edge.")]
    public void Trace()
    {
        var r = Drive(3, null, traceLap: 1, traceFrom: 3380f, traceTo: 3620f);
        Debug.Log("[LapSim] trace 3380-3620 lap 1\n" + r.trace);
        Despawn(); SpawnTrack();
        r = Drive(3, null, traceLap: 2, traceFrom: 2100f, traceTo: 2360f);
        Debug.Log("[LapSim] trace 2100-2360 lap 2\n" + r.trace);
    }

    [Test]
    [Explicit("Diagnostic: drives an AI car round Watkins Glen and prints lap times and every incident.")]
    public void WatkinsGlenLaps()
    {
        var r = Drive(laps: 4);
        Debug.Log(Report("[LapSim] Watkins Glen", r));
    }

    [Test]
    [Explicit("Diagnostic: sweeps SplineInputDriver knobs and prints incidents and pace for each.")]
    public void Sweep()
    {
        var sb = new StringBuilder("[LapSim] sweep");
        foreach (float lookMax in new[] { 22f, 32f, 45f })
        foreach (float lookTime in new[] { 0.45f, 0.65f, 0.85f })
        foreach (float damping in new[] { 0.08f, 0.16f })
        {
            var r = Drive(4, new Dictionary<string, object>
            {
                { "lookaheadMax", lookMax }, { "lookaheadTime", lookTime }, { "steerDamping", damping },
            });
            float sum = 0f; foreach (var t in r.lapTimes) sum += t;
            sb.Append($"\n  max {lookMax} time {lookTime} damp {damping}: {r.incidents.Count} incidents, " +
                      $"avg lap {(r.lapTimes.Count > 0 ? sum / r.lapTimes.Count : 0f):0.00}, max slip {r.maxSlip:0.0}");
            Despawn(); SpawnTrack();
        }
        Debug.Log(sb.ToString());
    }

    [Test]
    public void AnAICarLapsWatkinsGlenCleanly()
    {
        // Practice on an empty track is the easiest thing the AI ever do. It is not allowed to leave the
        // road, and it certainly is not allowed to spin.
        var r = Drive(laps: 3);
        Assert.AreEqual(3, r.lapTimes.Count, Report("The car didn't finish its laps", r));
        Assert.IsEmpty(r.incidents, Report("The AI didn't lap Watkins Glen cleanly", r));
    }

    [Test]
    [Explicit("Diagnostic: two flying laps at every venue with a track package; prints pace and incidents per track.")]
    public void EveryTrack()
    {
        var sb = new StringBuilder("[LapSim] every track");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/TrackPackages" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Despawn();
            SpawnTrack(path);
            var r = Drive(2);
            string report = Report(System.IO.Path.GetFileNameWithoutExtension(path), r);
            sb.AppendLine().Append("  ").Append(report.Replace('\n', '|'));
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: step-by-step trace through one stretch of any track.")]
    [TestCase("Michigan", 0, 60f, 260f)]
    [TestCase("Daytona", 0, 2850f, 3050f)]
    [TestCase("Talladega", -1, 1500f, 1750f)]
    public void TraceTrack(string trackId, int lap, float from, float to)
    {
        SpawnTrack($"Assets/Resources/TrackPackages/{trackId}.prefab");
        var r = Drive(2, null, traceLap: lap, traceFrom: from, traceTo: to);
        Debug.Log($"[LapSim] trace {trackId} {from:0}-{to:0} lap {lap}\n" + r.trace);
    }

    [Test]
    public void EveryTrackDrivesItsTrainedLine()
    {
        // A trained line only reaches the car if its file exists AND was trained against the road as it is
        // built today (TrainedRacingLine.MatchesLength). Regenerating a track silently drops its line back to
        // the authored one, so check the AI brain actually picked up a trained line at every venue.
        var splineType = Runtime("SplineDriver");
        var trackType = Runtime("TrackBuilder");
        var inUse = splineType.GetProperty("TrainedLineInUse");
        var missing = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/TrackPackages" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Despawn();
            SpawnTrack(path);
            var go = new GameObject("LineProbe");
            _cars.Add(go);
            var spline = go.AddComponent(splineType);
            Set(spline, "track", _package.GetComponentInChildren(trackType, true));
            Set(spline, "vehicleInfo", Resources.Load("Vehicles/Cup24"));
            splineType.GetMethod("Rebuild").Invoke(spline, null);
            if (!(bool)inUse.GetValue(spline)) missing.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        }
        Assert.IsEmpty(missing, "These tracks fall back to the authored line: " + string.Join(", ", missing));
    }

    [Test]
    public void TheBrainStaysWithTheCar()
    {
        // The brain used to count the car's speed along the centreline, but the car drives the racing line,
        // which cuts every corner — so the brain fell a little further behind the car in every turn. A lap in
        // it was ten metres adrift: late braking, a steering aim point too close to the car, a weave, and a
        // car in the run-off. It has to stay with the car lap after lap.
        var r = Drive(laps: 3);
        Assert.AreEqual(3, r.lapTimes.Count, Report("The car didn't finish its laps", r));
        Assert.Less(r.maxBrainGap, 3f, $"The brain drifted {r.maxBrainGap:0.0} m from the car it drives.");
        float spread = Mathf.Max(r.lapTimes.ToArray()) - Mathf.Min(r.lapTimes.ToArray());
        Assert.Less(spread, 0.3f, Report($"Lap times on an empty track spread by {spread:0.00} s", r));
    }

    static string Report(string title, Result r)
    {
        var sb = new StringBuilder(title);
        sb.Append($": laps {string.Join(", ", r.lapTimes.ConvertAll(t => t.ToString("0.00")))} s, max slip {r.maxSlip:0.0}°, {r.incidents.Count} incidents");
        foreach (var i in r.incidents)
            sb.Append($"\n  lap {i.lap}: {i.type} at {i.distance:0} m, {i.speed:0.0} m/s, slip {i.slip:0.0}°");
        return sb.ToString();
    }

    // One car, built like GridSpawner's practice cars, driven for `laps` flying laps after a run-up.
    Result Drive(int laps, Dictionary<string, object> inputOverrides = null,
                 int traceLap = -99, float traceFrom = 0f, float traceTo = 0f)
    {
        var trackType = Runtime("TrackBuilder");
        var track = _package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        var go = new GameObject("SimCar");
        _cars.Add(go);
        var splineType = Runtime("SplineDriver");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        // Order matters: AddComponent runs no Awake in edit mode, so each is set up and woken by hand.
        var spline = go.AddComponent(splineType);
        Set(spline, "track", track);
        Set(spline, "vehicleInfo", vehicleInfo);
        Set(spline, "cornerSpeedScale", 0.95f);
        Set(spline, "spriteFacesUp", false);
        Set(spline, "angleOffsetDeg", 180f);
        Set(spline, "speed", 45f);
        Set(spline, "startDistance", 0f);
        Set(spline, "externalMotionController", true);

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

        var input = go.AddComponent(inputType);
        if (inputOverrides != null) foreach (var kv in inputOverrides) Set(input, kv.Key, kv.Value);

        Call(spline, "Awake");
        Call(input, "Awake");
        Call(input, "OnEnable");
        Call(pvc, "Start");
        Call(spline, "Start");

        var trackLength = splineType.GetProperty("TrackLength");
        var distanceOnTrack = splineType.GetProperty("DistanceOnTrack");
        var speedMps = pvcType.GetProperty("SpeedMps");
        var slipDeg = pvcType.GetProperty("SlipAngleDeg");
        var recovering = inputType.GetProperty("IsRecovering");
        var isOnSurface = trackType.GetMethod("IsOnSurface");
        var lateralOnTrack = splineType.GetProperty("LateralOnTrack");
        var nearest = trackType.GetMethod("NearestCenterlineDistance");
        var noseErr = inputType.GetProperty("LastNoseErrorDeg");
        var steerProp = inputType.GetProperty("LastSteer");
        var thrProp = inputType.GetProperty("LastThrottle");
        var brkProp = inputType.GetProperty("LastBrake");
        var cmdProp = inputType.GetProperty("LastCommandedMps");
        var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        float length = (float)trackLength.GetValue(spline);
        Assert.Greater(length, 100f, "The AI brain never built its spline.");

        var result = new Result();
        float dt = Time.fixedDeltaTime;
        float lapClock = 0f, lastDistance = 0f;
        int lap = -1;                         // the first crossing starts lap 0 — the run-up lap is not timed
        bool wasOn = true, wasRecovering = false;
        var args = new object[] { Vector3.zero, 0f };
        int maxSteps = Mathf.RoundToInt((laps + 1.5f) * 180f / dt);   // generous: 3 minutes a lap

        for (int step = 0; step < maxSteps; step++)
        {
            inputStep.Invoke(input, null);
            pvcStep.Invoke(pvc, null);
            splineStep.Invoke(spline, null);
            lapClock += dt;

            float d = (float)distanceOnTrack.GetValue(spline);
            if (d < lastDistance - length * 0.5f)
            {
                if (lap >= 0) result.lapTimes.Add(lapClock);
                lap++;
                lapClock = 0f;
                if (lap >= laps) break;
            }
            lastDistance = d;

            float v = (float)speedMps.GetValue(pvc);
            float slip = (float)slipDeg.GetValue(pvc);
            if (lap >= 0) result.maxSlip = Mathf.Max(result.maxSlip, Mathf.Abs(slip));
            if (lap >= 0 && step % 10 == 0)
            {
                // The brain's point sits PathPointAheadOfCentre in front of the car's centre.
                float carD = (float)nearest.Invoke(track, new object[] { go.transform.position }) + 2.4f;
                float gap = Mathf.Abs(Mathf.Repeat(d - carD + length * 0.5f, length) - length * 0.5f);
                result.maxBrainGap = Mathf.Max(result.maxBrainGap, gap);
            }

            args[0] = go.transform.position;
            bool on = (bool)isOnSurface.Invoke(track, args);
            bool rec = (bool)recovering.GetValue(input);
            if (!on && wasOn) result.incidents.Add(new Incident { type = "off", lap = lap, distance = d, speed = v, slip = slip });
            if (rec && !wasRecovering) result.incidents.Add(new Incident { type = "spin", lap = lap, distance = d, speed = v, slip = slip });
            wasOn = on;
            wasRecovering = rec;

            if (lap == traceLap && d >= traceFrom && d <= traceTo && step % 5 == 0)
            {
                Vector3 pos = go.transform.position;
                float cd = (float)nearest.Invoke(track, new object[] { pos });
                object sample = null;
                foreach (var m in trackType.GetMethods())
                    if (m.Name == "SampleAt" && m.GetParameters().Length == 2) { sample = m.Invoke(track, new object[] { cd, null }); break; }
                var st = sample.GetType();
                Vector2 sp = (Vector2)st.GetField("position").GetValue(sample);
                Vector2 tg = (Vector2)st.GetField("tangent").GetValue(sample);
                float width = (float)st.GetField("width").GetValue(sample);
                Vector2 local = ((Component)track).transform.InverseTransformPoint(pos);
                float carLat = Vector2.Dot(local - sp, new Vector2(tg.y, -tg.x));
                result.trace.AppendFormat("d{0,6:0} v{1,5:0.0} cmd{2,5:0.0} carLat{3,6:0.0} lineLat{4,6:0.0} half{5,5:0.0} nose{6,6:0.0} slip{7,6:0.0} st{8,6:0.00} th{9,5:0.00} br{10,5:0.00}{11}\n",
                    d, v, (float)cmdProp.GetValue(input), carLat, (float)lateralOnTrack.GetValue(spline), width * 0.5f,
                    (float)noseErr.GetValue(input), slip, (float)steerProp.GetValue(input), (float)thrProp.GetValue(input),
                    (float)brkProp.GetValue(input), on ? "" : "  OFF");
            }
        }
        return result;
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
