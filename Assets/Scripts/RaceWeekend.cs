using System.Collections.Generic;

// Which session of the race weekend the track scene is running, plus the qualifying result carried
// across the scene reload into the race. Career scenes load into Friday practice first; the
// session buttons (PracticeDirector) advance Practice → Qualifying → Race, reloading the scene each
// time. The competitive lobby race is always a race; co-op runs the full career session flow, driven by
// the host and mirrored to the guest by CareerMirror.
public static class RaceWeekend
{
    public enum Session { Practice, Qualifying, Race }

    public static Session Current = Session.Practice;

    // CareerActive, not IsSinglePlayer: the competitive lobby race has no weekend around it and is always
    // a race, but co-op runs the full career and its practice and qualifying are real sessions.
    public static bool IsPractice => Current == Session.Practice && GameSession.CareerActive;
    public static bool IsQualifying => Current == Session.Qualifying && GameSession.CareerActive;
    // Practice-style sessions: no formation lap or safety car, AI run stints from their boxes.
    public static bool IsPracticeLike => IsPractice || IsQualifying;
    public static bool IsRaceSession => !IsPracticeLike;

    // One car's qualifying result. GridOrder[0] = pole. Captured by PracticeDirector at the end of
    // qualifying; GridSpawner reads it in the race to fix each AI's identity/livery to its grid slot
    // (and the player's reserved pit box to their rank) instead of shuffling.
    [System.Serializable]
    public class GridEntry
    {
        public string driverName;
        public int carNumber;      // livery number too (Resources/<carset>livery<n>)
        public bool isPlayer;
        public float bestLap;      // -1 = no time set
    }

    // null = no qualifying ran this weekend; the race grid falls back to random order.
    //
    // Persisted, because qualifying is Saturday and the race is Sunday: a player who quits in between and
    // comes back through CAREER has to start where they qualified, not from a shuffled grid. Stored with
    // the weekend it belongs to, so a grid can never carry into a different weekend — whatever reset that
    // weekend did or did not clear it.
    public static List<GridEntry> GridOrder
    {
        get
        {
            if (_gridWeekend != WeekendId) LoadGrid();
            return _grid;
        }
        set
        {
            _grid = value;
            _gridWeekend = WeekendId;

            // A co-op guest is in the host's weekend; its own prefs keep its own career (CoopGuestPrefs).
            if (Coop.IsGuest) return;

            if (value == null || value.Count == 0) UnityEngine.PlayerPrefs.DeleteKey(GridKey);
            else UnityEngine.PlayerPrefs.SetString(GridKey, UnityEngine.JsonUtility.ToJson(
                     new SavedGrid { weekendId = WeekendId, entries = value }));
            UnityEngine.PlayerPrefs.Save();
        }
    }

    const string GridKey = "raceweekend.grid";

    [System.Serializable]
    class SavedGrid
    {
        public int weekendId;
        public List<GridEntry> entries;
    }

    static List<GridEntry> _grid;
    static int _gridWeekend = int.MinValue;   // the weekend _grid was read for; MinValue = not read yet

    static void LoadGrid()
    {
        _gridWeekend = WeekendId;
        _grid = null;

        // The guest's prefs hold the guest's own career, and the weekend id in them is the host's (written
        // by CareerMirror) — the two can collide, so a guest never reads a grid off disk.
        if (Coop.IsGuest) return;

        string json = UnityEngine.PlayerPrefs.GetString(GridKey, "");
        if (string.IsNullOrEmpty(json)) return;

        SavedGrid saved = null;
        try { saved = UnityEngine.JsonUtility.FromJson<SavedGrid>(json); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"RaceWeekend: unreadable saved grid ({e.Message})."); }

        if (saved != null && saved.weekendId == WeekendId && saved.entries != null && saved.entries.Count > 0)
            _grid = saved.entries;
    }

    // Monotonic id for "which race weekend is this", bumped by ResetWeekend and persisted so it
    // survives scene loads and quits. AppearanceConditions scopes its once-per-weekend memory to it.
    const string WeekendIdKey = "raceweekend.id";
    public static int WeekendId => Draftmaster.Weekend.FramePrefs.GetInt(WeekendIdKey, 0);   // asked every frame

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
        // The competitive lobby race has no weekend around it — a lobby that has loaded the track is a race.
        // Co-op does have one, so it reads the flag like any career session; on a guest that flag is written
        // by CareerMirror from the host's copy rather than by anything local.
        get => !GameSession.CareerActive || Draftmaster.Weekend.FramePrefs.GetInt(SessionLiveKey, 0) == 1;
        set
        {
            // A co-op guest never decides whether a session is live — the host does, and the mirror tells us.
            // Letting a guest write here would fight the next push and date its own save with someone else's
            // weekend (CareerSave.Stamp, below).
            if (Coop.IsGuest) return;

            UnityEngine.PlayerPrefs.SetInt(SessionLiveKey, value ? 1 : 0);
            UnityEngine.PlayerPrefs.Save();
            Draftmaster.Weekend.FramePrefs.Invalidate();
            // Taking the car out and handing it back are both progress: date them, so CONTINUE knows when
            // the player was last actually racing rather than only when they last picked a track.
            CareerSave.Stamp();
        }
    }

    // Fresh weekend (call from menu flow before loading a track scene).
    public static void ResetWeekend()
    {
        if (Coop.IsGuest) return;   // the host starts weekends; the guest is told about them

        UnityEngine.PlayerPrefs.SetInt(WeekendIdKey, WeekendId + 1);
        UnityEngine.PlayerPrefs.Save();
        Draftmaster.Weekend.FramePrefs.Invalidate();
        Current = Session.Practice;
        GridOrder = null;
        // Whatever the last weekend had the player out driving belongs to that weekend. Booking ids are only
        // half-day + time + kind, so a route left behind matches the same session on every weekend after it.
        WeekendDirector.ClearRoute();
        SessionLive = false;      // stamps the save point
    }
}
