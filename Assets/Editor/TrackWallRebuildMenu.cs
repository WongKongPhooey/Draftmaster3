using UnityEditor;
using UnityEngine;

// Rebuilds only a package's TrackEnvironmentBuilder output (walls, strips, run-off, decorations) after its
// TrackEnvironment asset changes - without re-running the full Author / dress builders, which re-lay the paddock
// and would overwrite hand edits in the package.
public static class TrackWallRebuildMenu
{
    // Daytona's infield wall, three track widths in from the infield edge, and the paved back straight
    // (DaytonaInfieldWall), then a re-scatter so the scenery and fan RVs stand behind the wall, not in front of it.
    [MenuItem("Draftmaster/Tracks/Daytona/Infield Wall + Paved Back Straight (rebuild walls, re-scatter)")]
    static void DaytonaInfieldWallMenu()
    {
        Debug.Log("TrackWallRebuildMenu: " + ApplyDaytonaInfieldWall());
    }

    public static string ApplyDaytonaInfieldWall()
    {
        string note = null;
        string walls = RebuildEnvironment(DaytonaInfieldWall.TrackId, contents =>
        {
            var package = contents.GetComponent<TrackPackage>();
            var builder = package != null ? package.Builder : contents.GetComponentInChildren<TrackBuilder>(true);
            var envBuilder = contents.GetComponentInChildren<TrackEnvironmentBuilder>(true);
            if (builder == null || builder.track == null || envBuilder == null || envBuilder.environment == null)
                return false;
            note = DaytonaInfieldWall.Apply(envBuilder.environment, builder);
            AssetDatabase.SaveAssets();
            return true;
        });
        if (note == null) return $"Daytona left alone - {walls}";
        return $"{note}; {walls}; {TrackSceneryGenerator.ScatterPackage(DaytonaInfieldWall.TrackId)}";
    }

    // Texas (FortWorth): open the infield wall along the whole trioval - the front stretch from the end of turn 4 to
    // the start of turn 1 (segments 6, 7, 0, 1, 2: the straight, the start/finish kink, the straight, the second
    // kink and the straight into turn 1). Left of travel is BarrierSide.Outer, the infield wall. Re-running it is
    // harmless: the gaps are replaced, not added twice. The outside wall is untouched.
    static readonly int[] TexasTriovalSegments = { 6, 7, 0, 1, 2 };
    const string TexasTriovalLabel = "trioval (no infield wall)";

    [MenuItem("Draftmaster/Tracks/Texas/Remove Trioval Infield Wall (rebuild walls only)")]
    static void TexasNoTriovalInfieldWall()
    {
        var env = AssetDatabase.LoadAssetAtPath<TrackEnvironment>("Assets/Resources/Tracks/FortWorthEnvironment.asset");
        var track = AssetDatabase.LoadAssetAtPath<TrackInfoV2>("Assets/Resources/Tracks/FortWorth.asset");
        if (env == null || track == null) { Debug.LogError("TrackWallRebuildMenu: no FortWorth / FortWorthEnvironment asset."); return; }
        var gaps = new System.Collections.Generic.List<TrackEnvironment.BarrierGap>();
        if (env.barrierGaps != null)
            foreach (var g in env.barrierGaps) if (g.label != TexasTriovalLabel) gaps.Add(g);
        foreach (int seg in TexasTriovalSegments)
        {
            if (track.segments == null || seg >= track.segments.Length) continue;
            gaps.Add(new TrackEnvironment.BarrierGap
            {
                label = TexasTriovalLabel,
                side = TrackEnvironment.BarrierSide.Outer,
                segmentIndex = seg,
                startDistance = 0f,
                endDistance = track.segments[seg].length + 1f,   // clamped to the segment: the whole of it
            });
        }
        env.barrierGaps = gaps.ToArray();
        EditorUtility.SetDirty(env);
        AssetDatabase.SaveAssets();
        Debug.Log("TrackWallRebuildMenu: " + RebuildEnvironment("FortWorth"));
    }

    [MenuItem("Draftmaster/Tracks/Daytona/Report Wall Pieces")]
    static void ReportDaytona()
    {
        var contents = PrefabUtility.LoadPrefabContents("Assets/Resources/TrackPackages/Daytona.prefab");
        try
        {
            int inner = 0, outer = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var t in contents.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Barrier_Inner")) inner++;
                if (t.name.StartsWith("Barrier_Outer")) { outer++; if (outer <= 5) sb.Append(' ').Append(Path(t)); }
            }
            foreach (var b in contents.GetComponentsInChildren<TrackEnvironmentBuilder>(true))
            {
                sb.Append($" | builder {Path(b.transform)} env {AssetDatabase.GetAssetPath(b.environment)} instance {PrefabUtility.IsPartOfPrefabInstance(b.gameObject)} hide {b.transform.GetChild(0).gameObject.hideFlags} innerSide {b.environment?.innerSideBarrier} outerSide {b.environment?.outerSideBarrier} children:");
                foreach (Transform c in b.transform) sb.Append(' ').Append(c.name).Append('(').Append(c.childCount).Append(')');
            }
            Debug.Log($"TrackWallRebuildMenu report: Barrier_Inner {inner}, Barrier_Outer {outer}:{sb}");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    // editFirst: optional edit to the environment asset(s) with the package loaded; returning false stops without saving.
    public static string RebuildEnvironment(string trackId, System.Func<GameObject, bool> editFirst = null)
    {
        string path = $"Assets/Resources/TrackPackages/{trackId}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null) return $"no package at {path}";
        try
        {
            if (editFirst != null && !editFirst(contents)) return $"{trackId}: package has no TrackBuilder / environment to edit";
            var builders = contents.GetComponentsInChildren<TrackEnvironmentBuilder>(true);
            int before = 0;
            foreach (var b in builders)
            {
                // Loading the contents already ran the builder's OnEnable build, whose clear doesn't take during a
                // load - so the old walls are still there under the new ones. Clear by hand, then build once.
                before += b.transform.childCount;
                for (int i = b.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(b.transform.GetChild(i).gameObject, true);
                b.Build();
            }
            int walls = 0;
            foreach (var b in builders)
            {
                var root = b.transform.Find("Barriers");
                if (root != null) walls += root.childCount;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            int after = 0;
            foreach (var b in builders) after += b.transform.childCount;
            return $"{trackId}: rebuilt {builders.Length} environment(s), {before} -> {after} generated roots, {walls} wall pieces";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
