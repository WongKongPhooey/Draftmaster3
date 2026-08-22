using UnityEditor;
using UnityEngine;

// Rebuilds VT323 from its TTF into a temp asset, so the rebuilt atlas can be compared against the
// current one before anything in the project is pointed at it. Writing to a scratch path rather than
// over the real asset keeps a bad rebuild from becoming the new broken state.
public static class VT323Rebuild
{
    public const string TempPath = "Assets/Resources/Fonts/_VT323 Rebuild.asset";
    const string Ttf = "Assets/Fonts/VT323-Regular.ttf";
    const int PointSize = 16;   // kVt323Size — the face's documented ladder base

    [MenuItem("Draftmaster/Art/Rebuild VT323 Into Temp Asset")]
    public static void Run()
    {
        PixelUIKitSetup.ConfigurePixelTtf(Ttf, PointSize);
        var asset = PixelUIKitSetup.BuildBitmapFont(Ttf, TempPath, "_VT323 Rebuild", PointSize);
        Debug.Log(asset != null
            ? $"VT323Rebuild: wrote {TempPath}"
            : $"VT323Rebuild: FAILED to build from {Ttf}");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Draftmaster/Art/Delete VT323 Temp Asset")]
    public static void Cleanup()
    {
        if (AssetDatabase.DeleteAsset(TempPath)) Debug.Log($"VT323Rebuild: deleted {TempPath}");
    }
}
