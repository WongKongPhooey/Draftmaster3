using SQLite;

namespace Draftmaster.Data
{
    // One round on a series calendar. Results point at a Race. Generated per season, not statically seeded.
    [Table("Races")]
    public class Race
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int SeriesId { get; set; }
        [Indexed] public int Season { get; set; }    // year
        public int Round { get; set; }               // 1-based round number in the season

        public string TrackName { get; set; }        // matches a TrackInfoV2 / Resources/Tracks asset
        public int TrackId { get; set; }
        public int Laps { get; set; }
        public int Week { get; set; }                // calendar week within the season

        public bool Completed { get; set; }
    }
}
