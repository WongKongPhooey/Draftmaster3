using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The weekend timetable, as a sheet the player reads and books themselves onto.
//
// Six half-days across the top, everything happening in the selected half-day down the left, and what the
// highlighted booking actually is down the right with the button that commits to it. The clock at the top
// right is the thing that makes it a schedule rather than a menu: doing something moves it, and anything
// the clock has walked past is gone.
//
// Opened with F10, from the pause menu, from the phone, and on its own after every completed activity.
public class WeekendScheduleUI : MonoBehaviour
{
    public static WeekendScheduleUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._open;

    bool _open;
    WeekendSlot _viewing;
    int _selected;
    Vector2 _listScroll;

    string _toast = "";
    float _toastUntil;

    static WeekendScheduleUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("WeekendScheduleUI");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeekendScheduleUI>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (_open) WeekendModal.Pop();
        Instance = null;
    }

    // ------------------------------------------------------------------ open / close

    public static void Open()
    {
        var ui = Ensure();
        if (ui._open) return;
        ui._open = true;
        ui._viewing = WeekendLedger.WeekendOver ? WeekendSlot.SundayPM : WeekendLedger.CurrentSlot;
        ui._selected = 0;
        WeekendModal.Push();
    }

    public static void Close()
    {
        if (Instance == null || !Instance._open) return;
        Instance._open = false;
        WeekendModal.Pop();
    }

    public static void Toggle() { if (IsOpen) Close(); else Open(); }

    // A one-line notice under the header - why a booking was refused, what a no-show cost.
    public static void Toast(string message)
    {
        var ui = Ensure();
        ui._toast = message ?? "";
        ui._toastUntil = Time.unscaledTime + 4f;
    }

    // ------------------------------------------------------------------ drawing

    void OnGUI()
    {
        if (!_open) return;
        if (NPCInteractable.AnyConversationActive || GrandstandSpectate.Watching || WeekendResultCard.IsOpen) return;

        var timetable = WeekendDirector.Timetable;
        if (timetable == null) return;

        PixelGUI.Scrim(0.9f);

        float w = Mathf.Min(PixelGUI.Px(580f), Screen.width - PixelGUI.Px(12f));
        float h = Mathf.Min(PixelGUI.Px(340f), Screen.height - PixelGUI.Px(12f));
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Round((Screen.height - h) * 0.5f);
        var outer = new Rect(x, y, w, h);

        PixelGUI.Panel(outer, focused: true);
        var c = PixelGUI.PanelContent(outer, 8f);

        float cy = DrawHeader(c, timetable);
        cy += DrawDayStrip(new Rect(c.x, cy, c.width, PixelGUI.Px(18f))) + PixelGUI.Px(6f);

        float railW = PixelGUI.Px(196f);
        float listW = c.width - railW - PixelGUI.Px(6f);
        float bodyH = c.yMax - cy - PixelGUI.Px(22f);

        var rows = timetable.InSlot(_viewing);
        rows.Sort((a, b) => a.startMinute.CompareTo(b.startMinute));
        _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, rows.Count - 1));

        DrawList(new Rect(c.x, cy, listW, bodyH), rows);
        DrawRail(new Rect(c.x + listW + PixelGUI.Px(6f), cy, railW, bodyH),
                 rows.Count > 0 ? rows[_selected] : null, timetable);

        DrawFooter(new Rect(c.x, c.yMax - PixelGUI.Px(18f), c.width, PixelGUI.Px(18f)));
    }

    float DrawHeader(Rect c, WeekendTimetable t)
    {
        float bandH = PixelGUI.Px(18f);
        PixelGUI.Fill(new Rect(c.x, c.y, c.width, bandH), PixelGUI.PlateLight);

        string track = string.IsNullOrEmpty(t.trackName) ? "THE TRACK" : t.trackName.ToUpperInvariant();
        GUI.Label(new Rect(c.x + PixelGUI.Px(4f), c.y + PixelGUI.Px(4f), c.width, PixelGUI.Px(12f)),
                  "RACE WEEKEND  ·  " + track, PixelGUI.Heading);

        var meta = PixelGUI.DataDim;
        var prev = meta.alignment;
        var prevCol = meta.normal.textColor;
        meta.alignment = TextAnchor.MiddleRight;
        meta.normal.textColor = WeekendLedger.WeekendOver ? PixelGUI.TextDisabled : PixelGUI.Gold;
        GUI.Label(new Rect(c.x, c.y, c.width - PixelGUI.Px(4f), bandH),
                  WeekendLedger.WeekendOver ? "WEEKEND OVER" : WeekendLedger.ClockText, meta);
        meta.alignment = prev;
        meta.normal.textColor = prevCol;

        float cy = c.y + bandH + PixelGUI.Px(3f);

        // Who you are here as. A truck driver reading the Cup schedule needs to be told which line is theirs.
        var sub = PixelGUI.DataDim;
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.Px(11f)),
                  "ENTERED IN " + SeriesCatalog.Name(SeriesCatalog.PlayerSeries).ToUpperInvariant() +
                  "  ·  ROUND " + (t.weekendId + 1), sub);
        cy += PixelGUI.Px(12f);

        if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil)
        {
            var warn = PixelGUI.Data;
            var wc = warn.normal.textColor;
            warn.normal.textColor = PixelGUI.Gold;
            GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.Px(11f)), _toast, warn);
            warn.normal.textColor = wc;
            cy += PixelGUI.Px(12f);
        }

        return cy + PixelGUI.Px(2f);
    }

    // Six half-days as tabs. The one the clock is in is the live one; anything before it is spent.
    float DrawDayStrip(Rect r)
    {
        float gap = PixelGUI.Px(2f);
        float tabW = (r.width - gap * (WeekendSlots.Count - 1)) / WeekendSlots.Count;

        for (int i = 0; i < WeekendSlots.Count; i++)
        {
            var slot = WeekendSlots.All[i];
            var tab = new Rect(r.x + i * (tabW + gap), r.y, tabW, r.height);
            bool live = !WeekendLedger.WeekendOver && slot == WeekendLedger.CurrentSlot;
            bool spent = (int)slot < (int)WeekendLedger.CurrentSlot || WeekendLedger.WeekendOver;

            if (PixelGUI.Tab(tab, WeekendSlots.ShortLabel(slot), slot == _viewing))
            {
                _viewing = slot;
                _selected = 0;
            }
            // A thin kerb under the half-day the clock is actually in, so browsing ahead never loses "now".
            if (live) PixelGUI.Kerb(new Rect(tab.x, tab.yMax - PixelGUI.Px(2f), tab.width, PixelGUI.Px(2f)));
            else if (spent) PixelGUI.Fill(new Rect(tab.x, tab.y, tab.width, tab.height), new Color(0f, 0f, 0f, 0.35f));
        }
        return r.height;
    }

    void DrawList(Rect r, List<WeekendActivity> rows)
    {
        PixelGUI.Fill(r, PixelGUI.Plate);

        float pad = PixelGUI.Px(4f);
        float hx = r.x + pad, hy = r.y + PixelGUI.Px(3f);
        GUI.Label(new Rect(hx, hy, r.width, PixelGUI.Px(10f)),
                  $"{"TIME",-14}{"",-9}{"WHAT",-30}", PixelGUI.HeadingSmall);
        hy += PixelGUI.Px(11f);
        PixelGUI.Rule(hx, hy, r.width - pad * 2f);
        hy += PixelGUI.Px(3f);

        if (rows.Count == 0)
        {
            GUI.Label(new Rect(hx, hy, r.width - pad * 2f, PixelGUI.Px(12f)),
                      "Nothing booked. Take the window off.", PixelGUI.DataDim);
            return;
        }

        float rowH = PixelGUI.Px(13f);
        var view = new Rect(r.x, hy, r.width, r.yMax - hy - PixelGUI.Px(2f));
        var content = new Rect(0f, 0f, view.width - PixelGUI.Px(10f), rows.Count * rowH);
        _listScroll = GUI.BeginScrollView(view, _listScroll, content, false, false);

        for (int i = 0; i < rows.Count; i++)
        {
            var a = rows[i];
            var rowRect = new Rect(PixelGUI.Px(2f), i * rowH, content.width - PixelGUI.Px(4f), rowH);
            var state = WeekendLedger.Status(a);

            if (i == _selected)
                PixelGUI.Fill(rowRect, new Color(PixelGUI.Info.r, PixelGUI.Info.g, PixelGUI.Info.b, 0.25f));
            else if (a.IsOnTrack)
                PixelGUI.Fill(rowRect, new Color(PixelGUI.Gold.r, PixelGUI.Gold.g, PixelGUI.Gold.b, 0.12f));

            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none)) _selected = i;

            var style = PixelGUI.Data;
            var prevColour = style.normal.textColor;
            style.normal.textColor = ColourFor(a, state);

            string mark = state switch
            {
                WeekendLedger.State.Done => "[x]",
                WeekendLedger.State.Missed => "[-]",
                _ => a.mandatory ? "[!]" : "[ ]",
            };
            string title = a.title.Length > 26 ? a.title.Substring(0, 26) : a.title;
            GUI.Label(new Rect(rowRect.x + PixelGUI.Px(2f), rowRect.y, rowRect.width, rowH),
                      $"{WeekendSlots.Clock(a.startMinute),-7}{mark,-5}{ActivityKinds.Tag(a.kind),-9}{title,-28}", style);
            style.normal.textColor = prevColour;
        }

        GUI.EndScrollView();

        int nudge = UpDownPressed();
        if (nudge != 0) _selected = Mathf.Clamp(_selected + nudge, 0, rows.Count - 1);
    }

    static Color ColourFor(WeekendActivity a, WeekendLedger.State state) => state switch
    {
        WeekendLedger.State.Done => PixelGUI.Confirm,
        WeekendLedger.State.Missed => PixelGUI.Danger,
        WeekendLedger.State.Later => PixelGUI.TextDisabled,
        WeekendLedger.State.Past => PixelGUI.TextDisabled,
        _ => a.IsOnTrack ? PixelGUI.Gold : PixelGUI.Text,
    };

    // What the highlighted booking is, what it clashes with, and the button that commits to it - over the
    // running state of the weekend's four meters.
    void DrawRail(Rect r, WeekendActivity a, WeekendTimetable t)
    {
        PixelGUI.Fill(r, PixelGUI.Plate);
        float pad = PixelGUI.Px(5f);
        float ix = r.x + pad, iy = r.y + pad, iw = r.width - pad * 2f;

        if (a != null)
        {
            GUI.Label(new Rect(ix, iy, iw, PixelGUI.Px(11f)), a.title, PixelGUI.HeadingSmall);
            iy += PixelGUI.Px(12f);

            var meta = PixelGUI.DataDim;
            GUI.Label(new Rect(ix, iy, iw, PixelGUI.Px(10f)),
                      a.Clock + "  ·  " + WeekendSlots.Duration(a.minutes), meta);
            iy += PixelGUI.Px(11f);
            GUI.Label(new Rect(ix, iy, iw, PixelGUI.Px(10f)), a.location, meta);
            iy += PixelGUI.Px(12f);

            iy += Paragraph(new Rect(ix, iy, iw, 0f), a.subtitle, PixelGUI.Body) + PixelGUI.Px(4f);

            iy += DrawChampionshipNote(ix, iy, iw, a, t);

            if (a.appearanceFee > 0)
            {
                var fee = PixelGUI.Data;
                var fc = fee.normal.textColor;
                fee.normal.textColor = PixelGUI.Gold;
                GUI.Label(new Rect(ix, iy, iw, PixelGUI.Px(11f)),
                          "APPEARANCE FEE " + PlayerWallet.Format(a.appearanceFee), fee);
                fee.normal.textColor = fc;
                iy += PixelGUI.Px(12f);
            }

            if (a.mandatory && WeekendLedger.Status(a) == WeekendLedger.State.Available)
            {
                var must = PixelGUI.DataDim;
                var mc = must.normal.textColor;
                must.normal.textColor = PixelGUI.Danger;
                string cost = a.skipMoneyPenalty > 0 ? " (" + PlayerWallet.Format(a.skipMoneyPenalty) + " to skip)" : "";
                GUI.Label(new Rect(ix, iy, iw, PixelGUI.Px(11f)), "OBLIGATION" + cost, must);
                must.normal.textColor = mc;
                iy += PixelGUI.Px(12f);
            }

            // What booking this costs you. The whole point of the screen.
            var clashes = t.ClashesFor(a);
            var live = new List<string>();
            foreach (var other in clashes)
                if (WeekendLedger.Status(other) == WeekendLedger.State.Available)
                    live.Add(other.title);
            if (live.Count > 0)
            {
                var warn = PixelGUI.DataDim;
                var wc = warn.normal.textColor;
                warn.normal.textColor = PixelGUI.Danger;
                iy += Paragraph(new Rect(ix, iy, iw, 0f), "CLASHES WITH " + string.Join(", ", live), warn) + PixelGUI.Px(3f);
                warn.normal.textColor = wc;
            }

            float bh = PixelGUI.Px(18f);
            float by = Mathf.Min(iy + PixelGUI.Px(2f), r.yMax - PixelGUI.Px(72f) - bh);
            bool can = WeekendLedger.CanDo(a, out string why);
            if (can)
            {
                if (PixelGUI.Button(new Rect(ix, by, iw, bh), a.IsOnTrack ? "GET IN THE CAR" : "GO"))
                    WeekendDirector.Begin(a);
            }
            else
            {
                PixelGUI.Fill(new Rect(ix, by, iw, bh), PixelGUI.PlateDeep);
                var off = PixelGUI.Data;
                var oc = off.normal.textColor;
                var oa = off.alignment;
                off.normal.textColor = PixelGUI.TextDisabled;
                off.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(ix, by, iw, bh), why.ToUpperInvariant(), off);
                off.normal.textColor = oc;
                off.alignment = oa;
            }
        }

        DrawMeters(new Rect(ix, r.yMax - PixelGUI.Px(70f), iw, PixelGUI.Px(66f)));
    }

    // A race on the sheet - yours or somebody else's - carries the championship it belongs to: who won it
    // once it has been run, and who is leading the table it counts towards. The two series the player is
    // not entered in race whether they watch or not (SeasonChampionships), so the row for the Cup race is
    // blank on Friday and reads its winner on Sunday evening whatever the player spent the afternoon doing.
    float DrawChampionshipNote(float x, float y, float w, WeekendActivity a, WeekendTimetable t)
    {
        if (a.kind != ActivityKind.SpectateRace && a.kind != ActivityKind.Race) return 0f;

        float used = 0f;
        float row = PixelGUI.Px(11f);

        if (SeasonChampionships.HasRun(a.series, t.weekendId))
        {
            var winner = SeasonChampionships.Result(a.series, t.weekendId).Winner;
            if (winner != null)
            {
                var style = PixelGUI.Data;
                var c = style.normal.textColor;
                style.normal.textColor = winner.isPlayer ? PixelGUI.Gold : PixelGUI.Confirm;
                GUI.Label(new Rect(x, y + used, w, row),
                          winner.isPlayer ? "YOU WON IT" : "WON BY #" + winner.carNumber + " " + winner.driverName, style);
                style.normal.textColor = c;
                used += row + PixelGUI.Px(1f);
            }
        }

        var leader = SeasonChampionships.Leader(a.series);
        if (leader != null)
        {
            var dim = PixelGUI.DataDim;
            GUI.Label(new Rect(x, y + used, w, row),
                      "POINTS LEADER: " + leader.driverName + " (" + leader.points + ")", dim);
            used += row + PixelGUI.Px(3f);
        }

        return used;
    }

    // The four things the weekend moves, plus what it has paid so far.
    void DrawMeters(Rect r)
    {
        PixelGUI.Rule(r.x, r.y - PixelGUI.Px(3f), r.width);
        float y = r.y;
        float rowH = PixelGUI.Px(11f);

        MeterRow(r.x, y, r.width, "FANS", Draftmaster.Fans.FanAppeal.Normalised, PixelGUI.Confirm,
                 Mathf.RoundToInt(Draftmaster.Fans.FanAppeal.Value) + "/100"); y += rowH;
        MeterRow(r.x, y, r.width, "SPONSOR", Signed01(WeekendLedger.SponsorMood), MoodColour(WeekendLedger.SponsorMood),
                 Signed(WeekendLedger.SponsorMood)); y += rowH;
        MeterRow(r.x, y, r.width, "TEAM", Signed01(WeekendLedger.TeamMorale), MoodColour(WeekendLedger.TeamMorale),
                 Signed(WeekendLedger.TeamMorale)); y += rowH;
        MeterRow(r.x, y, r.width, "PRESS", Signed01(WeekendLedger.MediaStanding), MoodColour(WeekendLedger.MediaStanding),
                 Signed(WeekendLedger.MediaStanding)); y += rowH;
        MeterRow(r.x, y, r.width, "SETUP", WeekendLedger.SetupGain, PixelGUI.Info,
                 Mathf.RoundToInt(WeekendLedger.SetupGain * 100f) + "%"); y += rowH;

        var cash = PixelGUI.Data;
        var cc = cash.normal.textColor;
        cash.normal.textColor = WeekendLedger.NetEarnings >= 0 ? PixelGUI.Gold : PixelGUI.Danger;
        GUI.Label(new Rect(r.x, y, r.width, rowH),
                  "WEEKEND " + (WeekendLedger.NetEarnings >= 0 ? "+" : "-") +
                  PlayerWallet.Format(Mathf.Abs(WeekendLedger.NetEarnings)), cash);
        cash.normal.textColor = cc;
    }

    static void MeterRow(float x, float y, float w, string label, float fill01, Color colour, string value)
    {
        float labelW = PixelGUI.Px(42f), valueW = PixelGUI.Px(38f);
        GUI.Label(new Rect(x, y, labelW, PixelGUI.Px(10f)), label, PixelGUI.DataDim);
        PixelGUI.Bar(new Rect(x + labelW, y + PixelGUI.Px(3f), w - labelW - valueW, PixelGUI.Px(4f)),
                     Mathf.Clamp01(fill01), colour);
        var s = PixelGUI.DataDim;
        var a = s.alignment;
        s.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(x + w - valueW, y, valueW, PixelGUI.Px(10f)), value, s);
        s.alignment = a;
    }

    // -100..100 drawn as a bar that fills from the middle outward, so neutral reads as half.
    static float Signed01(float v) => Mathf.Clamp01((v + 100f) / 200f);
    static string Signed(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);
    static Color MoodColour(float v) => v >= 15f ? PixelGUI.Confirm : v <= -15f ? PixelGUI.Danger : PixelGUI.TextDim;

    void DrawFooter(Rect r)
    {
        float bw = PixelGUI.Px(150f), bh = r.height;

        if (WeekendLedger.WeekendOver)
        {
            if (PixelGUI.Button(new Rect(r.x, r.y, bw, bh), "START NEXT WEEKEND"))
            {
                WeekendDirector.NextWeekend();
                _viewing = WeekendSlot.FridayAM;
                _selected = 0;
                Toast("New weekend. Friday morning, 08:00.");
            }
        }
        else
        {
            var next = (int)WeekendLedger.CurrentSlot + 1;
            string label = next < WeekendSlots.Count
                ? "SKIP TO " + WeekendSlots.ShortLabel((WeekendSlot)next)
                : "END THE WEEKEND";
            if (PixelGUI.Button(new Rect(r.x, r.y, bw, bh), label))
            {
                WeekendLedger.AdvanceSlot();
                _viewing = WeekendLedger.WeekendOver ? WeekendSlot.SundayPM : WeekendLedger.CurrentSlot;
                _selected = 0;
                Toast(WeekendLedger.WeekendOver
                    ? "That is the weekend."
                    : "Now " + WeekendSlots.Label(WeekendLedger.CurrentSlot).ToLowerInvariant() + ".");
            }
        }

        if (PixelGUI.Tab(new Rect(r.xMax - PixelGUI.Px(70f), r.y, PixelGUI.Px(70f), bh), "CLOSE", false))
            Close();

        // Which championship you are entered in is a career decision, so it can only be changed on a weekend
        // nothing has happened in yet - once Friday morning has been spent, you are in the series you are in.
        float switchW = PixelGUI.Px(120f);
        float switchX = r.xMax - PixelGUI.Px(74f) - switchW;
        if (WeekendLedger.DoneCount == 0 && WeekendLedger.MissedCount == 0)
        {
            if (PixelGUI.Tab(new Rect(switchX, r.y, switchW, bh),
                             "SERIES: " + SeriesCatalog.ShortCode(SeriesCatalog.PlayerSeries), false))
            {
                var all = SeriesCatalog.All;
                int i = System.Array.IndexOf(all, SeriesCatalog.PlayerSeries);
                SeriesCatalog.PlayerSeries = all[(i + 1) % all.Length];
                WeekendDirector.Invalidate();
                _ = WeekendDirector.Timetable;
                _viewing = WeekendSlot.FridayAM;
                _selected = 0;
                Toast("Entered in the " + SeriesCatalog.Name(SeriesCatalog.PlayerSeries) + ".");
            }
        }

        var foot = PixelGUI.Footer;
        var a = foot.alignment;
        foot.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(r.x + bw, r.y, switchX - r.x - bw, bh), "W/S SELECT  ·  F10 CLOSE", foot);
        foot.alignment = a;
    }

    // ------------------------------------------------------------------ input helpers

    static int UpDownPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return 0;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) return -1;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) return 1;
        return 0;
    }

    static float Paragraph(Rect r, string text, GUIStyle style)
    {
        var content = new GUIContent(text);
        float h = style.CalcHeight(content, r.width);
        GUI.Label(new Rect(r.x, r.y, r.width, h), content, style);
        return h;
    }
}
