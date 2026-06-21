using System.Collections.Generic;

namespace Draftmaster.Data
{
    public static class DummyTeams
    {
        // seriesId references DummySeries insert order: 1=CUP 2=CHL 3=TRK 4=NSS 5=IOW 6=OWL 7=ODS.
        static Team T(string name, string shortName, int seriesId, string manu, string owner,
            int carRating, int prestige, int budget, string carset)
        {
            return new Team
            {
                Name = name, ShortName = shortName, SeriesId = seriesId, Manufacturer = manu, Owner = owner,
                Region = "USA", CarsetPrefix = carset, CarRating = carRating, Prestige = prestige,
                Morale = 70, Budget = budget
            };
        }

        public static List<Team> Build()
        {
            return new List<Team>
            {
                // Premier Cup (1)
                T("Penmark Racing",    "PEN", 1, "FRD", "Roger Penmark",   94, 95, 4200000, "cup26"),
                T("Gibson Motorsport", "GBM", 1, "TYT", "Joe Gibson",      92, 90, 3800000, "cup26"),
                T("Vantage Racing",    "VAN", 1, "CHV", "Rick Vantage",    95, 96, 4500000, "cup26"),
                T("Childers Racing",   "CHR", 1, "CHV", "Roy Childers",    85, 80, 2600000, "cup26"),
                // Challenger (2)
                T("Redline Racing",    "RDL", 2, "FRD", "Sam Redline",     78, 65, 1200000, "cup26"),
                T("Apex Autosport",    "APX", 2, "TYT", "Dana Apex",       74, 60,  900000, "cup26"),
                // Trucks (3)
                T("Hauler Motorsport", "HAU", 3, "CHV", "Bill Hauler",     70, 58,  600000, "cup26"),
                T("Ironworks Racing",  "IRN", 3, "FRD", "Gus Iron",        66, 52,  450000, "cup26"),
                // National Stock (4)
                T("Grassroots Garage", "GRG", 4, "CHV", "Pat Green",       55, 45,  180000, "cup26"),
                T("Sundown Speed",     "SUN", 4, "FRD", "Mel Sundown",     52, 42,  150000, "cup26"),
                // Indy Open Wheel (5)
                T("Andersen Autosport","AND", 5, "HON", "Mike Andersen",   90, 92, 3000000, "irl26"),
                T("Chip Racing",       "CHI", 5, "CHV", "Chip Granado",    91, 90, 3100000, "irl26"),
                // Open Wheel Lights (6)
                T("Junior Open Racing","JOR", 6, "HON", "Lena Juniper",    60, 55,  500000, "irl26"),
                // Outlaw Dirt Sprints (7)
                T("Dust Devils Racing","DDR", 7, "CHV", "Cole Dustin",     62, 58,  300000, "cup26"),
            };
        }
    }
}
