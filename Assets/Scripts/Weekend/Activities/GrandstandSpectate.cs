using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;

// Watching somebody else's session, from a seat in the grandstand.
//
// The other two championships at the venue race whether the player is there or not, so their sessions are
// simulated rather than spawned (SeriesSimulator). What changed is where you watch it from: you walk to a
// stand, sit down, and the session plays out in front of you — the world stays live and visible, the timing
// tower and the broadcast calls sit down the right-hand side of the screen like a TV graphic, and the
// player is sat in the crowd rather than staring at a panel with the game frozen behind it.
//
// SPEED shortens it, LEAVE gets up and walks off. Watching is not free time: a driver who does the homework
// learns where the track is going, which is worth real setup knowledge going into their own session.
public class GrandstandSpectate : MonoBehaviour
{
    public static GrandstandSpectate Active { get; private set; }
    public static bool Watching => Active != null;

    SeriesSimulator.Session _session;
    readonly List<SeriesSimulator.Entry> _order = new();
    readonly List<string> _feed = new();

    WeekendActivity _activity;
    OnFootController _player;
    bool _lockedPlayer;

    float _progress01;
    int _nextMoment;
    static readonly int[] Speeds = { 1, 4, 12 };
    int _speedIndex;
    bool _finished;
    float _startedAt;

    // Real seconds a full session takes at 1x.
    float PlaybackSeconds => _session != null && _session.kind == ActivityKind.SpectateRace ? 40f : 18f;

    // Sit down and watch. Returns null when there is nothing to watch.
    public static GrandstandSpectate Begin(WeekendActivity a)
    {
        if (a == null || !a.IsSpectate) return null;
        if (Active != null) Destroy(Active.gameObject);

        var go = new GameObject("GrandstandSpectate");
        DontDestroyOnLoad(go);
        var watcher = go.AddComponent<GrandstandSpectate>();
        watcher._activity = a;
        watcher.Build();
        return watcher;
    }

    void OnEnable()
    {
        Active = this;
        _startedAt = Time.unscaledTime;
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        StandUp();
    }

    void Build()
    {
        string track = TrackSelection.CurrentDisplayName;
        int id = RaceWeekend.WeekendId;
        float baseLap = EstimateLapSeconds();

        _session = _activity.kind switch
        {
            ActivityKind.SpectateQualifying => SeriesSimulator.Qualifying(_activity.series, id, track, baseLap),
            ActivityKind.SpectateRace => SeriesSimulator.Race(_activity.series, id, track, baseLap),
            _ => SeriesSimulator.Practice(_activity.series, id, track, baseLap),
        };
        _session.OrderAt(0f, _order);
        SitDown();
    }

    // Sitting is the player planted in their seat: the walk input is off, but the world is not frozen and
    // the camera is still theirs, so the cars, the crowd and the paddock all carry on around them.
    void SitDown()
    {
        _player = OnFootController.Current;
        if (_player == null) return;
        _player.MovementLocked = true;
        _lockedPlayer = true;
    }

    void StandUp()
    {
        if (!_lockedPlayer || _player == null) return;
        _player.MovementLocked = false;
        _lockedPlayer = false;
    }

    // A lap of the actual track the weekend is at, so the simulated times are not nonsense next to the
    // player's own. Falls back to a generic short-oval lap when there is no road in this scene.
    static float EstimateLapSeconds()
    {
        var builder = TrackPackage.ActiveTrack != null ? TrackPackage.ActiveTrack : Object.FindFirstObjectByType<TrackBuilder>();
        if (builder == null) return 32f;

        float length = RacePositionTracker.Instance != null ? RacePositionTracker.Instance.TrackLength : 0f;
        if (length <= 1f)
        {
            var samples = builder.SampleCenterline();
            if (samples != null && samples.Count > 0) length = samples[samples.Count - 1].distance;
        }
        if (length <= 1f) return 32f;

        return Mathf.Clamp(length / 45f, 12f, 190f);
    }

    void Update()
    {
        if (_session == null) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.unscaledTime - _startedAt > 0.3f)
        {
            FinishNow();
            return;
        }

        if (_finished) return;

        _progress01 += Time.unscaledDeltaTime * Speeds[_speedIndex] / PlaybackSeconds;
        if (_progress01 >= 1f)
        {
            _progress01 = 1f;
            PumpMoments();
            _session.OrderAt(1f, _order);
            _finished = true;
            return;
        }

        _session.OrderAt(_progress01, _order);
        PumpMoments();
    }

    void PumpMoments()
    {
        while (_nextMoment < _session.moments.Count && _session.moments[_nextMoment].at01 <= _progress01)
        {
            _feed.Add(_session.moments[_nextMoment].text);
            if (_feed.Count > 5) _feed.RemoveAt(0);
            _nextMoment++;
        }
    }

    void FinishNow()
    {
        // Walking out early still counts as having been there, but you saw less of it.
        if (!_finished) _progress01 = Mathf.Max(_progress01, 0.05f);

        var a = _activity;
        var outcome = Settle();
        _activity = null;
        _session = null;

        StandUp();
        Destroy(gameObject);

        WeekendAppointment.Clear();
        if (a != null) WeekendDirector.Finish(a, outcome, inWorld: true);
    }

    // ------------------------------------------------------------------ draw

    // Down the right-hand side, like a broadcast graphic — the track, the crowd and the cars stay visible,
    // which is the entire point of being sat here rather than reading a panel.
    void OnGUI()
    {
        if (_session == null) return;

        float w = Mathf.Min(PixelGUI.Px(210f), Screen.width * 0.42f);
        float h = Mathf.Min(PixelGUI.Px(190f), Screen.height - PixelGUI.Px(24f));
        var outer = new Rect(Screen.width - w - PixelGUI.Px(6f), Mathf.Round((Screen.height - h) * 0.5f), w, h);

        PixelGUI.Panel(outer, focused: true);
        var content = PixelGUI.PanelContent(outer, 6f);
        float y = content.y;

        var head = PixelGUI.DataDim;
        var hc = head.normal.textColor;
        head.normal.textColor = PixelGUI.Info;
        GUI.Label(new Rect(content.x, y, content.width, PixelGUI.Px(11f)),
                  SeriesCatalog.Name(_session.series).ToUpperInvariant() + "  ·  " + KindLabel(), head);
        head.normal.textColor = hc;
        y += PixelGUI.Px(12f);

        var right = PixelGUI.DataDim;
        var ra = right.alignment;
        right.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(content.x, content.y, content.width, PixelGUI.Px(11f)), StatusLine(), right);
        right.alignment = ra;

        PixelGUI.Bar(new Rect(content.x, y, content.width, PixelGUI.Px(4f)), _progress01,
                     _finished ? PixelGUI.Confirm : PixelGUI.Gold);
        y += PixelGUI.Px(9f);

        float controlsH = PixelGUI.Px(40f);
        float towerH = (content.yMax - y - controlsH) * 0.62f;
        var tower = new Rect(content.x, y, content.width, towerH);
        PixelGUI.Fill(tower, PixelGUI.Plate);
        DrawTower(tower);
        y += towerH + PixelGUI.Px(4f);

        var feed = new Rect(content.x, y, content.width, content.yMax - y - controlsH - PixelGUI.Px(2f));
        PixelGUI.Fill(feed, PixelGUI.Plate);
        DrawFeed(feed);

        float bh = PixelGUI.Px(16f);
        float by = content.yMax - bh * 2f - PixelGUI.Px(4f);
        float half = (content.width - PixelGUI.Px(4f)) * 0.5f;

        if (PixelGUI.Tab(new Rect(content.x, by, half, bh), "SPEED " + Speeds[_speedIndex] + "x", false))
            _speedIndex = (_speedIndex + 1) % Speeds.Length;

        if (!_finished && PixelGUI.Tab(new Rect(content.x + half + PixelGUI.Px(4f), by, half, bh), "SKIP", false))
        {
            _progress01 = 1f;
            PumpMoments();
            _session.OrderAt(1f, _order);
            _finished = true;
        }

        if (PixelGUI.Button(new Rect(content.x, content.yMax - bh, content.width, bh),
                            _finished ? "THAT'S THE RESULT" : "SEEN ENOUGH (ESC)"))
            FinishNow();
    }

    string KindLabel() => _session.kind switch
    {
        ActivityKind.SpectateQualifying => "QUALIFYING",
        ActivityKind.SpectateRace => "RACE",
        _ => "PRACTICE",
    };

    string StatusLine()
    {
        if (_session.kind != ActivityKind.SpectateRace)
            return _finished ? "OVER" : "RUNNING";

        int lap = Mathf.Clamp(Mathf.CeilToInt(_progress01 * _session.laps), 1, _session.laps);
        return _finished ? "CHECKERED" : $"LAP {lap}/{_session.laps}";
    }

    void DrawTower(Rect r)
    {
        float pad = PixelGUI.Px(4f);
        float x = r.x + pad, y = r.y + PixelGUI.Px(3f);
        float w = r.width - pad * 2f;

        bool race = _session.kind == ActivityKind.SpectateRace;
        GUI.Label(new Rect(x, y, w, PixelGUI.Px(10f)),
                  race ? $"{"POS",-4}{"#",-4}{"DRIVER",-12}{"INT",-8}" : $"{"POS",-4}{"#",-4}{"DRIVER",-12}{"LAP",-8}",
                  PixelGUI.HeadingSmall);
        y += PixelGUI.Px(11f);
        PixelGUI.Rule(x, y, w);
        y += PixelGUI.Px(3f);

        float rowH = PixelGUI.Px(11f);
        int rows = Mathf.Min(_order.Count, Mathf.FloorToInt((r.yMax - y - PixelGUI.Px(2f)) / rowH));
        var style = PixelGUI.Data;
        var prev = style.normal.textColor;

        for (int i = 0; i < rows; i++)
        {
            var e = _order[i];
            var rowRect = new Rect(x - PixelGUI.Px(2f), y, w, rowH);
            if (i == 0) PixelGUI.Fill(rowRect, new Color(PixelGUI.Gold.r, PixelGUI.Gold.g, PixelGUI.Gold.b, 0.18f));

            style.normal.textColor = e.retired && _progress01 > 0.55f ? PixelGUI.TextDisabled
                                   : i == 0 ? PixelGUI.Gold : PixelGUI.Text;

            string name = e.driverName.Length > 11 ? e.driverName.Substring(0, 11) : e.driverName;
            string col = race ? RaceInterval(e, i) : SeriesSimulator.Fmt(e.lapTime);
            GUI.Label(new Rect(x, y, w, rowH), $"{("P" + (i + 1)),-4}{("#" + e.carNumber),-4}{name,-12}{col,-8}", style);
            y += rowH;
        }
        style.normal.textColor = prev;
    }

    // Gaps open up as the race runs: the final gap, scaled by how far in we are, which reads right without
    // pretending to be a lap-by-lap model.
    string RaceInterval(SeriesSimulator.Entry e, int index)
    {
        if (_finished) return SeriesSimulator.GapText(e);
        if (index == 0) return "LEADER";
        if (e.retired && _progress01 > 0.55f) return "OUT";
        float gap = Mathf.Max(0.15f, (e.gapToLeader < 0f ? 12f : e.gapToLeader)) * Mathf.Lerp(0.25f, 1f, _progress01);
        return "+" + gap.ToString("0.00");
    }

    void DrawFeed(Rect r)
    {
        float pad = PixelGUI.Px(4f);
        float x = r.x + pad, y = r.y + PixelGUI.Px(3f);
        float w = r.width - pad * 2f;

        GUI.Label(new Rect(x, y, w, PixelGUI.Px(10f)), "BROADCAST", PixelGUI.HeadingSmall);
        y += PixelGUI.Px(11f);
        PixelGUI.Rule(x, y, w);
        y += PixelGUI.Px(4f);

        if (_feed.Count == 0)
        {
            GUI.Label(new Rect(x, y, w, PixelGUI.Px(11f)), "Waiting for the green.", PixelGUI.DataDim);
            return;
        }

        for (int i = 0; i < _feed.Count; i++)
        {
            bool latest = i == _feed.Count - 1;
            var style = latest ? PixelGUI.Body : PixelGUI.DataDim;
            var content = new GUIContent(_feed[i]);
            float h = style.CalcHeight(content, w);
            if (y + h > r.yMax - PixelGUI.Px(2f)) break;
            GUI.Label(new Rect(x, y, w, h), content, style);
            y += h + PixelGUI.Px(3f);
        }
    }

    // ------------------------------------------------------------------ scoring

    WeekendOutcome Settle()
    {
        var o = WeekendOutcome.Nothing;
        if (_session == null) return o;

        float watched = Mathf.Clamp01(_progress01);
        o.score = watched;

        // Homework. Watching the leaders here is worth setup knowledge, and a race is worth more than a
        // practice because the tyre tells you more over a run than it does over one lap.
        float value = _session.kind switch
        {
            ActivityKind.SpectateRace => 0.10f,
            ActivityKind.SpectateQualifying => 0.05f,
            _ => 0.035f,
        };
        o.setupGain = value * watched;
        o.teamMorale = watched * 2f;   // the engineers appreciate a driver who does the homework

        var winner = _session.ByFinish(1);
        var pole = _session.PoleSitter();

        if (watched < 0.3f)
        {
            o.headline = "Left the stand before it got interesting.";
        }
        else if (_session.kind == ActivityKind.SpectateRace && winner != null)
        {
            RecordResult(_session.series, winner.driverName);
            o.headline = $"{SeriesCatalog.Nickname(_session.series)} race: #{winner.carNumber} {winner.driverName} won it. " +
                         $"{_session.cautions} cautions, {_session.leadChanges} lead changes, and you saw where the track went.";
        }
        else if (_session.kind == ActivityKind.SpectateQualifying && pole != null)
        {
            o.headline = $"{SeriesCatalog.Nickname(_session.series)} pole to #{pole.carNumber} {pole.driverName}, " +
                         $"{SeriesSimulator.Fmt(pole.lapTime)}. You now know what the track will take.";
        }
        else
        {
            o.headline = $"Watched the {SeriesCatalog.Nickname(_session.series)} session from the stand.";
        }

        return o;
    }

    // Keep the winner on file so the paddock, the phone feed and next weekend's press questions can refer
    // to a race the player actually watched.
    static void RecordResult(RacingSeries series, string winner)
    {
        PlayerPrefs.SetString("weekend.lastwinner." + (int)series, winner ?? "");
        PlayerPrefs.Save();
    }

    // The last winner of a championship, for anything that wants to talk about it.
    //
    // Answered from the season book rather than from what the player sat and watched: these races happen
    // whether anybody is in the stand or not, so the paddock knows who won the truck race even if the
    // player spent Friday night signing hats. Falls back to the stash above for a save that predates the
    // season book.
    public static string LastWinner(RacingSeries series)
    {
        var rounds = SeasonChampionships.RunRounds(series);
        for (int i = rounds.Count - 1; i >= 0; i--)
        {
            var winner = SeasonChampionships.Result(series, rounds[i]).Winner;
            if (winner != null && !string.IsNullOrEmpty(winner.driverName)) return winner.driverName;
        }
        return PlayerPrefs.GetString("weekend.lastwinner." + (int)series, "");
    }
}
