using SQLite;

namespace Draftmaster.Data
{
    // A racing team. Belongs to a home Series; fields the cars driven by Drivers (linked via Entry).
    [Table("Teams")]
    public class Team
    {
        public const int RatingMax = 100;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }
        public string ShortName { get; set; }      // abbreviation, e.g. "PEN"
        [Indexed] public int SeriesId { get; set; } // home series

        public string Manufacturer { get; set; }   // FRD / CHV / TYT / HON / DDG
        public string Owner { get; set; }
        public string Region { get; set; }
        public string CarsetPrefix { get; set; }    // ties to Resources liveries (e.g. "cup26")

        public int CarRating { get; set; }          // 0-100, sim car pace
        public int Prestige { get; set; }           // 0-100
        public int Morale { get; set; }             // 0-100
        public int Budget { get; set; }             // money
    }
}
