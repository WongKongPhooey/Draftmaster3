using System;
using System.Collections.Generic;
using Draftmaster.Controls;
using Draftmaster.Sponsors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The Paint Scheme Designer's screen at Team HQ: base coat, a livery pattern over it and the colour it is
// sprayed in, the colour of the car number, then who goes where — PRIMARY on the hood, SECONDARY on both rear quarters, TRUNK on the
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
    readonly List<(Image ring, Color32 colour)> _numberSwatches = new();
    GameObject _accentRow;
    Button _firstButton;
    Font _font;
    Action _onClosed;

    // Working copy — the car as it will be if the player saves. _scheme.factory = the body exactly as drawn.
    PaintScheme _scheme;
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
        _scheme = saved != null ? saved.Clone() : new PaintScheme { factory = true, baseColour = PaintShop.Palette[0] };

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
                if (_scheme.IsStock) PaintBook.ClearAll();
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
        _scheme.factory = false;
        Refresh();
    }

    void PickAccent(Color32 c)
    {
        if (!_canRepaint) return;
        _scheme.accentColour = c;
        _scheme.factory = false;
        Refresh();
    }

    // The number is its own layer, so it can be recoloured over factory paint too.
    void PickNumber(Color32 c)
    {
        if (!_canRepaint) return;
        _scheme.numberColour = c;
        Refresh();
    }

    void CyclePattern(int step)
    {
        if (!_canRepaint) return;
        var list = PaintShop.Patterns;
        int i = Mathf.Max(0, IndexOf(list, _scheme.pattern));
        i = (i + step + list.Count) % list.Count;
        _scheme.pattern = list[i];
        _scheme.factory = false;
        Refresh();
    }

    void FactoryPaint()
    {
        if (!_canRepaint) return;
        _scheme.factory = true;
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
                    _status.text = $"{SponsorBook.ById(pick)?.sponsorName} moved off the {SlotName(other).ToLowerInvariant()} slot.";
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
        var sprite = PaintShop.RenderWithSponsors(_livery, _scheme, logos);
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

        bool factory = _scheme.factory;
        foreach (var (ring, colour) in _baseSwatches)
            ring.enabled = !factory && Same(colour, _scheme.baseColour);
        foreach (var (ring, colour) in _accentSwatches)
            ring.enabled = !factory && Same(colour, _scheme.accentColour);
        foreach (var (ring, colour) in _numberSwatches)
            ring.enabled = _canRepaint && Same(colour, _scheme.numberColour);

        _pattern.text = factory ? "Factory Paint" : PaintShop.PatternLabel(_scheme.pattern);
        if (_accentRow != null) _accentRow.SetActive(!factory && _scheme.HasPattern);

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
                text = $"{deal.sponsorName} ${pay:N0}";   // per race, as the income line says
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

    // Sizes off the shared type scale, on this 1920x1080 canvas, as the laptop sheet sets them on its 640x360
    // one: Fixedsys at the Data size for the title, rows and readouts, gold Silkscreen at the same size for
    // section headings, Silkscreen at the Label floor for captions and footers.
    const float RefH = 1080f;
    static int DataPx => UIType.CanvasPx(TypeRole.Data, RefH, 16);
    static int HeadingPx => UIType.CanvasPx(TypeRole.Data, RefH, 8);
    static int LabelPx => UIType.CanvasPx(TypeRole.Label, RefH, 8);
    static float LineH => DataPx + 8f;                 // one Fixedsys line: an 18px line per 16px cell

    const float CardW = 1840f, CardH = 940f;
    const float Margin = 44f;
    const float LeftW = 700f;
    const float ColX = Margin + LeftW + 40f;           // right-hand column, from the card's left edge
    const float ColW = CardW - Margin - ColX;
    const float TopY = 104f;                          // first row under the title

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

        var title = Label("Title", crt, _font, DataPx, PixelGUI.Text, TextAnchor.UpperLeft);
        title.text = "Paint Booth";
        At(title.rectTransform, Margin, 32, 900, LineH);

        // --- car preview, left ---
        var plate = Box("PreviewPlate", crt, PixelGUI.PlateDeep);
        At(plate.rectTransform, Margin, TopY, LeftW, 360);
        var pgo = new GameObject("Preview", typeof(RectTransform));
        pgo.transform.SetParent(plate.transform, false);
        _preview = pgo.AddComponent<RawImage>();
        var prt = pgo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640, 320);           // 64x32 livery at 10x

        // --- number colour, under the preview: one row of the whole palette ---
        float ny = Heading(crt, "Number Colour", TopY + 368f, x: Margin);
        ny = SwatchGrid(crt, ny, _numberSwatches, PickNumber, x0: Margin, size: 37f, gap: 6f);

        _income = Label("Income", crt, _font, DataPx, PixelGUI.Confirm, TextAnchor.UpperLeft);
        At(_income.rectTransform, Margin, ny + 4f, LeftW, LineH);
        _paintNote = Label("PaintNote", crt, BrandFonts.Display, LabelPx, PixelGUI.TextDisabled, TextAnchor.UpperLeft);
        At(_paintNote.rectTransform, Margin, ny + 8f + LineH, LeftW, 2 * LabelPx + 8f);

        // --- right column ---
        float y = TopY;
        y = Heading(crt, "Base Coat", y);
        y = SwatchGrid(crt, y, _baseSwatches, PickBase);

        y = Heading(crt, "Livery", y + 8f);
        _pattern = Stepper(crt, y, step => CyclePattern(step), out var patternLeft);
        _firstButton = patternLeft;
        y += RowPitch;

        _accentRow = new GameObject("AccentRow", typeof(RectTransform));
        _accentRow.transform.SetParent(crt, false);
        var art = _accentRow.GetComponent<RectTransform>();
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 1);
        art.anchoredPosition = Vector2.zero;
        art.sizeDelta = new Vector2(CardW, CardH);
        float ay = Heading(art, "Livery Colour", y);
        y = SwatchGrid(art, ay, _accentSwatches, PickAccent) + 4f;

        y = Heading(crt, "Sponsors", y);
        const float captionW = 320f;
        foreach (var slot in kSlots)
        {
            var label = Label("Slot", crt, _font, DataPx, PixelGUI.Text, TextAnchor.MiddleLeft);
            label.text = SlotName(slot);
            At(label.rectTransform, ColX, y, captionW, IronOvalUI.ButtonHeight());
            var captured = slot;
            _slotText[slot] = Stepper(crt, y, step => CycleSponsor(captured, step), out _, x: ColX + captionW, width: ColW - captionW);
            y += RowPitch;
        }

        // --- footer ---
        float footY = CardH - 32f - IronOvalUI.ButtonHeight();
        _status = Label("Status", crt, _font, DataPx, PixelGUI.TextDim, TextAnchor.UpperLeft);
        float statusY = ny + 16f + LineH + 2 * LabelPx + 8f;
        At(_status.rectTransform, Margin, statusY, LeftW, footY - statusY - 4f);

        // Paint "1" is the factory paint — the first of a row of up to six saved paints to switch between.
        float paintW = IronOvalUI.ButtonHeight();
        const float exitW = 200f, saveW = 200f, gap = 20f;
        MakeButton("1", crt, Margin, footY, paintW, FactoryPaint);
        MakeButton("Exit", crt, CardW - Margin - saveW - gap - exitW, footY, exitW, () => Close(save: false));
        MakeButton("Save", crt, CardW - Margin - saveW, footY, saveW, () => Close(save: true));

        // Clear of where the full row of six paints will sit.
        var hint = Label("Hint", crt, BrandFonts.Display, LabelPx, PixelGUI.TextDisabled, TextAnchor.MiddleLeft);
        hint.text = $"{InputGlyphs.Back} to leave without saving";
        At(hint.rectTransform, Margin + 6f * (paintW + gap) + 4f, footY, 600, IronOvalUI.ButtonHeight());
    }

    static string SlotName(SponsorSlot slot) => slot switch
    {
        SponsorSlot.Hood => "Hood",
        SponsorSlot.Quarters => "Rear Quarters",
        SponsorSlot.Tail => "Trunk",
        _ => "",
    };

    float Heading(RectTransform parent, string text, float y, float x = ColX)
    {
        var t = Label("Heading", parent, BrandFonts.Display, HeadingPx, PixelGUI.Gold, TextAnchor.LowerLeft);
        t.text = text;
        At(t.rectTransform, x, y, ColW, LineH);
        return y + LineH + 4f;
    }

    // The palette as rows of colour chips (one row of sixteen by default). The ring behind a chip shows which
    // is chosen.
    float SwatchGrid(RectTransform parent, float y, List<(Image, Color32)> into, Action<Color32> pick,
                     float x0 = ColX, int perRow = 16, float size = 52f, float gap = 6f)
    {
        for (int i = 0; i < PaintShop.Palette.Length; i++)
        {
            int col = i % perRow, row = i / perRow;
            float x = x0 + col * (size + gap), yy = y + row * (size + gap);

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
        int rows = (PaintShop.Palette.Length + perRow - 1) / perRow;
        return y + rows * (size + gap) + 4f;
    }

    // A stepper row: one button tall with 4px clear under it.
    static float RowPitch => IronOvalUI.ButtonHeight() + 4f;

    // [<]  value  [>] — square arrow buttons, one button tall.
    Text Stepper(RectTransform parent, float y, Action<int> step, out Button left, float x = ColX, float width = ColW)
    {
        float bh = IronOvalUI.ButtonHeight();
        left = MakeButton("<", parent, x, y, bh, () => step(-1));
        MakeButton(">", parent, x + width - bh, y, bh, () => step(+1));
        var value = Label("Value", parent, _font, DataPx, PixelGUI.Text, TextAnchor.MiddleCenter);
        At(value.rectTransform, x + bh + 4f, y, width - 2f * bh - 8f, bh);
        return value;
    }

    Button MakeButton(string text, RectTransform parent, float x, float y, float w, UnityEngine.Events.UnityAction onClick)
    {
        var img = Box("Button", parent, PixelGUI.PlateLight);
        At(img.rectTransform, x, y, w, IronOvalUI.ButtonHeight());   // every screen's button height (UIButton)
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        var t = Label("Text", img.rectTransform, _font, DataPx, PixelGUI.Gold, TextAnchor.MiddleCenter);
        IronOvalUI.ButtonCaption(t, PixelGUI.Gold, RefH);
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
