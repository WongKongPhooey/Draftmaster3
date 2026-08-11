#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Moves the authored Canvas UI in the open scene onto the pixel kit: theme font on every label, palette
// colours in place of the ad-hoc greys, and integer-scaling canvas scalers.
//
// The IMGUI panels are handled at runtime by PixelGUISkin and the code-built uGUI ones by BrandFonts, but
// authored prefabs (the speedometer, the position tracker) carry their fonts and colours in serialized
// data, so they need an actual edit. Doing it as a tool rather than by hand keeps it repeatable and makes
// the change reviewable in one place.
//
// Colour mapping is conservative: near-white becomes the theme's text colour, mid greys become textDim,
// and strong reds/greens/ambers map onto the palette's status colours. Anything already close to a theme
// colour is left alone.
public static class PixelUIRestyle
{
    [MenuItem("Draftmaster/Art/Restyle Scene Canvas UI", priority = 125)]
    public static void Run()
    {
        var theme = PixelUITheme.Instance;
        if (theme == null)
        {
            Debug.LogWarning("[PixelUIRestyle] theme not loaded — run Set Up Pixel UI Kit first.");
            return;
        }

        var report = new List<string>();
        int fonts = 0, colours = 0, scalers = 0;

        foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Only take over canvases already scaling with the screen; a constant-pixel canvas is usually
            // deliberate (a debug overlay), and forcing it would move things the author placed by hand.
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (Mathf.Approximately(scaler.referenceResolution.x, PixelUITheme.ReferenceWidth)) continue;

            Undo.RecordObject(scaler, "Restyle canvas");
            // Keep the authored 1920x1080 layout coordinates -- rewriting them to 640x360 would rescale
            // every hand-placed element by 3. Only the pixel-per-unit reference is corrected, so sliced
            // sprites from the kit render at the right density on this canvas.
            scaler.referencePixelsPerUnit = PixelUITheme.ReferencePixelsPerUnit;
            EditorUtility.SetDirty(scaler);
            scalers++;
            report.Note(scaler.name, "canvas scaler referencePixelsPerUnit -> 100");
        }

        foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (theme.body == null) break;
            Undo.RecordObject(label, "Restyle label");

            bool heading = label.fontSize >= 40f;
            var font = heading && theme.display != null ? theme.display : theme.body;
            if (label.font != font)
            {
                label.font = font;
                if (font.material != null) label.fontSharedMaterial = font.material;
                fonts++;
            }

            var mapped = MapColour(label.color, theme);
            if (mapped != label.color) { label.color = mapped; colours++; }

            EditorUtility.SetDirty(label);
            report.Note(label.name, $"{(heading ? "display" : "body")} font, colour {ColorUtility.ToHtmlStringRGB(mapped)}");
        }

        foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(text, "Restyle label");
            if (theme.imguiFont != null && text.font != theme.imguiFont) { text.font = theme.imguiFont; fonts++; }
            var mapped = MapColour(text.color, theme);
            if (mapped != text.color) { text.color = mapped; colours++; }
            EditorUtility.SetDirty(text);
            report.Note(text.name, "legacy Text -> pixel font");
        }

        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/PixelUIRestyle.md",
            "# Canvas UI restyle\n\n" +
            $"Scene(s): {string.Join(", ", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name))}\n\n" +
            $"{fonts} font change(s), {colours} colour change(s), {scalers} canvas scaler(s).\n\n" +
            "| object | change |\n|---|---|\n" +
            string.Join("\n", report));
        AssetDatabase.Refresh();

        Debug.Log($"[PixelUIRestyle] {fonts} fonts, {colours} colours, {scalers} scalers. See Docs/PixelUIRestyle.md");
    }

    // Unifies the ad-hoc greys and off-whites onto the theme's text colours, and leaves every saturated
    // colour exactly as authored.
    //
    // An earlier version also snapped hues onto the palette's status colours. That was too blunt: it turned
    // the dialogue text blue and the speaker name orange rather than gold, because a hue bucket cannot tell
    // "this blue is a deliberate status colour" from "this blue is just what someone typed". A saturated
    // colour in this project is nearly always deliberate, so the safe rule is to touch only neutrals --
    // which is where the inconsistency actually lived.
    static Color MapColour(Color c, PixelUITheme theme)
    {
        if (c.a < 0.05f) return c;   // invisible; leave it

        Color.RGBToHSV(c, out _, out float s, out float v);
        if (s >= 0.18f) return c;    // deliberate colour — not ours to reinterpret

        return v > 0.75f ? Keep(c, theme.text)        // body copy
             : v > 0.40f ? Keep(c, theme.textDim)     // secondary copy
             : Keep(c, theme.plateDeep);              // dark backing plate
    }

    // Preserves the original alpha so translucent backing plates stay translucent.
    static Color Keep(Color original, Color mapped) => new Color(mapped.r, mapped.g, mapped.b, original.a);

    static void Note(this List<string> list, string name, string change) =>
        list.Add($"| {name} | {change} |");
}
#endif
