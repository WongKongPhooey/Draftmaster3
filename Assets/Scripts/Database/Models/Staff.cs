using SQLite;

namespace Draftmaster.Data
{
    // Non-driving team personnel. Their ratings feed pit-stop speed, setup quality, and strategy calls — the
    // data backbone for the crew-chief / pit-crew features.
    public enum StaffRole
    {
        CrewChief = 0,
        RaceEngineer = 1,
        Mechanic = 2,
        PitCrew = 3,
        Spotter = 4,
        Strategist = 5
    }

    [Table("Staff")]
    public class Staff
    {
        public const int RatingMax = 100;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed] public int TeamId { get; set; }

        public string Name { get; set; }
        public StaffRole Role { get; set; }

        public int Rating { get; set; }      // 0-100, how good they are at the role
        public int Salary { get; set; }      // money per season
        public int Morale { get; set; }      // 0-100
        public bool Active { get; set; } = true;
    }
}
