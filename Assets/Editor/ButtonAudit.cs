using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Draftmaster.Controls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lists every captioned uGUI Button in the demo's scenes and in every prefab under Assets, measured as it
// comes out on a 1920x1080 screen: plate height, and the caption's face, size, tracking and style. Anything
// off the kit's standard button (UIButton: 20 kit px tall = 60px at 1080p, display face at the Label size,
// no tracking, no bold/italic) is flagged, worst first, in Docs/ButtonAudit.md.
//
// Scenes are opened additively and closed again without saving, so nothing open is touched. Buttons built
// at runtime (most of the IMGUI race panels, the garage laptop's code-built strip) are not in any asset and
// are not listed — they take their sizes from PixelGUI.MenuButtonH / IronOvalUI.Button directly.
public static class ButtonAudit
{
    const string ReportPath = "Docs/ButtonAudit.md";
    const float ScreenH = 1080f;

    static readonly string[] DemoScenes =
    {
        "Assets/Scenes/TitleScreen.unity",
        "Assets/Scenes/SingleRace.unity",
        "Assets/Scenes/GarageScreen.unity",
        "Assets/Menus/TeamGarage.unity",
    };

    static readonly string[] ThirdParty = { "Assets/Ink/", "Assets/PlayFab", "Assets/TextMesh Pro/", "Assets/Plugins/" };

    struct Finding
    {
        public string asset, path, caption, font, problems;
        public float heightPx, fontPx, spacing;
        public bool off;
    }

    [MenuItem("Draftmaster/UI/Audit Buttons", false, 71)]
    public static void Run()
    {
        var findings = new List<Finding>();
        int prefabs = 0;
        var shipped = Shipped();

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (ThirdParty.Any(p => path.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))) continue;
            if (!shipped.Contains(path)) continue;
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            prefabs++;
            Collect(root, path, findings);
        }

        var opened = new List<Scene>();
        foreach (var scenePath in DemoScenes)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!File.Exists(scenePath)) continue;
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                opened.Add(scene);
            }
            foreach (var go in scene.GetRootGameObjects()) Collect(go, scenePath, findings);
        }
        foreach (var scene in opened) EditorSceneManager.CloseScene(scene, true);

        Write(findings, prefabs);
        Debug.Log($"[ButtonAudit] {findings.Count(f => f.off)} of {findings.Count} button(s) off standard — {ReportPath}");
    }

    // Prefabs the standardise pass leaves alone. The satnav is mid-rework in its own builder
    // (TravelMapPrefabBuilder), which owns its buttons and gets them from there.
    static readonly string[] Excluded = { "Assets/Resources/UI/TravelMap.prefab" };

    // Puts every captioned button in the prefabs the build reaches onto the standard (IronOvalUI.StandardiseButton:
    // plate height, caption face, size and weight), touching nothing else in them, and re-runs the audit.
    // Scenes are left to their builders: a scene's buttons come from the builder that made it.
    [MenuItem("Draftmaster/UI/Standardise Buttons In Prefabs", false, 72)]
    public static void Standardise()
    {
        var shipped = Shipped();
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!shipped.Contains(path) || Excluded.Contains(path)) continue;
            if (ThirdParty.Any(p => path.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))) continue;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !asset.GetComponentsInChildren<Button>(true).Any(Captioned)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = 0;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (!Captioned(button) || button.GetComponent("TravelNodeMarker") != null) continue;
                    IronOvalUI.StandardiseButton(button);
                    n++;
                }
                if (n > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed += n;
                    Debug.Log($"[ButtonAudit] standardised {n} button(s) in {path}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log($"[ButtonAudit] standardised {changed} button(s)");
        Run();
    }

    static bool Captioned(Button b) =>
        b.GetComponentInChildren<TMP_Text>(true) != null || b.GetComponentInChildren<Text>(true) != null;

    // What the game can reach: everything the build's scenes pull in, plus anything under a Resources folder
    // (loaded by name at runtime). The DM2-era prefabs no live scene uses fall outside it.
    static HashSet<string> Shipped()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var set = new HashSet<string>(AssetDatabase.GetDependencies(scenes, true));
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Resources/"))
                foreach (var dep in AssetDatabase.GetDependencies(path, true)) set.Add(dep);
        }
        return set;
    }

    static void Collect(GameObject root, string asset, List<Finding> into)
    {
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var rt = button.transform as RectTransform;
            if (rt == null) continue;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            var text = tmp == null ? button.GetComponentInChildren<Text>(true) : null;
            if (tmp == null && text == null) continue;   // an icon, a swatch or a hit area, not a captioned button
            if (button.GetComponent("TravelNodeMarker") != null) continue;   // a place on the satnav, labelled like a map

            float h = rt.rect.height;
            if (!TextSizeAudit.Measure(rt, h, out float heightPx, out _)) continue;   // world space
            float size = tmp != null ? (tmp.enableAutoSizing ? tmp.fontSizeMax : tmp.fontSize) : text.fontSize;
            TextSizeAudit.Measure(tmp != null ? tmp.transform : text.transform, size, out float fontPx, out _);

            string font = tmp != null ? (tmp.font != null ? tmp.font.name : "(none)") : (text.font != null ? text.font.name : "(none)");
            float spacing = tmp != null ? tmp.characterSpacing : 0f;
            bool bold = tmp != null ? (tmp.fontStyle & (FontStyles.Bold | FontStyles.Italic)) != 0
                                    : text.fontStyle != FontStyle.Normal;

            var problems = new List<string>();
            if (Mathf.Abs(heightPx - UIButton.ScreenPx(ScreenH)) > 1.5f) problems.Add($"height {heightPx:0}");
            if (!font.StartsWith("Silkscreen")) problems.Add("face");
            if (Mathf.Abs(fontPx - UIButton.CaptionScreenPx(ScreenH)) > 0.5f) problems.Add($"size {fontPx:0}");
            if (Mathf.Abs(spacing) > 0.01f) problems.Add("tracking");
            if (bold) problems.Add("bold/italic");
            if (tmp != null && tmp.enableAutoSizing) problems.Add("auto-size");

            into.Add(new Finding
            {
                asset = asset,
                path = PathOf(button.transform, root.transform),
                caption = (tmp != null ? tmp.text : text.text)?.Replace("\n", " ").Replace("|", "/"),
                font = font,
                heightPx = heightPx,
                fontPx = fontPx,
                spacing = spacing,
                problems = string.Join(", ", problems),
                off = problems.Count > 0,
            });
        }
    }

    static string PathOf(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (var p = t; p != null; p = p.parent)
        {
            parts.Add(p.name);
            if (p == root) break;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    static void Write(List<Finding> findings, int prefabs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Button audit");
        sb.AppendLine();
        sb.AppendLine("Generated by `Draftmaster > UI > Audit Buttons` (`Assets/Editor/ButtonAudit.cs`). Every captioned uGUI");
        sb.AppendLine($"button in the {prefabs} prefabs the build reaches and the demo scenes, measured on a 1920x1080 screen. Standard: " +
                      $"{UIButton.ScreenPx(ScreenH):0}px tall, Silkscreen at {UIButton.CaptionScreenPx(ScreenH):0}px, no tracking, regular weight " +
                      "(`UIButton`, matching the pause menu). IMGUI buttons are drawn in code and not listed.");
        sb.AppendLine();
        sb.AppendLine("| Asset | Button | Caption | Height | Face | Size | Off standard |");
        sb.AppendLine("|---|---|---|---:|---|---:|---|");
        foreach (var f in findings.OrderByDescending(f => f.off).ThenBy(f => f.asset).ThenBy(f => f.path))
            sb.AppendLine($"| {f.asset} | {f.path} | {f.caption} | {f.heightPx:0} | {f.font} | {f.fontPx:0} | {(f.off ? f.problems : "—")} |");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
