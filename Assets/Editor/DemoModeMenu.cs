using UnityEditor;
using UnityEngine;

// Draftmaster > Demo — the wipe behind RESTART DEMO, and the two beats of the opening, from the editor.
//
//   Wipe Career Save    what RESTART DEMO does, from the editor.
//   Re-arm The Opening  puts the alarm and the liaison back so they can be watched again.
//   Send The Crew Chief's 'Where Are You' Text
//                       the phone tutorial from the walk to the briefing, fired now (play mode only).
//
// There used to be a demo/full build flag here as well — DemoMode, a DRAFTMASTER_DEMO define and a
// PlayerPrefs override — because the title screen drew one of two menus depending on it. It draws one
// menu now, the flag had nothing left to change, and all of it is gone.
//
// Nothing here opens a modal that blocks the editor except the wipe's confirmation, which is the one place
// a click deserves a second thought.
public static class DemoModeMenu
{
    const string WipeItem = "Draftmaster/Demo/Wipe Career Save";

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

        // And the crew chief's "where are you?" text on the walk to the briefing, with its P prompt and the
        // run hint it holds back until the phone has been put away.
        ChiefCheckInBeat.Rearm();

        Debug.Log("Opening re-armed: fresh three days, and the next race scene wakes you up in the dark "
                  + "with nothing booked until the liaison says so. (Her beat has its own appearance flag — "
                  + "Draftmaster > NPCs > Clear Appearance Flags if she has already had her say.)");
    }

    // The chief's text normally waits for the player to walk within 200 m of the briefing. This sends it
    // now, to look at the bleep, the prompt and the MESSAGES tile without the walk.
    const string ChiefTextItem = "Draftmaster/Demo/Send The Crew Chief's 'Where Are You' Text";

    [MenuItem(ChiefTextItem, priority = 414)]
    static void SendChiefText()
    {
        if (ChiefCheckInBeat.FireNow())
            Debug.Log("Crew chief's text sent: bleep, P prompt, and 1 unread message on the phone.");
        else
            Debug.Log("Crew chief's text not sent — it has already gone this save. Re-arm The Opening puts it back.");
    }

    [MenuItem(ChiefTextItem, true)]
    static bool ValidateSendChiefText() => Application.isPlaying;

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
