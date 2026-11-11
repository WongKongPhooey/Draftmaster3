using UnityEngine;

// The laptop's DriveScout sheet, coloured exactly as the Draftmaster3_UI_Unity design handoff draws it
// (Layout/DriveScout_PC.json, 2026-11-11). Kept apart from PixelUITheme on purpose: the theme's kit colours
// are close to these but not the same, and the screen is meant to match the design rather than the kit.
// The builder lays the sheet out in these; GarageScreenUI recolours rows at runtime from the same values,
// so a stat, trait or standing can never come out in a colour the design doesn't use.
public static class DriveScoutStyle
{
    static Color Hex(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

    public static readonly Color Background = Hex(0x0d0f16ff);   // behind everything
    public static readonly Color HeaderBand = Hex(0x1b2030ff);
    public static readonly Color HeaderRule = Hex(0x05060aff);   // 3px under the band; the close box's frame
    public static readonly Color PickerPlate = Hex(0x12141cff);
    public static readonly Color Edge = Hex(0x2c3145ff);         // picker frames, rules, empty stat cells, close box
    public static readonly Color Panel = Hex(0x182136ff);
    public static readonly Color PanelFrame = Hex(0xf4ead7ff);   // the 3px cream border round each panel

    public static readonly Color Text = Hex(0xf4ead7ff);         // values, names, traits
    public static readonly Color Label = Hex(0xb9ae9aff);        // row labels
    public static readonly Color Meta = Hex(0x8b93adff);         // "v3.1", "No starts yet", N/A, unknown stats
    public static readonly Color Gold = Hex(0xe8b13cff);         // headings, filled cells, standout values
    public static readonly Color Good = Hex(0x6fa85aff);
    public static readonly Color Bad = Hex(0xe0604aff);
    public static readonly Color KeyShadow = Hex(0x8b93adff);    // under the ESC keycap

    // A 0-20 skill this high is drawn in gold rather than cream: on the design's sheet every 18 and 20 is
    // gold and every 17 and under is not.
    public const int StandoutSkill = 18;
}
