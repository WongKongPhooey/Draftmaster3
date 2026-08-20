using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The Iron Oval widget set: windows, buttons, the three type roles, segmented stat cells, the blinking
// cursor and the spotter/NPC dialogue window. Everything reads PixelUITheme, so this is the layer that
// turns the imported kit (Docs/IronOvalKit.md) into things a screen can actually be built out of.
//
// Sizing rules baked in here, from the handoff sheet:
//  * Type only ever asks for a size on its face's crisp ladder — Silkscreen 8/16/24, Pixelify 16/32/48,
//    VT323 16/32. A bitmap atlas resamples badly downward, so off-ladder sizes are not offered.
//  * Tiled interiors are Image.Type.Tiled, never Stretched — a stretched 8x8 dither is a flat wash.
//  * Buttons have no pressed sprite: the content shifts 2px down instead (IronOvalPressOffset).
//  * Nothing rotates, scales or fades. Blinks are steps, not curves.
public static class IronOvalUI
{
    public static PixelUITheme Theme => PixelUITheme.Instance;

    // Type roles. Header is the uppercase Silkscreen voice, Body is Pixelify Sans prose, Data is the
    // fixed-advance VT323 used for anything that has to line up in columns.
    public enum Role { Header, HeaderSmall, Body, BodyLarge, Display, Data }

    public static TextMeshProUGUI Label(Transform parent, string name, string content,
                                        Role role = Role.Body, Color? colour = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = content;
        Apply(label, role, colour);
        return label;
    }

    public static void Apply(TMP_Text label, Role role = Role.Body, Color? colour = null)
    {
        var t = Theme;
        if (t == null) return;

        TMP_FontAsset font;
        int size;
        Color defaultColour;
        switch (role)
        {
            // Sizes are locked to each face's measured ladder (Docs/IronOvalKit.md). Pixelify Sans in
            // particular has a 20px design grid — 16 lands every glyph between pixels and the text
            // renders as overlapping mush, which is not obvious until you look at it at 1:1.
            case Role.Header:      font = t.display; size = 16; defaultColour = t.gold; break;
            case Role.HeaderSmall: font = t.display; size = 8;  defaultColour = t.gold; break;
            case Role.BodyLarge:   font = t.body;    size = 40; defaultColour = t.text; break;
            // The title-screen voice. 60 is the top of Pixelify's 20-grid ladder — the sheet's 62px
            // wordmark rounded onto it, so the logo stays hard-edged.
            case Role.Display:     font = t.body;    size = 60; defaultColour = t.text; break;
            case Role.Data:        font = t.data;    size = 16; defaultColour = t.textDim; break;
            default:               font = t.body;    size = 20; defaultColour = t.text; break;
        }
        if (font == null) font = t.body;
        if (font != null)
        {
            label.font = font;
            if (font.material != null) label.fontSharedMaterial = font.material;
        }

        label.fontSize = size;
        label.color = colour ?? defaultColour;
        // Silkscreen is drawn to be tracked out; the sheet asks for +1 to +3px on labels. TMP's
        // characterSpacing is in font units (1/100 em), so 12.5 at 16px is 2px.
        label.characterSpacing = role == Role.Header || role == Role.HeaderSmall ? 12.5f : 0f;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
    }

    // ---- surfaces ----------------------------------------------------------------------------------

    // A window: 9-sliced frame over a tiled interior. `focused` swaps the cream frame for the gold one,
    // which is how the sheet wants focus shown — cheaper and calmer than tinting the text inside.
    // The root carries no Image of its own: uGUI draws a parent BEFORE its children, so a frame on the
    // root would be painted over by the interior tile. Fill and frame are both children instead, in that
    // order, and callers still parent their content to the returned root (drawn last, on top of both).
    public static RectTransform Window(Transform parent, string name, Vector2 size, bool focused = false)
    {
        var t = Theme;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;

        var fill = Tile(rt, "Fill", t == null ? null : t.panelFill);
        if (fill.sprite == null && t != null) fill.color = t.plate;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(rt, false);
        var frt = (RectTransform)frameGo.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        var frame = frameGo.GetComponent<Image>();
        frame.sprite = t == null ? null : (focused ? t.frameGold : t.frameCream);
        frame.type = Image.Type.Sliced;
        frame.fillCenter = false;              // the tile below is the centre; don't paint over it
        frame.pixelsPerUnitMultiplier = 1f;
        frame.raycastTarget = false;
        return rt;
    }

    // Full-bleed tiled sprite (panel fill, hatch slot, kerb, asphalt). Stretch-to-fit on the rect,
    // tiled rather than scaled so the dither keeps its authored pixel size.
    public static Image Tile(Transform parent, string name, Sprite sprite, Color? colour = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = 1f;
        img.raycastTarget = false;
        if (colour.HasValue) img.color = colour.Value;
        if (sprite == null && Theme != null) img.color = Theme.plate;
        return img;
    }

    // Hatched placeholder for portraits, sponsor logos and car renders that aren't drawn yet. The sheet
    // is explicit that this ships in the build: an empty slot reads as a bug, a hatched one as pending.
    public static RectTransform ArtSlot(Transform parent, string name, Vector2 size,
                                        string caption = null, bool keyline = false)
    {
        var t = Theme;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;

        if (keyline)
        {
            // 2px cream border, as the sheet draws the dialogue portrait: this Image is the border and
            // the hatch goes inside it. A child would draw over a border on the same Image.
            img.color = t == null ? Color.white : t.text;
            var inner = Tile(rt, "Hatch", t == null ? null : t.hatchSlot);
            inner.rectTransform.offsetMin = new Vector2(2f, 2f);
            inner.rectTransform.offsetMax = new Vector2(-2f, -2f);
        }
        else
        {
            img.sprite = t == null ? null : t.hatchSlot;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 1f;
        }

        if (!string.IsNullOrEmpty(caption))
        {
            var cap = Label(rt, "Caption", caption, Role.Data, t == null ? Color.grey : t.textDisabled);
            var crt = cap.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.offsetMin = new Vector2(0f, 2f);
            crt.offsetMax = new Vector2(0f, 18f);
            cap.alignment = TextAlignmentOptions.Bottom;
        }
        return rt;
    }

    // ---- buttons -----------------------------------------------------------------------------------

    // The confirm button. One sprite for every state: the sheet bakes the highlight and shade in and
    // asks for the pressed look to come from offsetting the content 2px, so there is no second drawing
    // to keep in sync. Disabled tints the whole plate to the inner-shade colour.
    public static Button Button(Transform parent, string name, string content, Vector2 size)
    {
        var t = Theme;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.sprite = t == null ? null : t.buttonRed;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        if (img.sprite == null && t != null) img.color = t.danger;

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = Color.white;
        colours.pressedColor = Color.white;
        colours.selectedColor = Color.white;
        colours.disabledColor = t == null ? Color.grey : t.plateLight;
        colours.fadeDuration = 0f;             // no fades on pixel art
        button.colors = colours;

        var content_ = new GameObject("Content", typeof(RectTransform));
        content_.transform.SetParent(go.transform, false);
        var crt = (RectTransform)content_.transform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = crt.offsetMax = Vector2.zero;

        var label = Label(crt, "Label", content, Role.Header, t == null ? Color.white : t.text);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        // A button caption that wraps has outgrown its plate; let it overflow so the mistake is visible
        // at author time rather than silently splitting a word across two lines.
        label.textWrappingMode = TextWrappingModes.NoWrap;

        go.AddComponent<IronOvalPressOffset>().content = crt;
        return button;
    }

    // The flat tab/nav button from the screen designs: solid plate, hard 1px outline, 3px hard drop
    // shadow, no 9-slice. Selected fills with alarm red, unselected with the panel colour.
    public static Button TabButton(Transform parent, string name, string content, Vector2 size, bool selected = false)
    {
        var t = Theme;
        var shadow = new GameObject(name, typeof(RectTransform), typeof(Image));
        shadow.transform.SetParent(parent, false);
        ((RectTransform)shadow.transform).sizeDelta = size;
        var srt = (RectTransform)shadow.transform;
        var simg = shadow.GetComponent<Image>();
        simg.color = t == null ? Color.black : t.ink;
        simg.raycastTarget = false;

        var face = new GameObject("Face", typeof(RectTransform), typeof(Image), typeof(Button));
        face.transform.SetParent(srt, false);
        var frt = (RectTransform)face.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        // The shadow is the parent offset down-right by 3px, so the face sits back up-left over it.
        frt.offsetMin = new Vector2(-3f, 3f);
        frt.offsetMax = new Vector2(-3f, 3f);

        var fimg = face.GetComponent<Image>();
        fimg.color = selected ? (t == null ? Color.red : t.danger) : (t == null ? Color.grey : t.plateDeep);

        var label = Label(frt, "Label", content, Role.HeaderSmall,
                          selected ? (t == null ? Color.white : t.text) : (t == null ? Color.grey : t.textDim));
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;

        return face.GetComponent<Button>();
    }

    // ---- data ---------------------------------------------------------------------------------------

    // Segmented bar built from the stat-cell sheet: `max` cells, the first `value` of them filled.
    // Returns the row so a caller can re-run SetCells on it as the value changes.
    public static RectTransform StatCells(Transform parent, string name, int value, int max, Color? fillTint = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 2f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(max * 10f - 2f, 10f);
        SetCells(rt, value, max, fillTint);
        return rt;
    }

    public static void SetCells(RectTransform row, int value, int max, Color? fillTint = null)
    {
        var t = Theme;
        // Rebuild rather than tint-in-place: the two cells are different drawings, not one recoloured.
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            var child = row.GetChild(i).gameObject;
            if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
        }
        for (int i = 0; i < max; i++)
        {
            var cell = new GameObject($"Cell{i}", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(row, false);
            ((RectTransform)cell.transform).sizeDelta = new Vector2(8f, 10f);
            var img = cell.GetComponent<Image>();
            img.raycastTarget = false;
            bool on = i < value;

            if (!on)
            {
                img.sprite = t == null ? null : t.statCellEmpty;
                if (img.sprite == null && t != null) img.color = t.plateLight;
            }
            else if (fillTint.HasValue)
            {
                // The filled cell in the sheet is drawn in the accent gold with a baked shade, so tinting
                // it is a multiply and cannot reach another hue — gold x telemetry blue comes out olive.
                // The design's own HUD draws its tyre and draft cells as flat colour, so do that: a solid
                // cell in the asked-for colour, no sprite.
                img.color = fillTint.Value;
            }
            else
            {
                img.sprite = t == null ? null : t.statCellFilled;
                if (img.sprite == null && t != null) img.color = t.gold;
            }
        }
    }

    // ---- furniture ----------------------------------------------------------------------------------

    // The gold selection arrow. Blinks 0.45s on / 0.45s off and is moved in whole rows by the caller.
    public static Image Cursor(Transform parent, string name = "Cursor")
    {
        var t = Theme;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(6f, 8f);
        var img = go.GetComponent<Image>();
        img.sprite = t == null ? null : t.cursorArrow;
        img.raycastTarget = false;
        if (img.sprite == null && t != null) img.color = t.gold;
        go.AddComponent<IronOvalBlink>();
        return img;
    }

    // The dialogue window from section 04 of the sheet: portrait slot, name plate + status readout,
    // body copy, and the blinking advance caret. This is the shape the NPC/spotter/crew-chief lines use.
    public static IronOvalDialogue Dialogue(Transform parent, Vector2 size)
    {
        var t = Theme;
        var root = Window(parent, "IronOvalDialogue", size);
        var d = root.gameObject.AddComponent<IronOvalDialogue>();

        int pad = t == null ? 12 : t.panelPadding;
        int gap = t == null ? 8 : t.rowGap;

        var portrait = ArtSlot(root, "Portrait", new Vector2(48f, size.y - pad * 2f), null, keyline: true);
        portrait.anchorMin = new Vector2(0f, 0f);
        portrait.anchorMax = new Vector2(0f, 1f);
        portrait.pivot = new Vector2(0f, 0.5f);
        portrait.offsetMin = new Vector2(pad, pad);
        portrait.offsetMax = new Vector2(pad + 48f, -pad);

        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(root, false);
        var brt = (RectTransform)body.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(pad + 48f + gap, pad);
        brt.offsetMax = new Vector2(-pad, -pad);

        var nameRow = new GameObject("NameRow", typeof(RectTransform));
        nameRow.transform.SetParent(brt, false);
        var nrt = (RectTransform)nameRow.transform;
        nrt.anchorMin = new Vector2(0f, 1f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0f, 1f);
        nrt.offsetMin = new Vector2(0f, -10f);
        nrt.offsetMax = Vector2.zero;

        d.speaker = Label(nrt, "Speaker", "RAY — CREW CHIEF", Role.HeaderSmall);
        var srt = d.speaker.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(0.7f, 1f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;
        d.speaker.alignment = TextAlignmentOptions.Left;

        d.status = Label(nrt, "Status", "TRUST 3/5", Role.Data, t == null ? Color.grey : t.textDisabled);
        var strt = d.status.rectTransform;
        strt.anchorMin = new Vector2(0.7f, 0f);
        strt.anchorMax = new Vector2(1f, 1f);
        strt.offsetMin = strt.offsetMax = Vector2.zero;
        d.status.alignment = TextAlignmentOptions.Right;

        d.line = Label(brt, "Line", "", Role.Body);
        var lrt = d.line.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(0f, 0f);
        lrt.offsetMax = new Vector2(0f, -14f);
        d.line.alignment = TextAlignmentOptions.TopLeft;
        d.line.textWrappingMode = TextWrappingModes.Normal;
        // Page rather than overflow. A line longer than the window is the normal case, not an error, and
        // the blinking caret below already means "there is more" — so the copy breaks into pages the
        // player advances through instead of running out through the bottom of the frame.
        d.line.overflowMode = TextOverflowModes.Page;
        d.line.pageToDisplay = 1;

        d.caret = Label(brt, "Caret", "▼", Role.Header);
        var crt2 = d.caret.rectTransform;
        crt2.anchorMin = new Vector2(1f, 0f);
        crt2.anchorMax = new Vector2(1f, 0f);
        crt2.pivot = new Vector2(1f, 0f);
        crt2.sizeDelta = new Vector2(12f, 12f);
        crt2.anchoredPosition = Vector2.zero;
        d.caret.alignment = TextAlignmentOptions.Center;
        d.caret.gameObject.AddComponent<IronOvalBlink>().interval = 0.5f;

        return d;
    }
}

// Shifts a button's content 2px down while held. The sheet asks for the pressed state to come from the
// offset rather than a second sprite, so there is only ever one drawing of the button to maintain.
public class IronOvalPressOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform content;
    public float depth = 2f;
    Vector2 _rest;
    bool _held;

    void Awake() { if (content != null) _rest = content.anchoredPosition; }
    public void OnPointerDown(PointerEventData _) => Set(true);
    public void OnPointerUp(PointerEventData _) => Set(false);
    void OnDisable() => Set(false);

    void Set(bool held)
    {
        if (content == null || _held == held) return;
        _held = held;
        content.anchoredPosition = held ? _rest + new Vector2(0f, -depth) : _rest;
    }
}

// Hard on/off blink for the cursor and the advance caret. Steps, never a fade — a pixel cursor that
// cross-dissolves reads as a bug.
public class IronOvalBlink : MonoBehaviour
{
    [Tooltip("Seconds on, then the same off. The sheet asks for 0.45s for the selection cursor.")]
    public float interval = 0.45f;

    Graphic _graphic;
    float _t;

    void Awake() { _graphic = GetComponent<Graphic>(); }
    void OnDisable() { if (_graphic != null) _graphic.enabled = true; }

    void Update()
    {
        if (_graphic == null || interval <= 0f) return;
        _t += Time.unscaledDeltaTime;
        if (_t < interval) return;
        _t -= interval;
        _graphic.enabled = !_graphic.enabled;
    }
}

// Handle for a built dialogue window, so callers set text rather than walk the hierarchy. Copy longer
// than the window is paged; Advance() steps a page and reports whether the line is finished.
public class IronOvalDialogue : MonoBehaviour
{
    public TMP_Text speaker, status, line, caret;

    public int Page => line == null ? 1 : line.pageToDisplay;
    public int PageCount => line == null ? 1 : Mathf.Max(1, line.textInfo.pageCount);
    public bool AtEnd => Page >= PageCount;

    public void Set(string speakerName, string body, string statusText = null)
    {
        if (speaker != null) speaker.text = speakerName == null ? "" : speakerName.ToUpperInvariant();
        if (status != null)
        {
            status.text = statusText ?? "";
            status.gameObject.SetActive(!string.IsNullOrEmpty(statusText));
        }
        if (line != null)
        {
            line.text = body;
            line.pageToDisplay = 1;
            // pageCount is only valid once the text has been laid out, and the caret's visibility
            // depends on it.
            line.ForceMeshUpdate();
        }
        RefreshCaret();
    }

    // Returns true while there is more of this line to read.
    public bool Advance()
    {
        if (line == null) return false;
        if (AtEnd) return false;
        line.pageToDisplay++;
        RefreshCaret();
        return true;
    }

    void RefreshCaret()
    {
        if (caret != null) caret.gameObject.SetActive(!AtEnd);
    }
}
