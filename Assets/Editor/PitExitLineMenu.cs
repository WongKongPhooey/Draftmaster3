using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Paints the pit exit line — the point where PitLimiter releases the player and the AI drop their pit pace.
// Until now that release distance existed only in code, so the player had nothing to aim at.
//
// TrackEnvironmentBuilder anchors any strip labelled "PitExitLine" (on the Pit spline) to
// TrackInfoV2.PitExitLineDistance, so the paint and the rule can never drift apart. Strip arrays can't be
// grown through the MCP property API, hence a menu item — same reason FinishLineStripMenu exists.
public static class PitExitLineMenu
{
    const string Label = "PitExitLine";
    const string MaterialPath = "Assets/Materials/White.mat";
    const float BandMetres = 0.6f;   // depth of the painted line along the lane
    const int SortingOrder = 2;      // above the edge lines (1), below the marker boards (3)

    [MenuItem("Tools/Track/Add Missing Pit Exit Lines")]
    public static void AddMissingPitExitLines()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
            Debug.LogWarning($"[PitExitLine] No material at {MaterialPath} — the line will be added untextured.");

        int added = 0, skipped = 0, noPit = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:TrackEnvironment"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var env = AssetDatabase.LoadAssetAtPath<TrackEnvironment>(path);
            if (env == null) continue;

            var track = FindTrackFor(env);
            if (track == null || !track.hasPitLane || track.pitSegments == null || track.pitSegments.Length == 0)
            {
                noPit++;
                continue;
            }

            if (HasPitExitStrip(env))
            {
                skipped++;
                continue;
            }

            var strip = new TrackEnvironment.Strip
            {
                label = Label,
                useSpline = TrackEnvironment.SplineRef.Pit,
                anchor = TrackEnvironment.LateralAnchor.Centerline,
                // The builder overrides this span with PitExitLineDistance and keeps the band depth,
                // so the authored numbers only need to carry the right thickness.
                startSegmentIndex = 0,
                startDistance = 0f,
                endSegmentIndex = 0,
                endDistance = BandMetres,
                lateralOffset = 0f,
                width = WidestPitLane(track),
                sortingOrder = SortingOrder,
                material = material,
                uvLengthScale = 1f,
            };

            var list = new List<TrackEnvironment.Strip>(env.strips ?? new TrackEnvironment.Strip[0]) { strip };
            env.strips = list.ToArray();
            EditorUtility.SetDirty(env);
            added++;
            Debug.Log($"[PitExitLine] Added a {BandMetres}m line to {env.name} at {track.PitExitLineDistance:0.#}m " +
                      $"along {track.name}'s {track.PitLaneLength:0.#}m pit lane.");
        }

        if (added > 0) AssetDatabase.SaveAssets();
        Debug.Log($"[PitExitLine] Done — {added} added, {skipped} already had one, {noPit} track(s) have no pit lane. " +
                  "Run Tools > Track > Rebuild Track Environment to see it in the open scene.");
    }

    static bool HasPitExitStrip(TrackEnvironment env)
    {
        if (env.strips == null) return false;
        for (int i = 0; i < env.strips.Length; i++)
        {
            string label = env.strips[i].label;
            if (!string.IsNullOrEmpty(label) && label.ToLowerInvariant().Replace(" ", "").Contains("pitexit"))
                return true;
        }
        return false;
    }

    // Environment assets are named "<Track>Environment" alongside "<Track>" — the only link between the two.
    static TrackInfoV2 FindTrackFor(TrackEnvironment env)
    {
        string baseName = env.name.EndsWith("Environment")
            ? env.name.Substring(0, env.name.Length - "Environment".Length)
            : env.name;

        foreach (var guid in AssetDatabase.FindAssets($"t:TrackInfoV2 {baseName}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var track = AssetDatabase.LoadAssetAtPath<TrackInfoV2>(path);
            if (track != null && track.name == baseName) return track;
        }
        return null;
    }

    static float WidestPitLane(TrackInfoV2 track)
    {
        float width = track.pitDefaultWidth > 0f ? track.pitDefaultWidth : track.defaultWidth;
        if (track.pitSegments != null)
            for (int i = 0; i < track.pitSegments.Length; i++)
                width = Mathf.Max(width, track.pitSegments[i].width);
        return width;
    }
}
