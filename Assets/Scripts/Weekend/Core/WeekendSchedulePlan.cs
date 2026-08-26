namespace Draftmaster.Weekend
{
    // What the player is due at next.
    //
    // The timetable is a sheet of bookings against a clock; this is the one line of it that matters at any
    // moment — the next thing they can still turn up to. It is what the objective marker points at, what
    // the team liaison tells them on their way out of the motorhome, and what gets booked automatically
    // when the previous thing finishes, so a weekend plays as one thing after another rather than as a
    // screen you have to remember to open.
    //
    // Pure, in the core assembly, so the order the weekend runs in is a rule rather than something the UI
    // decides.
    public static class WeekendSchedulePlan
    {
        // The next booking the player can still do, earliest first: not already done, not missed, and
        // allowed by the ledger's clock. Null when the weekend has nothing left to offer.
        public static WeekendActivity NextUp()
        {
            var timetable = WeekendLedger.Timetable;
            if (timetable == null) return null;

            WeekendActivity best = null;
            foreach (var a in timetable.Activities)
            {
                if (a == null) continue;
                if (WeekendLedger.IsDone(a.id) || WeekendLedger.IsMissed(a.id)) continue;
                if (!WeekendLedger.CanDo(a, out _)) continue;

                if (best == null || Earlier(a, best)) best = a;
            }
            return best;
        }

        // The next booking that is worth walking to — the same list, minus the hour off. "Rest" is what is
        // left when there is nothing on, and pointing a marker at it would be telling the player to go and
        // do nothing.
        public static WeekendActivity NextWorthDoing()
        {
            var timetable = WeekendLedger.Timetable;
            if (timetable == null) return null;

            WeekendActivity best = null;
            foreach (var a in timetable.Activities)
            {
                if (a == null || a.kind == ActivityKind.Rest) continue;
                if (WeekendLedger.IsDone(a.id) || WeekendLedger.IsMissed(a.id)) continue;
                if (!WeekendLedger.CanDo(a, out _)) continue;

                if (best == null || Earlier(a, best)) best = a;
            }
            return best;
        }

        // How the weekend reads it out: "the team's plan meeting at the pit box, 09:30".
        public static string Describe(WeekendActivity a)
        {
            if (a == null) return "nothing until the schedule says so";

            var venue = WeekendVenues.For(a.kind);
            string place = venue == WeekendVenue.None ? "the car" : WeekendVenues.Label(venue);
            return $"{a.title.ToLowerInvariant()} at {place}, {WeekendSlots.Clock(a.startMinute)}";
        }

        static bool Earlier(WeekendActivity a, WeekendActivity than) =>
            (int)a.slot != (int)than.slot ? (int)a.slot < (int)than.slot : a.startMinute < than.startMinute;
    }
}
