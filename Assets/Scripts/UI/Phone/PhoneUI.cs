using System.Collections.Generic;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's phone. Slides up from the bottom of the screen while on foot and shows a home screen of
// app tiles — Schedule, Tasks, Notes, SoBuzz, Messages, Stats — each of which draws its own content (PhoneApp).
//
// Why a phone rather than another F-key panel: everything on it is stuff the driver would actually look
// up between sessions, and it keeps the on-foot half of the game from needing a menu screen. It is not a
// pause — the paddock keeps moving behind it — but the player stops walking while it's up.
//
// Six tiles, two by three, all filled; a spare bay would be drawn empty so the grid never reflows.
// Self-bootstraps like RacePauseMenu / DriverInfoPanel, arms itself only in scenes that have an on-foot
// player, and draws with the Iron Oval kit (PixelGUI).
//
// It sits over on the left of the screen and is held at a slight angle, drawn through one GUI.matrix
// about the bottom of the device — so every rect inside is authored square and the tilt costs nothing.
// Type is PhoneStyles, a step down from the kit's own: a panel-sized glyph in a phone-sized row is what
// made this thing read as squashed.
public class PhoneUI : MonoBehaviour
{
    public static PhoneUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._open;
    // Going off in the player's pocket and holding them still until they take it out (Summon).
    public static bool Summoned => Instance != null && Instance._summoned;

    [Tooltip("Opens and closes the phone while on foot.")]
    public Key toggleKey = Key.P;
    [Tooltip("The pad's View / Create button (Gamepad.selectButton) opens and closes the phone too. Without " +
             "it a pad player has no way to take the phone out — or to put it away again.")]
    public bool padToggle = true;
    [Tooltip("Seconds the phone takes to slide in or out.")]
    public float slideSeconds = 0.22f;
    [Tooltip("Phone body size in UI pixels, before PixelGUI.Scale.")]
    public Vector2 bodySize = new Vector2(216f, 340f);
    [Tooltip("Gap under the phone when it's all the way up, in UI pixels.")]
    public float restGap = 6f;
    [Tooltip("Where the phone's left edge sits, as a fraction of screen width.")]
    public float screenAnchorX = 0.20f;
    [Tooltip("Handheld tilt in degrees. Positive leans the top of the phone to the right.")]
    public float tiltDegrees = 3f;

    // Tiles the home screen has room for. Layout is 2 columns; three rows of two.
    public const int TileSlots = 6;

    readonly List<PhoneApp> _apps = new();
    PhoneApp _current;                 // null = home screen
    int _homeIndex;                    // keyboard selection on the home grid
    string _selectNext;                // app to highlight when the phone next opens (SelectOnNextOpen)
    Vector2 _scroll;

    bool _open;
    float _slide;                      // 0 = off screen, 1 = resting
    OnFootController _player;
    bool _lockedByPhone;               // we set MovementLocked, so we're the one who clears it
    bool _summoned;                    // that lock is a summons: the phone is going off and not out yet
    float _pollTimer;

    const string LastAppKey = "phone.lastapp";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("PhoneUI");
        DontDestroyOnLoad(go);
        go.AddComponent<PhoneUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildApps();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        ReleasePlayer();
        Instance = null;
    }

    void BuildApps()
    {
        _apps.Clear();
        Register(new PhoneScheduleApp());
        Register(new PhoneTasksApp());
        Register(new PhoneNotesApp());
        Register(new PhoneSoBuzzApp());
        // DrivR's old bay. The form guide itself moved in with POINTS, under STATS.
        Register(new PhoneMessagesApp());
        Register(new PhoneStatsApp());
    }

    // Later apps hook in here rather than editing the home screen. Extra apps past the six slots are
    // kept but not reachable from the grid, which is a loud enough failure to notice in a test.
    public static void Register(PhoneApp app)
    {
        if (Instance == null || app == null) return;
        for (int i = 0; i < Instance._apps.Count; i++)
            if (Instance._apps[i].Id == app.Id) return;
        Instance._apps.Add(app);
    }

    public static void Open(string appId = null)
    {
        if (Instance == null) return;
        Instance.OpenInternal(appId);
    }

    public static void Close() { if (Instance != null) Instance.CloseInternal(); }

    // Put the home screen's highlight on this app the next time the phone comes up on its home screen —
    // what a text arriving does, so the player who presses the key is already sat on MESSAGES.
    public static void SelectOnNextOpen(string appId)
    {
        if (Instance != null) Instance._selectNext = appId;
    }

    // The phone is going off: hold the player where they stand until they take it out. The phone owns that
    // lock, which is what lets the toggle open it over the top — anything else holding MovementLocked keeps
    // the phone shut. Opening it turns the summons into the ordinary open-phone lock; closing it lets them go.
    // False when there is nobody on foot, or somebody else already has them.
    public static bool Summon() => Instance != null && Instance.SummonInternal();

    // Let a summoned player go without the phone ever coming out.
    public static void CancelSummon()
    {
        if (Instance == null || !Instance._summoned) return;
        Instance.ReleasePlayer();
    }

    // Scroll the open app back to its top. PhoneApp.ScrollToTop is the way in.
    public static void ResetScroll()
    {
        if (Instance != null) Instance._scroll = Vector2.zero;
    }

    // ------------------------------------------------------------------ state

    void Update()
    {
        // The phone is the career's own device — contacts, deals, tasks, all of it the host's. A co-op guest
        // is a driver in the host's weekend, not the owner of that career, so they do not carry it.
        if (Coop.IsGuest) return;

        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer = 0.5f;
            CountBadges();
            var found = OnFootController.Current;
            if (found != _player)
            {
                // The old body is gone (scene change, got in the car) — never leave it locked.
                ReleasePlayer();
                _player = found;
                if (_open && _player == null) CloseInternal();
            }
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, slideSeconds);
        _slide = Mathf.MoveTowards(_slide, _open ? 1f : 0f, step);

        ReadTouches();

        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool toggle = (kb != null && toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame)
                   || (padToggle && PadInput.WasPressed(PadBindings.Phone))
                   || TouchButtonPressed();
        if (toggle)
        {
            if (_open) CloseInternal();
            else OpenInternal(null);
        }

        if (!_open) return;

        // Esc (the pad's back button) backs out one level: app → home → away. RacePauseMenu stands down while
        // the phone is up.
        bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                 || PadInput.WasPressed(PadBindings.Back);
        if (back)
        {
            PadInput.Consume();
            if (_current != null) { _current = null; _scroll = Vector2.zero; }
            else CloseInternal();
            return;
        }

        if (_current == null) { HomeKeys(kb); return; }

        if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
            _scroll.y = Mathf.Max(0f, _scroll.y + (kb.downArrowKey.wasPressedThisFrame ? PixelGUI.Px(24f) : -PixelGUI.Px(24f)));
        int padScroll = PadInput.VerticalStep();
        if (padScroll != 0) _scroll.y = Mathf.Max(0f, _scroll.y + padScroll * PixelGUI.Px(24f));
        // Either can close the phone (TASKS' travel), so re-check between them.
        if (kb != null) _current.HandleKeys(kb);
        if (pad != null && _current != null) _current.HandlePad(pad);
    }

    void HomeKeys(Keyboard kb)
    {
        int col = _homeIndex % 2, rowIdx = _homeIndex / 2;
        int padX = PadInput.HorizontalStep(), padY = PadInput.VerticalStep();
        bool right = padX > 0, left = padX < 0, down = padY > 0, up = padY < 0;
        bool open = PadInput.WasPressed(PadBindings.Confirm);
        if (kb != null)
        {
            right |= kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            left |= kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            down |= kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            up |= kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            open |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
        }
        if (right) col = 1;
        if (left) col = 0;
        if (down) rowIdx = Mathf.Min(2, rowIdx + 1);
        if (up) rowIdx = Mathf.Max(0, rowIdx - 1);
        _homeIndex = Mathf.Clamp(rowIdx * 2 + col, 0, TileSlots - 1);

        if (open) OpenApp(_homeIndex);
    }

    void OpenInternal(string appId)
    {
        if (_player == null) _player = OnFootController.Current;
        if (_player == null) return;                       // in the car, or a scene with no on-foot body
        if (!_open && _player.MovementLocked && !_summoned) return;   // a conversation or cutscene has the player

        _open = true;
        _summoned = false;
        _scroll = Vector2.zero;
        _current = null;
        if (string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(_selectNext))
        {
            int slot = IndexOf(_selectNext);
            if (slot >= 0 && slot < TileSlots) _homeIndex = slot;
        }
        _selectNext = null;
        if (!string.IsNullOrEmpty(appId))
        {
            for (int i = 0; i < _apps.Count; i++)
                if (_apps[i].Id == appId) { _current = _apps[i]; _homeIndex = i; break; }
        }
        if (_current != null) _current.OnOpen();

        _player.MovementLocked = true;
        _lockedByPhone = true;
    }

    void CloseInternal()
    {
        if (!_open) return;
        _open = false;
        if (_current != null) PlayerPrefs.SetString(LastAppKey, _current.Id);
        _current = null;
        ReleasePlayer();
    }

    void ReleasePlayer()
    {
        if (_lockedByPhone && _player != null) _player.MovementLocked = false;
        _lockedByPhone = false;
        _summoned = false;
    }

    bool SummonInternal()
    {
        if (Coop.IsGuest) return false;
        if (_open) return true;                            // already out; nothing to hold them for

        // Asked fresh rather than trusted from the half-second poll: locking a body that has just been swapped
        // out would hold nobody, and leave the lock behind on it.
        var found = OnFootController.Current;
        if (found != _player) { ReleasePlayer(); _player = found; }
        if (_player == null || _player.MovementLocked) return false;

        _player.MovementLocked = true;
        _lockedByPhone = true;
        _summoned = true;
        return true;
    }

    int IndexOf(string appId)
    {
        for (int i = 0; i < _apps.Count; i++)
            if (_apps[i].Id == appId) return i;
        return -1;
    }

    void OpenApp(int slot)
    {
        if (slot < 0 || slot >= _apps.Count) return;       // an empty bay
        _current = _apps[slot];
        _scroll = Vector2.zero;
        _current.OnOpen();
    }

    // ------------------------------------------------------------------ drawing

    // ------------------------------------------------------------------ touch button

    // A phone player has no P key and no View button, so the phone gets a button of its own in the bottom-left
    // corner — the spot TouchWalkLayout keeps clear of the stick. It is the way out as well as the way in.
    static bool TouchButtonShows(OnFootController body)
    {
        if (!TouchDriveControls.TouchPlatform || InputGlyphs.UsingGamepad) return false;
        if (body == null || body.RemotePuppet) return false;
        if (IsOpen || Summoned) return true;
        if (RacePauseMenu.IsPaused || WeekendScheduleUI.IsOpen || WeekendModal.AnyOpen || DialogueChoiceUI.IsOpen)
            return false;
        // Something else has the player (a conversation, a cutscene): the phone would refuse to open anyway.
        return !body.MovementLocked && !NPCInteractable.AnyConversationActive;
    }

    void DrawTouchButton()
    {
        if (Coop.IsGuest) return;
        var body = _player != null ? _player : OnFootController.Current;
        if (!TouchButtonShows(body)) return;

        var l = TouchWalkControls.CurrentLayout().phoneButton;
        var r = new Rect(l.x, l.y, l.width, l.height);

        // Summoned: the phone is going off in the pocket, so the button pulses gold until it comes out.
        bool ringing = Summoned;
        float pulse = ringing ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
        Color edge = IsOpen || ringing ? Color.Lerp(PixelGUI.Text, PixelGUI.Gold, IsOpen ? 1f : pulse) : PixelGUI.Text;

        PixelGUI.Fill(r, new Color(PixelGUI.PlateDeep.r, PixelGUI.PlateDeep.g, PixelGUI.PlateDeep.b, 0.8f));
        PixelGUI.Frame(r, edge);

        // A little handset: case, lit glass, earpiece and home bar — the same marks the big one is drawn with.
        float bw = r.width * 0.42f, bh = r.height * 0.7f;
        var handset = new Rect(r.center.x - bw * 0.5f, r.center.y - bh * 0.5f, bw, bh);
        PixelGUI.Fill(handset, edge);
        float b = Mathf.Max(1f, PixelGUI.Px(1f));
        var glass = new Rect(handset.x + b, handset.y + b * 3f, handset.width - b * 2f, handset.height - b * 6f);
        PixelGUI.Fill(glass, IsOpen ? PixelGUI.Gold : PixelGUI.ScreenBase);
        PixelGUI.Fill(new Rect(handset.center.x - handset.width * 0.2f, handset.y + b, handset.width * 0.4f, b), PixelGUI.PlateDeep);
        PixelGUI.Fill(new Rect(handset.center.x - handset.width * 0.25f, handset.yMax - b * 2f, handset.width * 0.5f, b), PixelGUI.PlateDeep);

        // Anything waiting: unread messages, a quest to hand in.
        if (!IsOpen && _badgeTotal > 0)
        {
            float d = r.width * 0.34f;
            var dot = new Rect(r.xMax - d * 0.75f, r.y - d * 0.25f, d, d);
            PixelGUI.Fill(dot, PixelGUI.Danger);
            PhoneStyles.Label(dot, _badgeTotal > 9 ? "9+" : _badgeTotal.ToString(), PhoneStyles.Footer,
                              PixelGUI.Text, TextAnchor.MiddleCenter);
        }

        // The press itself is read in Update, straight off the Input System (TouchButtonPressed) — the same
        // source the walk stick uses. IMGUI only gets a touch if the platform turns it into a mouse event,
        // which the Device Simulator does not do, and a button that only works on some devices is no button.
        // A mouse click landing here is still eaten, so nothing drawn underneath hears it as well.
        var e = Event.current;
        if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseUp) && r.Contains(e.mousePosition))
            e.Use();
    }

    // A finger or the mouse came down on the phone button this frame. Worked in IMGUI's space (top-left
    // origin), the space TouchWalkLayout is measured in.
    bool TouchButtonPressed()
    {
        if (Coop.IsGuest) return false;
        var body = _player != null ? _player : OnFootController.Current;
        if (!TouchButtonShows(body)) return false;

        Vector2? at = null;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            at = touch.primaryTouch.position.ReadValue();
        var mouse = Mouse.current;
        if (at == null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            at = mouse.position.ReadValue();
        if (at == null) return false;

        var l = TouchWalkControls.CurrentLayout().phoneButton;
        float y = UnityEngine.Device.Screen.height - at.Value.y;
        return l.Contains(at.Value.x, y);
    }

    // ------------------------------------------------------------------ touches on the device

    // Taps come from TouchTaps, like every other IMGUI screen's. The phone adds the one thing a phone has
    // that a menu does not: dragging a finger up the glass scrolls the open app, and the content follows it.
    void ReadTouches()
    {
        if (!_open || _current == null || !TouchTaps.Dragging) return;
        _scroll.y = Mathf.Max(0f, _scroll.y + TouchTaps.DragDelta.y);   // DrawApp clamps the far end
    }

    // True once for a click or a tap on `r`, given in whatever GUI space is current — the device's tilted
    // matrix and the app's scroll group included.
    public static bool Pressed(Rect r)
    {
        var ui = Instance;
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (!TouchTaps.Driven) return clicked;
        // A row scrolled up under the title bar is still laid out there; only what is visible is tappable.
        return TouchTaps.Hit(r, ui != null ? ui._clip : null);
    }

    Rect? _clip;                       // the app's visible window, in its scroll group's space, while drawing it

    int _badgeTotal;

    void CountBadges()
    {
        int n = 0;
        foreach (var app in _apps) n += Mathf.Max(0, app.Badge);
        _badgeTotal = n;
    }

    void OnGUI()
    {
        // First, so the open device is painted over it.
        DrawTouchButton();

        if (_slide <= 0.001f) return;

        // Over every other IMGUI panel: the phone is held up in front of everything else.
        int prevDepth = GUI.depth;
        GUI.depth = -50;
        var prevMatrix = GUI.matrix;

        float w = PixelGUI.Px(bodySize.x), h = PixelGUI.Px(bodySize.y);

        // The pivot is the bottom centre of the device — the hand. Sliding moves it, the tilt turns about
        // it, and the body is then just a rect hanging above the origin.
        float rest = Screen.height - PixelGUI.Px(restGap);
        float pivotY = Mathf.Round(Mathf.Lerp(Screen.height + h, rest, Ease(_slide)));
        float pivotX = Mathf.Round(Screen.width * Mathf.Clamp01(screenAnchorX) + w * 0.5f);

        GUI.matrix = Matrix4x4.TRS(new Vector3(pivotX, pivotY, 0f),
                                   Quaternion.Euler(0f, 0f, tiltDegrees), Vector3.one);
        DrawBody(new Rect(-w * 0.5f, -h, w, h));

        GUI.matrix = prevMatrix;
        GUI.depth = prevDepth;
    }

    // Ease-out: the phone arrives fast and settles, rather than sliding linearly like a menu.
    static float Ease(float t) => 1f - (1f - t) * (1f - t);

    void DrawBody(Rect body)
    {
        // Case, then the glass. The case is the kit's deepest plate so the screen reads as lit.
        PixelGUI.Fill(new Rect(body.x + PixelGUI.Px(2f), body.y + PixelGUI.Px(2f), body.width, body.height),
                      new Color(0f, 0f, 0f, 0.45f));
        PixelGUI.Fill(body, PixelGUI.Ink);
        PixelGUI.Fill(new Rect(body.x + PixelGUI.Px(1f), body.y + PixelGUI.Px(1f),
                               body.width - PixelGUI.Px(2f), body.height - PixelGUI.Px(2f)), PixelGUI.PlateLight);

        float bezel = PixelGUI.Px(5f);
        var screen = new Rect(body.x + bezel, body.y + PixelGUI.Px(10f),
                              body.width - bezel * 2f, body.height - PixelGUI.Px(10f) - PixelGUI.Px(14f));
        PixelGUI.Fill(screen, PixelGUI.ScreenBase);

        var content = new Rect(screen.x + PixelGUI.Px(3f), screen.y + PixelGUI.Px(11f),
                               screen.width - PixelGUI.Px(6f), screen.height - PixelGUI.Px(14f));

        if (_current == null) DrawHome(content);
        else DrawApp(content);

        // Chrome goes on last. A tilted GUI.matrix makes IMGUI's clipping approximate — a rotated clip
        // rect is enforced as its axis-aligned bounds — so whatever scrolled past the edge of the screen
        // is covered by the case rather than trusted to have been clipped.
        DrawCase(body, screen);
        DrawStatusBar(new Rect(screen.x, screen.y, screen.width, PixelGUI.Px(10f)));
    }

    // The bezel, redrawn over the content, plus the two marks that say "phone" without any art.
    void DrawCase(Rect body, Rect screen)
    {
        float e = PixelGUI.Px(1f);
        var inner = new Rect(body.x + e, body.y + e, body.width - e * 2f, body.height - e * 2f);
        var c = PixelGUI.PlateLight;
        PixelGUI.Fill(new Rect(inner.x, inner.y, inner.width, screen.y - inner.y), c);
        PixelGUI.Fill(new Rect(inner.x, screen.yMax, inner.width, inner.yMax - screen.yMax), c);
        PixelGUI.Fill(new Rect(inner.x, screen.y, screen.x - inner.x, screen.height), c);
        PixelGUI.Fill(new Rect(screen.xMax, screen.y, inner.xMax - screen.xMax, screen.height), c);

        float earW = body.width * 0.22f;
        PixelGUI.Fill(new Rect(body.center.x - earW * 0.5f, body.y + PixelGUI.Px(4f), earW, PixelGUI.Px(2f)),
                      PixelGUI.PlateDeep);
        float barW = body.width * 0.34f;
        PixelGUI.Fill(new Rect(body.center.x - barW * 0.5f, body.yMax - PixelGUI.Px(8f), barW, PixelGUI.Px(2f)),
                      PixelGUI.TextDisabled);
    }

    // Signal, carrier, clock, battery — the strip that makes the plate read as a screen.
    void DrawStatusBar(Rect r)
    {
        PixelGUI.Fill(r, PixelGUI.PlateDeep);

        float x = r.x + PixelGUI.Px(3f);
        for (int i = 0; i < 4; i++)
        {
            float bh = PixelGUI.Px(1.5f + i);
            PixelGUI.Fill(new Rect(x + i * PixelGUI.Px(2f), r.yMax - PixelGUI.Px(2f) - bh, PixelGUI.Px(1f), bh),
                          i < 3 ? PixelGUI.Text : PixelGUI.TextDisabled);
        }

        PhoneStyles.Label(r, SessionLabel(), PhoneStyles.Footer, PixelGUI.TextDim, TextAnchor.MiddleCenter);

        // Battery: charge tracks nothing, it's set dressing, so it stays put rather than ticking down.
        float bw = PixelGUI.Px(9f), bh2 = PixelGUI.Px(4f);
        var batt = new Rect(r.xMax - bw - PixelGUI.Px(4f), r.center.y - bh2 * 0.5f, bw, bh2);
        PixelGUI.Fill(batt, PixelGUI.TextDisabled);
        PixelGUI.Fill(new Rect(batt.x + PixelGUI.Px(1f), batt.y + PixelGUI.Px(1f),
                               (bw - PixelGUI.Px(2f)) * 0.72f, bh2 - PixelGUI.Px(2f)), PixelGUI.Confirm);
        PixelGUI.Fill(new Rect(batt.xMax, batt.center.y - PixelGUI.Px(1f), PixelGUI.Px(1f), PixelGUI.Px(2f)),
                      PixelGUI.TextDisabled);
    }

    static string SessionLabel()
    {
        if (RaceWeekend.IsQualifying) return "QUALIFYING";
        if (RaceWeekend.IsPractice) return "PRACTICE";
        return "RACE DAY";
    }

    void DrawHome(Rect r)
    {
        float gap = PixelGUI.Px(4f);
        float hint = PhoneApp.RowH;
        float tileW = (r.width - gap) * 0.5f;
        float tileH = (r.height - gap * 2f - hint) / 3f;

        for (int i = 0; i < TileSlots; i++)
        {
            int col = i % 2, rowIdx = i / 2;
            var tile = new Rect(r.x + col * (tileW + gap), r.y + rowIdx * (tileH + gap), tileW, tileH);
            if (i < _apps.Count) DrawTile(tile, _apps[i], i);
            else DrawEmptyBay(tile);
        }

        string keys = InputGlyphs.UsingGamepad
            ? InputGlyphs.PadName(PadBindings.Phone) + " CLOSE   " + InputGlyphs.PadName(PadBindings.Confirm) + " OPEN"
            : InputGlyphs.UsingTouch ? "TAP AN APP"
            : toggleKey.ToString().ToUpperInvariant() + " CLOSE   ENTER OPEN";
        PhoneStyles.Label(new Rect(r.x, r.yMax - hint, r.width, hint), keys,
                          PhoneStyles.Footer, null, TextAnchor.MiddleCenter);
    }

    void DrawTile(Rect r, PhoneApp app, int index)
    {
        bool selected = index == _homeIndex;
        PhoneApp.Plate(r, selected ? PixelGUI.Gold : app.Accent);
        PixelGUI.Fill(new Rect(r.x, r.y, r.width, PixelGUI.Px(2f)), app.Accent);

        if (Pressed(r)) { _homeIndex = index; OpenApp(index); }
        if (r.Contains(Event.current.mousePosition)) _homeIndex = index;

        float row = PhoneApp.RowH;
        var name = new Rect(r.x + PixelGUI.Px(4f), r.y + PixelGUI.Px(4f), r.width - PixelGUI.Px(8f), row);
        PhoneStyles.Label(name, app.TileName, PhoneStyles.Heading, selected ? PixelGUI.Gold : PixelGUI.Text);

        if (!string.IsNullOrEmpty(app.TileSubtitle))
            GUI.Label(new Rect(name.x, name.yMax, name.width, row), app.TileSubtitle, PhoneStyles.DataDim);

        int badge = app.Badge;
        if (badge > 0)
        {
            float d = PixelGUI.Px(8f);
            var dot = new Rect(r.xMax - d - PixelGUI.Px(3f), r.y + PixelGUI.Px(4f), d, d);
            PixelGUI.Fill(dot, PixelGUI.Danger);
            PhoneStyles.Label(dot, badge > 9 ? "9+" : badge.ToString(), PhoneStyles.Footer,
                              PixelGUI.Text, TextAnchor.MiddleCenter);
        }
    }

    // An unfilled bay. Drawn, not hidden, so the grid is stable as apps are added.
    void DrawEmptyBay(Rect r)
    {
        PixelGUI.Fill(r, new Color(PixelGUI.Plate.r, PixelGUI.Plate.g, PixelGUI.Plate.b, 0.35f));
        PhoneStyles.Label(r, "· · ·", PhoneStyles.Footer, null, TextAnchor.MiddleCenter);
    }

    void DrawApp(Rect r)
    {
        // Held for the whole draw: a tap settles on the repaint pass, in the middle of this, and a button that
        // closes the phone or backs out (TRAVEL THERE) clears _current before the rail and title bar are drawn.
        var app = _current;
        if (app == null) return;

        float barH = PhoneApp.RowH + PixelGUI.Px(2f);
        var bar = new Rect(r.x, r.y, r.width, barH);
        var view = new Rect(r.x, bar.yMax + PixelGUI.Px(2f), r.width, r.height - barH - PixelGUI.Px(2f));
        float contentW = view.width - PixelGUI.Px(4f);      // room for the scroll rail

        // Scrolled by hand rather than with GUI.BeginScrollView: the device is drawn through a rotated
        // matrix, and a scroll view's own bars and clipping do not survive one intact.
        float max = Mathf.Max(0f, _contentHeight - view.height);
        _scroll.y = Mathf.Clamp(_scroll.y, 0f, max);

        if (Event.current.type == EventType.ScrollWheel && view.Contains(Event.current.mousePosition))
        {
            _scroll.y = Mathf.Clamp(_scroll.y + Event.current.delta.y * PixelGUI.Px(6f), 0f, max);
            Event.current.Use();
        }

        GUI.BeginGroup(view);
        _clip = new Rect(0f, 0f, view.width, view.height);
        _contentHeight = app.Draw(0f, -_scroll.y, contentW);
        _clip = null;
        GUI.EndGroup();

        if (max > 0f)
        {
            float railW = PixelGUI.Px(2f);
            var rail = new Rect(view.xMax - railW, view.y, railW, view.height);
            PixelGUI.Fill(rail, new Color(0f, 0f, 0f, 0.35f));
            float thumbH = Mathf.Max(PixelGUI.Px(10f), view.height * (view.height / Mathf.Max(1f, _contentHeight)));
            PixelGUI.Fill(new Rect(rail.x, rail.y + (rail.height - thumbH) * (_scroll.y / max), railW, thumbH),
                          app.Accent);
        }

        // Title bar last, so a row scrolled up under it is covered rather than trusted to be clipped.
        PixelGUI.Fill(bar, app.Accent);
        PhoneStyles.Label(new Rect(bar.x + PixelGUI.Px(11f), bar.y, bar.width, bar.height),
                          app.TileName, PhoneStyles.Heading, PixelGUI.Ink);

        // Back chevron, left of the title, the whole strip clickable.
        var back = new Rect(bar.x, bar.y, PixelGUI.Px(11f), bar.height);
        PhoneStyles.Label(back, "<", PhoneStyles.Data, PixelGUI.Ink, TextAnchor.MiddleCenter);
        if (Pressed(back)) { _current = null; _scroll = Vector2.zero; }
    }

    float _contentHeight;
}
