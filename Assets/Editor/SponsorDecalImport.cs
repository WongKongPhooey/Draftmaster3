#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Car decal art dropped into Resources/Sponsors/Hood, Small or Car — and PaintShop livery layers and
// patterns — come in ready for the baker: Read/Write
// (SponsorLiveryBaker reads its pixels), point-filtered, uncompressed and on the 12.8 px/m grid. Without
// this a fresh PNG imports unreadable and the car silently stays bare. Full-size brand images
// (Sponsors/Full) are UI art and keep whatever import they are given.
public class SponsorDecalImport : AssetPostprocessor
{
    static readonly string[] kDecalFolders =
    {
        "Assets/Resources/Sponsors/Hood/",
        "Assets/Resources/Sponsors/Small/",
        "Assets/Resources/Sponsors/Car/",
        "Assets/Resources/PaintShop/",      // livery layers + patterns: PaintShop composites their pixels
    };

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        bool isDecal = false;
        foreach (string folder in kDecalFolders)
            if (path.StartsWith(folder)) { isDecal = true; break; }
        if (!isDecal) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelArt.PixelsPerMetre;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
    }
}
#endif
