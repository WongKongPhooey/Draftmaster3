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
    float _findTimer;      // seconds until the next look for the player's car
    PitLimiter _limiter;
    Canvas _canvas;

    void Awake()
    {
        ResolveRefs();
        _canvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        // A car that has been switched off is not a car to read: the scene's PlayerCar sits in its box with
        // its controller disabled for the whole walk up pit road, and a target latched onto it would hold
        // the gauge open over the paddock.
        if (target != null && !target.isActiveAndEnabled) target = null;

        // Retry on a timer, not every frame: the named-object half of the search is a GameObject.Find,
        // which walks the scene, and it finds nothing at all while the player is on foot.
        if (target == null)
        {
            _findTimer -= Time.unscaledDeltaTime;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.5f;
                target = FindPlayer();
            }
        }

        // The dial is a driving instrument. With nobody in a car it is a needle parked at zero over the
        // paddock — furniture from another screen — so the gauge shows itself only while there is a car to
        // read. In the car the Iron Oval HUD usually stands this whole canvas down and draws the speed its
        // own way; this is what covers the walk, before that ever happens.
        ShowGauge(target != null);

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

    // Hide by switching the Canvas off rather than the GameObject: this component has to keep ticking to
    // notice the player getting into a car, and a deactivated object never runs again.
    void ShowGauge(bool visible)
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;
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
        return PlayerVehicleController.Human;
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
