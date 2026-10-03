using UnityEngine;

// Protects the career from a one-off race.
//
// SINGLE RACE and EXHIBITION have no save of their own: the race scene reads which track to build, which
// series, which driver and whether the session is live from the same PlayerPrefs the career keeps them in
// (TrackSelection, SeriesCatalog.PlayerSeries, PlayerDriver, RaceWeekend.SessionLive). So a one-off race
// at Daytona in the Cup car used to leave the career pointing at Daytona, in the Cup series, with the track
// live — and CAREER then loaded that instead of where the career actually was. Worse, the weekend ledger
// is keyed on the series, so the changed series wiped the career's weekend sheet back to Friday morning.
//
// Same answer as CoopGuestPrefs: snapshot the keys before the one-off race writes them, put them back
// before the career reads them. The snapshot is persisted, so a race quit by killing the game is put right
// on the next boot.
public static class OneOffRacePrefs
{
    // Every career key a one-off race writes. CareerSave's stamp is in here because TrackSelection.Select
    // dates the save, and a single race is not a save point.
    static readonly string[] StringKeys =
    {
        "track.current",          // TrackSelection
        "career.drivername",      // PlayerDriver.NameKey
        "career.savedat",         // CareerSave
    };

    static readonly string[] IntKeys =
    {
        "weekend.series",             // SeriesCatalog.PlayerSeries
        "career.carnumber",           // PlayerDriver.NumberKey
        "raceweekend.sessionlive",    // RaceWeekend.SessionLive
    };

    const string StashPrefix = "oneoff.stash.";
    const string StashFlag = "oneoff.stashed";

    public static bool Stashed => PlayerPrefs.GetInt(StashFlag, 0) == 1;

    // Park the career's values. Idempotent: RACE AGAIN reloads without passing the title screen, and a
    // second capture would overwrite the career with the one-off race's own values.
    public static void Capture()
    {
        if (Stashed) return;

        foreach (var key in StringKeys)
        {
            if (PlayerPrefs.HasKey(key)) PlayerPrefs.SetString(StashPrefix + key, PlayerPrefs.GetString(key));
            else PlayerPrefs.DeleteKey(StashPrefix + key);
        }
        foreach (var key in IntKeys)
        {
            if (PlayerPrefs.HasKey(key)) PlayerPrefs.SetInt(StashPrefix + key, PlayerPrefs.GetInt(key));
            else PlayerPrefs.DeleteKey(StashPrefix + key);
        }

        PlayerPrefs.SetInt(StashFlag, 1);
        PlayerPrefs.Save();
    }

    // Put the career back exactly as it was; a key it had never set comes back unset.
    public static void Restore()
    {
        if (!Stashed) return;

        foreach (var key in StringKeys)
        {
            string stashKey = StashPrefix + key;
            if (PlayerPrefs.HasKey(stashKey)) PlayerPrefs.SetString(key, PlayerPrefs.GetString(stashKey));
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(stashKey);
        }
        foreach (var key in IntKeys)
        {
            string stashKey = StashPrefix + key;
            if (PlayerPrefs.HasKey(stashKey)) PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(stashKey));
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(stashKey);
        }

        PlayerPrefs.SetInt(StashFlag, 0);
        PlayerPrefs.Save();
        Draftmaster.Weekend.FramePrefs.Invalidate();

        // RaceWeekend.Current is a static the one-off race set to Race; the career's own session is
        // re-derived from the sheet when CAREER is pressed (WeekendDirector.ResumeRoutedSession).
        RaceWeekend.Current = RaceWeekend.Session.Practice;
    }

    // A one-off race never outlives the run it was started in.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RestoreOnBoot()
    {
        if (!Stashed) return;
        Debug.Log("OneOffRacePrefs: a single race ended without returning to the title — restoring the career.");
        Restore();
    }
}
