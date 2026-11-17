using UnityEditor;
using UnityEngine;

namespace IronOval.Cards.EditorTools
{
    /// Applies the Iron Oval pixel-art import settings to everything under Cards/Sprites.
    /// Matches the existing UI kit handoff: Point filter, no compression, no mips, full-rect meshes.
    public class IronOvalCardsSpriteImporter : AssetPostprocessor
    {
        public const string SpriteRoot = "Assets/IronOval/Cards/Sprites/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(SpriteRoot)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = IronOvalCardsBuilder.PixelsPerUnit;
            ti.maxTextureSize = 2048;

            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool tile = file.StartsWith("stripes_") || file.StartsWith("scanline_");
            ti.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteBorder = file == "px_frame" ? new Vector4(1, 1, 1, 1) : Vector4.zero;
            ti.SetTextureSettings(s);
        }
    }
}
