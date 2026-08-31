using System.Collections.Generic;
using UnityEngine;

// Wipes the save: everything the player has DONE goes, everything they have SET stays.
//
// Career progress in this project is PlayerPrefs, spread across a dozen subsystems that each own their own
// keys — the weekend ledger, the three championships, sponsor deals, quests, the stats ledger, the wallet,
// travel, rivalries, fan appeal, the phone, which beats an NPC has already played. PlayerPrefs cannot be
// enumerated, so clearing them key by key means keeping a list that goes stale the moment a subsystem adds
// one. So this does the opposite: DeleteAll, then put back the short list of keys that are settings or
// identity rather than progress. A subsystem added tomorrow is cleared by this without touching this file.
//
// The SQLite database is deliberately left alone: it holds the driver world (rosters, teams, tracks), which
// is content, not the player's save. Nothing writes career results into it yet — RaceDirector records into
// PlayerPrefs — so there is nothing of the player's in there to lose.
public static class CareerReset
{
    // Settings: how the game is set up to play, which a fresh career has no business resetting.
    static readonly string[] KeptInts =
    {
        "AudioOn", "CommsOn", "Volume",         // sound
        "CameraRotate", "CameraZoom",           // camera
        "FPSLimit", "SteeringType",             // performance + controls
        "Difficulty", "AIDifficulty",           // difficulty the player picked
        "ShowRacingLine", "ShowMiniMap", "hud.leaderboard",   // HUD toggles
        "DbSchemaVersion",                      // wiping this would drop and reseed the Drivers table
        "NewUser",
        DemoMode.OverrideKey,                   // a demo restart must not flip the build back to full
    };

    // Identity: the signed-in account. Clearing these logs the player out of PlayFab, which a "start the
    // demo again" button has no business doing.
    static readonly string[] KeptStrings =
    {
        "PlayerUsername", "PlayerEmail", "PlayerPassword", "PlayerPlayFabId",
        "ContactEmailSet", "LatestVersion",
    };

    // Clear the save and hand back a game that is on its first day again.
    public static void ClearAll()
    {
        var ints = new List<KeyValuePair<string, int>>();
        foreach (var key in KeptInts)
            if (PlayerPrefs.HasKey(key)) ints.Add(new KeyValuePair<string, int>(key, PlayerPrefs.GetInt(key)));

        var strings = new List<KeyValuePair<string, string>>();
        foreach (var key in KeptStrings)
            if (PlayerPrefs.HasKey(key)) strings.Add(new KeyValuePair<string, string>(key, PlayerPrefs.GetString(key)));

        PlayerPrefs.DeleteAll();

        foreach (var kv in ints) PlayerPrefs.SetInt(kv.Key, kv.Value);
        foreach (var kv in strings) PlayerPrefs.SetString(kv.Key, kv.Value);
        PlayerPrefs.Save();

        DropCaches();
    }

    // The prefs are gone, but the game is still running: several subsystems read their book once and keep
    // it in a static, and statics survive a scene load. Anything holding a copy of what was just deleted
    // has to be told, or the fresh career starts with the old career's ledger still in memory.
    static void DropCaches()
    {
        Draftmaster.Weekend.WeekendLedger.InvalidateCache();
        Draftmaster.Weekend.SeasonChampionships.InvalidateCache();
        Draftmaster.Sponsors.SponsorBook.InvalidateCache();

        PhoneNotes.Clear();
        DriverRelationships.ResetAll();
        AppearanceConditions.ClearAllSeen();   // also empties the once-per-play-session set

        RaceWeekend.Current = RaceWeekend.Session.Practice;
        RaceWeekend.GridOrder = null;
    }
}
