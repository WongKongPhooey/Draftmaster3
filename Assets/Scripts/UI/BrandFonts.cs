using UnityEngine;

// Central runtime font lookup for the code-built legacy uGUI panels (crew-chief HUD, drive/broadcast toggle,
// dialogue, garage/shop info panels, team-switch, session buttons). These build their Text at runtime with no
// prefab wiring, so they can't reference a Font asset in the inspector — this loads the brand body font
// (Now-Regular, from Resources/Fonts) once and caches it, replacing the builtin runtime font. Falls back to the
// builtin font only if the Resources copy is ever missing, so text is never left invisible.
public static class BrandFonts
{
    static Font _body;

    // Brand body font (Now-Regular). Use for runtime-built uGUI Text in place of the builtin font.
    public static Font Body
    {
        get
        {
            if (_body == null)
                _body = Resources.Load<Font>("Fonts/Now-Regular");
            if (_body == null)
                _body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _body;
        }
    }
}
