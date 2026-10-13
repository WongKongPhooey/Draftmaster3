using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Draftmaster.Controls;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Finds text drawn smaller than the floor of the shared type scale (Draftmaster.Controls.UIType: Label,
// 8 kit px = 24px on a 1080p screen). Every uGUI Text and TextMeshProUGUI in every prefab under Assets,
// plus whatever scenes are open, is measured as it would come out on a 1920x1080 screen — through its
// root canvas's CanvasScaler and any scale between the text and that canvas — and anything under the
// floor goes into Docs/TextSizeAudit.md, worst first.
//
// It reads, it does not fix: a size is only half of a fix, and the other half (does the bigger text
// still fit its box?) wants a person. World-space text (speech bubbles, signs) is sized in metres off a
// camera and is skipped, as are IMGUI panels, which are drawn in code through PixelGUI and already sit
// on the scale. Text inside something scaled at runtime (the satnav's map labels, which zoom) is
// measured at its authored size.
public static class TextSizeAudit
{
    const string ReportPath = "Docs/TextSizeAudit.md";
    const float ScreenW = 1920f, ScreenH = 1080f;

    // Imported packages' demo UI: not ours to resize.
    static readonly string[] ThirdParty = { "Assets/Ink/", "Assets/PlayFab", "Assets/TextMesh Pro/" };

    public struct Finding
    {
        public string asset;     // prefab path or scene name
        public string path;      // hierarchy path inside it
        public string font;
        public float size;       // as authored
        public float screenPx;   // on a 1920x1080 screen
        public bool active;
        public string note;
    }

    [MenuItem("Draftmaster/UI/Audit Text Sizes", false, 70)]
    public static void Run()
    {
        var findings = new List<Finding>();
        int prefabs = 0, texts = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (ThirdParty.Any(p => path.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))) continue;
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            prefabs++;
            texts += Collect(root, path, findings);
        }

        var scenes = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
            scenes.Add(scene.path);
            foreach (var go in scene.GetRootGameObjects())
                texts += Collect(go, scene.path, findings);
        }

        Write(findings, prefabs, texts, scenes);
        Debug.Log($"[TextSizeAudit] {findings.Count} text(s) under the {UIType.MinimumScreenPx(ScreenH):0}px floor " +
                  $"across {prefabs} prefabs and {scenes.Count} open scene(s) — {ReportPath}");
    }

    // Every under-floor text below `root`. Returns how many texts were measured.
    public static int Collect(GameObject root, string asset, List<Finding> into)
    {
        int measured = 0;
        float floor = UIType.MinimumScreenPx(ScreenH);

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            float size = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
            if (!Measure(text.transform, size, out float px, out string note)) continue;
            measured++;
            if (px + 0.001f < floor)
                into.Add(Make(asset, text.transform, root.transform, text.font != null ? text.font.name : "(none)", size, px, note));
        }
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            float size = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
            if (!Measure(text.transform, size, out float px, out string note)) continue;
            measured++;
            if (px + 0.001f < floor)
                into.Add(Make(asset, text.transform, root.transform, text.font != null ? text.font.name : "(none)", size, px, note));
        }
        return measured;
    }

    // How tall `size` comes out on a 1920x1080 screen. False for text this audit cannot judge
    // (world-space canvases).
    public static bool Measure(Transform t, float size, out float screenPx, out string note)
    {
        note = null;
        screenPx = size;

        Canvas rootCanvas = null;
        for (var p = t; p != null; p = p.parent)
        {
            var c = p.GetComponent<Canvas>();
            if (c != null) rootCanvas = c;
        }

        float canvasScale = 1f;
        Transform canvasT = null;
        if (rootCanvas == null)
        {
            // A piece of UI built to be dropped into somebody else's canvas — nearly always one of the
            // 1920x1080 ones.
            note = "no canvas: read as 1920x1080";
            canvasScale = ScreenH / 1080f;
        }
        else
        {
            if (rootCanvas.renderMode == RenderMode.WorldSpace) return false;
            canvasT = rootCanvas.transform;
            var scaler = rootCanvas.GetComponent<CanvasScaler>();
            canvasScale = ScaleFactor(scaler);
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                note = $"canvas {scaler.referenceResolution.x:0}x{scaler.referenceResolution.y:0}";
            else if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                note = $"constant pixel size x{scaler.scaleFactor:0.##}";
            else note = "canvas, no scaler";
        }

        // Anything scaled between the text and its canvas (a scaled panel, a zoomed map).
        float below = Mathf.Abs(t.lossyScale.y);
        if (canvasT != null && Mathf.Abs(canvasT.lossyScale.y) > 1e-6f) below /= Mathf.Abs(canvasT.lossyScale.y);
        if (below <= 1e-6f) below = 1f;

        screenPx = size * canvasScale * below;
        return true;
    }

    // CanvasScaler's own arithmetic, for a 1920x1080 screen.
    public static float ScaleFactor(CanvasScaler scaler)
    {
        if (scaler == null) return 1f;
        switch (scaler.uiScaleMode)
        {
            case CanvasScaler.ScaleMode.ConstantPixelSize:
                return scaler.scaleFactor;
            case CanvasScaler.ScaleMode.ScaleWithScreenSize:
            {
                var r = scaler.referenceResolution;
                if (r.x <= 0f || r.y <= 0f) return 1f;
                float w = ScreenW / r.x, h = ScreenH / r.y;
                switch (scaler.screenMatchMode)
                {
                    case CanvasScaler.ScreenMatchMode.Expand: return Mathf.Min(w, h);
                    case CanvasScaler.ScreenMatchMode.Shrink: return Mathf.Max(w, h);
                    default:
                        float log = Mathf.Lerp(Mathf.Log(w, 2f), Mathf.Log(h, 2f), scaler.matchWidthOrHeight);
                        return Mathf.Pow(2f, log);
                }
            }
            default:
                return 1f;
        }
    }

    static Finding Make(string asset, Transform t, Transform root, string font, float size, float px, string note) =>
        new Finding
        {
            asset = asset,
            path = PathOf(t, root),
            font = font,
            size = size,
            screenPx = px,
            active = t.gameObject.activeInHierarchy || IsActiveInPrefab(t, root),
            note = note,
        };

    static bool IsActiveInPrefab(Transform t, Transform root)
    {
        for (var p = t; p != null; p = p.parent)
        {
            if (!p.gameObject.activeSelf) return false;
            if (p == root) break;
        }
        return true;
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

    static void Write(List<Finding> findings, int prefabs, int texts, List<string> scenes)
    {
        float floor = UIType.MinimumScreenPx(ScreenH);
        var sb = new StringBuilder();
        sb.AppendLine("# Text size audit");
        sb.AppendLine();
        sb.AppendLine("Generated by `Draftmaster > UI > Audit Text Sizes` (`Assets/Editor/TextSizeAudit.cs`). Lists every uGUI");
        sb.AppendLine($"`Text` / `TextMeshProUGUI` that comes out under the type scale's floor — **{floor:0}px on a 1920x1080 screen**");
        sb.AppendLine("(Label, 8 kit px; see `Docs/Type-Scale.md`). World-space text and IMGUI panels are not measured.");
        sb.AppendLine("Inactive rows are often templates or legacy UI; they still count once something turns them on.");
        sb.AppendLine();
        sb.AppendLine($"{texts} texts measured in {prefabs} prefabs" +
                      (scenes.Count > 0 ? $" and the open scene(s) {string.Join(", ", scenes.Select(s => "`" + s + "`"))}" : "") +
                      $"; **{findings.Count} under the floor** in {findings.Select(f => f.asset).Distinct().Count()} assets.");
        sb.AppendLine();

        foreach (var group in findings.GroupBy(f => f.asset).OrderBy(g => g.Min(f => f.screenPx)).ThenBy(g => g.Key))
        {
            sb.AppendLine($"## `{group.Key}`");
            sb.AppendLine();
            sb.AppendLine("| on screen | authored | font | object | |");
            sb.AppendLine("|---:|---:|---|---|---|");
            foreach (var f in group.OrderBy(f => f.screenPx).ThenBy(f => f.path))
                sb.AppendLine($"| {f.screenPx:0.#}px | {f.size:0.#} | {f.font} | `{f.path}` | {(f.active ? "" : "inactive; ")}{f.note} |");
            sb.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
