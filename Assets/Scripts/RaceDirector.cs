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
    [Tooltip("Draw the small lap counter. Off when IronOvalRaceHUD is running — it draws its own.")]
    public bool drawLapCounter = true;

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
            if (drawLapCounter) DrawLapCounter();
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

    // The results screen from the Iron Oval design file: a header band, the classification down the left
    // on its own plate, and a right column carrying what the race paid - purse, sponsors, bank - over a
    // fan-appeal meter and the button out. The player's row is the one gold band on the screen.
    void DrawResults()
    {
        // Best laps for the table, looked up by transform.
        var best = new Dictionary<Transform, float>();
        var lt = LapTimingManager.Instance;
        if (lt != null)
            for (int i = 0; i < lt.Rows.Count; i++)
                if (lt.Rows[i] != null && lt.Rows[i].tf != null) best[lt.Rows[i].tf] = lt.Rows[i].bestLap;

        float row = PixelGUI.Px(12f);
        float railW = PixelGUI.Px(150f);          // the design's right-hand column
        float w = PixelGUI.Px(420f);
        float bandH = PixelGUI.Px(18f);
        float h = bandH + PixelGUI.Px(30f) + Mathf.Max(_results.Count * row, PixelGUI.Px(150f));
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Max(PixelGUI.Px(12f), Mathf.Round((Screen.height - h) * 0.35f));

        PixelGUI.Scrim(0.7f);
        PixelGUI.Panel(new Rect(x, y, w, h), focused: true);
        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 8f);

        // --- header band ---------------------------------------------------------------------------
        PixelGUI.Fill(new Rect(c.x, c.y, c.width, bandH), PixelGUI.PlateLight);
        GUI.Label(new Rect(c.x + PixelGUI.Px(4f), c.y + PixelGUI.Px(4f), c.width, PixelGUI.Px(12f)),
                  "RACE RESULTS", PixelGUI.Heading);
        var meta = PixelGUI.DataDim;
        var metaAlign = meta.alignment;
        meta.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(c.x, c.y, c.width - PixelGUI.Px(4f), bandH),
                  TrackSelection.CurrentDisplayName + "  ROUND " + (RaceWeekend.WeekendId + 1), meta);
        meta.alignment = metaAlign;

        float top = c.y + bandH + PixelGUI.Px(6f);
        float listW = c.width - railW - PixelGUI.Px(6f);

        // --- classification ------------------------------------------------------------------------
        float listH = h - (top - y) - PixelGUI.Px(10f);
        PixelGUI.Fill(new Rect(c.x, top, listW, listH), PixelGUI.Plate);

        float cx = c.x + PixelGUI.Px(4f), cy = top + PixelGUI.Px(3f);
        GUI.Label(new Rect(cx, cy, listW, PixelGUI.Px(10f)),
                  $"{"POS",-5}{"#",-5}{"DRIVER",-15}{"RESULT",-12}{"BEST",-10}", PixelGUI.HeadingSmall);
        cy += PixelGUI.Px(11f);
        PixelGUI.Rule(cx, cy, listW - PixelGUI.Px(8f));
        cy += PixelGUI.Px(3f);

        float winnerTime = _results.Count > 0 && _results[0].finished ? _results[0].raceTime : -1f;
        var rowStyle = PixelGUI.Data;
        var prevRow = rowStyle.normal.textColor;
        for (int i = 0; i < _results.Count; i++)
        {
            var r = _results[i];
            var rowRect = new Rect(cx - PixelGUI.Px(2f), cy, listW - PixelGUI.Px(6f), row);
            if (r.isPlayer)
                PixelGUI.Fill(rowRect, new Color(PixelGUI.Gold.r, PixelGUI.Gold.g, PixelGUI.Gold.b, 0.25f));
            rowStyle.normal.textColor = r.isPlayer ? PixelGUI.Gold : PixelGUI.Text;

            string name = string.IsNullOrEmpty(r.name) ? "?" : (r.name.Length > 13 ? r.name.Substring(0, 13) : r.name);
            string result;
            if (!r.finished) result = "+" + r.lapsDown + "L";
            else if (i == 0) result = "WINNER";
            else if (winnerTime > 0f) result = "+" + (r.raceTime - winnerTime).ToString("0.000");
            else result = LapTimingManager.Format(r.raceTime);

            string bestStr = (r.tf != null && best.TryGetValue(r.tf, out float b)) ? LapTimingManager.Format(b) : "--:--.---";
            GUI.Label(new Rect(cx, cy, listW, row),
                      $"{("P" + (i + 1)),-5}{("#" + r.carNumber),-5}{name,-15}{result,-12}{bestStr,-10}", rowStyle);
            cy += row;
        }
        rowStyle.normal.textColor = prevRow;

        // --- right column --------------------------------------------------------------------------
        float rx = c.x + listW + PixelGUI.Px(6f);
        float ry = top;

        int playerPos = 0;
        for (int i = 0; i < _results.Count; i++) if (_results[i].isPlayer) { playerPos = i + 1; break; }

        // The one shouted line, blinking like the sheet's LEVEL UP plate - but only when it's earned.
        if (playerPos == 1)
        {
            bool lit = ((int)(Time.unscaledTime / 0.55f) & 1) == 0;
            var winRect = new Rect(rx, ry, railW, PixelGUI.Px(16f));
            PixelGUI.Fill(winRect, lit ? PixelGUI.Gold : PixelGUI.PlateLight);
            var win = PixelGUI.HeadingSmall;
            var winPrev = win.normal.textColor;
            var winAlign = win.alignment;
            win.normal.textColor = lit ? PixelGUI.Ink : PixelGUI.Gold;
            win.alignment = TextAnchor.MiddleCenter;
            GUI.Label(winRect, "RACE WINNER", win);
            win.normal.textColor = winPrev;
            win.alignment = winAlign;
            ry += PixelGUI.Px(20f);
        }
        else if (playerPos > 0)
        {
            var pos = PixelGUI.Data;
            var posPrev = pos.normal.textColor;
            pos.normal.textColor = PixelGUI.Confirm;
            GUI.Label(new Rect(rx, ry, railW, PixelGUI.Px(14f)), "YOU FINISHED P" + playerPos, pos);
            pos.normal.textColor = posPrev;
            ry += PixelGUI.Px(18f);
        }

        PixelGUI.Fill(new Rect(rx, ry, railW, PixelGUI.Px(76f)), PixelGUI.Plate);
        float ix = rx + PixelGUI.Px(5f), iy = ry + PixelGUI.Px(4f), iw = railW - PixelGUI.Px(10f);

        GUI.Label(new Rect(ix, iy, iw, PixelGUI.Px(10f)), "THIS RACE", PixelGUI.HeadingSmall);
        iy += PixelGUI.Px(12f);
        iy += RewardRow(ix, iy, iw, "PURSE", _payout > 0 ? "+" + PlayerWallet.Format(_payout) : "-",
                        _payout > 0 ? PixelGUI.Gold : PixelGUI.TextDisabled);
        iy += RewardRow(ix, iy, iw, "SPONSORS", _sponsorPayout > 0 ? "+" + PlayerWallet.Format(_sponsorPayout) : "-",
                        _sponsorPayout > 0 ? PixelGUI.Confirm : PixelGUI.TextDisabled);
        iy += RewardRow(ix, iy, iw, "BANK", PlayerWallet.CashText, PixelGUI.Text);

        // Fan appeal stands in for the sheet's EXP bar: the progress meter this game actually keeps.
        float appeal = Draftmaster.Fans.FanAppeal.Value;
        GUI.Label(new Rect(ix, iy + PixelGUI.Px(2f), iw, PixelGUI.Px(10f)), "FAN APPEAL", PixelGUI.HeadingSmall);
        var appealStyle = PixelGUI.DataDim;
        var appealAlign = appealStyle.alignment;
        appealStyle.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(ix, iy + PixelGUI.Px(2f), iw, PixelGUI.Px(10f)),
                  Mathf.RoundToInt(appeal) + "/100", appealStyle);
        appealStyle.alignment = appealAlign;
        PixelGUI.Bar(new Rect(ix, iy + PixelGUI.Px(13f), iw, PixelGUI.Px(5f)), appeal / 100f, PixelGUI.Confirm);
        ry += PixelGUI.Px(80f);

        // Deals that ran out get their own line - losing one matters as much as the money.
        if (_expiredSponsors != null && _expiredSponsors.Count > 0)
        {
            var names = new List<string>(_expiredSponsors.Count);
            foreach (var d in _expiredSponsors) names.Add(d.sponsorName);
            var gone = PixelGUI.DataDim;
            var gonePrev = gone.normal.textColor;
            gone.normal.textColor = PixelGUI.Danger;
            GUI.Label(new Rect(rx, ry, railW, PixelGUI.Px(12f)),
                      "DEAL ENDED: " + string.Join(", ", names), gone);
            gone.normal.textColor = gonePrev;
            ry += PixelGUI.Px(14f);
        }

        // The road trip is the main loop: pick the next venue on the map, spend stops on detours, race
        // there. SKIP TRAVEL keeps the instant weekend loop for quick testing.
        float bh = PixelGUI.Px(18f);
        if (PixelGUI.Button(new Rect(rx, ry, railW, bh), "HIT THE ROAD")) { _panelHidden = true; TravelMapScreen.Open(); }
        ry += bh + PixelGUI.Px(4f);
        float halfW = (railW - PixelGUI.Px(4f)) * 0.5f;
        if (PixelGUI.Tab(new Rect(rx, ry, halfW, bh), "SKIP TRAVEL", false)) NextWeekend();
        if (PixelGUI.Tab(new Rect(rx + halfW + PixelGUI.Px(4f), ry, halfW, bh), "CLOSE", false))
            _panelHidden = true;
    }

    // One "label ... value" line in the rewards column.
    static float RewardRow(float x, float y, float w, string label, string value, Color valueColour)
    {
        float h = PixelGUI.Px(12f);
        GUI.Label(new Rect(x, y, w, h), label, PixelGUI.DataDim);

        var style = PixelGUI.Data;
        var prev = style.normal.textColor;
        var align = style.alignment;
        style.normal.textColor = valueColour;
        style.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(x, y, w, h), value, style);
        style.normal.textColor = prev;
        style.alignment = align;

        PixelGUI.Rule(x, y + h - PixelGUI.Px(1f), w, PixelGUI.PlateLight);
        return h + PixelGUI.Px(2f);
    }
}
