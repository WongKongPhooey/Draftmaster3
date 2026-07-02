using SQLite;

namespace Draftmaster.Data
{
    // One row per driver in the game. Personal info + skill stats (0-20) + overall ability ratings (0-100).
    [Table("Drivers")]
    public class Driver
    {
        // Max value for a skill stat (the 0-20 group below).
        public const int StatMax = 20;
        // Max value for the ability ratings (0-100).
        public const int AbilityMax = 100;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // --- Identity ---
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Nickname { get; set; }
        public int Age { get; set; }

        // --- Skill stats (0-20) ---
        // Track-type aptitudes
        public int ShortTracks { get; set; }
        public int Speedways { get; set; }
        public int Superspeedways { get; set; }
        public int RoadCourses { get; set; }
        public int DirtCourses { get; set; }
        public int OpenWheel { get; set; }
        // Craft
        public int FuelManagement { get; set; }
        public int TyreManagement { get; set; }
        public int Qualifying { get; set; }
        public int Consistency { get; set; }
        public int Aggression { get; set; }
        public int Awareness { get; set; }
        public int Adaptability { get; set; }
        // Commercial / standing
        public int SponsorAppeal { get; set; }
        public int FanSupport { get; set; }
        public int Prestige { get; set; }

        // --- Overall ability (0-100) ---
        public int CurrentAbility { get; set; }
        public int PotentialAbility { get; set; }

        // --- Career lifecycle (driven by DriverProgression / SeasonRollover each offseason) ---
        // Season the driver debuted (their rookie year). 0 for the hand-authored seed drivers.
        public int DebutSeason { get; set; }
        // Age at which CurrentAbility tops out; growth before it, decline after. 0 = not yet assigned
        // (DriverProgression assigns one lazily on the first Advance for seed drivers).
        public int PeakAge { get; set; }
        // True once the driver hangs it up; retired drivers stay in the table for history but leave the active pool.
        public bool Retired { get; set; }
        public int RetiredSeason { get; set; }
    }
}
