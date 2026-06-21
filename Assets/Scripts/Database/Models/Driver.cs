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
    }
}
