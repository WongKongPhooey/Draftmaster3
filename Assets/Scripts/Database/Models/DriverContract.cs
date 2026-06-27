using SQLite;

namespace Draftmaster.Data
{
    // A driver's EMPLOYMENT deal with a team (distinct from the sponsor↔team Contract). Entry says who drives
    // where for a season; this carries the terms — salary, length, buyout. Created during negotiation, not seeded.
    [Table("DriverContracts")]
    public class DriverContract
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int DriverId { get; set; }
        [Indexed] public int TeamId { get; set; }
        [Indexed] public int SeriesId { get; set; }

        public int StartSeason { get; set; }
        public int Seasons { get; set; }            // length in seasons
        public int Salary { get; set; }             // money per season (negative = driver pays for the ride)
        public int SignOnBonus { get; set; }        // one-off money
        public int BuyoutClause { get; set; }       // cost to break early

        public bool IsPlayer { get; set; }          // the player's own deal
        public bool Active { get; set; } = true;
    }
}
