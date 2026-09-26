#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Draftmaster.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Draftmaster > AI > Calibrate AI Pace
//
// Tunes how fast the AI are at a track to the player's own lap. Give it your lap time and it drives an AI car
// round the real track package in the headless lap sim — the same one AILapSimTests uses: SplineDriver brain,
// PlayerVehicleController physics, SplineInputDriver between them, stepped by hand, no play mode — raising the
// AI's grip and power together until its lap matches yours (AIPaceSearch picks each step). The answer is
// written to Resources/AI/AIPaceCalibration.asset and applied to the whole field whenever that track loads.
//
// The sim car is driven at a driver rating of 1.04 by default: the best driver in the database. So the
// fastest AI match your lap and the rest of the field (down to 0.93) spread out behind them. Lower it to have
// the middle of the field match you instead.
//
// A setting where the car leaves the road or spins never counts as a match — see AIPaceSearch.
public class AIPaceCalibratorWindow : EditorWindow
{
    const string AssetPath = "Assets/Resources/" + AIPaceCalibration.ResourcePath + ".asset";

    string _trackId = "WatkinsGlen";
    string _targetText = "";
    float _referencePace = 1.04f;
    int _timedLaps = 2;
    Vector2 _scroll;
    readonly StringBuilder _log = new StringBuilder();

    [MenuItem("Draftmaster/AI/Calibrate AI Pace")]
    static void Open() => GetWindow<AIPaceCalibratorWindow>("AI Pace");

    // Calibrate Watkins Glen to the fastest recorded player lap, without the window. Log in Temp/ai_pace_calibrate.txt.
    [MenuItem("Draftmaster/AI/Calibrate To My Fastest Lap (Watkins Glen)")]
    static void CalibrateToFastest()
    {
        var laps = PlayerLapFile.List("WatkinsGlen");
        if (laps.Count == 0) { Debug.LogWarning("No recorded laps for WatkinsGlen."); return; }
        laps.Sort((a, b) => a.lapSeconds.CompareTo(b.lapSeconds));
        var w = CreateInstance<AIPaceCalibratorWindow>();
        try
        {
            w._targetText = laps[0].lapSeconds.ToString("0.000", CultureInfo.InvariantCulture);
            w.Calibrate();
            System.IO.File.WriteAllText("Temp/ai_pace_calibrate.txt", w._log.ToString());
        }
        finally { DestroyImmediate(w); }
    }

    // The window's Measure button without the window, for a quick check (and for anything driving the editor
    // over MCP, which can't press window buttons). Result in the console and Temp/ai_pace_measure.txt.
    [MenuItem("Draftmaster/AI/Measure Today's AI Lap (Watkins Glen)")]
    static void MeasureWatkinsGlen()
    {
        var w = CreateInstance<AIPaceCalibratorWindow>();
        try
        {
            w.Measure();
            string text = w._log.ToString();
            Debug.Log(text);
            System.IO.File.WriteAllText("Temp/ai_pace_measure.txt", text);
        }
        finally { DestroyImmediate(w); }
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Enter your lap time and press Calibrate. An AI car is driven round the track in the headless sim, " +
            "getting stronger (grip and power together) until its lap matches yours. The result is saved per " +
            "track and applied to every AI car when that track loads.", MessageType.Info);

        _trackId = EditorGUILayout.TextField("Track id", _trackId);
        _targetText = EditorGUILayout.TextField(new GUIContent("Your lap", "m:ss.ss or seconds, e.g. 1:12.40 or 72.4"),
                                                _targetText);
        _referencePace = EditorGUILayout.Slider(new GUIContent("Match driver rating",
            "Which AI driver should run your pace. 1.04 = the best in the field; 0.985 = the middle of it."),
            _referencePace, 0.93f, 1.04f);
        _timedLaps = EditorGUILayout.IntSlider(new GUIContent("Timed laps per try",
            "Laps averaged per setting, after an untimed run-up lap. All of them must be clean."), _timedLaps, 1, 4);

        var cal = AssetDatabase.LoadAssetAtPath<AIPaceCalibration>(AssetPath);
        var entry = cal != null ? cal.Find(_trackId) : null;
        EditorGUILayout.LabelField("Saved for this track", entry == null
            ? $"none (defaults: grip x{TrackConditions.DefaultAiGrip}, pace x{TrackConditions.DefaultAiPace})"
            : $"grip x{entry.aiGrip:0.###}, pace x{entry.aiPace:0.###} — {entry.simLapSeconds:0.00}s " +
              $"against {entry.targetLapSeconds:0.00}s ({entry.calibratedOn})");

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Measure today's AI")) Measure();
            if (GUILayout.Button("Calibrate")) Calibrate();
            using (new EditorGUI.DisabledScope(entry == null))
                if (GUILayout.Button("Clear saved")) Clear();
            EditorGUILayout.EndHorizontal();
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Stop Play Mode first — the sim runs in edit mode.", MessageType.Warning);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ actions

    void Measure()
    {
        _log.Clear();
        WithSim(sim =>
        {
            var r = sim.Drive(1f, _referencePace, _timedLaps);
            _log.AppendLine($"Today's AI at {_trackId} (driver rating {_referencePace:0.00}): {r}");
        });
    }

    void Calibrate()
    {
        if (!TryParseLap(_targetText, out float target))
        {
            _log.Clear();
            _log.AppendLine("Enter your lap as m:ss.ss or seconds.");
            return;
        }

        _log.Clear();
        _log.AppendLine($"Calibrating {_trackId} to {Format(target)} (driver rating {_referencePace:0.00}).");
        AIPaceSearch search = null;
        WithSim(sim =>
        {
            search = new AIPaceSearch(target);
            while (!search.Done)
            {
                float k = search.Next();
                if (EditorUtility.DisplayCancelableProgressBar("Calibrating AI pace",
                        $"Try {search.Trials.Count + 1}: AI strength x{k:0.000}", search.Trials.Count / (float)search.MaxTrials))
                {
                    _log.AppendLine("Cancelled.");
                    return;
                }
                var r = sim.Drive(k, _referencePace, _timedLaps);
                _log.AppendLine($"  x{k:0.000}  grip {Grip(k):0.###}  pace {Pace(k):0.###}  -> {r}");
                search.Report(k, r.completed ? r.averageLap : float.MaxValue, r.Clean);
            }
        });

        if (search == null || !search.Done) return;
        _log.AppendLine(search.Outcome);

        var best = search.Best;
        if (float.IsNaN(best.k))
        {
            _log.AppendLine("No clean lap at any setting tried — nothing saved.");
            return;
        }
        Save(target, best.k, best.lapSeconds);
        _log.AppendLine($"Saved: {_trackId} AI grip x{Grip(best.k):0.###}, pace x{Pace(best.k):0.###} " +
                        $"(sim lap {Format(best.lapSeconds)}, {best.lapSeconds - target:+0.00;-0.00}s from yours).");
    }

    void Clear()
    {
        var cal = AssetDatabase.LoadAssetAtPath<AIPaceCalibration>(AssetPath);
        if (cal == null) return;
        cal.entries.RemoveAll(e => e != null && string.Equals(e.trackId, _trackId, StringComparison.OrdinalIgnoreCase));
        EditorUtility.SetDirty(cal);
        AssetDatabase.SaveAssets();
        _log.AppendLine($"Cleared {_trackId}; it runs at the defaults again.");
    }

    // One strength number drives both knobs, from the defaults a track has with no calibration.
    static float Grip(float k) => TrackConditions.DefaultAiGrip * k;
    static float Pace(float k) => TrackConditions.DefaultAiPace * k;

    void Save(float target, float k, float simLap)
    {
        var cal = AssetDatabase.LoadAssetAtPath<AIPaceCalibration>(AssetPath);
        if (cal == null)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
            cal = CreateInstance<AIPaceCalibration>();
            AssetDatabase.CreateAsset(cal, AssetPath);
        }
        var entry = cal.Find(_trackId);
        if (entry == null) { entry = new AIPaceCalibration.Entry { trackId = _trackId }; cal.entries.Add(entry); }
        entry.aiGrip = Grip(k);
        entry.aiPace = Pace(k);
        entry.targetLapSeconds = target;
        entry.simLapSeconds = simLap;
        entry.referenceDriverPace = _referencePace;
        entry.calibratedOn = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        EditorUtility.SetDirty(cal);
        AssetDatabase.SaveAssets();
    }

    // Builds the track in a throwaway scene beside whatever is open, runs `body`, and puts everything back:
    // the scene closes unsaved and TrackConditions' AI knobs return to what they were.
    void WithSim(Action<LapSim> body)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prevActive = SceneManager.GetActiveScene();
        float prevGrip = TrackConditions.AiGripMultiplier, prevPace = TrackConditions.AiPaceMultiplier;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var sim = LapSim.Build(_trackId, out string error);
            if (sim == null) { _log.AppendLine(error); return; }
            body(sim);
        }
        catch (Exception e)
        {
            _log.AppendLine("Sim failed: " + e.Message);
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            TrackConditions.AiGripMultiplier = prevGrip;
            TrackConditions.AiPaceMultiplier = prevPace;
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
            Repaint();
        }
    }

    static bool TryParseLap(string s, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        int colon = s.IndexOf(':');
        if (colon < 0) return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds > 0f;
        if (!int.TryParse(s.Substring(0, colon), out int m)) return false;
        if (!float.TryParse(s.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float sec)) return false;
        seconds = m * 60f + sec;
        return seconds > 0f;
    }

    static string Format(float s) => s >= 60f ? $"{(int)(s / 60f)}:{s % 60f:00.00}" : $"{s:0.00}s";

    // ------------------------------------------------------------------ the sim

    // One AI car on one track, built the way GridSpawner builds a practice car and stepped by hand. Mirrors
    // AILapSimTests.Drive (which can't be shared: the test assembly reaches the runtime by reflection, this
    // one references it), minus the diagnostics.
    public class LapSim
    {
        public struct Result
        {
            public bool completed;
            public float averageLap;
            public List<float> laps;
            public int offs, spins;
            public bool Clean => completed && offs == 0 && spins == 0;

            public override string ToString()
            {
                if (!completed) return "did not finish its laps";
                string laps = string.Join(", ", this.laps.ConvertAll(t => Format(t)));
                string incidents = offs + spins == 0 ? "clean" : $"{offs} off(s) onto grass/gravel, {spins} spin(s)";
                return $"{Format(averageLap)} ({laps}), {incidents}";
            }
        }

        TrackBuilder _track;
        VehicleInfo _vehicle;

        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static LapSim Build(string trackId, out string error)
        {
            error = null;
            string path = $"Assets/Resources/{TrackCatalog.PackageFolder}/{trackId}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { error = $"No track package at {path}."; return null; }
            var package = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());

            // Runoff and kerbs, so leaving the road puts the car on grass as it would in a session.
            var env = package.GetComponentInChildren<TrackEnvironmentBuilder>(true);
            if (env != null) env.Build();

            var sim = new LapSim
            {
                _track = package.GetComponentInChildren<TrackBuilder>(true),
                _vehicle = Resources.Load<VehicleInfo>("Vehicles/Cup24"),
            };
            if (sim._track == null) { error = $"{trackId}'s package has no TrackBuilder."; return null; }
            if (sim._vehicle == null) { error = "No Cup24 VehicleInfo at Resources/Vehicles/Cup24."; return null; }
            return sim;
        }

        public TrackBuilder Track => _track;

        // What a per-step observer sees: the car's three parts and which lap it is on (-1 = the run-up).
        public struct Step
        {
            public SplineDriver brain;
            public PlayerVehicleController car;
            public SplineInputDriver input;
            public int lap;
            public bool onRoad;
        }

        // Drive one fresh car at AI strength k: a run-up lap, then `timedLaps` timed ones. `trainedLine` false
        // drives the track's AUTHORED ideal line instead of the trained one; `observe` sees every physics step.
        public Result Drive(float k, float driverPace, int timedLaps, bool trainedLine = true, Action<Step> observe = null)
        {
            TrackConditions.AiGripMultiplier = Grip(k);
            TrackConditions.AiPaceMultiplier = Pace(k);

            var go = new GameObject("CalibrationCar");
            try { return Run(go, driverPace * TrackConditions.AiPaceMultiplier, timedLaps, trainedLine, observe); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        Result Run(GameObject go, float pace, int timedLaps, bool trainedLine, Action<Step> observe)
        {
            // Order matters: AddComponent runs no Awake in edit mode, so each is set up and woken by hand.
            var spline = go.AddComponent<SplineDriver>();
            spline.track = _track;
            spline.vehicleInfo = _vehicle;
            spline.cornerSpeedScale = 0.95f;
            spline.spriteFacesUp = false;
            spline.angleOffsetDeg = 180f;
            spline.speed = 45f;
            spline.startDistance = 0f;
            spline.externalMotionController = true;
            spline.useTrainedLine = trainedLine;
            spline.paceMultiplier = pace;
            spline.lineFactor = 0f;

            var pvc = go.AddComponent<PlayerVehicleController>();
            pvc.vehicleInfo = _vehicle;
            pvc.track = _track;
            pvc.spriteFacesUp = false;
            pvc.angleOffsetDeg = 180f;
            pvc.grassTrails = false;
            pvc.enableWheelspin = false;
            pvc.externalInput = true;
            pvc.damageImpairsHandling = false;
            pvc.surfaceSpray = false;
            pvc.impactDebris = false;

            var input = go.AddComponent<SplineInputDriver>();

            Call(spline, "Awake");
            Call(input, "Awake");
            Call(input, "OnEnable");
            Call(pvc, "Start");
            Call(spline, "Start");

            var inputStep = typeof(SplineInputDriver).GetMethod("FixedUpdate", Any);
            var pvcStep = typeof(PlayerVehicleController).GetMethod("FixedUpdate", Any);
            var splineStep = typeof(SplineDriver).GetMethod("FixedUpdate", Any);

            var result = new Result { laps = new List<float>() };
            float length = spline.TrackLength;
            if (length < 100f) return result;

            float dt = Time.fixedDeltaTime, lapClock = 0f, lastDistance = 0f;
            int lap = -1;                           // the first crossing starts lap 0; the run-up isn't timed
            bool wasOn = true, wasRecovering = false;
            int maxSteps = Mathf.RoundToInt((timedLaps + 1.5f) * 180f / dt);   // generous: 3 minutes a lap

            for (int step = 0; step < maxSteps; step++)
            {
                inputStep.Invoke(input, null);
                pvcStep.Invoke(pvc, null);
                splineStep.Invoke(spline, null);
                lapClock += dt;

                float d = spline.DistanceOnTrack;
                if (d < lastDistance - length * 0.5f)
                {
                    if (lap >= 0) result.laps.Add(lapClock);
                    lap++;
                    lapClock = 0f;
                    if (lap >= timedLaps) break;
                }
                lastDistance = d;

                // "On" by the lap timer's own rule: inside wheels on road, kerb or tarmac run-off. Using the run-off
                // is legal, and a player-lap line uses it on purpose — only grass and gravel count as an off.
                bool on = LapTimingManager.OnLegalSurface(_track, go.transform.position, 1f);
                bool rec = input.IsRecovering;
                observe?.Invoke(new Step { brain = spline, car = pvc, input = input, lap = lap, onRoad = on });
                if (lap >= 0)
                {
                    if (!on && wasOn) result.offs++;
                    if (rec && !wasRecovering) result.spins++;
                }
                wasOn = on;
                wasRecovering = rec;
            }

            result.completed = result.laps.Count == timedLaps;
            if (result.completed)
            {
                float sum = 0f;
                foreach (var t in result.laps) sum += t;
                result.averageLap = sum / result.laps.Count;
            }
            return result;
        }

        static void Call(object target, string method) => target.GetType().GetMethod(method, Any)?.Invoke(target, null);
    }
}
#endif
