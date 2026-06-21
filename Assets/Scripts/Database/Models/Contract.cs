using SQLite;

namespace Draftmaster.Data
{
    // A sponsorship deal between a Sponsor and a Team for a span of seasons. Created during negotiation, not seeded.
    [Table("Contracts")]
    public class Contract
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int SponsorId { get; set; }
        [Indexed] public int TeamId { get; set; }

        public int StartSeason { get; set; }   // year the deal begins
        public int Seasons { get; set; }        // length in seasons
        public int Value { get; set; }          // money per season
        public int SignOnBonus { get; set; }    // one-off money
        public bool IsPrimary { get; set; }     // primary (livery) sponsor vs associate
    }
}
