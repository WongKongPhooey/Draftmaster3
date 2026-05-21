using SQLite;

namespace Draftmaster.Data
{
    [Table("Drivers")]
    public class Driver
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Nickname { get; set; }
        public int Age { get; set; }

        public int Reputation { get; set; }
        public int Aggression { get; set; }
        public int Consistency { get; set; }
        public int Qualifying { get; set; }
        public int ShortTracks { get; set; }
        public int Superspeedways { get; set; }
        public int RoadCourses { get; set; }
        public int TireWear { get; set; }
        public int FuelEfficiency { get; set; }
        public int Potential { get; set; }
        public int SponsorAppeal { get; set; }
        public int Negotiation { get; set; }
        public int Motivation { get; set; }
        public int FanAppeal { get; set; }
    }
}
