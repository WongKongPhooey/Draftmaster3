using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Draftmaster.Controls;
using Draftmaster.Sim;

// The crew chief's headset icon, bottom right of the HUD (single player). Tapping it drops the player into
// an on-foot crew-chief character at the pit wall. It is the first of the team controls: one square glyph per
// person you can hand the car to, so the corner grows a face rather than another caption when the team gains
// somebody.
//
// Only during the player's own session. Standing on the pit wall calling a race is a job that only exists
// while there is a session to call: outside their hour the paddock is walkable but the car is not theirs to
// take out, so the headset is not on the HUD at all (RaceWeekend.SessionLive, the same gate the TEAM panel
// uses). If the session settles while the player is acting as chief, they are put back the way they came.
//
// Whether the car changes hands depends on where the player was standing. Stepping OUT of the car hands it to
// the AI (via DriveModeController) and climbing back in resumes driving; but a player who was already on foot
// — walking up pit road, stood beside the car after a tow — never got in it, so the car is left exactly as it
// was and their walking body is simply parked until they stop being the chief.
//
// As crew chief the player sees pit-wall telemetry the driver doesn't: fuel load and last-pit lap for the
// whole field. Tap again to go back to whoever they were.
//
// The chief can also watch the cars rather than the pit wall: , and . (d-pad left/right on a pad) step the
// camera through the field in running order and back round to the chief, and clicking a row on the timing
// screen cuts straight to that car. The chief stands still while the camera is away from them.
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
    [Tooltip("While crew chief: point the camera at the car ahead of the watched one in the running order " +
             "(or last place, from the pit wall). Past the leader it comes back to the chief.")]
    public Key watchPrevKey = Key.Comma;
    [Tooltip("While crew chief: point the camera at the next car down the running order (the leader, from " +
             "the pit wall). Past last place it comes back to the chief.")]
    public Key watchNextKey = Key.Period;

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

    // "Is the player on the pit wall right now?", asked from outside. Field-wide HUD notices that belong to
    // the chief rather than the driver (RivalryFeed's rivalry toasts) read this.
    public static bool IsCrewChief { get; private set; }

    bool _active;
    GameObject _avatar;
    GameObject _playerCar;
    Image _face;
    Image _icon;
    GameObject _buttonRoot;
    GameObject _timingBtn;
    Canvas _canvas;
    float _lift;             // UI pixels the corner controls are raised by, to stand clear of the touch pedals
    bool _keyPrev;
    bool _prevHeld, _nextHeld;
    bool _watchLocked;       // this component froze the chief while the camera is away
    Transform _watching;     // the car the camera is on; null = the chief at the pit wall
    Material _unlit;

    // Shown on the timing screen and in the one-off hint, so both teach the same buttons.
    string WatchKeys => $"{KeyName(watchPrevKey)} {KeyName(watchNextKey)}";
    static string WatchPad => InputGlyphs.Pad(PadBindings.WatchPrevCar) + "/" + InputGlyphs.Pad(PadBindings.WatchNextCar);

    // Captured the moment the headset is tapped, because taking the job changes both answers: the crew
    // chief avatar is itself a walking body, so "was the player on foot" cannot be asked again afterwards.
    bool _wasInCar;          // they were sat in the car rather than walking the paddock
    bool _wasDriving;        // ...and had the controls, rather than watching their own car on the TV cycle
    GameObject _parkedBody;  // their walking body, hidden for the duration when they were already on foot

    void Start()
    {
        // Crew chief mode stands the player on the pit wall as a walking body. A single race is driven from
        // inside the car start to finish, so neither the headset button nor its key exists there.
        if (!GameSession.OnFootAllowed) { enabled = false; return; }

        if (driveMode == null) driveMode = FindFirstObjectByType<DriveModeController>();
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
        _playerCar = GameObject.Find("PlayerCar");
        BuildButton();
        UpdateButton();
    }

    // Static state has to die with the scene (and with a component switched off), or the next race starts
    // believing the player is still stood on the pit wall.
    void OnDisable()
    {
        IsCrewChief = false;
    }

    void Update()
    {
        // The job only exists while the player's own session is running. Read every frame rather than at
        // Start: the session ends inside the scene (RaceDirector settles the race) and the paddock stays
        // walkable afterwards, so the headset has to come off the HUD without a reload.
        bool available = Available;
        if (_buttonRoot != null && _buttonRoot.activeSelf != available) _buttonRoot.SetActive(available);
        KeepClearOfTouchPedals();
        if (!available)
        {
            // Chequered flag while stood on the pit wall: put them back in the car (or back on their feet)
            // rather than stranding them as a chief with no session to call.
            if (_active) Exit();
            _keyPrev = Keyboard.current != null && toggleKey != Key.None && Keyboard.current[toggleKey].isPressed;
            return;
        }

        if (Keyboard.current != null && toggleKey != Key.None)
        {
            bool held = Keyboard.current[toggleKey].isPressed;
            if (held && !_keyPrev) Toggle();
            _keyPrev = held;
        }

        // The pad's headset button is a fight's shove, so it stands down while one is on.
        if (!DriverFight.IsActive && PadInput.Pressed(PadBindings.CrewChief)) Toggle();

        if (_active) UpdateWatchInput();

        // Keep the camera glued to the avatar — or the car being watched — while on the pit wall
        // (DriveModeController is told to leave it alone). A watched car that despawns hands it back.
        if (_active && _watchLocked && _watching == null) Watch(null);
        Transform camTarget = _watching != null ? _watching : (_avatar != null ? _avatar.transform : null);
        if (_active && camTarget != null && cameraFollow != null && cameraFollow.target != camTarget)
            cameraFollow.target = camTarget;
    }

    void UpdateWatchInput()
    {
        var kb = Keyboard.current;
        bool prev = kb != null && watchPrevKey != Key.None && kb[watchPrevKey].isPressed;
        bool next = kb != null && watchNextKey != Key.None && kb[watchNextKey].isPressed;
        int dir = 0;
        if (prev && !_prevHeld) dir = -1;
        if (next && !_nextHeld) dir = +1;
        _prevHeld = prev;
        _nextHeld = next;
        if (PadInput.PressedOnFoot(PadBindings.WatchPrevCar)) dir = -1;
        if (PadInput.PressedOnFoot(PadBindings.WatchNextCar)) dir = +1;
        if (dir != 0) StepWatch(dir);
    }

    // One press of , or . — next/previous car in the running order, round through the chief.
    void StepWatch(int dir)
    {
        var rt = RacePositionTracker.Instance;
        var order = rt != null ? rt.Order : null;
        int count = order != null ? order.Count : 0;
        int current = PitWallWatch.Self;
        for (int i = 0; _watching != null && i < count; i++)
            if (order[i] != null && order[i].tf == _watching) { current = i; break; }

        // Skip any empty rows rather than stopping on them.
        int next = current;
        for (int tries = 0; tries <= count; tries++)
        {
            next = PitWallWatch.Step(next, count, dir);
            if (next == PitWallWatch.Self || (order[next] != null && order[next].tf != null)) break;
        }
        Watch(next == PitWallWatch.Self ? null : order[next].tf);
    }

    // Point the pit-wall camera at a car, or back at the chief (null).
    public void Watch(Transform car)
    {
        _watching = car;
        SetLocked(car != null);
        if (cameraFollow == null) return;
        if (car != null) cameraFollow.target = car;
        else if (_avatar != null) cameraFollow.target = _avatar.transform;
    }

    // The chief stays put while the camera is off watching somebody — walking a body nobody can see is how a
    // player cuts back to find the chief stood in the pit lane.
    // Only ever undoes its own lock, so a fight or cutscene that froze the chief is left in charge of that.
    void SetLocked(bool locked)
    {
        if (locked == _watchLocked) return;
        _watchLocked = locked;
        var foot = _avatar != null ? _avatar.GetComponent<OnFootController>() : null;
        if (foot != null) foot.MovementLocked = locked;
    }

    static string KeyName(Key k)
    {
        switch (k)
        {
            case Key.None: return "";
            case Key.Comma: return ",";
            case Key.Period: return ".";
            case Key.LeftBracket: return "[";
            case Key.RightBracket: return "]";
            default: return k.ToString().ToUpperInvariant();
        }
    }

    // Practice, qualifying and the race all count — the timing screen is most of the point of a practice
    // session. What does not count is the rest of the weekend, when no series of the player's is on track.
    public bool Available => RaceWeekend.SessionLive;

    public void Toggle() { if (_active) Exit(); else Enter(); }

    void Enter()
    {
        if (_playerCar == null) _playerCar = GameObject.Find("PlayerCar");

        // Where the player is standing, asked BEFORE the chief's body exists — that body is an
        // OnFootController too, and would answer for them a frame later. No walking body means they are sat
        // in the car; PitLaneStart deactivates it when they climb in and hands it back after a tow.
        var body = OnFootController.Current;
        _wasInCar = body == null;
        // Driving is not the same as being in the car: the TV cycle (V) already gave the AI the wheel, and
        // exiting must not hand it back to a player who chose to watch.
        _wasDriving = _wasInCar && driveMode != null && driveMode.IsDriving;

        if (driveMode != null)
        {
            // Tell broadcast mode not to fight us for the camera either way.
            driveMode.suppressBroadcastCamera = true;
            // The car only changes hands if there was a driver in it to get out. A player still walking up
            // pit road never started it, so handing their parked car to the AI would drive it away from
            // under them.
            if (_wasDriving) driveMode.SetDriving(false);
        }

        // Their walking body stays exactly where it was standing, switched off for the duration, so coming
        // off the headset puts them back on the same patch of tarmac rather than beside the car.
        if (!_wasInCar)
        {
            _parkedBody = body.gameObject;
            _parkedBody.SetActive(false);
        }

        // The driver tracks fuel only as crew chief — make sure their own car has a tank to report.
        if (_playerCar != null && _playerCar.GetComponent<FuelTank>() == null)
            _playerCar.AddComponent<FuelTank>();

        EnsureAvatar();
        _avatar.SetActive(true);
        Watch(null);
        _prevHeld = _nextHeld = true;   // a key already down when the headset goes on is not a press

        _active = true;
        IsCrewChief = true;
        if (_timingBtn != null) _timingBtn.SetActive(true);
        UpdateButton();

        ControlHints.Show("crewchiefwatch", WatchKeys, WatchPad, "Watch the cars on track");
    }

    void Exit()
    {
        Watch(null);
        if (_avatar != null) _avatar.SetActive(false);

        // They come off the pit wall as whoever they were when they got on it.
        if (_parkedBody != null)
        {
            _parkedBody.SetActive(true);
            if (cameraFollow != null) cameraFollow.target = _parkedBody.transform;
            _parkedBody = null;
        }
        else if (driveMode != null)
        {
            // Climb back in only if they got out of a car they were driving — DriveModeController retargets
            // the camera back to it. Someone who handed the wheel to the AI first is left watching.
            if (_wasDriving && !driveMode.IsDriving) driveMode.SetDriving(true);
            else if (cameraFollow != null && _playerCar != null) cameraFollow.target = _playerCar.transform;
        }
        else if (cameraFollow != null && _playerCar != null)
        {
            cameraFollow.target = _playerCar.transform;
        }

        if (driveMode != null) driveMode.suppressBroadcastCamera = false;

        _active = false;
        IsCrewChief = false;
        _wasInCar = false;
        _wasDriving = false;
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
        _canvas = canvas;

        var theme = PixelUITheme.Instance;
        Sprite icon = buttonIcon != null ? buttonIcon : (theme != null ? theme.iconHeadset : null);
        var button = IronOvalUI.IconButton(canvas.transform, "CrewChiefButton", icon, buttonSize);
        var shadow = (RectTransform)button.transform.parent;   // IconButton returns the face; its root is the shadow
        Corner(shadow, buttonCorner);
        button.onClick.AddListener(Toggle);
        _buttonRoot = shadow.gameObject;
        _buttonRoot.SetActive(Available);
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

    // On a phone the on-screen pedals sit in this corner while the player drives; the headset (and TIMING
    // above it) step up to stand on top of them, and drop back when the pedals are put away.
    void KeepClearOfTouchPedals()
    {
        if (_buttonRoot == null || _canvas == null) return;
        float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        float lift = Mathf.Ceil(TouchDriveControls.PedalsTopFromBottom / scale);
        if (lift == _lift) return;
        _lift = lift;

        var corner = new Vector2(buttonCorner.x, buttonCorner.y + lift);
        Corner((RectTransform)_buttonRoot.transform, corner);
        if (_timingBtn != null)
            Corner((RectTransform)_timingBtn.transform, new Vector2(corner.x, corner.y + buttonSize + 6f));
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
        float h = PixelGUI.Px(36f) + (Mathf.Min(order.Count, 45) + 1) * row;
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(40f);

        PixelGUI.Panel(new Rect(x, y, w, h));
        PixelGUI.KeyTab(new Rect(x, y, w, h),
                        toggleKey == UnityEngine.InputSystem.Key.None ? "" : toggleKey.ToString(),
                        PadBindings.CrewChief);
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

            // The car on camera gets the leaderboard's featured-row fill. Each row is a button: click to cut
            // to that car, click it again to come back to the pit wall.
            var rowRect = new Rect(cx, cy, c.width, row);
            if (e.tf == _watching)
                PixelGUI.Fill(rowRect, new Color(PixelGUI.Info.r, PixelGUI.Info.g, PixelGUI.Info.b, 0.30f));
            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                Watch(e.tf == _watching ? null : e.tf);

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

        // How to get at the cars: the only standing reminder, beyond the one-off hint.
        cy += PixelGUI.Px(2f);
        string keys = InputGlyphs.UsingGamepad
            ? InputGlyphs.PadName(PadBindings.WatchPrevCar) + "/" +
              InputGlyphs.PadName(PadBindings.WatchNextCar).Replace("D-PAD ", "")
            : WatchKeys;
        string footer = _watching != null ? $"{keys}  NEXT CAR · CLICK ROW = BACK"
                                          : $"{keys}  WATCH CARS · CLICK A ROW";
        GUI.Label(new Rect(cx, cy, c.width, row), footer, PixelGUI.LabelDim);
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
