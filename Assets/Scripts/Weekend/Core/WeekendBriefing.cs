using UnityEngine;

namespace Draftmaster.Weekend
{
    // Who tells the driver where they are due, and when.
    //
    // The weekend books the next thing on the sheet by itself, so the player always has a marker to walk
    // to. That is right in the middle of a weekend and wrong at the start of one: waking up in your own
    // motorhome already knowing the team's plan for the day is the schedule screen leaking into the
    // fiction. The day is handed over in person — the team liaison catches the driver at the motorhome
    // door — so until she has said it, nothing is booked and the objective strip is empty.
    //
    // This is the rule; WeekendDirector is what asks it, and a PlacedNPC with `givesTheDaysObjective` is
    // who answers it. Pure and PlayerPrefs-backed so it survives the scene reloads a weekend is made of.
    public static class WeekendBriefing
    {
        // The weekend whose day has been handed over. Per weekend id, not a bool: the next weekend starts
        // in the dark again.
        const string Key = "weekend.briefed";

        public static bool Briefed(int weekendId) => PlayerPrefs.GetInt(Key, -1) == weekendId;

        public static void MarkBriefed(int weekendId)
        {
            if (Briefed(weekendId)) return;
            PlayerPrefs.SetInt(Key, weekendId);
            PlayerPrefs.Save();
        }

        // Tests, and a career wiped back to its first morning.
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        // Should the weekend keep its hands off the objective for now?
        //
        //   briefed          the day has already been handed over this weekend
        //   routed           the player is here to drive a session, not to be told about one
        //   weekendUnderway  something has already been done or missed, so this is not the first morning
        //   atTheVenue       there is a paddock to be told about; a menu or the garage is not
        //   giverComing      somebody is actually going to say it — no liaison, no waiting
        public static bool WaitingToBeTold(bool briefed, bool routed, bool weekendUnderway,
                                           bool atTheVenue, bool giverComing)
            => !briefed && !routed && !weekendUnderway && atTheVenue && giverComing;
    }
}
