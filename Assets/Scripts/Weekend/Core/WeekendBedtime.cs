namespace Draftmaster.Weekend
{
    // Where Friday and Saturday end: in bed.
    //
    // A half-day that runs out of things to do rolls straight on to the next one — finish the morning's last
    // obligation and the afternoon's first is already on the map. That is right at lunchtime and wrong at
    // night. The last thing on a Friday or Saturday evening is done, and the next thing on the sheet is the
    // following morning: a driver goes back to the motorhome and sleeps, and the day after starts the way the
    // first one did, with the alarm going off in the dark.
    //
    // So on those two evenings the sheet does not roll itself over. The weekend sits in "bedtime" until the
    // player taps the bed in their motorhome, and sleeping is what moves the clock to the next morning.
    // Sunday evening is the end of the weekend, not a night, and has its own way out.
    //
    // Pure, and derived entirely from the ledger's clock and the sheet, so it needs no memory of its own and
    // survives every scene reload a weekend is made of.
    public static class WeekendBedtime
    {
        // The half-days that end with the player going to sleep rather than the sheet rolling over.
        public static bool EndsInBed(WeekendSlot slot) =>
            slot == WeekendSlot.FridayPM || slot == WeekendSlot.SaturdayPM;

        // Is the evening over and the bed the only thing left to go to? True once nothing worth walking to
        // remains in a Friday or Saturday afternoon. The hour off does not count — "Rest" is what is left when
        // there is nothing on, not a reason to stay up.
        public static bool Due()
        {
            if (WeekendLedger.Timetable == null || WeekendLedger.WeekendOver) return false;
            if (!EndsInBed(WeekendLedger.CurrentSlot)) return false;
            return WeekendSchedulePlan.NextWorthDoing() == null;
        }

        // Go to sleep: the rest of the evening is given up (there is nothing left in it by definition) and the
        // clock opens on the next morning. False, and nothing moved, if it is not bedtime.
        public static bool Sleep()
        {
            if (!Due()) return false;
            WeekendLedger.AdvanceSlot();
            return true;
        }
    }
}
