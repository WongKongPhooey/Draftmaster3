using UnityEditor;
using UnityEngine;

// Logs the pit-box fit maths for the open scene: pit length, box-lane strip span, and the spacing
// GridSpawner would compute for the current field size. For chasing "field doesn't fit" layouts.
public static class PitFitDebug
{
    [MenuItem("Draftmaster/Debug/Log Pit Fit")]
    static void LogPitFit()
    {
        var track = Object.FindFirstObjectByType<TrackBuilder>();
        var spawner = Object.FindFirstObjectByType<GridSpawner>();
        if (track == null || spawner == null) { Debug.LogWarning("PitFitDebug: no TrackBuilder/GridSpawner in scene"); return; }

        var pit = track.SamplePitCenterline();
        float pitLen = pit.Count > 0 ? pit[pit.Count - 1].distance : 0f;
        int totalBoxes = spawner.count + 1; // + player

        var fit = PitLane.FitBoxes(track, pitLen, totalBoxes);

        Debug.Log($"PitFit: pitLen={pitLen:0.0} strip=[{fit.spanFrom:0.0}..{fit.spanTo:0.0}] usable={fit.usable:0.0} " +
                  $"boxes={fit.boxes} rawSpacing={fit.rawSpacing:0.00} clampedSpacing={fit.spacing:0.00} " +
                  $"span={fit.Span:0.0} overflow={fit.Overflow:0.0}");
    }
}
