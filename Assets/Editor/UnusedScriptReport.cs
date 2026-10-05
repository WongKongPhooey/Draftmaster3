using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draftmaster > Tools > Report Unused Scripts — lists every runtime MonoScript under Assets/Scripts
/// that no shipped asset reaches. "Shipped" = the enabled build scenes, everything under a Resources
/// folder, the preloaded assets and the render pipeline settings, followed through their dependencies.
/// Each unreached script also lists the non-shipped assets (legacy scenes, stray prefabs) that still
/// hold it, so a sweep can tell "dead" from "only alive in a Draftmaster 2 scene". Code-only use
/// (AddComponent&lt;T&gt;, static calls) is invisible here — check with a compile before deleting.
/// Writes Temp/UnusedScriptReport.txt.
/// </summary>
public static class UnusedScriptReport
{
    [MenuItem("Draftmaster/Tools/Report Unused Scripts")]
    public static void Run()
    {
        var roots = new List<string>();
        roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        roots.AddRange(AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)));
        foreach (var o in PlayerSettings.GetPreloadedAssets())
            if (o != null) roots.Add(AssetDatabase.GetAssetPath(o));
        if (GraphicsSettings.defaultRenderPipeline != null)
            roots.Add(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            var rp = QualitySettings.GetRenderPipelineAssetAt(i);
            if (rp != null) roots.Add(AssetDatabase.GetAssetPath(rp));
        }

        var reached = new HashSet<string>(AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true));

        var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.Contains("/Editor/"))
            .OrderBy(p => p)
            .ToList();
        var unreached = scripts.Where(p => !reached.Contains(p)).ToList();

        // Reverse index: which scenes/prefabs/assets directly hold each script.
        var holders = scripts.ToDictionary(p => p, p => new List<string>());
        var containers = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && (p.EndsWith(".unity") || p.EndsWith(".prefab") || p.EndsWith(".asset")));
        foreach (var c in containers)
            foreach (var d in AssetDatabase.GetDependencies(c, false))
                if (holders.TryGetValue(d, out var list)) list.Add(c);

        var sb = new StringBuilder();
        sb.AppendLine($"roots {roots.Count}, reached {reached.Count}, runtime scripts {scripts.Count}, unreached {unreached.Count}");
        sb.AppendLine("== REACHED ==");
        foreach (var p in scripts.Where(reached.Contains))
            sb.AppendLine(p + (holders[p].Count == 0 ? "" : "  <- " + string.Join(" | ", holders[p])));
        sb.AppendLine("== UNREACHED ==");
        foreach (var p in unreached)
            sb.AppendLine(p + (holders[p].Count == 0 ? "" : "  <- " + string.Join(" | ", holders[p])));
        sb.AppendLine("== REACHED NON-SCRIPT (Assets) ==");
        foreach (var p in reached.Where(p => p.StartsWith("Assets/") && !p.EndsWith(".cs")).OrderBy(p => p)) sb.AppendLine(p);

        // Every direct asset -> asset reference in Assets/, plus the shipped roots, for offline orphan sweeps
        // (the binary scenes and prefabs can't be grepped for GUIDs).
        var edges = new StringBuilder();
        foreach (var r in roots.Distinct()) edges.AppendLine("ROOT\t" + r);
        foreach (var c in AssetDatabase.GetAllAssetPaths())
        {
            if (!c.StartsWith("Assets/") || c.EndsWith(".cs") || AssetDatabase.IsValidFolder(c)) continue;
            foreach (var d in AssetDatabase.GetDependencies(c, false))
                if (d != c) edges.AppendLine(c + "\t" + d);
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/UnusedScriptReport.txt", sb.ToString());
        File.WriteAllText("Temp/AssetEdges.txt", edges.ToString());
        Debug.Log($"[UnusedScriptReport] {unreached.Count}/{scripts.Count} runtime scripts unreached — Temp/UnusedScriptReport.txt");
    }
}
