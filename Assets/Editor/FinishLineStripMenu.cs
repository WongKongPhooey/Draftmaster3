using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Adds the painted start/finish line to a TrackEnvironment. TrackEnvironmentBuilder already knows how to
// draw one — BuildStrips() anchors any strip whose label contains "finish" to TrackInfoV2.startFinishDistance
// — but WatkinsGlen's environment had no such strip, so the line was never drawn even though lap counting,
// the grid and the race finish all used it.
//
// Strip arrays can't be grown through the MCP property API, hence a menu item.
public static class FinishLineStripMenu
{
    const string FinishLabel = "FinishLine";
    const string FinishMaterialPath = "Assets/Materials/FinishLine.mat";
    const float BandMetres = 1.5f;      // depth of the painted band along the track
    const int SortingOrder = 2;         // above the edge lines (1), below the marker boards (3)

    [MenuItem("Tools/Track/Add Missing Finish Line Strips")]
    public static void AddMissingFinishLineStrips()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FinishMaterialPath);
        if (material == null)
            Debug.LogWarning($"[FinishLine] No material at {FinishMaterialPath} — strips will be added untextured.");

        var guids = AssetDatabase.FindAssets("t:TrackEnvironment");
        int added = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var env = AssetDatabase.LoadAssetAtPath<TrackEnvironment>(path);
            if (env == null) continue;

            if (HasFinishStrip(env))
            {
                skipped++;
                Debug.Log($"[FinishLine] {env.name} already has a finish strip — left alone.");
                continue;
            }

            var track = FindTrackFor(env);
            float width = WidestRoad(track);

            var strip = new TrackEnvironment.Strip
            {
                label = FinishLabel,
                useSpline = TrackEnvironment.SplineRef.Main,
                anchor = TrackEnvironment.LateralAnchor.Centerline,
                // BuildStrips overrides the start with startFinishDistance and keeps this band length,
                // so the authored span only needs to carry the right depth.
                startSegmentIndex = 0,
                startDistance = 0f,
                endSegmentIndex = 0,
                endDistance = BandMetres,
                lateralOffset = 0f,
                width = width,
                sortingOrder = SortingOrder,
                material = material,
                uvLengthScale = 1f,
            };

            var list = new List<TrackEnvironment.Strip>(env.strips ?? new TrackEnvironment.Strip[0]) { strip };
            env.strips = list.ToArray();
            EditorUtility.SetDirty(env);
            added++;

            string where = track != null ? $"{track.startFinishDistance}m on {track.name}" : "its track's start/finish";
            Debug.Log($"[FinishLine] Added a {BandMetres}m x {width}m finish strip to {env.name}, anchored at {where}.");
        }

        if (added > 0) AssetDatabase.SaveAssets();
        Debug.Log($"[FinishLine] Done — {added} added, {skipped} already had one.");
    }

    // The builder only rebuilds from its own inspector; this reruns it for the open scene so an asset
    // edit can be seen without hunting for the component.
    [MenuItem("Tools/Track/Rebuild Track Environment")]
    public static void RebuildTrackEnvironment()
    {
        var builders = Object.FindObjectsByType<TrackEnvironmentBuilder>(FindObjectsSortMode.None);
        if (builders.Length == 0) { Debug.LogWarning("[FinishLine] No TrackEnvironmentBuilder in the open scene."); return; }
        foreach (var b in builders) b.Build();
        Debug.Log($"[FinishLine] Rebuilt {builders.Length} track environment(s).");
    }

    static bool HasFinishStrip(TrackEnvironment env)
    {
        if (env.strips == null) return false;
        for (int i = 0; i < env.strips.Length; i++)
        {
            string label = env.strips[i].label;
            if (!string.IsNullOrEmpty(label) && label.ToLowerInvariant().Contains("finish")) return true;
        }
        return false;
    }

    // Environment assets are named "<Track>Environment" alongside "<Track>", which is the only link
    // between the two — TrackEnvironment holds no reference back to its TrackInfoV2.
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

    // Span the widest point of the road so the line reaches both edges even where a segment widens.
    static float WidestRoad(TrackInfoV2 track)
    {
        if (track == null) return 12f;
        float width = track.defaultWidth;
        if (track.segments != null)
            for (int i = 0; i < track.segments.Length; i++)
                width = Mathf.Max(width, track.segments[i].width);
        return width;
    }
}
