using UnityEngine;

// Restyles Unity's default IMGUI skin from PixelUITheme, so every OnGUI panel in the game picks up the
// Iron Oval font and palette without being edited individually.
//
// Most of this project's in-race UI is IMGUI: the race HUD, pause menu, timing screen, quest HUD, pit
// service, spawn intro, mini-map, and the F-key debug panels. They were all drawing with Unity's builtin
// skin, which is why the game looked like a prototype no matter how good the dialogue box got. They mostly
// derive their styles from `GUI.skin.label` / `.box` / `.button`, so restyling the skin itself reaches all
// of them at once — including the ones not yet moved onto PixelGUI's widgets by hand.
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
        if (FindAnyObjectByType<PixelGUISkin>() != null) return;
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
        Debug.Log($"[PixelGUISkin] applied at {scale}x — data font " +
                  $"{(theme.imguiFont != null ? theme.imguiFont.name : "MISSING")}, display " +
                  $"{(theme.imguiDisplayFont != null ? theme.imguiDisplayFont.name : "MISSING")}.");
    }

    static void Apply(GUISkin skin, PixelUITheme theme, int scale)
    {
        if (skin == null) return;

        // VT323 is the default face: nearly every unconverted panel is a readout, and its fixed advance
        // keeps their hand-spaced columns lined up.
        if (theme.imguiFont != null) skin.font = theme.imguiFont;

        int body = 16 * scale;   // VT323's cell
        int small = 8 * scale;   // Silkscreen's

        StyleText(skin.label, theme.text, body);
        StyleText(skin.textField, theme.text, body);
        StyleText(skin.textArea, theme.text, body);
        StyleText(skin.toggle, theme.textDim, body);
        StyleText(skin.window, theme.gold, body);
        if (skin.toggle != null) skin.toggle.onNormal.textColor = theme.text;

        // Buttons and boxes get the kit's plate art, point-upscaled so their 9-slice borders keep their
        // authored thickness at this display scale rather than thinning to a hairline.
        StyleButton(skin.button, theme, scale, small, theme.buttonRed, border: 3);
        StyleButton(skin.box, theme, scale, body, theme.frameCream, border: 4);
        if (skin.button != null && theme.imguiDisplayFont != null) skin.button.font = theme.imguiDisplayFont;

        // Sliders: a flat trough in the inner-shade colour with an accent thumb, both scaled. Unity's
        // own rounded slider art is the single loudest "this is a prototype" tell left in the debug panels.
        StyleSlider(skin.horizontalSlider, PixelGUI.Solid(theme.plateLight), 0, 6 * scale);
        StyleSlider(skin.horizontalSliderThumb, PixelGUI.Solid(theme.gold), 6 * scale, 12 * scale);
        StyleSlider(skin.verticalSlider, PixelGUI.Solid(theme.plateLight), 6 * scale, 0);
        StyleSlider(skin.verticalSliderThumb, PixelGUI.Solid(theme.gold), 12 * scale, 6 * scale);
    }

    static void StyleSlider(GUIStyle style, Texture2D tex, int fixedWidth, int fixedHeight)
    {
        if (style == null) return;
        style.normal.background = tex;
        style.hover.background = tex;
        style.active.background = tex;
        style.focused.background = tex;
        style.border = new RectOffset();
        style.fixedWidth = fixedWidth;
        style.fixedHeight = fixedHeight;
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

    // One sprite for every state, as the kit specifies: the highlight and shade are painted into the
    // drawing, so a hover tint or a second plate would only fight it.
    static void StyleButton(GUIStyle style, PixelUITheme theme, int scale, int size, Sprite plate, int border)
    {
        if (style == null) return;
        style.fontSize = size;

        var tex = PixelGUI.Up(plate);
        if (tex != null)
        {
            style.normal.background = tex;
            style.hover.background = tex;
            style.active.background = tex;
            style.focused.background = tex;
            style.onNormal.background = tex;
            style.onHover.background = tex;
            style.border = new RectOffset(border * scale, border * scale, border * scale, border * scale);
            // A sliced background needs room for its border or the corners eat the label.
            style.padding = new RectOffset((border + 3) * scale, (border + 3) * scale,
                                           (border + 1) * scale, (border + 1) * scale);
        }

        StyleText(style, theme.text, size);
    }
}
