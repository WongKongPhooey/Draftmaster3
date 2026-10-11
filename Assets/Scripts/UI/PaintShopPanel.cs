using System;
using System.Collections.Generic;
using Draftmaster.Sponsors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The Paint Scheme Designer's screen at Team HQ: base coat, a livery pattern over it and the colour it is
// sprayed in, then who goes where — PRIMARY on the hood, SECONDARY on both rear quarters, TRUNK on the
// decklid — chosen from the deals the player has signed. Everything previews live on the car; nothing is
// kept until SAVE, which writes the scheme to PaintBook and the layout to SponsorBook in one go.
//
// Runtime-built uGUI in the SponsorBoardPanel pattern — no prefab or font wiring. Opened by PaintShopNPC.
public class PaintShopPanel : MonoBehaviour
{
    public static PaintShopPanel Instance { get; private set; }

    static readonly SponsorSlot[] kSlots = { SponsorSlot.Hood, SponsorSlot.Quarters, SponsorSlot.Tail };

    GameObject _root;
    RawImage _preview;
    Text _status, _income, _pattern, _paintNote;
    readonly Dictionary<SponsorSlot, Text> _slotText = new();
    readonly List<(Image ring, Color32 colour)> _baseSwatches = new();
    readonly List<(Image ring, Color32 colour)> _accentSwatches = new();
    GameObject _accentRow;
    Button _firstButton;
    Font _font;
    Action _onClosed;

    // Working copy — the car as it will be if the player saves.
    PaintScheme _scheme;
    bool _factory;                                   // true = the livery exactly as drawn, no repaint
    readonly Dictionary<SponsorSlot, int> _assign = new();
    string _livery;
    bool _canRepaint;

    public static PaintShopPanel Ensure()
    {
        if (Instance != null) return Instance;
        return new GameObject("PaintShopPanel").AddComponent<PaintShopPanel>();
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Build();
        _root.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (!IsOpen) return;
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            Close(save: false);
    }

    // ---------------------------------------------------------------- open / close

    public void Show(Action onClosed = null)
    {
        _onClosed = onClosed;
        SponsorCatalog.All();       // a new career's starter sponsor is handed out once the brands load
        _livery = PaintShop.PlayerLiveryName;
        _canRepaint = PaintShop.CanRepaint(_livery);

        var saved = PaintBook.Current;
        _factory = saved == null;
        _scheme = saved != null ? saved.Clone() : new PaintScheme { baseColour = PaintShop.Palette[0] };

        _assign.Clear();
        foreach (var slot in kSlots) _assign[slot] = 0;
        foreach (var deal in SponsorBook.Deals)
        {
            if (!deal.IsActive || !deal.IsPlaced) continue;
            var slot = deal.slot == SponsorSlot.QuarterLeft || deal.slot == SponsorSlot.QuarterRight ? SponsorSlot.Quarters : deal.slot;
            if (_assign.TryGetValue(slot, out int taken) && taken == 0) _assign[slot] = deal.id;
        }

        _status.text = "";
        _paintNote.text = _canRepaint ? "" : "This car's paint hasn't been split into layers yet — sponsors only.";
        _root.SetActive(true);
        Refresh();
        if (_firstButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
    }

    void Close(bool save)
    {
        if (save)
        {
            if (_canRepaint)
            {
                if (_factory) PaintBook.ClearAll();
                else PaintBook.Save(_scheme);
            }
            SponsorBook.Arrange(_assign);
        }
        _root.SetActive(false);
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    public void Hide() => Close(save: false);

    // ---------------------------------------------------------------- choices

    void PickBase(Color32 c)
    {
        if (!_canRepaint) return;
        _scheme.baseColour = c;
        _factory = false;
        Refresh();
    }

    void PickAccent(Color32 c)
    {
        if (!_canRepaint) return;
        _scheme.accentColour = c;
        _factory = false;
        Refresh();
    }

    void CyclePattern(int step)
    {
        if (!_canRepaint) return;
        var list = PaintShop.Patterns;
        int i = Mathf.Max(0, IndexOf(list, _scheme.pattern));
        i = (i + step + list.Count) % list.Count;
        _scheme.pattern = list[i];
        _factory = false;
        Refresh();
    }

    void FactoryPaint()
    {
        if (!_canRepaint) return;
        _factory = true;
        _status.text = "Back to the paint the car was built in.";
        Refresh();
    }

    void CycleSponsor(SponsorSlot slot, int step)
    {
        var options = new List<int> { 0 };
        foreach (var d in SponsorBook.Deals) if (d.IsActive) options.Add(d.id);

        int i = Mathf.Max(0, options.IndexOf(_assign[slot]));
        i = (i + step + options.Count) % options.Count;
        int pick = options[i];
        _assign[slot] = pick;
        _status.text = "";

        // One brand, one panel: taking a deal that sat somewhere else leaves that panel empty.
        if (pick != 0)
            foreach (var other in kSlots)
                if (other != slot && _assign[other] == pick)
                {
                    _assign[other] = 0;
                    _status.text = $"{SponsorBook.ById(pick)?.sponsorName} moved off the {SponsorSlots.DesignerName(other).ToLowerInvariant()} slot.";
                }
        Refresh();
    }

    // ---------------------------------------------------------------- view

    void Refresh()
    {
        // Car preview: the real paint pipeline, so this is exactly what goes racing.
        var logos = new Dictionary<SponsorSlot, string>();
        foreach (var slot in kSlots)
        {
            var deal = _assign[slot] != 0 ? SponsorBook.ById(_assign[slot]) : null;
            if (deal != null) logos[slot] = deal.logoKey;
        }
        var sprite = PaintShop.RenderWithSponsors(_livery, _factory ? null : _scheme, logos);
        if (sprite != null)
        {
            var tex = sprite.texture;
            tex.filterMode = FilterMode.Point;
            _preview.texture = tex;
            var r = sprite.textureRect;
            _preview.uvRect = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            _preview.color = Color.white;
        }
        else _preview.color = new Color(1, 1, 1, 0);

        foreach (var (ring, colour) in _baseSwatches)
            ring.enabled = !_factory && Same(colour, _scheme.baseColour);
        foreach (var (ring, colour) in _accentSwatches)
            ring.enabled = !_factory && Same(colour, _scheme.accentColour);

        _pattern.text = _factory ? "FACTORY PAINT" : PaintShop.PatternLabel(_scheme.pattern);
        if (_accentRow != null) _accentRow.SetActive(!_factory && _scheme.HasPattern);

        int income = 0;
        foreach (var slot in kSlots)
        {
            var deal = _assign[slot] != 0 ? SponsorBook.ById(_assign[slot]) : null;
            string text;
            if (deal == null) text = "— empty —";
            else
            {
                int pay = Mathf.RoundToInt(deal.perRace * SponsorSlots.PayMultiplier(slot));
                income += pay;
                text = $"{deal.sponsorName}  ${pay:N0}/race";
                if (SponsorArt.Load(deal.logoKey, slot) == null) text += "  (no decal art)";
            }
            _slotText[slot].text = text;
        }
        _income.text = $"On the car: ${income:N0} per race";
    }

    static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

    static int IndexOf(IReadOnlyList<string> list, string s)
    {
        for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], s ?? "", StringComparison.Ordinal)) return i;
        return -1;
    }

    // ---------------------------------------------------------------- build

    const float CardW = 1560f, CardH = 900f;
    const float ColX = 820f;                 // right-hand column, from the card's left edge

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;                       // above the sponsor board and garage cards
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        _font = BrandFonts.Body;

        _root = new GameObject("Panel", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var dim = _root.AddComponent<Image>();
        dim.color = new Color(PixelGUI.ScreenBase.r, PixelGUI.ScreenBase.g, PixelGUI.ScreenBase.b, 0.9f);
        Stretch(_root.GetComponent<RectTransform>());

        var card = Box("Card", _root.transform, PixelGUI.Plate);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(CardW, CardH);

        var title = Label("Title", crt, BrandFonts.Display, 32, PixelGUI.Gold, TextAnchor.UpperLeft);
        title.text = "PAINT SCHEME DESIGNER";
        At(title.rectTransform, 44, 32, 900, 48);
        var sub = Label("Sub", crt, _font, 24, PixelGUI.TextDim, TextAnchor.UpperLeft);
        sub.text = "Pick a base coat and a livery, then who goes where. Nothing changes until you save.";
        At(sub.rectTransform, 44, 84, 1400, 36);

        // --- car preview, left ---
        var plate = Box("PreviewPlate", crt, PixelGUI.PlateDeep);
        At(plate.rectTransform, 44, 150, 720, 400);
        var pgo = new GameObject("Preview", typeof(RectTransform));
        pgo.transform.SetParent(plate.transform, false);
        _preview = pgo.AddComponent<RawImage>();
        var prt = pgo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640, 320);           // 64x32 livery at 10x

        _income = Label("Income", crt, _font, 28, PixelGUI.Confirm, TextAnchor.UpperLeft);
        At(_income.rectTransform, 44, 570, 720, 40);
        _paintNote = Label("PaintNote", crt, _font, 22, PixelGUI.TextDisabled, TextAnchor.UpperLeft);
        At(_paintNote.rectTransform, 44, 614, 720, 60);

        // --- right column ---
        float y = 150f;
        y = Heading(crt, "BASE COAT", y);
        y = SwatchGrid(crt, y, _baseSwatches, PickBase);

        y = Heading(crt, "LIVERY", y + 8f);
        _pattern = Stepper(crt, y, step => CyclePattern(step), out var patternLeft);
        _firstButton = patternLeft;
        y += 60f;

        _accentRow = new GameObject("AccentRow", typeof(RectTransform));
        _accentRow.transform.SetParent(crt, false);
        var art = _accentRow.GetComponent<RectTransform>();
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
        art.anchoredPosition = Vector2.zero;
        art.sizeDelta = new Vector2(CardW, CardH);
        float ay = Heading(art, "LIVERY COLOUR", y);
        y = SwatchGrid(art, ay, _accentSwatches, PickAccent) + 4f;

        y = Heading(crt, "SPONSORS", y);
        foreach (var slot in kSlots)
        {
            var label = Label("Slot", crt, BrandFonts.Display, 16, PixelGUI.Gold, TextAnchor.MiddleLeft);
            label.text = $"{SponsorSlots.DesignerName(slot)}  {PanelWord(slot)}  {SponsorSlots.PayMultiplier(slot):P0}";
            At(label.rectTransform, ColX, y, 300, 52);
            var captured = slot;
            _slotText[slot] = Stepper(crt, y, step => CycleSponsor(captured, step), out _, x: ColX + 300f, width: 400f);
            y += 60f;
        }

        // --- footer ---
        _status = Label("Status", crt, _font, 24, PixelGUI.TextDim, TextAnchor.UpperLeft);
        At(_status.rectTransform, 44, 690, 720, 60);

        MakeButton("FACTORY PAINT", crt, 44, CardH - 92, 260, FactoryPaint);
        MakeButton("CANCEL", crt, CardW - 44 - 220 - 20 - 260, CardH - 92, 260, () => Close(save: false));
        MakeButton("SAVE", crt, CardW - 44 - 220, CardH - 92, 220, () => Close(save: true));

        var hint = Label("Hint", crt, _font, 20, PixelGUI.TextDisabled, TextAnchor.MiddleLeft);
        hint.text = $"{InputGlyphs.Back} to leave without saving";
        At(hint.rectTransform, 330, CardH - 92, 600, 60);
    }

    static string PanelWord(SponsorSlot slot) => slot switch
    {
        SponsorSlot.Hood => "HOOD",
        SponsorSlot.Quarters => "QUARTERS",
        SponsorSlot.Tail => "DECKLID",
        _ => "",
    };

    float Heading(RectTransform parent, string text, float y)
    {
        var t = Label("Heading", parent, BrandFonts.Display, 20, PixelGUI.Gold, TextAnchor.LowerLeft);
        t.text = text;
        At(t.rectTransform, ColX, y, 700, 32);
        return y + 40f;
    }

    // Two rows of eight colour chips. The ring behind a chip shows which is chosen.
    float SwatchGrid(RectTransform parent, float y, List<(Image, Color32)> into, Action<Color32> pick)
    {
        const float size = 44f, gap = 6f;
        for (int i = 0; i < PaintShop.Palette.Length; i++)
        {
            int col = i % 8, row = i / 8;
            float x = ColX + col * (size + gap), yy = y + row * (size + gap);

            var ring = Box("Ring", parent, Color.white);
            At(ring.rectTransform, x - 4, yy - 4, size + 8, size + 8);
            ring.enabled = false;

            Color32 c = PaintShop.Palette[i];
            var chip = Box("Swatch", parent, c);
            At(chip.rectTransform, x, yy, size, size);
            var b = chip.gameObject.AddComponent<Button>();
            b.targetGraphic = chip;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.selectedColor = colors.highlightedColor;
            b.colors = colors;
            b.onClick.AddListener(() => pick(c));
            into.Add((ring, c));
        }
        int rows = (PaintShop.Palette.Length + 7) / 8;
        return y + rows * (size + gap) + 4f;
    }

    // [<]  value  [>]
    Text Stepper(RectTransform parent, float y, Action<int> step, out Button left, float x = ColX, float width = 700f)
    {
        left = MakeButton("<", parent, x, y, 60, () => step(-1));
        MakeButton(">", parent, x + width - 60, y, 60, () => step(+1));
        var value = Label("Value", parent, _font, 26, PixelGUI.Text, TextAnchor.MiddleCenter);
        At(value.rectTransform, x + 64, y, width - 128, 52);
        return value;
    }

    Button MakeButton(string text, RectTransform parent, float x, float y, float w, UnityEngine.Events.UnityAction onClick)
    {
        var img = Box("Button", parent, PixelGUI.PlateLight);
        At(img.rectTransform, x, y, w, 52);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        var t = Label("Text", img.rectTransform, BrandFonts.Display, 16, PixelGUI.Gold, TextAnchor.MiddleCenter);
        t.text = text;
        Stretch(t.rectTransform);
        return b;
    }

    static Image Box(string name, Transform parent, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = colour;
        return img;
    }

    Text Label(string name, Transform parent, Font font, int size, Color colour, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = colour;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Top-left placement in card pixels (y measured down from the card's top edge).
    static void At(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
