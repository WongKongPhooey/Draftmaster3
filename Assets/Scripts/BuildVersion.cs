using UnityEngine;

// The version a player sees. Patch And Run leaves Application.version alone (a new version name changes the APK and
// pram re-pushes every data file), so PhoneDeploy writes the counted version into Resources/BuildVersion.txt, which
// a patch carries like any other data. A build without the file falls back to the Player Settings version.
public static class BuildVersion
{
    static string cached;

    public static string Display
    {
        get
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("BuildVersion");
            string v = asset != null ? asset.text.Trim() : "";
            cached = string.IsNullOrEmpty(v) ? Application.version : v;
            return cached;
        }
    }
}
