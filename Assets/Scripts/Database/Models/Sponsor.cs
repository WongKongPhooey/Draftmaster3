using SQLite;

namespace Draftmaster.Data
{
    // A sponsor brand in the world. Catalogue entry; actual deals live in Contract.
    [Table("Sponsors")]
    public class Sponsor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Industry { get; set; }     // Energy, Telecom, Retail, Auto, Bank, Tech, Food, Oil...

        public int Wealth { get; set; }           // 0-100, payout capacity / how big a deal they can fund
        public int Prestige { get; set; }         // 0-100

        public int SeasonValue { get; set; }      // money per season for a primary deal
        public int BonusPerWin { get; set; }      // money
        public int BonusPerPodium { get; set; }   // money

        // Minimum team prestige (0-100) the sponsor will sign with.
        public int MinPrestige { get; set; }
    }
}
