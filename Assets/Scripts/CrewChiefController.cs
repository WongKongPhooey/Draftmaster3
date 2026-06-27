using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// "Crew Chief" toggle that sits next to the Driving On/Off button (single player). Tapping it hands the player's
// car to the AI (via DriveModeController) and drops the player into an on-foot crew-chief character at the pit
// wall. As crew chief the player sees pit-wall telemetry the driver doesn't: fuel load and last-pit lap for the
// whole field. Tap again to climb back in and resume driving.
//
// Camera + car hand-off are reused from DriveModeController; this component just owns the on-foot avatar, the
// camera target while on foot, and the data panel.
public class CrewChiefController : MonoBehaviour
{
    [Header("Refs (auto-found if empty)")]
    public DriveModeController driveMode;
    public CameraFollow cameraFollow;
    [Tooltip("PlayerControl asset (Assets/Input/PlayerControl). Feeds the on-foot avatar's OnFoot/Movement action.")]
    public InputActionAsset controls;

    [Header("Crew chief avatar")]
    [Tooltip("Paper-doll outfit library. If null, a placeholder sprite is used.")]
    public NPCPartLibrary crewLibrary;
    [Tooltip("Fallback single sprite when no library is set.")]
    public Sprite crewChiefSprite;
    [Tooltip("Where the crew chief stands. If null, spawns at the player car's position when first activated.")]
    public Transform spawnPoint;
    public float avatarScale = 1f;

    [Header("Input")]
    public Key toggleKey = Key.C;

    [Header("HUD")]
    [Tooltip("Capacity (litres) assumed for the fuel % when a car has no FuelTank yet.")]
    public bool showHud = true;

    bool _active;
    GameObject _avatar;
    GameObject _playerCar;
    Text _label;
    bool _keyPrev;
    Material _unlit;
    GUIStyle _rowStyle, _headStyle;

    void Start()
    {
        if (driveMode == null) driveMode = FindFirstObjectByType<DriveModeController>();
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
        _playerCar = GameObject.Find("PlayerCar");
        BuildButton();
        UpdateLabel();
    }

    void Update()
    {
        if (Keyboard.current != null && toggleKey != Key.None)
        {
            bool held = Keyboard.current[toggleKey].isPressed;
            if (held && !_keyPrev) Toggle();
            _keyPrev = held;
        }

        // Keep the camera glued to the avatar while on foot (DriveModeController is told to leave it alone).
        if (_active && _avatar != null && cameraFollow != null && cameraFollow.target != _avatar.transform)
            cameraFollow.target = _avatar.transform;
    }

    public void Toggle() { if (_active) Exit(); else Enter(); }

    void Enter()
    {
        if (_playerCar == null) _playerCar = GameObject.Find("PlayerCar");

        // Hand the car to the AI and tell broadcast mode not to fight us for the camera.
        if (driveMode != null)
        {
            driveMode.suppressBroadcastCamera = true;
            if (driveMode.IsDriving) driveMode.SetDriving(false);
        }

        // The driver tracks fuel only as crew chief — make sure their own car has a tank to report.
        if (_playerCar != null && _playerCar.GetComponent<FuelTank>() == null)
            _playerCar.AddComponent<FuelTank>();

        EnsureAvatar();
        _avatar.SetActive(true);
        if (cameraFollow != null) cameraFollow.target = _avatar.transform;

        _active = true;
        UpdateLabel();
    }

    void Exit()
    {
        if (_avatar != null) _avatar.SetActive(false);

        // Resume driving — DriveModeController retargets the camera back to the car.
        if (driveMode != null)
        {
            if (!driveMode.IsDriving) driveMode.SetDriving(true);
            driveMode.suppressBroadcastCamera = false;
        }
        else if (cameraFollow != null && _playerCar != null)
        {
            cameraFollow.target = _playerCar.transform;
        }

        _active = false;
        UpdateLabel();
    }

    void EnsureAvatar()
    {
        if (_avatar != null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position
                    : (_playerCar != null ? _playerCar.transform.position : transform.position);
        pos.z = -0.1f;

        _avatar = new GameObject("CrewChief");
        _avatar.transform.position = pos;
        _avatar.transform.localScale = Vector3.one * avatarScale;

        var rb = _avatar.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // Look: paper-doll if a library is set, else a single sprite / placeholder.
        bool built = false;
        if (crewLibrary != null)
        {
            var layered = _avatar.AddComponent<NPCLayeredAppearance>();
            layered.library = crewLibrary;
            layered.layerMaterial = UnlitSprite();
            layered.sortingLayerName = "Vehicles";
            layered.baseSortingOrder = 30;
            built = layered.Build();
            if (!built) Destroy(layered);
        }
        if (!built)
        {
            var sr = _avatar.AddComponent<SpriteRenderer>();
            sr.sprite = crewChiefSprite != null ? crewChiefSprite : Placeholder(new Color(0.95f, 0.8f, 0.2f));
            sr.sharedMaterial = UnlitSprite();
            sr.sortingLayerName = "Vehicles";
            sr.sortingOrder = 30;
            _avatar.transform.localScale = Vector3.one * (avatarScale > 0f ? avatarScale : 1f);
        }

        var ofc = _avatar.AddComponent<OnFootController>();
        ofc.controlsAsset = controls;     // built lazily after assignment
    }

    // ---- UI ----

    void BuildButton()
    {
        var canvasGO = new GameObject("CrewChiefCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 111;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var btnGO = new GameObject("CrewChiefToggle", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasGO.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(250f, -20f);   // just right of the DriveToggle (20 + 220 + 10)
        rt.sizeDelta = new Vector2(200f, 48f);

        btnGO.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        btnGO.GetComponent<Button>().onClick.AddListener(Toggle);

        var txtGO = new GameObject("Label", typeof(RectTransform));
        txtGO.transform.SetParent(btnGO.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        _label = txtGO.AddComponent<Text>();
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.fontSize = 18;
        _label.fontStyle = FontStyle.Bold;
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void UpdateLabel()
    {
        if (_label != null) _label.text = _active ? "Crew Chief: ON" : "Crew Chief";
    }

    void OnGUI()
    {
        if (!_active || !showHud) return;
        var rt = RacePositionTracker.Instance;
        if (rt == null) return;

        if (_headStyle == null)
        {
            _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _headStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _rowStyle.normal.textColor = Color.white;
        }

        var order = rt.Order;
        float w = 430f;
        float h = 40f + Mathf.Min(order.Count, 45) * 18f;
        float x = Screen.width - w - 16f;
        float y = 80f;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x - 8f, y - 8f, w + 16f, h + 16f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, w, 20f), "CREW CHIEF  —  PIT WALL", _headStyle);
        y += 22f;
        GUI.Label(new Rect(x, y, w, 18f), $"{"Pos",-4}{"#",-5}{"Driver",-16}{"Lap",-6}{"LastPit",-9}{"Fuel",-6}", _rowStyle);
        y += 18f;

        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;

            bool isPlayer = _playerCar != null && e.tf == _playerCar.transform;
            _rowStyle.normal.textColor = isPlayer ? new Color(0.4f, 1f, 0.5f) : Color.white;

            var fuel = e.tf.GetComponent<FuelTank>();
            string fuelStr = fuel != null ? $"{Mathf.RoundToInt(fuel.Fraction * 100f)}%" : "--";

            var hist = e.tf.GetComponent<PitHistory>();
            string pitStr = (hist != null && hist.HasPitted) ? $"L{hist.lastPitLap + 1}" : "—";

            string name = string.IsNullOrEmpty(e.name) ? "?" : (e.name.Length > 14 ? e.name.Substring(0, 14) : e.name);
            GUI.Label(new Rect(x, y, w, 18f),
                $"{("P" + e.position),-4}{("#" + e.carNumber),-5}{name,-16}{(e.lap + 1),-6}{pitStr,-9}{fuelStr,-6}", _rowStyle);
            y += 18f;
        }
    }

    Material UnlitSprite()
    {
        if (_unlit != null) return _unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlit = new Material(sh);
        return _unlit;
    }

    static Sprite Placeholder(Color color)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int yy = 0; yy < s; yy++)
            for (int xx = 0; xx < s; xx++)
                px[yy * s + xx] = Vector2.Distance(new Vector2(xx, yy), c) < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
}
