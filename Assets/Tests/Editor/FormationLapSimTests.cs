using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// A whole formation lap on a real track, with no play mode.
//
// PackAvoidanceTests drive the planner through an idealised world: point cars on straight lanes. The real field
// is kinematic SplineDrivers filing out of the pit boxes, merging off the pit exit, riding each car's racing line
// through real corners and clamped to the road, behind a real safety car that peels off into the pit. This builds
// that field exactly the way GridSpawner builds a formation race (kinematic AI + FormationController, pit start,
// FormationDirector + SafetyCar) on a real track package, steps every FixedUpdate by hand from the formation
// start to the green, and checks the car bodies against each other every step. Any overlap is a crash.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class FormationLapSimTests
{
    const float HalfLength = 2.4f; // GridSpawner.collisionHalfExtents — what FormationController assumes too
    const float HalfWidth = 1f;
    const int FieldSize = 43; // RaceScene's GridSpawner.count — the AI field the game really spawns

    GameObject _package;
    List<Component> _lastCars;
    readonly List<GameObject> _spawned = new List<GameObject>();
    readonly List<Component> _enabled = new List<Component>(); // components whose OnDisable must run

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

        var directorType = Runtime("FormationDirector");
        directorType.GetProperty("Instance").GetSetMethod(true)?.Invoke(null, new object[] { null });
        Runtime("PitLane").GetMethod("Clear").Invoke(null, null);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
    }

    public struct Contact
    {
        public float time;
        public string a, b;
        public float distance;   // along the main track (m) of car a
        public float overlap;    // penetration estimate (m)
        public string detail;
    }

    public class Result
    {
        public readonly List<Contact> contacts = new List<Contact>();
        public float seconds;
        public bool wentGreen;
        public int carsOnTrackAtGreen;
        public float minClearance = float.MaxValue;
        public string setup = "";
        public int parkedOverlaps;   // cars already touching in their pit boxes before anything moved
        public readonly StringBuilder trace = new StringBuilder();
        // A car moving further in one step than its speed allows (a teleport), or leaving the road on the
        // main track: one line per car, the first time it happens.
        public readonly List<string> jumps = new List<string>();
        public readonly List<string> offRoad = new List<string>();
        // Free-driven player only: how far (m, centre to centre) the nearest car behind the player was at the
        // green, and the most it ever was once the whole field was out on the track.
        public float playerLeadAtGreen = -1f;
        public float playerLeadWorst = -1f;
    }

    [Explicit("Diagnostic: formation laps at every venue; prints contacts per track.")]
    [Test]
    public void EveryTrack()
    {
        var sb = new StringBuilder("[FormationSim] every track");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/TrackPackages" }))
        {
            string id = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            Despawn();
            var r = Run(id, FieldSize);
            sb.Append($"\n  {id}: green {r.wentGreen} after {r.seconds:0}s, {r.contacts.Count} contacts, {r.parkedOverlaps} parked overlaps [{r.setup}]");
            for (int i = 0; i < Mathf.Min(3, r.contacts.Count); i++)
                sb.Append($"\n     t{r.contacts[i].time:0.0} {r.contacts[i].a} x {r.contacts[i].b} @ {r.contacts[i].distance:0} m: {r.contacts[i].detail}");
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: two cars' lateral state step by step through a window of the lap.")]
    [TestCase("Chicago", "AI_20", "AI_21", 70f, 76f)]
    [TestCase("SanDiego", "AI_12", "AI_13", 42f, 46.5f)]
    [TestCase("WatkinsGlen", "AI_01", "AI_02", 5.1f, 5.6f)]
    public void TracePair(string trackId, string a, string b, float from, float to)
    {
        var r = Run(trackId, FieldSize, traceA: a, traceB: b, traceFrom: from, traceTo: to);
        Debug.Log(Report($"[FormationSim] trace {trackId} {a}/{b}", r, 5));
    }

    [Explicit("Diagnostic: one track, every contact.")]
    [TestCase("WatkinsGlen", 30)]
    [TestCase("Daytona", 40)]
    [TestCase("Bristol", 30)]
    [TestCase("Chicago", 30)]
    [TestCase("SanDiego", 30)]
    [TestCase("Sonoma", 30)]
    public void Detail(string trackId, int cars)
    {
        var r = Run(trackId, cars);
        Debug.Log(Report($"[FormationSim] {trackId} x{cars}", r, 60));
    }

    // One of each thing that used to go wrong: a road course and an oval that were already clean, a short track,
    // two venues whose pit lane rejoins past the pace car's start (it drove into the cars that came out ahead of
    // it), and two street/road courses where corners squeezed the pairs together.
    [TestCase("WatkinsGlen")]
    [TestCase("Daytona")]
    [TestCase("Martinsville")]
    [TestCase("Darlington")]
    [TestCase("Milwaukee")]
    [TestCase("Chicago")]
    [TestCase("SanDiego")]
    public void AFullFieldFormationLapHasNoContact(string trackId)
    {
        var r = Run(trackId, FieldSize);
        Debug.Log(Report($"[FormationSim] {trackId}", r, 15));
        Assert.IsTrue(r.wentGreen, Report($"{trackId}: the safety car never pitted", r, 10));
        Assert.IsEmpty(r.contacts, Report($"{trackId}: cars touched on the formation lap", r, 15));
        Assert.AreEqual(0, r.parkedOverlaps, Report($"{trackId}: cars parked on top of each other in the pit boxes", r, 5));
    }

    [TestCase("WatkinsGlen", 0)]
    [TestCase("WatkinsGlen", 7)]
    [TestCase("Daytona", 12)]
    public void ThePlayersCarHandedToTheAIFormsUpWithTheField(string trackId, int playerBox)
    {
        // Drive/Broadcast (V) and the crew chief hand the human's car to the AI. It used to get a bare
        // SplineDriver — its race speed profile, no cap, blind to the field — and ran up the back of the train.
        var r = Run(trackId, 30, playerBox: playerBox, playerFormsUp: true);
        Debug.Log(Report($"[FormationSim] {trackId} player in box {playerBox}", r, 15));
        Assert.IsTrue(r.wentGreen, Report($"{trackId}: the safety car never pitted", r, 10));
        Assert.IsEmpty(r.contacts, Report($"{trackId}: the handed-over player car touched the field", r, 15));
    }

    [TestCase("WatkinsGlen")]
    [TestCase("Daytona")]
    [TestCase("Martinsville")]
    public void TheFieldStaysCloseBehindAPlayerOnPole(string trackId)
    {
        // The human on pole drives off first and runs up behind the safety car as hard as the pace-lap hold lets
        // them. The train behind used to be capped a few mph over cruise on straights only, so it never caught
        // the human back up and they took the green alone.
        var r = Run(trackId, 30, playerBox: 0, playerFormsUp: false, playerDrivesFree: true);
        Debug.Log(Report($"[FormationSim] {trackId} free player on pole", r, 15));
        Assert.IsTrue(r.wentGreen, Report($"{trackId}: the safety car never pitted", r, 10));
        Assert.IsEmpty(r.contacts, Report($"{trackId}: cars touched behind the player on pole", r, 15));
        Assert.Less(r.playerLeadAtGreen, 30f, Report($"{trackId}: the field was strung out behind the player at the green", r, 5));
    }

    [Explicit("Diagnostic: the free player on pole and the car behind, step by step.")]
    [TestCase("WatkinsGlen", "AI_01", 0f, 110f)]
    [TestCase("Daytona", "AI_01", 0f, 90f)]
    public void TracePlayerOnPole(string trackId, string other, float from, float to)
    {
        var r = Run(trackId, 30, playerBox: 0, playerFormsUp: false, playerDrivesFree: true,
                    traceA: "Player", traceB: other, traceFrom: from, traceTo: to);
        Debug.Log(Report($"[FormationSim] trace {trackId} free player on pole", r, 5));
    }

    [Explicit("Diagnostic: the player's car handed to the AI with no formation brain — what it did before TakeOver.")]
    [TestCase("WatkinsGlen", 7)]
    public void ThePlayersCarHandedToTheAIWithoutFormingUp(string trackId, int playerBox)
    {
        var r = Run(trackId, 30, playerBox: playerBox, playerFormsUp: false);
        Debug.Log(Report($"[FormationSim] {trackId} player in box {playerBox}, NO formation brain", r, 15));
    }

    [Explicit("Diagnostic: how far each venue's pit lane is from the track over its last stretch.")]
    [Test]
    public void PitLaneEnds()
    {
        var trackType = Runtime("TrackBuilder");
        var sb = new StringBuilder("[FormationSim] pit lane ends (lateral off the main centreline at % of the pit lane; nearest main distance)");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/TrackPackages" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var track = (Component)pkg.GetComponentInChildren(trackType, true);
                if (track == null) continue;
                var pit = (System.Collections.IList)trackType.GetMethod("SamplePitCenterline").Invoke(track, null);
                var main = trackType.GetMethod("SampleCenterline").Invoke(track, null);
                if (pit.Count < 2) { sb.Append($"\n  {System.IO.Path.GetFileNameWithoutExtension(path)}: no pit"); continue; }
                var st = pit[0].GetType();
                float pitLen = (float)st.GetField("distance").GetValue(pit[pit.Count - 1]);
                sb.Append($"\n  {System.IO.Path.GetFileNameWithoutExtension(path)} pit {pitLen:0} m:");
                MethodInfo sampleAt = null, samplePitAt = null;
                foreach (var m in trackType.GetMethods())
                {
                    if (m.Name == "SampleAt" && m.GetParameters().Length == 2) sampleAt = m;
                    if (m.Name == "SamplePitAt" && m.GetParameters().Length == 2) samplePitAt = m;
                }
                foreach (float f in new[] { 0f, 0.02f, 0.5f, 0.9f, 0.95f, 0.98f, 1f })
                {
                    var ps = samplePitAt.Invoke(track, new object[] { pitLen * f, pit });
                    Vector2 pp = (Vector2)st.GetField("position").GetValue(ps);
                    Vector3 world = track.transform.TransformPoint(new Vector3(pp.x, pp.y, 0f));
                    float md = (float)trackType.GetMethod("NearestCenterlineDistance").Invoke(track, new object[] { world });
                    var ms = sampleAt.Invoke(track, new object[] { md, main });
                    Vector2 mp = (Vector2)st.GetField("position").GetValue(ms);
                    Vector2 mt = (Vector2)st.GetField("tangent").GetValue(ms);
                    float lat = Vector2.Dot(pp - mp, new Vector2(mt.y, -mt.x));
                    float w = (float)st.GetField("width").GetValue(ms);
                    sb.Append($" {f:P0} {lat:0.0}m@{md:0}(w{w:0})");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(pkg); }
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: teleports and road exits at every venue, one line each.")]
    [Test]
    public void SeamsEveryTrack()
    {
        var sb = new StringBuilder("[FormationSim] seams every track");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/TrackPackages" }))
        {
            string id = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            Despawn();
            var r = Run(id, FieldSize);
            sb.Append($"\n  {id}: {r.jumps.Count} jumps, {r.offRoad.Count} off road, green {r.wentGreen} [{r.setup}]");
            if (r.jumps.Count > 0) sb.Append("\n     ").Append(r.jumps[0]);
            if (r.offRoad.Count > 0) sb.Append("\n     ").Append(r.offRoad[0]);
        }
        Debug.Log(sb.ToString());
    }

    [Explicit("Diagnostic: every car's teleports and road exits from the pit boxes to the green.")]
    [TestCase("WatkinsGlen")]
    [TestCase("Daytona")]
    public void Seams(string trackId)
    {
        var r = Run(trackId, FieldSize);
        var sb = new StringBuilder($"[FormationSim] seams {trackId} [{r.setup}]: {r.jumps.Count} jumps, {r.offRoad.Count} off road");
        foreach (var j in r.jumps) sb.Append("\n  JUMP ").Append(j);
        foreach (var o in r.offRoad) sb.Append("\n  OFF ").Append(o);
        // The line and bounds either side of the pit rejoin, off the first AI car.
        var car = _lastCars[1];
        var t = car.GetType();
        var latAt = t.GetMethod("LateralAt", BindingFlags.Instance | BindingFlags.NonPublic);
        var boundsAt = t.GetMethod("BoundsAt", BindingFlags.Instance | BindingFlags.NonPublic);
        var exitArgs = new object[] { 0f, 0f, 0f };
        t.GetMethod("TryGetPitExit").Invoke(car, exitArgs);
        for (float d = (float)exitArgs[0] - 60f; d <= (float)exitArgs[0] + 120f; d += 10f)
        {
            var b = new object[] { d, 0f, 0f };
            boundsAt.Invoke(car, b);
            sb.Append($"\n  d{d:0} line {(float)latAt.Invoke(car, new object[] { d }):0.00} bounds [{(float)b[1]:0.00}, {(float)b[2]:0.00}]");
        }
        Debug.Log(sb.ToString());
    }

    static string Report(string title, Result r, int max)
    {
        var sb = new StringBuilder(title);
        sb.Append($" [{r.setup}]: green {r.wentGreen} after {r.seconds:0.0}s, {r.contacts.Count} contacts, {r.parkedOverlaps} parked overlaps, " +
                  $"{r.carsOnTrackAtGreen} cars on track at green, " +
                  (r.playerLeadAtGreen >= 0f ? $"player {r.playerLeadAtGreen:0} m clear at green (worst {r.playerLeadWorst:0} m), " : "") +
                  $"closest {(r.minClearance < 1f ? r.minClearance.ToString("0.00") + " m" : "over 1 m")}");
        for (int i = 0; i < Mathf.Min(max, r.contacts.Count); i++)
        {
            var c = r.contacts[i];
            sb.Append($"\n  t{c.time,6:0.00} {c.a} x {c.b} @ {c.distance:0} m, overlap {c.overlap:0.00}: {c.detail}");
        }
        if (r.trace.Length > 0) sb.Append("\n").Append(r.trace);
        return sb.ToString();
    }

    // A formation race built like GridSpawner builds one, run from the formation start to the green.
    // playerBox >= 0 puts the player's own car in that pit box, handed to the AI the way the Drive/Broadcast
    // toggle and the crew chief hand it over (a bare SplineDriver engaged on the car); playerFormsUp says whether
    // it is given the formation brain (FormationController.TakeOver) as the game now does.
    // playerDrivesFree instead drives it like the human: no formation brain, flat out up to the pace-lap hold
    // (PaceLapAssist) behind the car directly ahead in grid order.
    Result Run(string trackId, int count, float maxSeconds = 600f, int playerBox = -1, bool playerFormsUp = true,
               bool playerDrivesFree = false, string traceA = null, string traceB = null, float traceFrom = 0f, float traceTo = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"no track package for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        var trackType = Runtime("TrackBuilder");
        var splineType = Runtime("SplineDriver");
        var fcType = Runtime("FormationController");
        var scType = Runtime("SafetyCar");
        var directorType = Runtime("FormationDirector");
        var pitLaneType = Runtime("PitLane");
        var raceStartType = Runtime("RaceStart");
        var phaseType = raceStartType.GetNestedType("Phase");
        var currentProp = raceStartType.GetProperty("Current");

        var track = (Component)_package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var trackInfo = trackType.GetField("track").GetValue(track);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        currentProp.SetValue(null, Enum.Parse(phaseType, "PreGrid"));

        // Pit boxes, fit to the grey box strip the way GridSpawner does.
        var pit = (System.Collections.IList)trackType.GetMethod("SamplePitCenterline").Invoke(track, null);
        float pitLen = pit.Count > 0 ? (float)pit[pit.Count - 1].GetType().GetField("distance").GetValue(pit[pit.Count - 1]) : 0f;
        Assert.Greater(pitLen, 10f, $"{trackId} has no pit lane");
        float parkLateral = 2.6f;
        if ((bool)trackType.GetProperty("HasPitBoxLane").GetValue(track))
            parkLateral = (float)trackType.GetProperty("PitBoxLaneCenterLateral").GetValue(track);
        int boxes = count + (playerBox >= 0 ? 1 : 0);
        var fit = pitLaneType.GetMethod("FitBoxes").Invoke(null, new object[] { track, pitLen, boxes });
        float exitGap = (float)fit.GetType().GetField("exitGap").GetValue(fit);
        float spacing = (float)fit.GetType().GetField("spacing").GetValue(fit);
        pitLaneType.GetMethod("Configure").Invoke(null, new object[] { exitGap, spacing, boxes, parkLateral });

        var cars = new List<Component>();       // SplineDrivers, safety car first
        _lastCars = cars;
        var fcs = new List<Component>();
        var names = new List<string>();

        // Safety car, as FormationDirector.SpawnSafetyCar builds it.
        var scGo = new GameObject("SafetyCar");
        _spawned.Add(scGo);
        var scSpline = scGo.AddComponent(splineType);
        Set(scSpline, "track", track);
        Set(scSpline, "vehicleInfo", vehicleInfo);
        Set(scSpline, "loop", true);
        Set(scSpline, "lineFactor", 0f);
        Set(scSpline, "spriteFacesUp", false);
        Set(scSpline, "angleOffsetDeg", 180f);
        Set(scSpline, "externalMotionController", false);
        Set(scSpline, "aiMaxSpeedMph", 60f);
        Set(scSpline, "startDistance", (float)directorType.GetMethod("SafetyCarStartDistance").Invoke(null, new object[] { track, 28f, 0.98f }));
        Set(scSpline, "qualifyingPosition", -1000);
        Call(scSpline, "Awake");
        Call(scSpline, "OnEnable");
        _enabled.Add(scSpline);
        var sc = scGo.AddComponent(scType);
        Set(sc, "cruiseMph", 60f);
        Call(sc, "Awake");
        cars.Add(scSpline);
        names.Add("SC");

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"AI_{i + 1:D2}");
            _spawned.Add(go);
            var s = go.AddComponent(splineType);
            Set(s, "track", track);
            Set(s, "vehicleInfo", vehicleInfo);
            Set(s, "cornerSpeedScale", 0.95f);
            Set(s, "spawnInPit", true);
            Set(s, "qualifyingPosition", playerBox >= 0 && i >= playerBox ? i + 1 : i);
            Set(s, "lateralOffset", parkLateral);
            Set(s, "spriteFacesUp", false);
            Set(s, "angleOffsetDeg", 180f);
            Set(s, "pitBoxExitGap", exitGap);
            Set(s, "pitBoxSpacing", spacing);
            Set(s, "freezeUntilFormation", true);
            // Aggression spreads each driver's line (AIDriverBinding); formation parks it at 0 anyway.
            Set(s, "lineFactor", Mathf.Lerp(-0.05f, 0.08f, (i * 7 % 11) / 10f));
            Call(s, "Awake");
            Call(s, "OnEnable");
            _enabled.Add(s);

            var fc = go.AddComponent(fcType);
            Set(fc, "kinematic", true);
            Call(fc, "Awake");
            Call(fc, "OnEnable");
            _enabled.Add(fc);

            cars.Add(s);
            fcs.Add(fc);
            names.Add(go.name);
        }

        Component playerSpline = null;
        if (playerBox >= 0)
        {
            // The player's car: no FormationController of its own, no grid slot on its SplineDriver.
            var go = new GameObject("Player");
            _spawned.Add(go);
            playerSpline = go.AddComponent(splineType);
            Set(playerSpline, "track", track);
            Set(playerSpline, "vehicleInfo", vehicleInfo);
            Set(playerSpline, "spawnInPit", true);
            Set(playerSpline, "qualifyingPosition", playerBox); // only to park it in its box; reset below
            Set(playerSpline, "lateralOffset", parkLateral);
            Set(playerSpline, "pitBoxExitGap", exitGap);
            Set(playerSpline, "pitBoxSpacing", spacing);
            Set(playerSpline, "freezeUntilFormation", true);
            Call(playerSpline, "Awake");
            Call(playerSpline, "OnEnable");
            _enabled.Add(playerSpline);
            cars.Add(playerSpline);
            names.Add("Player");
        }

        foreach (var c in cars) Call(c, "Start");
        var exitArgs = new object[] { 0f, 0f, 0f };
        splineType.GetMethod("TryGetPitExit").Invoke(cars[1], exitArgs);
        if (playerSpline != null) Set(playerSpline, "qualifyingPosition", 0);
        Call(sc, "Start");

        var dirGo = new GameObject("FormationDirector");
        _spawned.Add(dirGo);
        var director = dirGo.AddComponent(directorType);
        Set(director, "cruiseMph", 60f);
        Set(director, "_safetyCar", sc);
        directorType.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { director });

        currentProp.SetValue(null, Enum.Parse(phaseType, "Formation"));

        if (playerSpline != null && playerFormsUp)
        {
            // What DriveModeController does as it hands the car over. Edit mode runs no Awake/OnEnable for the
            // component TakeOver adds, so those two are called by hand after it.
            var fc = (Component)fcType.GetMethod("TakeOver").Invoke(null, new object[] { playerSpline, playerBox });
            Assert.IsNotNull(fc, "TakeOver refused a car before the green");
            Call(fc, "Awake");
            Call(fc, "OnEnable");
            _enabled.Add(fc);
            fcs.Add(fc);
        }

        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var fcStep = fcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var scStep = scType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var posProp = splineType.GetProperty("CommandedLocalPos");
        var headProp = splineType.GetProperty("CommandedHeadingDeg");
        var onPitProp = splineType.GetProperty("IsOnPit");
        var distProp = splineType.GetProperty("DistanceOnTrack");
        var latProp = splineType.GetProperty("LateralOnTrack");
        var mphProp = splineType.GetProperty("CurrentMph");
        var modeProp = fcType.GetProperty("DbgMode");
        var gapProp = fcType.GetProperty("DbgGap");
        var pittingField = scType.GetField("_pitting", BindingFlags.Instance | BindingFlags.NonPublic);

        var result = new Result();
        result.setup = $"pit {pitLen:0} exitGap {exitGap:0.0} spacing {spacing:0.00} raw {(float)fit.GetType().GetField("rawSpacing").GetValue(fit):0.00}, " +
                       $"SC start {(float)distProp.GetValue(scSpline):0} pit rejoins main at {(float)exitArgs[0]:0} lat {(float)exitArgs[1]:0.0}";
        float dt = Time.fixedDeltaTime;
        int steps = Mathf.RoundToInt(maxSeconds / dt);
        int n = cars.Count;
        var pos = new Vector2[n];
        var fwd = new Vector2[n];
        var touching = new HashSet<long>();
        var prevPos = new Vector2[n];
        var prevOnPit = new bool[n];
        var jumped = new HashSet<int>();
        var wentOff = new HashSet<int>();
        for (int i = 0; i < n; i++) { prevPos[i] = (Vector2)posProp.GetValue(cars[i]); prevOnPit[i] = (bool)onPitProp.GetValue(cars[i]); }

        Component playerAhead = playerSpline != null && playerDrivesFree ? cars[playerBox == 0 ? 0 : playerBox] : null;
        float lapLen = (float)splineType.GetProperty("TrackLength").GetValue(scSpline);
        for (int step = 0; step < steps; step++)
        {
            if (playerAhead != null) DrivePlayerFree();
            foreach (var fc in fcs) fcStep.Invoke(fc, null);
            scStep.Invoke(sc, null);
            foreach (var c in cars) splineStep.Invoke(c, null);
            result.seconds = (step + 1) * dt;

            if (playerAhead != null) MeasurePlayerLead();
            if ((bool)pittingField.GetValue(sc))
            {
                result.wentGreen = true;
                if (playerAhead != null) result.playerLeadAtGreen = PlayerLead(out _);
                foreach (var c in cars) if (!(bool)onPitProp.GetValue(c)) result.carsOnTrackAtGreen++;
                break;
            }

            if (traceA != null && result.seconds >= traceFrom && result.seconds <= traceTo && (traceTo - traceFrom < 1f || step % (traceTo - traceFrom > 30f ? 100 : 5) == 0))
            {
                result.trace.Append($"t{result.seconds:0.00}");
                for (int i = 0; i < n; i++)
                {
                    if (names[i] != traceA && names[i] != traceB) continue;
                    var sd = cars[i];
                    var b = new object[] { 0f, 0f };
                    splineType.GetMethod("GetLateralBounds").Invoke(sd, b);
                    result.trace.Append($" | {names[i]} {((bool)onPitProp.GetValue(sd) ? "PIT " : "")}p{(Vector2)posProp.GetValue(sd)} w{sd.transform.position} d{(float)distProp.GetValue(sd):0.0} lat{(float)latProp.GetValue(sd):0.00} " +
                        $"tac{(float)splineType.GetField("tacticalLateralOffset").GetValue(sd):0.00} " +
                        $"L{(float)splineType.GetProperty("UntacticalLateral").GetValue(sd):0.00} " +
                        $"b[{(float)b[0]:0.0},{(float)b[1]:0.0}] h{(float)headProp.GetValue(sd):0} {(float)mphProp.GetValue(sd):0.0}mph " +
                        $"{(sd.GetComponent(fcType) != null ? modeProp.GetValue(sd.GetComponent(fcType)) + " cap" + ((float)fcType.GetProperty("DbgCap").GetValue(sd.GetComponent(fcType))).ToString("0") : "free")}");
                }
                result.trace.Append('\n');
            }

            for (int i = 0; i < n; i++)
            {
                pos[i] = (Vector2)posProp.GetValue(cars[i]);
                float h = (float)headProp.GetValue(cars[i]) * Mathf.Deg2Rad;
                fwd[i] = new Vector2(Mathf.Cos(h), Mathf.Sin(h));

                bool onPit = (bool)onPitProp.GetValue(cars[i]);
                float mps = (float)mphProp.GetValue(cars[i]) / 2.237f;
                float moved = (pos[i] - prevPos[i]).magnitude;
                if (moved > mps * dt * 1.5f + 0.25f && jumped.Add(i))
                    result.jumps.Add($"t{result.seconds:0.00} {names[i]} moved {moved:0.00} m in one step at {mps * 2.237f:0} mph " +
                                     $"({(prevOnPit[i] ? "pit" : "main")}->{(onPit ? "pit" : "main")}) from {prevPos[i]} to {pos[i]}: {Describe(cars[i])}");
                if (!onPit)
                {
                    var lb = new object[] { 0f, 0f };
                    float lat = (float)latProp.GetValue(cars[i]);
                    if ((bool)splineType.GetMethod("GetLateralBounds").Invoke(cars[i], lb) &&
                        (lat < (float)lb[0] - 1.5f || lat > (float)lb[1] + 1.5f) && wentOff.Add(i))
                        result.offRoad.Add($"t{result.seconds:0.00} {names[i]}: {Describe(cars[i])}");
                }
                prevPos[i] = pos[i];
                prevOnPit[i] = onPit;
            }
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if ((pos[i] - pos[j]).sqrMagnitude > 64f) continue;
                float pen = Penetration(pos[i], fwd[i], pos[j], fwd[j]);
                long key = (long)i * 1000 + j;
                if (pen > -1f) result.minClearance = Mathf.Min(result.minClearance, -pen);
                if (pen <= 0f) { touching.Remove(key); continue; }
                if (!touching.Add(key)) continue; // one report per touch, not per frame
                if (step == 0) { result.parkedOverlaps++; continue; } // the box layout, not the driving
                result.contacts.Add(new Contact
                {
                    time = result.seconds,
                    a = names[i],
                    b = names[j],
                    distance = (float)distProp.GetValue(cars[i]),
                    overlap = pen,
                    detail = Describe(cars[i]) + " | " + Describe(cars[j]),
                });
            }
        }
        return result;

        // A keen human under PaceLapAssist's hold: flat out (the corner profile still applies) to run the car ahead
        // in grid order down, braking in time to arrive at the 13 m hold matched to its speed, and backing off under
        // it inside 6 m. The pit limit clamps the lane.
        void DrivePlayerFree()
        {
            float gap = Mathf.Repeat((float)distProp.GetValue(playerAhead) - (float)distProp.GetValue(playerSpline), lapLen);
            float aheadMph = (float)mphProp.GetValue(playerAhead);
            float cap = 120f;
            if (!(bool)onPitProp.GetValue(playerSpline) && !(bool)onPitProp.GetValue(playerAhead))
            {
                if (gap <= 6f) cap = aheadMph * 0.8f;
                else cap = Mathf.Min(cap, aheadMph + 2.5f * (gap - 13f));
            }
            Set(playerSpline, "aiMaxSpeedMph", Mathf.Max(0f, cap));
            Set(playerSpline, "aiMinDecelMphPerSec", 30f);
        }

        // Centre-to-centre metres back to the nearest car behind the player on the main track; -1 = nobody yet.
        float PlayerLead(out bool allOut)
        {
            allOut = true;
            float best = -1f;
            float me = (float)distProp.GetValue(playerSpline);
            for (int i = 1; i < n; i++)
            {
                if (cars[i] == playerSpline) continue;
                if ((bool)onPitProp.GetValue(cars[i])) { allOut = false; continue; }
                float back = Mathf.Repeat(me - (float)distProp.GetValue(cars[i]), lapLen);
                if (back > lapLen * 0.5f) continue; // ahead of the player, not behind
                if (best < 0f || back < best) best = back;
            }
            return best;
        }

        void MeasurePlayerLead()
        {
            if ((bool)onPitProp.GetValue(playerSpline)) return;
            float lead = PlayerLead(out bool allOut);
            if (allOut && lead > result.playerLeadWorst) result.playerLeadWorst = lead;
        }

        string Describe(Component s)
        {
            var fc = s.GetComponent(fcType);
            string txt = $"{((bool)onPitProp.GetValue(s) ? "pit " : "")}d{(float)distProp.GetValue(s):0.0} " +
                         $"lat{(float)latProp.GetValue(s):0.00} {(float)mphProp.GetValue(s):0.0}mph";
            var bargs = new object[] { 0f, 0f };
            if ((bool)splineType.GetMethod("GetLateralBounds").Invoke(s, bargs)) txt += $" bounds[{(float)bargs[0]:0.0},{(float)bargs[1]:0.0}]";
            if (fc != null) txt += $" {modeProp.GetValue(fc)} gap{(float)gapProp.GetValue(fc):0.0}";
            return txt;
        }
    }

    // Separating-axis test on two car rectangles. > 0 = overlapping by that much on the least-overlapping axis;
    // < 0 = the clearance between them (m) along the best separating axis.
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
