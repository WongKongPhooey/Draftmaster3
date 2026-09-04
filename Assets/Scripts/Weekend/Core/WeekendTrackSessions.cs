namespace Draftmaster.Weekend
{
    // Which championship has cars on the circuit at a given moment of the weekend.
    //
    // Three series share the venue for three days, and the track belongs to whichever of them is running:
    // the trucks practise Friday morning, the National cars qualify Saturday morning, Cup races Sunday
    // afternoon. Between those the circuit is cold. That is not an omission — the reason the player spends
    // Friday afternoon in a hospitality tent is that every other driver is doing the same thing, so a lap
    // of cars going past while the player signs hats would be nobody's session.
    //
    // A plain lookup against the timetable rather than a copy of the schedule, so an authored weekend (a
    // plan file that moves a session, drops one, or adds a second practice) is answered correctly with no
    // special case: the sheet is the schedule, and this reads it.
    public static class WeekendTrackSessions
    {
        // A booking with cars on the circuit. Both halves count: the player's own session and the same
        // session belonging to one of the two championships they can only spectate at put exactly the same
        // thing on track — the only difference is which side of the fence the player is on.
        public static bool IsTrackSession(ActivityKind k) =>
            ActivityKinds.IsOnTrack(k) || ActivityKinds.IsSpectate(k);

        // What a spectate booking is a session *of*. "Watch the truck race" and "drive your own race" are
        // both a race; only the seat differs.
        public static ActivityKind SessionKind(ActivityKind k) => k switch
        {
            ActivityKind.SpectatePractice => ActivityKind.Practice,
            ActivityKind.SpectateQualifying => ActivityKind.Qualifying,
            ActivityKind.SpectateRace => ActivityKind.Race,
            _ => k,
        };

        // The session under way at this half-day and clock time, or null when the circuit is cold.
        //
        // Touching end-to-start is not running: a practice that ends at 11:15 is over at 11:15, the same
        // rule WeekendActivity.ClashesWith uses, so a signing session booked to start on the hour the
        // trucks come in does not overlap them.
        public static WeekendActivity RunningAt(WeekendTimetable timetable, WeekendSlot slot, int minuteOfDay)
        {
            if (timetable == null) return null;

            foreach (var a in timetable.Activities)
            {
                if (a == null || a.slot != slot || !IsTrackSession(a.kind)) continue;
                if (minuteOfDay >= a.startMinute && minuteOfDay < a.EndMinute) return a;
            }
            return null;
        }

        // The same question asked of the weekend actually in progress. The timetable's activities are held
        // in chronological order, so two sessions authored over the same hour resolve to the earlier one.
        public static WeekendActivity RunningNow(WeekendTimetable timetable) =>
            WeekendLedger.WeekendOver
                ? null
                : RunningAt(timetable, WeekendLedger.CurrentSlot, WeekendLedger.ClockMinute);
    }
}
