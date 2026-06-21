using System.Collections.Generic;

namespace Draftmaster.Data
{
    public static class DummyDrivers
    {
        // Compact constructor. Stats are 0-20; current/potential ability are 0-100.
        static Driver D(string fn, string ln, string nick, int age,
            int shortTracks, int speedways, int superspeedways, int roadCourses, int dirtCourses, int openWheel,
            int fuel, int tyre, int qualifying, int consistency, int aggression, int awareness, int adaptability,
            int sponsorAppeal, int fanSupport, int prestige,
            int current, int potential)
        {
            return new Driver
            {
                FirstName = fn, LastName = ln, Nickname = nick, Age = age,
                ShortTracks = shortTracks, Speedways = speedways, Superspeedways = superspeedways,
                RoadCourses = roadCourses, DirtCourses = dirtCourses, OpenWheel = openWheel,
                FuelManagement = fuel, TyreManagement = tyre, Qualifying = qualifying, Consistency = consistency,
                Aggression = aggression, Awareness = awareness, Adaptability = adaptability,
                SponsorAppeal = sponsorAppeal, FanSupport = fanSupport, Prestige = prestige,
                CurrentAbility = current, PotentialAbility = potential
            };
        }

        public static List<Driver> Build()
        {
            //                                              age  ST SP SS RC DC OW FM TM  Q CO AG AW AD SA FS PR  CUR POT
            return new List<Driver>
            {
                D("Buck",    "Roberts",  "The Hammer",   38, 18, 13, 13, 11, 12,  5, 11, 12, 15, 14, 18, 13, 12, 15, 18, 16,  78, 80),
                D("Hank",    "Jensen",   "Smooth",       34, 14, 17, 17, 14,  9,  8, 18, 18, 16, 18,  9, 16, 15, 16, 14, 14,  82, 84),
                D("Dale",    "Whitlow",  "Pops",         47, 16, 18, 18, 15, 13,  7, 16, 17, 13, 18, 11, 18, 16, 19, 19, 19,  85, 85),
                D("Cody",    "Marsh",    "Rook",         21,  7, 11, 11,  9,  8,  6, 10, 10, 13, 10, 14,  8,  9, 12, 11,  6,  52, 92),
                D("Tyler",   "Briggs",   "Tank",         29, 17, 12, 12, 10, 14,  5,  9,  9, 14, 11, 19, 10, 11, 10, 16, 12,  66, 74),
                D("Owen",    "Calloway", "The Surgeon",  32, 15, 16, 16, 18, 10,  9, 16, 18, 17, 19, 10, 18, 18, 17, 16, 16,  88, 90),
                D("Rusty",   "Pike",     "Wildcard",     36, 15, 15, 15, 13, 16,  7, 11, 11, 14,  9, 16, 11, 13, 12, 14, 11,  60, 62),
                D("Sam",     "Holloway", "Slick",        27, 14, 15, 16, 17, 10,  8, 14, 14, 16, 15, 13, 14, 16, 14, 14, 13,  74, 86),
                D("Joaquin", "Reyes",    "Road Runner",  30, 13, 14, 14, 19,  9, 12, 15, 16, 15, 16, 12, 15, 18, 14, 15, 14,  79, 86),
                D("Jesse",   "McGraw",   "Gentleman",    41, 14, 17, 18, 15, 12,  6, 18, 18, 14, 18,  7, 17, 16, 18, 18, 18,  80, 80),
                D("Mason",   "Vance",    "Vandal",       25, 16, 14, 14, 11, 13,  7,  9, 10, 15, 12, 19,  9, 11, 11, 13,  8,  60, 90),
                D("Will",    "Atherton", "Will-Power",   33, 15, 16, 16, 16, 11, 10, 15, 15, 17, 16, 14, 15, 16, 16, 16, 15,  80, 82),
                D("Earl",    "Schaeffer","The Veteran",  50, 15, 18, 18, 14, 13,  6, 18, 18, 13, 18,  8, 19, 17, 18, 18, 20,  82, 82),
                D("Drew",    "Castillo", "Speed Demon",  28, 15, 16, 16, 15,  9,  9, 13, 13, 19, 14, 16, 13, 14, 14, 16, 13,  76, 88),
                D("Ben",     "Carlisle", "Beanpole",     24, 12, 13, 13, 14,  8,  7, 14, 14, 14, 13, 11, 13, 15, 10, 11,  7,  56, 90),
                D("Lance",   "Ortega",   "Lightning",    31, 16, 14, 14, 17, 11,  9, 13, 14, 18, 15, 15, 14, 16, 15, 16, 14,  78, 84),
                D("Cooper",  "Lange",    "The Closer",   37, 15, 17, 17, 16, 12,  8, 17, 17, 16, 18, 14, 17, 16, 17, 16, 16,  84, 84),
                D("Hector",  "Aldana",   "El Toro",      35, 18, 15, 15, 16, 17,  8, 12, 13, 16, 14, 17, 14, 15, 15, 16, 15,  76, 78),
                D("Reggie",  "Boone",    "Boone Dog",    43, 16, 18, 18, 14, 15,  6, 18, 18, 14, 17, 12, 18, 16, 16, 17, 17,  80, 80),
                D("Nico",    "Petrov",   "Iceman",       26, 14, 16, 16, 18, 10, 13, 16, 16, 17, 18, 10, 17, 18, 14, 14, 12,  82, 94),
            };
        }
    }
}
