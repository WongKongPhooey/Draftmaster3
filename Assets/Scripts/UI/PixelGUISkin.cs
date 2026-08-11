using UnityEngine;

// Restyles Unity's default IMGUI skin from PixelUITheme, so every OnGUI panel in the game picks up the
// pixel font and palette without being edited individually.
//
// Most of this project's in-race UI is IMGUI: the race HUD, pause menu, timing screen, quest HUD, pit
// service, spawn intro, mini-map, and the F-key debug panels. They were all drawing with Unity's builtin
// skin, which is why the game looked like a prototype no matter how good the dialogue box got. They mostly
// derive their styles from `GUI.skin.label` / `.box` / `.button`, so restyling the skin itself reaches all
// of them at once.
//
// GUI.skin can only be touched inside OnGUI, and the default skin object persists for the session, so this
// applies itself from an OnGUI that runs before everything else (execution order also governs OnGUI order).
// Panels that build a GUIStyle from scratch -- PixelGUI's own -- are unaffected, as they are already themed.
[DefaultExecutionOrder(-10000)]
public class PixelGUISkin : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<PixelGUISkin>() != null) return;
        var go = new GameObject("PixelGUISkin");
        go.AddComponent<PixelGUISkin>();
        DontDestroyOnLoad(go);
    }

    int _appliedScale = -1;
    PixelUITheme _appliedTheme;

    void OnGUI()
    {
        // Only reapply when something actually changed — the skin persists between frames.
        var theme = PixelGUI.Theme;
        int scale = PixelGUI.Scale;
        if (theme == null) return;
        if (_appliedTheme == theme && _appliedScale == scale) return;
        _appliedTheme = theme;
        _appliedScale = scale;

        Apply(GUI.skin, theme, scale);
        // IMGUI cannot be captured through a camera, so this line is the only way to confirm from outside
        // the editor that the skin actually took.
        Debug.Log($"[PixelGUISkin] applied at {scale}x — font " +
                  $"{(theme.imguiFont != null ? theme.imguiFont.name : "MISSING")}, body {16 * scale}px.");
    }

    static void Apply(GUISkin skin, PixelUITheme theme, int scale)
    {
        if (skin == null) return;

        if (theme.imguiFont != null) skin.font = theme.imguiFont;

        int body = 16 * scale;
        int small = 12 * scale;

        StyleText(skin.label, theme.text, body);
        StyleText(skin.textField, theme.text, body);
        StyleText(skin.textArea, theme.text, body);
        StyleText(skin.toggle, theme.text, body);
        StyleText(skin.window, theme.gold, body);

        // Buttons get the kit's plate art, with the gold variant as the hover/active state. Sprite swap
        // rather than a colour tint: tinting flat pixel art muddies it.
        StyleButton(skin.button, theme, body);
        StyleButton(skin.box, theme, body, boxStyle: true);

        // Everything derived from these picks up the scale too.
        skin.horizontalSlider.fixedHeight = 8 * scale;
        skin.horizontalSliderThumb.fixedWidth = 8 * scale;
        skin.horizontalSliderThumb.fixedHeight = 12 * scale;
    }

    static void StyleText(GUIStyle style, Color colour, int size)
    {
        if (style == null) return;
        style.fontSize = size;
        style.normal.textColor = colour;
        style.hover.textColor = colour;
        style.active.textColor = colour;
        style.focused.textColor = colour;
        style.onNormal.textColor = colour;
        style.onHover.textColor = colour;
        style.onActive.textColor = colour;
    }

    static void StyleButton(GUIStyle style, PixelUITheme theme, int size, bool boxStyle = false)
    {
        if (style == null) return;
        style.fontSize = size;

        var face = boxStyle ? theme.window : theme.button;
        var hover = boxStyle ? theme.window : theme.buttonHover;
        var pressed = boxStyle ? theme.window : theme.buttonPressed;
        int border = boxStyle ? 6 : 5;

        if (face != null && face.texture != null)
        {
            style.normal.background = face.texture;
            style.focused.background = face.texture;
            style.border = new RectOffset(border, border, border, border);
            // Sliced backgrounds need room for the border or the corners eat the label.
            style.padding = new RectOffset(border + 4, border + 4, border + 2, border + 2);
        }
        if (hover != null && hover.texture != null)
        {
            style.hover.background = hover.texture;
            style.onNormal.background = hover.texture;
        }
        if (pressed != null && pressed.texture != null)
            style.active.background = pressed.texture;

        StyleText(style, boxStyle ? theme.text : theme.text, size);
        // The gold hover plate is light, so the label needs to darken against it or it disappears.
        if (!boxStyle) style.hover.textColor = theme.ink;
    }
}
