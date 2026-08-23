using UnityEngine;
using UnityEngine.SceneManagement;

// Round trip into the garage sheet and back out to wherever it was opened from.
//
// The garage used to be a row on the title menu, which put the player's car sheet somewhere the player
// never is. It is now a thing in the world instead: the laptop in the RV and the one in the team
// factory (LaptopInteractable) open it, and BACK returns to the scene that laptop was sitting in.
//
// Modelled on LandmarkLoader: the scene we came from is remembered in PlayerPrefs rather than a static,
// so a domain reload or a quit halfway through the sheet can't strand the player in a screen with no way
// out. With nothing remembered — the garage opened cold, or the pref cleared — BACK falls through to the
// title screen.
//
// This is a scene swap, not an overlay, because the garage screen owns its own camera and clears the
// frame: laid over a race scene it would fight that scene's cameras and its OnGUI panels. The cost is
// that returning to a race scene rebuilds it, so a session in progress restarts — the laptops are things
// you use between sessions, standing in the RV or the factory, not mid-run.
public static class GarageScreenLoader
{
    public const string SceneName = "GarageScreen";
    public const string TitleSceneName = "TitleScreen";

    const string ReturnKey = "garage.return";

    public static bool SceneInBuild => Application.CanStreamedLevelBeLoaded(SceneName);

    // The scene the garage was opened from, or "" when it was opened cold.
    public static string ReturnScene => PlayerPrefs.GetString(ReturnKey, "");

    // Open the sheet, remembering where we were standing. False = the scene isn't in the build settings,
    // in which case nothing happens and the caller can say so.
    public static bool Open()
    {
        if (!SceneInBuild)
        {
            Debug.LogError($"GarageScreenLoader: '{SceneName}' is not in the Build Settings — the laptop has nothing to open.");
            return false;
        }

        PlayerPrefs.SetString(ReturnKey, SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneName);
        return true;
    }

    // Where BACK goes: the remembered scene when it can still be loaded, otherwise the fallback (the
    // title). null = neither is in the build settings, so the caller should complain rather than load
    // nothing. Pure — call Leave() to actually go.
    public static string ResolveExit(string fallbackScene = TitleSceneName)
    {
        string back = ReturnScene;
        if (!string.IsNullOrEmpty(back) && Application.CanStreamedLevelBeLoaded(back)) return back;
        if (!string.IsNullOrEmpty(fallbackScene) && Application.CanStreamedLevelBeLoaded(fallbackScene)) return fallbackScene;
        return null;
    }

    // Leave the sheet. The trip is over either way, so the memory is cleared even when the exit fails —
    // a stale return scene would otherwise send the next cold visit somewhere it was never opened from.
    public static bool Leave(string fallbackScene = TitleSceneName)
    {
        string exit = ResolveExit(fallbackScene);
        Clear();
        if (string.IsNullOrEmpty(exit)) return false;
        SceneManager.LoadScene(exit);
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(ReturnKey);
        PlayerPrefs.Save();
    }
}
