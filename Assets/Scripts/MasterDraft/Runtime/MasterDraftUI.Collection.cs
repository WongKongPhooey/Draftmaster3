using System;
using System.Collections.Generic;
using Draftmaster.Controls;
using Draftmaster.MasterDraft;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// MASTER DRAFT's Garage tab: the Card Collection screen, Draftmaster 2's garage redone to the Draftmaster3_UI_Unity
// design handoff (Designs/Draftmaster3_UI_Unity, Layout/Collection_PC.json and Reference/Collection_PC@3x.png).
//
// What the design draws, and what it reads here:
//  * Header "CARD COLLECTION  10 / 48 collected" — the chrome's title row (DrawChromeState): cars owned out of the
//    whole catalogue.
//  * Rarity tabs ALL / COMMON / RARE / EPIC / LEGEND — DM2's 1-4 star rarities, in the design's rarity colours.
//    "SORT: RARITY" cycles Rarity / Class / Number; a "SET" cycler beside it keeps the old garage's carset filter.
//  * Cards five across, two rows and the top of a third, scrolling: rarity frame and plate, the class badge where
//    the design has LV1-LV6 (R for Rookie, then D, C, B, A, S — CardCollection.ClassLetter), the number, the
//    livery on the hatched slot, the driver, the rarity and the gears bar ("9/50 Gears"; full and green, "Ready",
//    once the next class's gears are banked; the "UP" tag when the coins are there too). Cars still to collect
//    are the design's "?" cards: rarity stripe, rarity and which carset they're in.
//  * Confirm on a card opens the Card Detail screen (MasterDraftUI.CardDetail.cs, design CardDetail_PC): stats,
//    trait, the gears bar and the paid upgrade, and Set As Race Car. R / pad North upgrades the focused card from
//    the grid too.
//
// Not taken from the design: its bottom tab bar (GARAGE / CARDS / RACE / PACKS / CLUB) — the mode keeps its own
// tabs; the packs count in the header (Master Draft has no packs since the DM2 rework); the Mobile layout's seven
// columns (the menu stays on its 600 px content width, five across). Card names are the DM2 driver names.
public partial class MasterDraftUI
{
    // Design colours (Layout/Collection_PC.json), kept apart from PixelUITheme like DriveScoutStyle: the screen
    // matches the design, not the kit's near-misses.
    static class CollectionStyle
    {
        static Color Hex(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

        public static readonly Color Cream = Hex(0xf4ead7ff);
        public static readonly Color Meta = Hex(0x8b93adff);
        public static readonly Color Edge = Hex(0x2c3145ff);
        public static readonly Color Shadow = Hex(0x05060aff);
        public static readonly Color Ink = Hex(0x0b0c11ff);        // text on a coloured plate
        public static readonly Color Number = Hex(0xe0604aff);
        public static readonly Color Ready = Hex(0x6fa85aff);
        public static readonly Color TabPlate = Hex(0x171b28ff);
        public static readonly Color TabSelected = Hex(0x243a5eff);
        public static readonly Color LockedBody = Hex(0x12141cff);
        public static readonly Color LockedMark = Hex(0x3a4152ff);
        public static readonly Color LockedHatch = new Color(0.78f, 0.73f, 0.69f, 1f);   // the slot's hatch, a step darker

        // index = DM2 stars: 0 (All) cream, 1 Common, 2 Rare, 3 Epic, 4 Legend
        static readonly Color[] Rarity = { Cream, Meta, Hex(0x5a9ad6ff), Hex(0xa06fd0ff), Hex(0xe8b13cff) };
        static readonly Color[] Body = { Hex(0x1a1d27ff), Hex(0x1a1d27ff), Hex(0x14213aff), Hex(0x221838ff), Hex(0x2e2412ff) };

        public static Color RarityColour(int stars) => Rarity[Mathf.Clamp(stars, 0, Rarity.Length - 1)];
        public static Color BodyColour(int stars) => Body[Mathf.Clamp(stars, 0, Body.Length - 1)];
    }

    // Card geometry in kit px. Outer = the card with its 3px frame; the design's 113x106 body comes out 110x106 on
    // the menu's 600 px content width. Rows on the design's 116 px pitch.
    const float CardW = 116f, CardH = 112f, CardFrame = 3f, CardGapX = 5f, CardPitchY = 116f, CardShadow = 3f;
    const float CardBodyW = CardW - 2f * CardFrame;
    const float CollectionPad = 8f;          // viewport padding, so a focus frame on the edge cards isn't clipped
    const float CollectionTop = -22f;        // viewport top, under the filter row
    const int CollectionVisibleRows = 2;     // whole rows on screen; the top of a third peeks under them

    // The Garage tab gets a taller content area than the other screens (two rows of cards and the peek need it):
    // it starts right under the tab row and runs down to the help line, and the status line borrows the help
    // line's place while it shows.
    static readonly Vector2 CollectionContentPos = new Vector2(20f, -70f), CollectionContentSize = new Vector2(600f, 268f);
    static readonly Vector2 DefaultContentPos = new Vector2(20f, -78f), DefaultContentSize = new Vector2(600f, 236f);

    int _collectionRarity;                      // 0 = all
    string _collectionSet;                      // null = every carset
    CollectionSort _collectionSort = CollectionSort.Rarity;
    int _collectionScroll;                      // first whole row on screen
    float _collectionDrag;                      // touch drag in progress, kit px (up = positive)
    List<CarDef> _collectionList = new();
    RectTransform _collectionViewport, _collectionGrid;
    bool _collectionLayout;

    string _garageCar;                          // the car CarDetail shows / R upgrades
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

    // ------------------------------------------------------------------ what the screen shows (EditMode tests)

    public int GarageFilteredCount { get; private set; }   // cards passing the filters
    public int GarageOwnedCount { get; private set; }      // of those, owned
    public int GarageLockedCount { get; private set; }     // of those, still to collect
    public int GarageCollectedCount { get; private set; }  // the header's "n / N collected"
    public int GarageCatalogueCount { get; private set; }
    public int GarageRowCount { get; private set; }
    public int GarageScroll => _collectionScroll;
    public int GarageCardsShown { get; private set; }      // cards on screen, the peeking row included
    public string GarageSelectedCar => _garageCar;
    public bool GarageDetailOpen => _overlay == Overlay.CarDetail;
    public IReadOnlyList<CarDef> GarageList => _collectionList;

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

    public void SetCollectionFilter(int rarity, string carset)
    {
        _collectionRarity = Mathf.Clamp(rarity, 0, CarCatalog.MaxRarity);
        _collectionSet = string.IsNullOrEmpty(carset) ? null : carset;
        _collectionScroll = 0;
        if (_content != null) Rebuild();
    }

    public void SetCollectionSort(CollectionSort sort)
    {
        _collectionSort = sort;
        _collectionScroll = 0;
        if (_content != null) Rebuild();
    }

    // Scroll so `row` is the first whole row on screen (clamped).
    public void ScrollCollection(int row)
    {
        _collectionScroll = row;
        if (_content != null) Rebuild();
    }

    // Open a car (owned or not) on the Card Detail screen. Opening doesn't change the race car: the screen's
    // Set As Race Car button does (SetGarageRaceCar).
    public void SelectGarageCar(string carId)
    {
        _garageCar = carId;
        _overlay = Overlay.CarDetail;
        _lastFocusKey = null;
        if (_content != null) Rebuild();
    }

    public void CloseCarDetail()
    {
        _overlay = Overlay.None;
        _lastFocusKey = _garageCar != null ? "car." + _garageCar : null;
        if (_content != null) Rebuild();
    }

    // Class up the open (or focused) car, paying coins (CarProgression.TryUpgrade). Returns why not when it can't.
    // Offline-only, like the rest of the garage: online this has to become a backend operation so a client can't
    // spend coins it hasn't got.
    public UpgradeCheck UpgradeSelectedCar()
    {
        var p = P;
        var owned = p?.Car(_garageCar);
        if (owned == null) { SetStatus("You don't own this car yet."); return UpgradeCheck.NeedGears; }
        var check = CarProgression.TryUpgrade(owned, ref p.coins);
        var def = Cars?.Get(owned.id);
        string name = def != null ? $"#{def.number} {def.driver}" : owned.id;
        if (check == UpgradeCheck.Ok) SaveTeam($"{name} is now Class {CardCollection.ClassLetter(owned.carClass)}.");
        else SetStatus(UpgradeReason(owned, check, p.coins));
        return check;
    }

    static string UpgradeReason(OwnedCar car, UpgradeCheck check, int coins) => check switch
    {
        UpgradeCheck.MaxClass => "Max class.",
        UpgradeCheck.NeedGears => $"{CarProgression.GearsToNextClass(car)} more gears to the next class.",
        UpgradeCheck.NeedCoins => $"The next class costs {CarProgression.UpgradeCost(car.carClass):N0} coins, you have {coins:N0}.",
        _ => "",
    };

    // R / pad North on the grid: upgrade the focused card.
    void UpgradeFocusedCard()
    {
        string key = FocusKey;
        if (key == null || !key.StartsWith("car.", StringComparison.Ordinal)) return;
        _garageCar = key.Substring(4);
        UpgradeSelectedCar();
    }

    // ------------------------------------------------------------------ layout and input hooks (MasterDraftUI.cs)

    bool OnCollection => _tab == Tab.Garage && _overlay == Overlay.None;

    // Before every Rebuild: drop the last grid and size the content area for the screen about to be built.
    void PrepareCollectionLayout()
    {
        if (_collectionGrid != null) Discard(_collectionGrid.gameObject);
        _collectionGrid = null;
        if (_collectionViewport != null) _collectionViewport.gameObject.SetActive(false);
        GarageCardsShown = 0;

        PrepareCarDetailLayer();
        _collectionLayout = OnCollection && P != null;
        if (_content != null)
            TopLeft(_content, _collectionLayout ? CollectionContentPos : DefaultContentPos,
                    _collectionLayout ? CollectionContentSize : DefaultContentSize);
        PlaceStatusLine();
    }

    void PlaceStatusLine()
    {
        if (_status == null) return;
        bool showing = !string.IsNullOrEmpty(_status.text);
        // The Card Detail layer covers the menu: both lines move onto its footer strip, over the layer.
        Bottom(_status.rectTransform, _detailLayout ? 8f : _collectionLayout ? 6f : 24f);
        if (_help != null)
        {
            _help.enabled = !((_collectionLayout || _detailLayout) && showing);
            Bottom(_help.rectTransform, _detailLayout ? 9f : 6f);
        }
        if (_detailLayout)
        {
            _help?.transform.SetAsLastSibling();
            _status.transform.SetAsLastSibling();
        }
    }

    // Mouse wheel over the grid: a row at a time.
    void CollectionUpdate()
    {
        if (!OnCollection || _busy) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f) return;
        ScrollBy(wheel > 0f ? -1 : 1);
    }

    void ScrollBy(int rows)
    {
        int to = CardCollection.ClampScroll(_collectionScroll + rows, _collectionList.Count, CollectionVisibleRows);
        if (to == _collectionScroll) return;
        _collectionScroll = to;
        // Keep the focus on a card that is still on screen.
        string key = FocusKey;
        int at = key != null && key.StartsWith("car.", StringComparison.Ordinal) ? IndexOfCar(key.Substring(4)) : -1;
        if (at >= 0)
        {
            int col = at % CardCollection.CardsPerRow;
            int row = Mathf.Clamp(at / CardCollection.CardsPerRow, to, to + CollectionVisibleRows - 1);
            int index = Mathf.Min(_collectionList.Count - 1, row * CardCollection.CardsPerRow + col);
            _lastFocusKey = "car." + _collectionList[index].id;
        }
        Rebuild();
    }

    int IndexOfCar(string id) => _collectionList.FindIndex(c => c.id == id);

    // ------------------------------------------------------------------ the screen

    void BuildGarage()
    {
        var p = P;
        var cars = Cars;
        GarageFilteredCount = GarageOwnedCount = GarageLockedCount = GarageRowCount = 0;
        GarageCollectedCount = CardCollection.Collected(cars, p.garage);
        GarageCatalogueCount = cars?.Count ?? 0;
        _collectionList = new List<CarDef>();
        if (cars == null || cars.Count == 0)
        {
            Text(_content, $"No car catalogue (Resources/{CarCatalog.ResourcePath}.json).", IronOvalUI.Role.Body, T.danger,
                 Vector2.zero, new Vector2(600f, 20f));
            return;
        }
        BuildCollectionFilters(cars);

        _collectionList = CardCollection.List(cars, p.garage, _collectionRarity, _collectionSet, _collectionSort);
        var list = _collectionList;
        GarageFilteredCount = list.Count;
        foreach (var c in list) if (p.Owns(c.id)) GarageOwnedCount++;
        GarageLockedCount = list.Count - GarageOwnedCount;
        GarageRowCount = CardCollection.Rows(list.Count);
        _collectionScroll = CardCollection.ClampScroll(_collectionScroll, list.Count, CollectionVisibleRows);

        if (list.Count == 0)
        {
            Text(_content, "No cars match these filters.", IronOvalUI.Role.Data, CollectionStyle.Meta,
                 new Vector2(0f, CollectionTop - 10f), new Vector2(600f, 18f));
            return;
        }

        EnsureCollectionViewport();
        _collectionViewport.gameObject.SetActive(true);
        _collectionGrid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
        _collectionGrid.SetParent(_collectionViewport, false);
        TopLeft(_collectionGrid, new Vector2(0f, _collectionDrag), Vector2.zero);

        // Whole rows, the peeking one, and a row either side for a drag to pull into view. Only the rows that
        // show are focusable; tapping the peeking row scrolls it up instead of opening it.
        int first = Mathf.Max(0, _collectionScroll - 1);
        int last = Mathf.Min(GarageRowCount - 1, _collectionScroll + CollectionVisibleRows + 1);
        for (int row = first; row <= last; row++)
        {
            for (int col = 0; col < CardCollection.CardsPerRow; col++)
            {
                int index = row * CardCollection.CardsPerRow + col;
                if (index >= list.Count) break;
                var def = list[index];
                var pos = new Vector2(CollectionPad + col * (CardW + CardGapX),
                                      -CollectionPad - (row - _collectionScroll) * CardPitchY);
                var card = CollectionCard(_collectionGrid, def, p.Car(def.id), pos);
                bool whole = row >= _collectionScroll && row < _collectionScroll + CollectionVisibleRows;
                bool peeking = row == _collectionScroll + CollectionVisibleRows;
                if (!whole && !peeking) continue;
                string id = def.id;
                GarageCardsShown++;
                AddItem(card, "car." + id, whole
                    ? () => { _lastFocusKey = "car." + id; SelectGarageCar(id); }
                    : () => { _lastFocusKey = "car." + id; ScrollBy(1); });
            }
        }

        _navOverride = CollectionNav;
    }

    // Arrows on the grid move card to card and scroll at the edges; up off the top row (scrolled to the top) and
    // anything off a filter falls through to the menu's spatial navigation.
    bool CollectionNav(Vector2 dir)
    {
        string key = FocusKey;
        if (key == null || !key.StartsWith("car.", StringComparison.Ordinal)) return false;
        int at = IndexOfCar(key.Substring(4));
        if (at < 0) return false;
        int perRow = CardCollection.CardsPerRow;
        int count = _collectionList.Count;
        int to;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            int col = at % perRow;
            to = dir.x > 0f ? at + 1 : at - 1;
            if ((dir.x > 0f && (col == perRow - 1 || to >= count)) || (dir.x < 0f && col == 0)) return true;   // row ends: stay
        }
        else if (dir.y > 0f)
        {
            if (at < perRow) return false;   // top row: up to the filters
            to = at - perRow;
        }
        else
        {
            if (at / perRow >= CardCollection.Rows(count) - 1) return true;   // bottom row
            to = Mathf.Min(count - 1, at + perRow);
        }

        _lastFocusKey = "car." + _collectionList[to].id;
        int scroll = CardCollection.ScrollToShow(to, _collectionScroll, count, CollectionVisibleRows);
        if (scroll != _collectionScroll)
        {
            _collectionScroll = scroll;
            Rebuild();
        }
        else FocusKeyOrFirst(_lastFocusKey);
        return true;
    }

    // The viewport outlives Rebuild (it carries the drag handler, which has to survive a drag that scrolls past a
    // row); the grid inside it is rebuilt every time.
    void EnsureCollectionViewport()
    {
        if (_collectionViewport != null) return;
        var go = new GameObject("CollectionViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                                typeof(MasterDraftDragScroll));
        go.transform.SetParent(_root, false);
        go.transform.SetSiblingIndex(_content.GetSiblingIndex() + 1);
        _collectionViewport = (RectTransform)go.transform;
        var pos = CollectionContentPos + new Vector2(-CollectionPad, CollectionTop);
        TopLeft(_collectionViewport, pos,
                new Vector2(CollectionContentSize.x + 2f * CollectionPad, CollectionContentSize.y + CollectionTop));
        var catcher = go.GetComponent<Image>();
        catcher.color = Color.clear;   // takes the drag on the gaps between cards
        var drag = go.GetComponent<MasterDraftDragScroll>();
        drag.onDrag = OnCollectionDrag;
        drag.onEnd = OnCollectionDragEnd;
    }

    void OnCollectionDrag(float up)
    {
        if (!OnCollection || _busy) return;
        int rows = CardCollection.Rows(_collectionList.Count);
        int maxScroll = Mathf.Max(0, rows - CollectionVisibleRows);
        _collectionDrag += up;
        // Past a whole row: scroll and carry the rest of the drag.
        while (_collectionDrag >= CardPitchY && _collectionScroll < maxScroll) { _collectionDrag -= CardPitchY; _collectionScroll++; Rebuild(); }
        while (_collectionDrag <= -CardPitchY && _collectionScroll > 0) { _collectionDrag += CardPitchY; _collectionScroll--; Rebuild(); }
        // Nothing past either end: up (positive) only while there are rows below, down only while there are above.
        float min = _collectionScroll > 0 ? -CardPitchY : 0f;
        float max = _collectionScroll < maxScroll ? CardPitchY : 0f;
        _collectionDrag = Mathf.Clamp(_collectionDrag, min, max);
        if (_collectionGrid != null) _collectionGrid.anchoredPosition = new Vector2(0f, _collectionDrag);
    }

    void OnCollectionDragEnd()
    {
        if (!OnCollection) { _collectionDrag = 0f; return; }
        int rows = Mathf.RoundToInt(_collectionDrag / CardPitchY);
        _collectionDrag = 0f;
        _collectionScroll = CardCollection.ClampScroll(_collectionScroll + rows, _collectionList.Count, CollectionVisibleRows);
        Rebuild();
    }

    // ------------------------------------------------------------------ filter row

    void BuildCollectionFilters(CarCatalog cars)
    {
        // Rarity tabs, sized to the design's (ALL 40, COMMON 69, RARE 49, EPIC 46, LEGEND 64) and 6 px apart.
        float[] widths = { 40f, 69f, 49f, 46f, 64f };
        float x = 2f;
        for (int r = 0; r <= CarCatalog.MaxRarity; r++)
        {
            int rarity = r;
            var tab = FilterTab(_content, "Rarity_" + CardCollection.RarityName(r), CardCollection.RarityName(r),
                                CollectionStyle.RarityColour(r), rarity == _collectionRarity, new Vector2(x, -2f), widths[r]);
            AddItem(tab, "cf.r" + r, () => { _lastFocusKey = "cf.r" + rarity; SetCollectionFilter(rarity, _collectionSet); });
            x += widths[r] + 6f;
        }

        // Right-aligned cyclers: Sort, and the carset to its left.
        var sort = Cycler(_content, "Sort", "Sort: " + CardCollection.SortName(_collectionSort), 600f);
        AddItem(sort, "cf.sort", () =>
        {
            _lastFocusKey = "cf.sort";
            SetCollectionSort(CardCollection.NextSort(_collectionSort));
        });
        string setName = _collectionSet == null ? "All" : cars.GetCarset(_collectionSet)?.name ?? _collectionSet;
        var set = Cycler(_content, "Set", "Set: " + setName, sort.anchoredPosition.x - 14f);
        AddItem(set, "cf.set", () =>
        {
            _lastFocusKey = "cf.set";
            SetCollectionFilter(_collectionRarity, NextCarset(cars, _collectionSet));
        });
    }

    static string NextCarset(CarCatalog cars, string current)
    {
        var sets = cars.Carsets;
        if (sets.Count == 0) return null;
        int i = -1;
        for (int k = 0; k < sets.Count; k++)
            if (string.Equals(sets[k].id, current, StringComparison.OrdinalIgnoreCase)) i = k;
        return i + 1 >= sets.Count ? null : sets[i + 1].id;
    }

    // A rarity tab: dark plate in a 2px frame (cream and a lighter plate when picked), caption in the rarity's
    // colour, tracked Silkscreen.
    RectTransform FilterTab(Transform parent, string name, string caption, Color colour, bool selected, Vector2 pos, float width)
    {
        var rt = Plate(parent, name, selected ? CollectionStyle.TabSelected : CollectionStyle.TabPlate, pos, new Vector2(width, 18f));
        Frame(rt, selected ? CollectionStyle.Cream : CollectionStyle.Edge, 2f, 2f);
        var label = Small(rt, caption, colour, new Vector2(0f, -4f), new Vector2(width, 10f), tracked: true);
        label.alignment = TextAlignmentOptions.Top;
        return rt;
    }

    // "SORT: RARITY ▾" — meta Silkscreen ending at `right`, with the design's little drop arrow drawn in pixels
    // (Silkscreen has no ▾).
    RectTransform Cycler(Transform parent, string name, string caption, float right)
    {
        var label = Small(parent, caption, CollectionStyle.Meta, Vector2.zero, new Vector2(200f, 10f));
        float w = Mathf.Ceil(label.GetPreferredValues(caption).x);
        float total = w + 9f;
        var rt = Plate(parent, "Cycler_" + name, Color.clear, new Vector2(right - total, -2f), new Vector2(total, 18f));
        label.transform.SetParent(rt, false);
        TopLeft(label.rectTransform, new Vector2(0f, -5f), new Vector2(w + 2f, 10f));
        for (int i = 0; i < 3; i++)
            Plate(rt, "Arrow", CollectionStyle.Meta, new Vector2(w + 3f + i, -8f - i), new Vector2(5f - 2f * i, 1f));
        return rt;
    }

    // ------------------------------------------------------------------ a card

    // One card as the design draws it. Owned: rarity frame and plate, class badge and number, livery on the hatch
    // slot, driver, rarity (and carset), gears bar and caption, "UP" when the upgrade can be paid. Not owned: the
    // dark "?" card with the rarity stripe, rarity and the carset it's in.
    RectTransform CollectionCard(Transform parent, CarDef def, OwnedCar owned, Vector2 pos)
    {
        Plate(parent, "Shadow_" + def.id, CollectionStyle.Shadow, pos + new Vector2(CardShadow, -CardShadow), new Vector2(CardW, CardH));
        Color rarity = CollectionStyle.RarityColour(def.rarity);
        var card = Plate(parent, "Card_" + def.id, owned != null ? rarity : CollectionStyle.Edge, pos, new Vector2(CardW, CardH));
        var body = Plate(card, "Body", owned != null ? CollectionStyle.BodyColour(def.rarity) : CollectionStyle.LockedBody,
                         new Vector2(CardFrame, -CardFrame), new Vector2(CardBodyW, CardH - 2f * CardFrame));
        float inner = CardBodyW - 10f;

        if (owned == null)
        {
            body.name = "Locked";
            Plate(body, "Stripe", rarity, new Vector2(5f, -5f), new Vector2(inner, 3f));
            var slot = Plate(body, "Slot", CollectionStyle.LockedHatch, new Vector2(5f, -12f), new Vector2(inner, 58f));
            Hatch(slot, CollectionStyle.LockedHatch);
            var mark = Text(slot, "?", IronOvalUI.Role.BodyLarge, CollectionStyle.LockedMark, new Vector2(0f, -6f), new Vector2(inner, 50f));
            mark.alignment = TextAlignmentOptions.Top;
            Small(body, CardCollection.RarityName(def.rarity), rarity, new Vector2(5f, -76f), new Vector2(inner, 10f), tracked: true);
            Small(body, "In: " + SetName(def), CollectionStyle.Meta, new Vector2(5f, -89f), new Vector2(inner, 10f))
                .overflowMode = TextOverflowModes.Ellipsis;
            return card;
        }

        var progress = CardCollection.Progress(owned, P.coins);

        // Class badge (where the design has LV1-LV6) and the car number.
        string badge = "Class " + CardCollection.ClassLetter(owned.carClass);
        var badgeText = Small(body, badge, CollectionStyle.Ink, Vector2.zero, new Vector2(80f, 10f));
        float badgeW = Mathf.Ceil(badgeText.GetPreferredValues(badge).x) + 5f;
        var plate = Plate(body, "Class", rarity, new Vector2(5f, -5f), new Vector2(badgeW, 10f));
        badgeText.transform.SetParent(plate, false);
        TopLeft(badgeText.rectTransform, new Vector2(3f, 0f), new Vector2(badgeW, 10f));
        var number = Small(body, "#" + def.number, CollectionStyle.Number, new Vector2(5f, -6f), new Vector2(inner, 10f));
        number.alignment = TextAlignmentOptions.TopRight;

        // Livery on the hatched slot, in a 1px frame.
        var slotRt = Plate(body, "Slot", Color.white, new Vector2(5f, -18f), new Vector2(inner, 34f));
        Hatch(slotRt, Color.white);
        Frame(slotRt, CollectionStyle.Edge, 1f, 1f);
        var sprite = Livery(def);
        if (sprite != null)
        {
            var livery = Plate(slotRt, "Livery", Color.white, new Vector2((inner - 64f) / 2f, -1f), new Vector2(64f, 32f))
                .GetComponent<Image>();
            livery.sprite = sprite;
            livery.preserveAspect = true;
        }
        else
        {
            var none = Small(slotRt, "[ Car ]", CollectionStyle.Meta, new Vector2(0f, -13f), new Vector2(inner, 10f));
            none.alignment = TextAlignmentOptions.Top;
        }

        var name = Text(body, def.driver ?? def.id, IronOvalUI.Role.Data, CollectionStyle.Cream, new Vector2(5f, -54f), new Vector2(inner, 16f));
        name.name = "Name";
        name.overflowMode = TextOverflowModes.Ellipsis;

        Small(body, CardCollection.RarityName(def.rarity), rarity, new Vector2(5f, -74f), new Vector2(inner, 10f), tracked: true);
        var set = Small(body, SetName(def), CollectionStyle.Meta, new Vector2(5f, -74f), new Vector2(inner, 10f));
        set.alignment = TextAlignmentOptions.TopRight;

        // Gears bar: the rarity's colour, full green once the next class's gears are banked.
        var bar = Plate(body, "GearsBar", CollectionStyle.Edge, new Vector2(5f, -85f), new Vector2(inner, 6f));
        Frame(bar, CollectionStyle.Shadow, 1f, 1f);
        float fill = progress.ready ? 1f : progress.fill;
        if (fill > 0f)
            Plate(bar, "Fill", progress.ready ? CollectionStyle.Ready : rarity, Vector2.zero, new Vector2(Mathf.Round(inner * fill), 6f));
        var caption = Small(body, progress.caption, progress.ready ? CollectionStyle.Ready : CollectionStyle.Meta,
                            new Vector2(5f, -94f), new Vector2(inner, 10f));
        caption.name = "Gears";

        if (progress.canUpgrade)
        {
            // "▲ UP" over the top-right corner: green plate in a 2px dark frame, the arrow drawn in pixels.
            var up = Plate(card, "Up", CollectionStyle.Ready, new Vector2(CardFrame + 86f, -CardFrame + 7f), new Vector2(31f, 12f));
            Frame(up, CollectionStyle.Shadow, 2f, 2f);
            for (int i = 0; i < 3; i++)
                Plate(up, "Arrow", CollectionStyle.Ink, new Vector2(4f + 2f - i, -4f - i), new Vector2(1f + 2f * i, 1f));
            Small(up, "Up", CollectionStyle.Ink, new Vector2(11f, -1f), new Vector2(20f, 10f));
        }
        return card;
    }

    string SetName(CarDef def) => Cars?.GetCarset(def.carset)?.name ?? def.carset;

    // The car detail (the CarDetail overlay) is the Card Detail screen: MasterDraftUI.CardDetail.cs.

    // ------------------------------------------------------------------ building blocks

    // A flat rect, top-left placed. Never a raycast target unless AddItem makes it one.
    static RectTransform Plate(Transform parent, string name, Color colour, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        TopLeft(rt, pos, size);
        return rt;
    }

    // A frame of `thickness` px drawn `outset` px outside the rect (the design's px_frame outlines).
    static void Frame(RectTransform target, Color colour, float thickness, float outset)
    {
        var go = new GameObject("Frame", typeof(RectTransform));
        go.transform.SetParent(target, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
        Edge(rt, colour, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        Edge(rt, colour, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        Edge(rt, colour, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, thickness), new Vector2(thickness, -thickness));
        Edge(rt, colour, new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, thickness), new Vector2(0f, -thickness));
    }

    static void Edge(RectTransform parent, Color colour, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    // The kit's hatched art-slot tile (the design's stripes, same two colours) filling a plate.
    static void Hatch(RectTransform slot, Color tint)
    {
        var img = slot.GetComponent<Image>();
        var t = T;
        if (t == null || t.hatchSlot == null) { img.color = CollectionStyle.Edge * tint; return; }
        img.sprite = t.hatchSlot;
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = tint;
    }

    // 8px Silkscreen: tracked 1px for the rarity words and tabs, untracked for numbers and captions.
    static TextMeshProUGUI Small(Transform parent, string content, Color colour, Vector2 pos, Vector2 size, bool tracked = false)
    {
        var label = Text(parent, content, IronOvalUI.Role.HeaderSmall, colour, pos, size);
        if (!tracked) label.characterSpacing = 0f;
        return label;
    }
}
