using SQLite;

namespace Draftmaster.Data
{
    // Track type — aligned 1:1 with the Driver track-type aptitude stats so the sim can pick the right stat to
    // weight for a given round (ShortTracks / Speedways / Superspeedways / RoadCourses / DirtCourses).
    public enum TrackType
    {
        ShortTrack = 0,     // < 1 mile oval
        Speedway = 1,       // ~1-2 mile oval (intermediate)
        Superspeedway = 2,  // 2.5 mile pack-racing oval
        RoadCourse = 3,     // road / street circuit
        DirtCourse = 4      // dirt oval
    }

    // Track catalogue. Gives Race.TrackId something real to point at (instead of a bare string) and maps each
    // track to a TrackType so driver aptitudes apply. Name matches a TrackInfoV2 / Resources/Tracks asset + scene.
    [Table("Tracks")]
    public class Track
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }            // asset/scene name, e.g. "WatkinsGlen"
        public string DisplayName { get; set; }      // UI name, e.g. "Watkins Glen International"
        public string Country { get; set; }

        public TrackType Type { get; set; }
        public float LengthMiles { get; set; }
        public int BankingDegrees { get; set; }
        public int DefaultLaps { get; set; }
    }
}
