#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// One-shot setup for the pixel UI kit: imports the generated frame/icon art with the right settings
// and 9-slice borders, builds a crisp bitmap font from fixedsys.ttf, and fills in the theme asset.
//
// Safe to re-run -- it overwrites settings and re-links the theme rather than duplicating anything.
public static class PixelUIKitSetup
{
    const string kArt = "Assets/UI/Pixel";
    const string kIcons = kArt + "/Icons";
    const string kFontDir = "Assets/Resources/Fonts";
    const string kThemePath = "Assets/Resources/UI/PixelUITheme.asset";

    // Sprite name -> 9-slice border, in source pixels. Anything not listed is imported unsliced,
    // which is what the icons and the cursor want.
    static readonly Dictionary<string, int> kBorders = new Dictionary<string, int>
    {
        { "window", 6 }, { "window-plain", 6 },
        { "button", 5 }, { "button-hover", 5 }, { "button-press", 5 },
        { "button-danger", 5 }, { "button-confirm", 5 },
        { "bar-track", 3 }, { "bar-gold", 3 }, { "bar-red", 3 }, { "bar-teal", 3 },
    };

    [MenuItem("Draftmaster/Art/Set Up Pixel UI Kit", priority = 120)]
    public static void Run()
    {
        ImportArt();
        var body = BuildPixelFont();
        BuildTheme(body);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PixelUIKitSetup] Pixel UI kit ready. Theme at " + kThemePath);
    }

    [MenuItem("Draftmaster/Art/Verify Pixel UI Kit", priority = 121)]
    public static void Verify()
    {
        var theme = AssetDatabase.LoadAssetAtPath<PixelUITheme>(kThemePath);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Pixel UI kit");
        sb.AppendLine();
        if (theme == null)
        {
            sb.AppendLine($"**Theme asset missing** at `{kThemePath}` — run Draftmaster/Art/Set Up Pixel UI Kit.");
            File.WriteAllText("Docs/PixelUIKit.md", sb.ToString());
            AssetDatabase.Refresh();
            return;
        }

        sb.AppendLine($"Theme: `{kThemePath}`  ");
        sb.AppendLine($"Canvas: {PixelUITheme.ReferenceWidth}x{PixelUITheme.ReferenceHeight} UI px " +
                      $"(x3 = 1920x1080, x6 = 3840x2160), reference PPU {PixelUITheme.ReferencePixelsPerUnit}.");
        sb.AppendLine();
        sb.AppendLine("| slot | asset | notes |");
        sb.AppendLine("|---|---|---|");

        void Row(string slot, Object o, string note = "")
            => sb.AppendLine($"| {slot} | {(o == null ? "**MISSING**" : o.name)} | {note} |");

        void SpriteRow(string slot, Sprite s)
        {
            string note = "";
            if (s != null)
            {
                var b = s.border;
                note = $"{s.rect.width}x{s.rect.height}px" +
                       (b == Vector4.zero ? ", unsliced" : $", 9-slice border {b.x}");
            }
            Row(slot, s, note);
        }

        SpriteRow("window", theme.window);
        SpriteRow("windowPlain", theme.windowPlain);
        SpriteRow("button", theme.button);
        SpriteRow("buttonHover", theme.buttonHover);
        SpriteRow("buttonPressed", theme.buttonPressed);
        SpriteRow("buttonDanger", theme.buttonDanger);
        SpriteRow("buttonConfirm", theme.buttonConfirm);
        SpriteRow("barTrack", theme.barTrack);
        SpriteRow("barGold", theme.barGold);
        SpriteRow("cursor", theme.cursor);
        SpriteRow("dialogueTail", theme.dialogueTail);

        string[] keys = { "money", "part", "fuel", "trophy", "star", "quest", "map",
                          "speech", "clock", "flag", "tyre", "heart", "wrench-set", "warning" };
        foreach (var k in keys) SpriteRow("icon " + k, theme.Icon(k));

        // The shader matters as much as the render mode: a bitmap atlas drawn through the distance-field
        // shader comes out fringed and barely readable, which looks like a bad font rather than a bad material.
        string bodyNote = "";
        if (theme.body != null)
        {
            string shader = theme.body.material != null && theme.body.material.shader != null
                ? theme.body.material.shader.name : "no material";
            string filter = theme.body.atlasTexture != null
                ? theme.body.atlasTexture.filterMode.ToString() : "?";
            bodyNote = $"{theme.body.atlasRenderMode}, shader `{shader}`, atlas filter {filter}";
            bool ok = theme.body.atlasRenderMode == UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER_HINTED
                      && shader.Contains("Bitmap") && filter == "Point";
            bodyNote += ok ? " — OK" : "  **<- expected RASTER_HINTED + a Bitmap shader + Point filter**";
        }
        Row("body font", theme.body, bodyNote);
        Row("display font", theme.display);
        Row("IMGUI font", theme.imguiFont, "used by PixelGUI panels, which cannot take a TMP asset");

        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/PixelUIKit.md", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[PixelUIKitSetup] wrote Docs/PixelUIKit.md");
    }

    static void ImportArt()
    {
        var paths = Directory.GetFiles(kArt, "*.png", SearchOption.AllDirectories)
                             .Select(p => p.Replace('\\', '/'));
        foreach (var path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            // One source pixel == one UI pixel, given the canvas keeps referencePixelsPerUnit at 100.
            importer.spritePixelsPerUnit = PixelUITheme.ReferencePixelsPerUnit;

            string name = Path.GetFileNameWithoutExtension(path);
            importer.spriteBorder = kBorders.TryGetValue(name, out int b)
                ? new Vector4(b, b, b, b)
                : Vector4.zero;

            importer.SaveAndReimport();
        }
    }

    // Point size fixedsys is drawn for. It is a bitmap design shipped as outlines, so it is only truly
    // crisp when rasterised at its native cell height (or a whole multiple of it).
    const int kPixelFontPointSize = 16;

    // Gap between glyphs in the atlas. Zero packs them edge to edge, and every glyph then samples a sliver
    // of its neighbours -- which reads as a ghosted double outline on each letter, not as blur. One texel
    // of separation is enough for a bitmap font (SDF needs far more, for its gradient).
    const int kPixelFontPadding = 1;

    // A pixel font must be rasterised, not signed-distance-field: SDF resamples the glyph onto a smooth
    // field, which rounds off the very corners that make it read as pixel art.
    //
    // Two things have to line up, and only one of them is the render mode:
    //
    //  * RASTER_HINTED atlas   — hard-edged glyphs with stems snapped to the pixel grid. Un-hinted RASTER
    //                            lets outlines land between pixels, so stems come out uneven.
    //  * TextMeshPro/Bitmap shader — CreateFontAsset always builds its material with the DISTANCE FIELD
    //                            shader, whatever the render mode. Leaving it there makes the shader read
    //                            a bitmap atlas as if its alpha were a distance field, which is what
    //                            produces heavy fringing and near-unreadable text.
    static TMP_FontAsset BuildPixelFont()
        => BuildBitmapFont("Assets/Fonts/fixedsys.ttf", kFontDir + "/Fixedsys Pixel.asset",
                           "Fixedsys Pixel", kPixelFontPointSize);

    // Builds one bitmap TMP font asset by the recipe above. Public because the Iron Oval kit needs the
    // same five things to line up for its three faces, and a second copy of this would drift.
    public static TMP_FontAsset BuildBitmapFont(string ttfPath, string outPath, string niceName, int pointSize)
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (ttf == null)
        {
            Debug.LogWarning($"[PixelUIKitSetup] {ttfPath} not found — '{niceName}' not built.");
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
        if (existing != null)
        {
            // Render mode, padding and the rasterised point size are all baked into the atlas, so an
            // asset built with any of them wrong has to be regenerated -- fixing the material alone
            // would leave the glyphs exactly as they were.
            if (existing.atlasRenderMode == GlyphRenderMode.RASTER_HINTED &&
                existing.atlasPadding == kPixelFontPadding &&
                Mathf.RoundToInt(existing.faceInfo.pointSize) == pointSize)
            {
                EnforceBitmapRendering(existing);
                return existing;
            }
            AssetDatabase.DeleteAsset(outPath);
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            ttf, pointSize, kPixelFontPadding, GlyphRenderMode.RASTER_HINTED, 1024, 1024,
            AtlasPopulationMode.Dynamic);
        if (fontAsset == null)
        {
            Debug.LogWarning($"[PixelUIKitSetup] TMP could not build a font asset from {ttfPath}.");
            return null;
        }

        fontAsset.name = niceName;
        AssetDatabase.CreateAsset(fontAsset, outPath);

        // The atlas and material are sub-assets and must be stored alongside the font asset.
        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = niceName + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = niceName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EnforceBitmapRendering(fontAsset);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
    }

    // Same hinted-raster treatment for a plain UnityEngine.Font (the IMGUI path). Public for the same reason.
    public static void ConfigurePixelTtf(string path, int pointSize)
    {
        var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (importer == null) return;

        bool dirty = false;
        if (importer.fontRenderingMode != FontRenderingMode.HintedRaster)
        {
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            dirty = true;
        }
        if (importer.fontSize != pointSize)
        {
            importer.fontSize = pointSize;
            dirty = true;
        }
        if (dirty) importer.SaveAndReimport();
    }

    // The IMGUI panels (PixelGUI) take a plain Font, not a TMP asset, and Unity rasterises those
    // dynamically at runtime. Left on the default smooth rendering, the same typeface that looks crisp in
    // TextMeshPro comes out anti-aliased and blurry there. Hinted Raster gives hard edges with stems
    // snapped to the pixel grid, and the font size must be its native cell for that to line up.
    static void ConfigurePixelTtfImport(string path) => ConfigurePixelTtf(path, kPixelFontPointSize);

    // Puts the font's material on the bitmap shader and its atlas on point filtering. Split out so a
    // font asset built by an earlier version of this tool gets repaired in place.
    static void EnforceBitmapRendering(TMP_FontAsset fontAsset)
    {
        var bitmap = Shader.Find("TextMeshPro/Bitmap")
                  ?? Shader.Find("TextMeshPro/Mobile/Bitmap");

        if (fontAsset.material != null)
        {
            if (bitmap != null)
            {
                if (fontAsset.material.shader != bitmap) fontAsset.material.shader = bitmap;
                EditorUtility.SetDirty(fontAsset.material);
            }
            else Debug.LogWarning("[PixelUIKitSetup] TextMeshPro/Bitmap shader not found — the pixel font " +
                                  "will render through the distance-field shader and look fringed.");
        }

        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.filterMode = FilterMode.Point;
            EditorUtility.SetDirty(fontAsset.atlasTexture);
        }

        // TMP caches a material reference per font asset; make sure the change sticks on reload.
        EditorUtility.SetDirty(fontAsset);
    }

    static void BuildTheme(TMP_FontAsset body)
    {
        Directory.CreateDirectory("Assets/Resources/UI");
        var theme = AssetDatabase.LoadAssetAtPath<PixelUITheme>(kThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<PixelUITheme>();
            AssetDatabase.CreateAsset(theme, kThemePath);
        }

        Sprite S(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{kArt}/{file}.png");
        Sprite I(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{kIcons}/{file}.png");

        theme.window = S("window");
        theme.windowPlain = S("window-plain");
        theme.button = S("button");
        theme.buttonHover = S("button-hover");
        theme.buttonPressed = S("button-press");
        theme.buttonDanger = S("button-danger");
        theme.buttonConfirm = S("button-confirm");
        theme.barTrack = S("bar-track");
        theme.barGold = S("bar-gold");
        theme.barRed = S("bar-red");
        theme.barTeal = S("bar-teal");
        theme.cursor = S("cursor");
        theme.dialogueTail = S("dialogue-tail");

        theme.iconMoney = I("money");
        theme.iconPart = I("part");
        theme.iconFuel = I("fuel");
        theme.iconTrophy = I("trophy");
        theme.iconStar = I("star");
        theme.iconQuest = I("quest");
        theme.iconMap = I("map");
        theme.iconSpeech = I("speech");
        theme.iconClock = I("clock");
        theme.iconFlag = I("flag");
        theme.iconTyre = I("tyre");
        theme.iconHeart = I("heart");
        theme.iconWrenchSet = I("wrench-set");
        theme.iconWarning = I("warning");

        if (body != null) theme.body = body;
        if (theme.imguiFont == null)
            theme.imguiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/fixedsys.ttf");
        ConfigurePixelTtfImport("Assets/Fonts/fixedsys.ttf");
        if (theme.display == null)
        {
            // "mania SDF.asset" is a UI-Toolkit UnityEngine.TextCore.Text.FontAsset, which TextMeshPro
            // components cannot use. "mania SDF 1.asset" is the TMP build of the same typeface.
            theme.display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/mania SDF 1.asset")
                         ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Mania-Dialogue.asset");
        }

        EditorUtility.SetDirty(theme);

        var missing = new List<string>();
        if (theme.window == null) missing.Add("window");
        if (theme.body == null) missing.Add("body font");
        if (theme.iconMoney == null) missing.Add("icons");
        if (missing.Count > 0)
            Debug.LogWarning("[PixelUIKitSetup] theme is missing: " + string.Join(", ", missing));
    }
}
#endif
