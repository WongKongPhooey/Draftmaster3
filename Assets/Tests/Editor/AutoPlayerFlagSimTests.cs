using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The two flags as they fall live at Daytona with the player's car on Auto (AIHandover + FormationController.TakeOver,
// dynamicModel): the field is the live race's dynamic AI (FormationController + AIRacingBehaviour + SplineInputDriver
// + PlayerVehicleController on every car, built as GridSpawner builds them), stepped by hand with the OvalLaneDirector
// and CautionWatch ticking on the sim's own clock.
//
//   * The green: the field two-wide behind the line on the formation brain (kinematic), then the green - each car handed
//     back to its dynamic model and the racing brain. Nobody behind the Auto car may brake hard.
//   * The caution: the same field racing in a pack, then a wreck behind it and the full-course caution - every car onto
//     the formation brain again. Nobody at the front may stamp on the brakes; the caution is a lift.
//
// Phone build 0.8.24, Daytona on Auto (2026-11-19): at the green every car behind the player slowed and the field
// crashed; when the caution came out the leaders slammed on the brakes and caused a second, bigger crash.
// Neither reproduced here (2026-11-19): from the green every car is flat out (throttle 1, brake 0) with no contact, with
// and without the lane controls; at the caution the leaders lift at 2 m/s^2. Bisected against the 0.8.23 scripts
// (before a13f9548, 6746d1b2 and 6b3839fc - AIRacingBehaviour, FormationController, SplineDriver, CautionLineup,
// FullCourseCaution, PaceLapAssist): the same numbers, step for step. What the live race has that this does not:
// Physics2D contacts, the safety car driving out of the pit exit (SafetyCar.StepPitOut), RacePositionTracker.
//
// No physics: contacts are geometric (car boxes 4.8 x 2.0 m from the transforms). Everything is by reflection - the
// runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class AutoPlayerFlagSimTests
{
    const string TrackId = "Daytona";
    const float HalfLength = 2.4f, HalfWidth = 1f;
    const int FieldCars = 20;          // AI cars; the player's car is one more
    const int PlayerSlot = 4;          // row 3, inside: cars behind it and beside it
    const float RowGap = 7f;           // m, row to row as the close-up brings the field to the line
    const float FormationMph = 60f;    // FormationController.cruiseMph / the pace car's pace
    const float HardBrakeMps2 = 6f;    // a stamp on the brakes (the caution's own lift is CautionRules.ApproachDecelMps2, 2)
    const int LeadersAtTheCaution = 3;

    static StringBuilder s_trace, s_cautionTrace;
    static int s_traceCar = 12;
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
        s_trace = null;
        s_cautionTrace = null;
        foreach (var c in _enabled) if (c != null) Call(c, "OnDisable");
        _enabled.Clear();
        foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        Runtime("CautionWatch").GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        Runtime("OvalLaneDirector").GetProperty("Instance").GetSetMethod(true)?.Invoke(null, new object[] { null });
        Runtime("CautionLineup").GetMethod("End").Invoke(null, null);
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "" });
    }

    class Car
    {
        public string name;
        public bool player;
        public Component spline, racing, pvc, input, fc;
        public Action[] steps;
        public Behaviour[] stepOwners;
        public bool pvcWasOn, inputWasOn;
        public float prevMps = -1f, decel, maxDecel, maxDecelAt, maxBrake;
        public bool wasDyn;
    }

    class Sim
    {
        public readonly List<Car> cars = new List<Car>();
        public Action laneDirector;
        public Component watch;
        public float t, lap, clock = 1000f;
        public readonly List<string> contacts = new List<string>();
        public readonly HashSet<long> touching = new HashSet<long>();
        public List<Component> wreck = new List<Component>();
    }

    // ---- The two regressions ----

    // The green behind an Auto player: nobody behind the player's car brakes hard and nothing touches, from the green
    // through the launch.
    // laneControls: OPTIONS > Controls: Lanes (Draftmaster 2's scheme), which puts the car on Auto from the grid with the
    // player picking a lane (AIRacingBehaviour.playerLane) - the way Auto is driven on the phone.
    [TestCase(false)]
    [TestCase(true)]
    public void NoCarBrakesHardAtTheGreenBehindAnAutoPlayer(bool laneControls)
    {
        s_trace = new StringBuilder("\n  trace:");
        var sim = Build(laneControls);
        RunFormation(sim, 6f);
        var log = new StringBuilder();
        float playerD = CentreD(sim, sim.cars[PlayerSlot]);
        Green(sim);
        Run(sim, 10f, log, "green");
        string report = Report(sim, "green", log, behindOf: playerD) + (s_trace != null ? s_trace.ToString() : "");
        Debug.Log(report);
        foreach (var c in sim.cars)
        {
            if (c.player) continue;
            Assert.Less(c.maxDecel, HardBrakeMps2, $"{c.name} braked hard after the green. " + report);
        }
        Assert.IsEmpty(sim.contacts, "Cars touched in the launch. " + report);
    }

    // The caution comes out with the field racing in a pack: the leaders lift, nobody stamps on the brakes.
    [TestCase(false)]
    [TestCase(true)]
    public void NoLeaderBrakesHardWhenTheCautionComesOut(bool laneControls)
    {
        s_cautionTrace = new StringBuilder("\n  caution trace (cars decelerating over 3 m/s^2):");
        var sim = Build(laneControls);
        RunFormation(sim, 6f);
        Green(sim);
        var log = new StringBuilder();
        Run(sim, 25f, log, null);
        string before = string.Join("; ", sim.contacts);
        sim.contacts.Clear();

        // A wreck behind the pack (nobody comes up on it in the time run), and the caution.
        PlaceWreckBehindTheField(sim, 3);
        foreach (var c in sim.cars) { c.maxDecel = 0f; c.maxBrake = 0f; c.decel = 0f; }
        var order = RunningOrder(sim);
        Caution(sim, order);
        Run(sim, 8f, log, "caution");
        string report = Report(sim, "caution", log, behindOf: float.NaN) + "\n  contacts racing before it: " + before + s_cautionTrace;
        Debug.Log(report);
        // The leaders: the front of the pack, nobody in front of them but the road. They lift at the caution's own ease
        // (CautionRules.ApproachDecelMps2, 2 m/s^2) - measured 2.0 in every run, on this code and on 0.8.23's.
        //
        // Not asserted, reported: further back, a car pushing the one in front (bump drafting 0.6-6 m behind it, closing
        // at ~3.5 mph) has that closing speed taken off by the formation planner's safety law in ~0.15 s as the caution
        // hands it to the kinematic train - 5-9 m/s^2 for ~0.2 s, 3-5 mph. The same on 0.8.23's code (bisected
        // 2026-11-19), so not the regression this guards; the trace below shows it, car by car.
        for (int k = 0; k < Mathf.Min(LeadersAtTheCaution, order.Count); k++)
        {
            var c = order[k];
            Assert.Less(c.maxDecel, HardBrakeMps2, $"{c.name} (P{k + 1}) braked hard when the caution came out. " + report);
        }
    }

    // ---- The field ----

    Sim Build(bool laneControls)
    {
        UnityEngine.Random.InitState(11);   // the brains roll reaction times and the like: the same roll every run
        var calib = Runtime("AIPaceCalibration").GetMethod("ApplyFor");
        calib.Invoke(null, new object[] { TrackId });
        var raceStart = Runtime("RaceStart");
        raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        SetPhase("Formation");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{TrackId}.prefab");
        Assert.IsNotNull(prefab, $"track package missing for {TrackId}");
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
        var fcType = Runtime("FormationController");
        var track = (Component)_package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var info = trackType.GetField("track").GetValue(track);
        float sf = (float)info.GetType().GetField("startFinishDistance").GetValue(info);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");
        float aiPace = (float)Runtime("TrackConditions").GetField("AiPaceMultiplier").GetValue(null);

        var sim = new Sim();

        // The oval lane director and the caution watch, as they bootstrap themselves in the game.
        var laneDirType = Runtime("OvalLaneDirector");
        var laneGo = new GameObject("OvalLaneDirector");
        _spawned.Add(laneGo);
        var laneDir = laneGo.AddComponent(laneDirType);
        Call(laneDir, "Awake");
        laneDirType.GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 1);
        sim.laneDirector = Step(laneDir);
        var watchGo = new GameObject("CautionWatch");
        _spawned.Add(watchGo);
        sim.watch = watchGo.AddComponent(Runtime("CautionWatch"));
        Call(sim.watch, "Awake");

        int total = FieldCars + 1;
        for (int slot = 0; slot < total; slot++)
        {
            bool player = slot == PlayerSlot;
            var go = new GameObject(player ? "Player" : $"AI{slot:D2}");
            _spawned.Add(go);
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "loop", true);
            Set(spline, "spawnInPit", false);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", FormationMph / 2.237f);
            Set(spline, "qualifyingPosition", player ? 0 : slot);
            Set(spline, "startLanes", true);
            if (!player)
            {
                // GridSpawner's AI: the ratings spread the pace a little.
                Set(spline, "cornerSpeedScale", 0.95f);
                float pace = 0.97f + 0.03f * ((slot * 7) % 11) / 10f;
                Set(spline, "paceMultiplier", pace * aiPace);
            }
            var racing = go.AddComponent(racingType);
            Set(racing, "mistakeProbabilityPerSecond", 0f);
            if (player && laneControls) Set(racing, "playerLane", slot % 2 == 0 ? -1 : 1);   // DriveModeController.UpdateLanes: its own side
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
            if (!player) Set(pvc, "corneringStiffness", 15f);   // GridSpawner.aiCorneringStiffness; the player's own car keeps its own
            var input = go.AddComponent(inputType);
            Set(input, "steerGain", 1.5f);   // GridSpawner.aiSteerGain / aiSpeedGain, which AIHandover passes too
            Set(input, "speedGain", 2f);

            Call(spline, "Awake");
            Call(spline, "OnEnable");
            _enabled.Add(spline);
            Call(racing, "Awake");
            if (!player) racingType.GetMethod("SetBasePace").Invoke(racing, new object[] { (float)splineType.GetField("paceMultiplier").GetValue(spline) / aiPace });
            Call(input, "Awake");
            Call(pvc, "Start");
            Call(spline, "Start");
            sim.lap = (float)splineType.GetProperty("TrackLength").GetValue(spline);
            Assert.Greater(sim.lap, 100f, "The AI brain never built its spline.");
            int row = slot / 2;
            Set(spline, "startDistance", Mathf.Repeat(sf - 40f - row * RowGap, sim.lap));
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);
            Call(input, "OnEnable");

            Component fc;
            if (player)
            {
                // Auto from the grid: DriveModeController -> AIHandover.ToAI (the brain and the input driver on the car's
                // dynamic model), then FormationController.TakeOver(dynamicModel) with the car's grid slot.
                fc = (Component)fcType.GetMethod("TakeOver").Invoke(null, new object[] { spline, slot, true });
                Assert.IsNotNull(fc, "TakeOver refused the player's car on the formation lap");
                Call(fc, "Awake");
                Call(fc, "OnEnable");
            }
            else
            {
                fc = go.AddComponent(fcType);
                Set(fc, "kinematic", false);
                Call(fc, "Awake");
                Call(fc, "OnEnable");
            }
            _enabled.Add(fc);
            _enabled.Add(pvc);
            // Already on its column, as the pace lap leaves it at the line.
            float column = (slot % 2 == 0 ? -1f : 1f) * (float)fcType.GetField("columnHalfOffset").GetValue(fc);
            fcType.GetField("_lateral", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fc, column);
            Set(spline, "tacticalLateralOffset", column);
            splineType.GetMethod("PlaceAtStartDistance").Invoke(spline, null);

            var car = new Car { name = go.name, player = player, spline = spline, racing = racing, pvc = pvc, input = input, fc = fc };
            car.steps = new[] { Step(fc), Step(racing), Step(input), Step(pvc), Step(spline) };
            car.stepOwners = new[] { (Behaviour)fc, (Behaviour)racing, (Behaviour)input, (Behaviour)pvc, (Behaviour)spline };
            car.pvcWasOn = ((Behaviour)pvc).enabled;
            car.inputWasOn = ((Behaviour)input).enabled;
            sim.cars.Add(car);
        }
        return sim;
    }

    // The formation brain brings the two-wide field to the line at pace speed (kinematic, under the "safety car").
    void RunFormation(Sim sim, float seconds)
    {
        int steps = Mathf.RoundToInt(seconds / Time.fixedDeltaTime);
        for (int s = 0; s < steps; s++) StepAll(sim, null);
        sim.contacts.Clear();
        sim.touching.Clear();
    }

    void Green(Sim sim)
    {
        SetPhase("Green");
        foreach (var c in sim.cars) { SyncEnable(c); c.maxDecel = 0f; c.maxBrake = 0f; c.decel = 0f; }
        sim.t = 0f;
        SetGreenClock(0f);
    }

    void Caution(Sim sim, List<Car> order)
    {
        var running = new List<Transform>();
        foreach (var c in order) running.Add(c.spline.transform);
        var stopped = new List<Transform>();
        foreach (var w in sim.wreck) stopped.Add(w.transform);
        var begin = Runtime("CautionLineup").GetMethod("Begin", new[] { typeof(IList<Transform>), typeof(IEnumerable<Transform>), typeof(float), typeof(float) });
        Assert.IsNotNull(begin, "CautionLineup.Begin(running order) is gone.");
        begin.Invoke(null, new object[] { running, stopped, 3f, 30f });
        SetPhase("Caution");
        foreach (var c in sim.cars) SyncEnable(c);
        sim.t = 0f;
    }

    void Run(Sim sim, float seconds, StringBuilder log, string label)
    {
        int steps = Mathf.RoundToInt(seconds / Time.fixedDeltaTime);
        for (int s = 0; s < steps; s++) StepAll(sim, label != null && s % 25 == 0 ? log : null, label);
    }

    void StepAll(Sim sim, StringBuilder log, string label = null)
    {
        float dt = Time.fixedDeltaTime;
        sim.t += dt;
        if (IsPhase("Green")) SetGreenClock(sim.t);
        sim.laneDirector();
        sim.clock += dt;
        Runtime("CautionWatch").GetMethod("Tick").Invoke(sim.watch, new object[] { sim.clock });
        foreach (var c in sim.cars)
            for (int k = 0; k < c.steps.Length; k++)
            {
                if (!c.stepOwners[k].enabled) continue;   // Unity steps only enabled behaviours
                c.steps[k]();
                SyncEnable(c);
            }
        foreach (var w in sim.wreck) { Set(w, "aiMaxSpeedMph", 0f); Call(w, "FixedUpdate"); }

        var speed = Runtime("PlayerVehicleController").GetProperty("SpeedMps");
        var brake = Runtime("SplineInputDriver").GetProperty("LastBrake");
        foreach (var c in sim.cars)
        {
            // Dynamic: the model's speed; kinematic (PVC off behind the safety car): the spline's.
            bool dyn = ((Behaviour)c.pvc).enabled;
            float v = dyn ? (float)speed.GetValue(c.pvc) : (float)Runtime("SplineDriver").GetProperty("CurrentMph").GetValue(c.spline) / 2.237f;
            // The speed's source changes as the car goes between the dynamic model and the spline: start again.
            if (dyn != c.wasDyn) { c.prevMps = -1f; c.decel = 0f; c.wasDyn = dyn; }
            if (c.prevMps >= 0f)
            {
                float a = (c.prevMps - v) / dt;
                c.decel = Mathf.Lerp(c.decel, a, Mathf.Clamp01(dt / 0.2f));   // ~0.2 s average: a stamp, not a one-step blip
                if (c.decel > c.maxDecel) { c.maxDecel = c.decel; c.maxDecelAt = sim.t; }
            }
            c.prevMps = v;
            if (dyn && ((Behaviour)c.input).enabled) c.maxBrake = Mathf.Max(c.maxBrake, (float)brake.GetValue(c.input));
        }

        // Contacts between running cars (and with the wreck), from the transforms.
        int n = sim.cars.Count, m = n + sim.wreck.Count;
        Transform T(int i) => i < n ? sim.cars[i].spline.transform : sim.wreck[i - n].transform;
        for (int a = 0; a < n; a++)
        for (int b = a + 1; b < m; b++)
        {
            Vector2 pa = T(a).position, pb = T(b).position;
            long key = (long)a * 1000 + b;
            if ((pa - pb).sqrMagnitude > 64f || !Overlap(pa, Facing(T(a)), pb, Facing(T(b)))) { sim.touching.Remove(key); continue; }
            if (!sim.touching.Add(key)) continue;
            sim.contacts.Add($"t{sim.t:0.00} {T(a).name} x {T(b).name}");
        }

        if (s_cautionTrace != null && label == "caution" && sim.t < 0.3f)
        {
            var fcT = Runtime("FormationController");
            var sdT = Runtime("SplineDriver");
            foreach (var c in sim.cars)
            {
                if (c.decel < 3f) continue;
                object P(string n) => fcT.GetProperty(n).GetValue(c.fc);
                s_cautionTrace.Append((
                    $"\n    t{sim.t:0.00} {c.name} v {c.prevMps * 2.237f:0.0} decel {c.decel:0.0} mode {P("DbgMode")} gap {(float)P("DbgGap"):0.0} safe {(float)P("DbgSafeClearance"):0.0} " +
                    $"closing {(float)P("DbgClosingMph"):0.0} cap {Mathf.Min(999f, (float)P("DbgCap")):0} lat {(float)sdT.GetProperty("LateralOnTrack").GetValue(c.spline):0.0} " +
                    $"tac {(float)sdT.GetField("tacticalLateralOffset").GetValue(c.spline):0.0}"));
            }
        }
        if (s_trace != null && sim.t < 1.2f && label != null)
        {
            var tc = sim.cars[Mathf.Min(s_traceCar, sim.cars.Count - 1)];
            var sd = Runtime("SplineDriver");
            var inp = Runtime("SplineInputDriver");
            s_trace.Append($"\n    {label} t{sim.t:0.00} {tc.name} v {tc.prevMps * 2.237f:0.0} decel {tc.decel:0.0} cap {Mathf.Min(999f, (float)sd.GetField("aiMaxSpeedMph").GetValue(tc.spline)):0} " +
                           $"cmd {(float)sd.GetProperty("CurrentMph").GetValue(tc.spline):0.0} desired {(float)sd.GetProperty("DesiredMph").GetValue(tc.spline):0} " +
                           $"thr {(float)inp.GetProperty("LastThrottle").GetValue(tc.input):0.00} brk {(float)inp.GetProperty("LastBrake").GetValue(tc.input):0.00} " +
                           $"hold {sd.GetProperty("LaunchHolding")?.GetValue(tc.spline)} pvcOn {((Behaviour)tc.pvc).enabled}");
        }
        if (log != null)
        {
            log.Append($"\n  {label} t{sim.t:0.0}:");
            foreach (var c in sim.cars)
                log.Append($" {c.name}:{c.prevMps * 2.237f:0}");
        }
    }

    // Edit mode runs no OnEnable / OnDisable when FormationController switches a car between its dynamic model and the
    // kinematic spline (the green, a caution): run them here, as the game would.
    static void SyncEnable(Car c)
    {
        bool pvcOn = ((Behaviour)c.pvc).enabled, inputOn = ((Behaviour)c.input).enabled;
        if (pvcOn != c.pvcWasOn) Call(c.pvc, pvcOn ? "OnEnable" : "OnDisable");
        if (inputOn != c.inputWasOn) Call(c.input, inputOn ? "OnEnable" : "OnDisable");
        c.pvcWasOn = pvcOn;
        c.inputWasOn = inputOn;
    }

    // ---- Measures ----

    float CentreD(Sim sim, Car c) => (float)Runtime("SplineDriver").GetProperty("CentreDistanceOnTrack").GetValue(c.spline);

    // Leader first, by distance round the lap from the back of the field.
    List<Car> RunningOrder(Sim sim)
    {
        var order = new List<Car>(sim.cars);
        float refD = CentreD(sim, sim.cars[sim.cars.Count - 1]) - 200f;
        order.Sort((a, b) => Mathf.Repeat(CentreD(sim, b) - refD, sim.lap).CompareTo(Mathf.Repeat(CentreD(sim, a) - refD, sim.lap)));
        return order;
    }

    // Stopped cars well behind the last car of the field: the reason for the caution, nobody's problem for a lap.
    void PlaceWreckBehindTheField(Sim sim, int count)
    {
        var splineType = Runtime("SplineDriver");
        var order = RunningOrder(sim);
        float back = CentreD(sim, order[order.Count - 1]) - 400f;
        var proto = sim.cars[0].spline;
        for (int k = 0; k < count; k++)
        {
            var go = new GameObject($"Wreck{k}");
            _spawned.Add(go);
            var s = go.AddComponent(splineType);
            Set(s, "track", splineType.GetField("track").GetValue(proto));
            Set(s, "vehicleInfo", splineType.GetField("vehicleInfo").GetValue(proto));
            Set(s, "loop", true);
            Set(s, "spawnInPit", false);
            Set(s, "spriteFacesUp", false);
            Set(s, "angleOffsetDeg", 180f);
            Set(s, "speed", 0f);
            Set(s, "aiMaxSpeedMph", 0f);
            Call(s, "Awake");
            Call(s, "OnEnable");
            _enabled.Add(s);
            Call(s, "Start");
            Set(s, "startDistance", Mathf.Repeat(back - k * 6f, sim.lap));
            Set(s, "lateralOffset", -3f + 3f * k);
            splineType.GetMethod("PlaceAtStartDistance").Invoke(s, null);
            sim.wreck.Add(s);
        }
    }

    string Report(Sim sim, string what, StringBuilder log, float behindOf)
    {
        var sb = new StringBuilder($"[AutoFlagSim] {TrackId} {what}: {sim.contacts.Count} contacts");
        var order = RunningOrder(sim);
        sb.Append("\n  hardest braking (m/s^2 over ~0.2 s, at s; peak brake input), running order:");
        for (int k = 0; k < order.Count; k++)
        {
            var c = order[k];
            sb.Append($"\n    P{k + 1} {c.name}{(c.player ? " (Auto)" : "")}: {c.maxDecel:0.0} at {c.maxDecelAt:0.0}, brake {c.maxBrake:0.00}, now {c.prevMps * 2.237f:0} mph");
        }
        if (sim.contacts.Count > 0) sb.Append("\n  contacts: ").Append(string.Join("; ", sim.contacts));
        sb.Append("\n  speeds (mph) every 0.5 s:").Append(log);
        return sb.ToString();
    }

    // ---- Helpers ----

    static void SetPhase(string phase)
    {
        var rs = Runtime("RaceStart");
        var phaseType = rs.GetNestedType("Phase");
        rs.GetProperty("Current").SetValue(null, Enum.Parse(phaseType, phase));
    }

    static bool IsPhase(string phase)
    {
        var rs = Runtime("RaceStart");
        return rs.GetProperty("Current").GetValue(null).ToString() == phase;
    }

    static void SetGreenClock(float t)
    {
        var f = Runtime("RaceStart").GetField("<GreenTime>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(f, "RaceStart.GreenTime is gone.");
        f.SetValue(null, Time.time - t);
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
