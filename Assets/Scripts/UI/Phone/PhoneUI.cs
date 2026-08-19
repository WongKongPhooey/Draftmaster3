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
public class PhoneUI : MonoBehaviour
{
    public static PhoneUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._open;

    [Tooltip("Opens and closes the phone while on foot.")]
    public Key toggleKey = Key.P;
    [Tooltip("Seconds the phone takes to slide in or out.")]
    public float slideSeconds = 0.22f;
    [Tooltip("Phone body size in UI pixels, before PixelGUI.Scale.")]
    public Vector2 bodySize = new Vector2(196f, 304f);
    [Tooltip("Gap under the phone when it's all the way up, in UI pixels.")]
    public float restGap = 6f;

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
        Register(new PhoneTasksApp());
        Register(new PhoneNotesApp());
        Register(new PhoneSoBuzzApp());
        Register(new PhoneDrivRApp());
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
            var found = FindAnyObjectByType<OnFootController>();
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
        if (_player == null) _player = FindAnyObjectByType<OnFootController>();
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

        float w = PixelGUI.Px(bodySize.x), h = PixelGUI.Px(bodySize.y);
        float rest = Screen.height - h - PixelGUI.Px(restGap);
        float y = Mathf.Round(Mathf.Lerp(Screen.height, rest, Ease(_slide)));
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        var body = new Rect(x, y, w, h);

        DrawBody(body);
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

        // Earpiece slot above the screen, home bar below: two marks that say "phone" without any art.
        float earW = body.width * 0.22f;
        PixelGUI.Fill(new Rect(body.center.x - earW * 0.5f, body.y + PixelGUI.Px(4f), earW, PixelGUI.Px(2f)),
                      PixelGUI.PlateDeep);
        float barW = body.width * 0.34f;
        PixelGUI.Fill(new Rect(body.center.x - barW * 0.5f, body.yMax - PixelGUI.Px(8f), barW, PixelGUI.Px(2f)),
                      PixelGUI.TextDisabled);

        DrawStatusBar(new Rect(screen.x, screen.y, screen.width, PixelGUI.Px(10f)));

        var content = new Rect(screen.x + PixelGUI.Px(4f), screen.y + PixelGUI.Px(12f),
                               screen.width - PixelGUI.Px(8f), screen.height - PixelGUI.Px(16f));

        if (_current == null) DrawHome(content);
        else DrawApp(content);
    }

    // Signal, carrier, clock, battery — the strip that makes the plate read as a screen.
    void DrawStatusBar(Rect r)
    {
        PixelGUI.Fill(r, PixelGUI.PlateDeep);

        float x = r.x + PixelGUI.Px(3f);
        for (int i = 0; i < 4; i++)
        {
            float bh = PixelGUI.Px(2f + i);
            PixelGUI.Fill(new Rect(x + i * PixelGUI.Px(2f), r.yMax - PixelGUI.Px(2f) - bh, PixelGUI.Px(1f), bh),
                          i < 3 ? PixelGUI.Text : PixelGUI.TextDisabled);
        }

        var style = PixelGUI.Footer;
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(r, SessionLabel(), style);
        style.alignment = prevAlign;

        // Battery: charge tracks nothing, it's set dressing, so it stays put rather than ticking down.
        float bw = PixelGUI.Px(10f), bh2 = PixelGUI.Px(5f);
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
        float gap = PixelGUI.Px(5f);
        float tileW = (r.width - gap) * 0.5f;
        float tileH = (r.height - gap * 2f - PixelGUI.Px(12f)) / 3f;

        for (int i = 0; i < TileSlots; i++)
        {
            int col = i % 2, rowIdx = i / 2;
            var tile = new Rect(r.x + col * (tileW + gap), r.y + rowIdx * (tileH + gap), tileW, tileH);
            if (i < _apps.Count) DrawTile(tile, _apps[i], i);
            else DrawEmptyBay(tile);
        }

        var hint = new Rect(r.x, r.yMax - PixelGUI.Px(10f), r.width, PixelGUI.Px(10f));
        var style = PixelGUI.Footer;
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(hint, $"{toggleKey.ToString().ToUpperInvariant()} CLOSE   ENTER OPEN", style);
        style.alignment = prevAlign;
    }

    void DrawTile(Rect r, PhoneApp app, int index)
    {
        bool selected = index == _homeIndex;
        PhoneApp.Plate(r, selected ? PixelGUI.Gold : app.Accent);
        PixelGUI.Fill(new Rect(r.x, r.y, r.width, PixelGUI.Px(2f)), app.Accent);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _homeIndex = index; OpenApp(index); }
        if (r.Contains(Event.current.mousePosition)) _homeIndex = index;

        var name = new Rect(r.x + PixelGUI.Px(4f), r.y + PixelGUI.Px(5f), r.width - PixelGUI.Px(8f), PixelGUI.Px(10f));
        var style = PixelGUI.HeadingSmall;
        var prev = style.normal.textColor;
        style.normal.textColor = selected ? PixelGUI.Gold : PixelGUI.Text;
        GUI.Label(name, app.TileName, style);
        style.normal.textColor = prev;

        if (!string.IsNullOrEmpty(app.TileSubtitle))
            GUI.Label(new Rect(name.x, name.yMax + PixelGUI.Px(1f), name.width, PixelGUI.Px(11f)),
                      app.TileSubtitle, PixelGUI.DataDim);

        int badge = app.Badge;
        if (badge > 0)
        {
            float d = PixelGUI.Px(9f);
            var dot = new Rect(r.xMax - d - PixelGUI.Px(3f), r.y + PixelGUI.Px(4f), d, d);
            PixelGUI.Fill(dot, PixelGUI.Danger);
            var bs = PixelGUI.Footer;
            var pa = bs.alignment; var pc = bs.normal.textColor;
            bs.alignment = TextAnchor.MiddleCenter;
            bs.normal.textColor = PixelGUI.Text;
            GUI.Label(dot, badge > 9 ? "9+" : badge.ToString(), bs);
            bs.alignment = pa; bs.normal.textColor = pc;
        }
    }

    // An unfilled bay. Drawn, not hidden, so the grid is stable as apps are added.
    void DrawEmptyBay(Rect r)
    {
        PixelGUI.Fill(r, new Color(PixelGUI.Plate.r, PixelGUI.Plate.g, PixelGUI.Plate.b, 0.35f));
        var style = PixelGUI.Footer;
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(r, "· · ·", style);
        style.alignment = prevAlign;
    }

    void DrawApp(Rect r)
    {
        var bar = new Rect(r.x, r.y, r.width, PixelGUI.Px(12f));
        PixelGUI.Fill(bar, _current.Accent);
        var titleStyle = PixelGUI.HeadingSmall;
        var prev = titleStyle.normal.textColor;
        titleStyle.normal.textColor = PixelGUI.Ink;
        GUI.Label(new Rect(bar.x + PixelGUI.Px(14f), bar.y + PixelGUI.Px(2f), bar.width, PixelGUI.Px(9f)),
                  _current.TileName, titleStyle);
        titleStyle.normal.textColor = prev;

        // Back chevron, left of the title, the whole strip clickable.
        var back = new Rect(bar.x, bar.y, PixelGUI.Px(13f), bar.height);
        var backStyle = PixelGUI.Label;
        var bp = backStyle.normal.textColor; var ba = backStyle.alignment;
        backStyle.normal.textColor = PixelGUI.Ink;
        backStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(back, "<", backStyle);
        backStyle.normal.textColor = bp; backStyle.alignment = ba;
        if (GUI.Button(back, GUIContent.none, GUIStyle.none)) { _current = null; _scroll = Vector2.zero; return; }

        var view = new Rect(r.x, bar.yMax + PixelGUI.Px(3f), r.width, r.height - bar.height - PixelGUI.Px(3f));
        float contentW = view.width - PixelGUI.Px(10f);   // room for the scrollbar

        // Measure-then-draw in one pass: the app returns its height, which becomes next frame's view size.
        var inner = new Rect(0f, 0f, contentW, Mathf.Max(view.height, _contentHeight));
        _scroll = GUI.BeginScrollView(view, _scroll, inner, false, false);
        _contentHeight = _current.Draw(0f, 0f, contentW);
        GUI.EndScrollView();
    }

    float _contentHeight;
}
