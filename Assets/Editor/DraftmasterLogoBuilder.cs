#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The game's title logo (design handoff option 4a, Assets/design_handoff_draftmaster3_logo/README.md), built as
// live uGUI rather than one flat image: the 3 and the two bolt halves are pixel-art sprites, DRAFT / MASTER and
// A PHOOEY GAME are TextMeshPro.
//
//   Draftmaster > Art > Build Draftmaster Logo Prefab     → Assets/UI/Logo/DraftmasterLogo.prefab
//   Draftmaster > Art > Put Draftmaster Logo On Title Screen
//   Draftmaster > Art > Capture Title Screen (1280x720)    → Temp/title_capture.png, for checking by eye
//   Draftmaster > Art > Capture Title Screen (Pixel 5 landscape) → Temp/title_capture_phone.png
//   Draftmaster > Art > Export Draftmaster Logo PNGs       → App Store/Steam/Logo/, transparent, 1x 2x 4x
//
// Laid out at 1x in design pixels, top-left origin, from the handoff's reference render. The words are split
// DRAF | T  /  M | ASTER so the T sits over the M. Each text piece is placed by its first glyph's INK, not by
// its rect: TMP puts the rect's top at the line's ascender, which is nowhere near the top of a capital, so
// placing rects would leave every word a few pixels off where the design has it.
//
// The prefab is generated — hand edits to it are lost on a rebuild. The title scene is not rebuilt; the
// "Put ... On Title Screen" item edits it in place.
public static class DraftmasterLogoBuilder
{
    const string Handoff = "Assets/design_handoff_draftmaster3_logo/sprites/";
    const string Dir = "Assets/UI/Logo";
    const string PrefabPath = Dir + "/DraftmasterLogo.prefab";
    const string WordFontPath = Dir + "/Pixelify Sans Bold 54.asset";
    const string TagFontPath = Dir + "/Silkscreen 8.asset";
    const string TitleScene = "Assets/Scenes/TitleScreen.unity";

    const int WordSize = 54;
    const int TagSize = 8;

    static readonly Color Cream = Hex(0xF4EAD7);
    static readonly Color Ink = Hex(0x05060A);
    static readonly Color Gold = Hex(0xE8B13C);

    // The canvas the logo is drawn on: the reference PNG at 1x, 12 px of padding round the art so shadows
    // are never clipped.
    static readonly Vector2 Size = new Vector2(540f, 187f);

    // Where the ink lands, measured off reference/logo_4a_reference@2x.png and halved. x = left edge of the
    // first glyph, y = top of the capitals, both down from the canvas's top-left.
    static readonly Vector2 DrafInk = new Vector2(15.5f, 38.5f);
    static readonly Vector2 TInk = new Vector2(149.5f, 38.5f);
    static readonly Vector2 MInk = new Vector2(149.5f, 91.5f);
    static readonly Vector2 AsterInk = new Vector2(193f, 91.5f);
    static readonly Vector2 TagInk = new Vector2(185.5f, 145f);
    static readonly Vector2 BoltTopAt = new Vector2(193f, 55f);
    static readonly Vector2 BoltBottomAt = new Vector2(94f, 96f);
    static readonly Rect RuleRect = new Rect(146f, 145f, 30f, 3f);
    static readonly Vector2 ThreeAt = new Vector2(372f, 12f);
    static readonly Vector2 Shadow = new Vector2(4f, 4f);     // right and down

    // Where it goes on the title screen, inside Column: the D's ink on the column's 26 px margin, the
    // capitals just under the eyebrow, and the 3's right edge a little short of the middle of a landscape
    // phone (a Pixel 5 is 780 canvas units wide; the 3 ends at ~362).
    //
    // Two thirds, not whatever lines the 3 up exactly: the canvas is height-matched to 360, so on a 1080-tall
    // screen it is drawn at 3x and the logo at 2x — every art pixel two screen pixels, still hard-edged. An
    // exact fit (~0.72) would draw the pixel art at 2.17 screen pixels per art pixel, i.e. unevenly.
    const float OnTitleScale = 2f / 3f;
    static readonly Vector2 OnTitleAt = new Vector2(16f, -40f);
    const float EyebrowY = -40f;

    [MenuItem("Draftmaster/Art/Build Draftmaster Logo Prefab", priority = 127)]
    public static void BuildMenu() => Debug.Log(BuildPrefab());

    public static string BuildPrefab()
    {
        Directory.CreateDirectory(Dir);
        var three = ImportSprite("digit3_7x.png");
        var boltTop = ImportSprite("bolt_top_3x.png");
        var boltBottom = ImportSprite("bolt_bottom_3x.png");
        if (three == null || boltTop == null || boltBottom == null)
            return $"Logo sprites missing from {Handoff}.";

        var wordFont = BakedFont("Assets/Fonts/PixelifySans-Bold.ttf", WordFontPath, "Pixelify Sans Bold 54", WordSize);
        var tagFont = BakedFont("Assets/Fonts/Silkscreen-Regular.ttf", TagFontPath, "Silkscreen 8", TagSize);
        if (wordFont == null || tagFont == null) return "Logo fonts could not be built — see the console.";

        var root = new GameObject("DraftmasterLogo", typeof(RectTransform));
        try
        {
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = Size;

            var word = Group(rt, "Wordmark");
            var shadows = Group(word, "Shadow");

            // Shadows first, so every face draws over every shadow.
            foreach (var (name, text, ink) in new[] { ("DRAF", "DRAF", DrafInk), ("T", "T", TInk),
                                                       ("M", "M", MInk), ("ASTER", "ASTER", AsterInk) })
            {
                Text(shadows, name, text, wordFont, WordSize, 1.85f, Ink, ink + Shadow);
            }
            Text(word, "DRAF", "DRAF", wordFont, WordSize, 1.85f, Cream, DrafInk);
            Text(word, "T", "T", wordFont, WordSize, 1.85f, Cream, TInk);
            Text(word, "M", "M", wordFont, WordSize, 1.85f, Cream, MInk);
            Text(word, "ASTER", "ASTER", wordFont, WordSize, 1.85f, Cream, AsterInk);

            Picture(word, "BoltTop", boltTop, BoltTopAt);
            Picture(word, "BoltBottom", boltBottom, BoltBottomAt);

            var tag = Group(word, "Tagline");
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(tag, false);
            var ruleImg = rule.GetComponent<Image>();
            ruleImg.color = Gold;
            ruleImg.raycastTarget = false;
            Place((RectTransform)rule.transform, RuleRect.position, RuleRect.size);
            // 3 px of tracking at 8 px is 37.5 em-hundredths.
            Text(tag, "Text", "A PHOOEY GAME", tagFont, TagSize, 37.5f, Gold, TagInk);

            Picture(rt, "Three", three, ThreeAt);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        return $"Built {PrefabPath}.";
    }

    [MenuItem("Draftmaster/Art/Put Draftmaster Logo On Title Screen", priority = 128)]
    public static void PlaceMenu() => Debug.Log(PlaceOnTitle());

    // Replaces the one-line wordmark (and its shadow copy) in the open TitleScreen with the logo prefab.
    // Edits the scene in place; nothing else in it is touched.
    public static string PlaceOnTitle()
    {
        // A scene edited in Play Mode is thrown away when it stops, and cannot be saved while it runs.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play Mode first — the title scene can't be edited while it plays.";

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != TitleScene) return $"Open {TitleScene} first.";

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return $"{PrefabPath} not built yet — run Build Draftmaster Logo Prefab.";

        Transform column = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            column = go.transform.Find("Column");
            if (column != null) break;
        }
        if (column == null) return "TitleScreen has no TitleCanvas/Column.";

        foreach (var old in new[] { "Wordmark", "WordmarkShadow", "DraftmasterLogo" })
        {
            var t = column.Find(old);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        var logo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, column);
        Undo.RegisterCreatedObjectUndo(logo, "Place Draftmaster logo");
        var rt = (RectTransform)logo.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = OnTitleAt;
        rt.localScale = new Vector3(OnTitleScale, OnTitleScale, 1f);
        // Just after the eyebrow, where the wordmark was, so the menu and footer still draw over it if they meet.
        var eyebrow = column.Find("Eyebrow");
        logo.transform.SetSiblingIndex(eyebrow != null ? eyebrow.GetSiblingIndex() + 1 : 0);

        // The eyebrow sits over DRAFT. At full size the logo needed it moved up to -18; at two thirds it
        // fits back in its original place.
        if (eyebrow != null)
        {
            var ert = (RectTransform)eyebrow;
            Undo.RecordObject(ert, "Place Draftmaster logo");
            ert.anchoredPosition = new Vector2(ert.anchoredPosition.x, EyebrowY);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Logo placed on the title screen and the scene saved.";
    }

    [MenuItem("Draftmaster/Art/Capture Title Screen (1280x720)", priority = 129)]
    public static void CaptureMenu() => Debug.Log(CaptureTitle(1280, 720, "Temp/title_capture.png"));

    // The Device Simulator's phone, landscape: wider than 16:9, so it shows where the logo sits against the
    // middle of the screen on the shape most players will see it.
    [MenuItem("Draftmaster/Art/Capture Title Screen (Pixel 5 landscape)", priority = 130)]
    public static void CapturePhoneMenu() => Debug.Log(CaptureTitle(2340, 1080, "Temp/title_capture_phone.png"));

    // Renders the open scene's TitleCamera, UI included, at a fixed 16:9 size rather than whatever shape the
    // Game view happens to be.
    public static string CaptureTitle(int w, int h, string outPath)
    {
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null) return "No camera in the open scene.";

        var rtex = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rtex;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rtex;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(rtex);
            Canvas.ForceUpdateCanvases();
        }
        return $"Captured {Path.GetFullPath(outPath)}";
    }

    const string ExportDir = "App Store/Steam/Logo";

    [MenuItem("Draftmaster/Art/Export Draftmaster Logo PNGs", priority = 131)]
    public static void ExportMenu() => Debug.Log(ExportLogoPngs(ExportDir, 1, 2, 4));

    // The logo on its own for store pages and press kits: transparent, trimmed to its ink (shadows included),
    // at whole-number scales of the design's 1x so every art pixel stays square.
    //
    // From the open scene's DraftmasterLogo when there is one, overrides and all (the title's redesigned 3 is a
    // scene override, not in the prefab): drawn by its own canvas and camera, the way the title capture draws it,
    // with everything else in the scene switched off for the render and back on after, so the PNG is exactly what
    // the title draws. The prefab, in a preview scene, only when no scene has the logo.
    //
    // Alpha comes from rendering twice, over black and over white: how much of the background shows through a
    // pixel is how transparent it is. That holds whatever the render pipeline does with the camera's own alpha.
    public static string ExportLogoPngs(string dir, params int[] scales)
    {
        if (EditorApplication.isPlaying) return "Leave Play Mode first: the logo's intro has its pieces mid-slide.";
        GameObject source = null;
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rt.name == "DraftmasterLogo") { source = rt.gameObject; break; }
        Directory.CreateDirectory(dir);

        var written = new List<string>();
        string from;
        if (source != null)
        {
            string error = ExportFromScene(source, dir, scales, written);
            if (error != null) return error;
            from = $"{source.scene.path}'s DraftmasterLogo";
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return $"No DraftmasterLogo in the open scenes and no prefab at {PrefabPath}.";
            ExportFromPrefab(prefab, dir, scales, written);
            from = PrefabPath;
        }
        return $"Exported from {from}:\n" + string.Join("\n", written);
    }

    // The scene's logo through its own camera. The canvas matches height to the 360 px reference, so a render
    // 360 x N tall draws the logo, at scale 1, N pixels to the design pixel.
    static string ExportFromScene(GameObject logo, string dir, int[] scales, List<string> written)
    {
        var canvas = logo.GetComponentInParent<Canvas>(true);
        var cam = canvas != null ? canvas.rootCanvas.worldCamera : null;
        if (cam == null) return "The scene's DraftmasterLogo isn't on a camera canvas.";

        var root = canvas.rootCanvas.transform;
        var logoRt = (RectTransform)logo.transform;
        var cap = logo.GetComponent<MaxScreenWidth>();
        bool wasActive = logo.activeSelf, capWasOn = cap != null && cap.enabled;
        Vector3 scaleWas = logoRt.localScale;
        var clearWas = cam.clearFlags;
        var backgroundWas = cam.backgroundColor;
        var targetWas = cam.targetTexture;
        int maskWas = cam.cullingMask, layerWas = root.gameObject.layer;
        // The camera sees only a spare layer with the canvas on it: the track backdrop rebuilds itself for the
        // render's new aspect, so switching its renderers off isn't enough.
        int spare = 31;
        while (spare > 8 && !string.IsNullOrEmpty(LayerMask.LayerToName(spare))) spare--;

        // Everything drawn that isn't the logo: world renderers, other canvases, and this canvas's other graphics.
        var hidden = new List<Behaviour>();
        var hiddenRenderers = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
        foreach (var g in root.GetComponentsInChildren<Graphic>())
            if (g.enabled && !g.transform.IsChildOf(logo.transform)) { g.enabled = false; hidden.Add(g); }
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.enabled && c.rootCanvas.transform != root) { c.enabled = false; hidden.Add(c); }

        try
        {
            logo.SetActive(true);
            if (cap != null) cap.enabled = false;
            logoRt.localScale = Vector3.one;
            cam.clearFlags = CameraClearFlags.SolidColor;
            root.gameObject.layer = spare;
            cam.cullingMask = 1 << spare;

            foreach (int scale in scales)
            {
                int h = 360 * scale, w = 640 * scale;
                var black = RenderLogo(cam, w, h, Color.black, targetWas);
                var white = RenderLogo(cam, w, h, Color.white, targetWas);
                WriteTrimmed(Matte(black, white), w, h, dir, scale, written);
            }
        }
        finally
        {
            logoRt.localScale = scaleWas;
            if (cap != null) cap.enabled = capWasOn;
            logo.SetActive(wasActive);
            cam.clearFlags = clearWas;
            cam.backgroundColor = backgroundWas;
            cam.cullingMask = maskWas;
            root.gameObject.layer = layerWas;
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var b in hidden) if (b != null) b.enabled = true;
            Canvas.ForceUpdateCanvases();
        }
        return null;
    }

    // The prefab on a world-space canvas in a preview scene. The 3 and its shadow hang past the prefab's rect, so
    // the render has a margin round it; the trim takes it off.
    static void ExportFromPrefab(GameObject prefab, string dir, int[] scales, List<string> written)
    {
        const float unitsPerPx = 0.01f;
        const int pad = 64;
        var view = new Vector2Int(Mathf.RoundToInt(Size.x) + pad * 2, Mathf.RoundToInt(Size.y) + pad * 2);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var camGo = new GameObject("LogoExportCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.GetComponent<Camera>();
            cam.scene = scene;
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = view.y * unitsPerPx * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var canvasGo = new GameObject("LogoExportCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.sizeDelta = Size;
            canvasRt.localScale = Vector3.one * unitsPerPx;
            canvasRt.position = Vector3.zero;

            var logo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            logo.transform.SetParent(canvasGo.transform, false);
            if (logo.TryGetComponent(out MaxScreenWidth cap)) cap.enabled = false;
            var logoRt = (RectTransform)logo.transform;
            logoRt.anchorMin = logoRt.anchorMax = logoRt.pivot = new Vector2(0.5f, 0.5f);
            logoRt.anchoredPosition = Vector2.zero;
            logoRt.localScale = Vector3.one;

            foreach (int scale in scales)
            {
                int w = view.x * scale, h = view.y * scale;
                var black = RenderLogo(cam, w, h, Color.black, null);
                var white = RenderLogo(cam, w, h, Color.white, null);
                WriteTrimmed(Matte(black, white), w, h, dir, scale, written);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void WriteTrimmed(Color32[] cut, int w, int h, string dir, int scale, List<string> written)
    {
        var trimmed = TrimToInk(cut, w, h, out int tw, out int th);
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.SetPixels32(trimmed);
        tex.Apply();
        string path = Path.Combine(dir, $"DraftmasterLogo@{scale}x.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        written.Add($"{Path.GetFullPath(path)} ({tw}x{th})");
    }

    // Rendered twice, like CaptureTitle: a camera canvas only takes the target's size on the first pass.
    static Color32[] RenderLogo(Camera cam, int w, int h, Color background, RenderTexture restoreTarget)
    {
        var rtex = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevActive = RenderTexture.active;
        try
        {
            cam.backgroundColor = background;
            cam.targetTexture = rtex;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rtex;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            var pixels = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return pixels;
        }
        finally
        {
            cam.targetTexture = restoreTarget;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(rtex);
        }
    }

    // Over black a pixel reads c·a; over white, c·a + (1 − a). The difference is 1 − a, and c is black's over a.
    static Color32[] Matte(Color32[] black, Color32[] white)
    {
        var cut = new Color32[black.Length];
        for (int i = 0; i < black.Length; i++)
        {
            var b = black[i];
            var wh = white[i];
            int show = ((wh.r - b.r) + (wh.g - b.g) + (wh.b - b.b)) / 3;
            int a = Mathf.Clamp(255 - show, 0, 255);
            if (a == 0) { cut[i] = new Color32(0, 0, 0, 0); continue; }
            cut[i] = new Color32((byte)Mathf.Min(255, b.r * 255 / a), (byte)Mathf.Min(255, b.g * 255 / a),
                                 (byte)Mathf.Min(255, b.b * 255 / a), (byte)a);
        }
        return cut;
    }

    // Crops to the smallest box holding every pixel that isn't fully transparent.
    static Color32[] TrimToInk(Color32[] px, int w, int h, out int tw, out int th)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < 0) { tw = w; th = h; return px; }

        tw = maxX - minX + 1;
        th = maxY - minY + 1;
        var outPx = new Color32[tw * th];
        for (int y = 0; y < th; y++)
            System.Array.Copy(px, (y + minY) * w + minX, outPx, y * tw, tw);
        return outPx;
    }

    // ------------------------------------------------------------------ pieces

    static RectTransform Group(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void Place(RectTransform rt, Vector2 topLeft, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
    }

    static void Picture(Transform parent, string name, Sprite sprite, Vector2 topLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Place((RectTransform)go.transform, topLeft, sprite.rect.size);   // native size
    }

    // A text piece whose first glyph's ink top-left lands on `ink` (whole pixels).
    static void Text(Transform parent, string name, string text, TMP_FontAsset font, int size, float spacing,
                     Color color, Vector2 ink)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSharedMaterial = font.material;
        t.fontSize = size;
        t.enableAutoSizing = false;
        t.characterSpacing = spacing;
        t.color = color;
        t.text = text;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.richText = false;

        var rt = (RectTransform)go.transform;
        Place(rt, Vector2.zero, new Vector2(t.GetPreferredValues(text).x + 4f, size * 1.2f));

        // Where the first glyph's ink sits relative to the rect's top-left, from the font's own metrics: a
        // top-left aligned first line puts its baseline one ascender below the rect's top. Worked out rather
        // than read back from textInfo, which is empty for a text that is not under a canvas yet.
        if (!font.characterLookupTable.TryGetValue(text[0], out var ch))
        {
            Debug.LogWarning($"[DraftmasterLogoBuilder] '{text[0]}' is not in {font.name}.");
            return;
        }
        var m = ch.glyph.metrics;
        float scale = size / font.faceInfo.pointSize * font.faceInfo.scale;
        float left = m.horizontalBearingX * scale;
        float top = -font.faceInfo.ascentLine * scale + m.horizontalBearingY * scale;
        rt.anchoredPosition = new Vector2(Mathf.Round(ink.x - left), Mathf.Round(-ink.y - top));
    }

    // ------------------------------------------------------------------ assets

    // Copies a handoff sprite into Assets/UI/Logo (so the handoff folder can go) and imports it as pixel art.
    static Sprite ImportSprite(string file)
    {
        string src = Handoff + file, dst = Dir + "/" + file;
        if (!File.Exists(dst))
        {
            if (!File.Exists(src)) return null;
            AssetDatabase.CopyAsset(src, dst);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.spritePixelsPerUnit = 100f;
        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(dst);
    }

    // A hinted-raster bitmap font at one exact size (PixelUIKitSetup's recipe), with the glyphs the logo needs
    // baked in and the atlas frozen — a dynamic atlas is how the kit's faces once lost their glyphs.
    static TMP_FontAsset BakedFont(string ttf, string outPath, string niceName, int size)
    {
        var font = PixelUIKitSetup.BuildBitmapFont(ttf, outPath, niceName, size);
        if (font == null) return null;
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ");
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        if (font.atlasTexture != null) font.atlasTexture.filterMode = FilterMode.Point;
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
}
#endif
