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

        float boxSpanFrom = 6f, boxSpanTo = pitLen - 12f;
        if (track.HasPitBoxLane && pitLen > 0f)
        {
            const float bandMargin = 3f;
            boxSpanFrom = track.PitBoxLaneFrom(pitLen) + bandMargin;
            boxSpanTo = Mathf.Max(boxSpanFrom, track.PitBoxLaneTo(pitLen) - bandMargin);
        }
        float usable = Mathf.Max(0f, boxSpanTo - boxSpanFrom);
        float rawSpacing = totalBoxes > 1 ? usable / (totalBoxes - 1) : 0f;
        float spacing = Mathf.Clamp(rawSpacing, 4.5f, 12f);
        float span = (totalBoxes - 1) * spacing;

        Debug.Log($"PitFit: pitLen={pitLen:0.0} strip=[{boxSpanFrom:0.0}..{boxSpanTo:0.0}] usable={usable:0.0} " +
                  $"boxes={totalBoxes} rawSpacing={rawSpacing:0.00} clampedSpacing={spacing:0.00} " +
                  $"span={span:0.0} overflow={Mathf.Max(0f, span - usable):0.0}");
    }
}
