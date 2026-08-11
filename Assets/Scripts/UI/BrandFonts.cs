using UnityEngine;

// Central runtime font lookup for the code-built legacy uGUI panels (crew-chief HUD, drive/broadcast toggle,
// dialogue, garage/shop info panels, team-switch, session buttons). These build their Text at runtime with no
// prefab wiring, so they can't reference a Font asset in the inspector.
//
// This is the one place those panels get their typeface, which makes it the cheapest way to move the whole
// game onto the pixel UI kit: it serves PixelUITheme's font when the theme is present, so every runtime
// panel changes with the theme rather than needing its own edit.
//
// Falls back to the old brand face (Now-Regular) and then to Unity's builtin font, so text is never left
// invisible if the theme or its font is missing.
public static class BrandFonts
{
    static Font _body;
    static PixelUITheme _resolvedFor;

    // Body font for runtime-built uGUI Text.
    public static Font Body
    {
        get
        {
            var theme = PixelUITheme.Instance;
            // Re-resolve if the theme appeared (or changed) since the last lookup.
            if (_body != null && _resolvedFor == theme) return _body;
            _resolvedFor = theme;

            _body = theme != null ? theme.imguiFont : null;
            if (_body == null) _body = Resources.Load<Font>("Fonts/Now-Regular");
            if (_body == null) _body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _body;
        }
    }

    // Font size for a runtime uGUI panel, stepped with the screen the same way the IMGUI panels are so the
    // two families stay the same size as each other.
    public static int Size(int baseline) => Mathf.Max(1, baseline * PixelGUI.Scale);

    // Palette shortcuts, so a runtime panel does not have to reach through the theme for every colour.
    public static Color Text => PixelUITheme.Instance != null ? PixelUITheme.Instance.text : Color.white;
    public static Color Dim => PixelUITheme.Instance != null ? PixelUITheme.Instance.textDim : Color.grey;
    public static Color Accent => PixelUITheme.Instance != null ? PixelUITheme.Instance.gold : Color.yellow;
}
