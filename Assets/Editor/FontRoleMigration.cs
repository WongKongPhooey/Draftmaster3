using UnityEditor;
using UnityEngine;
using TMPro;

// Points the theme's type roles at the two faces that survived the legibility pass: Silkscreen for the
// display voice, Fixedsys for everything else.
//
// This edits the theme asset directly rather than going through Draftmaster/Art/Set Up Iron Oval Kit,
// because that path rebuilds every font asset with AtlasPopulationMode.Dynamic — which is what emptied
// the atlases and made the glyphs collide in the first place. Roles are data; they do not need a rebuild.
//
// Why the split rather than one face: Silkscreen has no lowercase (prose comes out shouting) and is
// proportional (timing columns go ragged). Fixedsys has true lowercase and a fixed advance, so it takes
// prose and every column readout. VT323 is gone — it collided at every size, rebuilt or not.
public static class FontRoleMigration
{
    const string ThemePath = "Assets/Resources/UI/PixelUITheme.asset";
    const string FontDir = "Assets/Resources/Fonts";

    [MenuItem("Draftmaster/Art/Apply Font Roles (Silkscreen + Fixedsys)")]
    public static void Run()
    {
        var theme = AssetDatabase.LoadAssetAtPath<PixelUITheme>(ThemePath);
        if (theme == null) { Debug.LogError($"[FontRoles] No theme at {ThemePath}."); return; }

        var silkscreen = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/Silkscreen Pixel.asset");
        var fixedsys = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/Fixedsys Pixel.asset");
        if (silkscreen == null || fixedsys == null)
        {
            Debug.LogError("[FontRoles] Silkscreen or Fixedsys asset missing — nothing applied.");
            return;
        }

        theme.display = silkscreen;   // headings, labels, buttons, the wordmark
        theme.body = fixedsys;        // prose and dialogue — needs real lowercase
        theme.data = fixedsys;        // columns, timing, telemetry — needs fixed advance

        var silkTtf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Silkscreen-Regular.ttf");
        var fixedTtf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/fixedsys.ttf");
        if (silkTtf != null) theme.imguiDisplayFont = silkTtf;
        if (fixedTtf != null) { theme.imguiFont = fixedTtf; theme.imguiBodyFont = fixedTtf; }

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        Debug.Log("[FontRoles] display=Silkscreen Pixel, body=data=Fixedsys Pixel, IMGUI body/data=fixedsys.ttf.");
    }
}
