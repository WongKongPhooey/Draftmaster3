using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Practice/qualifying session director. Active when RaceWeekend.IsPracticeLike: the track goes green
// immediately (no formation lap or safety car — FormationDirector disables itself), the AI field
// waits parked in their pit boxes, and this component cycles a handful of them out for lap stints
// so the track never holds more than maxOnTrack cars. Also owns lap timing (LapTimingManager) and
// the session button that advances the weekend: Practice → "QUALIFYING" reloads into a timed
// qualifying session; Qualifying → "START RACE" captures the best-lap order as the race grid
// (RaceWeekend.GridOrder) and reloads into the race.
public class PracticeDirector : MonoBehaviour
{
    public static PracticeDirector Instance { get; private set; }

    [Header("Qualifying")]
    [Tooltip("Length (s) of the qualifying session. The countdown is advisory — the grid is captured when START RACE is pressed, so late laps still count.")]
    public float qualifyingSeconds = 300f;

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
    GameObject _raceBtn;
    bool _isQualifying;
    float _qualiEndTime;

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
        BuildRaceButton();
        if (_isQualifying) _qualiEndTime = Time.time + qualifyingSeconds;
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
        _tick -= Time.deltaTime;
        if (_tick > 0f) return;
        _tick = 1f;

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

    // ---- Race button (temp) ----

    void BuildRaceButton()
    {
        var canvasGO = new GameObject("PracticeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 111;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        _raceBtn = new GameObject("RaceButton", typeof(RectTransform), typeof(Image), typeof(Button));
        _raceBtn.transform.SetParent(canvasGO.transform, false);
        var rt = _raceBtn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta = new Vector2(160f, 48f);

        _raceBtn.GetComponent<Image>().color = new Color(0.55f, 0.08f, 0.08f, 0.9f);
        _raceBtn.GetComponent<Button>().onClick.AddListener(StartRace);

        var txtGO = new GameObject("Label", typeof(RectTransform));
        txtGO.transform.SetParent(_raceBtn.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var label = txtGO.AddComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.font = BrandFonts.Body;
        // Under the weekend schedule the button just ends the session - what happens next is the player's
        // choice off the timetable, not this director's.
        label.text = WeekendRouted ? "END SESSION"
                                   : (_isQualifying ? "START RACE" : "QUALIFYING");
    }

    // Advance the weekend: practice → qualifying; qualifying → capture the grid → race. Each step
    // reloads the scene; the race then runs the normal pre-grid → formation → green flow.
    public void StartRace()
    {
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // True when this session is a booking off the weekend timetable rather than the standalone flow.
    static bool WeekendRouted => !string.IsNullOrEmpty(WeekendDirector.PendingRouteId);

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
            // Twelve clean laps is a full run sheet; past that the engineers have what they need.
            float run01 = Mathf.Clamp01(laps / 12f);
            o.setupGain = run01 * 0.28f;
            o.teamMorale = Mathf.Lerp(-4f, 8f, run01);
            o.score = run01;
            o.statKey = "practicesessions";
            o.statCount = 1;
            o.headline = laps == 0
                ? "Sat in the car and never turned a lap. The engineers have nothing."
                : $"{laps} laps in the book and a run sheet worth reading.";
            return o;
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

    // Rank the field by best qualifying lap (no-time cars go to the back, ordered by laps run) and
    // publish it as the race grid. Identity comes from the timing rows (name/number/isPlayer).
    void CaptureGrid()
    {
        var lt = LapTimingManager.Instance;
        if (lt == null || lt.Rows.Count == 0) { RaceWeekend.GridOrder = null; return; }

        var ranked = new List<LapTimingManager.CarTimes>();
        lt.RankByBest(ranked);

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
        if (!_isQualifying) return;

        float remaining = _qualiEndTime - Time.time;
        string text = remaining > 0f
            ? $"QUALIFYING  {Mathf.FloorToInt(remaining / 60f)}:{Mathf.FloorToInt(remaining % 60f):00}"
            : "QUALIFYING COMPLETE · PRESS START RACE";

        float w = PixelGUI.Px(200f), h = PixelGUI.Px(20f);
        var box = new Rect(Screen.width - w - PixelGUI.Px(8f), PixelGUI.Px(38f), w, h);
        PixelGUI.Panel(box);

        // Counting down is the accent; done and waiting on the player is the gain colour.
        var style = PixelGUI.Data;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = remaining > 0f ? PixelGUI.Gold : PixelGUI.Confirm;
        GUI.Label(box, text, style);
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;
    }
}
