using UnityEngine;
using TMPro;

// The one place the game's UI look is defined: palette, frames, icons, fonts and metrics.
//
// The UI is authored in "UI pixels" on a 640x360 canvas, which scales by exactly 3 to 1080p and 6 to
// 4K. Integer scaling is the whole point -- a UI pixel then covers a whole number of screen pixels and
// the interface stays as crisp as the world art, instead of the half-pixel shimmer you get from a
// canvas that scales by 2.37.
//
// Direction: JRPG window furniture (deep blue plate, gold rule, selection cursor, portrait + name
// plate dialogue) carrying racing content. That suits a game whose demo is paddock conversations,
// travel between races and side quests rather than lap times.
[CreateAssetMenu(fileName = "PixelUITheme", menuName = "Draftmaster/Pixel UI Theme")]
public class PixelUITheme : ScriptableObject
{
    // Canvas the UI is authored against. 640x360 is 16:9 and divides 1080p and 4K exactly.
    public const int ReferenceWidth = 640;
    public const int ReferenceHeight = 360;
    // Image native size = spriteRect / spritePPU * referencePixelsPerUnit, so keeping both at 100
    // makes one source pixel equal one UI pixel with no arithmetic anywhere else.
    public const float ReferencePixelsPerUnit = 100f;

    [Header("Palette — screen")]
    [Tooltip("The void behind everything — letterbox bars, the space around a menu. Iron Oval #0A0B10.")]
    public Color screenBase = new Color32(0x0a, 0x0b, 0x10, 0xff);

    [Header("Palette — plate")]
    public Color ink = new Color32(0x0b, 0x0d, 0x17, 0xff);
    public Color plateDeep = new Color32(0x10, 0x18, 0x2c, 0xff);
    public Color plate = new Color32(0x18, 0x24, 0x42, 0xff);
    public Color plateLight = new Color32(0x22, 0x31, 0x57, 0xff);

    [Header("Palette — rule and text")]
    public Color gold = new Color32(0xf2, 0xc1, 0x4e, 0xff);
    public Color goldShade = new Color32(0xa8, 0x7d, 0x28, 0xff);
    public Color text = new Color32(0xf4, 0xf1, 0xe8, 0xff);
    public Color textDim = new Color32(0x9a, 0xa3, 0xb8, 0xff);
    public Color textDisabled = new Color32(0x6b, 0x72, 0x8c, 0xff);

    [Header("Palette — status")]
    public Color danger = new Color32(0xe5, 0x48, 0x4d, 0xff);
    public Color confirm = new Color32(0x4e, 0xc9, 0xb0, 0xff);
    public Color caution = new Color32(0xe8, 0x8b, 0x2f, 0xff);
    public Color info = new Color32(0x4a, 0x8f, 0xe0, 0xff);

    [Header("Frames (9-sliced)")]
    public Sprite window;
    public Sprite windowPlain;
    public Sprite button;
    public Sprite buttonHover;
    public Sprite buttonPressed;
    public Sprite buttonDanger;
    public Sprite buttonConfirm;
    public Sprite barTrack;
    public Sprite barGold;
    public Sprite barRed;
    public Sprite barTeal;

    [Header("Furniture")]
    public Sprite cursor;
    public Sprite dialogueTail;

    // ---- Iron Oval texture set ---------------------------------------------------------------------
    // The handoff sheet's /textures, authored at 1x. Tiles are Repeat-wrapped and meant to be drawn with
    // Image.Type = Tiled; the frames are 9-sliced. See Docs/IronOvalKit.md.
    [Header("Iron Oval — frames")]
    [Tooltip("Default window frame: dark outline, cream edge, inner shade. 12x12, border 4.")]
    public Sprite frameCream;
    [Tooltip("Focus state only — the window the cursor is in. Swap the frame rather than tinting text.")]
    public Sprite frameGold;
    [Tooltip("Confirm button, highlight and shade baked in. 12x12, border 3. Pressed = offset content 2px down.")]
    public Sprite buttonRed;

    [Header("Iron Oval — tiles")]
    [Tooltip("Window interior dither. Draw Tiled, not Stretched.")]
    public Sprite panelFill;
    [Tooltip("Screen base and modal scrim (drop to 85% alpha over gameplay).")]
    public Sprite panelFillDeep;
    [Tooltip("Placeholder hatch for portraits and art that isn't drawn yet — reads as pending, not broken.")]
    public Sprite hatchSlot;
    [Tooltip("TV line overlay. Full-screen Tiled Image on top of everything, Raycast Target off.")]
    public Sprite scanline;
    [Tooltip("UI-only kerb band for menu backdrops and results headers — not the track.")]
    public Sprite kerbStripe;
    [Tooltip("UI-only asphalt noise for menu backdrops. Quiet enough to sit under text.")]
    public Sprite asphaltNoise;

    [Header("Iron Oval — cells")]
    [Tooltip("Filled 8x10 cell, left half of the stat-cell sheet. Repeat it for a segmented bar.")]
    public Sprite statCellFilled;
    [Tooltip("Empty 8x10 cell, right half of the stat-cell sheet.")]
    public Sprite statCellEmpty;
    [Tooltip("Gold selection cursor, 6x8. Blink 0.45s on / 0.45s off and step it by whole rows.")]
    public Sprite cursorArrow;

    [Header("Icons (16x16)")]
    public Sprite iconMoney, iconPart, iconFuel, iconTrophy, iconStar, iconQuest,
                  iconMap, iconSpeech, iconClock, iconFlag, iconTyre, iconHeart,
                  iconWrenchSet, iconWarning;

    [Header("Fonts")]
    [Tooltip("Bitmap pixel font for body copy, dialogue and data. Rendered RASTER (not SDF) so glyphs " +
             "land on the pixel grid instead of being anti-aliased off it.")]
    public TMP_FontAsset body;
    [Tooltip("Display face for headings and big numbers — the racing brand voice against the pixel body.")]
    public TMP_FontAsset display;
    [Tooltip("Fixed-advance face for dense columns: running order, timing, tyre readouts. Its constant " +
             "advance width keeps columns aligned with no tab work. Iron Oval: VT323.")]
    public TMP_FontAsset data;
    [Tooltip("The same pixel typeface as a plain Font, for the IMGUI panels (dialogue choices, debug " +
             "overlays) that cannot take a TMP asset. Iron Oval: VT323, the data face — most IMGUI in " +
             "this project is a readout. Use whole multiples of its 16pt cell.")]
    public Font imguiFont;
    [Tooltip("IMGUI counterpart of `display` — Silkscreen, for headings, labels and buttons. Whole " +
             "multiples of 8pt only; a bitmap face resampled to 12 or 20pt loses its stems.")]
    public Font imguiDisplayFont;
    [Tooltip("IMGUI counterpart of `body` — Pixelify Sans, for prose (dialogue, quest copy). Whole " +
             "multiples of its 20pt design grid.")]
    public Font imguiBodyFont;

    [Header("Metrics (UI pixels)")]
    [Tooltip("Body copy size. A multiple of the font's native pixel height keeps glyphs sharp.")]
    public int bodySize = 16;
    public int smallSize = 8;
    public int headingSize = 24;
    [Tooltip("Padding inside a window, in UI pixels. Matches the 6px frame border plus breathing room.")]
    public int panelPadding = 10;
    [Tooltip("Gap between stacked rows.")]
    public int rowGap = 6;
    [Tooltip("Whole-number zoom for the IMGUI panels (dialogue choices, debug overlays), which draw in raw " +
             "screen pixels rather than on the scaled canvas. 0 = derive it from the screen height " +
             "(2x at 1080p). Raise it if the panels read small on your display.")]
    public int imguiScaleOverride = 0;

    // ---- access -----------------------------------------------------------------------------------
    static PixelUITheme _instance;

    // Loaded from Resources so runtime-built panels (which have no inspector to wire) can reach it.
    public static PixelUITheme Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<PixelUITheme>("UI/PixelUITheme");
                if (_instance != null) _instance.EnsureCrispAtlas();
            }
            return _instance;
        }
    }

    // The body font uses a dynamic atlas, which TextMeshPro rebuilds at runtime whenever it meets a
    // glyph it has not rasterised yet -- and the rebuilt texture comes back on the default bilinear
    // filter. Bilinear on a bitmap font is exactly the mush this whole setup exists to avoid, so the
    // filter is re-asserted here rather than only at import time.
    public void EnsureCrispAtlas()
    {
        if (body != null && body.atlasTexture != null)
            body.atlasTexture.filterMode = FilterMode.Point;
    }

    public Sprite Icon(string key)
    {
        switch (key.ToLowerInvariant())
        {
            case "money": return iconMoney;
            case "part": return iconPart;
            case "fuel": return iconFuel;
            case "trophy": return iconTrophy;
            case "star": return iconStar;
            case "quest": return iconQuest;
            case "map": return iconMap;
            case "speech": return iconSpeech;
            case "clock": return iconClock;
            case "flag": return iconFlag;
            case "tyre": return iconTyre;
            case "heart": return iconHeart;
            case "wrench-set": return iconWrenchSet;
            case "warning": return iconWarning;
            default: return null;
        }
    }
}
