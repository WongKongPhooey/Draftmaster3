using UnityEngine;

// Which build this is: the free demo, or the full release.
//
// The flag is a COMPILE-TIME define (DRAFTMASTER_DEMO) rather than a setting, so a demo build cannot be
// talked into being the full game by editing a save file — Draftmaster > Demo > Build Is Demo toggles the
// define on the active build target.
//
// On top of that, the editor and development builds honour a PlayerPrefs override ("game.demo") so the
// demo menu can be looked at without recompiling. Release builds ignore it entirely.
//
// What it changes today: which rows the title menu draws (TitleScreenUI.Row.appearsIn). Anything else the
// demo needs to cut short — a lap limit, a locked calendar — reads the same flag.
public static class DemoMode
{
    // Editor / development-build override. Missing or negative = follow the build.
    public const string OverrideKey = "game.demo";

#if DRAFTMASTER_DEMO
    public const bool BuildIsDemo = true;
#else
    public const bool BuildIsDemo = false;
#endif

    public static bool IsDemo
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int over = PlayerPrefs.GetInt(OverrideKey, -1);
            if (over >= 0) return over == 1;
#endif
            return BuildIsDemo;
        }
    }

    // True when the flag is being forced from PlayerPrefs rather than read off the build.
    public static bool IsOverridden => PlayerPrefs.GetInt(OverrideKey, -1) >= 0;

    // null clears the override and goes back to whatever the build was compiled as.
    public static void SetOverride(bool? demo)
    {
        if (demo == null) PlayerPrefs.DeleteKey(OverrideKey);
        else PlayerPrefs.SetInt(OverrideKey, demo.Value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
