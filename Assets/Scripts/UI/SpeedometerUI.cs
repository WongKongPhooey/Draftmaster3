using UnityEngine;
using UnityEngine.UI;

// Thin binder for the authored speedometer Canvas (Assets/Prefabs/UI/SpeedometerHUD.prefab).
//
// The dial, needle, hub and labels are authored in the editor so the gauge is visible without Play mode. This
// script only reads the player's speed each frame, eases the needle toward it, and writes the mph readout.
public class SpeedometerUI : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player car GameObject (any component implementing IVehicleSpeedReadout). If blank, auto-finds GameObject named playerObjectName.")]
    public MonoBehaviour target;
    [Tooltip("Scene GameObject name used to auto-find the player car when target is blank.")]
    public string playerObjectName = "PlayerCar";

    [Header("Display")]
    [Tooltip("Top of the displayed speed range, in mph. Needle reaches max angle at this speed.")]
    public float maxMph = 220f;
    [Tooltip("Needle angle (degrees) at 0 mph. Positive = CCW.")]
    public float minNeedleAngle = 135f;
    [Tooltip("Needle angle (degrees) at maxMph.")]
    public float maxNeedleAngle = -135f;
    [Tooltip("How quickly the needle catches up to actual speed. Higher = snappier.")]
    public float needleResponse = 8f;

    [Header("Authored children (auto-wired in editor)")]
    public RectTransform needle;
    public Text speedText;
    [Tooltip("Optional pit-limiter chip under the dial. Shows the limit while the limiter is armed and warns when the driver speeds in the lane with it off.")]
    public Text limiterText;

    [Header("Pit limiter chip")]
    public Color limiterArmedColor = new Color(0.35f, 0.85f, 1f);
    public Color limiterSpeedingColor = new Color(1f, 0.35f, 0.25f);
    public Color limiterOffColor = new Color(1f, 0.8f, 0.25f);

    float _displayedMph;
    PitLimiter _limiter;

    void Awake() => ResolveRefs();

    void Update()
    {
        if (target == null) target = FindPlayer();
        if (target == null || needle == null) return;
        var readout = target as IVehicleSpeedReadout;
        if (readout == null) return;

        float mph = readout.SpeedMps * 2.237f;
        _displayedMph = Mathf.Lerp(_displayedMph, mph, 1f - Mathf.Exp(-needleResponse * Time.deltaTime));

        float t = Mathf.Clamp01(_displayedMph / Mathf.Max(maxMph, 1f));
        needle.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t));
        if (speedText != null) speedText.text = Mathf.RoundToInt(_displayedMph).ToString();

        UpdateLimiterChip();
    }

    // Pit-limiter state, on the dial where the driver is already looking. Blank outside the pit lane.
    void UpdateLimiterChip()
    {
        if (limiterText == null) return;

        if (_limiter == null || _limiter.gameObject != target.gameObject)
            _limiter = target.GetComponent<PitLimiter>();

        if (_limiter == null || !_limiter.InPitZone) { limiterText.text = ""; return; }

        if (_limiter.Speeding)
        {
            limiterText.text = $"SPEEDING — LIMIT {Mathf.RoundToInt(_limiter.LimitMph)}";
            limiterText.color = limiterSpeedingColor;
        }
        else if (_limiter.Armed)
        {
            limiterText.text = $"PIT LIMITER  {Mathf.RoundToInt(_limiter.LimitMph)}";
            limiterText.color = limiterArmedColor;
        }
        else
        {
            limiterText.text = $"LIMITER OFF — LIMIT {Mathf.RoundToInt(_limiter.LimitMph)}";
            limiterText.color = limiterOffColor;
        }
    }

    MonoBehaviour FindPlayer()
    {
        // Single-player: the named scene car (GameObject.Find skips inactive objects).
        if (!string.IsNullOrEmpty(playerObjectName))
        {
            var go = GameObject.Find(playerObjectName);
            if (go != null)
            {
                var pvc = go.GetComponent<PlayerVehicleController>();
                if (pvc != null && pvc.enabled) return pvc;
                var components = go.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                    if (components[i] is IVehicleSpeedReadout && components[i].enabled) return components[i];
            }
        }

        // Multiplayer: the local player drives a networked car, not the disabled scene "PlayerCar". The owned
        // car is the only enabled PlayerVehicleController with no AI SplineInputDriver (remote cars are disabled;
        // AI carry a SplineInputDriver).
        var all = FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].enabled && all[i].GetComponent<SplineInputDriver>() == null) return all[i];

        return null;
    }

    // Locate the authored gauge parts. Runtime fallback + editor baking.
    void ResolveRefs()
    {
        if (needle == null)
        {
            var t = transform.Find("Dial/Needle");
            if (t != null) needle = t as RectTransform;
        }
        if (speedText == null)
        {
            var t = transform.Find("Dial/SpeedText");
            if (t != null) speedText = t.GetComponent<Text>();
        }
        if (limiterText == null)
        {
            var t = transform.Find("Dial/LimiterText");
            if (t != null) limiterText = t.GetComponent<Text>();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) ResolveRefs();
    }
#endif
}
