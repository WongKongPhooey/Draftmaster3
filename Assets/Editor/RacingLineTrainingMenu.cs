using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEngine;

// Turns a track asset into a lap the AI can practise on, hands it to RacingLineTrainer, and writes the
// winning line out to Assets/Resources/RacingLines/<trackId>.json.
//
// The bridge lives in the editor assembly because TrackInfoV2, TrackBuilder and VehicleInfo are all in
// Assembly-CSharp, which an assembly definition cannot reference. Nothing here is needed at run time — the
// game only ever reads the JSON, through TrainedRacingLines.
//
// NO MODAL DIALOGS in here on purpose: these menu items get driven from tooling as well as by hand, and a
// DisplayDialog waiting on a click wedges the editor. Everything reports through the console and through the
// numbers stored in each JSON file.
public static class RacingLineTrainingMenu
{
    const string OutputFolder = "Assets/Resources/RacingLines";
    const string DefaultVehicleResource = "Vehicles/Cup24";
    const float MphToMps = 1f / 2.237f;

    // Metres between stored lateral samples. This is not just a file-size knob: the game drives the line as
    // STORED, so every metre of grid is lap time given away at the apexes, and curvature — a second
    // derivative — feels a coarse grid far more than the positions do. 3 m cost Watkins Glen over a second of
    // the time training had found. 2 m brings that back for about 150 KB across all 38 tracks.
    const float StorageSpacing = 2f;

    // What the AI's line looks like TODAY: the authored ideal, relaxed toward minimum curvature. Matches
    // SplineDriver's serialized defaults, so the "seed" lap time in each file is the pace we started from.
    const int SeedSmoothingIterations = 60;
    const float SeedSmoothingRelaxation = 0.3f;

    // SplineDriver.cornerSpeedScale's default — the margin the AI keeps off the grip limit.
    const float CornerSpeedScale = 0.95f;

    // Roughly how long one click of "Another Session" spends driving before it stops and writes up. Short
    // on purpose: the point of the budget is that the run is resumable, so clicking three times is the same
    // as one long run, and nothing is lost if a tool call gives up waiting. A short oval is a second or two;
    // a road course is a couple of minutes, and the budget is only checked BETWEEN tracks, so one click can
    // overrun by one track's worth. The whole 38-track calendar is about six clicks.
    const float SessionBudgetSeconds = 180f;

    static readonly string NewLine = System.Environment.NewLine;

    // Laps a stored line has behind it, tolerating files written before totalLapsSimulated existed.
    static int LapsBehind(TrainedRacingLine line)
        => Mathf.Max(line.totalLapsSimulated, line.lapsSimulated);

    [MenuItem("Draftmaster/AI/Train Racing Line (Selected Track)", priority = 300)]
    public static void TrainSelected()
    {
        var track = Selection.activeObject as TrackInfoV2;
        if (track == null)
        {
            Debug.LogWarning("[RacingLineTrainer] Select a TrackInfoV2 asset (Assets/Resources/Tracks) first.");
            return;
        }
        var car = DefaultVehicle();
        if (car == null) return;

        var line = TrainTrack(track, car, out string summary);
        if (line != null) Debug.Log("[RacingLineTrainer] " + summary);
    }

    [MenuItem("Draftmaster/AI/Train Racing Lines (Missing Only)", priority = 301)]
    public static void TrainMissing() => TrainAll(false);

    // The one to keep clicking. Brings anything missing or trained by an older optimiser up to date first,
    // then hands whichever line has had the LEAST practice another session on top of what it already knows —
    // so running it again always spends its laps where there is most left to find, and running it ten times
    // is ten sessions of practice rather than the same session ten times.
    //
    // Budgeted and resumable on purpose. A field of 38 tracks is far more than one menu click's worth of
    // driving, and a tool call that runs for twenty minutes is a tool call that times out halfway and loses
    // the lot. Each click does about SessionBudgetSeconds of work and always stops on a written file.
    [MenuItem("Draftmaster/AI/Train Racing Lines (Another Session)", priority = 302)]
    public static void TrainAnotherSession() => RunSession(SessionBudgetSeconds);

    [MenuItem("Draftmaster/AI/Retrain Every Racing Line (From Scratch)", priority = 303)]
    public static void RetrainAll() => TrainAll(true);

    [MenuItem("Draftmaster/AI/Report Trained Racing Lines", priority = 310)]
    public static void Report()
    {
        var sb = new StringBuilder("[RacingLineTrainer] trained lines on disk:" + NewLine);
        int found = 0, stale = 0;
        float totalGain = 0f;
        long totalLaps = 0;
        foreach (var track in AllTrackGeometry())
        {
            var line = LoadFromDisk(track.name);
            if (line == null) { sb.AppendLine($"  {track.name,-18} —"); continue; }
            found++;
            totalGain += line.GainSeconds;
            totalLaps += LapsBehind(line);
            bool old = line.trainerVersion < TrainedRacingLine.CurrentTrainerVersion;
            if (old) stale++;
            sb.AppendLine($"  {track.name,-18} base {line.Baseline,7:F2}s -> {line.trainedLapTime,7:F2}s" +
                          $"  gain {line.GainSeconds,6:F2}s ({100f * line.GainSeconds / Mathf.Max(0.01f, line.Baseline),4:F1}%)" +
                          $"  {line.refinePasses + 1} session(s), {LapsBehind(line)} laps" +
                          (old ? "  [older trainer]" : ""));
        }
        sb.AppendLine($"  {found} trained ({stale} from an older trainer), {totalLaps} laps driven, " +
                      $"{totalGain:F1}s of lap time found in total.");
        Debug.Log(sb.ToString());
    }

    // --- The batch ---------------------------------------------------------------------------------------

    // Resumable on purpose: `force` off skips any track whose stored line is already up to date, so a run
    // that gets interrupted (or a tool call that times out) can simply be run again. `force` on throws away
    // whatever is on disk and starts each track from the authored line — the only way to find out what a
    // change to the optimiser is worth from a standing start.
    static void TrainAll(bool force)
    {
        var car = DefaultVehicle();
        if (car == null) return;

        var tracks = AllTrackGeometry();
        int trained = 0, skipped = 0, failed = 0;
        float totalGain = 0f;
        var sb = new StringBuilder();

        try
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                EditorUtility.DisplayProgressBar("Training racing lines",
                    $"{track.name} ({i + 1}/{tracks.Count})", (i + 1) / (float)tracks.Count);

                if (!force)
                {
                    var existing = LoadFromDisk(track.name);
                    if (existing != null && existing.IsCurrent(SampledLength(track))) { skipped++; continue; }
                }

                var line = TrainTrack(track, car, !force, out string summary);
                if (line == null) { failed++; sb.AppendLine("  " + summary); continue; }
                trained++;
                totalGain += line.GainSeconds;
                sb.AppendLine("  " + summary);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        TrainedRacingLines.Invalidate();
        Debug.Log($"[RacingLineTrainer] trained {trained}, skipped {skipped}, failed {failed}; " +
                  $"{totalGain:F1}s of lap time found.\n{sb}");
    }

    // --- A practice session ------------------------------------------------------------------------------

    // One budgeted, resumable slice of training. Order of business:
    //
    //   1. tracks with no line at all, or a line that no longer fits the road,
    //   2. tracks whose line came from an older, weaker optimiser,
    //   3. everything else, least-practised first.
    //
    // Each track picks up from the line already on disk rather than starting over, so the laps are spent
    // improving what is there instead of rediscovering it. Stops as soon as the budget is gone — always
    // between tracks, never mid-track, so what is on disk is always a complete line.
    public static void RunSession(float budgetSeconds)
    {
        var car = DefaultVehicle();
        if (car == null) return;

        var tracks = AllTrackGeometry();
        var queue = new List<(TrackInfoV2 track, float length, TrainedRacingLine line, int rank)>(tracks.Count);
        foreach (var track in tracks)
        {
            float length = SampledLength(track);
            var line = LoadFromDisk(track.name);
            if (line != null && !line.MatchesLength(length)) line = null;    // road moved: start over
            int rank = line == null ? -2
                     : line.trainerVersion < TrainedRacingLine.CurrentTrainerVersion ? -1
                     : line.refinePasses;
            queue.Add((track, length, line, rank));
        }
        // Stable and deterministic: same rank, alphabetical.
        queue.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank)
                                              : string.CompareOrdinal(a.track.name, b.track.name));

        int outstanding = 0;
        foreach (var entry in queue) if (entry.rank < 0) outstanding++;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        int done = 0, failed = 0;
        float gained = 0f;
        var sb = new StringBuilder();

        try
        {
            foreach (var entry in queue)
            {
                if (done > 0 && clock.Elapsed.TotalSeconds >= budgetSeconds) break;
                EditorUtility.DisplayProgressBar("Racing line practice",
                    $"{entry.track.name} ({done + 1})",
                    Mathf.Clamp01((float)clock.Elapsed.TotalSeconds / Mathf.Max(1f, budgetSeconds)));

                float before = entry.line != null ? entry.line.trainedLapTime : 0f;
                var trained = TrainTrack(entry.track, car, out string summary);
                if (trained == null) { failed++; sb.AppendLine("  " + summary); continue; }
                done++;
                if (entry.rank < 0) outstanding--;
                if (before > 0.01f) gained += Mathf.Max(0f, before - trained.trainedLapTime);
                sb.AppendLine("  " + summary);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        TrainedRacingLines.Invalidate();
        Debug.Log($"[RacingLineTrainer] session over: {done} track(s) practised, {failed} failed, " +
                  $"{gained:F2}s found on top of what they already had, {clock.Elapsed.TotalSeconds:F0}s spent. " +
                  $"{outstanding} still waiting on a first pass from this trainer." + NewLine + sb);
    }

    // --- One track ---------------------------------------------------------------------------------------

    public static TrainedRacingLine TrainTrack(TrackInfoV2 track, VehicleInfo car, out string summary)
        => TrainTrack(track, car, true, out summary);

    public static TrainedRacingLine TrainTrack(TrackInfoV2 track, VehicleInfo car, bool continueFromStored,
                                               out string summary)
    {
        summary = null;
        if (track == null || track.segments == null || track.segments.Length == 0)
        {
            summary = $"{(track != null ? track.name : "(null)")}: no segments";
            return null;
        }

        var samples = Sample(track);
        if (samples == null || samples.Count < 16)
        {
            summary = $"{track.name}: only {(samples == null ? 0 : samples.Count)} centreline samples";
            return null;
        }

        // TrackBuilder closes a lap by walking back to the start point, so the last sample IS the first one
        // again. Leave it in and the seam is a zero-length step: the two ends of the lap stop constraining
        // each other, and the trainer discovers it can swing the line clean across the road at start/finish
        // for nothing. The lap length still comes from that sample — it is the honest loop length.
        float length = samples[samples.Count - 1].distance;
        if (track.closedLoop && samples.Count > 2 &&
            Vector2.Distance(samples[0].position, samples[samples.Count - 1].position) < 0.05f)
            samples.RemoveAt(samples.Count - 1);

        var anchors = track.BuildRacingLineAnchors();
        if (anchors == null || anchors.Count == 0)
        {
            summary = $"{track.name}: no racing-line anchors authored";
            return null;
        }

        var course = BuildCourse(track, car, samples, anchors, length);
        if (!course.IsValid(out string why))
        {
            summary = $"{track.name}: {why}";
            return null;
        }

        var limits = BuildLimits(car);

        // Where this session starts from. A track that has been out before picks up its own line and goes
        // again — that is the difference between practising and starting over every time, and it is the only
        // reason a second session finds anything. The authored line is still measured either way, because it
        // is the baseline every stored gain is quoted against: what the AI used to drive.
        var stored = continueFromStored ? LoadFromDisk(track.name) : null;
        if (stored != null && !stored.MatchesLength(length)) stored = null;

        float[] authored = BuildSeedLine(track, samples, anchors, length, course);
        float baseline = stored != null && stored.baselineLapTime > 0.01f
            ? stored.baselineLapTime
            : RacingLineTrainer.LapTime(course, authored, limits);

        float[] seed = authored;
        int refinePass = 0;
        if (stored != null)
        {
            var carried = new float[samples.Count];
            for (int i = 0; i < samples.Count; i++) carried[i] = stored.LateralAt(samples[i].distance);
            RacingLineTrainer.ClampToCorridor(course, carried, 0f);
            // Only build on it if it really is the quicker line — a stored line from a different car or a
            // different grip setting could be slower here, and practice should never make the AI worse.
            if (RacingLineTrainer.LapTime(course, carried, limits) <
                RacingLineTrainer.LapTime(course, authored, limits))
            {
                seed = carried;
                // A line found by an older optimiser keeps its lap time but starts the schedule over: the
                // new search has trials the old one never had, and they earn their keep at corner scale.
                // Only a line this trainer already practised gets to skip the coarse rounds.
                refinePass = stored.trainerVersion >= TrainedRacingLine.CurrentTrainerVersion
                    ? stored.refinePasses + 1
                    : 0;
            }
        }

        var settings = RacingLineTrainer.Settings.For(length, refinePass);

        var report = RacingLineTrainer.Train(course, seed, limits, settings);
        if (report.lateral == null || report.trainedLapTime >= float.MaxValue)
        {
            summary = $"{track.name}: training produced no line";
            return null;
        }

        // Store the line the session found — but store the one the session STARTED from if that turns out to
        // be the quicker of the two once both are on the storage grid. The optimiser measures the line at the
        // track's own sample spacing; the game reads it back off an even grid with a lerp, and on a track
        // sampled finer than that grid the round trip smooths apexes that curvature (a second derivative) is
        // very sensitive to. Measuring what is about to be written is the only honest number, and picking
        // between the two candidates is what makes practice monotonic instead of a random walk.
        var trainedGrid = OnStorageGrid(samples, length, report.lateral);
        var seedGrid = OnStorageGrid(samples, length, seed);
        var trainedAsStored = ReadBack(course, samples, length, trainedGrid);
        var seedAsStored = ReadBack(course, samples, length, seedGrid);

        var chosen = RacingLineTrainer.PickFaster(course, limits, trainedAsStored, seedAsStored);
        bool keptTheSeed = ReferenceEquals(chosen, seedAsStored);
        var telemetry = RacingLineTrainer.Analyse(course, chosen, limits);

        var line = new TrainedRacingLine
        {
            version = TrainedRacingLine.CurrentVersion,
            trackId = track.name,
            vehicle = car.name,
            trainedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            trainerVersion = TrainedRacingLine.CurrentTrainerVersion,
            trackLength = length,
            spacing = StorageSpacing,
            baselineLapTime = baseline,
            seedLapTime = report.seedLapTime,
            trainedLapTime = telemetry.lapTimeSeconds,
            lateralAccelMps2 = limits.lateralAccelMps2,
            drivenLength = telemetry.drivenLength,
            lapsSimulated = report.lapsSimulated,
            totalLapsSimulated = (stored != null ? LapsBehind(stored) : 0) + report.lapsSimulated,
            refinePasses = refinePass,
            lateral = keptTheSeed ? seedGrid : trainedGrid
        };

        Write(line);
        summary = $"{track.name,-18} session {refinePass + 1}: {report.seedLapTime,7:F2}s -> {line.trainedLapTime,7:F2}s " +
                  $"({report.lapsSimulated} laps, {report.improvements} kept" +
                  (keptTheSeed ? ", storage round trip cost more than the session found — kept the old line" : "") +
                  $")  |  {line.GainSeconds,5:F2}s off the authored line";
        return line;
    }

    // A lateral profile put onto the even storage grid, rounded the way it will be written out. One extra
    // entry at the loop end carries the line's own start value, so the resample interpolates ACROSS
    // start/finish instead of holding the last sample flat up to it.
    static float[] OnStorageGrid(List<TrackBuilder.Sample> samples, float length, float[] lateral)
    {
        int last = samples.Count;
        var distances = new float[last + 1];
        var values = new float[last + 1];
        for (int i = 0; i < last; i++)
        {
            distances[i] = samples[i].distance;
            values[i] = lateral[i];
        }
        distances[last] = length;
        values[last] = lateral[0];

        var grid = new TrainedRacingLine
        {
            trackLength = length,
            spacing = StorageSpacing,
            lateral = TrainedRacingLine.Resample(distances, values, length, StorageSpacing, out _)
        };
        grid.RoundForStorage();
        return grid.lateral;
    }

    // ...and read straight back out again, exactly the way SplineDriver will.
    static float[] ReadBack(RacingLineTrainer.Course course, List<TrackBuilder.Sample> samples, float length,
                            float[] grid)
    {
        var reader = new TrainedRacingLine { trackLength = length, spacing = StorageSpacing, lateral = grid };
        var lat = new float[samples.Count];
        for (int i = 0; i < samples.Count; i++) lat[i] = reader.LateralAt(samples[i].distance);
        RacingLineTrainer.ClampToCorridor(course, lat, 0f);
        return lat;
    }

    // --- Geometry ----------------------------------------------------------------------------------------

    static List<TrackBuilder.Sample> Sample(TrackInfoV2 track) => TrackBuilder.SampleSegments(
        track.startPosition,
        track.startHeading,
        track.segments,
        track.defaultWidth,
        Mathf.Max(1, track.samplesPerSegment),
        Mathf.Max(0.1f, track.maxArcStepMetres),
        track.closedLoop,
        seg => seg.width <= 0f ? track.defaultWidth : seg.width);

    static float SampledLength(TrackInfoV2 track)
    {
        var s = Sample(track);
        return (s == null || s.Count == 0) ? 0f : s[s.Count - 1].distance;
    }

    // The corridor is the authored leftmost/rightmost AI lines — the same bounds SplineDriver already clamps
    // its smoothed line to, so training cannot put a car anywhere the current code would not.
    static RacingLineTrainer.Course BuildCourse(TrackInfoV2 track, VehicleInfo car,
        List<TrackBuilder.Sample> samples, List<TrackInfoV2.RacingLineAnchor> anchors, float length)
    {
        int n = samples.Count;
        var course = new RacingLineTrainer.Course
        {
            centre = new Vector2[n],
            right = new Vector2[n],
            minLateral = new float[n],
            maxLateral = new float[n],
            speedCapMps = new float[n],
            bankingBonusMps = new float[n],
            loop = track.closedLoop
        };

        var segStart = new float[track.segments.Length];
        float cum = 0f;
        for (int i = 0; i < track.segments.Length; i++) { segStart[i] = cum; cum += track.segments[i].length; }

        float topMps = Mathf.Max(10f, car.topSpeed) * MphToMps;

        for (int i = 0; i < n; i++)
        {
            var s = samples[i];
            course.centre[i] = s.position;
            course.right[i] = s.normal;   // TrackBuilder emits normal = (tangent.y, -tangent.x) = right of travel
            course.minLateral[i] = track.GetLateralAt(s.distance, -1f, anchors, length);
            course.maxLateral[i] = track.GetLateralAt(s.distance, +1f, anchors, length);

            int segIdx = SegmentIndexAt(segStart, s.distance, cum);
            var seg = track.segments[Mathf.Clamp(segIdx, 0, track.segments.Length - 1)];
            course.speedCapMps[i] = seg.maxSpeed > 0 ? Mathf.Min(topMps, seg.maxSpeed * MphToMps) : topMps;
            course.bankingBonusMps[i] = seg.banking * car.bankingMphPerDegree * MphToMps;
        }
        return course;
    }

    // The line the AI drive today: authored ideal, then SplineDriver's min-curvature relaxation, clamped to
    // the corridor. Training starts here, so every stored gain is a gain over the shipped behaviour.
    static float[] BuildSeedLine(TrackInfoV2 track, List<TrackBuilder.Sample> samples,
        List<TrackInfoV2.RacingLineAnchor> anchors, float length, RacingLineTrainer.Course course)
    {
        int n = samples.Count;
        var lat = new float[n];
        for (int i = 0; i < n; i++) lat[i] = track.GetLateralAt(samples[i].distance, 0f, anchors, length);

        var tmp = new float[n];
        for (int p = 0; p < SeedSmoothingIterations; p++)
        {
            for (int i = 0; i < n; i++)
            {
                int prev = i == 0 ? (course.loop ? n - 1 : 0) : i - 1;
                int next = i == n - 1 ? (course.loop ? 0 : n - 1) : i + 1;
                float avg = 0.5f * (lat[prev] + lat[next]);
                float relaxed = Mathf.Lerp(lat[i], avg, SeedSmoothingRelaxation);
                float lo = Mathf.Min(course.minLateral[i], course.maxLateral[i]);
                float hi = Mathf.Max(course.minLateral[i], course.maxLateral[i]);
                tmp[i] = Mathf.Clamp(relaxed, lo, hi);
            }
            var swap = tmp; tmp = lat; lat = swap;
        }
        return lat;
    }

    static int SegmentIndexAt(float[] segStart, float distance, float total)
    {
        if (total > 0f) distance = ((distance % total) + total) % total;
        int idx = 0;
        for (int i = 0; i < segStart.Length; i++)
        {
            if (segStart[i] <= distance) idx = i;
            else break;
        }
        return idx;
    }

    // The grip and the engine the AI actually race with, read live off TrackConditions so a training run
    // reflects whatever the sliders are set to. AiEffective is the same number SplineDriver and
    // SplineInputDriver fold into their friction circles.
    static RacingLineTrainer.CarLimits BuildLimits(VehicleInfo car)
    {
        float maxLatG = car.maxLateralG > 0.01f ? car.maxLateralG : 1.8f;
        var limits = new RacingLineTrainer.CarLimits
        {
            lateralAccelMps2 = maxLatG * Mathf.Max(0.05f, TrackConditions.AiEffective) * 9.81f,
            topSpeedMps = Mathf.Max(10f, car.topSpeed) * MphToMps,
            fallbackAccelMps2 = 5f,
            fallbackDecelMps2 = 10f,
            cornerSpeedScale = CornerSpeedScale
        };

        var accel = car.accelerationCurve;
        if (accel != null && accel.length > 0)
            limits.AccelAtMps = mps => Mathf.Max(0f, accel.Evaluate(mps * 2.237f));

        var decel = car.decelerationCurve;
        if (decel != null && decel.length > 0)
            limits.DecelAtMps = mps => Mathf.Max(0.1f, decel.Evaluate(mps * 2.237f));

        return limits;
    }

    // --- Assets ------------------------------------------------------------------------------------------

    static VehicleInfo DefaultVehicle()
    {
        var car = Resources.Load<VehicleInfo>(DefaultVehicleResource);
        if (car == null)
            Debug.LogError($"[RacingLineTrainer] No VehicleInfo at Resources/{DefaultVehicleResource}; " +
                           "training needs a car to measure the lap against.");
        return car;
    }

    static List<TrackInfoV2> AllTrackGeometry()
    {
        var list = new List<TrackInfoV2>();
        foreach (var guid in AssetDatabase.FindAssets("t:TrackInfoV2", new[] { "Assets/Resources/Tracks" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<TrackInfoV2>(path);
            if (asset != null) list.Add(asset);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    static TrainedRacingLine LoadFromDisk(string trackId)
    {
        string path = Path.Combine(OutputFolder, trackId + ".json");
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<TrainedRacingLine>(File.ReadAllText(path)); }
        catch { return null; }
    }

    static void Write(TrainedRacingLine line)
    {
        Directory.CreateDirectory(OutputFolder);
        string path = Path.Combine(OutputFolder, line.trackId + ".json");
        File.WriteAllText(path, line.ToCompactJson());
        AssetDatabase.ImportAsset(path.Replace('\\', '/'));
        TrainedRacingLines.Invalidate();
    }
}
