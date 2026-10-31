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

// MASTER DRAFT's menu: Home, Go Race, My Team, Packs, Trades — and the pack-opening, card-detail,
// card-picker and race-result views over them. One scene (Assets/Scenes/MasterDraft.unity, built by
// Draftmaster > Master Draft > Build Master Draft Scene), one component.
//
// WHY THE UI IS BUILT IN CODE. Same reasoning as SingleRaceUI: almost everything here is a generated list
// or grid (a collection, a pack, 38 tracks, a trade board), a generated scene's button listeners don't
// survive a save in this project, and nothing serialised means nothing to lose. It is built from the same
// IronOvalUI kit pieces and type roles as the authored screens, on the kit's 640x360 grid.
//
// INPUT. One focus, moved spatially: arrows / WASD / d-pad step to the nearest item in that direction,
// Enter / Space / E / pad South confirm, Esc / Backspace / pad East back out (overlay -> screen -> Home ->
// title). Tab / Shift+Tab, 1-5 or the shoulder buttons change screen. Every item is also a Button, so a tap
// or a click picks it outright.
public class MasterDraftUI : MonoBehaviour
{
    [Tooltip("Scene BACK returns to from Home. Must be in the build settings.")]
    public string titleSceneName = "TitleScreen";

    enum Tab { Home, GoRace, MyTeam, Packs, Trades }
    enum Overlay { None, Reveal, Detail, Picker, WantType, Result }

    static readonly string[] TabNames = { "Home", "Go Race", "My Team", "Packs", "Trades" };

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
    Tab _tab = Tab.Home;
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

    MasterDraftSession S => MasterDraftSession.Instance;
    MasterDraftProfile P => S != null ? S.Profile : null;
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
            if (kb.digit1Key.wasPressedThisFrame) SetTab(Tab.Home);
            if (kb.digit2Key.wasPressedThisFrame) SetTab(Tab.GoRace);
            if (kb.digit3Key.wasPressedThisFrame) SetTab(Tab.MyTeam);
            if (kb.digit4Key.wasPressedThisFrame) SetTab(Tab.Packs);
            if (kb.digit5Key.wasPressedThisFrame) SetTab(Tab.Trades);
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
        string shortcut = _overlay == Overlay.Reveal ? "Reveal All" : _tab == Tab.MyTeam && _overlay == Overlay.None ? "Auto Pick" : "";
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

        if (_tab != Tab.Home) { SetTab(Tab.Home); return; }
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

    // The screen's one extra button: Reveal All while opening a pack, Auto Pick on My Team.
    void Shortcut()
    {
        if (_busy) return;
        if (_overlay == Overlay.Reveal) { RevealAll(); return; }
        if (_tab == Tab.MyTeam && _overlay == Overlay.None) AutoPick();
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
        if (_focusFrame != null) Destroy(_focusFrame.gameObject);
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
        foreach (Transform child in _content) Destroy(child.gameObject);
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
            default:
                switch (_tab)
                {
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
        if (S?.Backend == null) return;
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

        foreach (Transform child in _tabsRow) Destroy(child.gameObject);
        for (int i = 0; i < TabNames.Length; i++)
        {
            var tab = (Tab)i;
            var face = IronOvalUI.TabButton(_tabsRow, "Tab_" + TabNames[i], TabNames[i], new Vector2(110f, 0f),
                                            tab == _tab && _overlay == Overlay.None);
            TopLeft((RectTransform)face.transform.parent, new Vector2(i * 118f, 0f), new Vector2(110f, UIButton.KitPx));
            face.navigation = new Navigation { mode = Navigation.Mode.None };
            face.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                SetTab(tab);
            });
        }
    }

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
