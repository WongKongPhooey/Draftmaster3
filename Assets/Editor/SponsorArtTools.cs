#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Draftmaster.Data;
using Draftmaster.Sponsors;
using UnityEditor;
using UnityEngine;

// Authoring tools for car sponsorship:
//   • Generate Placeholder Decals — a car-scale logo per brand in the database, so the feature is playable
//     before any real art exists. Overwrite the PNGs with real art later; nothing else changes.
//   • Create Car Sponsor Layout — the asset that says where the hood / tail / quarter panels are.
//   • Preview Slots On Livery — writes a magnified PNG of the panels drawn over a real livery to
//     Temp/SponsorSlotPreview.png, for eyeballing the rects without entering play mode.
//
// Decals are drawn at the panel's own pixel size (12x6 by default): the baker never scales art, because
// resampling would take it off the project's 12.8 px/m grid.
public static class SponsorArtTools
{
    const string CarArtDir = "Assets/Resources/Sponsors/Car";
    const string LayoutPath = "Assets/Resources/Sponsors/cup26Layout.asset";
    const string PreviewLivery = "cup26livery8";

    [MenuItem("Draftmaster/Sponsors/Generate Placeholder Decals", priority = 200)]
    public static void GenerateDecals()
    {
        var layout = LoadOrCreateLayout();
        Vector2Int size = layout.SmallestPanel();

        Directory.CreateDirectory(CarArtDir);
        int written = 0;
        foreach (var sponsor in DummySponsors.Build())
        {
            string key = SponsorCatalog.LogoKey(sponsor.Name);
            string path = $"{CarArtDir}/{key}.png";
            var tex = BuildDecal(sponsor, size.x, size.y);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            written++;
        }

        AssetDatabase.Refresh();
        foreach (var sponsor in DummySponsors.Build())
            ApplyDecalImport($"{CarArtDir}/{SponsorCatalog.LogoKey(sponsor.Name)}.png");

        AssetDatabase.SaveAssets();
        Debug.Log($"[Sponsors] Wrote {written} placeholder decals ({size.x}x{size.y}px) to {CarArtDir}. " +
                  "Replace any file with real art at the same size and filename.");
    }

    [MenuItem("Draftmaster/Sponsors/Create Car Sponsor Layout", priority = 201)]
    public static void CreateLayout()
    {
        var layout = LoadOrCreateLayout();
        Selection.activeObject = layout;
        EditorGUIUtility.PingObject(layout);
    }

    [MenuItem("Draftmaster/Sponsors/Preview Slots On Livery", priority = 202)]
    public static void PreviewSlots()
    {
        var layout = LoadOrCreateLayout();
        var livery = Resources.Load<Sprite>(PreviewLivery);
        if (livery == null || livery.texture == null)
        {
            Debug.LogError($"[Sponsors] Preview needs Resources/{PreviewLivery}.");
            return;
        }
        if (!livery.texture.isReadable)
        {
            Debug.LogError($"[Sponsors] {PreviewLivery} is not Read/Write enabled. " +
                           "Run Draftmaster > Art > Retarget World Sprites to Pixel Standard first.");
            return;
        }

        const int zoom = 8;
        int w = livery.texture.width, h = livery.texture.height;
        var src = livery.texture.GetPixels32();
        var big = new Texture2D(w * zoom, h * zoom, TextureFormat.RGBA32, false);
        var outPx = new Color32[w * zoom * h * zoom];

        // Panel tints, drawn over the paint so both the rect and the art under it stay readable.
        var tints = new Dictionary<SponsorSlot, Color32>
        {
            { SponsorSlot.Hood,         new Color32(255, 80, 80, 255) },
            { SponsorSlot.Tail,         new Color32(80, 160, 255, 255) },
            { SponsorSlot.QuarterLeft,  new Color32(90, 230, 120, 255) },
            { SponsorSlot.QuarterRight, new Color32(240, 220, 90, 255) },
        };

        for (int y = 0; y < h * zoom; y++)
        {
            for (int x = 0; x < w * zoom; x++)
            {
                int sx = x / zoom, sy = y / zoom;
                Color32 c = src[sy * w + sx];
                if (c.a == 0) c = new Color32(30, 30, 34, 255);   // dark backdrop so the silhouette reads

                foreach (var kv in tints)
                {
                    RectInt r = layout.RectFor(kv.Key);
                    if (r.width <= 0 || r.height <= 0) continue;
                    bool inside = sx >= r.x && sx < r.x + r.width && sy >= r.y && sy < r.y + r.height;
                    if (!inside) continue;
                    bool edge = sx == r.x || sx == r.x + r.width - 1 || sy == r.y || sy == r.y + r.height - 1;
                    Color32 t = kv.Value;
                    float mix = edge ? 0.85f : 0.35f;
                    c = new Color32((byte)(t.r * mix + c.r * (1 - mix)),
                                    (byte)(t.g * mix + c.g * (1 - mix)),
                                    (byte)(t.b * mix + c.b * (1 - mix)), 255);
                }
                outPx[y * w * zoom + x] = c;
            }
        }

        big.SetPixels32(outPx);
        big.Apply();

        // Unity textures are bottom-up; flip so the PNG reads the way the art does in an image viewer.
        var flipped = new Texture2D(big.width, big.height, TextureFormat.RGBA32, false);
        var fp = new Color32[outPx.Length];
        for (int y = 0; y < big.height; y++)
            System.Array.Copy(outPx, y * big.width, fp, (big.height - 1 - y) * big.width, big.width);
        flipped.SetPixels32(fp);
        flipped.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "Temp");
        Directory.CreateDirectory(dir);
        string path = Path.GetFullPath(Path.Combine(dir, "SponsorSlotPreview.png"));
        File.WriteAllBytes(path, flipped.EncodeToPNG());
        Object.DestroyImmediate(big);
        Object.DestroyImmediate(flipped);

        Debug.Log($"[Sponsors] Slot preview written to {path} (hood=red, tail=blue, left quarter=green, right quarter=yellow).");
    }

    [MenuItem("Draftmaster/Sponsors/Preview Sponsored Car", priority = 203)]
    public static void PreviewSponsoredCar()
    {
        var layout = LoadOrCreateLayout();
        var livery = Resources.Load<Sprite>(PreviewLivery);
        if (livery == null) { Debug.LogError($"[Sponsors] Preview needs Resources/{PreviewLivery}."); return; }

        // Runs the real runtime baker, so what this shows is what a car wears in the scene.
        var brands = DummySponsors.Build();
        var decals = new List<SponsorLiveryBaker.Decal>();
        var slots = SponsorSlots.All;
        for (int i = 0; i < slots.Length && i < brands.Count; i++)
        {
            var art = Resources.Load<Texture2D>(SponsorKeys.CarArtPath(brands[i].Name));
            if (art != null) decals.Add(new SponsorLiveryBaker.Decal { slot = slots[i], art = art });
        }
        if (decals.Count == 0) { Debug.LogError("[Sponsors] No decal art — run Generate Placeholder Decals first."); return; }

        SponsorLiveryBaker.ClearCache();
        var painted = SponsorLiveryBaker.Bake(livery, layout, decals);
        if (painted == null || painted == livery) { Debug.LogError("[Sponsors] Bake returned the bare livery — see the warning above."); return; }

        WriteZoomed(painted.texture, "SponsorCarPreview.png");
        Debug.Log($"[Sponsors] Sponsored-car preview written for {decals.Count} decal(s): " +
                  string.Join(", ", decals.ConvertAll(d => $"{d.slot}={d.art.name}")));
    }

    // Magnified, flipped PNG of a 64x32 texture, written to Temp/ for eyeballing outside the editor.
    static void WriteZoomed(Texture2D tex, string fileName, int zoom = 8)
    {
        int w = tex.width, h = tex.height;
        var src = tex.GetPixels32();
        var outPx = new Color32[w * zoom * h * zoom];
        for (int y = 0; y < h * zoom; y++)
        {
            for (int x = 0; x < w * zoom; x++)
            {
                Color32 c = src[(y / zoom) * w + (x / zoom)];
                if (c.a == 0) c = new Color32(30, 30, 34, 255);
                outPx[(h * zoom - 1 - y) * w * zoom + x] = c;   // flip: textures are bottom-up
            }
        }
        var big = new Texture2D(w * zoom, h * zoom, TextureFormat.RGBA32, false);
        big.SetPixels32(outPx);
        big.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "Temp");
        Directory.CreateDirectory(dir);
        string path = Path.GetFullPath(Path.Combine(dir, fileName));
        File.WriteAllBytes(path, big.EncodeToPNG());
        Object.DestroyImmediate(big);
        Debug.Log($"[Sponsors] Wrote {path}");
    }

    // ---------------------------------------------------------------- assets

    static CarSponsorLayout LoadOrCreateLayout()
    {
        var layout = AssetDatabase.LoadAssetAtPath<CarSponsorLayout>(LayoutPath);
        if (layout != null) return layout;

        Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
        layout = ScriptableObject.CreateInstance<CarSponsorLayout>();
        AssetDatabase.CreateAsset(layout, LayoutPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Sponsors] Created {LayoutPath} with the default cup26 panel rects.");
        return layout;
    }

    static void ApplyDecalImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelArt.PixelsPerMetre;   // car-scale art, same grid as the paint
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;                               // the baker reads these pixels at runtime
        importer.maxTextureSize = 32;
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- placeholder art

    // A coloured plate with the brand's initials in a 3x5 pixel font. Ugly on purpose — it reads as
    // placeholder art, not as something finished, while still being legible on the hood at race zoom.
    static Texture2D BuildDecal(Sponsor sponsor, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32 plate = IndustryColour(sponsor.Industry);
        Color32 ink = Luminance(plate) > 140 ? new Color32(20, 20, 24, 255) : new Color32(245, 245, 240, 255);

        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = plate;

        string initials = Initials(sponsor.Name);
        int glyphW = 3, gap = 1;
        int textW = initials.Length * glyphW + (initials.Length - 1) * gap;
        int x0 = Mathf.Max(0, (w - textW) / 2);
        int y0 = Mathf.Max(0, (h - 5) / 2);

        for (int i = 0; i < initials.Length; i++)
        {
            var rows = Glyph(initials[i]);
            for (int gy = 0; gy < 5; gy++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    if (rows[gy][gx] != '#') continue;
                    int x = x0 + i * (glyphW + gap) + gx;
                    // Texture rows run bottom-up; the glyph table is written top-down.
                    int y = y0 + (4 - gy);
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    px[y * w + x] = ink;
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static string Initials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "SP";
        var words = name.Split(new[] { ' ', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1)
        {
            string one = words[0].ToUpperInvariant();
            return one.Length <= 3 ? one : one.Substring(0, 3);
        }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < words.Length && sb.Length < 3; i++) sb.Append(char.ToUpperInvariant(words[i][0]));
        return sb.ToString();
    }

    static Color32 IndustryColour(string industry) => (industry ?? "").ToLowerInvariant() switch
    {
        "energy" => new Color32(240, 90, 40, 255),
        "telecom" => new Color32(70, 130, 230, 255),
        "retail" => new Color32(235, 200, 60, 255),
        "auto" => new Color32(90, 95, 105, 255),
        "bank" => new Color32(40, 110, 90, 255),
        "tech" => new Color32(130, 80, 220, 255),
        "food" => new Color32(210, 70, 70, 255),
        "oil" => new Color32(30, 40, 60, 255),
        "insurance" => new Color32(200, 120, 40, 255),
        "beverage" => new Color32(70, 180, 210, 255),
        "travel" => new Color32(230, 140, 180, 255),
        _ => new Color32(160, 160, 165, 255),
    };

    static int Luminance(Color32 c) => (c.r * 3 + c.g * 6 + c.b) / 10;

    // 3x5 uppercase font, top row first. Only what brand initials need: A-Z and digits.
    static string[] Glyph(char c)
    {
        switch (char.ToUpperInvariant(c))
        {
            case 'A': return new[] { "###", "# #", "###", "# #", "# #" };
            case 'B': return new[] { "## ", "# #", "## ", "# #", "## " };
            case 'C': return new[] { "###", "#  ", "#  ", "#  ", "###" };
            case 'D': return new[] { "## ", "# #", "# #", "# #", "## " };
            case 'E': return new[] { "###", "#  ", "###", "#  ", "###" };
            case 'F': return new[] { "###", "#  ", "###", "#  ", "#  " };
            case 'G': return new[] { "###", "#  ", "# #", "# #", "###" };
            case 'H': return new[] { "# #", "# #", "###", "# #", "# #" };
            case 'I': return new[] { "###", " # ", " # ", " # ", "###" };
            case 'J': return new[] { "  #", "  #", "  #", "# #", "###" };
            case 'K': return new[] { "# #", "# #", "## ", "# #", "# #" };
            case 'L': return new[] { "#  ", "#  ", "#  ", "#  ", "###" };
            case 'M': return new[] { "# #", "###", "###", "# #", "# #" };
            case 'N': return new[] { "# #", "###", "###", "###", "# #" };
            case 'O': return new[] { "###", "# #", "# #", "# #", "###" };
            case 'P': return new[] { "###", "# #", "###", "#  ", "#  " };
            case 'Q': return new[] { "###", "# #", "# #", "###", "  #" };
            case 'R': return new[] { "###", "# #", "###", "## ", "# #" };
            case 'S': return new[] { "###", "#  ", "###", "  #", "###" };
            case 'T': return new[] { "###", " # ", " # ", " # ", " # " };
            case 'U': return new[] { "# #", "# #", "# #", "# #", "###" };
            case 'V': return new[] { "# #", "# #", "# #", "# #", " # " };
            case 'W': return new[] { "# #", "# #", "###", "###", "# #" };
            case 'X': return new[] { "# #", "# #", " # ", "# #", "# #" };
            case 'Y': return new[] { "# #", "# #", "###", " # ", " # " };
            case 'Z': return new[] { "###", "  #", " # ", "#  ", "###" };
            case '0': return new[] { "###", "# #", "# #", "# #", "###" };
            case '1': return new[] { " # ", "## ", " # ", " # ", "###" };
            case '2': return new[] { "###", "  #", "###", "#  ", "###" };
            case '3': return new[] { "###", "  #", "###", "  #", "###" };
            case '4': return new[] { "# #", "# #", "###", "  #", "  #" };
            case '5': return new[] { "###", "#  ", "###", "  #", "###" };
            case '6': return new[] { "###", "#  ", "###", "# #", "###" };
            case '7': return new[] { "###", "  #", "  #", "  #", "  #" };
            case '8': return new[] { "###", "# #", "###", "# #", "###" };
            case '9': return new[] { "###", "# #", "###", "  #", "###" };
            default: return new[] { "   ", "   ", "   ", "   ", "   " };
        }
    }
}
#endif
