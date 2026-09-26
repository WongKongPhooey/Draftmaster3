#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Draftmaster > AI > Use My Lap For AI
//
// The laps PlayerLapRecorder saved while you drove, and two things to do with one:
//
//   Compare with AI  — the AI (headless lap sim, best driver) against your lap, corner by corner: where the time
//                      goes, both cars' minimum speed, where each starts braking, where each sits at the apex.
//   Make AI line     — your line becomes the track's racing line: smoothed (keyboard steering leaves a wobble a
//                      car shouldn't copy), resampled onto the 2 m grid, and written over
//                      Resources/RacingLines/<track>.json, the trained line every AI car already reads. The old
//                      line is backed up first, and Restore puts it back.
//
// Then run Calibrate AI Pace again: a better line changes how strong the AI need to be.
public class PlayerLapAIWindow : EditorWindow
{
    string _trackId = "WatkinsGlen";
    float _smoothMetres = 6f;
    bool _showInvalid;
    List<PlayerLapFile> _laps = new List<PlayerLapFile>();
    Vector2 _lapScroll, _logScroll;
    readonly StringBuilder _log = new StringBuilder();

    static string LinePath(string id) => $"Assets/Resources/{TrainedRacingLines.ResourceFolder}/{id}.json";
    // A folder ending in ~ is ignored by Unity, so the backup is never loaded as a line of its own.
    static string BackupPath(string id) => $"Assets/Resources/{TrainedRacingLines.ResourceFolder}/Backups~/{id}.json";

    [MenuItem("Draftmaster/AI/Use My Lap For AI")]
    static void Open() => GetWindow<PlayerLapAIWindow>("My Lap -> AI");

    void OnEnable() => Refresh();

    void Refresh() => _laps = PlayerLapFile.List(_trackId);

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Every lap you drive in Play Mode is recorded (editor only). Pick one: compare it with the AI, or make " +
            "it the AI's racing line at this track. Stop Play Mode first — both run the headless lap sim.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        string id = EditorGUILayout.TextField("Track id", _trackId);
        if (id != _trackId) { _trackId = id; Refresh(); }
        if (GUILayout.Button("Refresh", GUILayout.Width(70))) Refresh();
        if (GUILayout.Button("Open folder", GUILayout.Width(90)))
        {
            Directory.CreateDirectory(PlayerLapFile.Folder(_trackId));
            EditorUtility.RevealInFinder(PlayerLapFile.Folder(_trackId));
        }
        EditorGUILayout.EndHorizontal();

        _smoothMetres = EditorGUILayout.Slider(new GUIContent("Smooth my line over (m)",
            "Irons out steering wobble before the AI copies the line. Too much and the apexes move off the kerb."),
            _smoothMetres, 0f, 20f);
        _showInvalid = EditorGUILayout.Toggle("Show invalid laps", _showInvalid);

        bool busy = EditorApplication.isPlayingOrWillChangePlaymode;
        _lapScroll = EditorGUILayout.BeginScrollView(_lapScroll, GUILayout.Height(160));
        int shown = 0;
        foreach (var lap in _laps)
        {
            if (!lap.valid && !_showInvalid) continue;
            shown++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{PlayerLapFile.FormatTime(lap.lapSeconds)}{(lap.valid ? "" : "  (invalid)")}",
                                       GUILayout.Width(140));
            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(lap.path), EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Compare with AI", GUILayout.Width(120))) Compare(lap);
                if (GUILayout.Button("Make AI line", GUILayout.Width(100))) MakeLine(lap);
            }
            EditorGUILayout.EndHorizontal();
        }
        if (shown == 0) EditorGUILayout.LabelField("No laps recorded for this track yet — drive some in Play Mode.");
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(busy || !File.Exists(BackupPath(_trackId))))
            if (GUILayout.Button("Restore the line from before my lap")) Restore();
        if (busy) EditorGUILayout.HelpBox("Stop Play Mode to compare or make a line.", MessageType.Warning);

        _logScroll = EditorGUILayout.BeginScrollView(_logScroll);
        EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ compare

    struct Trace
    {
        public List<float> d, t, lat, mph, brake;
        public static Trace New() => new Trace
            { d = new List<float>(), t = new List<float>(), lat = new List<float>(), mph = new List<float>(), brake = new List<float>() };
    }

    void Compare(PlayerLapFile header)
    {
        _log.Clear();
        var lap = PlayerLapFile.Load(header.path);
        var mine = Trace.New();
        foreach (var s in lap.samples) { mine.d.Add(s.d); mine.t.Add(s.t); mine.lat.Add(s.lat); mine.mph.Add(s.mps * 2.237f); mine.brake.Add(s.brake); }

        WithSim(sim =>
        {
            var ai = Trace.New();
            float clock = 0f, dt = Time.fixedDeltaTime;
            var track = sim.Track;
            var r = sim.Drive(CurrentK(), 1.04f, 1, true, s =>
            {
                if (s.lap != 0) return;
                clock += dt;
                Vector3 pos = s.car.transform.position;
                float cd = track.NearestCenterlineDistance(pos);
                var sample = track.SampleAt(cd);
                Vector2 local = track.transform.InverseTransformPoint(pos);
                ai.d.Add(cd); ai.t.Add(clock);
                ai.lat.Add(Vector2.Dot(local - sample.position, new Vector2(sample.tangent.y, -sample.tangent.x)));
                ai.mph.Add(s.car.SpeedMph); ai.brake.Add(s.input.LastBrake);
            });

            _log.AppendLine($"Your lap {PlayerLapFile.FormatTime(lap.lapSeconds)} vs the AI (best driver, this track's " +
                            $"calibrated strength): {r}");
            _log.AppendLine("Per turn: time you gain there (+ = you're quicker), min mph, where braking starts (m before the");
            _log.AppendLine("apex), and metres from the INSIDE edge at the apex (car centre; 1.0 = inside wheels on the paint).");
            _log.AppendLine("  turn                        gain |  min mph you / AI | brake at you / AI | apex you / AI");

            var info = track.track;
            float start = 0f, prevEnd = 0f, total = 0f;
            for (int i = 0; i < info.segments.Length; i++)
            {
                var seg = info.segments[i];
                float segStart = start;
                start += seg.length;
                if (seg.type != TrackInfoV2.SegmentType.Turn || Mathf.Abs(seg.angle) < 5f) continue;
                float apex = segStart + seg.length * 0.5f, end = segStart + seg.length;
                bool left = seg.angle > 0f;
                float half = track.SampleAt(apex).width * 0.5f;

                // Time for the stretch from the previous turn's end to this one's: what this corner costs, with the
                // straight that leads into it (braking is where most of it goes).
                float gain = (TimeAt(ai, end) - TimeAt(ai, prevEnd)) - (TimeAt(mine, end) - TimeAt(mine, prevEnd));
                total += gain;
                prevEnd = end;

                string label = string.IsNullOrEmpty(seg.label) ? $"#{i}" : seg.label;
                _log.AppendLine(
                    $"  {(label.Length > 24 ? label.Substring(0, 24) : label),-24} {gain,6:+0.00;-0.00}s |" +
                    $"{MinIn(mine.mph, mine.d, segStart - 30f, end),6:0} / {MinIn(ai.mph, ai.d, segStart - 30f, end),3:0} | " +
                    $"{BrakeLead(mine, apex),6:0} / {BrakeLead(ai, apex),4:0} | " +
                    $"{Margin(ValueAt(mine.lat, mine.d, apex), half, left),5:0.0} / {Margin(ValueAt(ai.lat, ai.d, apex), half, left),4:0.0}");
            }
            _log.AppendLine($"  (the rest of the lap: {(ai.t.Count > 0 ? ai.t[ai.t.Count - 1] : 0f) - lap.lapSeconds - total:+0.00;-0.00}s)");

            EdgeUsage(lap, track);
        });
    }

    // Where your lap ran, judged the way the lap timer judges it (LapTimingManager.OnLegalSurface): on the painted
    // road; off it but legal (inside wheels on road, kerb or tarmac run-off); or illegal (grass/gravel).
    void EdgeUsage(PlayerLapFile lap, TrackBuilder track)
    {
        float road = 0f, runoff = 0f, illegal = 0f, dist = 0f;
        var stretches = new List<(float from, float to)>();
        float runFrom = -1f, lastD = 0f;
        for (int i = 1; i < lap.samples.Count; i++)
        {
            var s = lap.samples[i];
            float step = Mathf.Abs(s.d - lap.samples[i - 1].d);
            if (step > 20f) continue;   // the wrap at the line
            dist += step;
            float half = track.SampleAt(s.d).width * 0.5f;
            bool legal = LapTimingManager.OnLegalSurface(track, World(track, s.d, s.lat), 1f);
            if (!legal) illegal += step;
            else if (Mathf.Abs(s.lat) > half) runoff += step;
            else road += step;

            if (!legal)
            {
                if (runFrom < 0f) runFrom = s.d;
                lastD = s.d;
            }
            else if (runFrom >= 0f) { stretches.Add((runFrom, lastD)); runFrom = -1f; }
        }
        if (runFrom >= 0f) stretches.Add((runFrom, lastD));

        float Pct(float v) => 100f * v / Mathf.Max(1f, dist);
        _log.AppendLine();
        _log.AppendLine($"Where your lap ran ({dist:0} m), by the lap timer's rule: painted road {Pct(road):0}%, " +
                        $"legal run-off/kerb {Pct(runoff):0}%, grass/gravel {Pct(illegal):0.0}%.");
        foreach (var st in stretches)
            _log.AppendLine($"  inside wheels on grass/gravel from {st.from:0} to {st.to:0} m");
        _log.AppendLine($"The lap timer called this lap {(lap.valid ? "VALID" : "INVALID")}" +
                        (lap.valid || stretches.Count > 0 ? "." :
                         " — but it stayed on legal surface throughout, so the timer must have voided it for something " +
                         "else (a wall contact counts)."));
    }

    // A recorded position (centreline distance, lateral + right) back in the world.
    static Vector3 World(TrackBuilder track, float d, float lat)
    {
        var s = track.SampleAt(d);
        Vector2 p = s.position + new Vector2(s.tangent.y, -s.tangent.x) * lat;
        return track.transform.TransformPoint(new Vector3(p.x, p.y, 0f));
    }

    // How far legal surface runs from the centreline on one side (m): the road's nominal half width, then on over
    // anything the lap timer counts as legal that carries straight on from it — tarmac run-off, kerb, or more
    // road. The road can be wider than its nominal width (at the Turn 9 exit it is several metres wider for 40 m,
    // with no run-off registered because it IS road), so the track's own surface test is asked as well. Stops at
    // the first grass or gravel.
    const float CorridorMargin = 0.5f;
    static float LegalExtent(TrackBuilder track, float d, int side, float half)
    {
        float extent = half;
        for (float x = half + 0.25f; x <= half + 25f; x += 0.25f)
        {
            Vector3 w = World(track, d, side * x);
            bool paved = SurfaceField.TryGetSurface(w, out var surf) &&
                         (surf == TrackEnvironment.SurfaceType.TarmacRunoff || surf == TrackEnvironment.SurfaceType.Kerb);
            if (!paved && !track.IsOnSurface(w, out _)) break;
            extent = x;
        }
        return extent;
    }

    // For MCP (which can't press window buttons) and a quick look: the comparison for the fastest recorded lap,
    // valid or not. Result in Temp/player_lap_ai.txt.
    [MenuItem("Draftmaster/AI/Compare My Fastest Lap With AI (Watkins Glen)")]
    static void CompareFastest() => WithFastest(w => w.Compare(w._fastest));

    // The window's Make AI line for the fastest recorded lap, valid or not (legal run-off laps may read invalid).
    [MenuItem("Draftmaster/AI/Make My Fastest Lap The AI Line (Watkins Glen)")]
    static void MakeFastest() => WithFastest(w => w.MakeLine(w._fastest));

    PlayerLapFile _fastest;

    static void WithFastest(Action<PlayerLapAIWindow> run)
    {
        var laps = PlayerLapFile.List("WatkinsGlen");
        if (laps.Count == 0) { Debug.LogWarning("No recorded laps for WatkinsGlen."); return; }
        laps.Sort((a, b) => a.lapSeconds.CompareTo(b.lapSeconds));
        var w = CreateInstance<PlayerLapAIWindow>();
        try { w._fastest = laps[0]; run(w); }
        finally { DestroyImmediate(w); }
    }

    static float TimeAt(Trace tr, float d)
    {
        if (d <= 0f || tr.d.Count == 0) return 0f;
        for (int i = 1; i < tr.d.Count; i++)
            if (tr.d[i] >= d && tr.d[i - 1] < d && tr.d[i] - tr.d[i - 1] < 50f)
                return Mathf.Lerp(tr.t[i - 1], tr.t[i], Mathf.InverseLerp(tr.d[i - 1], tr.d[i], d));
        return tr.t[tr.t.Count - 1];
    }

    static float ValueAt(List<float> v, List<float> d, float at)
    {
        int best = 0; float gap = float.MaxValue;
        for (int i = 0; i < d.Count; i++) { float g = Mathf.Abs(d[i] - at); if (g < gap) { gap = g; best = i; } }
        return v.Count > 0 ? v[best] : 0f;
    }

    static float MinIn(List<float> v, List<float> d, float from, float to)
    {
        float m = float.MaxValue;
        for (int i = 0; i < d.Count; i++) if (d[i] >= from && d[i] <= to) m = Mathf.Min(m, v[i]);
        return m < float.MaxValue ? m : 0f;
    }

    // How far before the apex the brakes first went on (above 20%) in the 250 m leading up to it.
    static float BrakeLead(Trace tr, float apex)
    {
        for (int i = 0; i < tr.d.Count; i++)
            if (tr.d[i] >= apex - 250f && tr.d[i] <= apex && tr.brake[i] > 0.2f) return apex - tr.d[i];
        return 0f;
    }

    static float Margin(float lateral, float half, bool leftTurn) => leftTurn ? lateral + half : half - lateral;

    // ------------------------------------------------------------------ make the line

    void MakeLine(PlayerLapFile header)
    {
        _log.Clear();
        var lap = PlayerLapFile.Load(header.path);
        if (lap.samples.Count < 100) { _log.AppendLine("That lap has too few samples to use."); return; }

        WithSim(sim =>
        {
            var built = sim.Track.SampleCenterline();
            float length = built[built.Count - 1].distance;
            if (Mathf.Abs(length - lap.trackLength) > Mathf.Max(2f, length * 0.01f))
            {
                _log.AppendLine($"This lap was driven on a {lap.trackLength:0.0} m version of the track; it is {length:0.0} m " +
                                "now. Drive a fresh lap.");
                return;
            }

            var before = sim.Drive(CurrentK(), 1.04f, 2, true);

            // Your line by distance. The first samples of a lap can read just short of the line (d near the end of
            // the lap) and the last just past it; fold both onto 0..length before sorting.
            var pts = new List<(float d, float lat)>(lap.samples.Count);
            int n = lap.samples.Count;
            for (int i = 0; i < n; i++)
            {
                float d = lap.samples[i].d;
                if (i < n / 10 && d > length * 0.5f) d -= length;
                if (i > n * 9 / 10 && d < length * 0.5f) d += length;
                if (d < 0f || d >= length) continue;
                pts.Add((d, lap.samples[i].lat));
            }
            pts.Sort((a, b) => a.d.CompareTo(b.d));
            var ds = new float[pts.Count]; var ls = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) { ds[i] = pts[i].d; ls[i] = pts[i].lat; }

            // 1 m grid, then a loop-aware moving average three times over (close to a Gaussian of that width).
            var fine = TrainedRacingLine.Resample(ds, ls, length, 1f, out int count);
            int w = Mathf.Max(0, Mathf.RoundToInt(_smoothMetres * 0.5f));
            for (int pass = 0; pass < 3 && w > 0; pass++)
            {
                var o = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float sum = 0f;
                    for (int k = -w; k <= w; k++) sum += fine[((i + k) % count + count) % count];
                    o[i] = sum / (2 * w + 1);
                }
                fine = o;
            }
            var fineD = new float[count];
            for (int i = 0; i < count; i++) fineD[i] = i;
            var lateral = TrainedRacingLine.Resample(fineD, fine, length, 2f, out _);

            // The legal corridor at every sample: the road, plus however far tarmac run-off or kerb carries on
            // past each edge (the lap timer's own rule), less a margin so the car isn't planned onto the seam
            // with the grass. The AI may use all of it on this line; your line is held inside it.
            var minLat = new float[lateral.Length];
            var maxLat = new float[lateral.Length];
            int onRunoff = 0, clamped = 0;
            for (int i = 0; i < lateral.Length; i++)
            {
                float d = i * 2f;
                float half = sim.Track.SampleAt(d).width * 0.5f;
                float lo = -LegalExtent(sim.Track, d, -1, half) + CorridorMargin;
                float hi = LegalExtent(sim.Track, d, +1, half) - CorridorMargin;
                minLat[i] = lo;
                maxLat[i] = hi;
                if (Mathf.Abs(lateral[i]) > half) onRunoff++;
                float held = Mathf.Clamp(lateral[i], lo, hi);
                if (Mathf.Abs(held - lateral[i]) > 0.05f) clamped++;
                lateral[i] = held;
            }

            // Back up whatever line is there now (once — a second lap mustn't back up the first lap).
            string linePath = LinePath(_trackId), backup = BackupPath(_trackId);
            if (File.Exists(linePath) && !File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(linePath, backup);
            }

            var existing = TrainedRacingLines.For(_trackId);
            var line = new TrainedRacingLine
            {
                trackId = _trackId,
                vehicle = "PlayerLap",
                trainedUtc = DateTime.UtcNow.ToString("o"),
                trainerVersion = TrainedRacingLine.CurrentTrainerVersion,
                trackLength = length,
                spacing = 2f,
                baselineLapTime = existing != null ? existing.Baseline : before.averageLap,
                seedLapTime = before.averageLap,
                lateral = lateral,
                minLateral = minLat,
                maxLateral = maxLat,
            };
            line.RoundForStorage();
            File.WriteAllText(linePath, line.ToCompactJson());
            AssetDatabase.ImportAsset(linePath);
            TrainedRacingLines.Invalidate();

            var after = sim.Drive(CurrentK(), 1.04f, 2, true);
            line.trainedLapTime = after.completed ? after.averageLap : 0f;
            line.RoundForStorage();
            File.WriteAllText(linePath, line.ToCompactJson());
            AssetDatabase.ImportAsset(linePath);
            TrainedRacingLines.Invalidate();

            _log.AppendLine($"Your {PlayerLapFile.FormatTime(lap.lapSeconds)} lap is now the AI's line at {_trackId} " +
                            $"(smoothed over {_smoothMetres:0} m).");
            _log.AppendLine($"  AI before: {before}");
            _log.AppendLine($"  AI after : {after}");
            _log.AppendLine($"  {100f * onRunoff / Mathf.Max(1, lateral.Length):0}% of your line is past the painted edge on legal " +
                            "run-off/kerb; the AI may follow it there on this line.");
            if (clamped > 0)
                _log.AppendLine($"  {100f * clamped / Mathf.Max(1, lateral.Length):0}% of it went past the legal surface (or within " +
                                $"{CorridorMargin:0.0} m of it) and was pulled back inside.");
            if (!after.Clean)
                _log.AppendLine("  The AI didn't drive it cleanly. Try more smoothing, or Restore.");
            _log.AppendLine("Now run Draftmaster > AI > Calibrate AI Pace again — the AI's strength was tuned on the old line.");
        });
    }

    void Restore()
    {
        _log.Clear();
        string linePath = LinePath(_trackId), backup = BackupPath(_trackId);
        if (!File.Exists(backup)) return;
        File.Copy(backup, linePath, true);
        File.Delete(backup);
        AssetDatabase.ImportAsset(linePath);
        TrainedRacingLines.Invalidate();
        _log.AppendLine($"Restored {_trackId}'s previous line.");
    }

    // The strength this track is calibrated to, so the AI is compared as it will actually race.
    float CurrentK()
    {
        var cal = Resources.Load<AIPaceCalibration>(AIPaceCalibration.ResourcePath);
        var e = cal != null ? cal.Find(_trackId) : null;
        return e != null ? e.aiGrip / TrackConditions.DefaultAiGrip : 1f;
    }

    void WithSim(Action<AIPaceCalibratorWindow.LapSim> body)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prevActive = SceneManager.GetActiveScene();
        float prevGrip = TrackConditions.AiGripMultiplier, prevPace = TrackConditions.AiPaceMultiplier;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            EditorUtility.DisplayProgressBar("AI lap sim", "Driving the track...", 0.5f);
            var sim = AIPaceCalibratorWindow.LapSim.Build(_trackId, out string error);
            if (sim == null) { _log.AppendLine(error); return; }
            body(sim);
        }
        catch (Exception e)
        {
            _log.AppendLine("Failed: " + e.Message);
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            TrackConditions.AiGripMultiplier = prevGrip;
            TrackConditions.AiPaceMultiplier = prevPace;
            if (prevActive.IsValid()) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText("Temp/player_lap_ai.txt", _log.ToString());
            Repaint();
        }
    }
}
#endif
