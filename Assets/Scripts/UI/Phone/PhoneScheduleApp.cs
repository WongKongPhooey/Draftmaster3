using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The weekend timetable on the phone: what is next, what is left today, and how the four meters are doing.
//
// Read-only on purpose. The phone is what a driver glances at walking across the paddock; committing to a
// booking is the schedule screen (F10), which is a room with a door you close behind you.
public class PhoneScheduleApp : PhoneApp
{
    public override string Id => "schedule";
    public override string TileName => "SCHEDULE";
    public override string TileSubtitle => "What's on today";
    public override Color Accent => PixelGUI.Gold;

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

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        var t = WeekendDirector.Timetable;
        if (t == null) return Empty(x, y, w, "No weekend running.");

        // --- where the clock is ---
        y += Section(x, y, w, WeekendLedger.WeekendOver ? "WEEKEND OVER" : WeekendSlots.Label(WeekendLedger.CurrentSlot));
        y += Row(x, y, w, string.IsNullOrEmpty(t.trackName) ? "The track" : t.trackName,
                 WeekendLedger.WeekendOver ? "-" : WeekendSlots.Clock(WeekendLedger.ClockMinute), PixelGUI.Gold);
        y += Row(x, y, w, SeriesCatalog.Nickname(SeriesCatalog.PlayerSeries) + " entry",
                 "ROUND " + (t.weekendId + 1), null, dim: true);
        y += PixelGUI.Px(6f);

        // --- what is left today ---
        var today = new List<WeekendActivity>();
        foreach (var a in t.InSlot(WeekendLedger.CurrentSlot))
            if (WeekendLedger.Status(a) == WeekendLedger.State.Available) today.Add(a);
        today.Sort((a, b) => a.startMinute.CompareTo(b.startMinute));

        y += Section(x, y, w, "STILL TO COME");
        if (today.Count == 0)
        {
            y += Empty(x, y, w, WeekendLedger.WeekendOver
                ? "That was the weekend. Open the schedule to start the next one."
                : "Nothing left today. Move the day on from the schedule.");
        }
        else
        {
            for (int i = 0; i < today.Count && i < 6; i++)
            {
                var a = today[i];
                Color colour = a.IsOnTrack ? PixelGUI.Gold : a.mandatory ? PixelGUI.Danger : PixelGUI.Text;
                y += Row(x, y, w, WeekendSlots.Clock(a.startMinute) + " " + Trim(a.title, 20),
                         ActivityKinds.Tag(a.kind), colour);
            }
        }
        y += PixelGUI.Px(6f);

        // --- your own sessions, wherever they fall ---
        y += Section(x, y, w, "YOUR SESSIONS");
        y += SessionRow(x, y, w, t.PlayerSession(ActivityKind.Practice), "PRACTICE");
        y += SessionRow(x, y, w, t.PlayerSession(ActivityKind.Qualifying), "QUALIFYING");
        y += SessionRow(x, y, w, t.PlayerSession(ActivityKind.Race), "RACE");
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
        y += PixelGUI.Px(6f);

        // --- what the weekend has been about ---
        var lines = WeekendLedger.Headlines;
        if (lines.Count > 0)
        {
            y += Section(x, y, w, "THIS WEEKEND");
            for (int i = lines.Count - 1, shown = 0; i >= 0 && shown < 4; i--, shown++)
                y += Body(x, y, w, "· " + lines[i], PixelGUI.TextDim);
        }

        y += Body(x, y, w, "F10 opens the full schedule.", PixelGUI.TextDisabled);
        return y - y0 + PixelGUI.Px(6f);
    }

    static float SessionRow(float x, float y, float w, WeekendActivity a, string label)
    {
        if (a == null) return Row(x, y, w, label, "-", PixelGUI.TextDisabled);

        var state = WeekendLedger.Status(a);
        string right = state switch
        {
            WeekendLedger.State.Done => "DONE",
            WeekendLedger.State.Missed => "MISSED",
            _ => WeekendSlots.ShortLabel(a.slot) + " " + WeekendSlots.Clock(a.startMinute),
        };
        Color colour = state switch
        {
            WeekendLedger.State.Done => PixelGUI.Confirm,
            WeekendLedger.State.Missed => PixelGUI.Danger,
            _ => PixelGUI.Gold,
        };
        return Row(x, y, w, label, right, colour);
    }

    static float Signed01(float v) => Mathf.Clamp01((v + 100f) / 200f);
    static string Signed(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v);
    static Color MoodColour(float v) => v >= 15f ? PixelGUI.Confirm : v <= -15f ? PixelGUI.Danger : PixelGUI.TextDim;
}
