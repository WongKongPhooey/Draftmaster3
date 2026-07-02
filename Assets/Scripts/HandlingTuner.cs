using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Play-time handling tuner. A draggable on-screen panel with sliders for the understeer-relevant
// PlayerVehicleController fields, plus live per-axle slip telemetry so you can SEE the balance while you tweak.
// Bound by reflection, so edits apply to the live car instantly (no recompile). Press F1 to toggle.
//
// Self-bootstraps in every scene; the panel only shows once it finds the human car, so it's harmless in menus.
// It's a dev tool — strip the bootstrap (or set startVisible=false) before shipping.
public class HandlingTuner : MonoBehaviour
{
    public static HandlingTuner Instance { get; private set; }

    public bool startVisible = true;
    public Key toggleKey = Key.F1;

    // Field name on PlayerVehicleController + slider range. Curated to the knobs that drive understeer/oversteer.
    struct Knob { public string field; public float min, max; public Knob(string f, float a, float b) { field = f; min = a; max = b; } }

    static readonly Knob[] Knobs =
    {
        new Knob("corneringStiffness",   5f,   30f),
        new Knob("understeerBias",      -0.3f,  0.3f),
        new Knob("frontWeightBias",      0.40f, 0.65f),
        new Knob("yawDamping",           0f,    6f),
        new Knob("brakeYawDamping",      0f,    6f),
        new Knob("yawInertiaFactor",     0.4f,  2f),
        new Knob("highSpeedSteerScale",  0.2f,  1f),
        new Knob("steerDecaySpeedMph",   60f,   300f),
        new Knob("steerExpo",            1f,    3f),
        new Knob("lowSpeedKinematic",    0f,    6f),
        new Knob("cgHeight",             0.2f,  0.9f),
    };

    PlayerVehicleController _pvc;
    readonly Dictionary<string, FieldInfo> _fields = new();
    readonly Dictionary<string, float> _defaults = new();
    readonly Dictionary<string, float> _globalDefaults = new();
    bool _show;
    float _findTimer;
    Rect _win = new Rect(16, 80, 380, 0);
    GUIStyle _head, _telem, _label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HandlingTuner");
        DontDestroyOnLoad(go);
        go.AddComponent<HandlingTuner>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _show = startVisible;
        var t = typeof(PlayerVehicleController);
        foreach (var k in Knobs)
        {
            var fi = t.GetField(k.field, BindingFlags.Public | BindingFlags.Instance);
            if (fi != null) _fields[k.field] = fi;
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (Keyboard.current != null && toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
            _show = !_show;

        if (_pvc == null)
        {
            _findTimer -= Time.unscaledDeltaTime;
            if (_findTimer <= 0f) { _pvc = FindPlayer(); _findTimer = 0.5f; CaptureDefaults(); }
        }
    }

    static PlayerVehicleController FindPlayer()
    {
        var all = Object.FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].enabled && all[i].GetComponent<SplineInputDriver>() == null) return all[i];
        return all.Length > 0 ? all[0] : null;
    }

    void CaptureDefaults()
    {
        if (_pvc == null || _defaults.Count > 0) return;
        foreach (var kv in _fields) _defaults[kv.Key] = (float)kv.Value.GetValue(_pvc);
        _globalDefaults["GripMultiplier"] = TrackConditions.GripMultiplier;
        _globalDefaults["PowerMultiplier"] = TrackConditions.PowerMultiplier;
        _globalDefaults["TireWearMultiplier"] = TrackConditions.TireWearMultiplier;
        _globalDefaults["FuelUseMultiplier"] = TrackConditions.FuelUseMultiplier;
        _globalDefaults["DamageMultiplier"] = TrackConditions.DamageMultiplier;
        _globalDefaults["AiPaceMultiplier"] = TrackConditions.AiPaceMultiplier;
    }

    void OnGUI()
    {
        if (!_show || _pvc == null) return;
        EnsureStyles();
        _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, "Handling Tuner  (F1)");
    }

    void DrawWindow(int id)
    {
        // --- Live telemetry: the readout that tells understeer from oversteer. ---
        float bal = _pvc.HandlingBalanceDeg;          // + = front sliding more = understeer
        string verdict = bal > 2f ? "UNDERSTEER (front wide)"
                       : bal < -2f ? "OVERSTEER (rear loose)"
                       : "neutral";
        Color vc = bal > 2f ? new Color(1f, 0.6f, 0.2f) : bal < -2f ? new Color(1f, 0.4f, 0.4f) : new Color(0.5f, 1f, 0.6f);

        GUILayout.Label($"Speed {_pvc.SpeedMph:0} mph    Yaw {_pvc.YawRateDeg:0.0}/s", _telem);
        GUILayout.Label($"Slip  F {_pvc.SlipFrontDeg:0.0}    R {_pvc.SlipRearDeg:0.0}    bal {bal:+0.0;-0.0}", _telem);
        var prev = _telem.normal.textColor; _telem.normal.textColor = vc;
        GUILayout.Label(verdict, _telem);
        _telem.normal.textColor = prev;

        GUILayout.Space(6);

        // --- Sliders ---
        foreach (var k in Knobs)
        {
            if (!_fields.TryGetValue(k.field, out var fi)) continue;
            float val = (float)fi.GetValue(_pvc);
            GUILayout.BeginHorizontal();
            GUILayout.Label(k.field, _label, GUILayout.Width(165));
            GUILayout.Label(val.ToString("0.###"), _label, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            float nv = GUILayout.HorizontalSlider(val, k.min, k.max);
            if (!Mathf.Approximately(nv, val)) fi.SetValue(_pvc, nv);
        }

        // --- Global track conditions: live multipliers layered on every car. ---
        GUILayout.Space(8);
        GUILayout.Label("Global  (track conditions — ALL cars)", _head);
        TrackConditions.GripMultiplier     = LabeledSlider("GripMultiplier",     TrackConditions.GripMultiplier,     0.5f, 1.5f);
        TrackConditions.PowerMultiplier    = LabeledSlider("PowerMultiplier",    TrackConditions.PowerMultiplier,    0.5f, 1.5f);
        TrackConditions.TireWearMultiplier = LabeledSlider("TireWearMultiplier", TrackConditions.TireWearMultiplier, 0f,   4f);
        TrackConditions.FuelUseMultiplier  = LabeledSlider("FuelUseMultiplier",  TrackConditions.FuelUseMultiplier,  0f,   4f);
        TrackConditions.DamageMultiplier   = LabeledSlider("DamageMultiplier",   TrackConditions.DamageMultiplier,   0f,   4f);
        TrackConditions.AiPaceMultiplier   = LabeledSlider("AiPaceMultiplier",   TrackConditions.AiPaceMultiplier,   0.5f, 1.5f);

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset")) ResetDefaults();
        if (GUILayout.Button("Log values")) LogValues();
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, 10000, 22));
    }

    // A label + value readout + slider row. Returns the (possibly changed) value. Used for the VehicleInfo
    // and global rows where the value lives off the PlayerVehicleController.
    float LabeledSlider(string name, float val, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, _label, GUILayout.Width(165));
        GUILayout.Label(val.ToString("0.###"), _label, GUILayout.Width(50));
        GUILayout.EndHorizontal();
        return GUILayout.HorizontalSlider(val, min, max);
    }

    void ResetDefaults()
    {
        if (_pvc == null) return;
        foreach (var kv in _defaults) if (_fields.TryGetValue(kv.Key, out var fi)) fi.SetValue(_pvc, kv.Value);
        if (_globalDefaults.TryGetValue("GripMultiplier", out var g)) TrackConditions.GripMultiplier = g;
        if (_globalDefaults.TryGetValue("PowerMultiplier", out var p)) TrackConditions.PowerMultiplier = p;
        if (_globalDefaults.TryGetValue("TireWearMultiplier", out var w)) TrackConditions.TireWearMultiplier = w;
        if (_globalDefaults.TryGetValue("FuelUseMultiplier", out var f)) TrackConditions.FuelUseMultiplier = f;
        if (_globalDefaults.TryGetValue("DamageMultiplier", out var d)) TrackConditions.DamageMultiplier = d;
        if (_globalDefaults.TryGetValue("AiPaceMultiplier", out var a)) TrackConditions.AiPaceMultiplier = a;
    }

    void LogValues()
    {
        if (_pvc == null) return;
        var sb = new StringBuilder("[HandlingTuner] current values:\n");
        foreach (var k in Knobs)
            if (_fields.TryGetValue(k.field, out var fi))
                sb.AppendLine($"  {k.field} = {(float)fi.GetValue(_pvc):0.####}");
        sb.AppendLine($"  [Global] GripMultiplier = {TrackConditions.GripMultiplier:0.####}");
        sb.AppendLine($"  [Global] PowerMultiplier = {TrackConditions.PowerMultiplier:0.####}");
        sb.AppendLine($"  [Global] TireWearMultiplier = {TrackConditions.TireWearMultiplier:0.####}");
        sb.AppendLine($"  [Global] FuelUseMultiplier = {TrackConditions.FuelUseMultiplier:0.####}");
        sb.AppendLine($"  [Global] DamageMultiplier = {TrackConditions.DamageMultiplier:0.####}");
        sb.AppendLine($"  [Global] AiPaceMultiplier = {TrackConditions.AiPaceMultiplier:0.####}");
        Debug.Log(sb.ToString());
    }

    void EnsureStyles()
    {
        if (_head != null) return;
        _head = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        _telem = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _telem.normal.textColor = Color.white;
        _label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        _label.normal.textColor = Color.white;
    }
}
