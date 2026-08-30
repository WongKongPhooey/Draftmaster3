using UnityEngine;

// The phone's own type, one step down from the kit's.
//
// PixelGUI sizes every face at its authored cell times the display Scale — 16px VT323 at 32px on a 1080p
// screen. That is right for a panel that owns the screen, and wrong for the phone, which is a screen
// drawn inside the screen: a 32px glyph in a 22px row is why everything on the phone read as squashed.
//
// So the phone builds its own styles at *half* the display scale, rounded to a whole multiple of the
// face's cell so the glyphs still land on the pixel grid. Everything else — plates, rules, meters — keeps
// using PixelGUI.Px, so the device stays the same size and simply fits about twice as much on it.
//
// The ink variants are for content drawn on a light page (the calendar), where the kit's light-on-dark
// text would be invisible.
public static class PhoneStyles
{
    static Object _builtFor;
    static int _builtAtScale = -1;

    static GUIStyle _data, _dataDim, _body, _heading, _footer;
    static GUIStyle _inkData, _inkDim, _inkHeading;

    // How many screen pixels one authored font pixel covers on the phone. Half the kit's, never below 1.
    public static int TypeScale => Mathf.Max(1, Mathf.RoundToInt(PixelGUI.Scale * 0.5f));

    public static GUIStyle Data { get { Ensure(); return _data; } }
    public static GUIStyle DataDim { get { Ensure(); return _dataDim; } }
    public static GUIStyle Body { get { Ensure(); return _body; } }
    public static GUIStyle Heading { get { Ensure(); return _heading; } }
    public static GUIStyle Footer { get { Ensure(); return _footer; } }

    // Dark-on-light: the calendar page and anything else drawn on paper rather than glass.
    public static GUIStyle InkData { get { Ensure(); return _inkData; } }
    public static GUIStyle InkDim { get { Ensure(); return _inkDim; } }
    public static GUIStyle InkHeading { get { Ensure(); return _inkHeading; } }

    // One row of the data face plus its leading — what a list row actually needs to not clip.
    public static float LineH { get { Ensure(); return _data.fontSize + PixelGUI.Px(3f); } }

    static void Ensure()
    {
        var t = PixelGUI.Theme;
        int scale = PixelGUI.Scale;
        if (_data != null && (Object)_builtFor == (Object)t && _builtAtScale == scale) return;
        _builtFor = t;
        _builtAtScale = scale;

        Font display = t != null && t.imguiDisplayFont != null ? t.imguiDisplayFont : (t != null ? t.imguiFont : null);
        Font data = t != null ? t.imguiFont : null;
        Font prose = t != null && t.imguiBodyFont != null ? t.imguiBodyFont : data;

        int unit = TypeScale;
        int dataPt = PixelGUI.FontCell(data, 16) * unit;
        int headPt = PixelGUI.FontCell(display, 8) * unit;
        int prosePt = PixelGUI.FontCell(prose, 16) * unit;

        _data = Style(data, dataPt, PixelGUI.Text, TextAnchor.MiddleLeft);
        _dataDim = Style(data, dataPt, PixelGUI.TextDim, TextAnchor.MiddleLeft);
        _body = Style(prose, prosePt, PixelGUI.Text, TextAnchor.UpperLeft, wrap: true);
        _heading = Style(display, headPt, PixelGUI.Gold, TextAnchor.MiddleLeft);
        _footer = Style(display, headPt, PixelGUI.TextDisabled, TextAnchor.MiddleLeft);

        _inkData = Style(data, dataPt, PixelGUI.Ink, TextAnchor.MiddleLeft);
        _inkDim = Style(data, dataPt, new Color(0.32f, 0.32f, 0.36f), TextAnchor.MiddleLeft);
        _inkHeading = Style(display, headPt, PixelGUI.Ink, TextAnchor.MiddleLeft);
    }

    static GUIStyle Style(Font font, int size, Color colour, TextAnchor anchor, bool wrap = false)
    {
        var s = new GUIStyle
        {
            font = font,
            fontSize = size,
            alignment = anchor,
            wordWrap = wrap,
            richText = true,
            clipping = TextClipping.Clip,
        };
        s.normal.textColor = colour;
        s.hover.textColor = colour;
        s.active.textColor = colour;
        s.focused.textColor = colour;
        s.onNormal.textColor = colour;
        s.onHover.textColor = colour;
        s.onActive.textColor = colour;
        return s;
    }

    // Draws `text` in `style` with a one-off colour, without leaving the shared style recoloured.
    public static void Label(Rect r, string text, GUIStyle style, Color? colour = null,
                             TextAnchor? align = null)
    {
        var prevColour = style.normal.textColor;
        var prevAlign = style.alignment;
        if (colour.HasValue) style.normal.textColor = colour.Value;
        if (align.HasValue) style.alignment = align.Value;

        GUI.Label(r, text, style);

        style.normal.textColor = prevColour;
        style.alignment = prevAlign;
    }
}
