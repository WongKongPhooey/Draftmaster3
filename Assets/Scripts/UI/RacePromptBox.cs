using UnityEngine;

// A race-time instruction drawn in the career quest box's style (WeekendObjectiveHUD): the kit's dark blue panel
// and border, centred at the top of the screen, a coloured heading over the instruction in cream, and an
// optional footer line under it.
//
// PaceLapAssist draws its caution and pace-lap prompts in it ("Caution" / "Hold station behind car #41" /
// "Pit road open"); the footer is FullCourseCaution.StatusLine, which used to be a full-width banner of its own.
// Out of position keeps its flashing red OutOfPositionBox — an alarm, not an instruction.
public static class RacePromptBox
{
    // Kit pixels from the top of the screen to the panel: the out-of-position box's top, so the two prompts
    // swap in place rather than jumping as the player drifts in and out of line.
    const float Top = 12f;

    static GUIStyle _title, _detail, _footer;
    static int _builtAt;

    // Draws the box and returns its rect. Any of the three lines may be null or empty and is then left out.
    public static Rect Draw(string title, Color titleColour, string detail, string footer)
    {
        EnsureStyles();
        _title.normal.textColor = titleColour;

        bool hasTitle = !string.IsNullOrEmpty(title);
        bool hasDetail = !string.IsNullOrEmpty(detail);
        bool hasFooter = !string.IsNullOrEmpty(footer);

        // Rows measured off their own faces, as the quest box does: the heading, data and footer faces differ in
        // size, and striding them by one line height drew each through the next.
        float titleH = hasTitle ? RowH(_title) : 0f;
        float detailH = hasDetail ? RowH(_detail) : 0f;
        float footerH = hasFooter ? RowH(_footer) : 0f;
        float textH = titleH + detailH + footerH;
        float textW = Mathf.Max(Width(_title, hasTitle ? title : null),
                                Mathf.Max(Width(_detail, hasDetail ? detail : null), Width(_footer, hasFooter ? footer : null)));

        float inset = PixelGUI.Px(8f);   // what PanelContent(box, 4f) takes off each side
        // Sized to its longest line, so a long instruction widens the box rather than spilling out of it.
        float w = Mathf.Clamp(textW + inset * 2f + PixelGUI.Px(8f), PixelGUI.Px(180f), Screen.width - PixelGUI.Px(16f));
        w = PixelGUI.Px(Mathf.Ceil(w / PixelGUI.Px(1f)));   // whole kit pixels, so the centring stays on the grid
        float h = textH + inset * 2f;

        var box = new Rect(Mathf.Round((Screen.width - w) * 0.5f), PixelGUI.Px(Top), w, h);
        PixelGUI.Panel(box, focused: false);
        var c = PixelGUI.PanelContent(box, 4f);

        float y = Mathf.Round(c.y + (c.height - textH) * 0.5f);
        if (hasTitle) { GUI.Label(new Rect(c.x, y, c.width, titleH), title, _title); y += titleH; }
        if (hasDetail) { GUI.Label(new Rect(c.x, y, c.width, detailH), detail, _detail); y += detailH; }
        if (hasFooter) GUI.Label(new Rect(c.x, y, c.width, footerH), footer, _footer);
        return box;
    }

    static float RowH(GUIStyle s) => s.fontSize + PixelGUI.Px(3f);

    static float Width(GUIStyle s, string text) =>
        string.IsNullOrEmpty(text) ? 0f : s.CalcSize(new GUIContent(text)).x;

    static void EnsureStyles()
    {
        if (_title != null && _builtAt == PixelGUI.Scale) return;
        _builtAt = PixelGUI.Scale;
        // The quest box's three faces, centred: there is no button beside the text here to left-align against.
        _title = new GUIStyle(PixelGUI.Heading)
        {
            alignment = TextAnchor.UpperCenter, wordWrap = false, clipping = TextClipping.Overflow, richText = false,
        };
        _detail = new GUIStyle(PixelGUI.Data)
        {
            alignment = TextAnchor.UpperCenter, wordWrap = false, clipping = TextClipping.Overflow, richText = false,
        };
        _footer = new GUIStyle(PixelGUI.Footer)
        {
            alignment = TextAnchor.UpperCenter, wordWrap = false, clipping = TextClipping.Overflow, richText = false,
        };
    }
}
