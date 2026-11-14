#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Master Draft series tree icons dropped into Resources/MasterDraft/TreeIcons come in ready to draw: a single
// point-filtered, uncompressed sprite with no mipmaps, so the 24x24 pixel art stays crisp on the tile
// (MasterDraftUI.SeriesMap.cs loads them by name, SeriesTree.IconFor).
public class SeriesTreeIconImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/MasterDraft/TreeIcons/";

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
#endif
