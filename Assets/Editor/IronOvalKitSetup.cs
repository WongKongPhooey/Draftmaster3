#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// Implements the "Iron Oval — UI kit" handoff sheet: imports /textures with the settings it specifies,
// builds the three named typefaces as bitmap TMP assets, and writes the palette + art into the shared
// PixelUITheme so the whole UI picks the direction up at once (PixelUI, PixelGUI and BrandFonts all read
// that one asset — see Docs/PixelUIKit.md).
//
//   Draftmaster > Art > Set Up Iron Oval Kit     applies everything
//   Draftmaster > Art > Verify Iron Oval Kit     writes Docs/IronOvalKit.md
//
// Two deliberate departures from the sheet, both to match machinery this project already has:
//  * Pixels Per Unit is PixelUITheme.ReferencePixelsPerUnit (100), not 1. The sheet's PPU 1 + Constant
//    Pixel Size canvas and this project's PPU 100 + 640x360 ScaleWithScreenSize canvas both land one
//    source pixel on one UI pixel at integer scale; mixing the two in one canvas would not.
//  * One dynamic atlas per typeface at its native point size, not one atlas per shipped size. Same
//    trade the fixedsys body font already makes. Sizes that are whole multiples stay crisp.
public static class IronOvalKitSetup
{
    const string kArt = "Assets/UI/IronOval";
    const string kFontDir = "Assets/Resources/Fonts";
    const string kThemePath = "Assets/Resources/UI/PixelUITheme.asset";

    // Texture -> 9-slice border in source pixels. Everything else imports unsliced.
    static readonly Dictionary<string, int> kBorders = new Dictionary<string, int>
    {
        { "frame-cream_9slice", 4 },
        { "frame-gold_9slice", 4 },
        { "button-red_9slice", 3 },
    };

    // Tiles are drawn with Image.Type = Tiled and must wrap; the cursor, frames and cell sheet clamp so
    // their edge texels never bleed in from the opposite side.
    static readonly HashSet<string> kRepeatTiles = new HashSet<string>
    {
        "scanline_1x3", "panel-fill_8x8", "panel-fill-deep_8x8",
        "hatch-slot_8x8", "kerb-stripe_8x16", "asphalt-noise_16x16",
    };

    // The stat-cell sheet holds the filled and empty 8x10 cell side by side in one 16x10 texture.
    const string kStatCell = "stat-cell_16x10";
    const string kStatFilled = "statcell-filled";
    const string kStatEmpty = "statcell-empty";

    // ---- palette (section 01 of the sheet) ---------------------------------------------------------
    static readonly Color32 cScreen = new Color32(0x0a, 0x0b, 0x10, 0xff); // behind everything
    static readonly Color32 cOutline = new Color32(0x05, 0x06, 0x0a, 0xff); // every panel edge, 1px
    static readonly Color32 cBase = new Color32(0x12, 0x14, 0x1c, 0xff); // menus, letterbox
    static readonly Color32 cPanel = new Color32(0x18, 0x21, 0x36, 0xff); // windows, list rows
    static readonly Color32 cShade = new Color32(0x2c, 0x31, 0x45, 0xff); // troughs, dividers
    static readonly Color32 cDisabled = new Color32(0x6d, 0x75, 0x90, 0xff);
    static readonly Color32 cSecondary = new Color32(0xb9, 0xae, 0x9a, 0xff);
    static readonly Color32 cPrimary = new Color32(0xf4, 0xea, 0xd7, 0xff);
    static readonly Color32 cAccent = new Color32(0xe8, 0xb1, 0x3c, 0xff); // the only accent
    static readonly Color32 cAlarm = new Color32(0xc4, 0x45, 0x2f, 0xff); // never decorative
    static readonly Color32 cTelemetry = new Color32(0x5a, 0x9a, 0xd6, 0xff);
    static readonly Color32 cGain = new Color32(0x6f, 0xa8, 0x5a, 0xff);

    // ---- type (section 02) -------------------------------------------------------------------------
    // Point size each atlas is rasterised at, then used at whole multiples of — a bitmap atlas scales up
    // cleanly and not down.
    //
    // These are measured, not guessed. Rasterising each face across 8..40px and counting pixels that come
    // out at intermediate alpha (the tell that a glyph edge landed between pixels) gives:
    //
    //   Pixelify Sans   16px = 53% intermediate,  20px = 0.6%.  Its design grid is 20px/em, so 16 lands
    //                   every glyph off-grid — that is what made body copy render as overlapping mush.
    //                   Ladder: 20 / 40 / 60. The sheet's 12, 16 and 32 are off-grid for this face.
    //   Silkscreen      clean at 8 under hinted raster; 8 is also the sheet's smallest size. Ladder 8/16/24.
    //   VT323           dropped. A CRT face with curves rather than strict pixel art, its advances collide
    //                   at every size on this pipeline — rebuilding it from the TTF reproduced the same
    //                   overlapping glyphs, so it was removed rather than kept as a trap.
    //   Fixedsys        16px cell, fixed advance, true lowercase. Takes prose and every column readout.
    //                   Ladder 16 / 32 / 48.
    const int kSilkscreenSize = 8;
    const int kPixelifySize = 20;
    const int kFixedsysSize = 16;

    struct FaceSpec
    {
        public string ttf, outPath, niceName;
        public int pointSize;
    }

    static readonly FaceSpec[] kFaces =
    {
        new FaceSpec { ttf = "Assets/Fonts/Silkscreen-Regular.ttf",
                       outPath = kFontDir + "/Silkscreen Pixel.asset",
                       niceName = "Silkscreen Pixel", pointSize = kSilkscreenSize },
        new FaceSpec { ttf = "Assets/Fonts/PixelifySans-Variable.ttf",
                       outPath = kFontDir + "/Pixelify Sans Pixel.asset",
                       niceName = "Pixelify Sans Pixel", pointSize = kPixelifySize },
        new FaceSpec { ttf = "Assets/Fonts/fixedsys.ttf",
                       outPath = kFontDir + "/Fixedsys Pixel.asset",
                       niceName = "Fixedsys Pixel", pointSize = kFixedsysSize },
    };

    [MenuItem("Draftmaster/Art/Set Up Iron Oval Kit", priority = 122)]
    public static void Run()
    {
        ImportTextures();
        var faces = BuildFaces();
        ApplyToTheme(faces);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[IronOvalKitSetup] Iron Oval kit applied to {kThemePath}. " +
                  "Run Draftmaster/Art/Verify Iron Oval Kit for the report.");
    }

    // ---- textures (section 03 + the import block in section 05) ------------------------------------
    static void ImportTextures()
    {
        if (!Directory.Exists(kArt))
        {
            Debug.LogWarning($"[IronOvalKitSetup] {kArt} not found — nothing to import.");
            return;
        }

        foreach (var path in Directory.GetFiles(kArt, "*.png", SearchOption.AllDirectories)
                                      .Select(p => p.Replace('\\', '/')))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            string name = Path.GetFileNameWithoutExtension(path);

            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            // PixelGUI point-upscales these at runtime for the IMGUI panels, which needs GetPixels. They
            // are 16x16 at most, so the readable copy costs nothing worth measuring.
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = PixelUITheme.ReferencePixelsPerUnit;
            importer.wrapMode = kRepeatTiles.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            if (name == kStatCell) SliceStatCells(importer);
            else
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = kBorders.TryGetValue(name, out int b)
                    ? new Vector4(b, b, b, b)
                    : Vector4.zero;
            }

            // Mesh Type lives on TextureImporterSettings rather than the importer. Full Rect: a tight mesh
            // would trim the transparent margin off the cursor and the frame corners, moving them.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }

    // The sheet says "slice at x=8": two 8x10 cells in one 16x10 sheet. That needs Multiple, and a
    // Multiple sprite is a sub-asset — LoadAssetAtPath<Sprite> returns null for it, so the theme wiring
    // below goes through LoadAllAssetsAtPath instead.
    static void SliceStatCells(TextureImporter importer)
    {
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spriteBorder = Vector4.zero;

        var pivot = new Vector2(0.5f, 0.5f);
        importer.spritesheet = new[]
        {
            new SpriteMetaData { name = kStatFilled, rect = new Rect(0, 0, 8, 10), pivot = pivot, alignment = (int)SpriteAlignment.Center },
            new SpriteMetaData { name = kStatEmpty,  rect = new Rect(8, 0, 8, 10), pivot = pivot, alignment = (int)SpriteAlignment.Center },
        };
    }

    // ---- fonts (section 02) ------------------------------------------------------------------------
    static Dictionary<string, TMP_FontAsset> BuildFaces()
    {
        var built = new Dictionary<string, TMP_FontAsset>();
        foreach (var f in kFaces)
        {
            // The plain-Font path (IMGUI) needs hinted raster at the same cell, or the same typeface that
            // is crisp in TextMeshPro comes out anti-aliased there.
            PixelUIKitSetup.ConfigurePixelTtf(f.ttf, f.pointSize);
            var asset = PixelUIKitSetup.BuildBitmapFont(f.ttf, f.outPath, f.niceName, f.pointSize);
            if (asset != null) built[f.niceName] = asset;
        }
        return built;
    }

    // ---- theme -------------------------------------------------------------------------------------
    static void ApplyToTheme(Dictionary<string, TMP_FontAsset> faces)
    {
        Directory.CreateDirectory("Assets/Resources/UI");
        var theme = AssetDatabase.LoadAssetAtPath<PixelUITheme>(kThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<PixelUITheme>();
            AssetDatabase.CreateAsset(theme, kThemePath);
        }

        // Palette. The existing slot names carry the Iron Oval roles: plate* are the neutral stack,
        // gold is the one accent, danger/info/confirm are alarm/telemetry/gain.
        theme.screenBase = cScreen;
        theme.ink = cOutline;
        theme.plateDeep = cBase;
        theme.plate = cPanel;
        theme.plateLight = cShade;
        theme.gold = cAccent;
        theme.goldShade = new Color32(0xa8, 0x7d, 0x28, 0xff);
        theme.text = cPrimary;
        theme.textDim = cSecondary;
        theme.textDisabled = cDisabled;
        theme.danger = cAlarm;
        theme.info = cTelemetry;
        theme.confirm = cGain;
        theme.caution = cAccent;

        // Art.
        theme.frameCream = Single("frame-cream_9slice");
        theme.frameGold = Single("frame-gold_9slice");
        theme.buttonRed = Single("button-red_9slice");
        theme.panelFill = Single("panel-fill_8x8");
        theme.panelFillDeep = Single("panel-fill-deep_8x8");
        theme.hatchSlot = Single("hatch-slot_8x8");
        theme.scanline = Single("scanline_1x3");
        theme.kerbStripe = Single("kerb-stripe_8x16");
        theme.asphaltNoise = Single("asphalt-noise_16x16");
        theme.cursorArrow = Single("cursor-arrow_6x8");
        theme.statCellFilled = Sub(kStatCell, kStatFilled);
        theme.statCellEmpty = Sub(kStatCell, kStatEmpty);

        // Point the generic slots the rest of the kit already draws through at the Iron Oval art, so
        // existing panels adopt the direction without per-file edits.
        if (theme.frameCream != null) theme.window = theme.frameCream;
        if (theme.frameCream != null) theme.windowPlain = theme.frameCream;
        if (theme.buttonRed != null) { theme.button = theme.buttonRed; theme.buttonDanger = theme.buttonRed; }
        if (theme.cursorArrow != null) theme.cursor = theme.cursorArrow;

        // Type roles: Silkscreen headers/labels, Fixedsys for prose and dense data alike (and the IMGUI
        // panels, which are almost all data readouts and take a plain Font rather than a TMP asset).
        // Fixedsys covers both because Silkscreen has no lowercase and no fixed advance.
        if (faces.TryGetValue("Silkscreen Pixel", out var silkscreen)) theme.display = silkscreen;
        if (faces.TryGetValue("Fixedsys Pixel", out var fixedsys)) { theme.body = fixedsys; theme.data = fixedsys; }
        // The IMGUI panels take plain Fonts rather than TMP assets, and they carry the same roles:
        // Fixedsys for the readouts and prose that most of them are, Silkscreen for headings and buttons.
        // PixelGUI sizes each at whole multiples of its cell.
        var fixedTtf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/fixedsys.ttf");
        if (fixedTtf != null) { theme.imguiFont = fixedTtf; theme.imguiBodyFont = fixedTtf; }
        var silkTtf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Silkscreen-Regular.ttf");
        if (silkTtf != null) theme.imguiDisplayFont = silkTtf;

        // Metrics from the sheet: Silkscreen 8/11/16/24, body 16, and the 12px margin / 8px gutter.
        theme.bodySize = 16;
        theme.smallSize = 8;
        theme.headingSize = 24;
        theme.panelPadding = 12;
        theme.rowGap = 8;

        EditorUtility.SetDirty(theme);
    }

    static Sprite Single(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{kArt}/{file}.png");

    static Sprite Sub(string file, string spriteName)
        => AssetDatabase.LoadAllAssetsAtPath($"{kArt}/{file}.png")
                        .OfType<Sprite>()
                        .FirstOrDefault(s => s.name == spriteName);

    // ---- verification ------------------------------------------------------------------------------
    [MenuItem("Draftmaster/Art/Verify Iron Oval Kit", priority = 123)]
    public static void Verify()
    {
        var theme = AssetDatabase.LoadAssetAtPath<PixelUITheme>(kThemePath);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Iron Oval UI kit");
        sb.AppendLine();
        sb.AppendLine("Generated by `Draftmaster > Art > Verify Iron Oval Kit`. Source: the " +
                      "*Iron Oval — UI kit* handoff sheet (Claude Design).");
        sb.AppendLine();

        if (theme == null)
        {
            sb.AppendLine($"**Theme missing** at `{kThemePath}` — run Draftmaster/Art/Set Up Iron Oval Kit.");
            Write(sb);
            return;
        }

        sb.AppendLine("## Textures");
        sb.AppendLine();
        sb.AppendLine("| file | size | wrap | filter | compression | 9-slice |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var path in Directory.GetFiles(kArt, "*.png", SearchOption.AllDirectories)
                                      .Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (imp == null || tex == null) continue;
            var border = imp.spriteBorder;
            string slice = imp.spriteImportMode == SpriteImportMode.Multiple
                ? $"{imp.spritesheet.Length} sprites"
                : (border == Vector4.zero ? "—" : $"border {border.x:0}");
            sb.AppendLine($"| `{Path.GetFileName(path)}` | {tex.width}x{tex.height} | {imp.wrapMode} | " +
                          $"{imp.filterMode} | {imp.textureCompression} | {slice} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Pixels Per Unit {PixelUITheme.ReferencePixelsPerUnit:0} on every sprite, matching the " +
                      $"canvas's referencePixelsPerUnit so one source pixel is one UI pixel. Mesh type Full Rect, " +
                      "mip maps off, max size 2048.");
        sb.AppendLine();

        sb.AppendLine("## Theme slots");
        sb.AppendLine();
        sb.AppendLine("| slot | asset | notes |");
        sb.AppendLine("|---|---|---|");
        void SpriteRow(string slot, Sprite s)
        {
            string note = s == null ? "" :
                $"{s.rect.width:0}x{s.rect.height:0}px" +
                (s.border == Vector4.zero ? "" : $", 9-slice {s.border.x:0}");
            sb.AppendLine($"| {slot} | {(s == null ? "**MISSING**" : s.name)} | {note} |");
        }
        SpriteRow("frameCream", theme.frameCream);
        SpriteRow("frameGold", theme.frameGold);
        SpriteRow("buttonRed", theme.buttonRed);
        SpriteRow("panelFill", theme.panelFill);
        SpriteRow("panelFillDeep", theme.panelFillDeep);
        SpriteRow("hatchSlot", theme.hatchSlot);
        SpriteRow("scanline", theme.scanline);
        SpriteRow("kerbStripe", theme.kerbStripe);
        SpriteRow("asphaltNoise", theme.asphaltNoise);
        SpriteRow("statCellFilled", theme.statCellFilled);
        SpriteRow("statCellEmpty", theme.statCellEmpty);
        SpriteRow("cursorArrow", theme.cursorArrow);
        sb.AppendLine();

        sb.AppendLine("## Palette");
        sb.AppendLine();
        sb.AppendLine("| role | slot | hex |");
        sb.AppendLine("|---|---|---|");
        void ColorRow(string role, string slot, Color c)
            => sb.AppendLine($"| {role} | `{slot}` | #{ColorUtility.ToHtmlStringRGB(c)} |");
        ColorRow("Screen void", "screenBase", theme.screenBase);
        ColorRow("Outline / shadow", "ink", theme.ink);
        ColorRow("Screen base", "plateDeep", theme.plateDeep);
        ColorRow("Panel fill", "plate", theme.plate);
        ColorRow("Inner shade / empty", "plateLight", theme.plateLight);
        ColorRow("Disabled text", "textDisabled", theme.textDisabled);
        ColorRow("Secondary text", "textDim", theme.textDim);
        ColorRow("Primary text / frame", "text", theme.text);
        ColorRow("Accent (only one)", "gold", theme.gold);
        ColorRow("Alarm only", "danger", theme.danger);
        ColorRow("Telemetry", "info", theme.info);
        ColorRow("Gain", "confirm", theme.confirm);
        sb.AppendLine();
        sb.AppendLine("Accent budget: under 3% of lit pixels on any screen. Alarm red is never decorative.");
        sb.AppendLine();

        sb.AppendLine("## Type");
        sb.AppendLine();
        sb.AppendLine("| role | slot | asset | atlas |");
        sb.AppendLine("|---|---|---|---|");
        void FontRow(string role, string slot, TMP_FontAsset f)
        {
            string note = "";
            if (f != null)
            {
                string shader = f.material != null && f.material.shader != null ? f.material.shader.name : "no material";
                string filter = f.atlasTexture != null ? f.atlasTexture.filterMode.ToString() : "?";
                bool ok = f.atlasRenderMode == UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER_HINTED
                          && shader.Contains("Bitmap") && filter == "Point" && f.atlasPadding == 1;
                note = $"{f.atlasRenderMode}, padding {f.atlasPadding}, `{shader}`, {filter}" +
                       (ok ? " — OK" : " **<- expected RASTER_HINTED + padding 1 + Bitmap shader + Point**");
            }
            sb.AppendLine($"| {role} | `{slot}` | {(f == null ? "**MISSING**" : f.name)} | {note} |");
        }
        FontRow("Headers, labels, buttons (Silkscreen)", "display", theme.display);
        FontRow("Dialogue, names, prose (Fixedsys)", "body", theme.body);
        FontRow("Dense data columns (Fixedsys)", "data", theme.data);
        sb.AppendLine($"| IMGUI panels | `imguiFont` | {(theme.imguiFont == null ? "**MISSING**" : theme.imguiFont.name)} | plain Font, hinted raster |");
        sb.AppendLine();
        sb.AppendLine($"Each atlas is rasterised at the smallest size its face ships at — Silkscreen {kSilkscreenSize}, " +
                      $"Pixelify Sans {kPixelifySize}, Fixedsys {kFixedsysSize} — because a bitmap atlas scales up cleanly " +
                      "and not down. Usable ladders:");
        sb.AppendLine();
        sb.AppendLine("| face | crisp sizes | off-ladder sizes from the sheet |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| Silkscreen | {kSilkscreenSize}, {kSilkscreenSize * 2}, {kSilkscreenSize * 3} | 11 |");
        sb.AppendLine($"| Pixelify Sans | {kPixelifySize}, {kPixelifySize * 2}, {kPixelifySize * 3} | 12, 16, 32, 48 |");
        sb.AppendLine($"| Fixedsys | {kFixedsysSize}, {kFixedsysSize * 2}, {kFixedsysSize * 3} | 19, 22 |");
        sb.AppendLine();
        sb.AppendLine("Disable auto-size on every label and keep text positions on whole pixels. " +
                      "`IronOvalUI` only ever asks for on-ladder sizes.");
        sb.AppendLine();

        sb.AppendLine("## Canvas");
        sb.AppendLine();
        sb.AppendLine($"- Reference {PixelUITheme.ReferenceWidth}x{PixelUITheme.ReferenceHeight} UI px " +
                      $"(x3 = 1080p, x6 = 4K), referencePixelsPerUnit {PixelUITheme.ReferencePixelsPerUnit:0} " +
                      "— built by `PixelUI.CreateCanvas` / `PixelUI.ApplyScaler`.");
        sb.AppendLine($"- Margin {theme.panelPadding}px, gutter {theme.rowGap}px, body {theme.bodySize}px, " +
                      $"small {theme.smallSize}px, heading {theme.headingSize}px.");
        sb.AppendLine("- 9-slice Images: Type Sliced, Pixels Per Unit Multiplier 1, Fill Center on.");
        sb.AppendLine("- Tiles (panel fill, hatch, kerb, asphalt): Type Tiled, never Stretched.");
        sb.AppendLine("- Scanline overlay: `IronOvalScanlines` installs one full-screen Tiled Image at the top " +
                      "of the sort order with Raycast Target off.");
        sb.AppendLine("- No rotation, scaling or fade on UI sprites — animate in whole-pixel steps and toggle " +
                      "visibility instead.");
        sb.AppendLine();

        sb.AppendLine("## IMGUI");
        sb.AppendLine();
        sb.AppendLine("Most in-race UI in the spline scenes draws with OnGUI rather than a Canvas, so the kit " +
                      "has a second front end: `PixelGUI`. It point-upscales the kit art to the display's " +
                      "integer scale (`PixelGUI.Scale`) and offers the same vocabulary as `IronOvalUI` — " +
                      "`Panel`, `Scrim`, `Kerb`, `Cells`, `Bar`, `Button`, `Tab`, `Hatch`, plus the type " +
                      "roles `Heading` / `HeadingSmall` / `Data` / `Row` / `Body` / `Footer`.");
        sb.AppendLine();
        sb.AppendLine("| slot | asset | used for |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| `imguiFont` | {Name(theme.imguiFont)} | readouts and columns (16pt cell) |");
        sb.AppendLine($"| `imguiDisplayFont` | {Name(theme.imguiDisplayFont)} | headings, labels, buttons (8pt) |");
        sb.AppendLine($"| `imguiBodyFont` | {Name(theme.imguiBodyFont)} | prose (20pt) |");
        sb.AppendLine();
        sb.AppendLine("Kit textures import Read/Write enabled because that upscale runs through `GetPixels`. " +
                      "`PixelGUISkin` additionally restyles Unity's built-in skin from the theme, so a panel " +
                      "not yet moved onto `PixelGUI` still picks up the font, palette, plate and sliders.");

        Write(sb);
    }

    static string Name(Object asset) => asset == null ? "**missing**" : asset.name;

    static void Write(System.Text.StringBuilder sb)
    {
        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/IronOvalKit.md", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[IronOvalKitSetup] wrote Docs/IronOvalKit.md");
    }
}
#endif
