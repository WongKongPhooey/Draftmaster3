using System.Collections.Generic;
using Draftmaster.Tracks;

namespace Draftmaster.Data
{
    // Track catalogue seed. Names match the racetrack scene / Resources asset names so Race.TrackName and
    // Race.TrackId line up with what actually loads. Types align to the Driver track-type aptitudes.
    //
    // The calendar half of this list is DERIVED from Draftmaster.Tracks.TrackDimensions rather than typed
    // out again here. That table holds each venue's published lap length, width and banking and is what
    // the layout generator builds from, so deriving the catalogue from it means the number the database
    // reports and the number the road is actually built to can never drift apart. Anything the three
    // championships no longer visit — tracks that exist as legacy scenes, or as nodes on the travel map —
    // is listed below by hand, because it has no entry in the dimensions table.
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

        public static TrackType ToTrackType(TrackKind kind) => kind switch
        {
            TrackKind.ShortTrack => TrackType.ShortTrack,
            TrackKind.Superspeedway => TrackType.Superspeedway,
            TrackKind.RoadCourse => TrackType.RoadCourse,
            TrackKind.DirtCourse => TrackType.DirtCourse,
            _ => TrackType.Speedway,
        };

        public static List<Track> Build()
        {
            var rows = new List<Track>();
            var seen = new HashSet<string>();

            // Every venue on the Cup / National / Truck calendars, straight from the dimensions table.
            foreach (var dim in TrackDimensions.All)
            {
                rows.Add(new Track
                {
                    Name = dim.id,
                    DisplayName = dim.displayName,
                    Country = dim.country,
                    Type = ToTrackType(dim.kind),
                    LengthMiles = dim.lapMiles,
                    BankingDegrees = UnityEngine.Mathf.RoundToInt(dim.turnBankingDeg),
                    DefaultLaps = dim.cupLaps,
                });
                seen.Add(dim.id);
            }

            // Off the current calendars, but still referenced: legacy racetrack scenes and travel-map nodes.
            // Kept so nothing that already names one of these ids resolves to nothing.
            foreach (var legacy in Legacy())
                if (seen.Add(legacy.Name)) rows.Add(legacy);

            return rows;
        }

        // Tracks the three championships no longer race, or never did. No dimensions row, so no generated
        // layout — these stay catalogue-only unless someone authors one.
        static IEnumerable<Track> Legacy()
        {
            yield return Tk("Fontana",   "Auto Club Speedway",           TrackType.Speedway,   2.0f, 14, 200);
            yield return Tk("Joliet",    "Chicagoland Speedway",         TrackType.Speedway,   1.5f, 18, 267);
            yield return Tk("Kentucky",  "Kentucky Speedway",            TrackType.Speedway,   1.5f, 14, 267);
            yield return Tk("Nazareth",  "Nazareth Speedway",            TrackType.ShortTrack, 0.946f, 6, 225);
            yield return Tk("LosAngeles", "LA Memorial Coliseum",        TrackType.ShortTrack, 0.25f,  0, 150);
            yield return Tk("Madison",   "Madison International Speedway", TrackType.ShortTrack, 0.5f, 12, 250);
            yield return Tk("Motegi",    "Twin Ring Motegi",             TrackType.RoadCourse, 2.98f,  0, 80, "Japan");
        }
    }
}
