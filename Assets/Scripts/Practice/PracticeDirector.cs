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
    GUIStyle _qualiStyle;

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
        label.text = _isQualifying ? "START RACE" : "QUALIFYING";
    }

    // Advance the weekend: practice → qualifying; qualifying → capture the grid → race. Each step
    // reloads the scene; the race then runs the normal pre-grid → formation → green flow.
    public void StartRace()
    {
        if (_isQualifying)
        {
            CaptureGrid();
            RaceWeekend.Current = RaceWeekend.Session.Race;
        }
        else
        {
            RaceWeekend.Current = RaceWeekend.Session.Qualifying;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Rank the field by best qualifying lap (no-time cars go to the back, ordered by laps run) and
    // publish it as the race grid. Identity comes from the timing rows (name/number/isPlayer).
    void CaptureGrid()
    {
        var lt = LapTimingManager.Instance;
        if (lt == null || lt.Rows.Count == 0) { RaceWeekend.GridOrder = null; return; }

        var ranked = new List<LapTimingManager.CarTimes>();
        for (int i = 0; i < lt.Rows.Count; i++)
            if (lt.Rows[i] != null && lt.Rows[i].tf != null) ranked.Add(lt.Rows[i]);
        ranked.Sort((a, b) =>
        {
            bool aHas = a.bestLap > 0f, bHas = b.bestLap > 0f;
            if (aHas != bHas) return aHas ? -1 : 1;
            if (aHas) return a.bestLap.CompareTo(b.bestLap);
            return b.lapsCompleted.CompareTo(a.lapsCompleted);
        });

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
        if (_qualiStyle == null)
        {
            _qualiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _qualiStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        }

        float remaining = _qualiEndTime - Time.time;
        string text = remaining > 0f
            ? $"QUALIFYING  {Mathf.FloorToInt(remaining / 60f)}:{Mathf.FloorToInt(remaining % 60f):00}"
            : "QUALIFYING COMPLETE — press START RACE";
        _qualiStyle.normal.textColor = remaining > 0f ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 1f, 0.5f);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(Screen.width - 356f, 76f, 340f, 26f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width - 352f, 76f, 332f, 26f), text, _qualiStyle);
    }
}
