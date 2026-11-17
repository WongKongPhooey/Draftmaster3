#if UNITY_EDITOR
using TMPro;
using UnityEditor;

// The one face the race-flow design handoff (Designs/draftmaster3_race_flow_unity_handoff) adds to the kit:
// Pixelify Sans BOLD at 30 px for the series codes on SINGLE RACE's Series Select (CUP, TRK...). The kit's
// "Pixelify Sans Pixel" is the variable font's regular weight at its 20 px grid, which is neither.
//
// Built by the kit's own recipe (hinted raster, padding 1, bitmap shader, point atlas) and then baked static with
// the other pixel faces, so the atlas never drifts. SingleRaceUI falls back to the 20 px face until this has run.
public static class RaceFlowFonts
{
    public const string BoldPath = "Assets/Resources/Fonts/Pixelify Bold Pixel.asset";
    public const int BoldSize = 30;

    [MenuItem("Draftmaster/UI/Build Race Flow Fonts")]
    public static void Build()
    {
        var font = PixelUIKitSetup.BuildBitmapFont("Assets/Fonts/PixelifySans-Bold.ttf", BoldPath,
                                                   "Pixelify Bold Pixel", BoldSize);
        if (font == null) return;
        PixelFontBake.Run();
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log($"[RaceFlowFonts] {BoldPath}: {font.glyphTable.Count} glyphs at {BoldSize} px, " +
                              $"{font.atlasRenderMode}, {font.atlasPopulationMode}.");
    }
}
#endif
