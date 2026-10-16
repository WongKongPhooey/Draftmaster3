using UnityEngine;

namespace Draftmaster.Weekend
{
    // The objective the player was walking to when they last saved.
    //
    // Progress in this game is PlayerPrefs, written by whichever subsystem owns it the moment it changes, so
    // there is nothing to "save" in the file sense. What a save anytime button does add is a promise: the
    // marker that was up when you pressed it is the marker you come back to. Most obligations are a short
    // walk and a conversation, so the booking itself is what is worth keeping — what was said halfway through
    // one is not.
    //
    // This is the record and the rule for when it applies; SaveAnytime writes it and asks it on CONTINUE.
    // Per weekend id, like the briefing, so a save from a weekend that has since been driven away from is
    // never pulled forward into the next one.
    public static class SavedObjective
    {
        const string IdKey = "save.objective.id";
        const string WeekendKey = "save.objective.weekend";

        // The booking's activity id, or "" when nothing was booked at the save.
        public static string Id => PlayerPrefs.GetString(IdKey, "");
        public static int WeekendId => PlayerPrefs.GetInt(WeekendKey, -1);

        // Written alongside the rest of the save; the caller flushes (CareerSave.Stamp).
        public static void Write(int weekendId, string activityId)
        {
            PlayerPrefs.SetString(IdKey, activityId ?? "");
            PlayerPrefs.SetInt(WeekendKey, weekendId);
        }

        public static void Forget()
        {
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.DeleteKey(WeekendKey);
        }

        // Should the saved booking be put back on the map?
        //
        // Only into an empty slot: a booking the game is already holding is newer than the save, since every
        // booking is itself written the moment it is made. Only in the weekend it was saved in. And only while
        // the sheet still says it can be done — one finished, missed, or left behind by the clock since the
        // save is not an objective any more, and restoring it would point the player at the past.
        public static bool ShouldRestore(int savedWeekendId, string savedId, int currentWeekendId,
                                         string livePendingId, bool stillDoable)
        {
            if (string.IsNullOrEmpty(savedId)) return false;
            if (savedWeekendId != currentWeekendId) return false;
            if (!string.IsNullOrEmpty(livePendingId)) return false;
            return stillDoable;
        }
    }
}
