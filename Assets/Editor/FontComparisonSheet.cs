using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds a throwaway scene showing the same three samples in each candidate face, so the choice of a
// replacement for VT323 is made by looking rather than by argument. Nothing is saved: the scene is
// created in memory, screenshotted, and discarded.
public static class FontComparisonSheet
{
    const string FontDir = "Assets/Resources/Fonts";
    static readonly (string file, string label)[] Faces =
    {
        ("VT323 Pixel", "VT323 (CURRENT)"),
        ("_VT323 Rebuild", "VT323 (REBUILT)"),
        ("Silkscreen Pixel", "SILKSCREEN"),
        ("Fixedsys Pixel", "FIXEDSYS"),
    };

    [MenuItem("Draftmaster/Art/Font Comparison Sheet")]
    public static void Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Camera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.055f, 0.063f, 0.078f);

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        // Screen-space-camera, not overlay: a camera-rendered screenshot (which is what the capture path
        // gives us) composites overlay canvases out entirely, so an overlay sheet photographs as empty black.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 10f;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 540);
        scaler.matchWidthOrHeight = 0.5f;

        float columnWidth = 1280f / Faces.Length;
        for (int i = 0; i < Faces.Length; i++)
        {
            var (file, label) = Faces[i];
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{file}.asset");
            if (font == null) { Debug.LogWarning($"FontComparisonSheet: skipping absent {FontDir}/{file}.asset"); continue; }

            float x = -640f + columnWidth * (i + 0.5f);
            Add(canvas, font, label,                       16, x, 210, columnWidth, new Color(0.85f, 0.68f, 0.21f));
            Add(canvas, font, "DRAFTMASTER 3",             48, x, 140, columnWidth, Color.white);
            Add(canvas, font, "LAP 118/200",               16, x,  60, columnWidth, Color.white);
            Add(canvas, font, "4  #11  HAMLIN   +8.1s",    16, x,  10, columnWidth, Color.white);
            Add(canvas, font, "5  #03  A.DILLON +9.0s",    16, x, -20, columnWidth, Color.white);
            Add(canvas, font, "12  #48  BOWMAN   +11.4s",  16, x, -50, columnWidth, Color.white);
            Add(canvas, font, "Twelve laps of clean air.", 16, x, -120, columnWidth, new Color(0.78f, 0.80f, 0.84f));
            Add(canvas, font, "CONTINUE",                  16, x, -190, columnWidth, Color.white);
        }

        Debug.Log("FontComparisonSheet: built (unsaved scene).");
    }

    static void Add(Canvas canvas, TMP_FontAsset font, string text, float size, float x, float y, float width, Color colour)
    {
        var go = new GameObject(text.Substring(0, Mathf.Min(text.Length, 12)), typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width - 24f, size * 2.2f);
        rect.anchoredPosition = new Vector2(x, y);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.text = text;
        tmp.color = colour;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
    }
}
