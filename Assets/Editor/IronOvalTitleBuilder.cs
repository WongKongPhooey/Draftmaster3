#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/Scenes/TitleScreen.unity from the Iron Oval design file's TITLE screen.
//
//   Draftmaster > Art > Build Title Screen Scene
//
// What the sheet specifies, and what it becomes here:
//   * 844x390 landscape mock → the project's 640x360 reference canvas, laid out to the same proportions
//     (menu column just over half the width, art filling the rest).
//   * Wordmark 62px Pixelify Sans with a 4px hard shadow → 60px (the top of Pixelify's 20px ladder;
//     62 is off-grid and renders soft) plus a duplicate label offset 4,-4 in ink. TMP's own underlay is
//     a blur, so the shadow is a second label.
//   * Eyebrow / season line / footer in Silkscreen 8, the ladder size the face is drawn for.
//   * Menu rows: gold cursor column, Pixelify labels, selected cream and the rest dim.
//   * The scrim under the column is the sheet's linear-gradient(90deg,#0d0f16 62%,transparent), baked
//     to a 64x1 PNG so it stays one draw and one asset rather than a shader.
//
// Re-running rebuilds the scene from scratch: it is generated, not hand-authored. Anything you want to
// keep, keep in a prefab the scene references.
public static class IronOvalTitleBuilder
{
    const string kScenePath = "Assets/Scenes/TitleScreen.unity";
    const string kScrimPath = "Assets/UI/IronOval/title-scrim.png";

    // THIS BUILDER REGENERATES THE WHOLE SCENE. Everything hand-authored in TitleScreen.unity since it
    // last ran is destroyed — that is not a theoretical risk, it has happened: a rebuild to add one menu
    // row reverted the wordmark from DRAFTMASTER 3 to the IRON OVAL placeholder below, dropped a prefab
    // instance and a material, and moved the layout. The scene is the source of truth for anything
    // authored by hand; this file only knows what its own code says.
    //
    // So the plain entry point now REFUSES to overwrite an existing scene, matching how the RV and travel
    // map builders are named in this project. To change one thing, change it in the scene — or, for a
    // menu row, use Draftmaster > UI > Add SINGLE RACE Row To Title Screen, which edits in place.
    [MenuItem("Draftmaster/Art/Build Title Screen Scene", priority = 125)]
    public static void Build() => EditorUtility.DisplayDialog("Iron Oval", BuildScene(force: false), "OK");

    [MenuItem("Draftmaster/Art/Force Rebuild Title Screen Scene (loses hand edits)", priority = 126)]
    public static void ForceRebuild()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild the title screen?",
                "This regenerates TitleScreen.unity from code and DESTROYS every hand edit in it — the " +
                "wordmark, the layout, any art placed in the scene.\n\nThe last time this ran by mistake it " +
                "reverted the game's name.",
                "Rebuild anyway", "Cancel"))
            return;

        Debug.Log(BuildScene(force: true));
    }

    // Returns what happened rather than announcing it, so it is safe to call from automation, MCP and
    // tests — a DisplayDialog blocks the editor until somebody clicks it.
    public static string BuildScene(bool force)
    {
        if (!force && File.Exists(kScenePath))
            return $"{kScenePath} already exists and was NOT rebuilt — a rebuild would destroy every hand " +
                   "edit in it. Edit the scene directly, or use Force Rebuild Title Screen Scene if you " +
                   "really do want it regenerated from code.";

        var theme = PixelUITheme.Instance;
        if (theme == null || theme.body == null)
            return "The theme has no Iron Oval fonts yet. Run Draftmaster > Art > Set Up Iron Oval Kit first.";

        int W = PixelUITheme.ReferenceWidth;    // 640
        int H = PixelUITheme.ReferenceHeight;   // 360
        var scrim = EnsureScrimSprite();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Screen Space - Camera on an orthographic camera framing the reference canvas: one UI pixel is
        // one rendered pixel, and the screen draws in the editor without entering play mode.
        var camGo = new GameObject("TitleCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = H / 2f / PixelUITheme.ReferencePixelsPerUnit;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.screenBase;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        PixelUI.ApplyScaler(canvasGo.GetComponent<CanvasScaler>());
        var root = (RectTransform)canvasGo.transform;

        // --- title art -------------------------------------------------------------------------------
        // The sheet leaves the art as a slot, and the hatch fill is what stood in for it. The crash tableau
        // (TitleCrashScene, below) is the art now, so the slot stays in the scene but draws nothing —
        // re-enable this Image and drop a Sprite on it to go back to a still hero image instead.
        var art = IronOvalUI.ArtSlot(root, "TitleArt", new Vector2(W, H));
        Stretch(art);
        art.GetComponent<Image>().enabled = false;

        // The sheet's "art goes here" note, kept but switched off for the same reason: the art is there.
        var artNote = IronOvalUI.Label(art, "ArtNote", "[ title art — hero car + oval ]",
                                       IronOvalUI.Role.HeaderSmall, theme.textDisabled);
        var anrt = artNote.rectTransform;
        anrt.anchorMin = new Vector2(1f, 0f);
        anrt.anchorMax = new Vector2(1f, 0f);
        anrt.pivot = new Vector2(1f, 0f);
        anrt.sizeDelta = new Vector2(260f, 10f);
        anrt.anchoredPosition = new Vector2(-16f, 16f);
        artNote.alignment = TextAlignmentOptions.BottomRight;
        artNote.gameObject.SetActive(false);

        // --- scrim -----------------------------------------------------------------------------------
        var scrimGo = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrimGo.transform.SetParent(root, false);
        var scrimRt = (RectTransform)scrimGo.transform;
        scrimRt.anchorMin = new Vector2(0f, 0f);
        scrimRt.anchorMax = new Vector2(0f, 1f);
        scrimRt.pivot = new Vector2(0f, 0.5f);
        scrimRt.sizeDelta = new Vector2(392f, 0f);      // 326 solid + the ramp running out over the art
        scrimRt.anchoredPosition = Vector2.zero;
        var scrimImg = scrimGo.GetComponent<Image>();
        scrimImg.sprite = scrim;
        scrimImg.type = Image.Type.Simple;
        scrimImg.raycastTarget = false;

        // --- copy column -----------------------------------------------------------------------------
        var column = new GameObject("Column", typeof(RectTransform));
        column.transform.SetParent(root, false);
        var col = (RectTransform)column.transform;
        col.anchorMin = new Vector2(0f, 0f);
        col.anchorMax = new Vector2(0f, 1f);
        col.pivot = new Vector2(0f, 0.5f);
        col.sizeDelta = new Vector2(326f, 0f);
        col.anchoredPosition = Vector2.zero;

        float x = 26f;          // left padding
        float y = -30f;         // running top-down cursor, from the column's top edge

        var eyebrow = IronOvalUI.Label(col, "Eyebrow", "STOCK CAR SAGA",
                                       IronOvalUI.Role.HeaderSmall, theme.danger);
        eyebrow.characterSpacing = 6f;                  // the sheet's 3px tracking at this size
        Place(eyebrow.rectTransform, x, y, 280f, 10f);
        y -= 16f;

        // Wordmark: shadow first so the face sits on top of it.
        var shadow = IronOvalUI.Label(col, "WordmarkShadow", "IRON\nOVAL", IronOvalUI.Role.Display, theme.ink);
        shadow.lineSpacing = -22f;                      // 0.88 line-height at 60px
        Place(shadow.rectTransform, x + 4f, y - 4f, 300f, 116f);

        var wordmark = IronOvalUI.Label(col, "Wordmark", "IRON\nOVAL", IronOvalUI.Role.Display, theme.text);
        wordmark.lineSpacing = -22f;
        Place(wordmark.rectTransform, x, y, 300f, 116f);
        y -= 122f;

        // Gold rule + season line.
        var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(col, false);
        rule.GetComponent<Image>().color = theme.gold;
        rule.GetComponent<Image>().raycastTarget = false;
        Place((RectTransform)rule.transform, x, y - 4f, 26f, 3f);

        var season = IronOvalUI.Label(col, "Season", "SEASON ONE · 1987",
                                      IronOvalUI.Role.HeaderSmall, theme.gold);
        season.characterSpacing = 4f;
        Place(season.rectTransform, x + 34f, y - 9f, 240f, 10f);
        y -= 30f;

        // --- menu ------------------------------------------------------------------------------------
        var menuGo = new GameObject("Menu", typeof(RectTransform));
        menuGo.transform.SetParent(col, false);
        var menu = (RectTransform)menuGo.transform;
        Place(menu, x, y, 280f, 150f);

        var ui = canvasGo.AddComponent<TitleScreenUI>();
        ui.raceSceneName = "RaceScene";
        // The shared race scene builds whichever track is selected, so "new season" is a track id rather
        // than a scene: the reference track, which is the one with a finished layout to race on.
        ui.newSeasonTrackId = TrackCatalog.DefaultTrackId;
        ui.rows.Clear();

        var entries = new[]
        {
            // No GARAGE row: the car sheet is reached from the laptop in the RV or in the factory
            // (LaptopInteractable / GarageScreenLoader), not from a menu the player never stands in.
            // TEAM FACTORY is the walk-in half of that — the shop, with the other laptop in it.
            ("NEW SEASON",   TitleScreenUI.Command.NewSeason,  ""),
            ("CONTINUE",     TitleScreenUI.Command.Continue,   ""),
            // SINGLE RACE is the "race anything now" row: pick a track, a series and a driver, then go.
            // It is a plain LoadScene row because the choosing happens in SingleRace.unity, not here.
            // EXHIBITION above it races whatever is ALREADY selected, which is the quick repeat; this is
            // the one that lets the player change their mind, and the only route to the other 37 tracks.
            ("SINGLE RACE",  TitleScreenUI.Command.LoadScene,  "SingleRace"),
            ("EXHIBITION",   TitleScreenUI.Command.Exhibition, ""),
            ("TEAM FACTORY", TitleScreenUI.Command.LoadScene,  "TeamGarage"),
            ("OPTIONS",      TitleScreenUI.Command.NotWired,   ""),
        };

        float rowY = 0f;
        foreach (var (label, command, sceneName) in entries)
        {
            var rowGo = new GameObject("Row_" + label.Replace(' ', '_'), typeof(RectTransform));
            rowGo.transform.SetParent(menu, false);
            var rowRt = (RectTransform)rowGo.transform;
            Place(rowRt, 0f, rowY, 280f, 22f);

            // The kit's own 6x8 arrow rather than a text glyph: the bitmap faces have no ▶ in their
            // atlas, so a typed one renders as a tofu box. It carries IronOvalBlink, which is where the
            // blink lives — the binder only decides which row shows one.
            var cursor = IronOvalUI.Cursor(rowRt);
            Place(cursor.rectTransform, 0f, -7f, 6f, 8f);

            var text = IronOvalUI.Label(rowRt, "Label", label, IronOvalUI.Role.Body, theme.textDisabled);
            text.characterSpacing = 2f;
            Place(text.rectTransform, 18f, 0f, 250f, 22f);

            ui.rows.Add(new TitleScreenUI.Row
            {
                label = label,
                command = command,
                sceneName = sceneName,
                labelText = text,
                cursor = cursor.gameObject,
                rect = rowRt,
            });

            rowY -= 26f;
        }

        // Bake the opening selection so the saved scene reads the way it will run, instead of every row
        // looking dim and every cursor showing at once until Start() gets a word in.
        for (int i = 0; i < ui.rows.Count; i++)
        {
            var built = ui.rows[i];
            if (built.cursor != null) built.cursor.SetActive(i == 0);
            if (built.labelText != null)
                built.labelText.color = i == 0 ? theme.text
                                      : built.command == TitleScreenUI.Command.NotWired ? theme.plateLight
                                      : theme.textDisabled;
        }

        // --- status + footer -------------------------------------------------------------------------
        var status = IronOvalUI.Label(col, "Status", "", IronOvalUI.Role.HeaderSmall, theme.caution);
        Place(status.rectTransform, x, y + rowY - 6f, 280f, 10f);
        ui.statusLabel = status;

        // (C) rather than ©: Silkscreen's bitmap atlas has no copyright glyph and renders it as a tofu box.
        var footer = IronOvalUI.Label(col, "Footer", "(C) 1987 DUFFETY-WONG MOTOR CLUB",
                                      IronOvalUI.Role.HeaderSmall, theme.textDisabled);
        footer.characterSpacing = 2f;
        var frt = footer.rectTransform;
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(0f, 0f);
        frt.pivot = new Vector2(0f, 0f);
        frt.sizeDelta = new Vector2(300f, 10f);
        frt.anchoredPosition = new Vector2(x, 18f);

        // --- crash tableau ---------------------------------------------------------------------------
        // What fills the art half of the sheet: four cars thrown in from off the right edge, time easing to
        // a stop as they land. Built at runtime from carset liveries and real VehicleDamage bodywork, so
        // nothing but the component lands in the scene file — the tableau itself is never serialised.
        var crash = new GameObject("TitleCrash", typeof(TitleCrashScene));
        crash.GetComponent<TitleCrashScene>().layoutCanvas = canvas;

        // --- scanlines -------------------------------------------------------------------------------
        var lines = new GameObject("IronOvalScanlines", typeof(IronOvalScanlines));
        lines.GetComponent<IronOvalScanlines>().opacity = 0.6f;   // the strength RaceScene already runs at

        Directory.CreateDirectory(Path.GetDirectoryName(kScenePath));
        EditorSceneManager.SaveScene(scene, kScenePath);
        AssetDatabase.SaveAssets();

        return $"Iron Oval: built the title screen at {kScenePath}. " +
               "Add it to the build settings (and put it first) to boot into it.";
    }

    // The sheet's column gradient: solid #0d0f16 for the first 62%, then out to nothing. Baked once to a
    // 64x1 PNG so the scene carries an asset rather than a runtime texture.
    static Sprite EnsureScrimSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(kScrimPath);
        if (existing != null) return existing;

        const int w = 64;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        var ink = new Color32(0x0d, 0x0f, 0x16, 0xff);
        for (int i = 0; i < w; i++)
        {
            float t = i / (float)(w - 1);
            float a = t <= 0.62f ? 1f : 1f - Mathf.InverseLerp(0.62f, 1f, t);
            tex.SetPixel(i, 0, new Color32(ink.r, ink.g, ink.b, (byte)Mathf.RoundToInt(a * 255f)));
        }
        tex.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(kScrimPath));
        File.WriteAllBytes(kScrimPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(kScrimPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(kScrimPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(kScrimPath);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Top-left placement in UI pixels: (x, y) is the corner, y running negative down the column.
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }
}
#endif
