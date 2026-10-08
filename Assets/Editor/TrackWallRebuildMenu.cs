using UnityEditor;
using UnityEngine;

// Rebuilds only a package's TrackEnvironmentBuilder output (walls, strips, run-off, decorations) after its
// TrackEnvironment asset changes - without re-running the full Author / dress builders, which re-lay the paddock
// and would overwrite hand edits in the package.
public static class TrackWallRebuildMenu
{
    [MenuItem("Draftmaster/Tracks/Daytona/Remove Infield Wall (rebuild walls only)")]
    static void DaytonaNoInfieldWall()
    {
        var env = AssetDatabase.LoadAssetAtPath<TrackEnvironment>("Assets/Resources/Tracks/DaytonaEnvironment.asset");
        if (env == null) { Debug.LogError("TrackWallRebuildMenu: no DaytonaEnvironment asset."); return; }
        // Left of travel on an anticlockwise oval is BarrierSide.Outer - the infield wall.
        env.outerSideBarrier = false;
        EditorUtility.SetDirty(env);
        AssetDatabase.SaveAssets();
        Debug.Log("TrackWallRebuildMenu: " + RebuildEnvironment("Daytona"));
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

    public static string RebuildEnvironment(string trackId)
    {
        string path = $"Assets/Resources/TrackPackages/{trackId}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        if (contents == null) return $"no package at {path}";
        try
        {
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
