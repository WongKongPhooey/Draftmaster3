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

    // Fresh weekend (call from menu flow before loading a track scene).
    public static void ResetWeekend()
    {
        Current = Session.Practice;
        GridOrder = null;
    }
}
