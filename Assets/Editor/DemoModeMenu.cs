using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Draftmaster > Demo — the two halves of the demo flag, plus the wipe it exists to drive.
//
//   Preview Demo Menu   flips DemoMode's PlayerPrefs override, so play mode draws the demo title menu
//                       without recompiling. Editor and development builds only.
//   Build Is Demo       adds/removes the DRAFTMASTER_DEMO define on the active build target. THIS is what
//                       ships: a built demo is a demo because it was compiled as one.
//   Wipe Career Save    what RESTART DEMO does, from the editor.
//
// Nothing here opens a modal that blocks the editor except the wipe's confirmation, which is the one place
// a click deserves a second thought.
public static class DemoModeMenu
{
    const string PreviewItem = "Draftmaster/Demo/Preview Demo Menu";
    const string BuildItem = "Draftmaster/Demo/Build Is Demo (DRAFTMASTER_DEMO)";
    const string WipeItem = "Draftmaster/Demo/Wipe Career Save";
    const string Define = "DRAFTMASTER_DEMO";

    [MenuItem(PreviewItem, priority = 400)]
    static void TogglePreview()
    {
        bool on = DemoMode.IsOverridden && DemoMode.IsDemo;
        // Off goes back to following the build rather than forcing "full", so the preview toggle never
        // masks what a DRAFTMASTER_DEMO build would actually do.
        DemoMode.SetOverride(on ? (bool?)null : true);
        Debug.Log(on
            ? "Demo preview off: the title menu follows the build again "
              + $"(DRAFTMASTER_DEMO is {(DemoMode.BuildIsDemo ? "on" : "off")})."
            : "Demo preview on: play mode draws the demo title menu.");
    }

    [MenuItem(PreviewItem, true)]
    static bool ValidatePreview()
    {
        Menu.SetChecked(PreviewItem, DemoMode.IsOverridden && DemoMode.IsDemo);
        return true;
    }

    [MenuItem(BuildItem, priority = 401)]
    static void ToggleBuildDefine()
    {
        var target = NamedBuildTarget.FromBuildTargetGroup(
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
        var list = new System.Collections.Generic.List<string>(defines);

        bool on = list.Contains(Define);
        if (on) list.Remove(Define); else list.Add(Define);
        PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());

        Debug.Log($"{Define} {(on ? "removed from" : "added to")} {target.TargetName} — builds for this "
                  + $"target are now the {(on ? "full release" : "demo")}. Recompiling.");
    }

    [MenuItem(BuildItem, true)]
    static bool ValidateBuildDefine()
    {
        Menu.SetChecked(BuildItem, DemoMode.BuildIsDemo);
        return true;
    }

    // The two beats that only play on the first morning of a weekend, and therefore only play once unless
    // you can put them back. No dialog: it is a testing gesture, run over and over while authoring.
    [MenuItem("Draftmaster/Demo/Re-arm The Opening (alarm + liaison)", priority = 411)]
    static void RearmOpening()
    {
        PlayerPrefs.DeleteKey("weekend.wokeup");    // PitLaneStart.WokeUpKey
        PlayerPrefs.DeleteKey("weekend.briefed");   // WeekendBriefing's memory
        PlayerPrefs.DeleteKey("weekend.appointment");
        PlayerPrefs.DeleteKey("weekend.greeted");
        PlayerPrefs.DeleteKey("weekend.route");
        PlayerPrefs.Save();

        // And put the three days back. Testing the opening over and over walks the weekend's clock to
        // Sunday night, and a finished weekend has nothing left to book — which reads exactly like the
        // objective being broken when it is only over.
        Draftmaster.Weekend.WeekendLedger.ClearAll();

        Debug.Log("Opening re-armed: fresh three days, and the next race scene wakes you up in the dark "
                  + "with nothing booked until the liaison says so. (Her beat has its own appearance flag — "
                  + "Draftmaster > NPCs > Clear Appearance Flags if she has already had her say.)");
    }

    // Put the cars on the track without walking the weekend to a session. GridSpawner (and therefore the
    // pit boxes, the crews and the pit box stands) only builds a field when a session is live, which is
    // right for the game and a nuisance when you are authoring anything on pit road.
    [MenuItem("Draftmaster/Debug/Session Live (spawn the field)", priority = 413)]
    static void ToggleSessionLive()
    {
        bool on = PlayerPrefs.GetInt("raceweekend.sessionlive", 0) == 1;
        PlayerPrefs.SetInt("raceweekend.sessionlive", on ? 0 : 1);
        PlayerPrefs.Save();
        Debug.Log(on
            ? "Session live OFF: the next race scene opens with an empty track, as a paddock half-day does."
            : "Session live ON: the next race scene spawns the field, the pit boxes, the crews and the stands.");
    }

    [MenuItem("Draftmaster/Debug/Session Live (spawn the field)", true)]
    static bool ValidateSessionLive()
    {
        Menu.SetChecked("Draftmaster/Debug/Session Live (spawn the field)",
                        PlayerPrefs.GetInt("raceweekend.sessionlive", 0) == 1);
        return true;
    }

    [MenuItem(WipeItem, priority = 412)]
    static void Wipe()
    {
        if (!EditorUtility.DisplayDialog(
                "Wipe the career save?",
                "Clears every bit of progress — money, stats, championship, sponsors, quests, rivalries, "
                + "who you have met, where you have been.\n\nSettings and the signed-in account are kept.",
                "Wipe it", "Cancel"))
            return;

        CareerReset.ClearAll();
        Debug.Log("Career save wiped: PlayerPrefs cleared apart from settings and account, caches dropped.");
    }
}
