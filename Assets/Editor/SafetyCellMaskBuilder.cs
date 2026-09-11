using System.IO;
using Draftmaster.Weekend;
using UnityEditor;
using UnityEngine;

// Paints one safety-cell mask per championship and imports it the way VehicleDamage needs to read it.
//
// A mask is a greyscale picture the size of a car livery: black where the shell is a tub and cannot fold,
// white where it is bodywork, with a soft band between the two. VehicleDamage samples it per vertex when it
// builds a car's deformable mesh, so the cell is a shape somebody drew rather than a rectangle described by
// two numbers — which is what a truck needs, its cab being forward of centre and squarer than a stock car's.
//
// The shapes themselves live in SeriesSafetyCells.ShapeOf, so the baked texture and the code fallback can
// never disagree about where the tub is. Edit them there, run this again, and the paint follows.
//
// The files are ordinary PNGs. Open one in any paint program and push the shape around by hand — nothing
// here reads them back, so a hand-edit survives everything except running this menu item again.
public static class SafetyCellMaskBuilder
{
    // A livery is 64x32. The mask is baked at four times that: it is sampled bilinearly at a handful of
    // vertices, and a soft edge across 32 pixels of car has room to actually be soft.
    const int Width = 256;
    const int Height = 128;

    [MenuItem("Draftmaster/Art/Build Safety Cell Masks")]
    public static void BuildAll()
    {
        string folder = $"Assets/Resources/{SeriesSafetyCells.Folder}";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Resources", SeriesSafetyCells.Folder);

        foreach (var series in SeriesCatalog.All)
            BuildOne(series);

        AssetDatabase.Refresh();
        SeriesSafetyCells.ForgetCache();

        Debug.Log($"Safety cell masks written to {folder}. Cars pick them up the next time their bodywork " +
                  "is built — re-enter play mode to see it.");
    }

    static void BuildOne(RacingSeries series)
    {
        var cell = SeriesSafetyCells.ShapeOf(series);
        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: false);
        var pixels = new Color32[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            // Pixel centres, so the shape is symmetric about the middle of the image rather than off by
            // half a pixel — which shows up as a cell that sits slightly left of where it was authored.
            float fy = (y + 0.5f) / Height;
            for (int x = 0; x < Width; x++)
            {
                float fx = (x + 0.5f) / Width;
                byte v = (byte)Mathf.RoundToInt(SeriesSafetyCells.DeformAt(cell, fx, fy) * 255f);
                pixels[y * Width + x] = new Color32(v, v, v, 255);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        string path = SeriesSafetyCells.AssetPath(series);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Configure(path);
    }

    // The import settings the mask is useless without. It is read on the CPU, so Read/Write has to be on
    // and the compression off; it is data rather than a picture, so it must not be colour-converted on the
    // way in — an sRGB import would bend every grey and move the soft edge.
    static void Configure(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = Mathf.Max(Width, Height);
        importer.alphaSource = TextureImporterAlphaSource.None;

        importer.SaveAndReimport();
    }
}
