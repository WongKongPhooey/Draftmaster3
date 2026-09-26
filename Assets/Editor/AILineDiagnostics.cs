#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Draftmaster > AI > Diagnose AI Racing Line (Watkins Glen)
//
// Where the AI actually drives, corner by corner, against the ideal line authored on the track. Runs the
// calibrator's headless lap sim twice — once on the trained line the AI normally uses, once on the authored
// ideal — and for every Turn segment reports, at its apex:
//
//   ideal   how far the authored ideal line's apex sits from the INSIDE edge of the road
//   plan    how far the line the AI brain is steering for sits from it
//   car     how far the car's centre actually was from it
//
// in metres, plus the car's minimum speed through the turn and any time it left the road. A car centre 1.0 m
// from the edge has its inside wheels on the paint; negative is inside wheels over it. Written to
// Temp/ai_line_report.txt and the console.
public static class AILineDiagnostics
{
    [MenuItem("Draftmaster/AI/Diagnose AI Racing Line (Watkins Glen)")]
    static void Run() => Run("WatkinsGlen");

    public static void Run(string trackId)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play Mode first."); return; }

        var report = new StringBuilder();
        var prevActive = SceneManager.GetActiveScene();
        float prevGrip = TrackConditions.AiGripMultiplier, prevPace = TrackConditions.AiPaceMultiplier;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var sim = AIPaceCalibratorWindow.LapSim.Build(trackId, out string error);
            if (sim == null) { report.AppendLine(error); return; }

            var trained = Drive(sim, trainedLine: true);
            var authored = Drive(sim, trainedLine: false);

            report.AppendLine($"AI racing line at {trackId} (best driver, today's AI strength).");
            report.AppendLine($"Trained line : {trained.result}");
            report.AppendLine($"Authored line: {authored.result}");
            report.AppendLine();
            report.AppendLine("Metres from the INSIDE edge at each apex (car centre; 1.0 = inside wheels on the paint).");
            report.AppendLine("  turn                       apex m   width | ideal | TRAINED plan  car  minMph | AUTHORED plan  car  minMph");

            var info = sim.Track.track;
            var anchors = info.BuildRacingLineAnchors();
            float length = trained.length > 0f ? trained.length : authored.length;
            float start = 0f;
            int n = 0;
            float sumIdeal = 0f, sumTrainedCar = 0f, sumAuthoredCar = 0f;
            for (int i = 0; i < info.segments.Length; i++)
            {
                var seg = info.segments[i];
                float segStart = start;
                start += seg.length;
                if (seg.type != TrackInfoV2.SegmentType.Turn || Mathf.Abs(seg.angle) < 5f) continue;

                float apex = segStart + seg.length * 0.5f;
                bool left = seg.angle > 0f;
                var sample = sim.Track.SampleAt(apex);
                float half = sample.width * 0.5f;
                float ideal = Margin(info.GetLateralAt(apex, 0f, anchors, length), half, left);

                var t = trained.At(apex, segStart, segStart + seg.length);
                var a = authored.At(apex, segStart, segStart + seg.length);
                string label = string.IsNullOrEmpty(seg.label) ? $"#{i}" : seg.label;
                report.AppendLine($"  {Trim(label, 24),-24} {apex,7:0} {sample.width,6:0.0} | {ideal,5:0.0} | " +
                                  $"{Margin(t.plan, half, left),10:0.0} {Margin(t.car, half, left),5:0.0} {t.minMph,6:0}{(t.off ? " OFF" : "    ")} | " +
                                  $"{Margin(a.plan, half, left),11:0.0} {Margin(a.car, half, left),5:0.0} {a.minMph,6:0}{(a.off ? " OFF" : "")}");
                n++;
                sumIdeal += ideal;
                sumTrainedCar += Margin(t.car, half, left);
                sumAuthoredCar += Margin(a.car, half, left);
            }
            if (n > 0)
                report.AppendLine($"  average over {n} turns: ideal {sumIdeal / n:0.0} m, trained car {sumTrainedCar / n:0.0} m, " +
                                  $"authored car {sumAuthoredCar / n:0.0} m");
            report.AppendLine();
            report.AppendLine($"SplineDriver.RoadEdgeMargin keeps the AI's planned centre {SplineDriver.RoadEdgeMargin:0.0} m inside each edge.");
        }
        catch (Exception e)
        {
            report.AppendLine("Diagnostic failed: " + e);
        }
        finally
        {
            TrackConditions.AiGripMultiplier = prevGrip;
            TrackConditions.AiPaceMultiplier = prevPace;
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText("Temp/ai_line_report.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }

    // ------------------------------------------------------------------ corner traces

    // Step-by-step through three corners where the car ran wide of the line it planned: what the controller
    // asked for, what the car could give, and where the tyres were. Temp/ai_corner_trace.txt.
    [MenuItem("Draftmaster/AI/Trace AI Corners (Watkins Glen)")]
    static void TraceCorners()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play Mode first."); return; }
        var corners = new (string name, float from, float to, bool left)[]
        {
            ("Turn 9 - Long Right", 2440f, 2960f, false),
            ("Bus Stop", 2080f, 2440f, false),
            ("Turn 10 - 90L", 3360f, 3620f, true),
        };
        // At the strength this track is calibrated to, so the trace is the AI as it races.
        var cal = Resources.Load<AIPaceCalibration>(AIPaceCalibration.ResourcePath);
        var entry = cal != null ? cal.Find("WatkinsGlen") : null;
        float k = entry != null ? entry.aiGrip / TrackConditions.DefaultAiGrip : 1f;

        var sb = new StringBuilder();
        var prevActive = SceneManager.GetActiveScene();
        float prevGrip = TrackConditions.AiGripMultiplier, prevPace = TrackConditions.AiPaceMultiplier;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var sim = AIPaceCalibratorWindow.LapSim.Build("WatkinsGlen", out string error);
            if (sim == null) { sb.AppendLine(error); return; }
            var track = sim.Track;
            var steerDegField = typeof(PlayerVehicleController).GetField("_steerDeg",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            sb.AppendLine($"AI corner traces, Watkins Glen, the track's current line, best driver, strength x{k:0.000}.");
            sb.AppendLine("Margins are metres from the corner's INSIDE edge (car centre). err = car - plan (+ = car wider).");
            sb.AppendLine("steer: controller command -> after the car's steering curve; wheel: actual / max at this speed (deg).");
            sb.AppendLine("R: turning radius the car is actually on / the planned line's radius ahead (m).");

            var blocks = new StringBuilder[corners.Length];
            var sat = new int[corners.Length];
            var count = new int[corners.Length];
            var worstErr = new float[corners.Length];
            for (int c = 0; c < corners.Length; c++)
            {
                blocks[c] = new StringBuilder();
                blocks[c].AppendLine($"\n== {corners[c].name} ({corners[c].from:0}-{corners[c].to:0} m) ==");
                blocks[c].AppendLine("     d   mph  tgt  cap | plan   car   err | steer->in   wheel/max | slipF slipR | R car / plan | thr  brk");
            }

            int step = 0;
            string carParams = "";
            var r = sim.Drive(k, 1.04f, 1, true, s =>
            {
                if (carParams.Length == 0)
                    carParams = $"Car: maxSteeringAngle {s.car.vehicleInfo.maxSteeringAngle}, steeringRate {s.car.vehicleInfo.steeringRate}/s, " +
                                $"steerExpo {s.car.steerExpo}, highSpeedSteerScale {s.car.highSpeedSteerScale} by {s.car.steerDecaySpeedMph} mph, " +
                                $"maxLateralG {s.car.vehicleInfo.maxLateralG}, AI grip x{TrackConditions.AiEffective:0.00}. " +
                                $"Controller: lookahead {s.input.lookaheadTime}s ({s.input.lookaheadMin}-{s.input.lookaheadMax} m).";
                if (s.lap != 0 || step++ % 4 != 0) return;
                Vector3 pos = s.car.transform.position;
                float cd = track.NearestCenterlineDistance(pos);
                for (int c = 0; c < corners.Length; c++)
                {
                    if (cd < corners[c].from || cd > corners[c].to) continue;
                    var sample = track.SampleAt(cd);
                    float half = sample.width * 0.5f;
                    Vector2 local = track.transform.InverseTransformPoint(pos);
                    float carLat = Vector2.Dot(local - sample.position, new Vector2(sample.tangent.y, -sample.tangent.x));
                    bool left = corners[c].left;
                    float plan = Margin(s.brain.LateralOnTrack, half, left), car = Margin(carLat, half, left);

                    float mph = s.car.SpeedMph;
                    float speedFrac = Mathf.Clamp01(mph / Mathf.Max(s.car.steerDecaySpeedMph, 1f));
                    float maxWheel = s.car.vehicleInfo.maxSteeringAngle * Mathf.Lerp(1f, s.car.highSpeedSteerScale, speedFrac);
                    float wheel = steerDegField != null ? (float)steerDegField.GetValue(s.car) : float.NaN;
                    if (Mathf.Abs(wheel) >= maxWheel * 0.97f) sat[c]++;
                    count[c]++;
                    worstErr[c] = Mathf.Max(worstErr[c], car - plan);

                    float yaw = Mathf.Abs(s.car.YawRateDeg) * Mathf.Deg2Rad;
                    float rCar = yaw > 1e-3f ? s.car.SpeedMps / yaw : 9999f;
                    float rPlan = s.brain.CurvatureRadiusAhead(Mathf.Max(8f, s.car.SpeedMps * 0.7f));

                    float cap = s.input.LastGripCapMps;
                    blocks[c].AppendLine(
                        $"{cd,6:0} {mph,5:0} {s.input.LastCommandedMps * 2.237f,4:0} {(cap > 900f ? "  - " : (cap * 2.237f).ToString("0").PadLeft(4))} | " +
                        $"{plan,5:0.0} {car,5:0.0} {car - plan,5:0.0} | {s.input.LastSteer,5:0.00}->{s.car.SteerInput,5:0.00} " +
                        $"{Mathf.Abs(wheel),5:0.0}/{maxWheel,4:0.0} | {Mathf.Abs(s.car.SlipFrontDeg),5:0.0} {Mathf.Abs(s.car.SlipRearDeg),5:0.0} | " +
                        $"{Mathf.Min(rCar, 9999f),6:0} / {Mathf.Min(rPlan, 9999f),5:0} | {s.input.LastThrottle,4:0.00} {s.input.LastBrake,4:0.00}" +
                        (s.onRoad ? "" : "  OFF"));
                }
            });

            sb.AppendLine($"Lap: {r}");
            sb.AppendLine(carParams);
            for (int c = 0; c < corners.Length; c++)
            {
                sb.Append(blocks[c]);
                sb.AppendLine($"-- wheel at >=97% of max on {sat[c]}/{count[c]} samples; car widest of plan by {worstErr[c]:0.0} m");
            }
        }
        catch (Exception e)
        {
            sb.AppendLine("Trace failed: " + e);
        }
        finally
        {
            TrackConditions.AiGripMultiplier = prevGrip;
            TrackConditions.AiPaceMultiplier = prevPace;
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText("Temp/ai_corner_trace.txt", sb.ToString());
            Debug.Log("AI corner trace written to Temp/ai_corner_trace.txt");
        }
    }

    // The brain's baked line either side of the start/finish seam: sample distance, planned lateral, the
    // curvature (as a radius) and the centreline position. Temp/ai_line_seam.txt.
    [MenuItem("Draftmaster/AI/Dump AI Line At Start-Finish (Watkins Glen)")]
    static void DumpSeam()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play Mode first."); return; }
        var sb = new StringBuilder();
        var prevActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var sim = AIPaceCalibratorWindow.LapSim.Build("WatkinsGlen", out string error);
            if (sim == null) { sb.AppendLine(error); return; }
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            bool done = false;
            sim.Drive(1f, 1.04f, 1, true, s =>
            {
                if (done) return;
                done = true;
                var t = typeof(SplineDriver);
                var samples = (List<TrackBuilder.Sample>)t.GetField("_mainSamples", flags).GetValue(s.brain);
                var lat = (float[])t.GetField("_lateralProfile", flags).GetValue(s.brain);
                var curv = (float[])t.GetField("_curvatureProfile", flags).GetValue(s.brain);
                int n = samples.Count;
                sb.AppendLine($"mainLength {s.brain.TrackLength:0.00}, samples {n}, authored segment total " +
                              $"{sim.Track.track.TotalLength():0.00}, loop {t.GetField("loop", flags | System.Reflection.BindingFlags.Public)?.GetValue(s.brain)}");
                sb.AppendLine("   i        d     lat   R(m)        x        y   step(m)");
                void Row(int i)
                {
                    var sm = samples[i];
                    float step = i > 0 ? Vector2.Distance(samples[i - 1].position, sm.position) : float.NaN;
                    float r = curv[i] > 1e-4f ? 1f / curv[i] : 9999f;
                    sb.AppendLine($"{i,4} {sm.distance,8:0.0} {lat[i],7:0.00} {Mathf.Min(r, 9999f),6:0} {sm.position.x,8:0.0} {sm.position.y,8:0.0} {step,8:0.00}");
                }
                for (int i = Mathf.Max(0, n - 40); i < n; i++) Row(i);
                sb.AppendLine("  -- wrap --");
                for (int i = 0; i < Mathf.Min(12, n); i++) Row(i);
            });
        }
        catch (Exception e) { sb.AppendLine("Dump failed: " + e); }
        finally
        {
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText("Temp/ai_line_seam.txt", sb.ToString());
        }
    }

    // What SurfaceField says is at each point across the road's left side at the Turn 9 exit, where the corridor
    // probe found no run-off but the player drove legally. R = road (inside the painted width), T = tarmac run-off,
    // K = kerb, G = grass, V = gravel, . = nothing registered. Temp/ai_surface_map.txt.
    [MenuItem("Draftmaster/AI/Map Surfaces At Turn 9 Exit (Watkins Glen)")]
    static void MapSurfaces()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play Mode first."); return; }
        var sb = new StringBuilder();
        var prevActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var sim = AIPaceCalibratorWindow.LapSim.Build("WatkinsGlen", out string error);
            if (sim == null) { sb.AppendLine(error); return; }
            var track = sim.Track;
            sb.AppendLine("lateral from -4 to -14 m (left of travel) in 0.5 m steps");
            for (float d = 2790f; d <= 2860f; d += 4f)
            {
                var s = track.SampleAt(d);
                float half = s.width * 0.5f;
                var row = new StringBuilder();
                for (float x = -4f; x >= -14f; x -= 0.5f)
                {
                    Vector2 p = s.position + new Vector2(s.tangent.y, -s.tangent.x) * x;
                    Vector3 w = track.transform.TransformPoint(new Vector3(p.x, p.y, 0f));
                    char c;
                    if (Mathf.Abs(x) <= half) c = 'R';
                    else if (!SurfaceField.TryGetSurface(w, out var surf)) c = '.';
                    else c = surf switch
                    {
                        TrackEnvironment.SurfaceType.TarmacRunoff => 'T',
                        TrackEnvironment.SurfaceType.Kerb => 'K',
                        TrackEnvironment.SurfaceType.Gravel => 'V',
                        _ => 'G',
                    };
                    row.Append(c);
                }
                bool onSurface = track.IsOnSurface(track.transform.TransformPoint(
                    new Vector3((s.position + new Vector2(s.tangent.y, -s.tangent.x) * -(half + 0.3f)).x,
                                (s.position + new Vector2(s.tangent.y, -s.tangent.x) * -(half + 0.3f)).y, 0f)), out _);
                sb.AppendLine($"{d,5:0} half {half,4:0.0}  {row}   (IsOnSurface 0.3 m past edge: {onSurface})");
            }
        }
        catch (Exception e) { sb.AppendLine("Map failed: " + e); }
        finally
        {
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText("Temp/ai_surface_map.txt", sb.ToString());
        }
    }

    // Distance of a lateral offset (+ right of travel) from the inside edge of a turn.
    static float Margin(float lateral, float half, bool leftTurn) => leftTurn ? lateral + half : half - lateral;

    static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n);

    class Run1
    {
        public AIPaceCalibratorWindow.LapSim.Result result;
        public float length;
        public readonly List<float> d = new List<float>(), plan = new List<float>(), car = new List<float>(),
                                    mph = new List<float>();
        public readonly List<bool> onRoad = new List<bool>();

        public struct Apex { public float plan, car, minMph; public bool off; }

        public Apex At(float apexD, float from, float to)
        {
            var r = new Apex { minMph = float.PositiveInfinity };
            float best = float.PositiveInfinity;
            for (int i = 0; i < d.Count; i++)
            {
                float gap = Mathf.Abs(d[i] - apexD);
                if (gap < best) { best = gap; r.plan = plan[i]; r.car = car[i]; }
                if (d[i] >= from && d[i] <= to)
                {
                    r.minMph = Mathf.Min(r.minMph, mph[i]);
                    if (!onRoad[i]) r.off = true;
                }
            }
            if (float.IsInfinity(r.minMph)) r.minMph = 0f;
            return r;
        }
    }

    // One timed lap (after the run-up), sampling where the brain aims and where the car is every few steps.
    static Run1 Drive(AIPaceCalibratorWindow.LapSim sim, bool trainedLine)
    {
        var run = new Run1();
        var track = sim.Track;
        int step = 0;
        run.result = sim.Drive(1f, 1.04f, 1, trainedLine, s =>
        {
            if (s.lap != 0 || step++ % 3 != 0) return;
            if (run.length <= 0f) run.length = s.brain.TrackLength;

            Vector3 pos = s.car.transform.position;
            float cd = track.NearestCenterlineDistance(pos);
            var sample = track.SampleAt(cd);
            Vector2 local = track.transform.InverseTransformPoint(pos);
            float carLat = Vector2.Dot(local - sample.position, new Vector2(sample.tangent.y, -sample.tangent.x));

            run.d.Add(cd);
            run.plan.Add(s.brain.LateralOnTrack);
            run.car.Add(carLat);
            run.mph.Add(s.car.SpeedMph);
            run.onRoad.Add(s.onRoad);
        });
        return run;
    }
}
#endif
