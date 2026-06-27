using System.Collections.Generic;

namespace Draftmaster.Data
{
    // Track catalogue seed. Names match the racetrack scene / Resources asset names so Race.TrackName/TrackId line
    // up with what actually loads. Types align to the Driver track-type aptitudes.
    public static class DummyTracks
    {
        static Track Tk(string name, string display, TrackType type, float miles, int banking, int laps, string country = "USA")
        {
            return new Track
            {
                Name = name, DisplayName = display, Country = country,
                Type = type, LengthMiles = miles, BankingDegrees = banking, DefaultLaps = laps
            };
        }

        public static List<Track> Build()
        {
            return new List<Track>
            {
                // Superspeedways
                Tk("Daytona",       "Daytona International Speedway", TrackType.Superspeedway, 2.5f, 31, 200),
                Tk("Talladega",     "Talladega Superspeedway",        TrackType.Superspeedway, 2.66f, 33, 188),
                Tk("Indianapolis",  "Indianapolis Motor Speedway",    TrackType.Superspeedway, 2.5f, 9, 160),

                // Speedways (intermediate ovals)
                Tk("Atlanta",       "Atlanta Motor Speedway",         TrackType.Speedway, 1.54f, 28, 260),
                Tk("Charlotte",     "Charlotte Motor Speedway",       TrackType.Speedway, 1.5f, 24, 400),
                Tk("LasVegas",      "Las Vegas Motor Speedway",       TrackType.Speedway, 1.5f, 20, 267),
                Tk("Kansas",        "Kansas Speedway",                TrackType.Speedway, 1.5f, 17, 267),
                Tk("Kentucky",      "Kentucky Speedway",              TrackType.Speedway, 1.5f, 14, 267),
                Tk("Miami",         "Homestead-Miami Speedway",       TrackType.Speedway, 1.5f, 20, 267),
                Tk("Joliet",        "Chicagoland Speedway",           TrackType.Speedway, 1.5f, 18, 267),
                Tk("Michigan",      "Michigan International Speedway", TrackType.Speedway, 2.0f, 18, 200),
                Tk("Fontana",       "Auto Club Speedway",             TrackType.Speedway, 2.0f, 14, 200),
                Tk("FortWorth",     "Texas Motor Speedway",           TrackType.Speedway, 1.5f, 20, 334),
                Tk("LongPond",      "Pocono Raceway",                 TrackType.Speedway, 2.5f, 8, 160),
                Tk("Darlington",    "Darlington Raceway",             TrackType.Speedway, 1.366f, 25, 367),
                Tk("Dover",         "Dover Motor Speedway",           TrackType.Speedway, 1.0f, 24, 400),
                Tk("Nashville",     "Nashville Superspeedway",        TrackType.Speedway, 1.33f, 18, 300),

                // Short tracks
                Tk("Bristol",       "Bristol Motor Speedway",         TrackType.ShortTrack, 0.533f, 28, 500),
                Tk("Martinsville",  "Martinsville Speedway",          TrackType.ShortTrack, 0.526f, 12, 500),
                Tk("Richmond",      "Richmond Raceway",               TrackType.ShortTrack, 0.75f, 14, 400),
                Tk("Phoenix",       "Phoenix Raceway",                TrackType.ShortTrack, 1.0f, 11, 312),
                Tk("NewHampshire",  "New Hampshire Motor Speedway",   TrackType.ShortTrack, 1.058f, 7, 301),
                Tk("Iowa",          "Iowa Speedway",                  TrackType.ShortTrack, 0.875f, 14, 300),
                Tk("NorthWilkesboro","North Wilkesboro Speedway",     TrackType.ShortTrack, 0.625f, 14, 400),
                Tk("Milwaukee",     "Milwaukee Mile",                 TrackType.ShortTrack, 1.015f, 9, 250),
                Tk("Nazareth",      "Nazareth Speedway",              TrackType.ShortTrack, 0.946f, 6, 225),
                Tk("LosAngeles",    "LA Memorial Coliseum",           TrackType.ShortTrack, 0.25f, 0, 150),
                Tk("Madison",       "Madison International Speedway",  TrackType.ShortTrack, 0.5f, 12, 250),

                // Road courses
                Tk("WatkinsGlen",   "Watkins Glen International",      TrackType.RoadCourse, 2.45f, 0, 90),
                Tk("Motegi",        "Twin Ring Motegi",               TrackType.RoadCourse, 2.98f, 0, 80, "Japan"),
            };
        }
    }
}
