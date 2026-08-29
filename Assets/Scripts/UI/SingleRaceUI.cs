using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Weekend;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// SINGLE RACE: pick a track, a championship and a driver, then go racing. Three steps in one scene.
//
// WHY THIS EXISTS. Until now the only way to race a track other than the reference one was to press Race
// in an editor window and hope nothing overwrote the selection — and something did: NEW SEASON restarts
// the calendar at its opening round, which silently replaced whatever had been picked. There was no way
// in the game itself to say "that track, that series, that driver". With 38 venues built, that is the
// difference between a test build and a game.
//
// WHY ONE SCENE AND NOT THREE. It reads as three screens, but back-navigation is a panel swap rather than
// a scene load, so no step has to smuggle its answer through PlayerPrefs to be read by the next one — the
// choices live in these three fields and are only committed at the end, when the player presses GO on the
// last step. Backing out of the flow therefore changes nothing, which is what a cancel should do.
//
// WHY THE UI IS BUILT IN CODE. The house pattern is an authored canvas plus a thin binder, and that is
// right for a fixed layout like the title screen. This screen is a LIST — 38 tracks, a variable field of
// drivers — so the rows have to be generated whatever happens; authoring them would only mean authoring
// placeholders. Building the whole screen here also means there is nothing serialised to lose, which is
// worth having: a generated scene's button listeners do not survive a save in this project, so a menu
// that wires itself at runtime is the more durable of the two options. It uses the same IronOvalUI kit
// helpers the authored screens do, so it matches them.
public class SingleRaceUI : MonoBehaviour
{
    enum Step { Track, Series, Driver }

    [Tooltip("Scene the flow ends in. Must be in the build settings.")]
    public string raceSceneName = "RaceScene";
    [Tooltip("Scene the BACK option returns to from the first step.")]
    public string titleSceneName = "TitleScreen";
    [Tooltip("How many list rows are visible at once. The list scrolls around the selection.")]
    public int visibleRows = 9;

    // What the player has chosen so far. Nothing is committed until GO on the last step.
    Step _step = Step.Track;
    string _trackId;
    Draftmaster.Data.Series _series;
    Driver _driver;

    // The options on the current step, and where the cursor is in them.
    readonly List<string> _labels = new();
    readonly List<string> _details = new();
    int _index;
    int _scroll;
    bool _loading;

    // Built once in Start, repopulated per step.
    readonly List<TextMeshProUGUI> _rowLabels = new();
    readonly List<TextMeshProUGUI> _rowDetails = new();
    readonly List<Image> _rowCursors = new();
    TextMeshProUGUI _title;
    TextMeshProUGUI _breadcrumb;
    TextMeshProUGUI _status;
    float _statusUntil;

    // Cached option sources so a step does not re-query the database every keypress.
    List<Track> _tracks;
    List<Draftmaster.Data.Series> _allSeries;
    List<Driver> _drivers;

    void Start()
    {
        IronOvalScanlines.Ensure();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        BuildChrome();
        EnterStep(Step.Track);
    }

    void Update()
    {
        if (_loading) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            if (kb.pageDownKey.wasPressedThisFrame) Move(visibleRows);
            if (kb.pageUpKey.wasPressedThisFrame) Move(-visibleRows);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Confirm();
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Back();
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.down.wasPressedThisFrame) Move(1);
            if (pad.dpad.up.wasPressedThisFrame) Move(-1);
            if (pad.buttonSouth.wasPressedThisFrame) Confirm();
            if (pad.buttonEast.wasPressedThisFrame) Back();
        }

        if (_statusUntil > 0f && Time.unscaledTime >= _statusUntil) SetStatus("");
    }

    // ------------------------------------------------------------------ steps

    void EnterStep(Step step)
    {
        _step = step;
        _labels.Clear();
        _details.Clear();
        _index = 0;
        _scroll = 0;

        switch (step)
        {
            case Step.Track: PopulateTracks(); break;
            case Step.Series: PopulateSeries(); break;
            case Step.Driver: PopulateDrivers(); break;
        }

        Redraw();
    }

    // Every track that is actually built. A catalogue row with no geometry is not offered, because
    // choosing it would load a race with no road in it.
    void PopulateTracks()
    {
        _tracks = new List<Track>();
        foreach (var row in TrackCatalog.Playable()) _tracks.Add(row);
        // Grouped by type, then alphabetical — the same order the Track Builder window lists them in.
        _tracks.Sort((a, b) => a.Type != b.Type
            ? a.Type.CompareTo(b.Type)
            : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));

        for (int i = 0; i < _tracks.Count; i++)
        {
            var row = _tracks[i];
            _labels.Add(row.DisplayName.ToUpperInvariant());

            string width = Draftmaster.Tracks.TrackDimensions.TryGet(row.Name, out var dim)
                ? $" · {dim.widthMetres:0.0}m"
                : "";
            _details.Add($"{Nice(row.Type)} · {row.LengthMiles:0.###} mi{width}");

            if (row.Name == TrackSelection.CurrentId) _index = i;
        }
    }

    void PopulateSeries()
    {
        _allSeries = SeriesRoster.AllSeries();
        for (int i = 0; i < _allSeries.Count; i++)
        {
            var s = _allSeries[i];
            _labels.Add(SeriesRoster.Label(s));

            int field = SeriesRoster.Drivers(s).Count;
            _details.Add(field > 0 ? $"{field} entered" : "no field entered");
        }
    }

    void PopulateDrivers()
    {
        _drivers = _series != null ? SeriesRoster.Drivers(_series) : new List<Driver>();
        int playerNumber = PlayerDriver.CarNumber;

        for (int i = 0; i < _drivers.Count; i++)
        {
            var d = _drivers[i];
            _labels.Add(SeriesRoster.Label(d));
            _details.Add(string.IsNullOrWhiteSpace(d.TeamName) ? "" : d.TeamName.ToUpperInvariant());
            if (d.CarNumber == playerNumber) _index = i;
        }

        // A series with nobody entered is a real state (only the top championship has a seeded field),
        // so say so and let the player race anyway as whoever they already are.
        if (_drivers.Count == 0)
        {
            _labels.Add("RACE AS " + (string.IsNullOrEmpty(PlayerDriver.CareerName)
                                      ? $"CAR #{PlayerDriver.CarNumber}"
                                      : PlayerDriver.CareerName.ToUpperInvariant()));
            _details.Add("no field entered in this series");
        }
    }

    // ------------------------------------------------------------------ input

    void Move(int by)
    {
        if (_labels.Count == 0) return;
        _index = Mathf.Clamp(_index + by, 0, _labels.Count - 1);
        Redraw();
    }

    void Confirm()
    {
        if (_labels.Count == 0) return;

        switch (_step)
        {
            case Step.Track:
                _trackId = _tracks[_index].Name;
                EnterStep(Step.Series);
                break;

            case Step.Series:
                _series = _allSeries[_index];
                EnterStep(Step.Driver);
                break;

            case Step.Driver:
                _driver = _drivers != null && _index < _drivers.Count ? _drivers[_index] : null;
                Go();
                break;
        }
    }

    void Back()
    {
        switch (_step)
        {
            case Step.Track:
                if (!string.IsNullOrEmpty(titleSceneName)
                    && Application.CanStreamedLevelBeLoaded(titleSceneName))
                {
                    _loading = true;
                    SceneManager.LoadScene(titleSceneName);
                }
                else SetStatus($"{titleSceneName} isn't in the build settings.");
                break;

            case Step.Series: EnterStep(Step.Track); break;
            case Step.Driver: EnterStep(Step.Series); break;
        }
    }

    // ------------------------------------------------------------------ commit

    // The only place this screen writes anything. Everything above is a choice held in a field.
    void Go()
    {
        if (!TrackSelection.Select(_trackId))
        {
            SetStatus("That track has no layout built.");
            return;
        }

        // Keep the weekend's idea of which championship the player is in aligned with the pick, when the
        // chosen series is one of the three that share a race weekend. The other championships in the
        // Series table (open wheel, dirt) have no weekend, so they leave it alone.
        foreach (var s in SeriesCatalog.All)
            if (_series != null && string.Equals(SeriesCatalog.ShortCode(s), _series.ShortName,
                                                 System.StringComparison.OrdinalIgnoreCase))
                SeriesCatalog.PlayerSeries = s;

        // PlayerDriver.CarNumber is read-only — the keys are the public contract.
        if (_driver != null)
        {
            if (_driver.CarNumber > 0) PlayerPrefs.SetInt(PlayerDriver.NumberKey, _driver.CarNumber);
            string name = string.Join(" ", _driver.FirstName, _driver.LastName).Trim();
            if (!string.IsNullOrWhiteSpace(name)) PlayerPrefs.SetString(PlayerDriver.NameKey, name);
            PlayerPrefs.Save();
        }

        // A single race is one race: no practice, no qualifying, and the session is live the moment the
        // scene loads — the same shape the title screen's EXHIBITION row uses.
        RaceWeekend.Current = RaceWeekend.Session.Race;
        RaceWeekend.SessionLive = true;

        if (!Application.CanStreamedLevelBeLoaded(raceSceneName))
        {
            SetStatus($"{raceSceneName} isn't in the build settings.");
            return;
        }

        _loading = true;
        SceneManager.LoadScene(raceSceneName);
    }

    // ------------------------------------------------------------------ drawing

    void Redraw()
    {
        // Keep the cursor inside the visible window.
        if (_index < _scroll) _scroll = _index;
        if (_index >= _scroll + visibleRows) _scroll = _index - visibleRows + 1;
        _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, _labels.Count - visibleRows));

        var theme = PixelUITheme.Instance;
        for (int i = 0; i < _rowLabels.Count; i++)
        {
            int option = _scroll + i;
            bool used = option < _labels.Count;
            bool selected = used && option == _index;

            _rowLabels[i].gameObject.SetActive(used);
            _rowDetails[i].gameObject.SetActive(used);
            _rowCursors[i].gameObject.SetActive(selected);
            if (!used) continue;

            _rowLabels[i].text = _labels[option];
            _rowDetails[i].text = _details[option];
            _rowLabels[i].color = selected ? theme.text : theme.textDisabled;
            _rowDetails[i].color = selected ? theme.text : theme.plateLight;
        }

        _title.text = _step switch
        {
            Step.Track => "SELECT TRACK",
            Step.Series => "SELECT SERIES",
            _ => "SELECT DRIVER",
        };

        // The breadcrumb is what stops the three steps feeling like three unrelated menus.
        string crumb = _step == Step.Track ? "" : TrackCatalog.DisplayName(_trackId).ToUpperInvariant();
        if (_step == Step.Driver && _series != null) crumb += "   >   " + SeriesRoster.Label(_series);
        int more = Mathf.Max(0, _labels.Count - visibleRows);
        _breadcrumb.text = string.IsNullOrEmpty(crumb)
            ? $"{_labels.Count} TRACKS BUILT" + (more > 0 ? "   (SCROLL FOR MORE)" : "")
            : crumb;
    }

    void SetStatus(string message)
    {
        if (_status == null) return;
        _status.text = message ?? "";
        _statusUntil = string.IsNullOrEmpty(message) ? 0f : Time.unscaledTime + 3f;
    }

    // ------------------------------------------------------------------ chrome

    void BuildChrome()
    {
        var theme = PixelUITheme.Instance;
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);   // the project's pixel canvas grid
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        var root = (RectTransform)canvas.transform;

        var backdrop = new GameObject("Backdrop", typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(root, false);
        backdrop.color = theme.ink;
        Stretch((RectTransform)backdrop.transform);

        _title = IronOvalUI.Label(root, "Title", "SELECT TRACK", IronOvalUI.Role.Header, theme.text);
        Place((RectTransform)_title.transform, new Vector2(0f, 1f), new Vector2(24f, -20f),
              new Vector2(400f, 28f), TextAlignmentOptions.TopLeft);

        _breadcrumb = IronOvalUI.Label(root, "Breadcrumb", "", IronOvalUI.Role.Body, theme.plateLight);
        Place((RectTransform)_breadcrumb.transform, new Vector2(0f, 1f), new Vector2(24f, -50f),
              new Vector2(560f, 18f), TextAlignmentOptions.TopLeft);

        for (int i = 0; i < visibleRows; i++)
        {
            float y = -78f - i * 22f;

            var cursor = IronOvalUI.Cursor(root, $"Cursor_{i}");
            Place((RectTransform)cursor.transform, new Vector2(0f, 1f), new Vector2(26f, y - 4f),
                  new Vector2(10f, 12f), TextAlignmentOptions.Left);
            _rowCursors.Add(cursor);

            var label = IronOvalUI.Label(root, $"Row_{i}", "", IronOvalUI.Role.Body, theme.textDisabled);
            Place((RectTransform)label.transform, new Vector2(0f, 1f), new Vector2(44f, y),
                  new Vector2(300f, 20f), TextAlignmentOptions.TopLeft);
            _rowLabels.Add(label);

            var detail = IronOvalUI.Label(root, $"Detail_{i}", "", IronOvalUI.Role.Body, theme.plateLight);
            Place((RectTransform)detail.transform, new Vector2(0f, 1f), new Vector2(350f, y),
                  new Vector2(266f, 20f), TextAlignmentOptions.TopLeft);
            _rowDetails.Add(detail);
        }

        var help = IronOvalUI.Label(root, "Help",
                                    "W/S or ARROWS  MOVE     ENTER  SELECT     ESC  BACK",
                                    IronOvalUI.Role.Body, theme.plateLight);
        Place((RectTransform)help.transform, new Vector2(0f, 0f), new Vector2(24f, 22f),
              new Vector2(560f, 18f), TextAlignmentOptions.BottomLeft);

        _status = IronOvalUI.Label(root, "Status", "", IronOvalUI.Role.Body, theme.text);
        Place((RectTransform)_status.transform, new Vector2(0f, 0f), new Vector2(24f, 42f),
              new Vector2(560f, 18f), TextAlignmentOptions.BottomLeft);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Anchor to a corner and offset in design pixels, so the layout holds at any window size.
    static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size,
                      TextAlignmentOptions alignment)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, anchor.y);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        var text = rect.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    static string Nice(TrackType type) => type switch
    {
        TrackType.Superspeedway => "SUPERSPEEDWAY",
        TrackType.Speedway => "SPEEDWAY",
        TrackType.ShortTrack => "SHORT TRACK",
        TrackType.RoadCourse => "ROAD COURSE",
        _ => "DIRT",
    };
}
