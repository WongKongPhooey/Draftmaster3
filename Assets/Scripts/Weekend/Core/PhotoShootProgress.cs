namespace Draftmaster.Weekend
{
    // Where this weekend's sponsor photo shoot has got to — the cue for the rival brand's rep outside the
    // winner's circle (SponsorPoachBeat), whose whole pitch is that they watched the driver work that room.
    public enum PhotoShootState { Waiting, Done, NeverHappening }

    public static class PhotoShootProgress
    {
        // Any shoot on the sheet being done is the cue — Friday's stills or Saturday's dealer photos, whichever
        // the player actually turned up to. A sheet whose shoots have all gone by unattended never plays the
        // beat at all, because the whole pitch is about a room the player was not in.
        //
        // Two things take the shoots off the sheet entirely, and both have to be read before "no shoot booked"
        // is taken to mean the weekend never had one — otherwise an empty sheet sends the rep straight out to
        // meet the player, impressed by a room they never walked into:
        //   - turning the shoots down at the team meeting (WaiveSponsorExtras);
        //   - a career mode that leaves sponsor duties off the sheet (CareerModes.Keeps) — Minimal and
        //     Driving Only. A shoot already done before the mode changed stays on the sheet and still counts.
        public static PhotoShootState Of(WeekendTimetable timetable, bool extrasWaived, CareerMode mode)
        {
            if (extrasWaived) return PhotoShootState.NeverHappening;
            if (timetable == null) return PhotoShootState.Done;

            bool booked = false, pending = false;
            foreach (var a in timetable.Activities)
            {
                if (a == null || a.kind != ActivityKind.PhotoShoot) continue;
                booked = true;
                if (WeekendLedger.IsDone(a.id)) return PhotoShootState.Done;
                if (!WeekendLedger.IsMissed(a.id)) pending = true;
            }
            if (!booked)
                return CareerModes.Keeps(mode, ActivityKind.PhotoShoot)
                    ? PhotoShootState.Done
                    : PhotoShootState.NeverHappening;
            return pending ? PhotoShootState.Waiting : PhotoShootState.NeverHappening;
        }
    }
}
