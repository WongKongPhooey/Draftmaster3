using SQLite;

namespace Draftmaster.Data
{
    // The player's career save: the anchor every Entry/Result/Contract ultimately belongs to. One row per save
    // slot. Replaces the scattered PlayerPrefs career state (cash, season, progression). Created on "New Career",
    // not statically seeded.
    [Table("Careers")]
    public class Career
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int SaveSlot { get; set; }
        public string PlayerName { get; set; }

        // The player's seat in the world.
        [Indexed] public int DriverId { get; set; }   // the player's Driver row
        public int TeamId { get; set; }               // current team (0 = none/free agent)
        [Indexed] public int CurrentSeriesId { get; set; }

        // --- Calendar position ---
        public int Season { get; set; }               // current year
        public int Week { get; set; }                 // calendar week within the season
        public int Round { get; set; }                // next/last round number

        // --- Economy / standing ---
        public long Cash { get; set; }
        public int Reputation { get; set; }           // 0-100, the player's standing in the paddock
        public int CareerStartSeason { get; set; }

        public long LastPlayedUnix { get; set; }
        public bool Active { get; set; } = true;      // the currently loaded save
    }
}
