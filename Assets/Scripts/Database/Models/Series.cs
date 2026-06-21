using SQLite;

namespace Draftmaster.Data
{
    // Broad family of a championship — drives which driver stats the sim weights most.
    public enum Discipline
    {
        StockCar = 0,   // ovals + some road; the Cup ladder
        OpenWheel = 1,  // Indy-style
        Dirt = 2,       // dirt ovals / sprints
        Truck = 3,      // stock-based trucks
        ShortTrack = 4, // grassroots short-oval feeder
        SportsCar = 5   // road / endurance
    }

    // One row per championship in the world. The player races one at a time; the rest are simulated each round.
    [Table("Series")]
    public class Series
    {
        public const int PrestigeMax = 100;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- Identity ---
        public string Name { get; set; }
        public string ShortName { get; set; }
        // 1 = top championship of its ladder; higher = lower feeder tiers.
        public int Tier { get; set; }
        public Discipline Discipline { get; set; }
        public string Region { get; set; }

        // --- Format ---
        public int FieldSize { get; set; }       // cars per race
        public int RoundsPerSeason { get; set; }
        public int CurrentSeason { get; set; }    // year

        // --- Standing / economy ---
        public int Prestige { get; set; }         // 0-100
        public int PurseScale { get; set; }       // base race purse (money), scales payouts

        // Whether the world simulates this series. Player's active series is tracked on the player's Entry, not here.
        public bool Active { get; set; } = true;
    }
}
