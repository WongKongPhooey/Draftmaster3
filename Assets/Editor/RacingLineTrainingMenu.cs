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

    // Metres between stored lateral samples. Fine enough that the resample is invisible next to a 12m road,
    // coarse enough that 38 tracks of JSON stay a sensible size.
    const float StorageSpacing = 3f;

    // What the AI's line looks like TODAY: the authored ideal, relaxed toward minimum curvature. Matches
    // SplineDriver's serialized defaults, so the "seed" lap time in each file is the pace we started from.
    const int SeedSmoothingIterations = 60;
    const float SeedSmoothingRelaxation = 0.3f;

    // SplineDriver.cornerSpeedScale's default — the margin the AI keeps off the grip limit.
    const float CornerSpeedScale = 0.95f;

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

    [MenuItem("Draftmaster/AI/Retrain Every Racing Line", priority = 302)]
    public static void RetrainAll() => TrainAll(true);

    [MenuItem("Draftmaster/AI/Report Trained Racing Lines", priority = 310)]
    public static void Report()
    {
        var sb = new StringBuilder("[RacingLineTrainer] trained lines on disk:\n");
        int found = 0;
        float totalGain = 0f;
        foreach (var track in AllTrackGeometry())
        {
            var line = LoadFromDisk(track.name);
            if (line == null) { sb.AppendLine($"  {track.name,-18} —"); continue; }
            found++;
            totalGain += line.GainSeconds;
            sb.AppendLine($"  {track.name,-18} seed {line.seedLapTime,7:F2}s  trained {line.trainedLapTime,7:F2}s" +
                          $"  gain {line.GainSeconds,6:F2}s  ({line.lapsSimulated} laps, {line.lateral.Length} samples)");
        }
        sb.AppendLine($"  {found} trained, {totalGain:F1}s of lap time found in total.");
        Debug.Log(sb.ToString());
    }

    // --- The batch ---------------------------------------------------------------------------------------

    // Resumable on purpose: `force` off skips any track whose stored line still matches its geometry, so a
    // run that gets interrupted (or a tool call that times out) can simply be run again.
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
                    if (existing != null && existing.MatchesLength(SampledLength(track))) { skipped++; continue; }
                }

                var line = TrainTrack(track, car, out string summary);
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

    // --- One track ---------------------------------------------------------------------------------------

    public static TrainedRacingLine TrainTrack(TrackInfoV2 track, VehicleInfo car, out string summary)
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

        float[] seed = BuildSeedLine(track, samples, anchors, length, course);
        var limits = BuildLimits(car);
        var settings = RacingLineTrainer.Settings.For(length);

        var report = RacingLineTrainer.Train(course, seed, limits, settings);
        if (report.lateral == null || report.trainedLapTime >= float.MaxValue)
        {
            summary = $"{track.name}: training produced no line";
            return null;
        }

        // One extra entry at the loop end, carrying the line's own start value, so the even-grid resample
        // interpolates ACROSS start/finish instead of holding the last sample flat up to it.
        int last = samples.Count;
        var distances = new float[last + 1];
        var trainedLateral = new float[last + 1];
        for (int i = 0; i < last; i++)
        {
            distances[i] = samples[i].distance;
            trainedLateral[i] = report.lateral[i];
        }
        distances[last] = length;
        trainedLateral[last] = report.lateral[0];

        var line = new TrainedRacingLine
        {
            version = TrainedRacingLine.CurrentVersion,
            trackId = track.name,
            vehicle = car.name,
            trainedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            trackLength = length,
            spacing = StorageSpacing,
            seedLapTime = report.seedLapTime,
            trainedLapTime = report.trainedLapTime,
            lateralAccelMps2 = limits.lateralAccelMps2,
            drivenLength = report.drivenLength,
            lapsSimulated = report.lapsSimulated,
            lateral = TrainedRacingLine.Resample(distances, trainedLateral, length, StorageSpacing, out _)
        };
        line.RoundForStorage();

        Write(line);
        summary = $"{track.name,-18} seed {report.seedLapTime,7:F2}s -> {report.trainedLapTime,7:F2}s " +
                  $"({report.GainSeconds,5:F2}s over {report.lapsSimulated} laps, {report.improvements} kept)";
        return line;
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
