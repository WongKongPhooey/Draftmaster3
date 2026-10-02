using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Practice/qualifying session director. Active when RaceWeekend.IsPracticeLike: the track goes green
// immediately (no formation lap or safety car — FormationDirector disables itself), the AI field
// waits parked in their pit boxes, and this component cycles a handful of them out for lap stints
// so the track never holds more than maxOnTrack cars. Also owns lap timing (LapTimingManager) and,
// OUTSIDE a weekend, the standalone flow: Practice → QUALIFYING reloads into a timed qualifying session;
// Qualifying → START RACE captures the best-lap order as the race grid (RaceWeekend.GridOrder) and reloads
// into the race. Both steps are rows in the pause menu, alongside END SESSION for a session booked off the
// weekend timetable — nothing is drawn over the windscreen.
//
// Both sessions run on a clock. When it runs out the field runs its last lap and comes in, and the player's
// car is brought home by SessionEndPitIn: finish the lap and the AI drives it into the box, or pit yourself.
// The session ends as the driver climbs out in the box.
public class PracticeDirector : MonoBehaviour
{
    public static PracticeDirector Instance { get; private set; }

    [Header("Session clock")]
    [Tooltip("Length (s) of a practice session. When it runs out a driver on track finishes their lap and is brought into their box (SessionEndPitIn); the session ends as they climb out.")]
    public float practiceSeconds = 1200f;
    [Tooltip("Length (s) of the qualifying session. The lap a driver is on when it runs out still counts; the grid is captured as the session ends — when the driver climbs out in their box, or START RACE is chosen in the pause menu.")]
    public float qualifyingSeconds = 600f;

    [Header("Track activity")]
    [Tooltip("Most AI cars allowed on track (out of their boxes) at once.")]
    public int maxOnTrack = 8;
    [Tooltip("Laps per stint, picked per run (x = min, y = max inclusive).")]
    public Vector2Int stintLaps = new Vector2Int(2, 4);
    [Tooltip("Seconds a car rests in its box between stints (x = min, y = max).")]
    public Vector2 restSeconds = new Vector2(10f, 45f);
    [Tooltip("Seconds after load before the first cars head out (x = min, y = max, staggered per car).")]
    public Vector2 initialDelaySeconds = new Vector2(4f, 25f);

    readonly List<PracticeAIStint> _stints = new();
    float _tick;
    bool _isQualifying;
    float _sessionEndTime;
    bool _sessionOver;
    bool _ended;
    SessionEndPitIn _wrapUp;

    public static PracticeDirector Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("PracticeDirector");
            Instance = go.AddComponent<PracticeDirector>();
        }
        return Instance;
    }

    void Awake()
    {
        if (!RaceWeekend.IsPracticeLike)
        {
            enabled = false;
            return;
        }
        Instance = this;
        _isQualifying = RaceWeekend.IsQualifying;
        // Practice-like sessions run under a green track: player unrestricted, AI brains live (their
        // stint controllers keep them parked until released).
        RaceStart.ResetToDefault();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        LapTimingManager.Ensure();
        _sessionEndTime = Time.time + (_isQualifying ? qualifyingSeconds : practiceSeconds);
    }

    // ---- The session clock ----

    // True while the session clock is running: practice or qualifying, before its time is up.
    public static bool TimedSessionRunning =>
        Instance != null && Instance.enabled && !Instance._sessionOver && Instance._sessionEndTime > Time.time;

    // The clock has run out. The session is not over until the player's car is back in its box and they are
    // out of it (SessionEndPitIn); until then this is the chequered flag, and stopping in the box gets the
    // driver out of the car without being asked (PitLaneStart).
    public static bool SessionOver => Instance != null && Instance.enabled && Instance._sessionOver;

    // The AI has the player's car and is driving it home: the broadcast toggle must not hand it back mid in-lap.
    public static bool BringingPlayerIn => SessionOver && Instance._wrapUp != null && Instance._wrapUp.AIDriving;

    // Take time off the session clock: the fast travel is free on the weekend's clock but not on this one,
    // which the player is racing against. Returns the seconds actually taken (never more than were left).
    public static float SpendSessionTime(float seconds)
    {
        if (!TimedSessionRunning || seconds <= 0f) return 0f;
        float taken = Mathf.Min(seconds, Instance._sessionEndTime - Time.time);
        Instance._sessionEndTime -= taken;
        return taken;
    }

    // GridSpawner registers each practice AI here after spawning it.
    public void Register(PracticeAIStint stint)
    {
        if (stint == null || _stints.Contains(stint)) return;
        stint.Bind(this);
        stint.nextReleaseTime = Time.time + Random.Range(initialDelaySeconds.x, initialDelaySeconds.y);
        _stints.Add(stint);
    }

    // A car finished its stint and is pinned back in its box — schedule its next run.
    public void OnStintParked(PracticeAIStint stint)
    {
        if (stint != null) stint.nextReleaseTime = Time.time + Random.Range(restSeconds.x, restSeconds.y);
    }

    void Update()
    {
        if (!_sessionOver && !_ended && Time.time >= _sessionEndTime) FlagSession();

        _tick -= Time.deltaTime;
        if (_tick > 0f) return;
        _tick = 1f;
        // Nobody new goes out under the chequered flag.
        if (_sessionOver) return;

        int onTrack = 0;
        for (int i = _stints.Count - 1; i >= 0; i--)
        {
            if (_stints[i] == null) { _stints.RemoveAt(i); continue; }
            if (!_stints[i].IsParked) onTrack++;
        }
        if (onTrack >= maxOnTrack) return;

        for (int i = 0; i < _stints.Count && onTrack < maxOnTrack; i++)
        {
            var s = _stints[i];
            if (s.IsParked && Time.time >= s.nextReleaseTime)
            {
                s.Release(Random.Range(stintLaps.x, stintLaps.y + 1));
                onTrack++;
            }
        }
    }

    // The clock has run out. Every car on track runs the lap it is on and comes in; the player's car is
    // brought home by SessionEndPitIn, which ends the session once the driver is out of it.
    void FlagSession()
    {
        _sessionOver = true;
        for (int i = 0; i < _stints.Count; i++)
            if (_stints[i] != null) _stints[i].EndAfterThisLap();
        _wrapUp = SessionEndPitIn.Begin(this);
    }

    // ---- Advancing the session ----

    // Advance the weekend: practice → qualifying; qualifying → capture the grid → race. Each step
    // reloads the scene; the race then runs the normal pre-grid → formation → green flow.
    public void StartRace()
    {
        // Once only: the clock's wrap-up and the pause menu can both end the session, and a second go would
        // walk a routed session on into the standalone flow's next step.
        if (_ended) return;
        _ended = true;
        if (_wrapUp != null) _wrapUp.MarkClosed();

        // Qualifying always publishes its grid, however the session was reached.
        if (_isQualifying) CaptureGrid();

        // The weekend schedule sent us out here, so the session reports back to it and the player picks what
        // to do with the rest of the day off the timetable. Without the schedule this is still the old
        // straight line: practice to qualifying to race.
        if (WeekendRouted)
        {
            WeekendDirector.FinishRoutedSession(BuildSessionOutcome());
            return;
        }

        RaceWeekend.Current = _isQualifying ? RaceWeekend.Session.Race : RaceWeekend.Session.Qualifying;
        ReloadScene();
    }

    // A phone turned upright for the swing camera turns back to landscape before the scene goes.
    static void ReloadScene()
    {
        if (DriveOrientationController.HoldSceneChangeForLandscape(ReloadScene)) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // True when this session is a booking off the weekend timetable rather than the standalone flow.
    public static bool WeekendRouted => !string.IsNullOrEmpty(WeekendDirector.PendingRouteId);

    // What the pause menu's END SESSION row should say, or null when there is nothing to end: outside a
    // weekend the session advances on its own button, and off a practice-like session there is no session
    // to hand back at all.
    // The row the pause menu offers for moving the session on. There used to be a red rectangle in the
    // top-right corner of the windscreen saying QUALIFYING or START RACE for the whole hour; the pause
    // menu is where every other way out of a session already lives, so this is the only one now.
    //
    // Routed sessions hand back to the timetable; the standalone flow walks practice -> qualifying ->
    // race itself, and says which step it is offering.
    public static string PauseMenuExitLabel
    {
        get
        {
            if (Instance == null || !Instance.enabled || Instance._ended) return null;
            if (WeekendRouted) return "END SESSION";
            return Instance._isQualifying ? "START RACE" : "QUALIFYING";
        }
    }

    // What the session was worth to the weekend. Practice pays in setup knowledge - laps are data, and a
    // driver who ran the whole session gives the engineers something to work with. Qualifying pays in where
    // you start, which is the only thing qualifying has ever paid in.
    Draftmaster.Weekend.WeekendOutcome BuildSessionOutcome()
    {
        var o = Draftmaster.Weekend.WeekendOutcome.Nothing;
        var lt = LapTimingManager.Instance;

        LapTimingManager.CarTimes player = null;
        if (lt != null)
            for (int i = 0; i < lt.Rows.Count; i++)
                if (lt.Rows[i] != null && lt.Rows[i].isPlayer) { player = lt.Rows[i]; break; }

        int laps = player != null ? player.lapsCompleted : 0;

        if (!_isQualifying)
        {
            // Laps buy setup knowledge; laps and where the best one left you on the timesheet together set
            // the crew's mood. The rules live in PracticeVerdict.
            int practicePos = 0, practiceField = 0;
            if (lt != null && player != null && player.bestLap > 0f)
            {
                var order = new List<LapTimingManager.CarTimes>();
                lt.RankByBest(order);
                practiceField = order.Count;
                practicePos = order.IndexOf(player) + 1;
            }
            return Draftmaster.Weekend.PracticeVerdict.Evaluate(laps, practicePos, practiceField);
        }

        // Qualifying: find where the captured grid put the player.
        int pos = 0;
        var grid = RaceWeekend.GridOrder;
        if (grid != null)
            for (int i = 0; i < grid.Count; i++)
                if (grid[i] != null && grid[i].isPlayer) { pos = i + 1; break; }

        o.statKey = "qualifyingsessions";
        o.statCount = 1;

        if (pos <= 0 || laps == 0)
        {
            o.score = 0f;
            o.teamMorale = -8f;
            o.headline = "No time set. You will start this race from the back of it.";
            return o;
        }

        int field = grid != null ? Mathf.Max(1, grid.Count) : 1;
        float rank01 = 1f - Mathf.Clamp01((pos - 1) / (float)Mathf.Max(1, field - 1));
        o.score = rank01;
        o.teamMorale = Mathf.Lerp(-4f, 10f, rank01);
        o.mediaStanding = pos == 1 ? 10f : pos <= 5 ? 5f : 0f;
        o.fanAppeal = pos == 1 ? 3f : pos <= 5 ? 1.2f : 0f;
        o.sponsorMood = pos <= 10 ? 5f : 0f;
        o.headline = pos == 1
            ? "POLE. The car was under you and you used all of it."
            : $"Qualified P{pos} of {field}.";
        return o;
    }

    // Rank the field by best qualifying lap (no-time cars go to the back, ordered by laps run; a player
    // with no time behind all of them) and publish it as the race grid. Identity comes from the timing
    // rows (name/number/isPlayer).
    void CaptureGrid()
    {
        var lt = LapTimingManager.Instance;
        if (lt == null || lt.Rows.Count == 0) { RaceWeekend.GridOrder = null; return; }

        var rows = new List<LapTimingManager.CarTimes>();
        lt.RankByBest(rows);
        var ranked = Draftmaster.Sim.StartingGrid.OrderForGrid(rows, c => c.bestLap, c => c.lapsCompleted,
                                                               c => c.isPlayer);

        var grid = new List<RaceWeekend.GridEntry>(ranked.Count);
        for (int i = 0; i < ranked.Count; i++)
        {
            grid.Add(new RaceWeekend.GridEntry
            {
                driverName = ranked[i].name,
                carNumber = ranked[i].carNumber,
                isPlayer = ranked[i].isPlayer,
                bestLap = ranked[i].bestLap,
            });
        }
        RaceWeekend.GridOrder = grid;
    }

    void OnGUI()
    {
        if (_ended) return;

        float remaining = _sessionEndTime - Time.time;
        string text = !_sessionOver
            ? $"{(_isQualifying ? "QUALIFYING" : "PRACTICE")}  {Mathf.FloorToInt(Mathf.Max(0f, remaining) / 60f)}:{Mathf.FloorToInt(Mathf.Max(0f, remaining) % 60f):00}"
            : _wrapUp != null && _wrapUp.AIDriving ? "CHEQUERED FLAG · BRINGING YOU IN"
            : _wrapUp != null && _wrapUp.OnPitRoad ? "CHEQUERED FLAG · STOP IN YOUR BOX"
            : "CHEQUERED FLAG · FINISH THE LAP OR PIT";

        // Up in the corner itself now. It used to be pushed down to clear the red session button that sat
        // above it, and that button is gone.
        float w = PixelGUI.Px(210f), h = PixelGUI.Px(20f);
        var box = new Rect(Screen.width - w - PixelGUI.Px(8f), PixelGUI.Px(8f), w, h);
        // A phone turned upright for the swing camera: the corner is under the pause and TV buttons and
        // the running order, so the clock sits centred on top of the timing strip (or the pedals) instead.
        if (DriveOrientationController.Portrait)
        {
            float bottom = LapTimingManager.StripTop ?? (Screen.height - TouchDriveControls.PedalsTopFromBottom);
            box = new Rect(Mathf.Round((Screen.width - w) * 0.5f), bottom - h - PixelGUI.Px(4f), w, h);
        }
        PixelGUI.Panel(box);

        // Counting down is the accent; done and waiting on the player is the gain colour.
        var style = PixelGUI.Data;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = !_sessionOver ? PixelGUI.Gold : PixelGUI.Confirm;
        GUI.Label(box, text, style);
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;
    }
}
