using UnityEngine;
using UnityEngine.UI;

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

    [Header("References (auto-built if left blank)")]
    public RectTransform needle;
    public Text speedText;
    public Image dial;

    [Header("Auto-Build")]
    [Tooltip("If true and no needle is assigned, a default speedometer UI is generated at runtime.")]
    public bool autoCreate = true;
    public float dialSize = 220f;
    public Color dialColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
    public Color needleColor = new Color(1f, 0.25f, 0.25f, 1f);
    public Color textColor = Color.white;

    float _displayedMph;

    void Start()
    {
        if (autoCreate && needle == null) BuildDefaultUI();
    }

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
    }

    MonoBehaviour FindPlayer()
    {
        if (string.IsNullOrEmpty(playerObjectName)) return null;
        var go = GameObject.Find(playerObjectName);
        if (go == null) return null;
        // Prefer PlayerVehicleController if present; else any enabled IVehicleSpeedReadout.
        var pvc = go.GetComponent<PlayerVehicleController>();
        if (pvc != null && pvc.enabled) return pvc;
        var components = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is IVehicleSpeedReadout && components[i].enabled) return components[i];
        }
        return null;
    }

    void BuildDefaultUI()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        var container = new GameObject("Speedometer", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(transform, false);
        container.anchorMin = new Vector2(1f, 0f);
        container.anchorMax = new Vector2(1f, 0f);
        container.pivot = new Vector2(1f, 0f);
        container.anchoredPosition = new Vector2(-30f, 30f);
        container.sizeDelta = new Vector2(dialSize, dialSize);

        var knobSprite = MakeCircleSprite(128);
        var uiSprite = MakeQuadSprite();

        dial = NewImage("Dial", container, knobSprite, dialColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var needleImg = NewImage("Needle", container, uiSprite, needleColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        needle = needleImg.rectTransform;
        needle.pivot = new Vector2(0.5f, 0.05f);
        needle.sizeDelta = new Vector2(6f, dialSize * 0.42f);
        needle.anchoredPosition = Vector2.zero;

        var hub = NewImage("Hub", container, knobSprite, new Color(0.2f, 0.2f, 0.2f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hub.rectTransform.sizeDelta = new Vector2(dialSize * 0.14f, dialSize * 0.14f);

        var textGO = new GameObject("SpeedText", typeof(RectTransform));
        textGO.transform.SetParent(container, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.22f);
        textRT.anchorMax = new Vector2(0.5f, 0.22f);
        textRT.pivot = new Vector2(0.5f, 0.5f);
        textRT.sizeDelta = new Vector2(dialSize * 0.7f, 44f);
        textRT.anchoredPosition = Vector2.zero;
        speedText = textGO.AddComponent<Text>();
        speedText.alignment = TextAnchor.MiddleCenter;
        speedText.color = textColor;
        speedText.fontSize = 24;
        speedText.fontStyle = FontStyle.Bold;
        speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speedText.text = "0";

        var unitGO = new GameObject("UnitLabel", typeof(RectTransform));
        unitGO.transform.SetParent(container, false);
        var unitRT = unitGO.GetComponent<RectTransform>();
        unitRT.anchorMin = new Vector2(0.5f, 0.08f);
        unitRT.anchorMax = new Vector2(0.5f, 0.08f);
        unitRT.pivot = new Vector2(0.5f, 0.5f);
        unitRT.sizeDelta = new Vector2(dialSize * 0.5f, 22f);
        unitRT.anchoredPosition = Vector2.zero;
        var unitText = unitGO.AddComponent<Text>();
        unitText.alignment = TextAnchor.MiddleCenter;
        unitText.color = new Color(textColor.r, textColor.g, textColor.b, 0.7f);
        unitText.fontSize = Mathf.RoundToInt(dialSize * 0.09f);
        unitText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        unitText.text = "MPH";
    }

    static Sprite _cachedCircle;
    static Sprite _cachedQuad;

    static Sprite MakeCircleSprite(int size)
    {
        if (_cachedCircle != null) return _cachedCircle;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SpeedometerCircle" };
        var pixels = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _cachedCircle;
    }

    static Sprite MakeQuadSprite()
    {
        if (_cachedQuad != null) return _cachedQuad;
        var tex = Texture2D.whiteTexture;
        _cachedQuad = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return _cachedQuad;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return img;
    }
}
