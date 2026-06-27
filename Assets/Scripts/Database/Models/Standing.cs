using SQLite;

namespace Draftmaster.Data
{
    // Cached championship standing for one driver in one series-season. Derivable by summing Results, but cached
    // here so standings screens are cheap and a finished season can store its final order/champion. Updated by the
    // sim after each round; not statically seeded.
    [Table("Standings")]
    public class Standing
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int SeriesId { get; set; }
        [Indexed] public int Season { get; set; }
        [Indexed] public int DriverId { get; set; }
        public int TeamId { get; set; }

        public int Position { get; set; }       // 1 = championship leader
        public int Points { get; set; }
        public int Wins { get; set; }
        public int Podiums { get; set; }
        public int Top10s { get; set; }
        public int Poles { get; set; }
        public int DNFs { get; set; }
        public bool IsPlayer { get; set; }
    }
}
