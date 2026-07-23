using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the two demo-flow Canvas prefabs into Assets/Resources/UI/ so the runtime can Instantiate them:
//   CarSetupPanel — the crew chief's "how do you want the car?" panel (tyres / fuel / balance)
//   ControlHint   — the bottom-of-screen "LEFT SHIFT — Run" teaching prompt
//
// Same approach as the RV interior builder: a menu item lays the prefab out once, after which it's a normal
// prefab you can open in Prefab Mode and restyle. Re-running overwrites it, so any hand edits are lost —
// rebuild only when you want the stock layout back.
//
// The binder scripts (CarSetupPanelUI / ControlHintUI) find their parts by path, so keep the names if you
// rearrange: Panel/Title, Panel/Tyres/SoftButton, Panel/Fuel/Slider, Panel/KeyBadge/Key, etc.
public static class DemoUIPrefabMenu
{
    const string Folder = "Assets/Resources/UI";
    const string SetupPath = Folder + "/CarSetupPanel.prefab";
    const string HintPath = Folder + "/ControlHint.prefab";

    // Brand-ish palette: near-black panels, one blue accent, off-white type.
    static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color PanelBg = new Color(0.075f, 0.082f, 0.098f, 0.97f);
    static readonly Color Accent = new Color(0.16f, 0.55f, 0.95f, 1f);
    static readonly Color Idle = new Color(0.18f, 0.18f, 0.2f, 1f);
    static readonly Color Ink = new Color(0.95f, 0.95f, 0.96f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.63f, 0.66f, 1f);

    [MenuItem("Draftmaster/UI/Build Demo UI Prefabs")]
    public static void BuildAll()
    {
        BuildCarSetupPanel();
        BuildControlHint();
    }

    [MenuItem("Draftmaster/UI/Build Car Setup Panel Prefab")]
    public static void BuildCarSetupPanel()
    {
        EnsureFolder();
        var root = NewCanvas("CarSetupPanel", 200);
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<CarSetupPanelUI>();

        // Full-screen dim so the pit lane behind reads as "paused for a decision".
        var backdrop = NewImage("Backdrop", root.transform, Backdrop);
        Stretch(backdrop.rectTransform);

        var panel = NewImage("Panel", root.transform, PanelBg);
        Center(panel.rectTransform, 780f, 560f);

        var title = NewText("Title", panel.transform, "CAR SETUP", 40, HeadingFont, Ink, TextAnchor.MiddleLeft);
        TopLeft(title.rectTransform, 44f, -34f, 500f, 56f);

        var subtitle = NewText("Subtitle", panel.transform, "Chief's waiting on your call", 18, BodyFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(subtitle.rectTransform, 46f, -84f, 500f, 28f);

        // --- Tyres
        var tyres = NewGroup("Tyres", panel.transform);
        TopLeft(tyres, 0f, -140f, 780f, 130f);
        var tyreLabel = NewText("Label", tyres, "TYRES", 20, HeadingFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(tyreLabel.rectTransform, 46f, -8f, 240f, 36f);

        var soft = NewButton("SoftButton", tyres, "SOFT", Accent, Ink);
        TopLeft(soft.GetComponent<RectTransform>(), 260f, -4f, 200f, 52f);
        var hard = NewButton("HardButton", tyres, "HARD", Idle, Muted);
        TopLeft(hard.GetComponent<RectTransform>(), 480f, -4f, 200f, 52f);

        var note = NewText("Note", tyres, "Softs: more grip, gone sooner.", 17, BodyFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(note.rectTransform, 262f, -62f, 460f, 32f);

        // --- Fuel
        var fuel = NewGroup("Fuel", panel.transform);
        TopLeft(fuel, 0f, -272f, 780f, 90f);
        var fuelLabel = NewText("Label", fuel, "FUEL", 20, HeadingFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(fuelLabel.rectTransform, 46f, -10f, 240f, 36f);
        var fuelSlider = NewSlider("Slider", fuel, CarSetup.MinFuel, CarSetup.MaxFuel, 12f, true);
        TopLeft(fuelSlider.GetComponent<RectTransform>(), 260f, -18f, 340f, 24f);
        var fuelValue = NewText("Value", fuel, "12 L", 24, HeadingFont, Ink, TextAnchor.MiddleRight);
        TopLeft(fuelValue.rectTransform, 620f, -10f, 110f, 36f);
        var fuelHint = NewText("Hint", fuel, "Every litre is weight. Take what the run needs.", 15, BodyFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(fuelHint.rectTransform, 262f, -52f, 460f, 28f);

        // --- Balance
        var balance = NewGroup("Balance", panel.transform);
        TopLeft(balance, 0f, -376f, 780f, 100f);
        var balLabel = NewText("Label", balance, "BALANCE", 20, HeadingFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(balLabel.rectTransform, 46f, -10f, 240f, 36f);
        var balSlider = NewSlider("Slider", balance, -1f, 1f, 0f, false);
        TopLeft(balSlider.GetComponent<RectTransform>(), 260f, -18f, 340f, 24f);
        var balValue = NewText("Value", balance, "Neutral", 22, HeadingFont, Ink, TextAnchor.MiddleRight);
        TopLeft(balValue.rectTransform, 610f, -10f, 130f, 36f);
        var loose = NewText("LooseLabel", balance, "OVERSTEER", 14, BodyFont, Muted, TextAnchor.MiddleLeft);
        TopLeft(loose.rectTransform, 260f, -50f, 160f, 26f);
        var tight = NewText("TightLabel", balance, "UNDERSTEER", 14, BodyFont, Muted, TextAnchor.MiddleRight);
        TopLeft(tight.rectTransform, 440f, -50f, 160f, 26f);

        // --- Confirm
        var confirm = NewButton("ConfirmButton", panel.transform, "CONFIRM", Accent, Ink, ButtonSprite);
        TopLeft(confirm.GetComponent<RectTransform>(), 260f, -482f, 260f, 60f);

        var confirmHint = NewText("ConfirmHint", panel.transform, "Enter / A", 15, BodyFont, Muted, TextAnchor.MiddleCenter);
        TopLeft(confirmHint.rectTransform, 260f, -540f, 260f, 24f);

        SaveAndClean(root, SetupPath);
        Debug.Log($"[DemoUI] Built {SetupPath}. Open it in Prefab Mode to restyle; CarSetupPanelUI re-finds its parts by name.");
    }

    [MenuItem("Draftmaster/UI/Build Control Hint Prefab")]
    public static void BuildControlHint()
    {
        EnsureFolder();
        var root = NewCanvas("ControlHintUI", 190);
        root.AddComponent<CanvasGroup>();     // ControlHintUI fades this
        root.AddComponent<ControlHintUI>();
        // Deliberately NO GraphicRaycaster: the hint must never swallow a click.

        var panel = NewImage("Panel", root.transform, new Color(0.05f, 0.055f, 0.07f, 0.86f));
        var pr = panel.rectTransform;
        pr.anchorMin = new Vector2(0.5f, 0f);
        pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, 96f);
        pr.sizeDelta = new Vector2(560f, 66f);

        var badge = NewImage("KeyBadge", panel.transform, new Color(0.85f, 0.86f, 0.9f, 1f));
        TopLeft(badge.rectTransform, 14f, -13f, 150f, 40f);
        var key = NewText("Key", badge.transform, "LEFT SHIFT", 19, HeadingFont, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter);
        Stretch(key.rectTransform);

        var hint = NewText("Hint", panel.transform, "Hold to run", 22, BodyFont, Ink, TextAnchor.MiddleLeft);
        TopLeft(hint.rectTransform, 178f, -13f, 366f, 40f);

        SaveAndClean(root, HintPath);
        Debug.Log($"[DemoUI] Built {HintPath}.");
    }

    // Adds the pit-limiter readout to the existing speedometer prefab (SpeedometerUI finds it at
    // "Dial/LimiterText"). Idempotent: run it twice and the second run leaves the chip alone.
    [MenuItem("Draftmaster/UI/Add Pit Limiter Chip To Speedometer")]
    public static void AddLimiterChip()
    {
        const string speedoPath = "Assets/Prefabs/UI/SpeedometerHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(speedoPath);
        if (root == null) { Debug.LogError($"[DemoUI] No prefab at {speedoPath}."); return; }

        var dial = root.transform.Find("Dial");
        if (dial == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[DemoUI] SpeedometerHUD has no Dial child."); return; }

        if (dial.Find("LimiterText") != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[DemoUI] Speedometer already has a LimiterText chip — left alone.");
            return;
        }

        var chip = NewText("LimiterText", dial, "", 22, HeadingFont, new Color(0.35f, 0.85f, 1f), TextAnchor.MiddleCenter);
        var rt = chip.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f); // under the dial
        rt.anchoredPosition = new Vector2(0f, -34f);
        rt.sizeDelta = new Vector2(420f, 30f);

        var ui = root.GetComponent<SpeedometerUI>();
        if (ui != null) ui.limiterText = chip;

        PrefabUtility.SaveAsPrefabAsset(root, speedoPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[DemoUI] Added the pit-limiter chip under the speedometer dial.");
    }

    // --- construction helpers -------------------------------------------------------------------

    static Font HeadingFont => Load<Font>("Assets/Fonts/mania.ttf") ?? BodyFont;
    static Font BodyFont =>
        Load<Font>("Assets/Resources/Fonts/Now-Regular.otf")
        ?? Load<Font>("Assets/Fonts/Now-Medium.otf")
        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    static Sprite ButtonSprite => Load<Sprite>("Assets/GUI/blue-button.png");

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }

    static GameObject NewCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    static RectTransform NewGroup(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        return img;
    }

    static Text NewText(string name, Transform parent, string content, int size, Font font, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button NewButton(string name, Transform parent, string label, Color bg, Color ink, Sprite sprite = null)
    {
        var img = NewImage(name, parent, bg, sprite);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var text = NewText("Text", img.transform, label, 22, HeadingFont, ink, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return button;
    }

    // Canonical uGUI slider (Background / Fill Area / Fill / Handle Slide Area / Handle), built by hand so
    // it doesn't depend on the editor's default-control resources being present.
    static Slider NewSlider(string name, Transform parent, float min, float max, float value, bool wholeNumbers)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var slider = go.AddComponent<Slider>();

        var bg = NewImage("Background", go.transform, new Color(0.22f, 0.23f, 0.26f, 1f));
        var bgr = bg.rectTransform;
        bgr.anchorMin = new Vector2(0f, 0.25f);
        bgr.anchorMax = new Vector2(1f, 0.75f);
        bgr.sizeDelta = Vector2.zero;
        bgr.anchoredPosition = Vector2.zero;

        var fillArea = NewGroup("Fill Area", go.transform);
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.offsetMin = new Vector2(5f, 0f);
        fillArea.offsetMax = new Vector2(-15f, 0f);

        var fill = NewImage("Fill", fillArea, Accent);
        var fr = fill.rectTransform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(0f, 1f);
        fr.sizeDelta = new Vector2(10f, 0f);

        var handleArea = NewGroup("Handle Slide Area", go.transform);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        var handle = NewImage("Handle", handleArea, new Color(0.93f, 0.94f, 0.96f, 1f));
        var hr = handle.rectTransform;
        hr.anchorMin = Vector2.zero;
        hr.anchorMax = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(22f, 0f);

        slider.fillRect = fr;
        slider.handleRect = hr;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.value = value;
        return slider;
    }

    // --- rect helpers ---------------------------------------------------------------------------

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    // Position from the parent's top-left corner, which is how the layout above is written.
    static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void SaveAndClean(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
