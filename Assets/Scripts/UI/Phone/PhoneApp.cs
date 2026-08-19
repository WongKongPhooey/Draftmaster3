using UnityEngine;

// One app on the player's phone (PhoneUI). An app owns a tile on the home screen and draws its own
// content into the phone's screen rect; the device handles the frame, the slide, scrolling and input.
//
// Adding an app is one subclass plus a line in PhoneUI.BuildApps — the home grid has six slots and
// draws the spare ones as empty bays, so a fifth and sixth app need no layout work.
//
// Drawing vocabulary is the Iron Oval kit (PixelGUI). Every helper takes a top-left corner and a width
// and returns the height it used, so an app's Draw() is a running `y += Something(...)` and the device
// gets a content height it can scroll.
public abstract class PhoneApp
{
    // Stable id, used for the "last app open" preference and by other systems asking for a badge.
    public abstract string Id { get; }
    // Name on the tile and in the app's title bar.
    public abstract string TileName { get; }
    // One line under the tile name. Keep it to about 16 characters.
    public virtual string TileSubtitle => "";
    // The app's one colour: tile frame, title bar, meters. Everything else stays kit-neutral.
    public virtual Color Accent => PixelGUI.Info;
    // Unread count. > 0 draws a badge on the tile; the device shows a dot on the phone when any app has one.
    public virtual int Badge => 0;

    public virtual void OnOpen() { }

    // Draw into a column `width` wide starting at (x, y). Return the total height used.
    public abstract float Draw(float x, float y, float width);

    // ------------------------------------------------------------------ shared drawing

    // Small gold section rule with a Silkscreen label above it.
    protected static float Section(float x, float y, float w, string label)
    {
        float h = PixelGUI.Px(10f);
        GUI.Label(new Rect(x, y, w, h), label, PixelGUI.HeadingSmall);
        PixelGUI.Rule(x, y + h, w, new Color(PixelGUI.Gold.r, PixelGUI.Gold.g, PixelGUI.Gold.b, 0.5f));
        return h + PixelGUI.Px(4f);
    }

    // A left label with an optional right-aligned readout on one VT323 row.
    protected static float Row(float x, float y, float w, string left, string right = "", Color? colour = null, bool dim = false)
    {
        float h = PixelGUI.Px(11f);
        var style = dim ? PixelGUI.DataDim : PixelGUI.Data;
        var prev = style.normal.textColor;
        if (colour.HasValue) style.normal.textColor = colour.Value;

        GUI.Label(new Rect(x, y, w, h), left, style);
        if (!string.IsNullOrEmpty(right))
        {
            var prevAlign = style.alignment;
            style.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x, y, w, h), right, style);
            style.alignment = prevAlign;
        }

        style.normal.textColor = prev;
        return h;
    }

    // Wrapped prose. Measured, so the caller's running y stays right however long the text is.
    protected static float Body(float x, float y, float w, string text, Color? colour = null)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        var style = PixelGUI.Body;
        var prev = style.normal.textColor;
        if (colour.HasValue) style.normal.textColor = colour.Value;

        float h = style.CalcHeight(new GUIContent(text), w);
        GUI.Label(new Rect(x, y, w, h), text, style);

        style.normal.textColor = prev;
        return h + PixelGUI.Px(2f);
    }

    // A labelled 0..1 meter with its value written on the right — fan appeal, deal progress, a stat bar.
    protected static float Meter(float x, float y, float w, string label, float fill01, string readout, Color fill)
    {
        float h = Row(x, y, w, label, readout);
        float bar = PixelGUI.Px(5f);
        PixelGUI.Bar(new Rect(x, y + h, w, bar), Mathf.Clamp01(fill01), fill);
        return h + bar + PixelGUI.Px(4f);
    }

    // A plate to sit content on: a post, a note, a driver card. Draw it first, then write inside it.
    public static void Plate(Rect r, Color? edge = null)
    {
        PixelGUI.Fill(r, PixelGUI.Plate);
        var c = edge ?? PixelGUI.PlateLight;
        PixelGUI.Fill(new Rect(r.x, r.y, r.width, PixelGUI.Px(1f)), c);
        PixelGUI.Fill(new Rect(r.x, r.yMax - PixelGUI.Px(1f), r.width, PixelGUI.Px(1f)), c);
        PixelGUI.Fill(new Rect(r.x, r.y, PixelGUI.Px(1f), r.height), c);
        PixelGUI.Fill(new Rect(r.xMax - PixelGUI.Px(1f), r.y, PixelGUI.Px(1f), r.height), c);
    }

    // "Nothing here yet" — every app needs one and they should all read the same.
    protected static float Empty(float x, float y, float w, string text)
    {
        return Body(x, y, w, text, PixelGUI.TextDisabled);
    }

    protected static string Trim(string s, int len) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= len ? s : s.Substring(0, len));
}
