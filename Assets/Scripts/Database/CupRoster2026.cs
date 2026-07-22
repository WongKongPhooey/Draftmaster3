using System.Collections.Generic;

namespace Draftmaster.Data
{
    // The 2026 NASCAR Cup Series field, keyed by car number so a livery sprite
    // (Resources/cup26livery<N>) always pairs with the driver who actually runs that car.
    //
    // This is the single place to edit the roster. Every entry here must have a matching
    // cup26livery<N>.png in Resources or the car simply never spawns (GridSpawner only uses
    // numbers it found art for).
    //
    // Stats: only the defining ones are hand-authored. The rest are derived from CurrentAbility
    // in Expand() so a roster line stays one readable row.
    public static class CupRoster2026
    {
        public const string CarsetPrefix = "cup26";

        // One car in the field.
        public class Entry
        {
            public int Number;
            public string First, Last, Short, Team, Manufacturer;
            public int Age, Current, Potential;
            // Defining skills (0-20): aggression, qualifying pace, consistency,
            // road courses, superspeedways, short tracks.
            public int Agg, Qual, Cons, Road, Super, Short_;
        }

        static Entry E(int num, string first, string last, string shortName, string team, string manu,
            int age, int cur, int pot, int agg, int qual, int cons, int road, int super, int shortTrack)
        {
            return new Entry
            {
                Number = num, First = first, Last = last, Short = shortName, Team = team, Manufacturer = manu,
                Age = age, Current = cur, Potential = pot,
                Agg = agg, Qual = qual, Cons = cons, Road = road, Super = super, Short_ = shortTrack
            };
        }

        // Ordered roughly by team. Entries flagged VERIFY are seats that moved (or open/part-time rides)
        // — correct the name/team here if the real 2026 entry list differs.
        //                num  first          last              short          team                        manu   age cur pot  AG  Q CO RC SS ST
        static readonly Entry[] Field =
        {
            // Hendrick Motorsports
            E( 5, "Kyle",    "Larson",         "Larson",       "Hendrick Motorsports",     "CHV", 33, 96, 96, 15, 20, 17, 18, 15, 19),
            E( 9, "Chase",   "Elliott",        "Elliott",      "Hendrick Motorsports",     "CHV", 30, 90, 92, 12, 16, 19, 19, 16, 16),
            E(24, "William", "Byron",          "Byron",        "Hendrick Motorsports",     "CHV", 28, 92, 95, 12, 18, 19, 15, 19, 16),
            E(48, "Alex",    "Bowman",         "Bowman",       "Hendrick Motorsports",     "CHV", 32, 82, 83, 11, 15, 15, 15, 14, 16),

            // Joe Gibbs Racing
            E(11, "Denny",   "Hamlin",         "Hamlin",       "Joe Gibbs Racing",         "TYT", 45, 92, 92, 16, 17, 18, 15, 17, 18),
            E(19, "Chase",   "Briscoe",        "Briscoe",      "Joe Gibbs Racing",         "TYT", 31, 86, 88, 13, 18, 15, 16, 14, 15),
            E(20, "Christopher","Bell",         "Bell",         "Joe Gibbs Racing",         "TYT", 31, 92, 94, 13, 18, 18, 17, 14, 18),
            E(54, "Ty",      "Gibbs",          "Gibbs",        "Joe Gibbs Racing",         "TYT", 23, 82, 92, 15, 16, 13, 15, 14, 14),

            // Team Penske
            E( 2, "Austin",  "Cindric",        "Cindric",      "Team Penske",              "FRD", 27, 79, 84, 13, 14, 14, 15, 18, 13),
            E(12, "Ryan",    "Blaney",         "Blaney",       "Team Penske",              "FRD", 32, 91, 92, 14, 17, 17, 15, 19, 17),
            E(22, "Joey",    "Logano",         "Logano",       "Team Penske",              "FRD", 35, 89, 89, 18, 15, 16, 14, 18, 18),

            // Trackhouse Racing
            E( 1, "Ross",    "Chastain",       "Chastain",     "Trackhouse Racing",        "CHV", 33, 85, 86, 18, 14, 14, 15, 18, 15),
            E(88, "Shane",   "van Gisbergen",  "vanGisbergen", "Trackhouse Racing",        "CHV", 37, 82, 85, 14, 16, 13, 20,  9, 13),
            E(97, "Connor",  "Zilisch",        "Zilisch",      "Trackhouse Racing",        "CHV", 20, 76, 95, 14, 17, 12, 18, 12, 14), // VERIFY number
            E(99, "Justin",  "Haley",          "Haley",        "Trackhouse Racing",        "CHV", 27, 70, 74, 12, 12, 12, 13, 16, 12), // VERIFY seat

            // 23XI Racing
            E(23, "Bubba",   "Wallace",        "Wallace",      "23XI Racing",              "TYT", 32, 82, 84, 14, 15, 14, 12, 18, 14),
            E(35, "Riley",   "Herbst",         "Herbst",       "23XI Racing",              "TYT", 27, 68, 76, 12, 12, 11, 12, 13, 12),
            E(45, "Tyler",   "Reddick",        "Reddick",      "23XI Racing",              "TYT", 30, 89, 91, 15, 17, 16, 18, 15, 16),
            E(67, "Corey",   "Heim",           "Heim",         "23XI Racing",              "TYT", 23, 76, 90, 13, 15, 14, 14, 13, 16), // VERIFY full-time

            // Richard Childress Racing
            E( 3, "Austin",  "Dillon",         "A.Dillon",     "Richard Childress Racing", "CHV", 36, 72, 72, 15, 12, 12, 11, 16, 13),
            E( 8, "Kyle",    "Busch",          "Busch",        "Richard Childress Racing", "CHV", 40, 84, 84, 17, 15, 14, 16, 13, 18),
            E(33, "Austin",  "Hill",           "Hill",         "Richard Childress Racing", "CHV", 31, 66, 72, 14, 12, 11, 11, 14, 12), // part-time

            // RFK Racing
            E( 6, "Brad",    "Keselowski",     "Keselowski",   "RFK Racing",               "FRD", 42, 83, 83, 15, 14, 15, 12, 19, 16),
            E(17, "Chris",   "Buescher",       "Buescher",     "RFK Racing",               "FRD", 33, 82, 83, 11, 15, 17, 15, 16, 15),
            E(60, "Ryan",    "Preece",         "Preece",       "RFK Racing",               "FRD", 35, 74, 75, 15, 13, 12, 12, 16, 15),

            // Spire Motorsports
            E( 7, "Daniel",  "Suarez",         "Suarez",       "Spire Motorsports",        "CHV", 34, 74, 75, 14, 13, 12, 15, 16, 12), // VERIFY seat
            E(71, "Michael", "McDowell",       "McDowell",     "Spire Motorsports",        "CHV", 41, 78, 78, 11, 14, 14, 18, 17, 12),
            E(77, "Carson",  "Hocevar",        "Hocevar",      "Spire Motorsports",        "CHV", 23, 80, 90, 18, 16, 11, 13, 15, 16),

            // Front Row Motorsports
            E( 4, "Noah",    "Gragson",        "Gragson",      "Front Row Motorsports",    "FRD", 27, 71, 76, 16, 12, 11, 12, 15, 13),
            E(34, "Todd",    "Gilliland",      "Gilliland",    "Front Row Motorsports",    "FRD", 26, 71, 76, 12, 13, 12, 13, 15, 13),
            E(36, "Layne",   "Riggs",          "Riggs",        "Front Row Motorsports",    "FRD", 24, 64, 78, 13, 12, 10, 11, 13, 14), // VERIFY seat
            E(38, "Zane",    "Smith",          "Z.Smith",      "Front Row Motorsports",    "FRD", 27, 70, 76, 12, 12, 12, 12, 14, 14),

            // Legacy Motor Club
            E(42, "John Hunter","Nemechek",    "Nemechek",     "Legacy Motor Club",        "TYT", 29, 73, 77, 12, 13, 12, 12, 13, 15),
            E(43, "Erik",    "Jones",          "Jones",        "Legacy Motor Club",        "TYT", 30, 76, 78, 11, 13, 14, 12, 17, 14),
            E(84, "Jimmie",  "Johnson",        "Johnson",      "Legacy Motor Club",        "TYT", 50, 72, 72, 13, 12, 14, 12, 15, 16), // part-time

            // Kaulig Racing
            E(10, "Ty",      "Dillon",         "T.Dillon",     "Kaulig Racing",            "CHV", 34, 66, 67, 12, 11, 12, 11, 14, 12),
            E(16, "AJ",      "Allmendinger",   "Allmendinger", "Kaulig Racing",            "CHV", 44, 78, 78, 13, 14, 14, 19, 12, 14),

            // Single-car and part-time entries
            E(21, "Josh",    "Berry",          "Berry",        "Wood Brothers Racing",     "FRD", 35, 77, 79, 12, 15, 13, 13, 15, 16),
            E(41, "Cole",    "Custer",         "Custer",       "Haas Factory Team",        "FRD", 28, 72, 76, 12, 13, 13, 12, 13, 14),
            E(47, "Ricky",   "Stenhouse Jr.",  "Stenhouse",    "Hyak Motorsports",         "CHV", 38, 74, 74, 16, 12, 12, 11, 18, 13),
            E(51, "Cody",    "Ware",           "Ware",         "Rick Ware Racing",         "FRD", 30, 55, 58, 10,  8,  9, 10, 11,  9),
            E(40, "Kaz",     "Grala",          "Grala",        "Rick Ware Racing",         "FRD", 27, 58, 64, 11, 10,  9, 14, 12, 10), // VERIFY seat
            E(44, "J.J.",    "Yeley",          "Yeley",        "NY Racing Team",           "CHV", 49, 50, 50, 10,  7,  9,  9, 12,  9), // part-time
            E(50, "Josh",    "Bilicki",        "Bilicki",      "Garage 66",                "FRD", 31, 50, 55,  9,  7,  9, 12, 11,  8), // part-time
            E(62, "Anthony", "Alfredo",        "Alfredo",      "Beard Motorsports",        "CHV", 26, 54, 62, 10,  9,  9, 10, 14,  9), // part-time
            E(66, "Chad",    "Finchum",        "Finchum",      "Garage 66",                "FRD", 31, 46, 50,  9,  6,  8,  8, 11,  8), // part-time
            E(78, "Katherine","Legge",         "Legge",        "Live Fast Motorsports",    "CHV", 45, 52, 54,  9,  8,  9, 15, 10,  8), // part-time
        };

        public static IReadOnlyList<Entry> Entries => Field;

        // Full Driver rows for the database. Stats not in the table above are derived from CurrentAbility
        // so every driver stays internally consistent without 47 hand-tuned rows.
        public static List<Driver> BuildDrivers()
        {
            var list = new List<Driver>(Field.Length);
            foreach (var e in Field) list.Add(Expand(e));
            return list;
        }

        // One expanded Driver row, for callers resolving a single car rather than seeding the table.
        public static Driver BuildDriver(Entry e) => Expand(e);

        // Scale a 0-100 ability onto the 0-20 stat range, nudged by `offset` and clamped.
        static int FromAbility(int ability, int offset)
        {
            int v = (ability / 5) + offset;
            return v < 1 ? 1 : (v > Driver.StatMax ? Driver.StatMax : v);
        }

        static Driver Expand(Entry e)
        {
            return new Driver
            {
                FirstName = e.First, LastName = e.Last, ShortName = e.Short, Nickname = "", Age = e.Age,
                CarNumber = e.Number, TeamName = e.Team, Manufacturer = e.Manufacturer,

                ShortTracks = e.Short_, Superspeedways = e.Super, RoadCourses = e.Road,
                Speedways = FromAbility(e.Current, 0),
                DirtCourses = FromAbility(e.Current, -4),
                OpenWheel = FromAbility(e.Current, -8),

                // Craft: veterans manage fuel/tyres better than their raw pace suggests.
                FuelManagement = FromAbility(e.Current, e.Age >= 35 ? 2 : 0),
                TyreManagement = FromAbility(e.Current, e.Age >= 35 ? 2 : 0),
                Qualifying = e.Qual,
                Consistency = e.Cons,
                Aggression = e.Agg,
                Awareness = FromAbility(e.Current, e.Agg >= 16 ? -2 : 1),
                Adaptability = FromAbility(e.Current, 0),

                // Commercial standing tracks results, with a bonus for long-tenured names.
                SponsorAppeal = FromAbility(e.Current, 1),
                FanSupport = FromAbility(e.Current, 1),
                Prestige = FromAbility(e.Current, e.Age >= 38 ? 2 : 0),

                CurrentAbility = e.Current, PotentialAbility = e.Potential,
                DebutSeason = 0, PeakAge = 0, Retired = false, RetiredSeason = 0
            };
        }
    }
}
