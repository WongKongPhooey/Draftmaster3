namespace Draftmaster.Tracks
{
    // The road between two race weekends runs Monday to Thursday, a morning and an afternoon each: eight
    // half-days, one per stop driven. The eighth stop lands on Friday morning, which is when the next
    // weekend's first session is — so the satnav's clock reads the week down as the stops count down.
    // Pure, so the labels are tested without the map.
    public static class TravelDays
    {
        // Half-days on the road before the next weekend starts.
        public const int HalfDaysOnTheRoad = 8;

        static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri" };

        // The day after `halfDaysDriven` stops on this leg: 0 is Mon AM, 7 is Thu PM, 8 is Fri AM. Past the
        // eighth the clock holds at Fri AM rather than running into the weekend it is driving to.
        public static string Label(int halfDaysDriven)
        {
            int h = halfDaysDriven < 0 ? 0 : halfDaysDriven > HalfDaysOnTheRoad ? HalfDaysOnTheRoad : halfDaysDriven;
            return Days[h / 2] + (h % 2 == 0 ? " AM" : " PM");
        }
    }
}
