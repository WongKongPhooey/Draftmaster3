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

            WeekendLedger.AdvanceSlot();
            // AdvanceSlot sweeps while the clock is still in the half-day being left, where the player's own
            // sessions are exempt, so those would only be marked missed by whatever swept next. Sweep again
            // from the new half-day (SkipTo the clock's own minute moves nothing) so they land in this report.
            WeekendLedger.SkipTo(WeekendLedger.ClockMinute);

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
                toll.headline = "Skipped " + WeekendSlots.Label(report.from).ToLowerInvariant() + ": " +
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
