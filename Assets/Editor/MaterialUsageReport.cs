#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Lists which scenes and prefabs depend on a given material.
//
// Written because applying the pixel standard retiles shared material assets to (1,1) -- correct for the
// spline scenes, whose generated meshes now carry the density in their UVs, but wrong for anything still
// drawing that material on a plain 0..1-UV quad. This answers "who else uses this?" without opening every
// scene, using the asset database's static dependency graph.
public static class MaterialUsageReport
{
    static readonly string[] kWatch =
    {
        "Assets/Materials/TrackSurface.mat",
        "Assets/Materials/FinishLine.mat",
        "Assets/Materials/Kerb.mat",
        "Assets/Materials/Grass.mat",
    };

    [MenuItem("Draftmaster/Art/Report Surface Material Usage", priority = 103)]
    public static void Run()
    {
        var scenes = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .OrderBy(p => p)
            .ToList();
        var prefabs = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .OrderBy(p => p)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Surface material usage");
        sb.AppendLine();
        sb.AppendLine("Which scenes and prefabs reference each retiled surface material. A scene listed here " +
                      "that does NOT use the spline builders (TrackBuilder / TrackEnvironmentBuilder / " +
                      "ExtraTrackSpline) would still be drawing the material on 0..1 UVs, and needs its own " +
                      "copy of the material rather than the retiled shared one.");
        sb.AppendLine();

        foreach (var mat in kWatch)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(mat) == null)
            {
                sb.AppendLine($"## {Path.GetFileName(mat)}\n\n_asset not found_\n");
                continue;
            }

            var usingScenes = scenes.Where(s => AssetDatabase.GetDependencies(s, true).Contains(mat)).ToList();
            var usingPrefabs = prefabs.Where(p => AssetDatabase.GetDependencies(p, true).Contains(mat)).ToList();

            sb.AppendLine($"## {Path.GetFileName(mat)}");
            sb.AppendLine();
            sb.AppendLine($"Scenes ({usingScenes.Count}):");
            if (usingScenes.Count == 0) sb.AppendLine("- none");
            foreach (var s in usingScenes) sb.AppendLine($"- `{s}`");
            sb.AppendLine();
            sb.AppendLine($"Prefabs ({usingPrefabs.Count}):");
            if (usingPrefabs.Count == 0) sb.AppendLine("- none");
            foreach (var p in usingPrefabs) sb.AppendLine($"- `{p}`");
            sb.AppendLine();
        }

        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/SurfaceMaterialUsage.md", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[MaterialUsageReport] wrote Docs/SurfaceMaterialUsage.md");
    }
}
#endif
