using System;
using System.Collections.Generic;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Thin binder for the authored pre-race setup panel (Assets/Resources/UI/CarSetupPanel.prefab).
//
// The crew chief asks how you want the car; this is the answer. Four choices — grille tape, fuel load, balance,
// steering ratio —
// then CONFIRM hands a CarSetup back to whoever opened it (PitLaneStart), which applies it and lets the driver go.
// Colours and parts are authored in the prefab; the layout is not. The prefab was drawn on a 1920x1080 canvas
// that scaled with the screen (ScaleWithScreenSize, match 0.5), which gave every device a different fractional
// scale and set 16 px captions at a third of the kit's Data size — tiny on a monitor. On open the panel is put
// on the kit's 640x360 grid instead (FitToKit): a whole-number canvas scale off the short side, as PixelGUI's
// panels use, every caption at the Data role, and the rows laid out from the measured text.
public class CarSetupPanelUI : MonoBehaviour
{
    const string PrefabPath = "UI/CarSetupPanel";

    [Header("Authored children (auto-wired)")]
    public Text titleText;
    public Slider tapeSlider;
    public Text tapeValueText;
    public Text tapeNoteText;
    public Slider fuelSlider;
    public Text fuelValueText;
    public Slider balanceSlider;
    public Text balanceValueText;
    public Slider steeringSlider;
    public Text steeringValueText;
    public Button confirmButton;

    CarSetup _setup;
    Action<CarSetup> _onConfirm;
    bool _confirmed;
    CursorLockMode _prevCursorLock;
    bool _prevCursorVisible;
    int _openFrame = -1;

    // Is a setup panel currently up? Callers use this to hold the car still while the driver decides.
    public static bool IsOpen { get; private set; }

    // Spawn the panel from Resources and hand back the instance. `setup` seeds the controls (pass
    // CarSetup.Load() for the driver's last choices); onConfirm fires once, with the chosen setup. `title`
    // replaces the authored heading when given (the race-morning plan in the motorhome).
    public static CarSetupPanelUI Open(CarSetup setup, Action<CarSetup> onConfirm, string title = null)
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"CarSetupPanelUI: no prefab at Resources/{PrefabPath} — confirming the setup unchanged " +
                           "so the driver isn't stranded in a car that won't start.");
            onConfirm?.Invoke(setup ?? new CarSetup());
            return null;
        }

        EnsureEventSystem();

        var go = Instantiate(prefab);
        go.name = "CarSetupPanel";
        var ui = go.GetComponent<CarSetupPanelUI>();
        if (ui == null) ui = go.AddComponent<CarSetupPanelUI>();
        ui.Bind(setup ?? new CarSetup(), onConfirm);
        if (!string.IsNullOrEmpty(title) && ui.titleText != null) ui.titleText.text = title;
        return ui;
    }

    // The racing scenes have no EventSystem (nothing in them is clickable), so the panel's buttons and
    // sliders would be dead without one.
    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void Awake() => ResolveRefs();

    void OnDestroy()
    {
        if (IsOpen) IsOpen = false;
        Cursor.lockState = _prevCursorLock;
        Cursor.visible = _prevCursorVisible;
    }

    void Bind(CarSetup setup, Action<CarSetup> onConfirm)
    {
        _setup = setup.Clone();
        _onConfirm = onConfirm;
        IsOpen = true;
        _openFrame = Time.frameCount;

        // The driving scenes hide the pointer; the panel needs it back to drag the sliders.
        _prevCursorLock = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ResolveRefs();

        if (confirmButton != null) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }

        // Grille tape in 5% steps (0..20 on the slider).
        if (tapeSlider != null)
        {
            tapeSlider.minValue = 0f;
            tapeSlider.maxValue = TapeSteps;
            tapeSlider.wholeNumbers = true;
            tapeSlider.SetValueWithoutNotify(Mathf.Round(Draftmaster.Sim.GrilleTape.Clamp(_setup.tape) * TapeSteps));
            tapeSlider.onValueChanged.RemoveAllListeners();
            tapeSlider.onValueChanged.AddListener(v => { _setup.tape = v / TapeSteps; Refresh(); });
        }

        if (fuelSlider != null)
        {
            fuelSlider.minValue = CarSetup.MinFuel;
            fuelSlider.maxValue = CarSetup.MaxFuel;
            fuelSlider.wholeNumbers = true;
            fuelSlider.SetValueWithoutNotify(Mathf.Round(_setup.fuelLitres));
            fuelSlider.onValueChanged.RemoveAllListeners();
            fuelSlider.onValueChanged.AddListener(v => { _setup.fuelLitres = v; Refresh(); });
        }

        if (balanceSlider != null)
        {
            balanceSlider.minValue = -1f;
            balanceSlider.maxValue = 1f;
            balanceSlider.wholeNumbers = false;
            balanceSlider.SetValueWithoutNotify(_setup.balance);
            balanceSlider.onValueChanged.RemoveAllListeners();
            balanceSlider.onValueChanged.AddListener(v => { _setup.balance = v; Refresh(); });
        }

        if (steeringSlider != null)
        {
            steeringSlider.minValue = Draftmaster.Sim.SteeringRatio.Min;
            steeringSlider.maxValue = Draftmaster.Sim.SteeringRatio.Max;
            steeringSlider.wholeNumbers = true;
            steeringSlider.SetValueWithoutNotify(Draftmaster.Sim.SteeringRatio.Clamp(_setup.steeringRatio));
            steeringSlider.onValueChanged.RemoveAllListeners();
            steeringSlider.onValueChanged.AddListener(v => { _setup.steeringRatio = v; Refresh(); });
        }

        Refresh();
        // Every screen's button height and caption (UIButton) and the kit's type sizes, whatever the prefab
        // was last restyled to.
        FitToKit();
    }

    void Update()
    {
        if (_confirmed) return;
        if (Screen.width != _fitW || Screen.height != _fitH) FitToKit();
        // The button press that closed the crew chief's last line is still "pressed this frame" when the
        // panel opens — without this guard a gamepad player confirms the default setup instantly.
        if (Time.frameCount <= _openFrame) return;
        // Confirm from the keyboard/pad too — the driver is sat in the car, not on a mouse.
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Confirm();
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) Confirm();
    }

    const float TapeSteps = 20f;

    void Confirm()
    {
        if (_confirmed) return;
        _confirmed = true;
        IsOpen = false;
        _setup.Save();
        var result = _setup.Clone();
        var cb = _onConfirm;
        _onConfirm = null;
        Destroy(gameObject);
        cb?.Invoke(result);
    }

    void Refresh()
    {
        if (tapeValueText != null) tapeValueText.text = _setup.TapeLabel;
        if (tapeNoteText != null) tapeNoteText.text = Draftmaster.Sim.GrilleTape.Note(_setup.tape);
        if (fuelValueText != null) fuelValueText.text = $"{Mathf.RoundToInt(_setup.fuelLitres)} L";
        if (balanceValueText != null) balanceValueText.text = _setup.BalanceLabel;
        if (steeringValueText != null) steeringValueText.text = _setup.SteeringRatioLabel;
    }


    // ------------------------------------------------------------------ layout on the kit grid

    const float KitH = PixelUITheme.ReferenceHeight;   // 360: canvas units are kit pixels
    const float Pad = 10f;
    const float ColGap = 8f;
    const float RowGap = 6f;
    const float MinSliderW = 112f;
    const float SliderH = 8f;
    int _fitW, _fitH;

    void FitToKit()
    {
        _fitW = Screen.width;
        _fitH = Screen.height;
        int scale = PixelGUI.Scale;

        var canvas = GetComponent<Canvas>();
        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale;
        }
        if (canvas != null) { canvas.scaleFactor = scale; canvas.pixelPerfect = true; }

        var panel = Find<RectTransform>("Panel");
        if (panel == null) return;

        // Every caption at the Data role on the kit grid (the monogram face's own 16 px cell), so the panel
        // reads at the size of the HUD and the PixelGUI panels around it on every screen.
        int textPx = UIType.CanvasPx(TypeRole.Data, KitH, 16);
        foreach (var t in panel.GetComponentsInChildren<Text>(true))
        {
            t.fontSize = textPx;
            t.resizeTextForBestFit = false;
            t.lineSpacing = 1f;
        }
        StyleAsKitWindow(panel);

        float lineH = Mathf.Ceil(LineHeight(titleText != null ? titleText : panel.GetComponentInChildren<Text>(true)));

        // Columns sized to the widest thing each will ever hold, not what it holds now, so dragging a slider
        // never reflows the panel.
        float labelW = 0f;
        foreach (var row in Rows) labelW = Mathf.Max(labelW, Width(Find<Text>($"Panel/{row}/Label")));
        float valueW = 0f;
        var probe = tapeValueText != null ? tapeValueText : titleText;
        foreach (var v in ValueSamples()) valueW = Mathf.Max(valueW, Width(probe, v));
        labelW = Mathf.Ceil(labelW);
        valueW = Mathf.Ceil(valueW);

        float sliderW = MinSliderW;
        sliderW = Mathf.Max(sliderW, Mathf.Ceil(Width(Find<Text>("Panel/Balance/LooseLabel")) +
                                                Width(Find<Text>("Panel/Balance/TightLabel")) + ColGap));
        sliderW = Mathf.Max(sliderW, Mathf.Ceil(Width(Find<Text>("Panel/Steering/QuickLabel")) +
                                                Width(Find<Text>("Panel/Steering/SlowLabel")) + ColGap));

        float contentW = labelW + ColGap + sliderW + ColGap + valueW;
        // An upright phone is 360 kit px across: give way in the slider, never in the type.
        float maxW = Mathf.Floor(Screen.width / (float)scale) - 8f;
        if (contentW + Pad * 2f > maxW)
        {
            float cut = Mathf.Min(contentW + Pad * 2f - maxW, sliderW - 48f);
            sliderW -= cut;
            contentW -= cut;
        }
        float panelW = contentW + Pad * 2f;
        float sliderX = Pad + labelW + ColGap;
        float subW = sliderW + ColGap + valueW;   // under-slider lines run on under the value column

        float y = Pad;
        y = Place(titleText, Pad, y, contentW, lineH);
        var subtitle = Find<Text>("Panel/Subtitle");
        if (subtitle != null)
        {
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            y = Place(subtitle, Pad, y, contentW, Mathf.Max(lineH, WrappedHeight(subtitle, subtitle.text, contentW)));
        }
        y += RowGap;

        foreach (var row in Rows)
        {
            var rowRt = Find<RectTransform>($"Panel/{row}");
            if (rowRt == null || !rowRt.gameObject.activeSelf) continue;
            float ry = 0f;
            Place(Find<Text>($"Panel/{row}/Label"), Pad, ry, labelW, lineH);
            Place(Find<Text>($"Panel/{row}/Value"), sliderX + sliderW + ColGap, ry, valueW, lineH);
            var slider = Find<Slider>($"Panel/{row}/Slider");
            if (slider != null)
            {
                TopLeft((RectTransform)slider.transform, sliderX, Mathf.Round((lineH - SliderH) * 0.5f), sliderW, SliderH);
                FitSlider(slider);
            }
            ry += lineH;

            float subH = 0f;
            var note = Find<Text>($"Panel/{row}/Note");
            if (note == null) note = Find<Text>($"Panel/{row}/Hint");
            if (note != null)
            {
                note.horizontalOverflow = HorizontalWrapMode.Wrap;
                float h = row == "Tape" ? NoteHeight(note, subW) : WrappedHeight(note, note.text, subW);
                Place(note, sliderX, ry, subW, h);
                subH = Mathf.Max(subH, h);
            }
            foreach (var end in new[] { ("LooseLabel", false), ("TightLabel", true), ("QuickLabel", false), ("SlowLabel", true) })
            {
                var t = Find<Text>($"Panel/{row}/{end.Item1}");
                if (t == null) continue;
                float w = Mathf.Ceil(Width(t));
                Place(t, end.Item2 ? sliderX + sliderW - w : sliderX, ry, w, lineH);
                subH = Mathf.Max(subH, lineH);
            }
            ry += subH;

            TopLeft(rowRt, 0f, y, panelW, ry);
            y += ry + RowGap;
        }

        // The confirm button is drawn by PixelGUI.ActionButton (OnGUI) — the gold-framed button with the key,
        // pad button or steering-wheel picture on it that the grandstand and objective strip use — so the
        // authored uGUI button only holds its place in the layout. The "Enter / A" hint line is what the
        // button's own glyph now says, so it goes.
        if (confirmButton != null)
        {
            float bh = Mathf.Ceil(PixelGUI.ActionButtonSize(ConfirmKey, ConfirmLabel).y / scale);
            TopLeft((RectTransform)confirmButton.transform, Pad, y, contentW, bh);
            confirmButton.gameObject.SetActive(false);
            y += bh;
        }
        var hint = Find<Text>("Panel/ConfirmHint");
        if (hint != null) hint.gameObject.SetActive(false);
        y += Pad;

        // Even kit-pixel size centred on the screen, so the panel's edges land on whole screen pixels.
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(Mathf.Ceil(panelW / 2f) * 2f, Mathf.Ceil(y / 2f) * 2f);
        panel.anchoredPosition = Vector2.zero;
    }

    static readonly string[] Rows = { "Tape", "Fuel", "Balance", "Steering" };

    const string ConfirmKey = "Enter";
    const string ConfirmLabel = "Confirm";

    // The box every prompt sits in (PixelGUI.Panel, focused): the gold 9-sliced frame with the kit's dithered
    // blue fill inside its 4 px border, and the text and sliders in the kit's palette. The panel's own Image
    // becomes the frame (drawn first, as a parent is) and a "Fill" child, first in line, tiles the dither over
    // its centre, so everything else still draws on top.
    static void StyleAsKitWindow(RectTransform panel)
    {
        var theme = PixelUITheme.Instance;
        if (theme == null) return;

        var frame = panel.GetComponent<Image>();
        if (frame != null)
        {
            frame.sprite = theme.frameGold;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = true;
            frame.pixelsPerUnitMultiplier = 1f;
            frame.color = theme.frameGold != null ? Color.white : theme.plate;
        }

        var fill = panel.Find("Fill") as RectTransform;
        if (fill == null)
        {
            fill = IronOvalUI.Tile(panel, "Fill", theme.panelFill).rectTransform;
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
        }
        fill.SetAsFirstSibling();

        foreach (var t in panel.GetComponentsInChildren<Text>(true))
        {
            if (t.transform.parent == panel && t.name == "Title") t.color = theme.gold;
            else if (t.name == "Value") t.color = theme.text;
            else t.color = theme.textDim;
        }

        foreach (var s in panel.GetComponentsInChildren<Slider>(true))
        {
            if (s.transform.Find("Background") is RectTransform bg && bg.TryGetComponent<Image>(out var track))
                track.color = theme.ink;
            if (s.fillRect != null && s.fillRect.TryGetComponent<Image>(out var bar)) bar.color = theme.gold;
            if (s.handleRect != null && s.handleRect.TryGetComponent<Image>(out var knob)) knob.color = theme.text;
        }
    }

    void OnGUI()
    {
        if (_confirmed || confirmButton == null) return;
        var slot = (RectTransform)confirmButton.transform;

        // An overlay canvas's world space is screen pixels, y up; IMGUI's is y down.
        var corners = new Vector3[4];
        slot.GetWorldCorners(corners);
        float top = Screen.height - corners[1].y;
        float h = corners[1].y - corners[0].y;

        Vector2 size = PixelGUI.ActionButtonSize(ConfirmKey, ConfirmLabel);
        float x = Mathf.Round((corners[0].x + corners[2].x - size.x) * 0.5f);
        if (PixelGUI.ActionButton(new Rect(x, Mathf.Round(top), size.x, Mathf.Round(h)), PixelGUI.ActionIcon.Car,
                                  ConfirmKey, PadButton.South, ConfirmLabel))
            Confirm();
    }

    IEnumerable<string> ValueSamples()
    {
        for (int i = 0; i <= (int)TapeSteps; i++) yield return Draftmaster.Sim.GrilleTape.Label(i / TapeSteps);
        yield return $"{Mathf.RoundToInt(CarSetup.MaxFuel)} L";
        var probe = new CarSetup();
        for (int i = -20; i <= 20; i++) { probe.balance = i / 20f; yield return probe.BalanceLabel; }
        for (float r = Draftmaster.Sim.SteeringRatio.Min; r <= Draftmaster.Sim.SteeringRatio.Max; r++)
            yield return Draftmaster.Sim.SteeringRatio.Label(r);
    }

    // The tape note changes with the slider: room for the longest of them, so the rows below never jump.
    static float NoteHeight(Text note, float width)
    {
        float h = 0f;
        for (int i = 0; i <= (int)TapeSteps; i++)
            h = Mathf.Max(h, WrappedHeight(note, Draftmaster.Sim.GrilleTape.Note(i / TapeSteps), width));
        return h;
    }

    // The slider parts were authored in 1080 units (a 22 px handle, 5/15 px insets), which on the kit grid
    // would make the handle a fifth of the slider. Kit-sized: a 6 px handle on a 4 px track, fill under it.
    static void FitSlider(Slider slider)
    {
        var root = (RectTransform)slider.transform;
        if (root.Find("Background") is RectTransform bg)
        {
            bg.anchorMin = new Vector2(0f, 0.25f);
            bg.anchorMax = new Vector2(1f, 0.75f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;
        }
        if (root.Find("Fill Area") is RectTransform fillArea)
        {
            fillArea.offsetMin = new Vector2(3f, fillArea.offsetMin.y);
            fillArea.offsetMax = new Vector2(-3f, fillArea.offsetMax.y);
        }
        if (slider.fillRect != null) slider.fillRect.sizeDelta = new Vector2(0f, slider.fillRect.sizeDelta.y);
        if (root.Find("Handle Slide Area") is RectTransform handleArea)
        {
            handleArea.offsetMin = new Vector2(3f, 0f);
            handleArea.offsetMax = new Vector2(-3f, 0f);
        }
        if (slider.handleRect != null) slider.handleRect.sizeDelta = new Vector2(6f, 0f);
    }

    static float Place(Text t, float x, float y, float w, float h)
    {
        if (t == null) return y;
        TopLeft(t.rectTransform, x, y, w, h);
        return y + h;
    }

    static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        rt.sizeDelta = new Vector2(Mathf.Ceil(w), Mathf.Ceil(h));
    }

    static float Width(Text t) => t == null ? 0f : Width(t, t.text);

    static float Width(Text t, string s)
    {
        if (t == null || string.IsNullOrEmpty(s)) return 0f;
        var gs = t.GetGenerationSettings(Vector2.zero);
        gs.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t.cachedTextGeneratorForLayout.GetPreferredWidth(s, gs) / t.pixelsPerUnit;
    }

    static float WrappedHeight(Text t, string s, float width)
    {
        if (t == null || string.IsNullOrEmpty(s)) return 0f;
        var gs = t.GetGenerationSettings(new Vector2(width, 0f));
        gs.horizontalOverflow = HorizontalWrapMode.Wrap;
        return Mathf.Ceil(t.cachedTextGeneratorForLayout.GetPreferredHeight(s, gs) / t.pixelsPerUnit);
    }

    static float LineHeight(Text t) => t == null ? 16f : Mathf.Max(t.fontSize, WrappedHeight(t, "Ag", 1000f));

    // Locate the authored parts by path, so the prefab can be rearranged without re-wiring every field by hand.
    void ResolveRefs()
    {
        if (titleText == null) titleText = Find<Text>("Panel/Title");
        if (tapeSlider == null) tapeSlider = Find<Slider>("Panel/Tape/Slider");
        if (tapeValueText == null) tapeValueText = Find<Text>("Panel/Tape/Value");
        if (tapeNoteText == null) tapeNoteText = Find<Text>("Panel/Tape/Note");
        if (fuelSlider == null) fuelSlider = Find<Slider>("Panel/Fuel/Slider");
        if (fuelValueText == null) fuelValueText = Find<Text>("Panel/Fuel/Value");
        if (balanceSlider == null) balanceSlider = Find<Slider>("Panel/Balance/Slider");
        if (balanceValueText == null) balanceValueText = Find<Text>("Panel/Balance/Value");
        if (steeringSlider == null) steeringSlider = Find<Slider>("Panel/Steering/Slider");
        if (steeringValueText == null) steeringValueText = Find<Text>("Panel/Steering/Value");
        if (confirmButton == null) confirmButton = Find<Button>("Panel/ConfirmButton");
    }

    T Find<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

#if UNITY_EDITOR
    void OnValidate() => ResolveRefs();
#endif
}
