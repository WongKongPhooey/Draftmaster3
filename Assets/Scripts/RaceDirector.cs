using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gives the single-player race an ending: counts racing laps from the green flag (formation-lap line
// crossings excluded via a per-car baseline), throws the checkered flag when the leader completes
// raceLaps, records each car's finish as it crosses, then classifies stragglers by running order and
// shows a results screen. "NEXT WEEKEND" resets the weekend (back to Friday practice) and reloads.
//
// Runs alongside RacePositionTracker (positions/laps) and LapTimingManager (best laps for the results
// table). Cars keep circulating behind the results panel — cosmetic cool-down, nothing is frozen.
public class RaceDirector : MonoBehaviour
{
    public static RaceDirector Instance { get; private set; }

    [Tooltip("Race length in laps (counted from the green flag).")]
    public int raceLaps = 3;
    [Tooltip("Seconds after the checkered flag before still-running cars are classified where they are.")]
    public float stragglerTimeout = 45f;
    [Tooltip("Seconds the CHECKERED FLAG banner stays up.")]
    public float bannerSeconds = 4f;

    enum Phase { Waiting, Racing, Checkered, Results }
    Phase _phase = Phase.Waiting;

    class Result
    {
        public string name;
        public int carNumber;
        public bool isPlayer;
        public Transform tf;
        public bool finished;
        public float raceTime;   // s from green to the line; -1 for classified-unfinished cars
        public int lapsDown;     // >0 only for classified-unfinished cars
    }

    readonly Dictionary<Transform, int> _lapBaseline = new();
    readonly Dictionary<Transform, Result> _finishedBy = new();
    readonly List<Result> _results = new();
    float _greenTime = -1f;
    float _checkeredTime;
    bool _panelHidden;
    GUIStyle _lapStyle, _bannerStyle, _titleStyle, _headlineStyle, _rowStyle, _headStyle;

    public static RaceDirector Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("RaceDirector");
            Instance = go.AddComponent<RaceDirector>();
        }
        return Instance;
    }

    void Awake()
    {
        if (!RaceWeekend.IsRaceSession || !GameSession.IsSinglePlayer)
        {
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() { LapTimingManager.Ensure(); }

    void Update()
    {
        var rt = RacePositionTracker.Instance;
        if (rt == null) return;

        switch (_phase)
        {
            case Phase.Waiting:
                if (RaceStart.IsGreen)
                {
                    _greenTime = Time.time;
                    BaselineLaps(rt);       // line crossings before green (formation) don't count
                    // Career ledger: taking the green counts as a start. When the player's career gains a
                    // manufacturer, also increment "starts.<manufacturer>" here for sponsor-style quests.
                    PlayerStatsLedger.Increment("starts");
                    _phase = Phase.Racing;
                }
                break;

            case Phase.Racing:
                BaselineLaps(rt);           // cars appearing mid-race start from their current lap
                CheckFinishes(rt);
                if (_results.Count > 0)     // leader took the flag
                {
                    _checkeredTime = Time.time;
                    _phase = Phase.Checkered;
                }
                break;

            case Phase.Checkered:
                BaselineLaps(rt);
                CheckFinishes(rt);
                bool playerDone = true;
                int running = 0;
                for (int i = 0; i < rt.Order.Count; i++)
                {
                    var e = rt.Order[i];
                    if (e == null || e.tf == null || _finishedBy.ContainsKey(e.tf)) continue;
                    running++;
                    if (e.isPlayer) playerDone = false;
                }
                // Close the race once the player has finished (short beat for the moment to land),
                // everyone is home, or the stragglers have had long enough.
                if ((playerDone && Time.time - _checkeredTime > 2f) || running == 0 || Time.time - _checkeredTime > stragglerTimeout)
                {
                    ClassifyRemaining(rt);
                    RecordCareerResult();
                    _phase = Phase.Results;
                }
                break;
        }
    }

    void BaselineLaps(RacePositionTracker rt)
    {
        for (int i = 0; i < rt.Order.Count; i++)
        {
            var e = rt.Order[i];
            if (e == null || e.tf == null || _lapBaseline.ContainsKey(e.tf)) continue;
            _lapBaseline[e.tf] = e.lap;
        }
    }

    int RacingLaps(RacePositionTracker.Entry e) =>
        _lapBaseline.TryGetValue(e.tf, out int baseLap) ? e.lap - baseLap : e.lap;

    void CheckFinishes(RacePositionTracker rt)
    {
        // Entries are already sorted by progress, so same-frame finishers record in track order.
        for (int i = 0; i < rt.Order.Count; i++)
        {
            var e = rt.Order[i];
            if (e == null || e.tf == null || _finishedBy.ContainsKey(e.tf)) continue;
            if (RacingLaps(e) < raceLaps) continue;

            var r = new Result
            {
                name = e.name,
                carNumber = e.carNumber,
                isPlayer = e.isPlayer,
                tf = e.tf,
                finished = true,
                raceTime = Time.time - _greenTime,
            };
            _finishedBy[e.tf] = r;
            _results.Add(r);
        }
    }

    void ClassifyRemaining(RacePositionTracker rt)
    {
        for (int i = 0; i < rt.Order.Count; i++)   // running order = classification order
        {
            var e = rt.Order[i];
            if (e == null || e.tf == null || _finishedBy.ContainsKey(e.tf)) continue;
            var r = new Result
            {
                name = e.name,
                carNumber = e.carNumber,
                isPlayer = e.isPlayer,
                tf = e.tf,
                finished = false,
                raceTime = -1f,
                lapsDown = Mathf.Max(1, raceLaps - RacingLaps(e)),
            };
            _finishedBy[e.tf] = r;
            _results.Add(r);
        }
    }

    int _payout; // prize money earned this race, shown on the results panel

    // Career ledger + quest evaluation, once, on the final classification.
    void RecordCareerResult()
    {
        int playerPos = 0;
        for (int i = 0; i < _results.Count; i++) if (_results[i].isPlayer) { playerPos = i + 1; break; }
        if (playerPos > 0)
        {
            PlayerStatsLedger.Increment("races");
            if (playerPos == 1) PlayerStatsLedger.Increment("wins");
            if (playerPos <= 5) PlayerStatsLedger.Increment("top5s");
            if (playerPos <= 10) PlayerStatsLedger.Increment("top10s");
            // Prize money funds the travel-map economy (parts, tows).
            _payout = PlayerWallet.PayoutForPosition(playerPos);
            PlayerWallet.Add(_payout);
        }

        var classification = new List<(string name, bool isPlayer)>(_results.Count);
        foreach (var r in _results) classification.Add((r.name, r.isPlayer));
        QuestManager.OnRaceFinished(classification);

        // Time heals: every stored driver relationship drifts a little back toward neutral each race, so
        // feuds fade unless refreshed with new contact.
        DriverRelationships.RegenTowardNeutral(4f);
    }

    public void NextWeekend()
    {
        RaceWeekend.ResetWeekend();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---- HUD ----

    void OnGUI()
    {
        EnsureStyles();

        if (_phase == Phase.Racing || _phase == Phase.Checkered)
        {
            DrawLapCounter();
            if (_phase == Phase.Checkered && Time.time - _checkeredTime < bannerSeconds)
                DrawBanner("CHECKERED FLAG");
        }
        else if (_phase == Phase.Results)
        {
            if (_panelHidden)
            {
                if (GUI.Button(new Rect(Screen.width - 176f, 76f, 160f, 34f), "RESULTS")) _panelHidden = false;
            }
            else
            {
                DrawResults();
            }
        }
    }

    void DrawLapCounter()
    {
        var rt = RacePositionTracker.Instance;
        int lap = 1;
        if (rt != null)
        {
            for (int i = 0; i < rt.Order.Count; i++)
            {
                var e = rt.Order[i];
                if (e != null && e.tf != null && e.isPlayer) { lap = Mathf.Clamp(RacingLaps(e) + 1, 1, raceLaps); break; }
            }
        }
        // Sits above LeaderboardUI (origin y=70), which would otherwise draw straight over it.
        var box = new Rect(12f, 34f, 140f, 26f);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(box, $"LAP {lap} / {raceLaps}", _lapStyle);
    }

    void DrawBanner(string text)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0f, Screen.height * 0.2f, Screen.width, 56f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, 56f), text, _bannerStyle);
    }

    void DrawResults()
    {
        // Best laps for the table, looked up by transform.
        var best = new Dictionary<Transform, float>();
        var lt = LapTimingManager.Instance;
        if (lt != null)
            for (int i = 0; i < lt.Rows.Count; i++)
                if (lt.Rows[i] != null && lt.Rows[i].tf != null) best[lt.Rows[i].tf] = lt.Rows[i].bestLap;

        float w = 620f;
        float h = 132f + _results.Count * 21f;
        float x = (Screen.width - w) * 0.5f;
        float y = Mathf.Max(40f, (Screen.height - h) * 0.4f);

        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(x - 12f, y - 12f, w + 24f, h + 24f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, w, 26f), "RACE RESULTS", _titleStyle);
        y += 30f;

        int playerPos = 0;
        for (int i = 0; i < _results.Count; i++) if (_results[i].isPlayer) { playerPos = i + 1; break; }
        string headline = playerPos == 1 ? "YOU WIN!" : (playerPos > 0 ? $"YOU FINISHED P{playerPos}" : "");
        if (_payout > 0) headline += $"    +{PlayerWallet.Format(_payout)}  (bank: {PlayerWallet.CashText})";
        GUI.Label(new Rect(x, y, w, 24f), headline, _headlineStyle);
        y += 30f;

        GUI.Label(new Rect(x, y, w, 18f), $"{"Pos",-5}{"#",-5}{"Driver",-17}{"Result",-13}{"Best Lap",-11}", _headStyle);
        y += 20f;

        float winnerTime = _results.Count > 0 && _results[0].finished ? _results[0].raceTime : -1f;
        for (int i = 0; i < _results.Count; i++)
        {
            var r = _results[i];
            _rowStyle.normal.textColor = r.isPlayer ? new Color(0.4f, 1f, 0.5f) : Color.white;

            string name = string.IsNullOrEmpty(r.name) ? "?" : (r.name.Length > 15 ? r.name.Substring(0, 15) : r.name);
            string result;
            if (!r.finished) result = $"+{r.lapsDown}L";
            else if (i == 0) result = "WINNER";
            else if (winnerTime > 0f) result = $"+{r.raceTime - winnerTime:0.000}";
            else result = LapTimingManager.Format(r.raceTime);

            string bestStr = (r.tf != null && best.TryGetValue(r.tf, out float b)) ? LapTimingManager.Format(b) : "--:--.---";
            GUI.Label(new Rect(x, y, w, 20f), $"{("P" + (i + 1)),-5}{("#" + r.carNumber),-5}{name,-17}{result,-13}{bestStr,-11}", _rowStyle);
            y += 21f;
        }

        y += 8f;
        // The road trip is the main loop: pick the next venue on the map, spend stops on detours, race there.
        // SKIP TRAVEL keeps the old instant weekend loop for quick testing.
        if (GUI.Button(new Rect(x, y, 200f, 34f), "HIT THE ROAD")) { _panelHidden = true; TravelMapScreen.Open(); }
        if (GUI.Button(new Rect(x + 216f, y, 140f, 34f), "SKIP TRAVEL")) NextWeekend();
        if (GUI.Button(new Rect(x + 372f, y, 140f, 34f), "CLOSE")) _panelHidden = true;
    }

    void EnsureStyles()
    {
        if (_lapStyle != null) return;
        _lapStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _lapStyle.normal.textColor = Color.white;
        _bannerStyle = new GUIStyle(_lapStyle) { fontSize = 34 };
        _bannerStyle.normal.textColor = new Color(1f, 0.95f, 0.75f);
        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        _headlineStyle = new GUIStyle(_titleStyle) { fontSize = 18 };
        _headlineStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
        _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _headStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        _rowStyle.normal.textColor = Color.white;
    }
}
