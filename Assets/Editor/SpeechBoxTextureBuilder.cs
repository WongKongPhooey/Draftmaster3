using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the placeholder speech-box texture SpeechBubble uses as its 9-sliced background
// (Resources/OnFoot/SpeechBox.png): a pixel-art dialogue frame — dark outline, light inner frame,
// navy fill, clipped pixel corners. The PNG is YOURS to repaint (Build refuses to overwrite);
// keep the import settings (Sprite/Single, Point filter, 8px 9-slice border, PPU 100) so the
// bubble keeps slicing and scaling correctly at any box size.
public static class SpeechBoxTextureBuilder
{
    const string TexPath = "Assets/Resources/OnFoot/SpeechBox.png";
    const int Size = 48;      // texture is square; 9-slice stretches the middle
    const int Border = 8;     // 9-slice border, px — art's frame must stay inside this

    [MenuItem("Draftmaster/UI/Build Speech Box Texture")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath) != null)
        {
            Debug.LogWarning($"Speech box texture already exists at {TexPath} — it may hold your hand-painted art. " +
                             "Repaint it externally, or Force Rebuild to start over.");
            return;
        }
        BuildInternal();
    }

    [MenuItem("Draftmaster/UI/Force Rebuild Speech Box Texture (loses hand edits)")]
    public static void ForceRebuild()
    {
        BuildInternal();
    }

    static void BuildInternal()
    {
        var outline = new Color32(20, 18, 26, 255);
        var frame = new Color32(236, 236, 240, 255);
        var fill = new Color32(33, 36, 51, 245);
        var clear = new Color32(0, 0, 0, 0);

        int s = Size;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int dx = Mathf.Min(x, s - 1 - x);
                int dy = Mathf.Min(y, s - 1 - y);
                int d = Mathf.Min(dx, dy);
                int diag = dx + dy;

                Color32 c;
                if (diag < 2) c = clear;                    // clipped pixel corner
                else if (d <= 1 || diag <= 3) c = outline;  // 2px dark outline (bends round the corner)
                else if (d <= 3 || diag <= 5) c = frame;    // 2px light inner frame
                else c = fill;
                px[y * s + x] = c;
            }
        tex.SetPixels32(px);

        Directory.CreateDirectory(Path.GetDirectoryName(TexPath));
        File.WriteAllBytes(TexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; // project default is Multiple, which yields no Sprite sub-asset
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(Border, Border, Border, Border); // 9-slice
        importer.filterMode = FilterMode.Point;              // crisp pixel look at any scale
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // SpriteRenderer's Sliced draw mode needs a FullRect sprite mesh — the default Tight mesh
        // renders nothing when sliced (the bubble background silently disappears).
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        Debug.Log($"Speech box texture built at {TexPath}. Repaint the PNG to restyle every dialogue bubble; " +
                  "keep the frame art inside the 8px 9-slice border.");
    }
}
