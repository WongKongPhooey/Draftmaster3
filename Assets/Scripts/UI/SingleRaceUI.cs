using System.Collections.Generic;
using Draftmaster.Controls;
using Draftmaster.Data;
using Draftmaster.Tracks;
using Draftmaster.Weekend;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// SINGLE RACE: pick a track, a championship and a car, then go racing. Three screens in one scene, built to the
// race-flow design handoff (Designs/draftmaster3_race_flow_unity_handoff: README.md, data.json and the
// track/series/car_select_{pc,mobile} references).
//
// WHY THIS EXISTS. Until it did, the only way to race a track other than the reference one was an editor window,
// and the title screen's since-removed NEW SEASON row overwrote that choice. With 38 venues built, "that track,
// that series, that car" is the difference between a test build and a game.
//
// WHY ONE SCENE AND NOT THREE. Back-navigation is a screen swap rather than a scene load, so no step smuggles its
// answer through PlayerPrefs: the choices live in fields and are only committed when RACE is pressed on the last
// screen. Backing out changes nothing, which is what a cancel should do.
//
// WHY THE UI IS BUILT IN CODE. The screens are grids of generated tiles (38 tracks, a variable field), so the rows
// have to be generated whatever happens; and a generated scene's button listeners do not survive a save in this
// project, so a screen that wires itself at runtime is the more durable option.
//
// THE DESIGN, AND WHERE IT IS READ FROM:
//  * Shared shell: 30 px header bar (title, slate context, BACK with an ESC keycap), a grid of tiles that scrolls
//    under it, 28 px footer (34 on a phone) with key hints / "TAP A ..." on the left and "9 / 17" + the red
//    confirm button on the right. Tiles: idle = panel fill + 3 px line outline; selected = panel-sel + 3 px gold
//    outline + 4 px ink hard shadow + a blinking gold cursor; PC hover lifts the fill.
//  * Track tile: blueprint art box with the layout traced off the track's own spline (RaceFlowArt), type tag
//    (ROAD / STREET / OVAL / DIRT), length chip, name and town. Filters ALL / ROAD / OVAL / DIRT in the header.
//  * Series tile: the series' colour in 135° stripes with its code in Pixelify Bold, kind tag, field chip,
//    name and "48 entered" / "No field entered".
//  * Car tile: asphalt slot with the livery at native 64x32, the player's car tagged YOU, surname, #number in
//    red and the team.
//  * Canvas: Constant Pixel Size at a whole-number factor (RaceFlow.ScaleFactor); PC tiles 4 x 140 / cars
//    6 x 92, phone 5 x 150 / 7 x 100, fewer when the canvas is narrower.
//  * Tapping a tile selects it; tapping the selected tile again (or the confirm button) moves on.
public class SingleRaceUI : MonoBehaviour
{
    public enum Step { Track, Series, Car }

    [Tooltip("Scene the flow ends in. Must be in the build settings.")]
    public string raceSceneName = "RaceScene";
    [Tooltip("Scene the BACK option returns to from the first step.")]
    public string titleSceneName = "TitleScreen";

    // ------------------------------------------------------------------ design tokens (README "Colours")

    static class Pal
    {
        public static readonly Color Ink = RaceFlowArt.Hex(0x05060a);
        public static readonly Color Bg = RaceFlowArt.Hex(0x0d0f16);
        public static readonly Color Bar = RaceFlowArt.Hex(0x1b2030);
        public static readonly Color Panel = RaceFlowArt.Hex(0x182136);
        public static readonly Color PanelSel = RaceFlowArt.Hex(0x243a5e);
        public static readonly Color PanelHover = RaceFlowArt.Hex(0x1b2840);
        public static readonly Color Line = RaceFlowArt.Hex(0x2c3145);
        public static readonly Color Cream = RaceFlowArt.Hex(0xf4ead7);
        public static readonly Color Sand = RaceFlowArt.Hex(0xb9ae9a);
        public static readonly Color Slate = RaceFlowArt.Hex(0x8b93ad);
        public static readonly Color Gold = RaceFlowArt.Hex(0xe8b13c);
        public static readonly Color Red = RaceFlowArt.Hex(0xc4452f);
        public static readonly Color RedText = RaceFlowArt.Hex(0xe0604a);
        public static readonly Color ChipOff = RaceFlowArt.Hex(0x12141c);
        public static readonly Color KeyInk = RaceFlowArt.Hex(0x12141c);
        public static readonly Color DarkInk = RaceFlowArt.Hex(0x0b0c11);
        public static readonly Color Ink80 = RaceFlowArt.Hex(0x05060a, 0xcc);
    }

    // Layout per platform (design LAYOUT table).
    struct Layout
    {
        public float tile, tileGap; public int tileCols;
        public float car, carGap; public int carCols;
        public float footer, button;
    }

    static readonly Layout PcLayout = new() { tile = 140, tileGap = 10, tileCols = 4, car = 92, carGap = 8, carCols = 6, footer = 28, button = 20 };
    static readonly Layout PhoneLayout = new() { tile = 150, tileGap = 10, tileCols = 5, car = 100, carGap = 8, carCols = 7, footer = 34, button = 26 };

    // The design's boxes are border-box: the header is 30 px in all, 27 of bar over a 3 px ink rule, and the
    // footer's 28 / 34 includes its 2 px top rule.
    const float HeaderH = 27f, HeaderBorder = 3f, GridPadY = 8f, Row = 16f;
    const float BlinkPeriod = 0.9f;

    // ------------------------------------------------------------------ state

    Step _step = Step.Track;
    RaceFlow.TrackFilter _filter = RaceFlow.TrackFilter.All;
    string _trackId;                          // highlighted / chosen track
    Draftmaster.Data.Series _series;          // chosen series
    int _seriesIndex = -1;                    // highlighted series
    int _carIndex = -1;                       // highlighted car
    Driver _driver;
    bool _loading, _opened;

    // Option sources.
    readonly List<Track> _allTracks = new();
    readonly List<Track> _tracks = new();     // after the filter
    List<Draftmaster.Data.Series> _allSeries = new();
    readonly List<int> _seriesField = new();
    List<Driver> _drivers = new();
    string _carset;

    int _index;
    int _cols = 1;

    // ------------------------------------------------------------------ built UI

    Canvas _canvas;
    CanvasScaler _scaler;
    RectTransform _root;          // the current screen, rebuilt per step
    RectTransform _viewport, _content;
    ScrollRect _scroll;
    TextMeshProUGUI _title, _status;
    readonly List<Tile> _tiles = new();
    float _statusUntil;
    string _chromeKey;
    Layout _L;
    float _W, _H;                 // canvas size in design px

    TMP_FontAsset _mono, _silk, _pixelify;
    float _pixelifySize = 30f;

    class Tile
    {
        public RectTransform rect;
        public Image shadow, ring, fill, cursor;
        public RawImage layout;
        public Texture idleArt, selectedArt;
        public TextMeshProUGUI tag, chip, name, sub, code, codeShadow, number;
        public string nameIdle, nameSelected;
        public float nameX;
        public bool hovered;
        public bool open = true;  // series: has a field
        public bool unbuilt;      // track: not built yet (RaceFlow.IsAuthored) - greyed out
    }

    // ------------------------------------------------------------------ what the screen shows (tests)

    public Step CurrentStep => _step;
    public string StepName => _step.ToString();
    public int OptionCount => _step switch { Step.Track => _tracks.Count, Step.Series => _allSeries.Count, _ => CarCount };
    public int SelectedIndex => _index;
    public int Columns => _cols;
    public string FilterName => RaceFlow.FilterLabel(_filter);
    public string TitleText => _title != null ? _title.text : "";
    public string SelectedTrackId => _step == Step.Track ? CurrentTrackId : _trackId;
    public string SelectedSeriesCode => _series?.ShortName;
    public int TileCount => _tiles.Count;
    int CarCount => _drivers.Count > 0 ? _drivers.Count : 1;   // an empty field still offers the player's own car
    string CurrentTrackId => _index >= 0 && _index < _tracks.Count ? _tracks[_index].Name : _trackId;

    // ------------------------------------------------------------------ lifecycle

    void Start()
    {
        IronOvalScanlines.Ensure();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Open();
    }

    // Builds the screen on the first step. Public so EditMode tests can open it without a scene.
    public void Open()
    {
        if (_opened) return;
        _opened = true;

        _mono = Resources.Load<TMP_FontAsset>("Fonts/Monogram Pixel");
        _silk = Resources.Load<TMP_FontAsset>("Fonts/Silkscreen Pixel");
        _pixelify = Resources.Load<TMP_FontAsset>("Fonts/Pixelify Bold Pixel");
        if (_pixelify == null)
        {
            // Draftmaster > UI > Build Race Flow Fonts makes the 30 px bold face; until then the kit's 20 px one.
            _pixelify = Resources.Load<TMP_FontAsset>("Fonts/Pixelify Sans Pixel");
            _pixelifySize = 20f;
        }
        foreach (var f in new[] { _mono, _silk, _pixelify })
            if (f != null && f.atlasTexture != null) f.atlasTexture.filterMode = FilterMode.Point;

        LoadTracks();
        _trackId = TrackSelection.CurrentId;
        BuildCanvas();
        EnterStep(Step.Track);
    }

    void Update()
    {
        if (!_opened || _loading) return;

        if (ChromeKey() != _chromeKey) Rebuild();

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) MoveCursor(-1, 0);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) MoveCursor(1, 0);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) MoveCursor(0, -1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) MoveCursor(0, 1);
            if (kb.qKey.wasPressedThisFrame) StepFilter(-1);
            if (kb.eKey.wasPressedThisFrame) StepFilter(1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame) ConfirmSelection();
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) GoBack();
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) MoveCursor(-1, 0);
            if (pad.dpad.right.wasPressedThisFrame) MoveCursor(1, 0);
            if (pad.dpad.up.wasPressedThisFrame) MoveCursor(0, -1);
            if (pad.dpad.down.wasPressedThisFrame) MoveCursor(0, 1);
            if (pad.leftShoulder.wasPressedThisFrame) StepFilter(-1);
            if (pad.rightShoulder.wasPressedThisFrame) StepFilter(1);
            if (PadInput.Control(pad, PadBindings.Confirm).wasPressedThisFrame) ConfirmSelection();
            if (PadInput.Control(pad, PadBindings.Back).wasPressedThisFrame) GoBack();
        }

        if (_statusUntil > 0f && Time.unscaledTime >= _statusUntil) SetStatus("");

        // The ▶ cursor blinks on a stepped 0.9 s cycle.
        bool on = Mathf.Repeat(Time.unscaledTime, BlinkPeriod) < BlinkPeriod * 0.5f;
        if (_index >= 0 && _index < _tiles.Count && _tiles[_index].cursor != null)
            _tiles[_index].cursor.enabled = on;
    }

    // Whole-pixel scrolling: the grid only ever moves in whole design pixels.
    void LateUpdate()
    {
        if (_content == null) return;
        var p = _content.anchoredPosition;
        float y = Mathf.Round(p.y);
        if (!Mathf.Approximately(p.x, 0f) || !Mathf.Approximately(p.y, y)) _content.anchoredPosition = new Vector2(0f, y);
    }

    // ------------------------------------------------------------------ steps

    void LoadTracks()
    {
        _allTracks.Clear();
        foreach (var row in TrackCatalog.Playable()) _allTracks.Add(row);
        _allTracks.Sort((a, b) =>
        {
            int ta = TagOrder(TagOf(a)), tb = TagOrder(TagOf(b));
            return ta != tb ? ta.CompareTo(tb)
                            : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
        });
    }

    static RaceFlow.TrackTag TagOf(Track t) =>
        RaceFlow.Tag(t.Name, t.Type == TrackType.RoadCourse, t.Type == TrackType.DirtCourse);

    static int TagOrder(RaceFlow.TrackTag tag) => tag switch
    {
        RaceFlow.TrackTag.Road => 0, RaceFlow.TrackTag.Street => 0, RaceFlow.TrackTag.Oval => 1, _ => 2,
    };

    void FilterTracks()
    {
        _tracks.Clear();
        // The built venues first, then the rest - greyed out (RaceFlow.IsAuthored).
        foreach (bool built in new[] { true, false })
            foreach (var t in _allTracks)
                if (RaceFlow.IsAuthored(t.Name) == built && RaceFlow.Passes(_filter, TagOf(t))) _tracks.Add(t);
        int at = _tracks.FindIndex(t => t.Name == _trackId && RaceFlow.IsAuthored(t.Name));
        if (at < 0) at = _tracks.FindIndex(t => RaceFlow.IsAuthored(t.Name));
        _index = Mathf.Max(0, at);
    }

    void EnterStep(Step step)
    {
        _step = step;
        switch (step)
        {
            case Step.Track:
                FilterTracks();
                break;

            case Step.Series:
                _allSeries = SeriesRoster.AllSeries();
                _seriesField.Clear();
                foreach (var s in _allSeries) _seriesField.Add(SeriesRoster.Drivers(s).Count);
                if (_seriesIndex < 0 || _seriesIndex >= _allSeries.Count)
                    _seriesIndex = Mathf.Max(0, _allSeries.FindIndex(s => SeriesRoster.IsSeededField(s)));
                _index = _seriesIndex;
                break;

            case Step.Car:
                _drivers = _series != null ? SeriesRoster.Drivers(_series) : new List<Driver>();
                _carset = CarsetOf(_series);
                if (_carIndex < 0 || _carIndex >= CarCount)
                {
                    int mine = PlayerDriver.CarNumber;
                    _carIndex = Mathf.Max(0, _drivers.FindIndex(d => d.CarNumber == mine));
                }
                _index = _carIndex;
                break;
        }
        Rebuild();
    }

    // ------------------------------------------------------------------ input (public for tests)

    public void MoveCursor(int dx, int dy)
    {
        if (_loading || OptionCount == 0) return;
        int next = RaceFlow.Move(_index, OptionCount, _cols, dx, dy);
        if (next != _index) Select(next);
    }

    public void StepFilter(int by)
    {
        if (_loading || _step != Step.Track) return;
        SetTrackFilter(RaceFlow.StepFilter(_filter, by));
    }

    public void SetTrackFilter(RaceFlow.TrackFilter filter)
    {
        if (_step != Step.Track) return;
        _trackId = CurrentTrackId;   // keep the highlighted track when it survives the filter
        _filter = filter;
        FilterTracks();
        Rebuild();
    }

    // A tap or click on a tile selects it; on the tile already selected it moves on (a phone has no Enter).
    public void TapTile(int i)
    {
        Deselect();
        if (_loading || i < 0 || i >= OptionCount) return;
        if (i == _index) { ConfirmSelection(); return; }
        Select(i);
    }

    public void ConfirmSelection()
    {
        if (_loading) return;
        switch (_step)
        {
            case Step.Track:
                if (_tracks.Count == 0) { SetStatus("No tracks under this filter."); return; }
                if (!RaceFlow.IsAuthored(_tracks[_index].Name))
                {
                    SetStatus($"{_tracks[_index].DisplayName} is coming soon - pick Watkins Glen, Daytona or Texas.");
                    return;
                }
                _trackId = _tracks[_index].Name;
                EnterStep(Step.Series);
                break;

            case Step.Series:
                if (_allSeries.Count == 0) return;
                _seriesIndex = _index;
                var picked = _allSeries[_index];
                if (_series == null || _series.Id != picked.Id) _carIndex = -1;   // a new field, a new default car
                _series = picked;
                EnterStep(Step.Car);
                break;

            case Step.Car:
                _carIndex = _index;
                _driver = _index < _drivers.Count ? _drivers[_index] : null;
                Go();
                break;
        }
    }

    public void GoBack()
    {
        if (_loading) return;
        switch (_step)
        {
            case Step.Track:
                if (!string.IsNullOrEmpty(titleSceneName) && Application.isPlaying
                    && Application.CanStreamedLevelBeLoaded(titleSceneName))
                {
                    _loading = true;
                    SceneManager.LoadScene(titleSceneName);
                }
                else SetStatus($"{titleSceneName} isn't in the build settings.");
                break;

            case Step.Series:
                _seriesIndex = _index;
                EnterStep(Step.Track);
                break;

            case Step.Car:
                _carIndex = _index;
                EnterStep(Step.Series);
                break;
        }
    }

    void Select(int i)
    {
        int old = _index;
        _index = Mathf.Clamp(i, 0, Mathf.Max(0, OptionCount - 1));
        if (_step == Step.Track && _index < _tracks.Count) _trackId = _tracks[_index].Name;
        if (old >= 0 && old < _tiles.Count) Restyle(old);
        if (_index < _tiles.Count) Restyle(_index);
        RefreshPosition();
        ScrollIntoView(_index);
    }

    // ------------------------------------------------------------------ commit

    // The only place this screen writes anything. Everything above is a choice held in a field.
    void Go()
    {
        if (!TrackCatalog.HasGeometry(_trackId))
        {
            SetStatus("That track has no layout built.");
            return;
        }
        if (!Application.isPlaying) return;   // EditMode tests stop at the commit

        // Everything below writes keys the career keeps its place in. Park them first; the title screen
        // puts them back (OneOffRacePrefs).
        OneOffRacePrefs.Capture();
        TrackSelection.Select(_trackId);

        // Keep the weekend's idea of which championship the player is in aligned with the pick, when the
        // chosen series is one of the three that share a race weekend. The others (open wheel, dirt) have no
        // weekend, so they leave it alone.
        foreach (var s in SeriesCatalog.All)
            if (_series != null && string.Equals(SeriesCatalog.ShortCode(s), _series.ShortName,
                                                 System.StringComparison.OrdinalIgnoreCase))
                SeriesCatalog.PlayerSeries = s;

        // PlayerDriver.CarNumber is read-only — the keys are the public contract.
        PlayerCarLivery.SingleRaceLivery = null;
        if (_driver != null)
        {
            if (_driver.CarNumber > 0) PlayerPrefs.SetInt(PlayerDriver.NumberKey, _driver.CarNumber);
            string name = string.Join(" ", _driver.FirstName, _driver.LastName).Trim();
            if (!string.IsNullOrWhiteSpace(name)) PlayerPrefs.SetString(PlayerDriver.NameKey, name);
            PlayerPrefs.Save();

            // Race in the paint the tile showed: a Truck or National pick is in that championship's carset, not
            // the Cup paint the scene's car is authored in.
            string livery = LiveryName(_carset, _driver.CarNumber);
            if (livery != null && Resources.Load<Sprite>(livery) != null) PlayerCarLivery.SingleRaceLivery = livery;
        }

        // A single race is one race: no practice, no qualifying, live the moment the scene loads — and not a race
        // weekend: the mode switches the career layer off (no paddock, no sheet, no walking).
        GameSession.CurrentMode = GameSession.Mode.SingleRace;
        RaceWeekend.Current = RaceWeekend.Session.Race;
        RaceWeekend.SessionLive = true;

        if (!Application.CanStreamedLevelBeLoaded(raceSceneName))
        {
            SetStatus($"{raceSceneName} isn't in the build settings.");
            return;
        }

        _loading = true;
        LoadingScreen.Load(raceSceneName);
    }

    // The carset a championship's cars are painted in — the three GridSpawner puts on track. Null for a series
    // with no paint of its own.
    static string CarsetOf(Draftmaster.Data.Series series)
    {
        if (series == null) return null;
        if (SimulatedField.TryRacingSeries(series, out var racing))
            return racing == RacingSeries.Trucks ? "cts25" : "xfi25";
        return SeriesRoster.IsSeededField(series) ? CupRoster2026.CarsetPrefix : null;
    }

    static string LiveryName(string carset, int number) =>
        string.IsNullOrEmpty(carset) || number < 0 ? null : carset + "livery" + number;

    // ------------------------------------------------------------------ canvas

    // Lays the screens out for a phone (or not) regardless of the device — for previewing the phone layout in the
    // editor and for tests. Null = follow PixelGUI.Handheld.
    public bool? ForceHandheld { get => _forceHandheld; set { _forceHandheld = value; if (_opened) Rebuild(); } }
    bool? _forceHandheld;

    bool Handheld => _forceHandheld ?? PixelGUI.Handheld;
    bool ShowKeys => !Handheld && !InputGlyphs.UsingTouch;

    string ChromeKey() =>
        $"{Screen.width}x{Screen.height}:{Handheld}:{InputGlyphs.UsingGamepad}:{InputGlyphs.UsingTouch}:{InputGlyphs.Family}";

    void BuildCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.pixelPerfect = true;
        _scaler = go.GetComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        _scaler.referencePixelsPerUnit = 100f;
    }

    void Rebuild()
    {
        _chromeKey = ChromeKey();
        _L = Handheld ? PhoneLayout : PcLayout;

        int scale = RaceFlow.ScaleFactor(Screen.width, Screen.height, Handheld);
        _scaler.scaleFactor = scale;
        _W = Mathf.Floor(Screen.width / (float)scale);
        _H = Mathf.Floor(Screen.height / (float)scale);

        // Hidden at once (GameObject.Find and the raycaster skip it), destroyed at the end of the frame — this
        // can run inside the old screen's own button callback.
        if (_root != null) { _root.gameObject.SetActive(false); DestroyNow(_root.gameObject); }
        _tiles.Clear();

        _root = NewRect("Screen", (RectTransform)_canvas.transform);
        _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 1f);
        _root.sizeDelta = new Vector2(_W, _H);
        _root.anchoredPosition = Vector2.zero;

        Box(_root, "Backdrop", 0, 0, _W, _H, Pal.Bg);

        BuildGrid();
        BuildHeader();
        BuildFooter();

        for (int i = 0; i < _tiles.Count; i++) Restyle(i);
        RefreshPosition();
        ScrollIntoView(_index, instant: true);
    }

    // ------------------------------------------------------------------ header

    void BuildHeader()
    {
        var bar = Box(_root, "Header", 0, 0, _W, HeaderH + HeaderBorder, Pal.Ink);
        Box(bar, "Bar", 0, 0, _W, HeaderH, Pal.Bar);

        string title = _step switch { Step.Track => "SELECT TRACK", Step.Series => "SELECT SERIES", _ => "SELECT CAR" };
        _title = Mono(bar, "Title", title, Pal.Cream);
        float titleW = Width(_title);
        Place(_title.rectTransform, 10, 6, titleW, Row);

        // Right-hand side, right to left: BACK, then the filter chips on the track screen.
        float right = _W - 6f;
        var back = Pill(bar, "Back", ShowKeys ? BackKey : null, "BACK", 2f, 22f, Pal.Line, Pal.Cream, GoBack);
        right -= back.sizeDelta.x;
        Place(back, right, 3, back.sizeDelta.x, back.sizeDelta.y);

        float contextRight = right - 8f;
        if (_step == Step.Track)
        {
            float x = contextRight;
            for (int i = RaceFlow.Filters.Length - 1; i >= 0; i--)
            {
                var f = RaceFlow.Filters[i];
                bool on = f == _filter;
                var chip = Pill(bar, "Filter_" + RaceFlow.FilterLabel(f), null, RaceFlow.FilterLabel(f), 1f, 20f,
                                on ? Pal.Gold : Pal.ChipOff, on ? Pal.DarkInk : Pal.Sand,
                                () => SetTrackFilter(f));
                x -= chip.sizeDelta.x;
                Place(chip, x, 4, chip.sizeDelta.x, chip.sizeDelta.y);
                x -= 4f;
            }
            contextRight = x + 4f - 8f - 2f;   // clear the chips' outline
        }

        string context = _step switch
        {
            Step.Track => $"{_tracks.Count} TRACKS",
            Step.Series => TrackCatalog.DisplayName(_trackId),
            _ => TrackCatalog.DisplayName(_trackId) + " > " + SeriesRoster.Label(_series),
        };
        var ctx = Mono(bar, "Context", "", Pal.Slate);
        float ctxX = 10f + titleW + 8f;
        float room = Mathf.Max(0f, contextRight - ctxX);
        ctx.text = Fit(ctx, context, room);
        Place(ctx.rectTransform, ctxX, 6, room, Row);
    }

    string BackKey => InputGlyphs.UsingGamepad ? InputGlyphs.PadName(PadBindings.Back).ToUpperInvariant() : "ESC";
    string ConfirmKey => InputGlyphs.UsingGamepad ? InputGlyphs.PadName(PadBindings.Confirm).ToUpperInvariant() : "ENTER";

    // ------------------------------------------------------------------ footer

    void BuildFooter()
    {
        float h = _L.footer, top = _H - h;
        var foot = Box(_root, "Footer", 0, top, _W, h, Pal.Bg);
        Box(foot, "Rule", 0, 0, _W, 2, Pal.Bar);
        float mid = 2f + (h - 2f) / 2f;

        // Right: confirm button, then the position counter.
        string verb = _step == Step.Car ? "RACE" : "NEXT";
        var button = ConfirmButton(foot, verb);
        float bx = _W - 10f - button.sizeDelta.x;
        Place(button, bx, Mathf.Floor(mid - button.sizeDelta.y / 2f), button.sizeDelta.x, button.sizeDelta.y);

        var pos = Mono(foot, "Position", "", Pal.Slate);
        pos.alignment = TextAlignmentOptions.TopRight;
        Place(pos.rectTransform, bx - 8f - 80f, Mathf.Floor(mid - Row / 2f), 80f, Row);

        // Left: key hints on a keyboard / pad, a tap hint on a phone; a status line borrows the place.
        var left = NewRect("Hints", foot);
        Place(left, 10, 0, Mathf.Max(0f, bx - 100f - 10f), h);
        float x = 0f, y = Mathf.Floor(mid - 5f);
        if (ShowKeys)
        {
            if (InputGlyphs.UsingGamepad)
            {
                x = Keycap(left, "D-PAD", x, y) + 8f;
                x = Hint(left, "MOVE", x, y) + 6f + 8f;
                if (_step == Step.Track)
                {
                    x = Keycap(left, InputGlyphs.PadName(PadButton.LeftShoulder).ToUpperInvariant(), x, y) + 8f;
                    x = Keycap(left, InputGlyphs.PadName(PadButton.RightShoulder).ToUpperInvariant(), x, y) + 8f;
                    Hint(left, "FILTER", x, y);
                }
            }
            else
            {
                foreach (var k in new[] { "W", "A", "S", "D" }) x = Keycap(left, k, x, y) + 8f;
                x = Hint(left, "MOVE", x, y) + 6f + 8f;
                if (_step == Step.Track)
                {
                    x = Keycap(left, "Q", x, y) + 8f;
                    x = Keycap(left, "E", x, y) + 8f;
                    Hint(left, "FILTER", x, y);
                }
            }
        }
        else
        {
            string tap = _step switch
            {
                Step.Track => "TAP A TRACK · SWIPE FOR MORE",
                Step.Series => "TAP A SERIES",
                _ => "TAP A CAR · SWIPE FOR MORE",
            };
            Hint(left, tap, 0, Mathf.Floor(mid - 4f));
        }
        _status = Mono(foot, "Status", "", Pal.Cream);
        Place(_status.rectTransform, 10, Mathf.Floor(mid - Row / 2f), Mathf.Max(0f, bx - 100f - 10f), Row);
        _hintsRoot = left;
    }

    RectTransform _hintsRoot;

    void RefreshPosition()
    {
        var pos = _root != null ? _root.Find("Footer/Position")?.GetComponent<TextMeshProUGUI>() : null;
        if (pos != null) pos.text = RaceFlow.Position(_index, OptionCount);
    }

    void SetStatus(string message)
    {
        if (_status == null) return;
        _status.text = message ?? "";
        bool showing = !string.IsNullOrEmpty(message);
        if (_hintsRoot != null) _hintsRoot.gameObject.SetActive(!showing);
        _statusUntil = showing ? Time.unscaledTime + 3f : 0f;
    }

    // ------------------------------------------------------------------ grid

    void BuildGrid()
    {
        float top = HeaderH + HeaderBorder, bottom = _L.footer;
        _viewport = NewRect("Grid", _root);
        Place(_viewport, 0, top, _W, _H - top - bottom);
        _viewport.gameObject.AddComponent<RectMask2D>();
        var hit = _viewport.gameObject.AddComponent<Image>();   // so a drag on the gaps still scrolls
        hit.color = Color.clear;

        _content = NewRect("Content", _viewport);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;

        _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.inertia = true;
        _scroll.scrollSensitivity = 24f;
        _scroll.viewport = _viewport;
        _scroll.content = _content;

        bool cars = _step == Step.Car;
        float tile = cars ? _L.car : _L.tile, gap = cars ? _L.carGap : _L.tileGap;
        int max = cars ? _L.carCols : _L.tileCols;
        _cols = RaceFlow.Columns(_W - 8f, tile, gap, max);
        int count = OptionCount;
        int rows = Mathf.Max(1, (count + _cols - 1) / _cols);
        float gridW = _cols * tile + (_cols - 1) * gap;
        float x0 = Mathf.Floor((_W - gridW) / 2f);
        _content.sizeDelta = new Vector2(0f, GridPadY * 2f + rows * tile + (rows - 1) * gap);

        if (count == 0)
        {
            var empty = Mono(_content, "Empty", $"No {RaceFlow.FilterLabel(_filter).ToLowerInvariant()} tracks built yet.", Pal.Slate);
            Place(empty.rectTransform, x0, GridPadY + 4f, gridW, Row);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float x = x0 + (i % _cols) * (tile + gap);
            float y = GridPadY + (i / _cols) * (tile + gap);
            var t = _step switch
            {
                Step.Track => TrackTile(i, tile),
                Step.Series => SeriesTile(i, tile),
                _ => CarTile(i, tile),
            };
            Place(t.rect, x, y, tile, tile);
            _tiles.Add(t);
        }
    }

    // The tile frame shared by every screen: hard shadow, outline ring, fill, and a Button over the lot.
    Tile Frame(int i, float size, string kind)
    {
        var t = new Tile();
        t.rect = NewRect($"Tile_{i}", _content);
        t.rect.sizeDelta = new Vector2(size, size);

        t.shadow = Box(t.rect, "Shadow", 4, 4, size, size, Pal.Ink).GetComponent<Image>();
        t.ring = Box(t.rect, "Ring", -3, -3, size + 6, size + 6, Pal.Line).GetComponent<Image>();
        t.fill = Box(t.rect, "Fill", 0, 0, size, size, Pal.Panel).GetComponent<Image>();
        t.fill.raycastTarget = true;

        var button = t.fill.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        int index = i;
        button.onClick.AddListener(() => TapTile(index));
        var hover = t.fill.gameObject.AddComponent<TileHover>();
        hover.owner = this;
        hover.index = i;
        t.fill.gameObject.name = kind;
        return t;
    }

    // Name row with the blinking cursor in front of it when selected. Two ellipsized copies, so a selection
    // change swaps text rather than re-measuring.
    void NameRow(Tile t, float pad, float y, float width, string name)
    {
        t.cursor = Img(t.rect, "Cursor", RaceFlowArt.Cursor, Pal.Gold);
        Place(t.cursor.rectTransform, pad, y + 5, 4, 7);
        t.name = Mono(t.rect, "Name", "", Pal.Sand);
        t.nameIdle = Fit(t.name, name, width);
        t.nameSelected = Fit(t.name, name, width - 8f);
        t.nameX = pad;
        Place(t.name.rectTransform, pad, y, width, Row);
    }

    Tile TrackTile(int i, float size)
    {
        var track = _tracks[i];
        var t = Frame(i, size, "Track");
        const float pad = 6f, gap = 4f;
        float inner = size - 2f * pad;
        float artH = inner - 2f * gap - 2f * Row;

        var art = ArtBox(t.rect, pad, pad, inner, artH);
        var grid = Raw(art, "Blueprint", RaceFlowArt.Blueprint((int)inner, (int)artH));
        Place(grid.rectTransform, 0, 0, inner, artH);

        int lw = (int)inner - 8, lh = (int)artH - 18;
        t.idleArt = RaceFlowArt.TrackLayout(track.Name, lw, lh, Pal.Sand);
        t.selectedArt = RaceFlowArt.TrackLayout(track.Name, lw, lh, Pal.Gold);
        t.layout = Raw(art, "Layout", t.idleArt);
        Place(t.layout.rectTransform, 4, 14, lw, lh);
        t.layout.enabled = t.idleArt != null;

        t.tag = Silk(art, "Type", RaceFlow.TagLabel(TagOf(track)), Pal.Slate, 1f);
        Place(t.tag.rectTransform, 3, 3, Width(t.tag), 8);
        t.chip = Chip(art, "Length", RaceFlow.Miles(track.LengthMiles), inner, Pal.Ink);

        NameRow(t, pad, pad + artH + gap, inner, track.DisplayName);
        t.sub = Mono(t.rect, "Place", "", Pal.Slate);
        t.sub.text = Fit(t.sub, RaceFlow.IsAuthored(track.Name) ? RaceFlow.Place(track.Name, track.Country) : RaceFlow.NotBuiltNote, inner);
        t.unbuilt = !RaceFlow.IsAuthored(track.Name);
        Place(t.sub.rectTransform, pad, pad + artH + gap + Row + gap, inner, Row);
        return t;
    }

    Tile SeriesTile(int i, float size)
    {
        var s = _allSeries[i];
        int field = i < _seriesField.Count ? _seriesField[i] : 0;
        var t = Frame(i, size, "Series");
        t.open = field > 0;
        const float pad = 6f, gap = 4f;
        float inner = size - 2f * pad;
        float artH = inner - 2f * gap - 2f * Row;

        var art = ArtBox(t.rect, pad, pad, inner, artH);
        uint colour = RaceFlow.SeriesColour(s.ShortName, (int)s.Discipline);
        var stripes = Raw(art, "Stripes", RaceFlowArt.SeriesStripes(colour, t.open, (int)inner, (int)artH));
        Place(stripes.rectTransform, 0, 0, inner, artH);

        // Code, centred and 6 px low, over a 3 px ink hard shadow.
        string code = string.IsNullOrWhiteSpace(s.ShortName) ? "?" : s.ShortName.Trim().ToUpperInvariant();
        t.codeShadow = Big(art, "CodeShadow", code, Pal.Ink);
        t.code = Big(art, "Code", code, t.open ? Pal.Cream : Pal.Slate);
        float cw = Width(t.code), ch = _pixelifySize;
        float cx = Mathf.Floor((inner - cw) / 2f), cy = Mathf.Floor((artH - ch) / 2f) + 3f;
        Place(t.codeShadow.rectTransform, cx + 3, cy + 3, cw + 2, ch + 4);
        Place(t.code.rectTransform, cx, cy, cw + 2, ch + 4);

        // Kind tag on an 80% ink chip, field chip on the right. A long kind is shortened rather than overlapping.
        string chip = RaceFlow.FieldChip(field);
        t.chip = Chip(art, "Field", chip, inner, Pal.Ink);
        string kind = RaceFlow.SeriesKind((int)s.Discipline);
        var probe = Silk(art, "Kind", kind, Pal.Slate, 1f);
        if (Width(probe) + 6f + 3f + 4f + t.chip.transform.parent.GetComponent<RectTransform>().sizeDelta.x + 3f > inner)
            probe.text = kind.Replace("OPEN WHEEL", "OPEN W.");
        float kw = Width(probe);
        var kindBg = Box(art, "KindChip", 3, 3, kw + 6, 12, Pal.Ink80);
        probe.transform.SetParent(kindBg, false);
        Place(probe.rectTransform, 3, 2, kw, 8);
        t.tag = probe;

        NameRow(t, pad, pad + artH + gap, inner, SeriesRoster.Label(s));
        t.sub = Mono(t.rect, "Field", "", t.open ? Pal.Sand : Pal.Slate);
        t.sub.text = Fit(t.sub, RaceFlow.FieldLine(field), inner);
        Place(t.sub.rectTransform, pad, pad + artH + gap + Row + gap, inner, Row);
        return t;
    }

    Tile CarTile(int i, float size)
    {
        var t = Frame(i, size, "Car");
        const float pad = 5f, gap = 3f;
        float inner = size - 2f * pad;
        float artH = inner - 2f * gap - 2f * Row;

        Driver d = i < _drivers.Count ? _drivers[i] : null;
        int number = d != null ? d.CarNumber : PlayerDriver.CarNumber;
        bool mine = d == null || d.CarNumber == PlayerDriver.CarNumber;

        var art = ArtBox(t.rect, pad, pad, inner, artH);
        var asphalt = Raw(art, "Asphalt", RaceFlowArt.Asphalt((int)inner, (int)artH));
        Place(asphalt.rectTransform, 0, 0, inner, artH);

        // Livery at native size, centred on whole pixels. The player's own car with no field keeps its paint.
        Sprite livery = null;
        string name = LiveryName(_carset ?? CupRoster2026.CarsetPrefix, number);
        if (name != null) livery = Resources.Load<Sprite>(name);
        if (livery != null)
        {
            var img = Img(art, "Livery", livery, Color.white);
            float w = livery.rect.width, h = livery.rect.height;
            Place(img.rectTransform, Mathf.Floor((inner - w) / 2f), Mathf.Floor((artH - h) / 2f), w, h);
        }
        else
        {
            var num = Mono(art, "NoPaint", "#" + number, Pal.Slate);
            float w = Width(num);
            Place(num.rectTransform, Mathf.Floor((inner - w) / 2f), Mathf.Floor((artH - Row) / 2f), w, Row);
        }

        if (mine)
        {
            var you = Silk(art, "YouText", "YOU", Pal.DarkInk, 1f);
            float yw = Width(you);
            var outline = Box(art, "You", inner - 2 - (yw + 6) - 1, 1, yw + 8, 14, Pal.Ink);
            var plate = Box(outline, "Plate", 1, 1, yw + 6, 12, Pal.Gold);
            you.transform.SetParent(plate, false);
            Place(you.rectTransform, 3, 2, yw, 8);
        }

        string label = d != null ? SeriesRoster.Label(d) : PlayerDriver.CareerName;
        string surname = d != null ? RaceFlow.Surname(d.FirstName, d.LastName, label) : RaceFlow.Surname(null, null, label);
        if (string.IsNullOrWhiteSpace(surname)) surname = "You";
        NameRow(t, pad, pad + artH + gap, inner, surname);

        float y2 = pad + artH + gap + Row + gap;
        t.number = Mono(t.rect, "Number", "#" + number, Pal.RedText);
        float nw = Width(t.number);
        Place(t.number.rectTransform, pad, y2, nw, Row);
        string team = d != null ? (d.TeamName ?? "") : "No field entered";
        t.sub = Mono(t.rect, "Team", "", Pal.Slate);
        float tw = Mathf.Max(0f, inner - nw - 4f);
        t.sub.text = Fit(t.sub, team, tw);
        Place(t.sub.rectTransform, pad + nw + 4f, y2, tw, Row);
        return t;
    }

    // The art slot: 2 px ink outline outside the box, children clipped to it.
    RectTransform ArtBox(RectTransform parent, float x, float y, float w, float h)
    {
        var outline = Box(parent, "ArtOutline", x - 2, y - 2, w + 4, h + 4, Pal.Ink);
        var art = NewRect("Art", parent);
        Place(art, x, y, w, h);
        art.gameObject.AddComponent<RectMask2D>();
        return art;
    }

    // A Silkscreen chip in the art box's top-right corner (3 px in, 2 px down): ink fill, padding 2 x 3.
    TextMeshProUGUI Chip(RectTransform art, string name, string text, float artW, Color fill)
    {
        var label = Silk(art, name + "Text", text, Pal.Sand, 1f);
        float w = Width(label);
        var bg = Box(art, name, artW - 3 - (w + 6), 2, w + 6, 12, fill);
        label.transform.SetParent(bg, false);
        Place(label.rectTransform, 3, 2, w, 8);
        return label;
    }

    void Restyle(int i)
    {
        if (i < 0 || i >= _tiles.Count) return;
        var t = _tiles[i];
        bool sel = i == _index;
        bool hover = t.hovered && !sel && !Handheld;

        t.fill.color = sel ? Pal.PanelSel : hover ? Pal.PanelHover : Pal.Panel;
        t.ring.color = sel ? Pal.Gold : Pal.Line;
        t.shadow.enabled = sel;
        t.cursor.enabled = sel;
        t.name.text = sel ? t.nameSelected : t.nameIdle;
        t.name.color = sel ? Pal.Cream : Pal.Sand;
        var p = t.name.rectTransform.anchoredPosition;
        t.name.rectTransform.anchoredPosition = new Vector2(t.nameX + (sel ? 8f : 0f), p.y);

        switch (_step)
        {
            case Step.Track:
                t.tag.color = sel ? Pal.Gold : Pal.Slate;
                t.chip.color = sel ? Pal.Cream : Pal.Sand;
                if (t.layout != null && t.idleArt != null) t.layout.texture = sel ? t.selectedArt : t.idleArt;
                if (t.unbuilt)
                {
                    // Not built yet: greyed out. The cursor can still rest on it; it can't be picked.
                    t.fill.color = Pal.ChipOff;
                    t.ring.color = sel ? Pal.Slate : Pal.Line;
                    t.name.color = Pal.Slate;
                    t.tag.color = t.chip.color = Pal.Line;
                    if (t.sub != null) t.sub.color = Pal.Slate;
                    if (t.layout != null)
                    {
                        t.layout.texture = t.idleArt;
                        t.layout.color = new Color(1f, 1f, 1f, 0.25f);
                    }
                }
                break;
            case Step.Series:
                t.tag.color = sel ? Pal.Gold : Pal.Slate;
                t.chip.color = t.open ? (sel ? Pal.Cream : Pal.Sand) : Pal.Slate;
                break;
        }

        // Bring the selected tile's frame above its neighbours' so the shadow and gold ring aren't overdrawn.
        if (sel) t.rect.SetAsLastSibling();
    }

    void HoverChanged(int i, bool on)
    {
        if (i < 0 || i >= _tiles.Count) return;
        _tiles[i].hovered = on;
        Restyle(i);
    }

    // Keep the selected tile (with its ring and shadow) inside the viewport, in whole pixels.
    void ScrollIntoView(int i, bool instant = false)
    {
        if (_content == null || _viewport == null || i < 0 || i >= _tiles.Count) return;
        float view = _viewport.rect.height > 0f ? _viewport.rect.height : _viewport.sizeDelta.y;
        float max = Mathf.Max(0f, _content.sizeDelta.y - view);
        var tile = _tiles[i].rect;
        float top = -tile.anchoredPosition.y - 3f - GridPadY, bottom = -tile.anchoredPosition.y + tile.sizeDelta.y + 4f + GridPadY;
        float y = _content.anchoredPosition.y;
        if (top < y) y = top;
        if (bottom > y + view) y = bottom - view;
        y = Mathf.Clamp(Mathf.Round(y), 0f, max);
        _scroll.StopMovement();
        _content.anchoredPosition = new Vector2(0f, y);
    }

    // ------------------------------------------------------------------ building blocks

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        return rt;
    }

    // Top-left placement in design px, y measured down from the parent's top edge.
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        rt.sizeDelta = new Vector2(Mathf.Round(w), Mathf.Round(h));
    }

    static RectTransform Box(RectTransform parent, string name, float x, float y, float w, float h, Color colour)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        Place(rt, x, y, w, h);
        return rt;
    }

    static Image Img(RectTransform parent, string name, Sprite sprite, Color colour)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = colour;
        img.raycastTarget = false;
        return img;
    }

    static RawImage Raw(RectTransform parent, string name, Texture tex)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI Text(Transform parent, string name, string content, TMP_FontAsset font, float size, Color colour,
                         float trackingPx)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.characterSpacing = trackingPx / size * 100f;
        t.color = colour;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.margin = Vector4.zero;
        t.text = content;
        return t;
    }

    // monogram Bold 16: titles, names, rows, values, buttons.
    TextMeshProUGUI Mono(Transform parent, string name, string content, Color colour) =>
        Text(parent, name, content, _mono, 16f, colour, 0f);

    // Silkscreen 8, tracked: tags, chips, small labels, key hints.
    TextMeshProUGUI Silk(Transform parent, string name, string content, Color colour, float trackingPx) =>
        Text(parent, name, content, _silk, 8f, colour, trackingPx);

    // Pixelify Sans Bold: the series code.
    TextMeshProUGUI Big(Transform parent, string name, string content, Color colour) =>
        Text(parent, name, content, _pixelify, _pixelifySize, colour, 1f);

    static float Width(TMP_Text t) => Mathf.Ceil(t.GetPreferredValues(t.text).x);

    // Cut to `maxWidth` with an ellipsis, the CSS text-overflow the design uses.
    static string Fit(TMP_Text t, string s, float maxWidth)
    {
        s ??= "";
        if (maxWidth <= 0f) return "";
        if (t.GetPreferredValues(s).x <= maxWidth) return s;
        string ellipsis = t.font != null && t.font.HasCharacter('…') ? "…" : "...";
        for (int n = s.Length - 1; n > 0; n--)
        {
            string cut = s.Substring(0, n).TrimEnd() + ellipsis;
            if (t.GetPreferredValues(cut).x <= maxWidth) return cut;
        }
        return ellipsis;
    }

    // Keycap: Silkscreen 8 in ink on cream, padding 1 x 4, 2 px slate drop below. Returns its right edge.
    float Keycap(RectTransform parent, string key, float x, float y)
    {
        var label = Silk(parent, "KeyText", key, Pal.KeyInk, 0f);
        float w = Width(label) + 8f;
        Box(parent, "Key_" + key + "_Drop", x, y + 2, w, 10, Pal.Slate);
        var cap = Box(parent, "Key_" + key, x, y, w, 10, Pal.Cream);
        label.transform.SetParent(cap, false);
        Place(label.rectTransform, 4, 1, w - 8f, 8);
        return x + w;
    }

    float Hint(RectTransform parent, string text, float x, float y)
    {
        var label = Silk(parent, "Hint_" + text, text, Pal.Slate, 1f);
        float w = Width(label);
        Place(label.rectTransform, x, y + 1, w, 8);
        return x + w;
    }

    // Header pill (BACK, filter chips): fill, 2 px ink outline, optional keycap, Silkscreen caption. The rect
    // returned is the pill without its outline; the outline is drawn outside it, CSS-style.
    RectTransform Pill(RectTransform parent, string name, string key, string caption, float tracking, float h,
                       Color fill, Color text, UnityEngine.Events.UnityAction onPress)
    {
        var rt = NewRect(name, parent);
        Box(rt, "Outline", -2, -2, 0, 0, Pal.Ink);   // sized below
        var face = Box(rt, "Face", 0, 0, 0, h, fill);
        float x = 6f;
        float y = Mathf.Floor((h - 8f) / 2f);
        if (!string.IsNullOrEmpty(key))
        {
            x = Keycap(face, key, x, y - 1f) + 5f;
        }
        var label = Silk(face, "Caption", caption, text, tracking);
        float lw = Width(label);
        Place(label.rectTransform, x, y, lw, 8);
        float w = x + lw + 6f - tracking;   // CSS letter-spacing trails the last glyph; padding is measured from the ink
        Place(face, 0, 0, w, h);
        Place((RectTransform)rt.Find("Outline"), -2, -2, w + 4, h + 4);
        rt.sizeDelta = new Vector2(Mathf.Round(w), h);

        var img = face.GetComponent<Image>();
        img.raycastTarget = true;
        var button = face.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => { Deselect(); if (!_loading) onPress(); });
        face.gameObject.name = name;      // "Back" etc. carries the Button, for tests and taps
        rt.gameObject.name = name + "Pill";
        return rt;
    }

    // Footer confirm: red fill, 2 px ink outline, 3 px ink hard shadow, ENTER keycap on a keyboard, monogram caption.
    RectTransform ConfirmButton(RectTransform parent, string verb)
    {
        float h = _L.button;
        var rt = NewRect("ConfirmPill", parent);
        var shadow = Box(rt, "Shadow", 3, 3, 0, h, Pal.Ink);
        var outline = Box(rt, "Outline", -2, -2, 0, h + 4, Pal.Ink);
        var face = Box(rt, "Face", 0, 0, 0, h, Pal.Red);
        float x = 10f;
        if (ShowKeys) x = Keycap(face, ConfirmKey, x, Mathf.Floor((h - 10f) / 2f)) + 6f;
        var label = Mono(face, "Caption", verb, Pal.Cream);
        float lw = Width(label);
        Place(label.rectTransform, x, Mathf.Floor((h - Row) / 2f) + 1f, lw, Row);
        float w = x + lw + 10f;
        Place(face, 0, 0, w, h);
        Place(outline, -2, -2, w + 4, h + 4);
        Place(shadow, 3, 3, w, h);
        rt.sizeDelta = new Vector2(w, h);

        face.GetComponent<Image>().raycastTarget = true;
        var button = face.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => { Deselect(); ConfirmSelection(); });
        face.gameObject.name = verb == "RACE" ? "Race" : "Next";
        return rt;
    }

    // A clicked Button stays the EventSystem's selection, and Enter would then press it again on top of this
    // screen's own Enter. Nothing here is navigated by selection, so let it go.
    static void Deselect()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    static void DestroyNow(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // PC hover: the idle tile's fill lifts to #1B2840.
    class TileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public SingleRaceUI owner;
        public int index;
        public void OnPointerEnter(PointerEventData e) => owner.HoverChanged(index, true);
        public void OnPointerExit(PointerEventData e) => owner.HoverChanged(index, false);
    }
}
