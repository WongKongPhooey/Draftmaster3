using System;
using System.Collections.Generic;
using System.Linq;
using Draftmaster.Controls;
using Draftmaster.Data;
using Draftmaster.MasterDraft;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// MASTER DRAFT's menu: Garage (the screen the mode opens on), Series (DM2's championships, raced in RaceScene),
// Challenges (this week's DM2 events and their chapters, raced the same way, with a prize reveal), then the card mode's Home, Go Race, My Team, Packs, Trades — and the pack-opening, card-detail, card-picker and race-result views over them. The card
// screens stay reachable until the rework retires them (Docs/MasterDraft-Rework.md, row 9). One scene (Assets/Scenes/MasterDraft.unity, built by
// Draftmaster > Master Draft > Build Master Draft Scene), one component.
//
// WHY THE UI IS BUILT IN CODE. Same reasoning as SingleRaceUI: almost everything here is a generated list
// or grid (a collection, a pack, 38 tracks, a trade board), a generated scene's button listeners don't
// survive a save in this project, and nothing serialised means nothing to lose. It is built from the same
// IronOvalUI kit pieces and type roles as the authored screens, on the kit's 640x360 grid.
//
// INPUT. One focus, moved spatially: arrows / WASD / d-pad step to the nearest item in that direction,
// Enter / Space / E / pad South confirm, Esc / Backspace / pad East back out (overlay -> screen -> Garage ->
// title). Tab / Shift+Tab, 1-8 or the shoulder buttons change screen. Every item is also a Button, so a tap
// or a click picks it outright.
public class MasterDraftUI : MonoBehaviour
{
    [Tooltip("Scene BACK returns to from Home. Must be in the build settings.")]
    public string titleSceneName = "TitleScreen";

    enum Tab { Garage, Series, Challenges, Home, GoRace, MyTeam, Packs, Trades }
    enum Overlay { None, Reveal, Detail, Picker, WantType, Result, SeriesResult, ChallengeResult }

    static readonly string[] TabNames = { "Garage", "Series", "Challenges", "Home", "Go Race", "My Team", "Packs", "Trades" };

    class Item
    {
        public string key;
        public RectTransform rect;
        public Action confirm;
        public bool enabled = true;
    }

    // ---- chrome
    RectTransform _root, _content, _tabsRow;
    TextMeshProUGUI _coins, _badge, _status, _help;
    int _helpVersion = -1;
    float _statusUntil;

    // ---- focus
    readonly List<Item> _items = new();
    int _focus;
    RectTransform _focusFrame;
    Func<Vector2, bool> _navOverride;

    // ---- state
    Tab _tab = Tab.Garage;
    Overlay _overlay = Overlay.None;
    bool _busy = true, _leaving;

    // Pack reveal
    List<CardInstance> _reveal = new();
    int _revealed;
    string _revealTitle = "";

    // My Team
    CardType _filter = CardType.Driver;
    int _page;
    CardInstance _detail;

    // Go Race
    List<Track> _tracks;
    int _trackIndex, _trackScroll;
    string _driverUid;
    const int TrackRows = 8;

    // Picker (trades)
    List<CardInstance> _pickList = new();
    string _pickTitle = "";
    Action<CardInstance> _onPick;
    int _pickPage;
    CardInstance _listing;

    // Set by BuildOffline (EditMode tests): draw against this profile and catalogue with no session.
    MasterDraftProfile _profileOverride;
    CarCatalog _carsOverride;

    MasterDraftSession S => MasterDraftSession.Instance;
    MasterDraftProfile P => _profileOverride ?? (S != null ? S.Profile : null);
    static PixelUITheme T => PixelUITheme.Instance;

    // ------------------------------------------------------------------ lifecycle

    async void Start()
    {
        IronOvalScanlines.Ensure();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // Back from a Master Draft race: the career's track/driver keys go back where they were, and the
        // mode stops claiming a race is on.
        OneOffRacePrefs.Restore();
        if (GameSession.CurrentMode == GameSession.Mode.MasterDraft) GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
        AIDifficulty.MasterDraftStrengthScale = 0f;
        PlayerCarLivery.MasterDraftLivery = null;

        BuildChrome();
        SetStatus("Loading your collection…", 0f);
        Rebuild();

        var session = MasterDraftSession.Ensure();
        await session.InitialiseAsync();
        if (this == null) return;
        _busy = false;

        if (!session.Ready)
        {
            SetStatus(session.Status, 0f);
            Rebuild();
            return;
        }

        session.Pending = null;
        if (session.LastRace != null) _overlay = Overlay.Result;
        if (session.LastSeries?.plan != null)
        {
            // Back from a championship round: its result over the series it belongs to.
            _tab = Tab.Series;
            _overlay = Overlay.SeriesResult;
            var played = session.Series?.Get(session.LastSeries.plan.seriesId);
            if (played != null) { _seriesMenu = played.menu; _seriesPick = played.id; }
        }
        if (session.LastChallenge?.plan != null)
        {
            // Back from a challenge: its result (and the prize reveal) over the chapter it belongs to.
            _tab = Tab.Challenges;
            _overlay = Overlay.ChallengeResult;
            _prizeRevealed = false;
            _challengeEvent = session.LastChallenge.plan.eventId;
            _challengeChapter = session.LastChallenge.plan.chapterId;
        }
        SetStatus("");
        Rebuild();
    }

    void Update()
    {
        if (_leaving) return;

        if (_help != null && _helpVersion != InputGlyphs.Version)
        {
            _helpVersion = InputGlyphs.Version;
            _help.text = HelpText();
        }
        if (_statusUntil > 0f && Time.unscaledTime >= _statusUntil) SetStatus("");

        if (_busy) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Nav(Vector2.up);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Nav(Vector2.down);
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Nav(Vector2.left);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Nav(Vector2.right);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Confirm();
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Back();
            if (kb.tabKey.wasPressedThisFrame) StepTab(kb.shiftKey.isPressed ? -1 : 1);
            if (kb.rKey.wasPressedThisFrame) Shortcut();
            if (kb.digit1Key.wasPressedThisFrame) SetTab(Tab.Garage);
            if (kb.digit2Key.wasPressedThisFrame) SetTab(Tab.Series);
            if (kb.digit3Key.wasPressedThisFrame) SetTab(Tab.Challenges);
            if (kb.digit4Key.wasPressedThisFrame) SetTab(Tab.Home);
            if (kb.digit5Key.wasPressedThisFrame) SetTab(Tab.GoRace);
            if (kb.digit6Key.wasPressedThisFrame) SetTab(Tab.MyTeam);
            if (kb.digit7Key.wasPressedThisFrame) SetTab(Tab.Packs);
            if (kb.digit8Key.wasPressedThisFrame) SetTab(Tab.Trades);
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) Nav(Vector2.up);
            if (pad.dpad.down.wasPressedThisFrame) Nav(Vector2.down);
            if (pad.dpad.left.wasPressedThisFrame) Nav(Vector2.left);
            if (pad.dpad.right.wasPressedThisFrame) Nav(Vector2.right);
            if (PadInput.Control(pad, PadBindings.Confirm).wasPressedThisFrame) Confirm();
            if (PadInput.Control(pad, PadBindings.Back).wasPressedThisFrame) Back();
            if (pad.leftShoulder.wasPressedThisFrame) StepTab(-1);
            if (pad.rightShoulder.wasPressedThisFrame) StepTab(1);
            if (pad.buttonNorth.wasPressedThisFrame) Shortcut();
        }
    }

    string HelpText()
    {
        string shortcut = _overlay == Overlay.Reveal ? "Reveal All"
            : _overlay != Overlay.None ? ""
            : _tab == Tab.MyTeam ? "Auto Pick"
            : _tab == Tab.Garage ? "Upgrade" : "";
        if (InputGlyphs.UsingGamepad)
            return $"D-Pad  Move     {InputGlyphs.PadName(PadBindings.Confirm)}  Select     {InputGlyphs.PadName(PadBindings.Back)}  Back" +
                   $"     {InputGlyphs.PadName(PadButton.LeftShoulder)}/{InputGlyphs.PadName(PadButton.RightShoulder)}  Screen" +
                   (shortcut.Length > 0 ? $"     {InputGlyphs.PadName(PadButton.North)}  {shortcut}" : "");
        if (InputGlyphs.UsingTouch) return "Tap  Pick";
        return "Arrows  Move     Enter  Select     Esc  Back     Tab  Screen" + (shortcut.Length > 0 ? $"     R  {shortcut}" : "");
    }

    // ------------------------------------------------------------------ navigation

    void SetTab(Tab tab)
    {
        if (_busy) return;
        _tab = tab;
        _overlay = Overlay.None;
        _page = 0;
        _focus = 0;
        _lastFocusKey = null;
        Rebuild();
    }

    void StepTab(int by)
    {
        if (_busy || _overlay != Overlay.None) return;
        int n = TabNames.Length;
        SetTab((Tab)((((int)_tab + by) % n + n) % n));
    }

    void Back()
    {
        if (_busy) return;
        switch (_overlay)
        {
            case Overlay.Detail:
                CloseDetail();
                return;
            case Overlay.SeriesResult:
                CloseSeriesResult();
                return;
            case Overlay.ChallengeResult:
                CloseChallengeResult();
                return;
            case Overlay.Result:
                if (S != null) S.LastRace = null;
                _overlay = Overlay.None;
                Rebuild();
                return;
            case Overlay.Reveal:
                _overlay = Overlay.None;
                Rebuild();
                return;
            case Overlay.Picker:
            case Overlay.WantType:
                _overlay = Overlay.None;
                _listing = null;
                Rebuild();
                return;
        }

        if (_tab != Tab.Garage) { SetTab(Tab.Garage); return; }
        LeaveToTitle();
    }

    void LeaveToTitle()
    {
        if (!string.IsNullOrEmpty(titleSceneName) && Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            _leaving = true;
            SceneManager.LoadScene(titleSceneName);
        }
        else SetStatus($"{titleSceneName} isn't in the build settings.");
    }

    // The screen's one extra button: Reveal All while opening a pack, Auto Pick on My Team, Upgrade in the
    // Garage.
    void Shortcut()
    {
        if (_busy) return;
        if (_overlay == Overlay.Reveal) { RevealAll(); return; }
        if (_tab == Tab.MyTeam && _overlay == Overlay.None) AutoPick();
        if (_tab == Tab.Garage && _overlay == Overlay.None) UpgradeSelectedCar();
    }

    void Confirm()
    {
        if (_busy || _items.Count == 0) return;
        var item = _items[Mathf.Clamp(_focus, 0, _items.Count - 1)];
        if (!item.enabled) return;
        item.confirm?.Invoke();
    }

    void Tap(Item item)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (_busy || _leaving) return;
        int i = _items.IndexOf(item);
        if (i < 0) return;
        _focus = i;
        ApplyFocus();
        if (item.enabled) item.confirm?.Invoke();
    }

    void Nav(Vector2 dir)
    {
        if (_items.Count == 0) return;
        if (_navOverride != null && _navOverride(dir)) return;

        var from = Centre(_items[Mathf.Clamp(_focus, 0, _items.Count - 1)]);
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == _focus) continue;
            var d = Centre(_items[i]) - from;
            float along = Vector2.Dot(d, dir);
            if (along <= 1f) continue;
            float perp = (d - along * dir).magnitude;
            float score = along + perp * 2f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        if (best < 0) return;
        _focus = best;
        ApplyFocus();
    }

    Vector2 Centre(Item item) => _root.InverseTransformPoint(item.rect.TransformPoint(item.rect.rect.center));

    string FocusKey => _items.Count > 0 && _focus < _items.Count ? _items[_focus].key : null;

    void FocusKeyOrFirst(string key)
    {
        int i = key == null ? -1 : _items.FindIndex(it => it.key == key);
        if (i < 0) i = _items.FindIndex(it => it.enabled);
        _focus = Mathf.Max(0, i);
        ApplyFocus();
    }

    void ApplyFocus()
    {
        if (_focusFrame != null) Discard(_focusFrame.gameObject);
        _focusFrame = null;
        if (_items.Count == 0) return;
        _focus = Mathf.Clamp(_focus, 0, _items.Count - 1);
        var item = _items[_focus];
        _focusFrame = MasterDraftCardView.Outline(item.rect, T.gold, 2f, 3f);
        _focusFrame.SetAsLastSibling();
        _lastFocusKey = item.key;
    }

    string _lastFocusKey;

    // ------------------------------------------------------------------ rebuild

    void Rebuild()
    {
        string keep = _lastFocusKey;
        ClearChildren(_content);
        _items.Clear();
        _focusFrame = null;
        _navOverride = null;

        DrawChromeState();

        if (P == null)
        {
            Text(_content, S != null && !S.Loading && !string.IsNullOrEmpty(S.Status) ? S.Status : "Loading…",
                 IronOvalUI.Role.Body, T.textDim, new Vector2(0f, 0f), new Vector2(600f, 20f));
            return;
        }

        switch (_overlay)
        {
            case Overlay.Reveal: BuildReveal(); break;
            case Overlay.Detail: BuildDetail(); break;
            case Overlay.Picker: BuildPicker(); break;
            case Overlay.WantType: BuildWantType(); break;
            case Overlay.Result: BuildResult(); break;
            case Overlay.SeriesResult: BuildSeriesResult(); break;
            case Overlay.ChallengeResult: BuildChallengeResult(); break;
            default:
                switch (_tab)
                {
                    case Tab.Garage: BuildGarage(); break;
                    case Tab.Series: BuildSeries(); break;
                    case Tab.Challenges: BuildChallenges(); break;
                    case Tab.Home: BuildHome(); break;
                    case Tab.GoRace: BuildGoRace(); break;
                    case Tab.MyTeam: BuildMyTeam(); break;
                    case Tab.Packs: BuildPacks(); break;
                    case Tab.Trades: BuildTrades(); break;
                }
                break;
        }

        _helpVersion = -1;
        FocusKeyOrFirst(keep);
    }

    // ------------------------------------------------------------------ Garage

    // The collection: every catalogue car as its livery, owned cars with their class stars and locked ones
    // greyed out, filtered by carset / stars / team / manufacturer. The panel on the right is the picked car:
    // class, gears bar to the next class and the paid upgrade (CarProgression).
    const int GarageCols = 5, GarageRows = 3;
    const float GarageTileW = 76f, GarageTileH = 52f, GarageStepX = 80f, GarageStepY = 56f, GarageGridTop = -30f;
    const float GaragePanelX = 410f, GaragePanelW = 190f;
    static readonly Color LockedTint = new Color(0.3f, 0.3f, 0.3f, 0.75f);

    string _garageCarset, _garageTeam, _garageMake;   // null = all
    int _garageRarity;                                  // 0 = all
    string _garageCar;                                  // the car the panel shows
    int _garagePage;

    CarCatalog _carsLoaded;
    readonly Dictionary<string, Sprite> _liveries = new();

    // The car catalogue: the test override, the local backend's copy, or Resources/MasterDraft/Cars.json.
    CarCatalog Cars
    {
        get
        {
            if (_carsOverride != null) return _carsOverride;
            if (S?.Backend is LocalMasterDraftBackend local && local.Cars != null) return local.Cars;
            if (_carsLoaded != null) return _carsLoaded;
            MasterDraftJsonInstaller.Install();
            var text = Resources.Load<TextAsset>(CarCatalog.ResourcePath);
            _carsLoaded = text == null ? null : CarCatalog.FromJson(text.text);
            return _carsLoaded;
        }
    }

    Sprite Livery(CarDef car)
    {
        if (car == null || string.IsNullOrEmpty(car.livery)) return null;
        if (!_liveries.TryGetValue(car.livery, out var sprite))
            _liveries[car.livery] = sprite = Resources.Load<Sprite>(car.livery);
        return sprite;
    }

    // What the garage is showing, for the screen and for EditMode tests (GarageScreenTests).
    public int GarageFilteredCount { get; private set; }   // cars passing the filters, all pages
    public int GarageOwnedCount { get; private set; }      // of those, owned
    public int GarageLockedCount { get; private set; }     // of those, not owned
    public int GaragePageCount { get; private set; }
    public int GarageTilesShown { get; private set; }      // tiles on this page
    public string GarageSelectedCar => _garageCar;

    // Draw the screens against a profile and catalogue with no session, backend or scene — the EditMode tests'
    // way in (Start never runs in edit mode). Opens on the Garage.
    public void BuildOffline(MasterDraftProfile profile, CarCatalog cars)
    {
        _profileOverride = profile;
        _carsOverride = cars;
        _busy = false;
        if (_root == null) BuildChrome();
        _tab = Tab.Garage;
        _overlay = Overlay.None;
        Rebuild();
    }

    public void SetGarageFilter(string carset, int rarity, string team, string manufacturer)
    {
        _garageCarset = string.IsNullOrEmpty(carset) ? null : carset;
        _garageRarity = Mathf.Clamp(rarity, 0, CarCatalog.MaxRarity);
        _garageTeam = string.IsNullOrEmpty(team) ? null : team;
        _garageMake = string.IsNullOrEmpty(manufacturer) ? null : manufacturer;
        _garagePage = 0;
        if (_content != null) Rebuild();
    }

    public void SetGaragePage(int page)
    {
        _garagePage = page;
        if (_content != null) Rebuild();
    }

    // Show a car in the panel; an owned one also becomes the profile's selected car (saved).
    public void SelectGarageCar(string carId)
    {
        _garageCar = carId;
        var owned = P?.Car(carId);
        if (owned != null && P.selectedCar != carId)
        {
            P.selectedCar = carId;
            SaveTeam();
            return;
        }
        if (_content != null) Rebuild();
    }

    // Class up the picked car, paying coins (CarProgression.TryUpgrade). Returns why not when it can't.
    // Offline-only, like the rest of the garage: online this has to become a backend operation so a client
    // can't spend coins it hasn't got.
    public UpgradeCheck UpgradeSelectedCar()
    {
        var p = P;
        var owned = p?.Car(_garageCar);
        if (owned == null) { SetStatus("You don't own this car yet."); return UpgradeCheck.NeedGears; }
        var check = CarProgression.TryUpgrade(owned, ref p.coins);
        var def = Cars?.Get(owned.id);
        string name = def != null ? $"#{def.number} {def.driver}" : owned.id;
        if (check == UpgradeCheck.Ok) SaveTeam($"{name} is now Class {owned.carClass}.");
        else SetStatus(UpgradeReason(owned, check, p.coins));
        return check;
    }

    static string UpgradeReason(OwnedCar car, UpgradeCheck check, int coins) => check switch
    {
        UpgradeCheck.MaxClass => "Max class.",
        UpgradeCheck.NeedGears => $"{CarProgression.GearsToNextClass(car)} more gears to Class {car.carClass + 1}.",
        UpgradeCheck.NeedCoins => $"Class {car.carClass + 1} costs {CarProgression.UpgradeCost(car.carClass):N0} coins, you have {coins:N0}.",
        _ => "",
    };

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // Does a car pass the filters? skip names one filter to ignore (for building that filter's options).
    bool GarageMatch(CarDef c, string skip = null) =>
        (skip == "set" || _garageCarset == null || Same(c.carset, _garageCarset))
        && (skip == "stars" || _garageRarity == 0 || c.rarity == _garageRarity)
        && (skip == "team" || _garageTeam == null || Same(c.team, _garageTeam))
        && (skip == "make" || _garageMake == null || Same(c.manufacturer, _garageMake));

    // Owned first, then catalogue order (carset, number).
    List<CarDef> GarageList(CarCatalog cars)
    {
        var p = P;
        var owned = new List<CarDef>();
        var locked = new List<CarDef>();
        foreach (var c in cars.Cars)
            if (GarageMatch(c)) (p.Owns(c.id) ? owned : locked).Add(c);
        owned.AddRange(locked);
        return owned;
    }

    // The values the team / manufacturer buttons step through: what the other filters leave, so a press
    // never lands on an empty grid.
    List<string> FilterOptions(CarCatalog cars, string which, Func<CarDef, string> value) => cars.Cars
        .Where(c => GarageMatch(c, which))
        .Select(value).Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(v => v, StringComparer.Ordinal).ToList();

    static string NextOption(List<string> options, string current)
    {
        if (options.Count == 0) return null;
        int i = current == null ? -1 : options.FindIndex(o => Same(o, current));
        return i + 1 >= options.Count ? null : options[i + 1];
    }

    void CycleGarageFilter(string which)
    {
        var cars = Cars;
        if (cars == null) return;
        switch (which)
        {
            case "set": _garageCarset = NextOption(cars.Carsets.Select(s => s.id).ToList(), _garageCarset); break;
            case "stars": _garageRarity = _garageRarity >= CarCatalog.MaxRarity ? 0 : _garageRarity + 1; break;
            case "team": _garageTeam = NextOption(FilterOptions(cars, "team", c => c.team), _garageTeam); break;
            case "make": _garageMake = NextOption(FilterOptions(cars, "make", c => c.manufacturer), _garageMake); break;
        }
        // A carset or stars change can strand the team / make picked: drop those back to All.
        if (_garageTeam != null && !FilterOptions(cars, "team", c => c.team).Any(t => Same(t, _garageTeam))) _garageTeam = null;
        if (_garageMake != null && !FilterOptions(cars, "make", c => c.manufacturer).Any(m => Same(m, _garageMake))) _garageMake = null;
        _garagePage = 0;
        _lastFocusKey = "gf." + which;
        Rebuild();
    }

    void BuildGarage()
    {
        var p = P;
        var cars = Cars;
        GarageFilteredCount = GarageOwnedCount = GarageLockedCount = GarageTilesShown = 0;
        GaragePageCount = 1;
        if (cars == null || cars.Count == 0)
        {
            Text(_content, $"No car catalogue (Resources/{CarCatalog.ResourcePath}.json).", IronOvalUI.Role.Body, T.danger,
                 Vector2.zero, new Vector2(600f, 20f));
            return;
        }

        // Filters along the top: each press steps to the next value, then back round to All.
        string setName = _garageCarset == null ? "All" : cars.GetCarset(_garageCarset)?.name ?? _garageCarset;
        var filters = new (string key, string caption)[]
        {
            ("set", "Set " + setName),
            ("stars", "Stars " + (_garageRarity == 0 ? "All" : _garageRarity.ToString())),
            ("team", "Team " + (_garageTeam ?? "All")),
            ("make", "Make " + (_garageMake ?? "All")),
        };
        for (int i = 0; i < filters.Length; i++)
        {
            string which = filters[i].key;
            ButtonItem(_content, "gf." + which, filters[i].caption, new Vector2(i * 100f, 0f), 96f, () => CycleGarageFilter(which));
        }

        var list = GarageList(cars);
        int perPage = GarageCols * GarageRows;
        int pages = Mathf.Max(1, (list.Count + perPage - 1) / perPage);
        _garagePage = Mathf.Clamp(_garagePage, 0, pages - 1);
        GarageFilteredCount = list.Count;
        GarageOwnedCount = list.Count(c => p.Owns(c.id));
        GarageLockedCount = list.Count - GarageOwnedCount;
        GaragePageCount = pages;

        var prev = IronOvalUI.Button(_content, "GaragePrev", "<", new Vector2(26f, 0f));
        TopLeft((RectTransform)prev.transform, new Vector2(GaragePanelX + 100f, 0f), new Vector2(26f, UIButton.KitPx));
        AddItem((RectTransform)prev.transform, "gp.prev", () => GaragePage(-1, list), _garagePage > 0);
        var next = IronOvalUI.Button(_content, "GarageNext", ">", new Vector2(26f, 0f));
        TopLeft((RectTransform)next.transform, new Vector2(GaragePanelX + 130f, 0f), new Vector2(26f, UIButton.KitPx));
        AddItem((RectTransform)next.transform, "gp.next", () => GaragePage(1, list), _garagePage < pages - 1);
        var pageText = Text(_content, $"{_garagePage + 1}/{pages}", IronOvalUI.Role.Data, T.textDim,
                            new Vector2(GaragePanelX + 156f, -4f), new Vector2(34f, 16f));
        pageText.alignment = TextAlignmentOptions.TopRight;

        // The picked car: kept while it's in the catalogue, else the profile's selected car, else the first tile.
        if (cars.Get(_garageCar) == null) _garageCar = p.SelectedCar()?.id;
        if (cars.Get(_garageCar) == null) _garageCar = list.Count > 0 ? list[0].id : null;

        for (int i = 0; i < perPage; i++)
        {
            int index = _garagePage * perPage + i;
            if (index >= list.Count) break;
            var def = list[index];
            var tile = GarageTile(def, p.Car(def.id),
                                  new Vector2((i % GarageCols) * GarageStepX, GarageGridTop - (i / GarageCols) * GarageStepY));
            if (def.id == p.selectedCar) MasterDraftCardView.Outline(tile, T.confirm, 1f, 0f);
            string id = def.id;
            AddItem(tile, "car." + id, () => { _lastFocusKey = "car." + id; SelectGarageCar(id); });
            GarageTilesShown++;
        }

        if (list.Count == 0)
            Text(_content, "No cars match these filters.", IronOvalUI.Role.Data, T.textDim, new Vector2(0f, GarageGridTop - 10f),
                 new Vector2(390f, 18f));

        Text(_content, $"{list.Count} cars   {GarageOwnedCount} owned   {GarageLockedCount} locked", IronOvalUI.Role.Data, T.textDim,
             new Vector2(0f, GarageGridTop - GarageRows * GarageStepY - 2f), new Vector2(396f, 16f));

        GaragePanel(cars, cars.Get(_garageCar), p.Car(_garageCar));

        // Stepping off the side of the grid turns the page.
        _navOverride = dir =>
        {
            string key = FocusKey;
            if (key == null || !key.StartsWith("car.") || Mathf.Abs(dir.x) < 0.5f) return false;
            int at = list.FindIndex(c => "car." + c.id == key) - _garagePage * perPage;
            if (at < 0) return false;
            int col = at % GarageCols;
            if (dir.x > 0f && col == GarageCols - 1 && _garagePage < pages - 1) { GaragePage(1, list); return true; }
            if (dir.x < 0f && col == 0 && _garagePage > 0) { GaragePage(-1, list); return true; }
            return false;
        };
    }

    void GaragePage(int by, List<CarDef> list)
    {
        int perPage = GarageCols * GarageRows;
        int pages = Mathf.Max(1, (list.Count + perPage - 1) / perPage);
        _garagePage = Mathf.Clamp(_garagePage + by, 0, pages - 1);
        int index = Mathf.Clamp(_garagePage * perPage, 0, Mathf.Max(0, list.Count - 1));
        _lastFocusKey = list.Count > 0 ? "car." + list[index].id : null;
        Rebuild();
    }

    // One car: its livery (greyed when locked) with class stars under it, or "Locked".
    RectTransform GarageTile(CarDef def, OwnedCar owned, Vector2 pos)
    {
        var go = new GameObject("Car_" + def.id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_content, false);
        var rt = (RectTransform)go.transform;
        TopLeft(rt, pos, new Vector2(GarageTileW, GarageTileH));
        go.GetComponent<Image>().color = def.id == _garageCar ? T.plate : owned != null ? T.plateDeep : T.ink;

        var livery = new GameObject("Livery", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        livery.transform.SetParent(rt, false);
        TopLeft(livery.rectTransform, new Vector2((GarageTileW - 64f) / 2f, -4f), new Vector2(64f, 32f));
        livery.raycastTarget = false;
        livery.preserveAspect = true;
        livery.sprite = Livery(def);
        if (livery.sprite == null)
        {
            livery.color = Color.clear;
            Text(livery.rectTransform, "#" + def.number, IronOvalUI.Role.Data, T.textDim, new Vector2(4f, -8f), new Vector2(56f, 16f));
        }
        else livery.color = owned != null ? Color.white : LockedTint;

        if (owned != null)
        {
            float w = CarProgression.MaxClass * 10f - 2f;
            var stars = IronOvalUI.StatCells(rt, "Class", owned.carClass, CarProgression.MaxClass);
            TopLeft(stars, new Vector2((GarageTileW - w) / 2f, -39f), new Vector2(w, 10f));
        }
        else
        {
            var locked = Text(rt, "Locked", IronOvalUI.Role.Data, T.textDisabled, new Vector2(0f, -36f), new Vector2(GarageTileW, 16f));
            locked.name = "Locked";
            locked.alignment = TextAlignmentOptions.Top;
        }
        return rt;
    }

    // The picked car: big livery, who and what it is, class, gears bar and the upgrade.
    void GaragePanel(CarCatalog cars, CarDef def, OwnedCar owned)
    {
        float x = GaragePanelX, w = GaragePanelW;
        if (def == null)
        {
            Text(_content, "Pick a car.", IronOvalUI.Role.Data, T.textDim, new Vector2(x, GarageGridTop), new Vector2(w, 16f));
            return;
        }

        var big = new GameObject("PanelLivery", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        big.transform.SetParent(_content, false);
        TopLeft(big.rectTransform, new Vector2(x + (w - 128f) / 2f, GarageGridTop), new Vector2(128f, 64f));
        big.raycastTarget = false;
        big.preserveAspect = true;
        big.sprite = Livery(def);
        big.color = big.sprite == null ? Color.clear : owned != null ? Color.white : LockedTint;

        float y = GarageGridTop - 68f;
        Text(_content, $"#{def.number} {def.driver}", IronOvalUI.Role.Body, owned != null ? T.gold : T.textDim,
             new Vector2(x, y), new Vector2(w, 18f)).overflowMode = TextOverflowModes.Ellipsis;
        y -= 18f;
        string set = cars.GetCarset(def.carset)?.name ?? def.carset;
        Text(_content, $"{set}  {def.team}  {def.manufacturer}", IronOvalUI.Role.Data, T.text, new Vector2(x, y), new Vector2(w, 16f));
        y -= 16f;
        Text(_content, $"{def.rarity}-Star {def.type}", IronOvalUI.Role.Data, T.text, new Vector2(x, y), new Vector2(w, 16f));
        y -= 18f;

        if (owned == null)
        {
            Text(_content, "Locked. Win it in a challenge or series to add it to your garage.", IronOvalUI.Role.Data, T.textDim,
                 new Vector2(x, y), new Vector2(w, 48f), wrap: true);
            return;
        }

        float starsW = CarProgression.MaxClass * 10f - 2f;
        Text(_content, $"Class {owned.carClass}", IronOvalUI.Role.Data, T.gold, new Vector2(x, y), new Vector2(w - starsW - 4f, 16f));
        var stars = IronOvalUI.StatCells(_content, "PanelClass", owned.carClass, CarProgression.MaxClass);
        TopLeft(stars, new Vector2(x + w - starsW, y - 3f), new Vector2(starsW, 10f));
        y -= 18f;

        // Gears bar to the next class.
        var bar = new GameObject("GearsBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(_content, false);
        var barRt = (RectTransform)bar.transform;
        TopLeft(barRt, new Vector2(x, y), new Vector2(w, 8f));
        var barImage = bar.GetComponent<Image>();
        barImage.color = T.plateDeep;
        barImage.raycastTarget = false;
        var fill = MasterDraftCardView.Box(barRt, "Fill", T.gold, 1f);
        fill.anchorMax = new Vector2(CarProgression.ClassProgress(owned), 1f);
        y -= 10f;

        bool max = CarProgression.IsMaxClass(owned);
        string gears = max
            ? $"Max class   {owned.gears} gears"
            : $"{owned.gears}/{CarProgression.ClassGears(owned.carClass)} gears to Class {owned.carClass + 1}";
        Text(_content, gears, IronOvalUI.Role.Data, T.textDim, new Vector2(x, y), new Vector2(w, 16f));
        y -= 20f;

        var check = CarProgression.CanUpgrade(owned, P.coins);
        string caption = max ? "Max Class" : $"Upgrade  {CarProgression.UpgradeCost(owned.carClass):N0}";
        ButtonItem(_content, "g.upgrade", caption, new Vector2(x, y), w, () => UpgradeSelectedCar(), check == UpgradeCheck.Ok);
        if (check != UpgradeCheck.Ok && !max)
            Text(_content, UpgradeReason(owned, check, P.coins), IronOvalUI.Role.Data, T.caution,
                 new Vector2(x, y - UIButton.KitPx - 2f), new Vector2(w, 16f)).overflowMode = TextOverflowModes.Ellipsis;
    }

    // ------------------------------------------------------------------ Series

    // DM2's series: the menus down the left, the picked menu's series in the middle, the picked series on the
    // right — entry requirements, its tracks, the garage cars that can enter, and Start Championship / Race Round N
    // (MasterDraftRaceLauncher.LaunchSeriesRound). A finished round comes back here as the SeriesResult overlay.
    const int SeriesRows = 10;
    const float SeriesMenuW = 130f, SeriesListX = 136f, SeriesListW = 200f, SeriesPanelX = 342f, SeriesPanelW = 258f;

    string _seriesMenu, _seriesPick, _seriesEntry, _seriesQuitArmed;
    int _seriesScroll;
    DraftSeriesCatalog _seriesOverride, _seriesLoaded;

    DraftSeriesCatalog SeriesData =>
        _seriesOverride ?? S?.Series ?? (_seriesLoaded ??= MasterDraftSession.LoadSeries());

    // What the Series screen is showing, for EditMode tests (SeriesScreenTests).
    public string SeriesPicked => _seriesPick;
    public string SeriesEntryCar => _seriesEntry;
    public int SeriesEligibleCount { get; private set; }
    public bool SeriesCanStart { get; private set; }
    public bool SeriesInProgress { get; private set; }

    // Draw the Series screen against a profile and catalogues with no session (EditMode tests).
    public void BuildOfflineSeries(MasterDraftProfile profile, CarCatalog cars, DraftSeriesCatalog series, string seriesId = null)
    {
        _profileOverride = profile;
        _carsOverride = cars;
        _seriesOverride = series;
        _busy = false;
        if (_root == null) BuildChrome();
        _tab = Tab.Series;
        _overlay = Overlay.None;
        PickSeries(seriesId);
    }

    // Show a series (and its menu); null = keep the current pick.
    public void PickSeries(string seriesId)
    {
        var data = SeriesData;
        var def = data?.Get(seriesId);
        if (def != null)
        {
            _seriesMenu = def.menu;
            _seriesPick = def.id;
            _seriesEntry = null;
            _seriesQuitArmed = null;
            var menu = data.GetMenu(def.menu);
            int at = menu == null ? 0 : menu.series.FindIndex(x => x.id == def.id);
            if (at < _seriesScroll || at >= _seriesScroll + SeriesRows) _seriesScroll = Mathf.Max(0, at - SeriesRows + 1);
        }
        if (_content != null) Rebuild();
    }

    void BuildSeries()
    {
        var p = P;
        var data = SeriesData;
        var cars = Cars;
        SeriesEligibleCount = 0;
        SeriesCanStart = SeriesInProgress = false;
        if (data == null || data.Menus.Count == 0 || cars == null)
        {
            Text(_content, data == null || data.Menus.Count == 0
                    ? $"No series data (Resources/{DraftSeriesCatalog.ResourcePath}.json)."
                    : $"No car catalogue (Resources/{CarCatalog.ResourcePath}.json).",
                 IronOvalUI.Role.Body, T.danger, Vector2.zero, new Vector2(600f, 20f));
            return;
        }

        var menu = data.GetMenu(_seriesMenu) ?? data.Menus[0];
        _seriesMenu = menu.id;
        var series = data.Get(_seriesPick);
        if (series == null || series.menu != menu.id) series = menu.series[0];
        _seriesPick = series.id;

        // Menus.
        Text(_content, "Series", IronOvalUI.Role.Data, T.gold, Vector2.zero, new Vector2(SeriesMenuW, 16f));
        for (int i = 0; i < data.Menus.Count && i < SeriesRows; i++)
        {
            var m = data.Menus[i];
            bool open = DraftSeriesCatalog.MenuUnlocked(m, p.level);
            string id = m.id;
            RowItem(_content, "sm." + id, m.name, new Vector2(0f, -18f - i * 20f), new Vector2(SeriesMenuW, 18f), () =>
            {
                _seriesMenu = id;
                _seriesPick = null;
                _seriesEntry = null;
                _seriesScroll = 0;
                _lastFocusKey = "sm." + id;
                Rebuild();
            }, id == menu.id, open ? m.series.Count.ToString() : $"Lv{DraftSeriesCatalog.MenuMinLevel(m)}");
        }

        // The menu's series, scrolled.
        int count = menu.series.Count;
        int pickAt = menu.series.FindIndex(x => x.id == series.id);
        if (pickAt < _seriesScroll) _seriesScroll = pickAt;
        if (pickAt >= _seriesScroll + SeriesRows) _seriesScroll = pickAt - SeriesRows + 1;
        _seriesScroll = Mathf.Clamp(_seriesScroll, 0, Mathf.Max(0, count - SeriesRows));
        Text(_content, menu.name + (count > SeriesRows ? $"  {pickAt + 1}/{count}" : ""), IronOvalUI.Role.Data, T.gold,
             new Vector2(SeriesListX, 0f), new Vector2(SeriesListW, 16f));
        for (int i = 0; i < SeriesRows; i++)
        {
            int index = _seriesScroll + i;
            if (index >= count) break;
            var s = menu.series[index];
            var running = p.Championship(s.id);
            string right = running != null ? $"R{running.round + 1}/{running.Length}"
                : !Restrictions.LevelUnlocked(s.restriction, p.level) ? $"Lv{s.restriction.minLevel}"
                : (p.SeriesRecord(s.id)?.titles ?? 0) > 0 ? "Won" : "";
            string id = s.id;
            RowItem(_content, "sr" + i, s.name, new Vector2(SeriesListX, -18f - i * 20f), new Vector2(SeriesListW, 18f), () =>
            {
                _seriesPick = id;
                _seriesEntry = null;
                _seriesQuitArmed = null;
                _lastFocusKey = "se.go";
                Rebuild();
            }, s.id == series.id, right);
        }

        // Up/down on the list walks every series of the menu, not just the visible ones.
        _navOverride = dir =>
        {
            string key = FocusKey;
            if (key == null || !key.StartsWith("sr") || Mathf.Abs(dir.y) < 0.5f) return false;
            int row = int.Parse(key.Substring(2));
            int target = _seriesScroll + row + (dir.y > 0f ? -1 : 1);
            if (target < 0 || target >= count) return dir.y < 0f;
            if (target < _seriesScroll) _seriesScroll = target;
            if (target >= _seriesScroll + SeriesRows) _seriesScroll = target - SeriesRows + 1;
            _seriesPick = menu.series[target].id;
            _seriesEntry = null;
            _seriesQuitArmed = null;
            _lastFocusKey = "sr" + (target - _seriesScroll);
            Rebuild();
            return true;
        };

        SeriesPanel(p, cars, series);
    }

    void SeriesPanel(MasterDraftProfile p, CarCatalog cars, SeriesDef s)
    {
        float x = SeriesPanelX, w = SeriesPanelW, y = 0f;
        Text(_content, s.name, IronOvalUI.Role.Body, T.gold, new Vector2(x, y), new Vector2(w, 18f)).overflowMode = TextOverflowModes.Ellipsis;
        y -= 20f;
        string prize = s.prize == null || s.prize.IsNone ? "none" : s.prize.code;
        Text(_content, $"{s.RoundCount} rounds   AI {SeriesRules.DifficultyPercent(s.aiLevel)}%   Prize {prize}", IronOvalUI.Role.Data,
             T.text, new Vector2(x, y), new Vector2(w, 16f)).overflowMode = TextOverflowModes.Ellipsis;
        y -= 16f;
        Text(_content, string.Join("   ", Restrictions.Describe(s.restriction)), IronOvalUI.Role.Data, T.text,
             new Vector2(x, y), new Vector2(w, 32f), wrap: true);
        y -= 32f;
        var tracks = s.rounds.Select(r => TrackCatalog.Nicify(r.track)).ToList();   // short: five rounds fit two lines
        Text(_content, string.Join(", ", tracks), IronOvalUI.Role.Data, T.textDim, new Vector2(x, y), new Vector2(w, 32f), wrap: true);
        y -= 34f;

        var running = p.Championship(s.id);
        SeriesInProgress = running != null;
        if (running != null)
        {
            var def = cars.Get(running.carId);
            var owned = p.Car(running.carId);
            int place = SeriesRules.Position(running);
            int points = SeriesRules.Standings(running).FirstOrDefault(e => e.carId == running.carId)?.points ?? 0;
            Text(_content, $"Round {running.round + 1}/{running.Length}   P{place} of {running.table.Count}   {points} pts",
                 IronOvalUI.Role.Data, T.gold, new Vector2(x, y), new Vector2(w, 16f));
            y -= 16f;
            string car = def == null ? running.carId : $"#{def.number} {def.driver}";
            Text(_content, owned == null ? $"{car}  (no longer owned)" : $"{car}   Class {owned.carClass}", IronOvalUI.Role.Data,
                 owned == null ? T.danger : T.text, new Vector2(x, y), new Vector2(w, 16f)).overflowMode = TextOverflowModes.Ellipsis;
            y -= 16f;
            var next = SeriesRules.NextRound(running);
            if (next != null)
                Text(_content, $"Next  {TrackName(next.track)}   {SeriesRules.NextRaceLaps(running)} laps   AI {SeriesRules.DifficultyPercent(running.aiLevel)}%",
                     IronOvalUI.Role.Data, T.text, new Vector2(x, y), new Vector2(w, 16f)).overflowMode = TextOverflowModes.Ellipsis;
            y -= 22f;
            ButtonItem(_content, "se.go", $"Race Round {running.round + 1}", new Vector2(x, y), 164f, () => RaceSeriesRound(s.id), owned != null && next != null);
            ButtonItem(_content, "se.quit", _seriesQuitArmed == s.id ? "Sure?" : "Quit", new Vector2(x + 170f, y), w - 170f, () => QuitChampionship(s.id));
            SeriesRecordLine(p, s, x, w);
            return;
        }

        // Not running: pick a garage car that can enter.
        var eligible = Restrictions.EligibleGarage(s.restriction, cars, p.garage, p.level);
        SeriesEligibleCount = eligible.Count;
        if (eligible.All(c => c.id != _seriesEntry)) _seriesEntry = SeriesRace.DefaultEntry(s, p, cars)?.id;
        var entry = cars.Get(_seriesEntry);
        var ownedEntry = p.Car(_seriesEntry);
        if (entry != null && ownedEntry != null)
        {
            string caption = (eligible.Count > 1 ? "< " : "") + $"#{entry.number} {entry.driver}  C{ownedEntry.carClass}" + (eligible.Count > 1 ? " >" : "");
            ButtonItem(_content, "se.car", caption, new Vector2(x, y), w, () => CycleSeriesEntry(eligible));
            y -= UIButton.KitPx + 2f;
            Text(_content, $"{eligible.Count} of {p.garage.Count} garage cars can enter", IronOvalUI.Role.Data, T.textDim,
                 new Vector2(x, y), new Vector2(w, 16f));
        }
        else
        {
            // Why not: the level, else the selected garage car's first reason.
            string why = !Restrictions.LevelUnlocked(s.restriction, p.level)
                ? $"Reach level {s.restriction.minLevel} (you're level {p.level})."
                : "No car in your garage can enter: " + FirstReason(s, p, cars);
            Text(_content, why, IronOvalUI.Role.Data, T.caution, new Vector2(x, y), new Vector2(w, 32f), wrap: true);
            y -= UIButton.KitPx + 2f;
        }
        y -= 22f;
        SeriesCanStart = entry != null && SeriesRules.CanEnter(s, entry, p.garage, p.level).Eligible;
        ButtonItem(_content, "se.go", "Start Championship", new Vector2(x, y), w, () => StartChampionship(s), SeriesCanStart);
        SeriesRecordLine(p, s, x, w);
    }

    void SeriesRecordLine(MasterDraftProfile p, SeriesDef s, float x, float w)
    {
        var rec = p.SeriesRecord(s.id);
        if (rec == null || rec.championships == 0) return;
        Text(_content, $"Run {rec.championships}   Titles {rec.titles}   Best P{rec.bestFinish}", IronOvalUI.Role.Data, T.textDim,
             new Vector2(x, -220f), new Vector2(w, 16f));
    }

    static string TrackName(string trackId)
    {
        string name = TrackCatalog.DisplayName(trackId);
        return string.IsNullOrEmpty(name) ? trackId : name;
    }

    // The first thing stopping the profile's selected car from entering.
    static string FirstReason(SeriesDef s, MasterDraftProfile p, CarCatalog cars)
    {
        var car = cars.Get(p.SelectedCar()?.id);
        if (car == null) return "your garage is empty.";
        var check = SeriesRules.CanEnter(s, car, p.garage, p.level);
        return check.Eligible ? "" : check.Reasons[0].message + ".";
    }

    void CycleSeriesEntry(List<OwnedCar> eligible)
    {
        if (eligible.Count == 0) return;
        int i = eligible.FindIndex(c => c.id == _seriesEntry);
        _seriesEntry = eligible[(i + 1) % eligible.Count].id;
        _lastFocusKey = "se.car";
        Rebuild();
    }

    // Start the picked series in the picked car (DM2: lock in the car, reset the points) and stay on the screen
    // with Race Round 1 under the cursor. False (and why, on the status line) if the car can't enter.
    public bool StartPickedChampionship()
    {
        var s = SeriesData?.Get(_seriesPick);
        return s != null && StartChampionship(s);
    }

    bool StartChampionship(SeriesDef s)
    {
        var p = P;
        var cars = Cars;
        var car = cars?.Get(_seriesEntry);
        var check = SeriesRules.CanEnter(s, car, p.garage, p.level);
        if (!check.Eligible) { SetStatus(check.Reasons.Count > 0 ? check.Reasons[0].message : "Can't enter."); return false; }
        p.StartChampionship(s, car, cars);
        _lastFocusKey = "se.go";
        SaveTeam($"{s.name}: championship started in the #{car.number}.");
        return true;
    }

    void QuitChampionship(string seriesId)
    {
        if (_seriesQuitArmed != seriesId)
        {
            _seriesQuitArmed = seriesId;
            _lastFocusKey = "se.quit";
            SetStatus("Press again to quit this championship. Its points are lost.");
            Rebuild();
            return;
        }
        _seriesQuitArmed = null;
        P.AbandonChampionship(seriesId);
        _lastFocusKey = "se.go";
        SaveTeam("Championship abandoned.");
    }

    void RaceSeriesRound(string seriesId)
    {
        string error = MasterDraftRaceLauncher.LaunchSeriesRound(seriesId);
        if (error != null) { SetStatus(error); return; }
        _leaving = true;
    }

    // What a finished round paid, over the Series screen.
    void BuildSeriesResult()
    {
        var o = S?.LastSeries;
        if (o == null || o.plan == null) { _overlay = Overlay.None; BuildSeries(); return; }

        var win = IronOvalUI.Window(_content, "SeriesResult", new Vector2(400f, 222f));
        TopLeft(win, new Vector2(100f, -4f), new Vector2(400f, 222f));
        Text(win, $"{o.plan.seriesName}  Round {o.plan.round}/{o.plan.rounds}", IronOvalUI.Role.Header, T.gold,
             new Vector2(14f, -10f), new Vector2(372f, 20f)).overflowMode = TextOverflowModes.Ellipsis;

        var lines = new List<string> { TrackName(o.plan.trackId) };
        var r = o.rewards;
        if (r == null) lines.Add(string.IsNullOrEmpty(o.error) ? "This round couldn't be scored." : o.error);
        else
        {
            lines.Add((r.position > 0 ? $"Finished P{r.position}" : "Not classified") + $"   +{r.round.playerPoints} pts");
            if (r.championshipOver)
                lines.Add($"Championship over: P{r.championshipPosition}   +{r.money:N0} coins");
            else
            {
                var running = P.Championship(o.plan.seriesId);
                if (running != null) lines.Add($"Championship P{SeriesRules.Position(running)} after {running.round} of {running.Length}");
            }
            lines.Add($"XP +{r.xp}" + (r.LevelsGained > 0 ? $"   Level up! Now level {r.levelAfter}" : ""));
            if (r.gears > 0) lines.Add($"+{r.gears} gears to the #{o.plan.number}");
            foreach (var prize in r.prizes)
            {
                var car = Cars?.Get(prize.carId);
                string name = car != null ? $"#{car.number} {car.driver}" : prize.carId;
                lines.Add(prize.newCar ? $"Prize car: {name}!" : $"Prize: +{prize.gears} gears for the {name}");
            }
            lines.Add($"Coins  {r.coins:N0}");
        }
        Text(win, string.Join("\n", lines), IronOvalUI.Role.Data, T.text, new Vector2(14f, -36f), new Vector2(372f, 144f), wrap: true);
        ButtonItem(win, "res.ok", "Continue", new Vector2(140f, -184f), 120f, CloseSeriesResult);
    }

    void CloseSeriesResult()
    {
        string id = S?.LastSeries?.plan?.seriesId;
        if (S != null) S.LastSeries = null;
        _overlay = Overlay.None;
        _tab = Tab.Series;
        _lastFocusKey = "se.go";
        PickSeries(id);
    }

    // ------------------------------------------------------------------ Challenges

    // DM2's challenges ("Events"): the events down the left (this week's first, with their progress; the rest with
    // when they're back), the picked event's chapters in the middle (won / locked / level), and the picked chapter
    // on the right — track, laps, AI, prize, entry requirements, the garage car to race (or the car the chapter hands
    // over), every reason it can't start, and Start Challenge (MasterDraftRaceLauncher.LaunchChallenge). A finished
    // chapter comes back as the ChallengeResult overlay, which reveals the prize.
    const int ChallengeRows = 11;
    const float ChallengeEventW = 170f, ChallengeListX = 176f, ChallengeListW = 170f, ChallengePanelX = 352f, ChallengePanelW = 248f;
    const float ChallengeRowStep = 19f, ChallengeRowH = 17f;

    string _challengeEvent, _challengeChapter, _challengeEntry;
    int _challengeWeekOverride;   // > 0: BuildOfflineChallenges' week instead of the calendar's
    ChallengeCatalog _challengesOverride, _challengesLoaded;
    MasterDraftSession.ChallengeOutcome _challengeOutcomeOverride;
    bool _prizeRevealed;
    int _prizeShown;

    ChallengeCatalog ChallengeData =>
        _challengesOverride ?? S?.Challenges ?? (_challengesLoaded ??= MasterDraftSession.LoadChallenges());

    int ChallengeWeek => _challengeWeekOverride > 0 ? _challengeWeekOverride : MasterDraftSession.ChallengeWeek(ChallengeData);

    MasterDraftSession.ChallengeOutcome ChallengeOutcome => _challengeOutcomeOverride ?? S?.LastChallenge;

    // What the Challenges screen is showing, for EditMode tests (ChallengesScreenTests).
    public string ChallengePickedEvent => _challengeEvent;
    public string ChallengePickedChapter => _challengeChapter;
    public string ChallengeEntryCar => _challengeEntry;
    public int ChallengeShownWeek { get; private set; }
    public int ChallengeLiveCount { get; private set; }
    public bool ChallengeCanStart { get; private set; }
    public string ChallengeReasons { get; private set; } = "";   // why it can't start, one per line; "" when it can
    public string ChallengePrizeText { get; private set; } = "";  // the revealed prize's caption
    public string ChallengePrizeSprite { get; private set; } = ""; // the revealed prize's livery sprite

    // Draw the Challenges screen against a profile and catalogues with no session (EditMode tests). week = the
    // cycle week to show; chapterId = the chapter to pick (null = the first live event's next chapter).
    public void BuildOfflineChallenges(MasterDraftProfile profile, CarCatalog cars, ChallengeCatalog challenges, int week,
        string chapterId = null)
    {
        _profileOverride = profile;
        _carsOverride = cars;
        _challengesOverride = challenges;
        _challengeWeekOverride = week;
        _busy = false;
        if (_root == null) BuildChrome();
        _tab = Tab.Challenges;
        _overlay = Overlay.None;
        PickChallenge(chapterId);
    }

    // Show a finished chapter's result over the screen (EditMode tests), as coming back from the race does.
    public void BuildOfflineChallengeResult(MasterDraftProfile profile, CarCatalog cars, ChallengeCatalog challenges,
        MasterDraftSession.ChallengeOutcome outcome)
    {
        _profileOverride = profile;
        _carsOverride = cars;
        _challengesOverride = challenges;
        _challengeOutcomeOverride = outcome;
        _busy = false;
        if (_root == null) BuildChrome();
        _tab = Tab.Challenges;
        _overlay = Overlay.ChallengeResult;
        _prizeRevealed = false;
        _prizeShown = 0;
        _challengeEvent = outcome?.plan?.eventId;
        _challengeChapter = outcome?.plan?.chapterId;
        Rebuild();
    }

    // Show a chapter (and its event); null = keep the current pick.
    public void PickChallenge(string chapterId)
    {
        var chapter = ChallengeData?.GetChapter(chapterId);
        if (chapter != null)
        {
            _challengeEvent = chapter.eventId;
            _challengeChapter = chapter.id;
            _challengeEntry = null;
        }
        if (_content != null) Rebuild();
    }

    void BuildChallenges()
    {
        var p = P;
        var data = ChallengeData;
        var cars = Cars;
        ChallengeLiveCount = 0;
        ChallengeCanStart = false;
        ChallengeReasons = "";
        if (data == null || data.Events.Count == 0 || cars == null)
        {
            Text(_content, data == null || data.Events.Count == 0
                    ? $"No challenge data (Resources/{ChallengeCatalog.ResourcePath}.json)."
                    : $"No car catalogue (Resources/{CarCatalog.ResourcePath}.json).",
                 IronOvalUI.Role.Body, T.danger, Vector2.zero, new Vector2(600f, 20f));
            return;
        }

        int week = ChallengeWeek;
        ChallengeShownWeek = Draftmaster.MasterDraft.Challenges.CycleWeek(week, data.CycleWeeks);

        // This week's events first, then the rest in catalogue order.
        var live = Draftmaster.MasterDraft.Challenges.LiveEvents(data, week);
        ChallengeLiveCount = live.Count;
        var ordered = new List<ChallengeEvent>(live);
        foreach (var e in data.Events) if (!ordered.Contains(e)) ordered.Add(e);

        var ev = data.Get(_challengeEvent);
        if (ev == null) ev = ordered[0];
        _challengeEvent = ev.id;
        var chapter = data.GetChapter(_challengeChapter);
        if (chapter == null || chapter.eventId != ev.id)
        {
            // An event opens on its next chapter to win.
            var progress = Draftmaster.MasterDraft.Challenges.Progress(ev, p.challengeRecords);
            chapter = ev.chapters[Mathf.Clamp(progress.nextChapter, 0, ev.chapters.Count - 1)];
            _challengeEntry = null;
        }
        _challengeChapter = chapter.id;

        // Events.
        Text(_content, $"Week {ChallengeShownWeek} of {data.CycleWeeks}", IronOvalUI.Role.Data, T.gold, Vector2.zero,
             new Vector2(ChallengeEventW, 16f));
        for (int i = 0; i < ordered.Count && i < ChallengeRows; i++)
        {
            var e = ordered[i];
            bool isLive = live.Contains(e);
            string right;
            if (isLive)
            {
                var progress = Draftmaster.MasterDraft.Challenges.Progress(e, p.challengeRecords);
                right = $"{progress.completed}/{progress.total}";
            }
            else
            {
                int ahead = Draftmaster.MasterDraft.Challenges.WeeksUntilLive(e, week, data.CycleWeeks);
                right = ahead < 0 ? "Off" : ahead == 1 ? "Next wk" : $"In {ahead} wk";
            }
            string id = e.id;
            var row = RowItem(_content, "ce" + i, e.name, new Vector2(0f, -18f - i * ChallengeRowStep), new Vector2(ChallengeEventW, ChallengeRowH), () =>
            {
                _challengeEvent = id;
                _challengeChapter = null;
                _challengeEntry = null;
                _lastFocusKey = "ce" + ordered.FindIndex(x => x.id == id);
                Rebuild();
            }, e.id == ev.id, right);
            if (!isLive)
                foreach (var t in row.rect.GetComponentsInChildren<TMP_Text>(true)) if (t.color != T.gold) t.color = T.textDim;
        }

        // The event's chapters.
        Text(_content, ev.name + (ev.IsProgression ? "" : "  (replay)"), IronOvalUI.Role.Data, T.gold,
             new Vector2(ChallengeListX, 0f), new Vector2(ChallengeListW, 16f)).overflowMode = TextOverflowModes.Ellipsis;
        for (int i = 0; i < ev.chapters.Count && i < ChallengeRows; i++)
        {
            var c = ev.chapters[i];
            var record = Draftmaster.MasterDraft.Challenges.FindRecord(p.challengeRecords, c.id);
            bool unlocked = Draftmaster.MasterDraft.Challenges.ChapterUnlocked(ev, i, p.challengeRecords);
            string right = record != null && record.Completed ? (record.completions > 1 && !ev.IsProgression ? $"Won x{record.completions}" : "Won")
                : !unlocked ? "Locked"
                : !Restrictions.LevelUnlocked(c.restriction, p.level) ? $"Lv{c.restriction.minLevel}"
                : record != null && record.bestFinish > 0 ? $"P{record.bestFinish}" : "";
            string id = c.id;
            RowItem(_content, "cc" + i, $"{i + 1}. {c.name}", new Vector2(ChallengeListX, -18f - i * ChallengeRowStep),
                new Vector2(ChallengeListW, ChallengeRowH), () =>
                {
                    _challengeChapter = id;
                    _challengeEntry = null;
                    _lastFocusKey = "ch.go";
                    Rebuild();
                }, c.id == chapter.id, right);
        }

        ChallengePanel(p, cars, data, ev, chapter, week);
    }

    void ChallengePanel(MasterDraftProfile p, CarCatalog cars, ChallengeCatalog data, ChallengeEvent ev, ChallengeChapter c, int week)
    {
        float x = ChallengePanelX, w = ChallengePanelW, y = 0f;
        Text(_content, c.name, IronOvalUI.Role.Body, T.gold, new Vector2(x, y), new Vector2(w, 18f)).overflowMode = TextOverflowModes.Ellipsis;
        y -= 20f;
        if (!string.IsNullOrEmpty(c.description))
        {
            Text(_content, c.description, IronOvalUI.Role.Data, T.textDim, new Vector2(x, y), new Vector2(w, 32f), wrap: true);
            y -= 34f;
        }
        Text(_content, $"{TrackName(c.track)}   {Draftmaster.MasterDraft.Challenges.LapsToRun(c)} laps   AI {SeriesRules.DifficultyPercent(c.aiLevel)}%",
             IronOvalUI.Role.Data, T.text, new Vector2(x, y), new Vector2(w, 16f)).overflowMode = TextOverflowModes.Ellipsis;
        y -= 16f;
        Text(_content, "Prize  " + ChallengePrizeLine(ev, c, p, cars), IronOvalUI.Role.Data, T.text, new Vector2(x, y),
             new Vector2(w, 16f)).overflowMode = TextOverflowModes.Ellipsis;
        y -= 16f;
        var reqs = c.ProvidesCar ? new List<string> { $"Level: {c.restriction.minLevel}+" } : Restrictions.Describe(c.restriction);
        Text(_content, string.Join("   ", reqs), IronOvalUI.Role.Data, T.text, new Vector2(x, y), new Vector2(w, 16f))
            .overflowMode = TextOverflowModes.Ellipsis;
        y -= 16f;
        string moment = ChallengeRace.MomentNote(c);
        if (moment.Length > 0)
        {
            Text(_content, moment, IronOvalUI.Role.Data, T.textDim, new Vector2(x, y), new Vector2(w, 32f), wrap: true);
            y -= 32f;
        }
        y -= 4f;

        // The car: the one the chapter hands over, else a garage car that can enter.
        CarDef car;
        if (c.ProvidesCar)
        {
            string providedId = Draftmaster.MasterDraft.Challenges.ProvidedCarId(c);
            car = cars.Get(providedId);
            string name = car != null ? $"#{car.number} {car.driver}" : $"#{providedId?.Substring(providedId.LastIndexOf('_') + 1)}";
            Text(_content, $"You race the {name} (provided)", IronOvalUI.Role.Data, T.text, new Vector2(x, y), new Vector2(w, 16f))
                .overflowMode = TextOverflowModes.Ellipsis;
            y -= UIButton.KitPx + 2f;
            _challengeEntry = null;
        }
        else
        {
            var eligible = Restrictions.EligibleGarage(c.restriction, cars, p.garage, p.level);
            if (eligible.All(o => o.id != _challengeEntry)) _challengeEntry = ChallengeRace.DefaultEntry(c, p, cars)?.id;
            car = cars.Get(_challengeEntry);
            var owned = p.Car(_challengeEntry);
            if (car != null && owned != null)
            {
                string caption = (eligible.Count > 1 ? "< " : "") + $"#{car.number} {car.driver}  C{owned.carClass}" + (eligible.Count > 1 ? " >" : "");
                ButtonItem(_content, "ch.car", caption, new Vector2(x, y), w, () => CycleChallengeEntry(eligible));
            }
            else
            {
                // Nobody can enter: say why against the garage's picked car.
                car = cars.Get(p.SelectedCar()?.id);
                Text(_content, "No car in your garage can enter.", IronOvalUI.Role.Data, T.caution, new Vector2(x, y), new Vector2(w, 16f));
            }
            y -= UIButton.KitPx + 2f;
        }

        var entry = Draftmaster.MasterDraft.Challenges.CanEnter(ev, c, car, p.garage, p.level, week, p.challengeRecords, data.CycleWeeks);
        ChallengeCanStart = entry.Eligible && (c.ProvidesCar || _challengeEntry != null);
        if (!entry.Eligible)
        {
            var lines = new List<string>();
            if (!entry.live)
            {
                int ahead = Draftmaster.MasterDraft.Challenges.WeeksUntilLive(ev, week, data.CycleWeeks);
                lines.Add(ahead < 0 ? "This event isn't scheduled." : $"Not running this week. Back in {ahead} week{(ahead == 1 ? "" : "s")} (weeks {string.Join(", ", ev.weeks)}).");
            }
            if (!entry.chapterUnlocked) lines.Add("Win the previous chapter first.");
            foreach (var r in entry.restriction.Reasons) lines.Add(r.message + ".");
            ChallengeReasons = string.Join("\n", lines);
            Text(_content, ChallengeReasons, IronOvalUI.Role.Data, T.caution, new Vector2(x, y), new Vector2(w, 34f), wrap: true);
        }
        y -= 36f;
        // The chapter's record (won / best finish) is on its row in the list.
        ButtonItem(_content, "ch.go", "Start Challenge", new Vector2(x, y), w, () => StartChallenge(c.id), ChallengeCanStart);
    }

    // The prize, in a line: the car (and its gears), "a one-star car", the alt paint — or that it's been paid.
    static string ChallengePrizeLine(ChallengeEvent ev, ChallengeChapter c, MasterDraftProfile p, CarCatalog cars)
    {
        var prize = c.prize;
        if (prize == null || prize.IsNone) return "none";
        var record = Draftmaster.MasterDraft.Challenges.FindRecord(p.challengeRecords, c.id);
        if (ev.IsProgression && record != null && record.PrizeCollected) return "collected";
        string Name(string id)
        {
            var def = cars.Get(id);
            return def != null ? $"#{def.number} {def.driver}" : id;
        }
        string first = prize.cars != null && prize.cars.Count > 0 ? prize.cars[0] : null;
        switch (prize.kind)
        {
            case ChallengePrizeKind.Car:
                return Name(first) + (prize.gears > 0 ? $"  ({prize.gears} gears)" : "");
            case ChallengePrizeKind.AltPaint:
                return $"Alt paint, {Name(first)}";
            case ChallengePrizeKind.Category:
                return prize.code == "1Star" ? "a one-star car" : $"a {prize.code} car";
            default:
                return prize.code;
        }
    }

    void CycleChallengeEntry(List<OwnedCar> eligible)
    {
        if (eligible.Count == 0) return;
        int i = eligible.FindIndex(o => o.id == _challengeEntry);
        _challengeEntry = eligible[(i + 1) % eligible.Count].id;
        _lastFocusKey = "ch.car";
        Rebuild();
    }

    // The picked chapter's launch plan in the picked car, without launching (what Start Challenge would race);
    // null and why when it can't.
    public ChallengeRacePlan PlanPickedChallenge(out string error) =>
        ChallengeRace.Plan(P, ChallengeData, _challengeChapter, _challengeEntry, Cars, ChallengeWeek, out error);

    void StartChallenge(string chapterId)
    {
        if (PlanPickedChallenge(out string error) == null) { SetStatus(error); return; }
        error = MasterDraftRaceLauncher.LaunchChallenge(chapterId, _challengeEntry);
        if (error != null) { SetStatus(error); return; }
        _leaving = true;
    }

    // What a finished chapter did, over the Challenges screen: the finish, completion, money and XP, then the prize
    // behind a Reveal button — the car's livery with "New car!" or the gears it gained, or the alt paint.
    void BuildChallengeResult()
    {
        var o = ChallengeOutcome;
        if (o == null || o.plan == null) { _overlay = Overlay.None; BuildChallenges(); return; }

        var win = IronOvalUI.Window(_content, "ChallengeResult", new Vector2(400f, 222f));
        TopLeft(win, new Vector2(100f, -4f), new Vector2(400f, 222f));
        Text(win, $"{o.plan.eventName}: {o.plan.chapterName}", IronOvalUI.Role.Header, T.gold,
             new Vector2(14f, -10f), new Vector2(372f, 20f)).overflowMode = TextOverflowModes.Ellipsis;

        var r = o.rewards;
        var prizes = r?.prizes ?? new List<PrizeOutcome>();
        if (_prizeRevealed && prizes.Count > 0)
        {
            BuildPrizeReveal(win, prizes[Mathf.Clamp(_prizeShown, 0, prizes.Count - 1)]);
            bool more = _prizeShown + 1 < prizes.Count;
            ButtonItem(win, more ? "res.next" : "res.ok", more ? "Next Prize" : "Continue", new Vector2(140f, -184f), 120f, () =>
            {
                if (more) { _prizeShown++; Rebuild(); }
                else CloseChallengeResult();
            });
            return;
        }

        var lines = new List<string> { TrackName(o.plan.trackId) };
        if (r == null) lines.Add(string.IsNullOrEmpty(o.error) ? "This challenge couldn't be scored." : o.error);
        else
        {
            lines.Add(r.position > 0 ? $"Finished P{r.position}" : "Not classified");
            lines.Add(r.challenge.completed
                ? (r.challenge.firstCompletion ? "Challenge complete!" : "Challenge complete again!") + (r.money > 0 ? $"   +{r.money:N0} coins" : "")
                : "Win to complete this challenge.");
            lines.Add($"XP +{r.xp}" + (r.LevelsGained > 0 ? $"   Level up! Now level {r.levelAfter}" : ""));
            if (prizes.Count > 0) lines.Add(prizes.Count == 1 ? "You've won a prize!" : $"You've won {prizes.Count} prizes!");
            else if (r.challenge.completed && !r.challenge.prizeDue) lines.Add("This chapter's prize was already collected.");
            lines.Add($"Coins  {r.coins:N0}");
        }
        Text(win, string.Join("\n", lines), IronOvalUI.Role.Data, T.text, new Vector2(14f, -36f), new Vector2(372f, 144f), wrap: true);
        if (prizes.Count > 0)
            ButtonItem(win, "res.reveal", "Reveal Prize", new Vector2(130f, -184f), 140f, () =>
            {
                _prizeRevealed = true;
                _prizeShown = 0;
                _lastFocusKey = "res.ok";
                Rebuild();
            });
        else
            ButtonItem(win, "res.ok", "Continue", new Vector2(140f, -184f), 120f, CloseChallengeResult);
    }

    void BuildPrizeReveal(RectTransform win, PrizeOutcome prize)
    {
        var cars = Cars;
        var def = cars?.Get(prize.carId);
        string sprite = prize.kind == ChallengePrizeKind.AltPaint ? prize.livery : def?.livery;
        ChallengePrizeSprite = sprite ?? "";

        var big = new GameObject("PrizeLivery", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        big.transform.SetParent(win, false);
        TopLeft(big.rectTransform, new Vector2(14f, -44f), new Vector2(160f, 80f));
        big.raycastTarget = false;
        big.preserveAspect = true;
        big.sprite = string.IsNullOrEmpty(sprite) ? null : Resources.Load<Sprite>(sprite);
        big.color = big.sprite == null ? Color.clear : Color.white;

        string name = def != null ? $"#{def.number} {def.driver}" : prize.carId;
        var lines = new List<string>();
        string headline;
        if (prize.kind == ChallengePrizeKind.AltPaint)
        {
            headline = "Alt paint unlocked!";
            lines.Add($"A new scheme for the {name}");
        }
        else if (prize.newCar)
        {
            headline = "New car!";
            lines.Add(name);
            if (def != null)
            {
                lines.Add($"{cars.GetCarset(def.carset)?.name ?? def.carset}  {def.team}  {def.manufacturer}");
                lines.Add($"{def.rarity}-Star {def.type}");
            }
            lines.Add($"Joins your garage with {prize.gears} gears");
        }
        else
        {
            headline = $"+{prize.gears} gears";
            lines.Add($"for the {name}");
            var owned = P?.Car(prize.carId);
            if (owned != null)
                lines.Add(CarProgression.IsMaxClass(owned)
                    ? $"Class {owned.carClass} (max)"
                    : $"Class {owned.carClass}   {CarProgression.GearsToNextClass(owned)} to Class {owned.carClass + 1}");
        }
        ChallengePrizeText = headline + "\n" + string.Join("\n", lines);
        Text(win, headline, IronOvalUI.Role.Header, T.gold, new Vector2(186f, -44f), new Vector2(200f, 20f));
        Text(win, string.Join("\n", lines), IronOvalUI.Role.Data, T.text, new Vector2(186f, -70f), new Vector2(200f, 100f), wrap: true);
    }

    void CloseChallengeResult()
    {
        string chapter = ChallengeOutcome?.plan?.chapterId;
        if (S != null) S.LastChallenge = null;
        _challengeOutcomeOverride = null;
        _overlay = Overlay.None;
        _tab = Tab.Challenges;
        _prizeRevealed = false;
        _lastFocusKey = "ch.go";
        PickChallenge(chapter);
    }

    // ------------------------------------------------------------------ Home

    void BuildHome()
    {
        var p = P;
        var win = IronOvalUI.Window(_content, "Profile", new Vector2(300f, 236f));
        TopLeft(win, Vector2.zero, new Vector2(300f, 236f));

        Text(win, p.displayName, IronOvalUI.Role.Header, T.gold, new Vector2(12f, -10f), new Vector2(276f, 20f));
        var lines = new List<string>
        {
            $"Coins  {p.coins}",
            $"Cards  {p.cards.Count}",
            $"  Drivers {p.CountOf(CardType.Driver)}  Cars {p.CountOf(CardType.Car)}",
            $"  Chiefs {p.CountOf(CardType.CrewChief)}  Actions {p.CountOf(CardType.Action)}",
            $"Team OVR  {TeamRules.Rating(p.team, p)}",
            $"Races  {p.racesRun}   Wins  {p.wins}",
            $"Server  {S.Backend.Name}",
        };
        Text(win, string.Join("\n", lines), IronOvalUI.Role.Data, T.text, new Vector2(12f, -36f), new Vector2(276f, 130f), wrap: true);

        var badge = Text(win, MasterDraftOnline.BadgeText, IronOvalUI.Role.Data,
                         MasterDraftOnline.Enabled ? T.confirm : T.caution, new Vector2(12f, -170f), new Vector2(276f, 18f));
        badge.name = "OnlineBadge";

        var problems = TeamRules.Validate(p.team, p);
        Text(win, problems.Count == 0 ? "Your team is ready to race." : problems[0], IronOvalUI.Role.Data,
             problems.Count == 0 ? T.confirm : T.danger, new Vector2(12f, -192f), new Vector2(276f, 36f), wrap: true);

        float x = 330f, y = 0f;
        if (!p.starterClaimed)
        {
            ButtonItem(_content, "home.starter", "Open Starter Pack", new Vector2(x, y), 240f, () => { SetTab(Tab.Packs); OpenPack(PackKind.Starter); });
            y -= 34f;
        }
        ButtonItem(_content, "home.race", "Go Race", new Vector2(x, y), 240f, () => SetTab(Tab.GoRace)); y -= 34f;
        ButtonItem(_content, "home.team", "My Team", new Vector2(x, y), 240f, () => SetTab(Tab.MyTeam)); y -= 34f;
        ButtonItem(_content, "home.packs", "Packs", new Vector2(x, y), 240f, () => SetTab(Tab.Packs)); y -= 34f;
        ButtonItem(_content, "home.trades", "Trades", new Vector2(x, y), 240f, () => SetTab(Tab.Trades)); y -= 34f;
        ButtonItem(_content, "home.title", "Back to Title", new Vector2(x, y), 240f, LeaveToTitle);
    }

    // ------------------------------------------------------------------ Packs

    void BuildPacks()
    {
        var p = P;
        PackTile("pack.starter", "Starter Pack", new Vector2(0f, 0f), T.gold,
                 p.starterClaimed ? "Opened" : "Free", $"{PackGenerator.StarterSize} cards", !p.starterClaimed,
                 () => OpenPack(PackKind.Starter));

        int price = Economy.PriceOf(PackKind.Standard);
        PackTile("pack.standard", "Standard Pack", new Vector2(160f, 0f), T.info,
                 $"{price} Coins", $"{PackGenerator.StandardSize} cards", true,
                 () => OpenPack(PackKind.Standard));

        string about =
            "The Starter Pack is free, once. It guarantees a driver, a car, a crew chief and five action cards — a team that can race.\n\n" +
            $"A Standard Pack holds {PackGenerator.StandardSize} cards, the last one Rare or better.\n\n" +
            $"You have {p.coins} coins. Races pay coins by finishing position.";
        Text(_content, about, IronOvalUI.Role.Data, T.textDim, new Vector2(330f, 0f), new Vector2(270f, 230f), wrap: true);
    }

    void PackTile(string key, string title, Vector2 pos, Color frame, string price, string size, bool enabled, Action open)
    {
        var tile = new GameObject(key, typeof(RectTransform), typeof(Image));
        tile.transform.SetParent(_content, false);
        var rt = (RectTransform)tile.transform;
        TopLeft(rt, pos, new Vector2(140f, 200f));
        tile.GetComponent<Image>().color = enabled ? frame : T.plateLight;
        var inner = MasterDraftCardView.Box(rt, "Inner", T.plateDeep, 4f);

        Text(inner, title, IronOvalUI.Role.Header, enabled ? frame : T.textDisabled, new Vector2(8f, -10f), new Vector2(116f, 44f), wrap: true);
        Text(inner, size, IronOvalUI.Role.Data, T.textDim, new Vector2(8f, -70f), new Vector2(116f, 18f));
        Text(inner, price, IronOvalUI.Role.Body, enabled ? T.text : T.textDisabled, new Vector2(8f, -150f), new Vector2(116f, 20f));
        AddItem(rt, key, open, enabled);
    }

    async void OpenPack(PackKind kind)
    {
        if (_busy || S?.Backend == null) return;
        _busy = true;
        SetStatus("Opening…", 0f);
        var result = await S.Backend.BuyAndOpenPack(kind);
        if (this == null) return;
        _busy = false;
        if (!result.ok)
        {
            SetStatus(result.error);
            Rebuild();
            return;
        }

        SetStatus(kind == PackKind.Starter ? "Your team has been picked from the Starter Pack — change it in My Team." : "");
        _reveal = result.cards;
        _revealed = 0;
        _revealTitle = PackGenerator.NameOf(kind);
        _overlay = Overlay.Reveal;
        _lastFocusKey = "rev0";
        Rebuild();
    }

    // ------------------------------------------------------------------ Reveal

    void BuildReveal()
    {
        int n = _reveal.Count;
        Text(_content, $"{_revealTitle}   {_revealed} of {n}", IronOvalUI.Role.Header, T.gold, new Vector2(0f, -2f), new Vector2(330f, 22f));

        bool done = _revealed >= n;
        ButtonItem(_content, "rev.all", "Reveal All", new Vector2(370f, 0f), 110f, RevealAll, !done);
        ButtonItem(_content, "rev.done", "Done", new Vector2(490f, 0f), 110f, CloseReveal, done);

        const int cols = 6;
        var size = new Vector2(80f, 98f);
        int rows = (n + cols - 1) / cols;
        for (int i = 0; i < n; i++)
        {
            int col = i % cols, row = i / cols;
            int inRow = row == rows - 1 ? n - row * cols : cols;
            float rowWidth = inRow * (size.x + 5f) - 5f;
            float x = (600f - rowWidth) / 2f + col * (size.x + 5f);
            float y = -32f - row * (size.y + 5f);

            bool shown = i < _revealed;
            var card = MasterDraftCardView.Build(_content, _reveal[i].def, size, faceDown: !shown);
            TopLeft(card, new Vector2(x, y), size);
            int index = i;
            AddItem(card, "rev" + i, () =>
            {
                if (index >= _revealed) RevealNext();
                else { _detail = _reveal[index]; ShowDetail(); }
            });
        }
    }

    void RevealNext()
    {
        if (_revealed >= _reveal.Count) return;
        var card = _reveal[_revealed];
        _revealed++;
        if (card.def.rarity >= Rarity.Epic) SetStatus($"{CardText.RarityName(card.def.rarity)} pull — {card.def.name}!");
        _lastFocusKey = _revealed < _reveal.Count ? "rev" + _revealed : "rev.done";
        Rebuild();
    }

    void RevealAll()
    {
        if (_overlay != Overlay.Reveal) return;
        _revealed = _reveal.Count;
        _lastFocusKey = "rev.done";
        Rebuild();
    }

    void CloseReveal()
    {
        _overlay = Overlay.None;
        Rebuild();
    }

    // ------------------------------------------------------------------ My Team

    static readonly (CardType type, string label)[] Filters =
    {
        (CardType.Driver, "Drivers"), (CardType.Car, "Cars"), (CardType.CrewChief, "Chiefs"), (CardType.Action, "Actions"),
    };
    const int TeamCols = 5, TeamRows = 2;

    void BuildMyTeam()
    {
        var p = P;
        var team = p.team;

        // Left: the slots.
        string Name(string uid) { var c = p.Find(uid); return c == null ? "None" : $"{c.def.name} {c.def.overall}"; }
        float y = 0f;
        RowItem(_content, "slot.driver", $"Driver  {Name(team.activeDriver)}", new Vector2(0f, y), new Vector2(196f, 18f), () => Filter(CardType.Driver)); y -= 21f;
        RowItem(_content, "slot.bench", $"Bench  {team.bench.Count}/{TeamRules.MaxBench}", new Vector2(0f, y), new Vector2(196f, 18f), () => Filter(CardType.Driver)); y -= 21f;
        RowItem(_content, "slot.car", $"Car  {Name(team.car)}", new Vector2(0f, y), new Vector2(196f, 18f), () => Filter(CardType.Car)); y -= 21f;
        RowItem(_content, "slot.chief", $"Chief  {Name(team.crewChief)}", new Vector2(0f, y), new Vector2(196f, 18f), () => Filter(CardType.CrewChief)); y -= 21f;
        RowItem(_content, "slot.deck", $"Deck  {team.actionDeck.Count}/{TeamRules.MaxDeck}", new Vector2(0f, y), new Vector2(196f, 18f), () => Filter(CardType.Action)); y -= 23f;
        ButtonItem(_content, "slot.auto", "Auto Pick", new Vector2(0f, y), 96f, AutoPick);
        Text(_content, $"OVR  {TeamRules.Rating(team, p)}", IronOvalUI.Role.Data, T.gold, new Vector2(108f, y - 5f), new Vector2(88f, 16f)); y -= 30f;

        var problems = TeamRules.Validate(team, p);
        Text(_content, problems.Count == 0 ? "Ready to race." : problems[0], IronOvalUI.Role.Data,
             problems.Count == 0 ? T.confirm : T.danger, new Vector2(0f, y), new Vector2(196f, 32f), wrap: true); y -= 34f;

        // Who to send where.
        var drivers = TeamRules.TeamDrivers(team, p);
        var hints = new List<string>();
        foreach (var kind in RacedKinds())
        {
            var best = TeamRules.BestDriverFor(kind, drivers);
            hints.Add(best == null ? $"{CardText.TrackKindShort(kind)}  -" : $"{CardText.TrackKindShort(kind)}  {Surname(best.def.name)} {best.def.Aptitude(kind)}");
        }
        Text(_content, string.Join("\n", hints), IronOvalUI.Role.Data, T.textDim, new Vector2(0f, y), new Vector2(196f, Mathf.Max(16f, 236f + y)), wrap: false);

        // Right: filter tabs, then the collection grid.
        float fx = 206f;
        for (int i = 0; i < Filters.Length; i++)
        {
            var f = Filters[i];
            var face = IronOvalUI.TabButton(_content, "Filter_" + f.label, f.label, new Vector2(80f, 0f), f.type == _filter);
            TopLeft((RectTransform)face.transform.parent, new Vector2(fx + i * 84f, 0f), new Vector2(80f, UIButton.KitPx));
            var type = f.type;
            AddItem((RectTransform)face.transform, "filter." + f.label, () => Filter(type));
        }

        var list = Collection(_filter);
        int perPage = TeamCols * TeamRows;
        int pages = Mathf.Max(1, (list.Count + perPage - 1) / perPage);
        _page = Mathf.Clamp(_page, 0, pages - 1);

        var prev = IronOvalUI.Button(_content, "PagePrev", "<", new Vector2(26f, 0f));
        TopLeft((RectTransform)prev.transform, new Vector2(fx + 4 * 84f, 0f), new Vector2(26f, UIButton.KitPx));
        AddItem((RectTransform)prev.transform, "page.prev", () => Page(-1, pages), _page > 0);
        var next = IronOvalUI.Button(_content, "PageNext", ">", new Vector2(26f, 0f));
        TopLeft((RectTransform)next.transform, new Vector2(fx + 4 * 84f + 30f, 0f), new Vector2(26f, UIButton.KitPx));
        AddItem((RectTransform)next.transform, "page.next", () => Page(1, pages), _page < pages - 1);

        if (list.Count == 0)
            Text(_content, p.cards.Count == 0 ? "No cards yet. Open the Starter Pack in Packs." : "No cards of this type yet.",
                 IronOvalUI.Role.Data, T.textDim, new Vector2(fx, -40f), new Vector2(390f, 40f), wrap: true);

        var size = new Vector2(74f, 90f);
        for (int i = 0; i < perPage; i++)
        {
            int index = _page * perPage + i;
            if (index >= list.Count) break;
            var card = list[index];
            var view = MasterDraftCardView.Build(_content, card.def, size);
            TopLeft(view, new Vector2(fx + (i % TeamCols) * 79f, -32f - (i / TeamCols) * 94f), size);
            if (team.Uses(card.uid)) MasterDraftCardView.Outline(view, T.confirm, 2f, 0f);
            AddItem(view, "card." + card.uid, () => { _detail = card; ShowDetail(); });
        }
        Text(_content, $"Page {_page + 1}/{pages}   {list.Count} cards", IronOvalUI.Role.Data, T.textDim,
             new Vector2(fx, -236f + 16f), new Vector2(390f, 16f));

        // Stepping off the side of the grid turns the page.
        _navOverride = dir =>
        {
            string key = FocusKey;
            if (key == null || !key.StartsWith("card.") || Mathf.Abs(dir.x) < 0.5f) return false;
            int at = list.FindIndex(c => "card." + c.uid == key) - _page * perPage;
            int col = at % TeamCols;
            if (dir.x > 0f && col == TeamCols - 1 && _page < pages - 1) { Page(1, pages); return true; }
            if (dir.x < 0f && col == 0 && _page > 0) { Page(-1, pages); return true; }
            return false;
        };
    }

    // The track types the built venues actually cover — no point hinting a dirt driver with no dirt track.
    List<TrackKind> _racedKinds;
    List<TrackKind> RacedKinds()
    {
        if (_racedKinds != null) return _racedKinds;
        var kinds = new HashSet<TrackKind>();
        foreach (var t in TrackCatalog.Playable()) kinds.Add(MasterDraftSources.KindOf(t.Type));
        _racedKinds = CardText.AllTrackKinds.Where(k => kinds.Count == 0 || kinds.Contains(k)).ToList();
        return _racedKinds;
    }

    List<CardInstance> Collection(CardType type)
    {
        var list = P.cards.Where(c => c.def.type == type).ToList();
        list.Sort((a, b) => b.def.overall != a.def.overall ? b.def.overall.CompareTo(a.def.overall) : string.CompareOrdinal(a.def.name, b.def.name));
        return list;
    }

    void Filter(CardType type)
    {
        _filter = type;
        _page = 0;
        var first = Collection(type).FirstOrDefault();
        _lastFocusKey = first != null ? "card." + first.uid : "filter." + Filters.First(f => f.type == type).label;
        Rebuild();
    }

    void Page(int by, int pages)
    {
        _page = Mathf.Clamp(_page + by, 0, pages - 1);
        var list = Collection(_filter);
        int index = Mathf.Clamp(_page * TeamCols * TeamRows, 0, Mathf.Max(0, list.Count - 1));
        _lastFocusKey = list.Count > 0 ? "card." + list[index].uid : null;
        Rebuild();
    }

    void AutoPick()
    {
        P.team = TeamRules.AutoBuild(P);
        SaveTeam("Team picked from your best cards.");
    }

    async void SaveTeam(string message = null)
    {
        if (S?.Backend == null)
        {
            // No backend: a BuildOffline (test) profile. Nothing to save; just show the change.
            if (!string.IsNullOrEmpty(message)) SetStatus(message);
            if (_content != null) Rebuild();
            return;
        }
        _busy = true;
        await S.Backend.SaveProfile();
        if (this == null) return;
        _busy = false;
        if (!string.IsNullOrEmpty(message)) SetStatus(message);
        Rebuild();
    }

    // ------------------------------------------------------------------ Detail

    Overlay _detailReturn = Overlay.None;

    void ShowDetail()
    {
        _detailReturn = _overlay;
        _overlay = Overlay.Detail;
        _lastFocusKey = null;
        Rebuild();
    }

    void CloseDetail()
    {
        _overlay = _detailReturn == Overlay.Detail ? Overlay.None : _detailReturn;
        Rebuild();
    }

    void BuildDetail()
    {
        var card = _detail;
        if (card == null || P.Find(card.uid) == null) { _overlay = Overlay.None; BuildMyTeam(); return; }
        var d = card.def;
        var team = P.team;

        var view = MasterDraftCardView.Build(_content, d, new Vector2(150f, 200f));
        TopLeft(view, Vector2.zero, new Vector2(150f, 200f));

        Text(_content, d.name, IronOvalUI.Role.Header, MasterDraftCardView.RarityColour(d.rarity), new Vector2(170f, 0f), new Vector2(430f, 20f));
        Text(_content, $"{CardText.RarityName(d.rarity)} {CardText.TypeName(d.type)}   OVR {d.overall}", IronOvalUI.Role.Data, T.textDim,
             new Vector2(170f, -24f), new Vector2(430f, 16f));
        Text(_content, MasterDraftCardView.FullStats(d), IronOvalUI.Role.Data, T.text, new Vector2(170f, -46f), new Vector2(430f, 130f), wrap: true);

        string role = Role(card);
        if (role.Length > 0) Text(_content, role, IronOvalUI.Role.Data, T.confirm, new Vector2(170f, -178f), new Vector2(430f, 16f));

        // What this card can do in the team. Not offered from inside a pack reveal — the reveal is for looking.
        float x = 170f, y = -204f;
        if (_detailReturn == Overlay.None)
        {
            switch (d.type)
            {
                case CardType.Driver:
                    ButtonItem(_content, "d.active", "Make Active", new Vector2(x, y), 120f, () => MakeActive(card), team.activeDriver != card.uid); x += 126f;
                    bool benched = team.bench.Contains(card.uid);
                    ButtonItem(_content, "d.bench", benched ? "Unbench" : "Bench", new Vector2(x, y), 100f, () => ToggleBench(card),
                               benched || (team.activeDriver != card.uid && team.bench.Count < TeamRules.MaxBench)); x += 106f;
                    break;
                case CardType.Car:
                    ButtonItem(_content, "d.use", "Use This Car", new Vector2(x, y), 140f, () => { team.car = card.uid; SaveTeam(); }, team.car != card.uid); x += 146f;
                    break;
                case CardType.CrewChief:
                    ButtonItem(_content, "d.use", "Use This Chief", new Vector2(x, y), 150f, () => { team.crewChief = card.uid; SaveTeam(); }, team.crewChief != card.uid); x += 156f;
                    break;
                case CardType.Action:
                    bool inDeck = team.actionDeck.Contains(card.uid);
                    if (inDeck)
                        ButtonItem(_content, "d.deck", "Take Out of Deck", new Vector2(x, y), 160f, () => { team.actionDeck.Remove(card.uid); SaveTeam(); });
                    else
                    {
                        int copies = team.actionDeck.Count(uid => P.Find(uid)?.def.id == d.id);
                        bool can = ActionCatalog.IsPlayable(d.effect) && copies < TeamRules.MaxCopiesPerAction && team.actionDeck.Count < TeamRules.MaxDeck;
                        ButtonItem(_content, "d.deck", "Add to Deck", new Vector2(x, y), 130f, () => { team.actionDeck.Add(card.uid); SaveTeam(); }, can);
                    }
                    x += 166f;
                    break;
            }
        }
        ButtonItem(_content, "d.close", "Close", new Vector2(Mathf.Max(x, 170f), y), 90f, CloseDetail);
    }

    string Role(CardInstance card)
    {
        var team = P.team;
        if (team.activeDriver == card.uid) return "Active driver";
        if (team.bench.Contains(card.uid)) return "On the bench";
        if (team.car == card.uid) return "Your car";
        if (team.crewChief == card.uid) return "Your crew chief";
        if (team.actionDeck.Contains(card.uid)) return "In your action deck";
        if (TradeRules.IsListed(P, card.uid)) return "Listed for trade";
        return "";
    }

    void MakeActive(CardInstance card)
    {
        var team = P.team;
        team.bench.Remove(card.uid);
        if (!string.IsNullOrEmpty(team.activeDriver) && team.activeDriver != card.uid && team.bench.Count < TeamRules.MaxBench)
            team.bench.Insert(0, team.activeDriver);
        team.activeDriver = card.uid;
        SaveTeam();
    }

    void ToggleBench(CardInstance card)
    {
        var team = P.team;
        if (team.bench.Remove(card.uid)) { SaveTeam(); return; }
        if (team.bench.Count >= TeamRules.MaxBench || team.activeDriver == card.uid) return;
        team.bench.Add(card.uid);
        SaveTeam();
    }

    // ------------------------------------------------------------------ Go Race

    void BuildGoRace()
    {
        var p = P;

        var quick = IronOvalUI.TabButton(_content, "Mode_Quick", "Quick Race", new Vector2(120f, 0f), true);
        TopLeft((RectTransform)quick.transform.parent, Vector2.zero, new Vector2(120f, UIButton.KitPx));
        AddItem((RectTransform)quick.transform, "mode.quick", () => SetStatus("Quick Race: you and your team against the AI field."));

        var online = IronOvalUI.TabButton(_content, "Mode_Online", "Online (Coming Soon)", new Vector2(190f, 0f), false);
        TopLeft((RectTransform)online.transform.parent, new Vector2(126f, 0f), new Vector2(190f, UIButton.KitPx));
        AddItem((RectTransform)online.transform, "mode.online", () => SetStatus(MasterDraftOnline.Enabled
            ? "Online racing is coming soon."
            : "Online racing is coming soon — it is built but switched off while it is tested."));

        if (_tracks == null)
        {
            _tracks = TrackCatalog.Playable().ToList();
            _tracks.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
            _trackIndex = Mathf.Max(0, _tracks.FindIndex(t => t.Name == TrackSelection.CurrentId));
        }
        if (_tracks.Count == 0)
        {
            Text(_content, "No tracks are built.", IronOvalUI.Role.Body, T.danger, new Vector2(0f, -40f), new Vector2(300f, 20f));
            return;
        }

        _trackIndex = Mathf.Clamp(_trackIndex, 0, _tracks.Count - 1);
        if (_trackIndex < _trackScroll) _trackScroll = _trackIndex;
        if (_trackIndex >= _trackScroll + TrackRows) _trackScroll = _trackIndex - TrackRows + 1;
        _trackScroll = Mathf.Clamp(_trackScroll, 0, Mathf.Max(0, _tracks.Count - TrackRows));

        Text(_content, "Track", IronOvalUI.Role.Data, T.gold, new Vector2(0f, -34f), new Vector2(300f, 16f));
        for (int i = 0; i < TrackRows; i++)
        {
            int index = _trackScroll + i;
            if (index >= _tracks.Count) break;
            var track = _tracks[index];
            bool selected = index == _trackIndex;
            RowItem(_content, "trk" + i, track.DisplayName, new Vector2(0f, -52f - i * 20f), new Vector2(310f, 18f),
                    () => { _trackIndex = index; _driverUid = null; _lastFocusKey = "go"; Rebuild(); },
                    selected, Nice(track.Type));
        }
        if (_tracks.Count > TrackRows)
            Text(_content, $"{_trackIndex + 1} of {_tracks.Count}", IronOvalUI.Role.Data, T.textDim, new Vector2(0f, -236f + 16f), new Vector2(310f, 16f));

        // Up/down on the list moves through all the tracks, not just the visible ones.
        _navOverride = dir =>
        {
            string key = FocusKey;
            if (key == null || !key.StartsWith("trk") || Mathf.Abs(dir.y) < 0.5f) return false;
            int row = int.Parse(key.Substring(3));
            int target = _trackScroll + row + (dir.y > 0f ? -1 : 1);
            if (target < 0 || target >= _tracks.Count) return dir.y > 0f ? false : true;
            if (target < _trackScroll) _trackScroll = target;
            if (target >= _trackScroll + TrackRows) _trackScroll = target - TrackRows + 1;
            _trackIndex = target;
            _driverUid = null;
            _lastFocusKey = "trk" + (target - _trackScroll);
            Rebuild();
            return true;
        };

        // Right: the team's drivers, rated for this track's type.
        var selectedTrack = _tracks[_trackIndex];
        var kind = MasterDraftSources.KindOf(selectedTrack.Type);
        var drivers = TeamRules.TeamDrivers(p.team, p);
        var best = TeamRules.BestDriverFor(kind, drivers);
        if (_driverUid == null || drivers.All(d => d.uid != _driverUid)) _driverUid = best?.uid;

        float x = 330f;
        Text(_content, $"{CardText.TrackKindName(kind)} Aptitude", IronOvalUI.Role.Data, T.gold, new Vector2(x, -34f), new Vector2(270f, 16f));
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            string tag = d == best ? "  Best" : "";
            RowItem(_content, "drv" + d.uid, $"{Surname(d.def.name)}  {d.def.Aptitude(kind)}/20{tag}", new Vector2(x, -52f - i * 20f), new Vector2(270f, 18f),
                    () => { _driverUid = d.uid; _lastFocusKey = "go"; Rebuild(); }, d.uid == _driverUid, $"OVR {d.def.overall}");
        }
        if (drivers.Count == 0)
            Text(_content, "No drivers in your team. Open the Starter Pack.", IronOvalUI.Role.Data, T.textDim, new Vector2(x, -52f), new Vector2(270f, 36f), wrap: true);

        var problems = TeamRules.Validate(p.team, p);
        if (problems.Count > 0)
            Text(_content, problems[0], IronOvalUI.Role.Data, T.danger, new Vector2(x, -160f), new Vector2(270f, 34f), wrap: true);

        Text(_content, selectedTrack.DisplayName, IronOvalUI.Role.Data, T.text, new Vector2(x, -196f), new Vector2(150f, 32f), wrap: true);
        ButtonItem(_content, "go", "Go", new Vector2(490f, -204f), 110f, Go, problems.Count == 0 && _driverUid != null);
    }

    void Go()
    {
        var driver = P.Find(_driverUid);
        string error = MasterDraftRaceLauncher.Launch(_tracks[_trackIndex].Name, driver);
        if (error != null) { SetStatus(error); return; }
        _leaving = true;
    }

    static string Nice(TrackType type) => CardText.TrackKindName(MasterDraftSources.KindOf(type));

    static string Surname(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        int space = name.LastIndexOf(' ');
        return space > 0 ? name.Substring(space + 1) : name;
    }

    // ------------------------------------------------------------------ Trades

    List<TradeOffer> _offers;

    void BuildTrades()
    {
        var p = P;
        Text(_content, MasterDraftOnline.Enabled
                ? "Trade cards with other players."
                : "Real trading needs online. These offers come from practice bots so you can try the flow.",
             IronOvalUI.Role.Data, T.caution, new Vector2(0f, 0f), new Vector2(600f, 18f));

        if (_offers == null) { LoadOffers(); return; }

        Text(_content, "Offers", IronOvalUI.Role.Data, T.gold, new Vector2(0f, -22f), new Vector2(200f, 16f));
        float y = -40f;
        foreach (var offer in _offers.Take(6))
        {
            var o = offer;
            string give = o.giving == null ? "?" : $"{CardText.RarityName(o.giving.rarity)} {CardText.TypeName(o.giving.type)} {o.giving.name} {o.giving.overall}";
            RowItem(_content, "off" + o.id, $"{o.fromName}: {give}", new Vector2(0f, y), new Vector2(600f, 18f),
                    () => PickForOffer(o), false, "wants " + o.WantText);
            y -= 20f;
        }
        if (_offers.Count == 0)
        {
            Text(_content, "No offers right now.", IronOvalUI.Role.Data, T.textDim, new Vector2(0f, y), new Vector2(600f, 18f));
            y -= 20f;
        }

        y = Mathf.Min(y, -164f) - 4f;
        Text(_content, "Your Listings", IronOvalUI.Role.Data, T.gold, new Vector2(0f, y), new Vector2(200f, 16f));
        y -= 18f;
        foreach (var listing in p.myListings.Take(1))
        {
            var l = listing;
            var card = p.Find(l.myCardUid);
            RowItem(_content, "lst" + l.id, $"{card?.def.name ?? "?"} for {l.WantText}", new Vector2(0f, y), new Vector2(440f, 18f),
                    () => CancelListing(l), false, "cancel");
        }
        if (p.myListings.Count == 0)
            Text(_content, "Nothing listed.", IronOvalUI.Role.Data, T.textDim, new Vector2(0f, y), new Vector2(440f, 18f));
        else if (p.myListings.Count > 1)
            Text(_content, $"and {p.myListings.Count - 1} more", IronOvalUI.Role.Data, T.textDim, new Vector2(0f, y - 18f), new Vector2(440f, 16f));

        ButtonItem(_content, "trade.list", "List a Card", new Vector2(460f, y), 140f, PickForListing);
    }

    async void LoadOffers()
    {
        if (_busy || S?.Backend == null) return;
        _busy = true;
        _offers = await S.Backend.ListTradeOffers();
        if (this == null) return;
        _busy = false;
        Rebuild();
    }

    void PickForOffer(TradeOffer offer)
    {
        var eligible = P.cards.Where(c => TradeRules.CanFulfil(P, offer, c.uid) == null).ToList();
        if (eligible.Count == 0)
        {
            SetStatus($"You have no spare card that fits: they want {offer.WantText}.");
            return;
        }
        OpenPicker($"Give for {offer.giving?.name}  ({offer.WantText})", eligible, async card =>
        {
            _busy = true;
            var result = await S.Backend.AcceptTrade(offer.id, card.uid);
            if (this == null) return;
            _busy = false;
            _overlay = Overlay.None;
            _offers = null;
            SetStatus(result.ok ? $"Traded {card.def.name} for {offer.giving?.name}." : result.error);
            Rebuild();
        });
    }

    void PickForListing()
    {
        var spare = TradeRules.Tradeable(P);
        if (spare.Count == 0)
        {
            SetStatus("Every card you own is in your team or already listed.");
            return;
        }
        OpenPicker("List which card?", spare, card =>
        {
            _listing = card;
            _overlay = Overlay.WantType;
            _lastFocusKey = null;
            Rebuild();
        });
    }

    async void CancelListing(TradeOffer listing)
    {
        _busy = true;
        bool ok = await S.Backend.CancelTrade(listing.id);
        if (this == null) return;
        _busy = false;
        SetStatus(ok ? "Listing cancelled." : "Couldn't cancel that listing.");
        Rebuild();
    }

    void BuildWantType()
    {
        var card = _listing;
        if (card == null) { _overlay = Overlay.None; BuildTrades(); return; }
        Text(_content, $"List {card.def.name} for…", IronOvalUI.Role.Header, T.gold, Vector2.zero, new Vector2(600f, 20f));
        Text(_content, $"A trader would have to give a {CardText.RarityName(card.def.rarity)} or better card of the type you pick." +
                       (MasterDraftOnline.Enabled ? "" : " Offline, nobody will answer — the listing waits for online trading."),
             IronOvalUI.Role.Data, T.textDim, new Vector2(0f, -28f), new Vector2(600f, 40f), wrap: true);

        float x = 0f;
        foreach (CardType type in Enum.GetValues(typeof(CardType)))
        {
            var t = type;
            ButtonItem(_content, "want." + t, CardText.TypeName(t), new Vector2(x, -80f), 140f, async () =>
            {
                _busy = true;
                var result = await S.Backend.ProposeTrade(card.uid, t, card.def.rarity);
                if (this == null) return;
                _busy = false;
                _overlay = Overlay.None;
                _listing = null;
                SetStatus(result.ok ? $"{card.def.name} listed." : result.error);
                Rebuild();
            });
            x += 150f;
        }
    }

    // ------------------------------------------------------------------ Picker

    const int PickCols = 7, PickRows = 2;

    void OpenPicker(string title, List<CardInstance> cards, Action<CardInstance> onPick)
    {
        _pickTitle = title;
        _pickList = cards.OrderByDescending(c => c.def.overall).ToList();
        _onPick = onPick;
        _pickPage = 0;
        _overlay = Overlay.Picker;
        _lastFocusKey = null;
        Rebuild();
    }

    void BuildPicker()
    {
        Text(_content, _pickTitle, IronOvalUI.Role.Header, T.gold, Vector2.zero, new Vector2(360f, 20f)).overflowMode = TextOverflowModes.Ellipsis;
        ButtonItem(_content, "pick.cancel", "Cancel", new Vector2(490f, 0f), 110f, Back);

        int perPage = PickCols * PickRows;
        int pages = Mathf.Max(1, (_pickList.Count + perPage - 1) / perPage);
        _pickPage = Mathf.Clamp(_pickPage, 0, pages - 1);
        var size = new Vector2(80f, 98f);
        for (int i = 0; i < perPage; i++)
        {
            int index = _pickPage * perPage + i;
            if (index >= _pickList.Count) break;
            var card = _pickList[index];
            var view = MasterDraftCardView.Build(_content, card.def, size);
            TopLeft(view, new Vector2((i % PickCols) * 86f, -32f - (i / PickCols) * 102f), size);
            AddItem(view, "pk." + card.uid, () => _onPick?.Invoke(card));
        }
        if (pages > 1)
            Text(_content, $"Page {_pickPage + 1}/{pages}", IronOvalUI.Role.Data, T.textDim, new Vector2(370f, -4f), new Vector2(110f, 16f));

        _navOverride = dir =>
        {
            string key = FocusKey;
            if (key == null || !key.StartsWith("pk.") || Mathf.Abs(dir.x) < 0.5f) return false;
            int at = _pickList.FindIndex(c => "pk." + c.uid == key) - _pickPage * perPage;
            int col = at % PickCols;
            int to = -1;
            if (dir.x > 0f && col == PickCols - 1 && _pickPage < pages - 1) to = _pickPage + 1;
            if (dir.x < 0f && col == 0 && _pickPage > 0) to = _pickPage - 1;
            if (to < 0) return false;
            _pickPage = to;
            _lastFocusKey = "pk." + _pickList[Mathf.Min(_pickList.Count - 1, to * perPage)].uid;
            Rebuild();
            return true;
        };
    }

    // ------------------------------------------------------------------ Result

    void BuildResult()
    {
        var r = S.LastRace;
        if (r == null) { _overlay = Overlay.None; BuildHome(); return; }

        var win = IronOvalUI.Window(_content, "Result", new Vector2(360f, 200f));
        TopLeft(win, new Vector2(120f, -10f), new Vector2(360f, 200f));
        Text(win, "Race Result", IronOvalUI.Role.Header, T.gold, new Vector2(14f, -12f), new Vector2(330f, 20f));
        string finish = r.position > 0 ? $"Finished P{r.position}" + (r.fieldSize > 0 ? $" of {r.fieldSize}" : "") : "Did not finish";
        var lines = new[]
        {
            TrackCatalog.DisplayName(r.trackId),
            finish,
            $"Cards played  {r.cardsPlayed}",
            $"Coins earned  +{r.coinsAwarded}",
            $"Coins now  {P.coins}",
        };
        Text(win, string.Join("\n", lines), IronOvalUI.Role.Data, T.text, new Vector2(14f, -40f), new Vector2(330f, 110f), wrap: true);
        ButtonItem(win, "res.ok", "Continue", new Vector2(120f, -160f), 120f, () =>
        {
            S.LastRace = null;
            _overlay = Overlay.None;
            Rebuild();
        });
    }

    // ------------------------------------------------------------------ chrome

    void BuildChrome()
    {
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
        _root = (RectTransform)canvas.transform;

        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(_root, false);
        backdrop.color = T.ink;
        MasterDraftCardView.Fill(backdrop.rectTransform, 0f);

        Text(_root, "Master Draft", IronOvalUI.Role.Header, T.gold, new Vector2(20f, -12f), new Vector2(200f, 22f));
        _badge = Text(_root, "", IronOvalUI.Role.Data, T.caution, new Vector2(190f, -14f), new Vector2(180f, 18f));

        _coins = Text(_root, "", IronOvalUI.Role.Data, T.gold, new Vector2(400f, -14f), new Vector2(220f, 18f));
        _coins.alignment = TextAlignmentOptions.TopRight;

        _tabsRow = new GameObject("Tabs", typeof(RectTransform)).GetComponent<RectTransform>();
        _tabsRow.SetParent(_root, false);
        TopLeft(_tabsRow, new Vector2(20f, -40f), new Vector2(600f, UIButton.KitPx));

        _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        _content.SetParent(_root, false);
        TopLeft(_content, new Vector2(20f, -78f), new Vector2(600f, 236f));

        _status = Text(_root, "", IronOvalUI.Role.Body, T.text, Vector2.zero, new Vector2(600f, 18f));
        Bottom(_status.rectTransform, 24f);
        _help = Text(_root, "", IronOvalUI.Role.Data, T.textDim, Vector2.zero, new Vector2(600f, 16f));
        Bottom(_help.rectTransform, 6f);
    }

    void DrawChromeState()
    {
        _badge.text = MasterDraftOnline.BadgeText;
        _badge.color = MasterDraftOnline.Enabled ? T.confirm : T.caution;
        _coins.text = P != null ? $"Coins  {P.coins}" : "";

        ClearChildren(_tabsRow);
        for (int i = 0; i < TabNames.Length; i++)
        {
            var tab = (Tab)i;
            var face = IronOvalUI.TabButton(_tabsRow, "Tab_" + TabNames[i], TabNames[i], new Vector2(TabWidth, 0f),
                                            tab == _tab && _overlay == Overlay.None);
            TopLeft((RectTransform)face.transform.parent, new Vector2(i * (TabWidth + 4f), 0f), new Vector2(TabWidth, UIButton.KitPx));
            face.navigation = new Navigation { mode = Navigation.Mode.None };
            face.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                SetTab(tab);
            });
        }
    }

    const float TabWidth = 71f;   // eight tabs across the 600 px content width

    void SetStatus(string message, float seconds = 4f)
    {
        if (_status == null) return;
        _status.text = message ?? "";
        _statusUntil = string.IsNullOrEmpty(message) || seconds <= 0f ? 0f : Time.unscaledTime + seconds;
    }

    // ------------------------------------------------------------------ building blocks

    Item AddItem(RectTransform rect, string key, Action confirm, bool enabled = true)
    {
        var item = new Item { key = key, rect = rect, confirm = confirm, enabled = enabled };
        var graphic = rect.GetComponent<Graphic>();
        if (graphic == null)
        {
            var img = rect.gameObject.AddComponent<Image>();
            img.color = Color.clear;
            graphic = img;
        }
        graphic.raycastTarget = true;

        var button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
        if (button.transition != Selectable.Transition.ColorTint) button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.interactable = enabled;
        button.onClick.AddListener(() => Tap(item));

        if (!enabled)
            foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true)) text.color = T.textDisabled;

        _items.Add(item);
        return item;
    }

    Item ButtonItem(Transform parent, string key, string caption, Vector2 pos, float width, Action confirm, bool enabled = true)
    {
        var button = IronOvalUI.Button(parent, "Button_" + key, caption, new Vector2(width, 0f));
        var rt = (RectTransform)button.transform;
        TopLeft(rt, pos, new Vector2(Mathf.Max(width, rt.sizeDelta.x), UIButton.KitPx));
        return AddItem(rt, key, confirm, enabled);
    }

    // A list row: a plate the width of the row, the label on the left, an optional detail on the right.
    Item RowItem(Transform parent, string key, string label, Vector2 pos, Vector2 size, Action confirm, bool selected = false, string right = null)
    {
        var go = new GameObject("Row_" + key, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        TopLeft(rt, pos, size);
        go.GetComponent<Image>().color = selected ? T.plate : T.plateDeep;

        var l = Text(rt, label, IronOvalUI.Role.Data, selected ? T.gold : T.text, new Vector2(4f, -1f), new Vector2(size.x - 8f, size.y));
        l.overflowMode = TextOverflowModes.Ellipsis;
        if (!string.IsNullOrEmpty(right))
        {
            float w = Mathf.Min(size.x * 0.45f, 200f);
            l.rectTransform.sizeDelta = new Vector2(size.x - w - 12f, size.y);
            var r = Text(rt, right, IronOvalUI.Role.Data, T.textDim, new Vector2(size.x - w - 4f, -1f), new Vector2(w, size.y));
            r.alignment = TextAlignmentOptions.TopRight;
            r.overflowMode = TextOverflowModes.Ellipsis;
        }
        return AddItem(rt, key, confirm);
    }

    static TextMeshProUGUI Text(Transform parent, string content, IronOvalUI.Role role, Color colour, Vector2 pos, Vector2 size, bool wrap = false)
    {
        var label = IronOvalUI.Label(parent, "Text", content, role, colour);
        TopLeft(label.rectTransform, pos, size);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
        return label;
    }

    // Destroy works only in Play Mode; BuildOffline runs in the editor (EditMode tests).
    static void Discard(UnityEngine.Object victim)
    {
        if (victim == null) return;
        if (Application.isPlaying) Destroy(victim);
        else DestroyImmediate(victim);
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Discard(parent.GetChild(i).gameObject);
    }

    static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Bottom(RectTransform rt, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(20f, y);
    }
}
