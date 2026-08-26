namespace Draftmaster.Weekend
{
    // The six half-days a race weekend is played through. Each one is a window on the clock, not a single
    // appointment: a half-day holds several things happening at stated times, and the player spends the
    // window on whichever of them they choose. Two things booked over the same hour clash, which is the
    // whole game of the schedule screen — the sponsor suite meet-and-greet is at the same time as the
    // National race, and you cannot be in both rooms.
    public enum WeekendSlot
    {
        FridayAM = 0,
        FridayPM = 1,
        SaturdayAM = 2,
        SaturdayPM = 3,
        SundayAM = 4,
        SundayPM = 5,
    }

    public static class WeekendSlots
    {
        public const int Count = 6;

        public static readonly WeekendSlot[] All =
        {
            WeekendSlot.FridayAM, WeekendSlot.FridayPM,
            WeekendSlot.SaturdayAM, WeekendSlot.SaturdayPM,
            WeekendSlot.SundayAM, WeekendSlot.SundayPM,
        };

        public static string Day(WeekendSlot s) => (int)s switch
        {
            0 or 1 => "FRIDAY",
            2 or 3 => "SATURDAY",
            _ => "SUNDAY",
        };

        // The short form the timetable strip uses across the top of the screen.
        public static string DayShort(WeekendSlot s) => (int)s switch
        {
            0 or 1 => "FRI",
            2 or 3 => "SAT",
            _ => "SUN",
        };

        public static bool IsMorning(WeekendSlot s) => ((int)s & 1) == 0;

        public static string Half(WeekendSlot s) => IsMorning(s) ? "MORNING" : "AFTERNOON";

        public static string Label(WeekendSlot s) => Day(s) + " " + Half(s);

        public static string ShortLabel(WeekendSlot s) => DayShort(s) + " " + (IsMorning(s) ? "AM" : "PM");

        // Window open, minutes from midnight. Mornings start at 08:00; afternoons pick up at noon.
        public static int OpensAt(WeekendSlot s) => IsMorning(s) ? 8 * 60 : 12 * 60;

        // Window close. Friday and Saturday afternoons run long — Friday has the truck race under lights and
        // Saturday has the National race — while Sunday afternoon closes once the Cup race is over.
        public static int ClosesAt(WeekendSlot s) => s switch
        {
            WeekendSlot.FridayPM => 21 * 60,
            WeekendSlot.SaturdayPM => 20 * 60,
            WeekendSlot.SundayPM => 18 * 60,
            _ => 12 * 60,
        };

        public static int Minutes(WeekendSlot s) => ClosesAt(s) - OpensAt(s);

        // The half-day a given clock time falls in. Anything before noon is that day's morning.
        public static WeekendSlot Containing(WeekendSlot day, int minuteOfDay) =>
            minuteOfDay < 12 * 60 ? MorningOf(day) : AfternoonOf(day);

        public static WeekendSlot MorningOf(WeekendSlot anyOnThatDay) => (WeekendSlot)((int)anyOnThatDay & ~1);
        public static WeekendSlot AfternoonOf(WeekendSlot anyOnThatDay) => (WeekendSlot)(((int)anyOnThatDay & ~1) | 1);

        // "14:30". Minutes past midnight, 24h, which is how a timetable sheet is printed.
        public static string Clock(int minuteOfDay)
        {
            int h = (minuteOfDay / 60) % 24;
            int m = minuteOfDay % 60;
            return h.ToString("00") + ":" + m.ToString("00");
        }

        // "9:30 AM". The 24h form is what a timetable prints; this is what a person says out loud, and it
        // is what the spawn card reads under the track name.
        public static string ClockAmPm(int minuteOfDay)
        {
            int h24 = (minuteOfDay / 60) % 24;
            int m = minuteOfDay % 60;
            int h = h24 % 12;
            if (h == 0) h = 12;
            return h + ":" + m.ToString("00") + (h24 < 12 ? " AM" : " PM");
        }

        // "14:30 – 16:00" for one booking.
        public static string ClockRange(int startMinute, int minutes) =>
            Clock(startMinute) + " - " + Clock(startMinute + minutes);

        // "3h 10m" / "45m", for a duration read on its own.
        public static string Duration(int minutes)
        {
            if (minutes < 60) return minutes + "m";
            int h = minutes / 60, m = minutes % 60;
            return m == 0 ? h + "h" : h + "h " + m + "m";
        }
    }
}
