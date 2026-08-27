using System.Collections.Generic;

// Which session of the race weekend the track scene is running, plus the qualifying result carried
// across the scene reload into the race. Single-player scenes load into Friday practice first; the
// session buttons (PracticeDirector) advance Practice → Qualifying → Race, reloading the scene each
// time. Multiplayer is always a race.
public static class RaceWeekend
{
    public enum Session { Practice, Qualifying, Race }

    public static Session Current = Session.Practice;

    public static bool IsPractice => Current == Session.Practice && GameSession.IsSinglePlayer;
    public static bool IsQualifying => Current == Session.Qualifying && GameSession.IsSinglePlayer;
    // Practice-style sessions: no formation lap or safety car, AI run stints from their boxes.
    public static bool IsPracticeLike => IsPractice || IsQualifying;
    public static bool IsRaceSession => !IsPracticeLike;

    // One car's qualifying result. GridOrder[0] = pole. Captured by PracticeDirector at the end of
    // qualifying; GridSpawner reads it in the race to fix each AI's identity/livery to its grid slot
    // (and the player's reserved pit box to their rank) instead of shuffling.
    public class GridEntry
    {
        public string driverName;
        public int carNumber;      // livery number too (Resources/<carset>livery<n>)
        public bool isPlayer;
        public float bestLap;      // -1 = no time set
    }

    // null = no qualifying ran this weekend; the race grid falls back to random order.
    public static List<GridEntry> GridOrder;

    // Monotonic id for "which race weekend is this", bumped by ResetWeekend and persisted so it
    // survives scene loads and quits. AppearanceConditions scopes its once-per-weekend memory to it.
    const string WeekendIdKey = "raceweekend.id";
    public static int WeekendId => UnityEngine.PlayerPrefs.GetInt(WeekendIdKey, 0);

    // True while one of the player's own on-track sessions is actually running.
    //
    // The paddock is walkable for all three days of a weekend, but the car is only the player's to take out
    // for the hour the sheet gives them: practice, qualifying, the race. Outside those the pit box is
    // something you walk past on the way to a sponsor, and no series has cars circulating. Whatever puts the
    // player on track sets this — WeekendDirector routing a booked session, the title screen starting an
    // exhibition race — and settling the session clears it again.
    //
    // PlayerPrefs for the same reason as WeekendDirector.PendingRouteId: practice, qualifying and the race
    // each reload the scene, which would eat a static.
    const string SessionLiveKey = "raceweekend.sessionlive";

    public static bool SessionLive
    {
        // Multiplayer has no weekend around it — a lobby that has loaded the track is a race.
        get => !GameSession.IsSinglePlayer || UnityEngine.PlayerPrefs.GetInt(SessionLiveKey, 0) == 1;
        set
        {
            UnityEngine.PlayerPrefs.SetInt(SessionLiveKey, value ? 1 : 0);
            UnityEngine.PlayerPrefs.Save();
        }
    }

    // Fresh weekend (call from menu flow before loading a track scene).
    public static void ResetWeekend()
    {
        UnityEngine.PlayerPrefs.SetInt(WeekendIdKey, WeekendId + 1);
        UnityEngine.PlayerPrefs.Save();
        Current = Session.Practice;
        GridOrder = null;
        SessionLive = false;
    }
}
