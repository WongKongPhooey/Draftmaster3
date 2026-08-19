using System.Collections.Generic;
using Draftmaster.Sponsors;
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

    // How far through the race the leader is, 0 at the green and 1 at the checkered, counted in whole
    // laps plus the leader's fraction of the current one. The AI reads this to settle in early and charge
    // over the closing laps (Draftmaster.Sim.RaceCraft).
    public float RaceProgress01 { get; private set; }

    // -1 when the race distance isn't known — practice, qualifying, multiplayer, or before the green.
    // Callers treat that as "mid-race", rather than pretending it's forever lap one.
    public static float Progress01 =>
        (Instance != null && Instance.isActiveAndEnabled && Instance._phase != Phase.Waiting)
            ? Instance.RaceProgress01 : -1f;

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
                UpdateRaceProgress(rt);
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

    // Leader's share of the race distance. Order is already sorted by progress, so entry 0 is the leader.
    void UpdateRaceProgress(RacePositionTracker rt)
    {
        if (raceLaps <= 0 || rt.Order.Count == 0) return;
        var leader = rt.Order[0];
        if (leader == null || leader.tf == null) return;

        // Fraction of the current lap, so a 3-lap race doesn't step through the phases in thirds.
        float len = rt.TrackLength;
        float frac = len > 0f ? Mathf.Clamp01((leader.progress - leader.lap * len) / len) : 0f;
        RaceProgress01 = Mathf.Clamp01((RacingLaps(leader) + frac) / raceLaps);
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

    int _payout;          // prize money earned this race, shown on the results panel
    int _sponsorPayout;   // sponsorship money earned this race (placed decals + met clauses)
    List<SponsorDeal> _expiredSponsors;   // deals that ran out on this race

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

            // Sponsorship pays on top — but only for decals actually on the car, and only while the deal
            // still has races left on it. Clause bonuses land here too (finish inside the agreed position).
            _sponsorPayout = SponsorBook.PayoutForFinish(playerPos);
            if (_sponsorPayout > 0) PlayerWallet.Add(_sponsorPayout);

            // Every live deal burns a race, placed or not: sitting on a contract you never painted on the
            // car wastes it, which is what makes the four panels worth arguing over.
            _expiredSponsors = SponsorBook.TickRace();
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
                if (PixelGUI.Button(new Rect(Screen.width - PixelGUI.Px(96f), PixelGUI.Px(38f),
                                             PixelGUI.Px(88f), PixelGUI.Px(20f)), "RESULTS"))
                    _panelHidden = false;
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
        // Sits above LeaderboardUI, which would otherwise draw straight over it.
        var box = new Rect(PixelGUI.Px(8f), PixelGUI.Px(14f), PixelGUI.Px(80f), PixelGUI.Px(20f));
        PixelGUI.Panel(box);
        var style = PixelGUI.Data;
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(box, $"LAP {lap}/{raceLaps}", style);
        style.alignment = prevAlign;
    }

    // Full-width announcement. The kerb band above and below is the kit's own way of marking a race
    // moment, and it keeps the banner from reading as just another panel that happens to be wide.
    void DrawBanner(string text)
    {
        float h = PixelGUI.Px(34f);
        float y = Mathf.Round(Screen.height * 0.2f);
        float kerb = PixelGUI.Px(4f);

        PixelGUI.Fill(new Rect(0f, y, Screen.width, h), PixelGUI.PlateDeep);
        PixelGUI.Kerb(new Rect(0f, y - kerb, Screen.width, kerb));
        PixelGUI.Kerb(new Rect(0f, y + h, Screen.width, kerb));

        var style = PixelGUI.Heading;
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0f, y, Screen.width, h), text, style);
        style.alignment = prevAlign;
    }

    void DrawResults()
    {
        // Best laps for the table, looked up by transform.
        var best = new Dictionary<Transform, float>();
        var lt = LapTimingManager.Instance;
        if (lt != null)
            for (int i = 0; i < lt.Rows.Count; i++)
                if (lt.Rows[i] != null && lt.Rows[i].tf != null) best[lt.Rows[i].tf] = lt.Rows[i].bestLap;

        float row = PixelGUI.Px(13f);
        float w = PixelGUI.Px(340f);
        float h = PixelGUI.Px(108f) + _results.Count * row;
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Max(PixelGUI.Px(16f), Mathf.Round((Screen.height - h) * 0.4f));

        PixelGUI.Scrim(0.7f);
        PixelGUI.Panel(new Rect(x, y, w, h), focused: true);

        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 10f);
        float cx = c.x, cy = c.y;

        GUI.Label(new Rect(cx, cy, c.width, PixelGUI.Px(18f)), "RACE RESULTS", PixelGUI.Heading);
        cy += PixelGUI.Px(20f);
        PixelGUI.Kerb(new Rect(cx, cy, c.width, PixelGUI.Px(4f)));
        cy += PixelGUI.Px(8f);

        int playerPos = 0;
        for (int i = 0; i < _results.Count; i++) if (_results[i].isPlayer) { playerPos = i + 1; break; }
        string headline = playerPos == 1 ? "YOU WIN!" : (playerPos > 0 ? $"YOU FINISHED P{playerPos}" : "");
        if (_payout > 0) headline += $"   +{PlayerWallet.Format(_payout)}  (BANK {PlayerWallet.CashText})";
        var headStyle = PixelGUI.Data;
        var prevHead = headStyle.normal.textColor;
        headStyle.normal.textColor = playerPos == 1 ? PixelGUI.Gold : PixelGUI.Confirm;
        GUI.Label(new Rect(cx, cy, c.width, PixelGUI.Px(14f)), headline, headStyle);
        headStyle.normal.textColor = prevHead;
        cy += PixelGUI.Px(16f);

        // Sponsorship line: what the decals on the car earned, and anything whose contract just ran out.
        if (_sponsorPayout > 0 || (_expiredSponsors != null && _expiredSponsors.Count > 0))
        {
            string sponsors = _sponsorPayout > 0 ? $"SPONSORS  +{PlayerWallet.Format(_sponsorPayout)}" : "SPONSORS  —";
            if (_expiredSponsors != null && _expiredSponsors.Count > 0)
            {
                var names = new List<string>(_expiredSponsors.Count);
                foreach (var d in _expiredSponsors) names.Add(d.sponsorName);
                sponsors += $"   (DEAL ENDED: {string.Join(", ", names)})";
            }
            GUI.Label(new Rect(cx, cy, c.width, row), sponsors, PixelGUI.Row);
            cy += PixelGUI.Px(14f);
        }

        GUI.Label(new Rect(cx, cy, c.width, PixelGUI.Px(10f)),
                  $"{"POS",-5}{"#",-5}{"DRIVER",-17}{"RESULT",-13}{"BEST LAP",-11}", PixelGUI.HeadingSmall);
        cy += PixelGUI.Px(12f);
        PixelGUI.Rule(cx, cy, c.width);
        cy += PixelGUI.Px(3f);

        float winnerTime = _results.Count > 0 && _results[0].finished ? _results[0].raceTime : -1f;
        var rowStyle = PixelGUI.Data;
        var prevRow = rowStyle.normal.textColor;
        for (int i = 0; i < _results.Count; i++)
        {
            var r = _results[i];
            rowStyle.normal.textColor = r.isPlayer ? PixelGUI.Gold : PixelGUI.Text;

            string name = string.IsNullOrEmpty(r.name) ? "?" : (r.name.Length > 15 ? r.name.Substring(0, 15) : r.name);
            string result;
            if (!r.finished) result = $"+{r.lapsDown}L";
            else if (i == 0) result = "WINNER";
            else if (winnerTime > 0f) result = $"+{r.raceTime - winnerTime:0.000}";
            else result = LapTimingManager.Format(r.raceTime);

            string bestStr = (r.tf != null && best.TryGetValue(r.tf, out float b)) ? LapTimingManager.Format(b) : "--:--.---";
            GUI.Label(new Rect(cx, cy, c.width, row),
                      $"{("P" + (i + 1)),-5}{("#" + r.carNumber),-5}{name,-17}{result,-13}{bestStr,-11}", rowStyle);
            cy += row;
        }
        rowStyle.normal.textColor = prevRow;

        cy += PixelGUI.Px(6f);
        float bw = PixelGUI.Px(104f), bh = PixelGUI.Px(20f), bgap = PixelGUI.Px(8f);
        // The road trip is the main loop: pick the next venue on the map, spend stops on detours, race there.
        // SKIP TRAVEL keeps the old instant weekend loop for quick testing.
        if (PixelGUI.Button(new Rect(cx, cy, bw, bh), "HIT THE ROAD")) { _panelHidden = true; TravelMapScreen.Open(); }
        if (PixelGUI.Tab(new Rect(cx + bw + bgap, cy, PixelGUI.Px(76f), bh), "SKIP TRAVEL", false)) NextWeekend();
        if (PixelGUI.Tab(new Rect(cx + bw + PixelGUI.Px(76f) + bgap * 2f, cy, PixelGUI.Px(56f), bh), "CLOSE", false))
            _panelHidden = true;
    }
}
