#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/Scenes/IronOvalShowcase.unity: every Iron Oval widget on one 640x360 screen, so the
// direction can be judged before anything in RaceScene is touched. Nothing else in the project is
// modified by this — it writes one scene and stops.
//
//   Draftmaster > Art > Build Iron Oval Showcase Scene
//
// The canvas is Screen Space - Camera on its own orthographic camera rather than Overlay, for one
// practical reason: an Overlay canvas cannot be captured through a camera, so an Overlay showcase can
// only be looked at by entering play mode. On a camera it renders in the editor.
public static class IronOvalShowcaseBuilder
{
    const string kScenePath = "Assets/Scenes/IronOvalShowcase.unity";

    [MenuItem("Draftmaster/Art/Build Iron Oval Showcase Scene", priority = 124)]
    public static void Build()
    {
        var theme = PixelUITheme.Instance;
        if (theme == null || theme.frameCream == null)
        {
            EditorUtility.DisplayDialog("Iron Oval",
                "The theme has no Iron Oval art yet. Run Draftmaster > Art > Set Up Iron Oval Kit first.", "OK");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int W = PixelUITheme.ReferenceWidth;   // 640
        int H = PixelUITheme.ReferenceHeight;  // 360

        // Camera framing exactly the reference canvas, so one UI pixel is one rendered pixel at 640x360
        // and a whole number of them at any integer multiple.
        var camGo = new GameObject("ShowcaseCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = H / 2f / PixelUITheme.ReferencePixelsPerUnit;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.screenBase;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        camGo.tag = "MainCamera";

        var canvasGo = new GameObject("IronOvalShowcase", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        PixelUI.ApplyScaler(canvasGo.GetComponent<CanvasScaler>());
        var root = (RectTransform)canvasGo.transform;

        // Screen base behind everything, then the deep dither over it.
        var bg = IronOvalUI.Tile(root, "ScreenBase", theme.panelFillDeep);
        bg.transform.SetSiblingIndex(0);

        BuildHeader(root, theme);
        BuildSwatches(root, theme);
        BuildTypeSpecimens(root, theme);
        BuildButtons(root, theme);
        BuildWindows(root, theme);
        BuildDialogue(root, theme);
        BuildScanlines(root, theme);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, kScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[IronOvalShowcase] built {kScenePath} — open it and look at the Game view at 640x360 " +
                  "(or any integer multiple).");
    }

    static RectTransform At(RectTransform rt, float x, float y, float w = 0f, float h = 0f)
    {
        // Top-left origin in UI pixels: easier to reason about against the design, which is laid out
        // from the top-left too.
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        if (w > 0f || h > 0f) rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        return rt;
    }

    static void BuildHeader(RectTransform root, PixelUITheme t)
    {
        At(IronOvalUI.Label(root, "Kicker", "IRON OVAL · UI KIT", IronOvalUI.Role.HeaderSmall).rectTransform,
           12, 10, 300, 10);
        var title = IronOvalUI.Label(root, "Title", "RACE FURNITURE", IronOvalUI.Role.Header, t.text);
        At(title.rectTransform, 12, 22, 300, 18);
    }

    static void BuildSwatches(RectTransform root, PixelUITheme t)
    {
        (string label, Color c)[] swatches =
        {
            ("VOID", t.screenBase), ("INK", t.ink), ("BASE", t.plateDeep), ("PANEL", t.plate),
            ("SHADE", t.plateLight), ("DIM", t.textDim), ("TEXT", t.text),
            ("ACCENT", t.gold), ("ALARM", t.danger), ("TELEM", t.info), ("GAIN", t.confirm),
        };
        for (int i = 0; i < swatches.Length; i++)
        {
            var go = new GameObject("Swatch_" + swatches[i].label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            go.GetComponent<Image>().color = swatches[i].c;
            go.GetComponent<Image>().raycastTarget = false;
            At((RectTransform)go.transform, 12 + i * 22, 46, 20, 14);
        }
        At(IronOvalUI.Label(root, "SwatchNote", "ONE ACCENT · RED IS ALARM ONLY",
                            IronOvalUI.Role.HeaderSmall, t.textDisabled).rectTransform, 12, 64, 300, 10);
    }

    static void BuildTypeSpecimens(RectTransform root, PixelUITheme t)
    {
        At(IronOvalUI.Label(root, "TypeHeader", "LAP 118/200", IronOvalUI.Role.Header).rectTransform,
           12, 82, 220, 18);
        var body = IronOvalUI.Label(root, "TypeBody", "Twelve laps of clean air.", IronOvalUI.Role.Body);
        At(body.rectTransform, 12, 102, 290, 24);
        var data = IronOvalUI.Label(root, "TypeData",
            "4  #11  HAMLIN   +8.1s\n5  #03  A.DILLON +9.0s", IronOvalUI.Role.Data, t.text);
        At(data.rectTransform, 12, 128, 290, 40);
    }

    static void BuildButtons(RectTransform root, PixelUITheme t)
    {
        var confirm = IronOvalUI.Button(root, "Confirm", "CONTINUE", new Vector2(120f, 26f));
        At((RectTransform)confirm.transform, 12, 178);

        var disabled = IronOvalUI.Button(root, "Disabled", "PIT NOW", new Vector2(110f, 26f));
        At((RectTransform)disabled.transform, 140, 178);
        disabled.interactable = false;

        string[] tabs = { "DRIVE", "TEAM", "CHIEF" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = IronOvalUI.TabButton(root, "Tab_" + tabs[i], tabs[i], new Vector2(62f, 18f), i == 0);
            At((RectTransform)tab.transform.parent, 12 + i * 68, 214);
        }

        // Cursor + a menu row, the selection idiom the design uses everywhere.
        var cursor = IronOvalUI.Cursor(root);
        At(cursor.rectTransform, 14, 248);
        At(IronOvalUI.Label(root, "MenuRow0", "Take the wheel", IronOvalUI.Role.Body, t.text).rectTransform,
           26, 242, 240, 24);
        At(IronOvalUI.Label(root, "MenuRow1", "Stay on the wall", IronOvalUI.Role.Body, t.textDisabled).rectTransform,
           26, 266, 240, 24);
    }

    static void BuildWindows(RectTransform root, PixelUITheme t)
    {
        // Unfocused window with a telemetry readout: the shape a HUD block takes.
        var win = IronOvalUI.Window(root, "TelemetryWindow", new Vector2(150f, 92f));
        At(win, 330, 84);
        At(IronOvalUI.Label(win, "Head", "TYRE", IronOvalUI.Role.HeaderSmall, t.danger).rectTransform, 10, 8, 120, 10);
        var tyre = IronOvalUI.StatCells(win, "TyreCells", 6, 10, t.danger);
        At(tyre, 10, 22);
        At(IronOvalUI.Label(win, "Head2", "DRAFT", IronOvalUI.Role.HeaderSmall, t.info).rectTransform, 10, 40, 120, 10);
        var draft = IronOvalUI.StatCells(win, "DraftCells", 4, 10, t.info);
        At(draft, 10, 54);
        At(IronOvalUI.Label(win, "Gap", "P4 /20   +8.1s", IronOvalUI.Role.Data, t.text).rectTransform, 10, 70, 130, 16);

        // Focused window (gold frame) holding an art slot — the sheet's focus idiom next to the default.
        var focused = IronOvalUI.Window(root, "FocusedWindow", new Vector2(140f, 92f), focused: true);
        At(focused, 490, 84);
        var slot = IronOvalUI.ArtSlot(focused, "MapSlot", new Vector2(116f, 60f), "[ track map ]");
        At(slot, 12, 12);
        At(IronOvalUI.Label(focused, "FocusNote", "FOCUSED", IronOvalUI.Role.HeaderSmall).rectTransform, 12, 76, 120, 10);
    }

    static void BuildDialogue(RectTransform root, PixelUITheme t)
    {
        // 20px body over three lines plus the name row needs ~110px of window; anything less and the copy
        // runs out through the frame.
        var dialogue = IronOvalUI.Dialogue(root, new Vector2(300f, 114f));
        At((RectTransform)dialogue.transform, 330, 186);
        dialogue.Set("Ray — Crew Chief",
                     "Forty-first is not where I put you. Twelve laps of clean air and we take it back.",
                     "TRUST 3/5");
    }

    static void BuildScanlines(RectTransform root, PixelUITheme t)
    {
        var scan = IronOvalUI.Tile(root, "Scanlines", t.scanline);
        scan.transform.SetAsLastSibling();
        scan.raycastTarget = false;
    }
}
#endif
