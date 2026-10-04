using System.Collections.Generic;
using Draftmaster.Controls;
using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.InputSystem;

// The weekend timetable on the phone, drawn as a calendar: a white page, one cell an hour down the day,
// and every booking laid on it as a block at its own time and length. Two things booked over the same
// hour sit side by side in that hour, which is the clash the schedule screen makes you choose between.
//
// Tap a booking (or pick it with left/right and confirm) for a card over the page: what it is, where and
// when, what it pays or costs, and SKIP TO HERE — the clock jumps to it, with everything in between left
// unattended and charged the way the half-day skip charges it (WeekendDirector.SkipTo). You cannot skip past
// one of your own driving sessions you have not run; the card says so instead of offering the button.
//
// This is the weekend's one schedule screen in play: the pause menu's WEEKEND SCHEDULE row was folded into it.
public class PhoneScheduleApp : PhoneApp
{
    public override string Id => "schedule";
    public override string TileName => "SCHEDULE";
    public override string TileSubtitle => "What's on today";
    public override Color Accent => PixelGUI.Gold;

    // The page. A calendar is dark-on-light or it isn't a calendar, so this app is the one place on the
    // phone that leaves the kit's glass behind.
    static readonly Color Paper = new Color(0.94f, 0.93f, 0.89f);
    static readonly Color PaperLine = new Color(0.78f, 0.77f, 0.72f);
    static readonly Color PaperGutter = new Color(0.88f, 0.87f, 0.83f);

    int _day = -1;             // 0 Friday, 1 Saturday, 2 Sunday. -1 = follow the clock.

    WeekendActivity _selected;  // the booking whose card is open, null when none is
    string _note = "";          // why the last SKIP TO HERE did not go
    string _highlightId;        // keyboard / pad cursor on the calendar, by activity id

    // The badge is the count of obligations still ahead of the clock today that would cost you something.
    public override int Badge
    {
        get
        {
            var t = WeekendDirector.Timetable;
            if (t == null || WeekendLedger.WeekendOver) return 0;
            int n = 0;
            foreach (var a in t.InSlot(WeekendLedger.CurrentSlot))
                if (a.mandatory && WeekendLedger.Status(a) == WeekendLedger.State.Available) n++;
            return n;
        }
    }

    public override void OnOpen()
    {
        _day = -1;
        _selected = null;
        _highlightId = null;
        _note = "";
    }

    // Back closes the card before it closes the app.
    public override bool Back()
    {
        if (_selected == null) return false;
        _selected = null;
        return true;
    }

    int Day => _day >= 0 ? _day : Mathf.Clamp((int)WeekendLedger.CurrentSlot / 2, 0, 2);
    WeekendSlot Morning => (WeekendSlot)(Day * 2);
    WeekendSlot Afternoon => (WeekendSlot)(Day * 2 + 1);

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        var t = WeekendDirector.Timetable;
        if (t == null) return Empty(x, y, w, "No weekend running.");

        // --- where the clock is ---
        y += Row(x, y, w, string.IsNullOrEmpty(t.trackName) ? "The track" : t.trackName,
                 WeekendLedger.WeekendOver ? "-" : WeekendSlots.Clock(WeekendLedger.ClockMinute), PixelGUI.Gold);
        y += Row(x, y, w, SeriesCatalog.Nickname(SeriesCatalog.PlayerSeries) + " entry",
                 "ROUND " + (t.weekendId + 1), null, dim: true);
        y += PixelGUI.Px(3f);

        y += DayTabs(x, y, w);
        y += PixelGUI.Px(3f);
        y += Page(x, y, w, t);
        y += PixelGUI.Px(6f);

        // --- the meters ---
        y += Section(x, y, w, "STANDING");
        y += Meter(x, y, w, "FANS", Draftmaster.Fans.FanAppeal.Normalised,
                   Mathf.RoundToInt(Draftmaster.Fans.FanAppeal.Value) + "/100", PixelGUI.Confirm);
        y += Meter(x, y, w, "SPONSOR", Signed01(WeekendLedger.SponsorMood), Signed(WeekendLedger.SponsorMood),
                   MoodColour(WeekendLedger.SponsorMood));
        y += Meter(x, y, w, "TEAM", Signed01(WeekendLedger.TeamMorale), Signed(WeekendLedger.TeamMorale),
                   MoodColour(WeekendLedger.TeamMorale));
        y += Meter(x, y, w, "PRESS", Signed01(WeekendLedger.MediaStanding), Signed(WeekendLedger.MediaStanding),
                   MoodColour(WeekendLedger.MediaStanding));
        y += Meter(x, y, w, "SETUP", WeekendLedger.SetupGain,
                   Mathf.RoundToInt(WeekendLedger.SetupGain * 100f) + "%", PixelGUI.Info);
        y += Row(x, y, w, "WEEKEND",
                 (WeekendLedger.NetEarnings >= 0 ? "+" : "-") + PlayerWallet.Format(Mathf.Abs(WeekendLedger.NetEarnings)),
                 WeekendLedger.NetEarnings >= 0 ? PixelGUI.Gold : PixelGUI.Danger);
        y += PixelGUI.Px(5f);

        // --- what the weekend has been about ---
        var lines = WeekendLedger.Headlines;
        if (lines.Count > 0)
        {
            y += Section(x, y, w, "THIS WEEKEND");
            for (int i = lines.Count - 1, shown = 0; i >= 0 && shown < 4; i--, shown++)
                y += Body(x, y, w, "· " + lines[i], PixelGUI.TextDim);
        }

        y += Body(x, y, w, InputGlyphs.UsingTouch ? "Tap a booking for details and to skip to it."
                                                   : "Click a booking, or use left/right and confirm, for details.",
                  PixelGUI.TextDisabled);
        return y - y0 + PixelGUI.Px(6f);
    }

    // FRI / SAT / SUN. The day the clock is on is gold; a day already spent is dimmed but still readable,
    // because looking back at what you missed is half the point of a calendar.
    float DayTabs(float x, float y, float w)
    {
        float h = RowH + PixelGUI.Px(2f);
        int today = Mathf.Clamp((int)WeekendLedger.CurrentSlot / 2, 0, 2);
        float tw = w / 3f;

        for (int d = 0; d < 3; d++)
        {
            var r = new Rect(x + d * tw, y, tw - PixelGUI.Px(1f), h);
            bool shown = d == Day;
            PixelGUI.Fill(r, shown ? PixelGUI.Gold : PixelGUI.Plate);
            if (_selected == null && Pressed(r)) _day = d;

            Color text = shown ? PixelGUI.Ink : d < today ? PixelGUI.TextDisabled : PixelGUI.Text;
            PhoneStyles.Label(r, WeekendSlots.DayShort((WeekendSlot)(d * 2)), PhoneStyles.Data, text,
                              TextAnchor.MiddleCenter);
        }
        return h;
    }

    // One day on paper: an hour a cell, every booking on it as a block.
    float Page(float x, float y, float w, WeekendTimetable t)
    {
        int openMin = WeekendSlots.OpensAt(Morning);
        int closeMin = WeekendSlots.ClosesAt(Afternoon);
        int hours = Mathf.Max(1, Mathf.CeilToInt((closeMin - openMin) / 60f));

        float hourH = PixelGUI.Px(13f);
        float gutter = PixelGUI.Px(13f);
        float pageH = hours * hourH;
        var page = new Rect(x, y, w, pageH);

        PixelGUI.Fill(page, Paper);
        PixelGUI.Fill(new Rect(page.x, page.y, gutter, page.height), PaperGutter);

        for (int i = 0; i <= hours; i++)
        {
            float ly = page.y + i * hourH;
            PixelGUI.Fill(new Rect(page.x, ly, page.width, PixelGUI.Px(1f)), PaperLine);
            if (i == hours) break;
            PhoneStyles.Label(new Rect(page.x + PixelGUI.Px(1f), ly, gutter - PixelGUI.Px(2f), hourH),
                              ((openMin / 60 + i) % 24).ToString("00"), PhoneStyles.InkDim);
        }

        var day = new List<WeekendActivity>();
        day.AddRange(t.InSlot(Morning));
        day.AddRange(t.InSlot(Afternoon));
        day.Sort((a, b) => a.startMinute.CompareTo(b.startMinute));

        var lane = new Rect(page.x + gutter + PixelGUI.Px(1f), page.y,
                            page.width - gutter - PixelGUI.Px(2f), page.height);

        foreach (var p in Layout(day))
            DrawBlock(lane, p, openMin, closeMin);

        // Where the clock actually is, if it is on this day.
        int todayIdx = Mathf.Clamp((int)WeekendLedger.CurrentSlot / 2, 0, 2);
        if (!WeekendLedger.WeekendOver && todayIdx == Day)
        {
            float ny = page.y + Mathf.Clamp01((WeekendLedger.ClockMinute - openMin) / (float)(closeMin - openMin)) * pageH;
            PixelGUI.Fill(new Rect(page.x, ny, page.width, PixelGUI.Px(1f)), PixelGUI.Danger);
            PixelGUI.Fill(new Rect(page.x, ny - PixelGUI.Px(1f), PixelGUI.Px(3f), PixelGUI.Px(3f)), PixelGUI.Danger);
        }

        return pageH;
    }

    void DrawBlock(Rect lane, Placed p, int openMin, int closeMin)
    {
        var a = p.a;
        float span = Mathf.Max(1f, closeMin - openMin);
        float top = lane.y + Mathf.Clamp01((a.startMinute - openMin) / span) * lane.height;
        float bottom = lane.y + Mathf.Clamp01((a.startMinute + Mathf.Max(15, a.minutes) - openMin) / span) * lane.height;
        float h = Mathf.Max(RowH, bottom - top);

        float colW = lane.width / Mathf.Max(1, p.cols);
        var r = new Rect(lane.x + p.col * colW, top, colW - PixelGUI.Px(1f), h - PixelGUI.Px(1f));

        var state = WeekendLedger.Status(a);
        Color kind = state == WeekendLedger.State.Missed ? PixelGUI.Danger
                   : state == WeekendLedger.State.Done ? PixelGUI.Confirm
                   : KindColour(a.kind);

        PixelGUI.Fill(r, new Color(kind.r, kind.g, kind.b, 0.22f));
        PixelGUI.Fill(new Rect(r.x, r.y, PixelGUI.Px(2f), r.height), kind);
        if (a.id == _highlightId && !InputGlyphs.UsingTouch) Outline(r, PixelGUI.Ink);

        // The page is drawn before the card, so it would take the card's taps; while a card is open it takes none.
        if (_selected == null && Pressed(r)) Open(a);

        var text = new Rect(r.x + PixelGUI.Px(4f), r.y, r.width - PixelGUI.Px(5f), RowH);
        string title = Fit((state == WeekendLedger.State.Done ? "· " : "") + a.title,
                           PhoneStyles.InkData, text.width);
        PhoneStyles.Label(text, title, PhoneStyles.InkData,
                          state == WeekendLedger.State.Missed || state == WeekendLedger.State.Past
                              ? new Color(0.45f, 0.42f, 0.42f) : PixelGUI.Ink);

        // A block only tall enough for its name says just its name. Anything taller gets the time it
        // starts and what kind of thing it is, which is what a calendar cell is for.
        if (h >= RowH * 2f)
            PhoneStyles.Label(new Rect(text.x, text.yMax, text.width, RowH),
                              WeekendSlots.Clock(a.startMinute) + " " + ActivityKinds.Tag(a.kind),
                              PhoneStyles.InkDim);

        if (a.mandatory && state == WeekendLedger.State.Available)
            PixelGUI.Fill(new Rect(r.xMax - PixelGUI.Px(3f), r.y, PixelGUI.Px(2f), PixelGUI.Px(2f)), PixelGUI.Danger);
    }

    // As much of the title as the block is actually wide enough for, measured in the font it is drawn in,
    // so a two-up clash cell shows sixteen-odd letters instead of a fixed character count's eleven.
    static string Fit(string s, GUIStyle style, float width)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (style.CalcSize(new GUIContent(s)).x <= width) return s;
        int lo = 0, hi = s.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (style.CalcSize(new GUIContent(s.Substring(0, mid))).x <= width) lo = mid;
            else hi = mid - 1;
        }
        return s.Substring(0, lo).TrimEnd();
    }

    // ------------------------------------------------------------------ the card

    void Open(WeekendActivity a)
    {
        _selected = a;
        _highlightId = a.id;
        _note = "";
    }

    void TrySkip()
    {
        if (_selected == null) return;
        if (WeekendDirector.SkipTo(_selected, out string reason)) { _selected = null; return; }
        _note = reason;
    }

    public override void DrawOverlay(Rect view)
    {
        var a = _selected;
        if (a == null) return;

        // The page dims behind the card; the card sits in the middle of whatever is on screen.
        PixelGUI.Fill(view, new Color(0f, 0f, 0f, 0.55f));

        float pad = PixelGUI.Px(5f);
        float cw = view.width - PixelGUI.Px(6f);
        float iw = cw - pad * 2f;

        // Laid out once to measure, then for real — the card is as tall as what it says.
        float h = CardBody(0f, 0f, iw, a, measure: true) + pad * 2f;
        var card = new Rect(view.x + PixelGUI.Px(3f), view.y + Mathf.Max(PixelGUI.Px(3f), (view.height - h) * 0.5f), cw, h);
        Plate(card, KindColour(a.kind));
        CardBody(card.x + pad, card.y + pad, iw, a, measure: false);
    }

    float CardBody(float x, float y, float w, WeekendActivity a, bool measure)
    {
        float y0 = y;
        var state = WeekendLedger.Status(a);

        if (measure) GUI.enabled = false;   // the measuring pass must not take a tap
        y += Row(x, y, w, a.title, ActivityKinds.Tag(a.kind), KindColour(a.kind));
        y += Row(x, y, w, WeekendSlots.DayShort(a.slot) + " " + a.Clock, a.location ?? "", null, dim: true);
        y += PixelGUI.Px(2f);
        y += Body(x, y, w, a.subtitle, PixelGUI.Text);

        // What it is worth, and what not going costs.
        if (a.appearanceFee > 0) y += Body(x, y, w, "Pays " + PlayerWallet.Format(a.appearanceFee) + " for turning up.", PixelGUI.Gold);
        if (ActivityKinds.IsOptional(a.kind))
            y += Body(x, y, w, "Optional. Watching scouts the " + SeriesCatalog.Nickname(a.series) +
                               " field's stats for your laptop.", PixelGUI.Info);
        else if (a.mandatory)
        {
            string cost = a.skipMoneyPenalty > 0 ? "Missing it costs " + PlayerWallet.Format(a.skipMoneyPenalty) + "."
                                                 : "Missing it counts against you.";
            y += Body(x, y, w, "Obligation. " + cost + (string.IsNullOrEmpty(a.skipReason) ? "" : " " + a.skipReason),
                      PixelGUI.Danger);
        }

        string status = state switch
        {
            WeekendLedger.State.Done => "DONE",
            WeekendLedger.State.Missed => "MISSED",
            WeekendLedger.State.Past => "GONE BY",
            _ => WeekendAppointment.IsPending(a) ? "BOOKED - YOUR NEXT STOP" : "",
        };
        if (status.Length > 0) y += Row(x, y, w, status, "", state == WeekendLedger.State.Missed ? PixelGUI.Danger : PixelGUI.Confirm);
        y += PixelGUI.Px(3f);

        // SKIP TO HERE, or why not.
        float bh = RowH + PixelGUI.Px(4f);
        if (WeekendSkip.CanSkipTo(a, out string why))
        {
            int leaving = LeftBehind(a);
            if (leaving > 0)
                y += Body(x, y, w, $"Skipping leaves {leaving} booking{(leaving == 1 ? "" : "s")} before it unattended.",
                          PixelGUI.TextDim);
            var button = new Rect(x, y, w, bh);
            PixelGUI.Fill(button, PixelGUI.Gold);
            string label = InputGlyphs.UsingTouch ? "SKIP TO HERE"
                         : InputGlyphs.UsingGamepad ? InputGlyphs.PadName(PadBindings.Confirm) + "  SKIP TO HERE"
                         : "ENTER  SKIP TO HERE";
            PhoneStyles.Label(button, label, PhoneStyles.Heading, PixelGUI.Ink, TextAnchor.MiddleCenter);
            if (!measure && Pressed(button)) TrySkip();
            y += bh + PixelGUI.Px(2f);
        }
        else if (StillAhead(a))
            y += Body(x, y, w, why, PixelGUI.TextDisabled);

        if (_note.Length > 0) y += Body(x, y, w, _note, PixelGUI.Danger);

        var close = new Rect(x, y, w, bh);
        PixelGUI.Fill(close, PixelGUI.Plate);
        PhoneStyles.Label(close, "CLOSE", PhoneStyles.Data, PixelGUI.Text, TextAnchor.MiddleCenter);
        if (!measure && Pressed(close)) _selected = null;
        y += bh;

        if (measure) GUI.enabled = true;
        return y - y0;
    }

    // How many bookings between now and `target` the skip would leave behind.
    static int LeftBehind(WeekendActivity target)
    {
        var t = WeekendDirector.Timetable;
        if (t == null) return 0;
        int at = (int)target.slot * 1440 + target.startMinute;
        int n = 0;
        foreach (var a in t.Activities)
        {
            if (a == target || a.kind == ActivityKind.Rest || !StillAhead(a)) continue;
            if ((int)a.slot * 1440 + a.startMinute < at) n++;
        }
        return n;
    }

    // Not done, not missed, and today-later or a later half-day: something the clock can still reach.
    static bool StillAhead(WeekendActivity a)
    {
        var st = WeekendLedger.Status(a);
        return st == WeekendLedger.State.Available || st == WeekendLedger.State.Later;
    }

    static void Outline(Rect r, Color c)
    {
        float t = PixelGUI.Px(1f);
        PixelGUI.Fill(new Rect(r.x, r.y, r.width, t), c);
        PixelGUI.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        PixelGUI.Fill(new Rect(r.x, r.y, t, r.height), c);
        PixelGUI.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // ------------------------------------------------------------------ keys and pad

    public override void HandleKeys(Keyboard kb)
    {
        bool confirm = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
        if (_selected != null) { if (confirm) TrySkip(); return; }
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Step(1);
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Step(-1);
        if (confirm) OpenHighlighted();
    }

    public override void HandlePad(Gamepad pad)
    {
        bool confirm = PadInput.Control(pad, PadBindings.Confirm).wasPressedThisFrame;
        if (_selected != null) { if (confirm) TrySkip(); return; }
        if (pad.dpad.right.wasPressedThisFrame) Step(1);
        if (pad.dpad.left.wasPressedThisFrame) Step(-1);
        if (confirm) OpenHighlighted();
    }

    // Every booking of the weekend in time order; the cursor walks it and the day tab follows.
    List<WeekendActivity> Ordered()
    {
        var list = new List<WeekendActivity>();
        var t = WeekendDirector.Timetable;
        if (t == null) return list;
        foreach (var a in t.Activities) if (a.kind != ActivityKind.Rest) list.Add(a);
        list.Sort((a, b) => a.slot != b.slot ? a.slot.CompareTo(b.slot) : a.startMinute.CompareTo(b.startMinute));
        return list;
    }

    void Step(int dir)
    {
        var list = Ordered();
        if (list.Count == 0) return;
        int i = list.FindIndex(a => a.id == _highlightId);
        if (i < 0)
        {
            // Start from the next thing still to come rather than Friday 08:00.
            i = list.FindIndex(StillAhead);
            if (i < 0) i = 0;
        }
        else i = Mathf.Clamp(i + dir, 0, list.Count - 1);
        _highlightId = list[i].id;
        _day = Mathf.Clamp((int)list[i].slot / 2, 0, 2);
    }

    void OpenHighlighted()
    {
        if (_highlightId == null) { Step(0); return; }
        var t = WeekendDirector.Timetable;
        var a = t != null ? t.ById(_highlightId) : null;
        if (a != null) Open(a);
    }

    // ------------------------------------------------------------------ overlap packing

    struct Placed
    {
        public WeekendActivity a;
        public int col, cols;
    }

    // Anything whose hour overlaps somebody else's shares the width with it. Bookings are walked in start
    // order and clustered while they keep overlapping, so a clash is two columns and a triple is three.
    static List<Placed> Layout(List<WeekendActivity> items)
    {
        var placed = new List<Placed>();
        int i = 0;
        while (i < items.Count)
        {
            var cluster = new List<WeekendActivity> { items[i] };
            int clusterEnd = End(items[i]);
            int j = i + 1;
            for (; j < items.Count && items[j].startMinute < clusterEnd; j++)
            {
                cluster.Add(items[j]);
                clusterEnd = Mathf.Max(clusterEnd, End(items[j]));
            }

            var colEnds = new List<int>();
            var cols = new int[cluster.Count];
            for (int k = 0; k < cluster.Count; k++)
            {
                int c = 0;
                while (c < colEnds.Count && colEnds[c] > cluster[k].startMinute) c++;
                if (c == colEnds.Count) colEnds.Add(End(cluster[k]));
                else colEnds[c] = End(cluster[k]);
                cols[k] = c;
            }

            for (int k = 0; k < cluster.Count; k++)
                placed.Add(new Placed { a = cluster[k], col = cols[k], cols = colEnds.Count });

            i = j;
        }
        return placed;
    }

    static int End(WeekendActivity a) => a.startMinute + Mathf.Max(15, a.minutes);

    // What sort of thing it is, at a glance — the same grouping ActivityKinds.Tag prints.
    static Color KindColour(ActivityKind k)
    {
        if (ActivityKinds.IsOnTrack(k)) return PixelGUI.Gold;
        if (ActivityKinds.IsSpectate(k)) return PixelGUI.Info;
        if (ActivityKinds.IsMedia(k)) return new Color(0.55f, 0.42f, 0.72f);
        if (ActivityKinds.IsFanDuty(k)) return PixelGUI.Confirm;
        if (ActivityKinds.IsSponsorDuty(k)) return new Color(0.85f, 0.55f, 0.25f);
        if (ActivityKinds.IsTeam(k)) return new Color(0.30f, 0.55f, 0.62f);
        if (ActivityKinds.IsCeremony(k)) return PixelGUI.Danger;
        return new Color(0.45f, 0.45f, 0.48f);
    }

    static float Signed01(float v) => Mathf.Clamp01((v + 100f) / 200f);
    static string Signed(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);
    static Color MoodColour(float v) => v >= 15f ? PixelGUI.Confirm : v <= -15f ? PixelGUI.Danger : PixelGUI.TextDim;
}
