using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // The real 2025 full-time entry lists for the two championships that share every weekend with the Cup:
    // the Xfinity Series (National) and the Craftsman Truck Series (Trucks). A car shared between drivers is
    // listed under whoever started the most races in it. These match the xfi25 / cts25 paint in Resources,
    // which is keyed by the same car numbers.
    //
    // Order is talent order, because the simulator seeds its grid off it and SimulatedField rates down the
    // list: best first, by the driver's own record in that series (top 10s, wins counted twice, over starts,
    // with a little shrinkage so a handful of starts cannot top the table).
    //
    // Source: each season's Wikipedia article as it stood at the end of 2025; birth years from the drivers'
    // own articles (0 = not on file). Generated — edit the scrape, not this file, for a new season.
    public static class SeriesEntryLists2025
    {
        public readonly struct Entry
        {
            public readonly int Number;
            public readonly string First, Last, Team, Manufacturer;
            public readonly int BirthYear;

            public Entry(int number, string first, string last, string team, string manufacturer, int birthYear)
            {
                Number = number; First = first; Last = last; Team = team; Manufacturer = manufacturer;
                BirthYear = birthYear;
            }

            public string Name => (First + " " + Last).Trim();
        }

        static Entry E(int number, string first, string last, string team, string manufacturer, int birthYear) =>
            new Entry(number, first, last, team, manufacturer, birthYear);

        static readonly Entry[] National =
        {
            //   num  first            last                     team                              make  born
            E( 88, "Connor",         "Zilisch",               "JR Motorsports",                  "CHV", 2006),
            E( 21, "Austin",         "Hill",                  "Richard Childress Racing",        "CHV", 1994),
            E(  7, "Justin",         "Allgaier",              "JR Motorsports",                  "CHV", 1986),
            E(  2, "Jesse",          "Love",                  "Richard Childress Racing",        "CHV", 2005),
            E( 25, "Harrison",       "Burton",                "AM Racing",                       "FRD", 2000),
            E( 41, "Sam",            "Mayer",                 "Haas Factory Team",               "FRD", 2003),
            E( 19, "Aric",           "Almirola",              "Joe Gibbs Racing",                "TYT", 1984),
            E(  8, "Sammy",          "Smith",                 "JR Motorsports",                  "CHV", 2004),
            E(  0, "Sheldon",        "Creed",                 "Haas Factory Team",               "FRD", 1997),
            E( 20, "Brandon",        "Jones",                 "Joe Gibbs Racing",                "TYT", 1997),
            E( 16, "Christian",      "Eckes",                 "Kaulig Racing",                   "CHV", 2000),
            E(  1, "Carson",         "Kvapil",                "JR Motorsports",                  "CHV", 2003),
            E( 54, "Taylor",         "Gray",                  "Joe Gibbs Racing",                "TYT", 2005),
            E( 48, "Nick",           "Sanchez",               "Big Machine Racing",              "CHV", 2001),
            E( 18, "William",        "Sawalich",              "Joe Gibbs Racing",                "TYT", 2006),
            E( 27, "Jeb",            "Burton",                "Jordan Anderson Racing",          "CHV", 1992),
            E( 44, "Brennan",        "Poole",                 "Alpha Prime Racing",              "CHV", 1991),
            E( 26, "Dean",           "Thompson",              "Sam Hunt Racing",                 "TYT", 2001),
            E( 10, "Daniel",         "Dye",                   "Kaulig Racing",                   "CHV", 2003),
            E( 39, "Ryan",           "Sieg",                  "RSS Racing",                      "FRD", 1987),
            E( 42, "Anthony",        "Alfredo",               "Young's Motorsports",             "CHV", 1999),
            E(  4, "Parker",         "Retzlaff",              "Alpha Prime Racing",              "CHV", 2003),
            E( 51, "Jeremy",         "Clements",              "Jeremy Clements Racing",          "CHV", 1985),
            E( 70, "Leland",         "Honeyman",              "Cope Family Racing",              "CHV", 2005),
            E( 11, "Josh",           "Williams",              "Kaulig Racing",                   "CHV", 1993),
            E( 45, "Mason",          "Massey",                "Alpha Prime Racing",              "CHV", 1997),
            E( 31, "Blaine",         "Perkins",               "Jordan Anderson Racing",          "CHV", 2000),
            E( 99, "Matt",           "DiBenedetto",           "Viking Motorsports",              "CHV", 1991),
            E( 35, "Greg",           "Van Alst",              "Joey Gase Motorsports",           "CHV", 1981),
            E( 28, "Kyle",           "Sieg",                  "RSS Racing",                      "FRD", 2001),
            E( 91, "Josh",           "Bilicki",               "DGM Racing",                      "CHV", 1995),
            E( 14, "Garrett",        "Smithley",              "SS-Green Light Racing",           "CHV", 1992),
            E( 53, "Joey",           "Gase",                  "Joey Gase Motorsports",           "CHV", 1993),
            E( 71, "Ryan",           "Ellis",                 "DGM Racing",                      "CHV", 1989),
        };

        static readonly Entry[] Trucks =
        {
            //   num  first            last                     team                              make  born
            E( 11, "Corey",          "Heim",                  "Tricon Garage",                   "TYT", 2002),
            E( 38, "Chandler",       "Smith",                 "Front Row Motorsports",           "FRD", 2002),
            E(  9, "Grant",          "Enfinger",              "CR7 Motorsports",                 "CHV", 1985),
            E( 98, "Ty",             "Majeski",               "ThorSport Racing",                "FRD", 1994),
            E( 34, "Layne",          "Riggs",                 "Front Row Motorsports",           "FRD", 2002),
            E( 88, "Matt",           "Crafton",               "ThorSport Racing",                "FRD", 1976),
            E( 19, "Daniel",         "Hemric",                "McAnally–Hilgemann Racing",       "CHV", 1991),
            E( 99, "Ben",            "Rhodes",                "ThorSport Racing",                "FRD", 1997),
            E( 52, "Stewart",        "Friesen",               "Halmar Friesen Racing",           "TYT", 1983),
            E( 17, "Gio",            "Ruggiero",              "Tricon Garage",                   "TYT", 2006),
            E( 71, "Rajah",          "Caruth",                "Spire Motorsports",               "CHV", 2002),
            E( 18, "Tyler",          "Ankrum",                "McAnally–Hilgemann Racing",       "CHV", 2001),
            E( 45, "Kaden",          "Honeycutt",             "Niece Motorsports",               "CHV", 2003),
            E( 13, "Jake",           "Garcia",                "ThorSport Racing",                "FRD", 2005),
            E(  1, "Brent",          "Crews",                 "Tricon Garage",                   "TYT", 2008),
            E( 15, "Tanner",         "Gray",                  "Tricon Garage",                   "TYT", 1999),
            E(  7, "Corey",          "Day",                   "Spire Motorsports",               "CHV", 2005),
            E( 81, "Connor",         "Mosack",                "McAnally–Hilgemann Racing",       "CHV", 1999),
            E( 77, "Andrés",         "Pérez de Lara",         "Spire Motorsports",               "CHV", 2005),
            E( 44, "Bayley",         "Currey",                "Niece Motorsports",               "CHV", 1996),
            E( 26, "Dawson",         "Sutton",                "Rackley W.A.R.",                  "CHV", 2006),
            E( 42, "Matt",           "Mills",                 "Niece Motorsports",               "CHV", 1996),
            E( 33, "Frankie",        "Muniz",                 "Reaume Brothers Racing",          "FRD", 1985),
            E( 76, "Spencer",        "Boyd",                  "Freedom Racing Enterprises",      "CHV", 1995),
            E(  2, "Nathan",         "Byrd",                  "Young's Motorsports",             "CHV", 2000),
            E(  5, "Toni",           "Breidinger",            "Tricon Garage",                   "TYT", 1999),
            E( 22, "Josh",           "Reaume",                "Reaume Brothers Racing",          "FRD", 1990),
        };

        static readonly Entry[] None = new Entry[0];

        // The field for a championship, best first. The Cup's real field is the Drivers table, not this.
        public static IReadOnlyList<Entry> For(RacingSeries series) => series switch
        {
            RacingSeries.National => National,
            RacingSeries.Trucks => Trucks,
            _ => None,
        };

        public static string[] Names(RacingSeries series)
        {
            var field = For(series);
            var names = new string[field.Count];
            for (int i = 0; i < field.Count; i++) names[i] = field[i].Name;
            return names;
        }

        public static bool TryNumberAt(RacingSeries series, int index, out int number)
        {
            var field = For(series);
            if (index >= 0 && index < field.Count) { number = field[index].Number; return true; }
            number = 0;
            return false;
        }

        public static bool TryNumber(RacingSeries series, string name, out int number)
        {
            string key = SeriesCareers2025.Key(name);
            foreach (var e in For(series))
                if (SeriesCareers2025.Key(e.Name) == key) { number = e.Number; return true; }
            number = 0;
            return false;
        }
    }
}
