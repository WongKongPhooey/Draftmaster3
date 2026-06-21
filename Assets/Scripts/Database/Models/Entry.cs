using SQLite;

namespace Draftmaster.Data
{
    // A driver's seat in a series for one season: which Driver drives for which Team, in which Series, under what
    // car number. The backbone of "who races where". Populated when a season is generated, not statically seeded.
    [Table("Entries")]
    public class Entry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int SeriesId { get; set; }
        [Indexed] public int Season { get; set; }    // year
        [Indexed] public int DriverId { get; set; }
        public int TeamId { get; set; }
        public int CarNumber { get; set; }
        public bool IsPlayer { get; set; }            // the human player's seat
        public bool Active { get; set; } = true;
    }
}
