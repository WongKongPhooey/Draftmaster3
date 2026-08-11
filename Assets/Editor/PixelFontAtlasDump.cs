#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Exports the pixel font's glyph atlas to a PNG so the rasterisation can actually be looked at.
//
// Font problems are hard to diagnose from inside the editor: "barely readable with artifacting" can mean
// the atlas was rasterised badly, or that the atlas is fine and the material is decoding it wrong. Dumping
// the atlas separates the two -- if the PNG shows clean hard-edged glyphs, the atlas is right and the fault
// is in the shader or the filtering.
public static class PixelFontAtlasDump
{
    [MenuItem("Draftmaster/Art/Dump Pixel Font Atlas", priority = 122)]
    public static void Run()
    {
        var theme = AssetDatabase.LoadAssetAtPath<PixelUITheme>("Assets/Resources/UI/PixelUITheme.asset");
        var font = theme != null ? theme.body : null;
        if (font == null)
        {
            Debug.LogWarning("[PixelFontAtlasDump] no body font on the theme.");
            return;
        }

        // The atlas is populated on demand, so an untouched font asset has an empty texture. Ask for the
        // printable ASCII range first or the dump is a blank sheet.
        var sb = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) sb.Append(c);
        font.TryAddCharacters(sb.ToString(), out string missing);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning($"[PixelFontAtlasDump] font is missing glyphs: {missing}");

        var atlas = font.atlasTexture;
        if (atlas == null)
        {
            Debug.LogWarning("[PixelFontAtlasDump] font asset has no atlas texture.");
            return;
        }

        // The atlas is single-channel and usually not CPU-readable; blit it through a RenderTexture to
        // get the pixels back, then spread the alpha across RGB so the PNG is visible rather than a
        // transparent sheet.
        var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(atlas, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = readable.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            // Coverage lands in red for an R8 atlas and in alpha for an Alpha8 one, and which of the two
            // TMP picks varies by version — take whichever channel actually carries the glyph.
            byte v = px[i].r > px[i].a ? px[i].r : px[i].a;
            px[i] = new Color32(v, v, v, 255);
        }
        readable.SetPixels32(px);
        readable.Apply();

        string dir = Path.Combine(Path.GetTempPath(), "draftmaster-font");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "pixel-font-atlas.png");
        File.WriteAllBytes(path, readable.EncodeToPNG());
        Object.DestroyImmediate(readable);

        string shader = font.material != null && font.material.shader != null ? font.material.shader.name : "none";
        Debug.Log($"[PixelFontAtlasDump] {atlas.width}x{atlas.height}, mode {font.atlasRenderMode}, " +
                  $"shader {shader}, point size {font.faceInfo.pointSize} -> {path}");

        File.WriteAllText("Docs/PixelFontAtlas.txt",
            $"atlas: {path}\nsize: {atlas.width}x{atlas.height}\nformat: {atlas.graphicsFormat}\n" +
            $"renderMode: {font.atlasRenderMode}\nshader: {shader}\n" +
            $"pointSize: {font.faceInfo.pointSize}\nlineHeight: {font.faceInfo.lineHeight}\n" +
            $"scale: {font.faceInfo.scale}\nglyphs: {font.glyphTable.Count}\n");
        AssetDatabase.Refresh();
    }
}
#endif
