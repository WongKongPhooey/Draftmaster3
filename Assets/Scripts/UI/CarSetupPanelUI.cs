using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Thin binder for the authored pre-race setup panel (Assets/Resources/UI/CarSetupPanel.prefab).
//
// The crew chief asks how you want the car; this is the answer. Three choices — compound, fuel load, balance —
// then CONFIRM hands a CarSetup back to whoever opened it (PitLaneStart), which applies it and lets the driver go.
// Layout, fonts and colours are authored in the prefab; this script only reflects state and reports the result.
public class CarSetupPanelUI : MonoBehaviour
{
    const string PrefabPath = "UI/CarSetupPanel";

    [Header("Authored children (auto-wired)")]
    public Text titleText;
    public Button softButton;
    public Button hardButton;
    public Text compoundNoteText;
    public Slider fuelSlider;
    public Text fuelValueText;
    public Slider balanceSlider;
    public Text balanceValueText;
    public Button confirmButton;

    [Header("Selection colours")]
    public Color selectedColor = new Color(0.16f, 0.55f, 0.95f);
    public Color unselectedColor = new Color(0.18f, 0.18f, 0.2f);
    public Color selectedTextColor = Color.white;
    public Color unselectedTextColor = new Color(0.75f, 0.75f, 0.78f);

    [Tooltip("Note shown under the tyre buttons for each compound.")]
    public string softNote = "Softs: more grip, gone sooner.";
    public string hardNote = "Hards: a touch slower, they'll last.";

    CarSetup _setup;
    Action<CarSetup> _onConfirm;
    bool _confirmed;

    // Is a setup panel currently up? Callers use this to hold the car still while the driver decides.
    public static bool IsOpen { get; private set; }

    // Spawn the panel from Resources and hand back the instance. `setup` seeds the controls (pass
    // CarSetup.Load() for the driver's last choices); onConfirm fires once, with the chosen setup.
    public static CarSetupPanelUI Open(CarSetup setup, Action<CarSetup> onConfirm)
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"CarSetupPanelUI: no prefab at Resources/{PrefabPath} — confirming the setup unchanged " +
                           "so the driver isn't stranded in a car that won't start.");
            onConfirm?.Invoke(setup ?? new CarSetup());
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "CarSetupPanel";
        var ui = go.GetComponent<CarSetupPanelUI>();
        if (ui == null) ui = go.AddComponent<CarSetupPanelUI>();
        ui.Bind(setup ?? new CarSetup(), onConfirm);
        return ui;
    }

    void Awake() => ResolveRefs();

    void OnDestroy()
    {
        if (IsOpen) IsOpen = false;
    }

    void Bind(CarSetup setup, Action<CarSetup> onConfirm)
    {
        _setup = setup.Clone();
        _onConfirm = onConfirm;
        IsOpen = true;

        ResolveRefs();

        if (softButton != null) { softButton.onClick.RemoveAllListeners(); softButton.onClick.AddListener(() => SetCompound(TireModel.Compound.Soft)); }
        if (hardButton != null) { hardButton.onClick.RemoveAllListeners(); hardButton.onClick.AddListener(() => SetCompound(TireModel.Compound.Hard)); }
        if (confirmButton != null) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }

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

        Refresh();
    }

    void Update()
    {
        if (_confirmed) return;
        // Confirm from the keyboard/pad too — the driver is sat in the car, not on a mouse.
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Confirm();
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) Confirm();
    }

    void SetCompound(TireModel.Compound c)
    {
        _setup.compound = c;
        Refresh();
    }

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
        bool soft = _setup.compound == TireModel.Compound.Soft;
        Tint(softButton, soft);
        Tint(hardButton, !soft);
        if (compoundNoteText != null) compoundNoteText.text = soft ? softNote : hardNote;
        if (fuelValueText != null) fuelValueText.text = $"{Mathf.RoundToInt(_setup.fuelLitres)} L";
        if (balanceValueText != null) balanceValueText.text = _setup.BalanceLabel;
    }

    void Tint(Button b, bool selected)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null) img.color = selected ? selectedColor : unselectedColor;
        var label = b.GetComponentInChildren<Text>();
        if (label != null) label.color = selected ? selectedTextColor : unselectedTextColor;
    }

    // Locate the authored parts by path, so the prefab can be rearranged without re-wiring every field by hand.
    void ResolveRefs()
    {
        if (titleText == null) titleText = Find<Text>("Panel/Title");
        if (softButton == null) softButton = Find<Button>("Panel/Tyres/SoftButton");
        if (hardButton == null) hardButton = Find<Button>("Panel/Tyres/HardButton");
        if (compoundNoteText == null) compoundNoteText = Find<Text>("Panel/Tyres/Note");
        if (fuelSlider == null) fuelSlider = Find<Slider>("Panel/Fuel/Slider");
        if (fuelValueText == null) fuelValueText = Find<Text>("Panel/Fuel/Value");
        if (balanceSlider == null) balanceSlider = Find<Slider>("Panel/Balance/Slider");
        if (balanceValueText == null) balanceValueText = Find<Text>("Panel/Balance/Value");
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
