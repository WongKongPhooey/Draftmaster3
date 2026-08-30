using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's phone. Slides up from the bottom of the screen while on foot and shows a home screen of
// app tiles — Tasks, Notes, SoBuzz, DrivR — each of which draws its own content (PhoneApp).
//
// Why a phone rather than another F-key panel: everything on it is stuff the driver would actually look
// up between sessions, and it keeps the on-foot half of the game from needing a menu screen. It is not a
// pause — the paddock keeps moving behind it — but the player stops walking while it's up.
//
// Six tiles, two by three. Four are filled; the spare bays are drawn as empty so the grid doesn't reflow
// when the fifth and sixth arrive. Self-bootstraps like RacePauseMenu / DriverInfoPanel, arms itself only
// in scenes that have an on-foot player, and draws with the Iron Oval kit (PixelGUI).
//
// It sits over on the left of the screen and is held at a slight angle, drawn through one GUI.matrix
// about the bottom of the device — so every rect inside is authored square and the tilt costs nothing.
// Type is PhoneStyles, a step down from the kit's own: a panel-sized glyph in a phone-sized row is what
// made this thing read as squashed.
public class PhoneUI : MonoBehaviour
{
    public static PhoneUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._open;

    [Tooltip("Opens and closes the phone while on foot.")]
    public Key toggleKey = Key.P;
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
    Vector2 _scroll;

    bool _open;
    float _slide;                      // 0 = off screen, 1 = resting
    OnFootController _player;
    bool _lockedByPhone;               // we set MovementLocked, so we're the one who clears it
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
        Register(new PhoneDrivRApp());
        Register(new PhoneChampionshipApp());
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

    // ------------------------------------------------------------------ state

    void Update()
    {
        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer = 0.5f;
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

        var kb = Keyboard.current;
        if (kb == null) return;

        if (toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame)
        {
            if (_open) CloseInternal();
            else OpenInternal(null);
        }

        if (!_open) return;

        // Esc backs out one level: app → home → away. RacePauseMenu stands down while the phone is up.
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
        {
            if (_current != null) { _current = null; _scroll = Vector2.zero; }
            else CloseInternal();
            return;
        }

        if (_current == null) HomeKeys(kb);
        else if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            _scroll.y = Mathf.Max(0f, _scroll.y + (kb.downArrowKey.wasPressedThisFrame ? PixelGUI.Px(24f) : -PixelGUI.Px(24f)));
    }

    void HomeKeys(Keyboard kb)
    {
        int col = _homeIndex % 2, rowIdx = _homeIndex / 2;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) col = 1;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) col = 0;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) rowIdx = Mathf.Min(2, rowIdx + 1);
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) rowIdx = Mathf.Max(0, rowIdx - 1);
        _homeIndex = Mathf.Clamp(rowIdx * 2 + col, 0, TileSlots - 1);

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
            OpenApp(_homeIndex);
    }

    void OpenInternal(string appId)
    {
        if (_player == null) _player = OnFootController.Current;
        if (_player == null) return;                       // in the car, or a scene with no on-foot body
        if (!_open && _player.MovementLocked) return;      // a conversation or cutscene has the player

        _open = true;
        _scroll = Vector2.zero;
        _current = null;
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
    }

    void OpenApp(int slot)
    {
        if (slot < 0 || slot >= _apps.Count) return;       // an empty bay
        _current = _apps[slot];
        _scroll = Vector2.zero;
        _current.OnOpen();
    }

    // ------------------------------------------------------------------ drawing

    void OnGUI()
    {
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

        PhoneStyles.Label(new Rect(r.x, r.yMax - hint, r.width, hint),
                          toggleKey.ToString().ToUpperInvariant() + " CLOSE   ENTER OPEN",
                          PhoneStyles.Footer, null, TextAnchor.MiddleCenter);
    }

    void DrawTile(Rect r, PhoneApp app, int index)
    {
        bool selected = index == _homeIndex;
        PhoneApp.Plate(r, selected ? PixelGUI.Gold : app.Accent);
        PixelGUI.Fill(new Rect(r.x, r.y, r.width, PixelGUI.Px(2f)), app.Accent);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _homeIndex = index; OpenApp(index); }
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
        _contentHeight = _current.Draw(0f, -_scroll.y, contentW);
        GUI.EndGroup();

        if (max > 0f)
        {
            float railW = PixelGUI.Px(2f);
            var rail = new Rect(view.xMax - railW, view.y, railW, view.height);
            PixelGUI.Fill(rail, new Color(0f, 0f, 0f, 0.35f));
            float thumbH = Mathf.Max(PixelGUI.Px(10f), view.height * (view.height / Mathf.Max(1f, _contentHeight)));
            PixelGUI.Fill(new Rect(rail.x, rail.y + (rail.height - thumbH) * (_scroll.y / max), railW, thumbH),
                          _current.Accent);
        }

        // Title bar last, so a row scrolled up under it is covered rather than trusted to be clipped.
        PixelGUI.Fill(bar, _current.Accent);
        PhoneStyles.Label(new Rect(bar.x + PixelGUI.Px(11f), bar.y, bar.width, bar.height),
                          _current.TileName, PhoneStyles.Heading, PixelGUI.Ink);

        // Back chevron, left of the title, the whole strip clickable.
        var back = new Rect(bar.x, bar.y, PixelGUI.Px(11f), bar.height);
        PhoneStyles.Label(back, "<", PhoneStyles.Data, PixelGUI.Ink, TextAnchor.MiddleCenter);
        if (GUI.Button(back, GUIContent.none, GUIStyle.none)) { _current = null; _scroll = Vector2.zero; }
    }

    float _contentHeight;
}
