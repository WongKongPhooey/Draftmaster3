#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

// Builds a throwaway world-space copy of the dialogue bubble in the open scene so the font can be
// looked at in the Scene View (and screenshotted) without entering play mode.
//
// SpeechBubble only ever appears mid-conversation, which makes "the text looks wrong" expensive to
// iterate on. This reproduces the same construction -- themed window sprite, TMP label with the theme's
// bitmap font, measured transform scale -- as a static object that can be inspected directly.
public static class PixelFontPreview
{
    const string kName = "__PixelFontPreview";

    [MenuItem("Draftmaster/Art/Preview Dialogue Bubble", priority = 123)]
    public static void Build()
    {
        Clear();

        var theme = PixelUITheme.Instance;
        if (theme == null)
        {
            Debug.LogWarning("[PixelFontPreview] theme not loaded.");
            return;
        }
        theme.EnsureCrispAtlas();

        var root = new GameObject(kName);
        root.hideFlags = HideFlags.DontSave;
        root.transform.position = new Vector3(0f, 0f, -5f);

        const float textSize = 0.22f;
        const string sample = "Morning! Car's prepped and\nfuelled, ready when you are.";

        var labelGo = new GameObject("Text");
        labelGo.transform.SetParent(root.transform, false);
        var label = labelGo.AddComponent<TextMeshPro>();
        label.font = theme.body;
        if (theme.body != null && theme.body.material != null)
            label.fontSharedMaterial = theme.body.material;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.enableWordWrapping = false;
        label.color = theme.text;

        // Same measured sizing SpeechBubble uses: native point size, then scale to metres.
        float pointSize = theme.body != null && theme.body.faceInfo.pointSize > 0
            ? theme.body.faceInfo.pointSize : 16f;
        label.fontSize = pointSize;
        label.text = "Ag";
        label.ForceMeshUpdate();
        float measured = label.GetPreferredValues().y;
        float scale = measured > 0.0001f ? textSize / measured : 1f;
        label.transform.localScale = new Vector3(scale, scale, 1f);

        label.text = sample;
        Vector2 local = label.GetPreferredValues();
        label.rectTransform.sizeDelta = local;
        label.ForceMeshUpdate();
        Vector2 b = local * scale;

        var padding = new Vector2(0.18f, 0.12f);
        var boxSize = new Vector2(b.x + padding.x * 2f, b.y + padding.y * 2f);

        var bg = new GameObject("BG").AddComponent<SpriteRenderer>();
        bg.transform.SetParent(root.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        bg.sprite = theme.window;
        if (bg.sprite != null && bg.sprite.border.sqrMagnitude > 0f)
        {
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = boxSize;
        }
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                  ?? Shader.Find("Sprites/Default");
        if (shader != null) bg.sharedMaterial = new Material(shader);
        bg.sortingOrder = 60;

        var mr = labelGo.GetComponent<MeshRenderer>();
        mr.sortingOrder = 61;

        string shaderName = theme.body != null && theme.body.material != null
            ? theme.body.material.shader.name : "none";
        string filter = theme.body != null && theme.body.atlasTexture != null
            ? theme.body.atlasTexture.filterMode.ToString() : "?";
        Debug.Log($"[PixelFontPreview] built at {root.transform.position}, box {boxSize}, " +
                  $"text scale {scale:0.####}, shader {shaderName}, atlas filter {filter}");

        // A dedicated orthographic camera framed on the bubble, so a screenshot always shows the text at a
        // known magnification instead of wherever the Scene View happened to be looking.
        var camGo = new GameObject("PreviewCamera");
        camGo.hideFlags = HideFlags.DontSave;
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        // Frame the whole plate, not just its height — a 16:9 viewport crops a wide dialogue box otherwise.
        const float previewAspect = 16f / 9f;
        cam.orthographicSize = Mathf.Max(boxSize.y, boxSize.x / previewAspect) * 0.75f;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        cam.depth = 100f;   // draw over the scene's own camera

        Selection.activeGameObject = null;   // no selection outline over the art
    }

    [MenuItem("Draftmaster/Art/Clear Dialogue Bubble Preview", priority = 124)]
    public static void Clear()
    {
        var existing = GameObject.Find(kName);
        while (existing != null)
        {
            Object.DestroyImmediate(existing);
            existing = GameObject.Find(kName);
        }
    }
}
#endif
