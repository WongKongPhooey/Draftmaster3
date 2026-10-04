using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // Skipping ahead off the schedule sheet: give up the rest of the half-day and go back to the motorhome.
    //
    // The sheet has always let the player roll the clock on to the next half-day, and an obligation skipped
    // that way cost what its no-show penalty said. Anything that was not an obligation cost nothing at all,
    // so skipping straight past a morning of optional media, signings and your own practice was free. It is
    // not free any more: everything the skip walks past takes a small toll from the meter it belongs to —
    // the crew who had the car ready, the reporters who were waiting, the queue at the fence — on top of the
    // no-show penalty an obligation already carries.
    //
    // The report is what the player is shown when they land back in the motorhome: what they skipped and
    // what the weekend's meters did about it, measured off the ledger before and after rather than added up
    // here, so the obligations' own penalties are counted exactly as the sweep applied them.
    public static class WeekendSkip
    {
        public class Report
        {
            public WeekendSlot from;        // the half-day that was given up
            public bool endedWeekend;       // the skip walked off the end of Sunday afternoon
            public WeekendSlot to;          // where the clock now is (meaningless when endedWeekend)

            // Everything the skip left unattended, in timetable order. Rest windows are not listed — giving
            // up an hour off is not skipping anything.
            public readonly List<WeekendActivity> skipped = new();

            // The net movement the skip caused: money, fan appeal, the three moods, setup.
            public WeekendOutcome cost;

            public bool SkippedAnything => skipped.Count > 0;
        }

        // What leaving a booking unattended costs on top of its no-show penalty. Obligations already pay their
        // own penalty when the sweep marks them missed, so this is only taken for the optional ones.
        public static WeekendOutcome TollFor(ActivityKind kind)
        {
            var o = WeekendOutcome.Nothing;
            switch (kind)
            {
                case ActivityKind.Practice:
                case ActivityKind.Qualifying:
                    o.teamMorale = -6f;           // the car was ready and the crew stood by it
                    break;
                case ActivityKind.Race:
                    o.teamMorale = -10f;
                    o.sponsorMood = -10f;
                    break;
                case ActivityKind.TeamBriefing:
                case ActivityKind.Debrief:
                case ActivityKind.Orientation:
                    o.teamMorale = -4f;
                    break;
                case ActivityKind.DriversMeeting:
                case ActivityKind.DriverIntros:
                    o.mediaStanding = -3f;
                    break;
                case ActivityKind.PressConference:
                case ActivityKind.MediaHit:
                    o.mediaStanding = -5f;
                    break;
                case ActivityKind.Autographs:
                case ActivityKind.HaulerParade:
                    o.fanAppeal = -1.5f;
                    break;
                case ActivityKind.SponsorDuty:
                case ActivityKind.PhotoShoot:
                    o.sponsorMood = -6f;
                    break;
                // Somebody else's session and an hour off are nobody's loss but the player's own.
            }
            return o;
        }

        // Give up the rest of the current half-day and move to the next one, as WeekendLedger.AdvanceSlot
        // does, then charge the toll for everything optional it walked past. Returns what was skipped and what
        // it cost. Null when the weekend is already over.
        public static Report SkipHalfDay()
        {
            if (WeekendLedger.WeekendOver) return null;
            var from = WeekendLedger.CurrentSlot;
            return Run(() =>
            {
                WeekendLedger.AdvanceSlot();
                // AdvanceSlot sweeps while the clock is still in the half-day being left, where the player's own
                // sessions are exempt, so those would only be marked missed by whatever swept next. Sweep again
                // from the new half-day (SkipTo the clock's own minute moves nothing) so they land in this report.
                WeekendLedger.SkipTo(WeekendLedger.ClockMinute);
            }, "Skipped " + WeekendSlots.Label(from).ToLowerInvariant());
        }

        // ------------------------------------------------------------------ skip to a booking

        // Can the clock be moved straight to `target` (the phone's SKIP TO HERE)? Only forwards, only to
        // something still to come, and never past one of the player's own driving sessions that has not been
        // run: practice, qualifying and the race are what the weekend is for, so the way past one is to drive
        // it. Skipping TO a session is fine — that is just arriving for it. `reason` says why not, in a line
        // the phone can show.
        public static bool CanSkipTo(WeekendActivity target, out string reason)
        {
            reason = "";
            var t = WeekendLedger.Timetable;
            if (target == null || t == null) { reason = "Nothing to skip to."; return false; }
            if (WeekendLedger.WeekendOver) { reason = "The weekend is over."; return false; }
            if (WeekendLedger.IsDone(target.id)) { reason = "Already done."; return false; }
            if (WeekendLedger.IsMissed(target.id)) { reason = "Already missed."; return false; }

            int clock = Position(WeekendLedger.CurrentSlot, WeekendLedger.ClockMinute);
            int at = Position(target.slot, target.startMinute);
            if (at < clock) { reason = "That has already started."; return false; }
            if (at == clock) { reason = "That's on now."; return false; }

            foreach (var a in t.Activities)
            {
                if (a == null || a == target || !a.IsOnTrack) continue;
                if (WeekendLedger.IsDone(a.id) || WeekendLedger.IsMissed(a.id)) continue;
                if (Position(a.slot, a.startMinute) >= at) continue;
                reason = $"You can't skip past {a.title}. Drive it first.";
                return false;
            }
            return true;
        }

        // Move the clock to `target`'s start: every half-day in between is given up and everything the clock
        // walks past is left unattended, charged exactly as SkipHalfDay charges it. Null when it is not allowed.
        public static Report SkipToActivity(WeekendActivity target)
        {
            if (!CanSkipTo(target, out _)) return null;
            return Run(() =>
            {
                for (int guard = WeekendSlots.Count; guard > 0 && !WeekendLedger.WeekendOver &&
                     (int)WeekendLedger.CurrentSlot < (int)target.slot; guard--)
                    WeekendLedger.AdvanceSlot();
                WeekendLedger.SkipTo(target.startMinute);
            }, "Skipped to " + target.title.ToLowerInvariant());
        }

        // Minutes since Friday morning opened, so two bookings on different days compare as one number.
        static int Position(WeekendSlot slot, int minute) => (int)slot * 24 * 60 + minute;

        // ------------------------------------------------------------------ the shared half

        // Run a move of the clock and report what it left behind: everything open before it that is missed
        // after it, the toll on the optional ones, and the net change to every meter.
        static Report Run(System.Action move, string headlineLead)
        {
            var report = new Report { from = WeekendLedger.CurrentSlot };
            var t = WeekendLedger.Timetable;

            // Everything still open before the skip. What of it is missed afterwards is what the skip cost.
            var open = new List<WeekendActivity>();
            if (t != null)
                foreach (var a in t.Activities)
                    if (!WeekendLedger.IsDone(a.id) && !WeekendLedger.IsMissed(a.id)) open.Add(a);

            float sponsor = WeekendLedger.SponsorMood, team = WeekendLedger.TeamMorale;
            float media = WeekendLedger.MediaStanding, setup = WeekendLedger.SetupGain;
            float appeal = Draftmaster.Fans.FanAppeal.Value;
            int net = WeekendLedger.NetEarnings;

            move();

            var toll = WeekendOutcome.Nothing;
            foreach (var a in open)
            {
                if (!WeekendLedger.IsMissed(a.id) || a.kind == ActivityKind.Rest) continue;
                report.skipped.Add(a);
                if (a.mandatory) continue;

                var cost = TollFor(a.kind);
                toll.fanAppeal += cost.fanAppeal;
                toll.sponsorMood += cost.sponsorMood;
                toll.teamMorale += cost.teamMorale;
                toll.mediaStanding += cost.mediaStanding;
            }
            report.skipped.Sort((a, b) => a.slot != b.slot ? a.slot.CompareTo(b.slot) : a.startMinute.CompareTo(b.startMinute));

            if (report.SkippedAnything)
            {
                toll.headline = headlineLead + ": " +
                                report.skipped.Count + (report.skipped.Count == 1 ? " booking" : " bookings") +
                                " left unattended.";
                WeekendLedger.Apply(toll);
            }

            report.endedWeekend = WeekendLedger.WeekendOver;
            report.to = WeekendLedger.CurrentSlot;
            report.cost = new WeekendOutcome
            {
                money = WeekendLedger.NetEarnings - net,
                fanAppeal = Draftmaster.Fans.FanAppeal.Value - appeal,
                sponsorMood = WeekendLedger.SponsorMood - sponsor,
                teamMorale = WeekendLedger.TeamMorale - team,
                mediaStanding = WeekendLedger.MediaStanding - media,
                setupGain = WeekendLedger.SetupGain - setup,
            };
            return report;
        }
    }
}
