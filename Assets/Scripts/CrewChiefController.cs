using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// The crew chief's headset icon, bottom right of the HUD (single player). Tapping it hands the player's
// car to the AI (via DriveModeController) and drops the player into an on-foot crew-chief character at the pit
// wall. It is the first of the team controls: one square glyph per person you can hand the car to, so the
// corner grows a face rather than another caption when the team gains somebody.
//
// As crew chief the player sees pit-wall telemetry the driver doesn't: fuel load and last-pit lap for the
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
    [Tooltip("On-foot player prefab (TaylorEmerson) — the animated pixel character with the walk cycle. If null, it's borrowed from the scene's PitLaneStart; the sprite fallbacks below only apply when neither exists.")]
    public GameObject onFootPrefab;
    [Tooltip("Paper-doll outfit library. Fallback when no on-foot prefab is available.")]
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
    [Tooltip("Side of the square icon button, in 640x360 UI pixels.")]
    public float buttonSize = 32f;
    [Tooltip("Gap from the bottom-right corner to the button, in 640x360 UI pixels. The x default clears " +
             "the speedometer dial, which is anchored to the same corner on its own canvas.")]
    public Vector2 buttonCorner = new Vector2(92f, 12f);
    [Tooltip("Icon on the button. Defaults to the kit's headset glyph.")]
    public Sprite buttonIcon;

    bool _active;
    GameObject _avatar;
    GameObject _playerCar;
    Image _face;
    Image _icon;
    GameObject _timingBtn;
    bool _keyPrev;
    Material _unlit;

    void Start()
    {
        if (driveMode == null) driveMode = FindFirstObjectByType<DriveModeController>();
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
        _playerCar = GameObject.Find("PlayerCar");
        BuildButton();
        UpdateButton();
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
        if (_timingBtn != null) _timingBtn.SetActive(true);
        UpdateButton();
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
        if (_timingBtn != null) _timingBtn.SetActive(false);
        if (TimingScreenUI.Instance != null) TimingScreenUI.Instance.Hide();
        UpdateButton();
    }

    void EnsureAvatar()
    {
        if (_avatar != null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position
                    : (_playerCar != null ? _playerCar.transform.position : transform.position);
        pos.z = -0.1f;

        // Preferred look: the player's on-foot rig (TaylorEmerson) — animated pixel character whose
        // walk cycle OnFootController already drives via the Animator's Horizontal/Vertical/Speed
        // params. Same sanitisation as PitLaneStart.SpawnPlayer. Prefab and controls are borrowed
        // from the scene's PitLaneStart when not wired here, so no scene setup is needed.
        var pit = FindAnyObjectByType<PitLaneStart>();
        GameObject prefab = onFootPrefab != null ? onFootPrefab : (pit != null ? pit.onFootPrefab : null);
        if (controls == null && pit != null) controls = pit.controls;

        if (prefab != null)
        {
            _avatar = Instantiate(prefab, pos, Quaternion.identity);
            _avatar.name = "CrewChief";
            // The prefab is authored at true world scale (its sprite is imported at the project pixel
            // standard, so scale 1 is a 0.625m figure) — avatarScale multiplies it, never replaces it.
            if (avatarScale > 0f && !Mathf.Approximately(avatarScale, 1f))
                _avatar.transform.localScale *= avatarScale;

            // Legacy components depend on RaceManager/InputManager which aren't active here.
            var legacy = _avatar.GetComponent<MovementOnFoot>();
            if (legacy != null) legacy.enabled = false;
            var pi = _avatar.GetComponent<PlayerInput>();
            if (pi != null) pi.enabled = false;

            // 3D URP renderer: Sprite-Lit-Default gets no Light2D and renders black — swap to unlit.
            var sprite = _avatar.GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.sharedMaterial = UnlitSprite();

            var body = _avatar.GetComponent<Rigidbody2D>();
            if (body == null) body = _avatar.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var walk = _avatar.GetComponent<OnFootController>();
            if (walk == null) walk = _avatar.AddComponent<OnFootController>();
            walk.controlsAsset = controls;
            return;
        }

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
        EnsureEventSystem();

        // The team controls sit in the bottom-right corner as square glyphs on the kit's 640x360 canvas.
        // The speedometer dial owns the corner itself, so buttonCorner.x steps in far enough to clear it.
        var canvas = PixelUI.CreateCanvas("TeamControlsCanvas", 111);

        var theme = PixelUITheme.Instance;
        Sprite icon = buttonIcon != null ? buttonIcon : (theme != null ? theme.iconHeadset : null);
        var button = IronOvalUI.IconButton(canvas.transform, "CrewChiefButton", icon, buttonSize);
        var shadow = (RectTransform)button.transform.parent;   // IconButton returns the face; its root is the shadow
        Corner(shadow, buttonCorner);
        button.onClick.AddListener(Toggle);
        _face = button.GetComponent<Image>();
        var glyph = button.transform.Find("Icon");
        _icon = glyph != null ? glyph.GetComponent<Image>() : null;

        // "Timing" sits above the headset — only while acting as crew chief. Opens the full-field timing
        // screen (lap times from LapTimingManager).
        var timing = IronOvalUI.TabButton(canvas.transform, "TimingButton", "TIMING", new Vector2(56f, 16f));
        var timingRoot = (RectTransform)timing.transform.parent;
        Corner(timingRoot, new Vector2(buttonCorner.x, buttonCorner.y + buttonSize + 6f));
        timing.onClick.AddListener(() => TimingScreenUI.Ensure().Toggle());
        _timingBtn = timingRoot.gameObject;
        _timingBtn.SetActive(false);
    }

    // Pin a control to the bottom-right corner, `margin` UI pixels in from it.
    static void Corner(RectTransform rt, Vector2 margin)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-margin.x, margin.y);
    }

    // On duty the plate goes alarm red, the same way the kit marks a selected tab; off duty it is the
    // ordinary panel colour with the glyph dimmed.
    void UpdateButton()
    {
        if (_face != null) _face.color = _active ? PixelGUI.Danger : PixelGUI.PlateDeep;
        if (_icon != null) _icon.color = _active ? PixelGUI.Text : PixelGUI.TextDim;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void OnGUI()
    {
        if (!_active || !showHud) return;
        var rt = RacePositionTracker.Instance;
        if (rt == null) return;

        // The pit-wall timing screen, in the kit's furniture: framed plate, Silkscreen column heads, VT323
        // rows so the six columns line up without measuring anything.
        var order = rt.Order;
        float row = PixelGUI.Px(11f);
        float w = PixelGUI.Px(212f);
        float h = PixelGUI.Px(34f) + Mathf.Min(order.Count, 45) * row;
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(40f);

        PixelGUI.Panel(new Rect(x, y, w, h));
        PixelGUI.KeyTab(new Rect(x, y, w, h),
                        toggleKey == UnityEngine.InputSystem.Key.None ? "" : toggleKey.ToString());
        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 6f);
        float cx = c.x, cy = c.y;

        GUI.Label(new Rect(cx, cy, c.width, PixelGUI.Px(10f)), "CREW CHIEF · PIT WALL", PixelGUI.HeadingSmall);
        cy += PixelGUI.Px(12f);
        GUI.Label(new Rect(cx, cy, c.width, PixelGUI.Px(10f)),
                  $"{"POS",-4}{"#",-5}{"DRIVER",-16}{"LAP",-6}{"PIT",-7}{"FUEL",-6}", PixelGUI.LabelDim);
        cy += PixelGUI.Px(11f);
        PixelGUI.Rule(cx, cy, c.width);
        cy += PixelGUI.Px(3f);

        var style = PixelGUI.Data;
        var prev = style.normal.textColor;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;

            bool isPlayer = _playerCar != null && e.tf == _playerCar.transform;
            style.normal.textColor = isPlayer ? PixelGUI.Gold : PixelGUI.Text;

            var fuel = e.tf.GetComponent<FuelTank>();
            string fuelStr = fuel != null ? $"{Mathf.RoundToInt(fuel.Fraction * 100f)}%" : "--";
            // A car about to run dry is the whole reason this screen exists, so it gets the alarm colour
            // even on a row that is not the player's.
            if (fuel != null && fuel.Fraction < 0.12f) style.normal.textColor = PixelGUI.Danger;

            var hist = e.tf.GetComponent<PitHistory>();
            string pitStr = (hist != null && hist.HasPitted) ? $"L{hist.lastPitLap + 1}" : "—";

            string name = string.IsNullOrEmpty(e.name) ? "?" : (e.name.Length > 14 ? e.name.Substring(0, 14) : e.name);
            GUI.Label(new Rect(cx, cy, c.width, row),
                $"{("P" + e.position),-4}{("#" + e.carNumber),-5}{name,-16}{(e.lap + 1),-6}{pitStr,-7}{fuelStr,-6}", style);
            cy += row;
        }
        style.normal.textColor = prev;
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
