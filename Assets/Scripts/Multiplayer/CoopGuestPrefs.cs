using UnityEngine;

// Protects the guest's own career from the host's.
//
// Career state is PlayerPrefs-backed getters read all over the codebase — TrackSelection.CurrentId,
// RaceWeekend.WeekendId, RaceWeekend.SessionLive. For a guest to see the HOST's weekend, the mirror has to
// put the host's values where those getters look, which means writing the guest's prefs. Left alone, the
// guest's next solo session would boot into a weekend that was never theirs.
//
// So the small set of keys the mirror writes is snapshotted on connect and put back on disconnect. The
// snapshot is itself persisted, so an ungraceful exit — a crash, a kill, pulling the plug — is recoverable:
// the next boot finds a snapshot still parked and restores from it before anything reads a career value.
//
// The weekend ledger is NOT in here. WeekendLedger.ImportJson deliberately holds the host's book in memory
// only and never writes prefs, so there is nothing to protect.
public static class CoopGuestPrefs
{
    // Every key the mirror writes on a guest.
    static readonly string[] Keys =
    {
        "track.current",              // TrackSelection
        "raceweekend.id",             // RaceWeekend.WeekendId
        "raceweekend.sessionlive",    // RaceWeekend.SessionLive
    };

    const string StashPrefix = "coop.guest.stash.";
    const string StashFlag = "coop.guest.stashed";

    public static bool Stashed => PlayerPrefs.GetInt(StashFlag, 0) == 1;

    // Park the guest's own values before the mirror starts overwriting them. Idempotent: calling it twice
    // must not overwrite the real snapshot with the host's values we already wrote.
    public static void Capture()
    {
        if (Stashed) return;

        foreach (var key in Keys)
        {
            // Types differ per key, so the stash records the string form plus whether the key existed at all.
            // An absent key must come back absent, not as a zero.
            if (PlayerPrefs.HasKey(key))
                PlayerPrefs.SetString(StashPrefix + key, ReadAsString(key));
            else
                PlayerPrefs.DeleteKey(StashPrefix + key);
        }

        PlayerPrefs.SetInt(StashFlag, 1);
        PlayerPrefs.Save();
    }

    // Put the guest's career back exactly as it was.
    public static void Restore()
    {
        if (!Stashed) return;

        foreach (var key in Keys)
        {
            string stashKey = StashPrefix + key;
            if (PlayerPrefs.HasKey(stashKey))
            {
                WriteFromString(key, PlayerPrefs.GetString(stashKey));
                PlayerPrefs.DeleteKey(stashKey);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);   // the guest had never set it; leave it unset
            }
        }

        PlayerPrefs.SetInt(StashFlag, 0);
        PlayerPrefs.Save();

        // The in-memory ledger still holds the host's book. Drop it so the next read rebuilds from the
        // guest's own prefs rather than serving a weekend that has gone home with somebody else.
        Draftmaster.Weekend.WeekendLedger.InvalidateCache();
    }

    // Boot-time safety net: a previous co-op session that ended without a clean disconnect left a stash
    // parked, and the host's career values are still sitting in this player's prefs. Put them back before
    // anything reads them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RestoreOnBoot()
    {
        if (!Stashed) return;
        Debug.Log("CoopGuestPrefs: a co-op session ended without disconnecting — restoring this player's own career.");
        Restore();
    }

    // ---- typed key handling ----
    //
    // Two of the three are strings and one is an int. PlayerPrefs will not tell us which, so the type is
    // decided here per key rather than probed.

    static bool IsInt(string key) => key == "raceweekend.id" || key == "raceweekend.sessionlive";

    static string ReadAsString(string key) =>
        IsInt(key) ? PlayerPrefs.GetInt(key, 0).ToString() : PlayerPrefs.GetString(key, "");

    static void WriteFromString(string key, string value)
    {
        if (IsInt(key))
        {
            int.TryParse(value, out int i);
            PlayerPrefs.SetInt(key, i);
        }
        else PlayerPrefs.SetString(key, value ?? "");
    }
}
