#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Brings world-space sprite imports onto the project pixel standard.
//
// The art library was drawn for 12.8 px/m all along -- asphalt-128 is exactly 10.00m, kerb.png is
// 1.25 x 2.50m, garage.png is 10.00 x 7.50m, a livery is 5.00 x 2.50m. What drifted is the IMPORT
// setting: 1551 sprites came in at Unity's default 100 px/unit and only the cup26 carset at 12.8, so a
// cup20 car would spawn 0.64m long next to a 5m one and props land at a fraction of their drawn size.
//
// This only touches sprites that are placed in the WORLD. UI sprites (icons, menu backgrounds, cards,
// sponsor logos, paint-booth swatches) are laid out in canvas pixels, where PPU has a different job, so
// their folders are deliberately excluded.
public static class PixelSpriteImport
{
    // Folders whose sprites are rendered in the world at metre scale.
    static readonly string[] kWorldFolders =
    {
        "Assets/Textures/Environment",
        "Assets/Textures/Props",
        "Assets/Sprites/Props",
        "Assets/Sprites/Walking",
        "Assets/Sprites/NPCs",
        "Assets/Sprites/Pushing",
        "Assets/Resources/Environment",
        "Assets/Resources/OnFoot",
    };

    // Folders that are UI regardless of what else matches -- never retargeted.
    static readonly string[] kExcludedFolders =
    {
        "Assets/Textures/Icons",
        "Assets/Textures/Cards",
        "Assets/Textures/Backgrounds",
        "Assets/Textures/CarEditor",
        "Assets/Textures/Sponsors",
        "Assets/Textures/Manufacturers",
        "Assets/Resources/Icons",
        "Assets/Resources/UI",
        "Assets/Resources/PaintBooth",
        "Assets/Resources/Events",
        "Assets/UI",
        "Assets/GUI",
        "Assets/RefImages",
    };

    // Single files that live in a world folder but are NOT drawn art: a flat unit quad, stretched to its
    // metres by transform scale, which is the one place that is the right thing to do. Retargeting one of
    // these to 12.8 px/m does not resize a drawing — it silently rescales everything built out of it. The
    // white square is what the RV exterior and its interior floor are made of, and a pass over this folder
    // once shrank the whole motorhome to 4/12.8 of its size inside a full-size collider shell.
    static readonly string[] kExcludedFiles =
    {
        "Assets/Textures/Environment/WhiteSquare.png",
    };

    // Sprites the CPU has to read pixels from at runtime, rather than just hand to the GPU.
    static bool NeedsCpuRead(string path)
    {
        string p = path.Replace('\\', '/');
        return Path.GetFileName(p).Contains("livery") || p.Contains("/Resources/Sponsors/");
    }

    [MenuItem("Draftmaster/Art/Retarget World Sprites to Pixel Standard", priority = 110)]
    public static void Run()
    {
        var paths = new List<string>();

        // Every carset livery, wherever it sits in Resources. These are the worst offenders: the same
        // 64x32 drawing imported at two different PPUs across carsets.
        paths.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileName(p).Contains("livery")));

        foreach (var folder in kWorldFolders.Where(AssetDatabase.IsValidFolder))
            paths.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath));

        paths = paths.Distinct()
                     .Where(p => !kExcludedFolders.Any(x => p.Replace('\\', '/').StartsWith(x + "/")))
                     .Where(p => !kExcludedFiles.Contains(p.Replace('\\', '/')))
                     .ToList();

        int changed = 0, skipped = 0;
        var log = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < paths.Count; i++)
            {
                var importer = AssetImporter.GetAtPath(paths[i]) as TextureImporter;
                if (importer == null) { skipped++; continue; }
                if (importer.textureType != TextureImporterType.Sprite) { skipped++; continue; }

                bool dirty = false;
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, PixelArt.PixelsPerMetre))
                {
                    log.Add($"{paths[i]}: {importer.spritePixelsPerUnit} -> {PixelArt.PixelsPerMetre} px/unit");
                    importer.spritePixelsPerUnit = PixelArt.PixelsPerMetre;
                    dirty = true;
                }
                // Pixel art must not be smoothed, mipped or block-compressed: filtering it re-samples the
                // very pixel grid this standard exists to keep honest.
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                // Liveries are composited at runtime — SponsorLiveryBaker reads a car's paint and blits the
                // sponsor decals its driver has sold into a copy of it — so they have to be Read/Write.
                // Only the liveries: making the whole 1500-sprite world library readable would double its
                // memory for no reason. A 64x32 paint costs 8KB.
                if (NeedsCpuRead(paths[i]) && !importer.isReadable)
                {
                    importer.isReadable = true;
                    dirty = true;
                }

                if (dirty) { importer.SaveAndReimport(); changed++; }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        foreach (var l in log.Take(40)) Debug.Log($"[PixelSpriteImport] {l}");
        Debug.Log($"[PixelSpriteImport] {changed} sprite(s) retargeted to {PixelArt.PixelsPerMetre} px/m, " +
                  $"{skipped} skipped (not sprites), {paths.Count} considered.");
    }
}
#endif
