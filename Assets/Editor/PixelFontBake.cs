#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Bakes the pixel TMP faces to a fixed character set and locks them STATIC.
//
// They were built Dynamic, so TMP rasterised glyphs into the atlas on demand -- and in the editor it
// writes those glyphs back into the .asset. The files swung between ~25 KB (empty atlas) and ~1 MB
// (populated) from commit to commit, and whenever a smaller copy landed on disk under an editor still
// holding the larger in-memory atlas, the glyph table and the texture went out of step: dialogue drew
// '!', 'p', 'f', 'u' with slivers of their neighbours stamped over them. A static atlas is never
// written at runtime, so there is nothing left to drift.
//
// Anything outside the baked set renders as TMP's missing glyph. Add it to Charset and re-run.
public static class PixelFontBake
{
    static readonly string[] kFonts =
    {
        "Assets/Resources/Fonts/Fixedsys Pixel.asset",
        "Assets/Resources/Fonts/Silkscreen Pixel.asset",
        "Assets/Resources/Fonts/Pixelify Sans Pixel.asset",
    };

    // Printable ASCII + Latin-1 (accented names) + the typographic punctuation dialogue gets pasted with.
    static string Charset
    {
        get
        {
            var sb = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) sb.Append(c);
            for (char c = '¡'; c <= 'ÿ'; c++) sb.Append(c);
            sb.Append("‘’“”–—…•€™");
            return sb.ToString();
        }
    }

    [MenuItem("Draftmaster/Art/Bake Pixel Fonts (static atlas)", priority = 125)]
    public static void Run()
    {
        string charset = Charset;
        var bitmap = Shader.Find("TextMeshPro/Bitmap") ?? Shader.Find("TextMeshPro/Mobile/Bitmap");

        foreach (var path in kFonts)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) { Debug.LogWarning($"[PixelFontBake] {path} not found."); continue; }
            if (font.sourceFontFile == null)
            {
                Debug.LogWarning($"[PixelFontBake] {font.name} has no source font file — cannot re-rasterise.");
                continue;
            }

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;   // TryAddCharacters refuses a static asset
            font.ClearFontAssetData(false);
            font.TryAddCharacters(charset, out string missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            if (bitmap != null && font.material != null && font.material.shader != bitmap)
                font.material.shader = bitmap;
            if (font.atlasTexture != null)
            {
                font.atlasTexture.filterMode = FilterMode.Point;
                EditorUtility.SetDirty(font.atlasTexture);
            }
            if (font.material != null) EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font);

            Debug.Log($"[PixelFontBake] {font.name}: {font.glyphTable.Count} glyphs, " +
                      $"{font.characterTable.Count} chars, mode {font.atlasPopulationMode}, " +
                      $"render {font.atlasRenderMode}" +
                      (string.IsNullOrEmpty(missing) ? "" : $", not in face: {missing}"));
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
