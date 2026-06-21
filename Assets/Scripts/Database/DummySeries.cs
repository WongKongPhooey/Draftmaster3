using System.Collections.Generic;

namespace Draftmaster.Data
{
    public static class DummySeries
    {
        static Series S(string name, string shortName, int tier, Discipline disc, string region,
            int fieldSize, int rounds, int prestige, int purse)
        {
            return new Series
            {
                Name = name, ShortName = shortName, Tier = tier, Discipline = disc, Region = region,
                FieldSize = fieldSize, RoundsPerSeason = rounds, Prestige = prestige, PurseScale = purse,
                CurrentSeason = 2025, Active = true
            };
        }

        // Real-based but fictionalised championships. The stock-car ladder (Cup → Challenger → Trucks → grassroots)
        // mirrors the carset prefixes already in the project (cup**/irl**), plus an open-wheel ladder and dirt.
        public static List<Series> Build()
        {
            return new List<Series>
            {
                S("Premier Cup Series",   "CUP",  1, Discipline.StockCar,   "USA", 38, 36, 100, 500000),
                S("Challenger Series",    "CHL",  2, Discipline.StockCar,   "USA", 38, 33,  75, 200000),
                S("Truck Series",         "TRK",  3, Discipline.Truck,      "USA", 36, 23,  65, 120000),
                S("National Stock Series","NSS",  4, Discipline.ShortTrack, "USA", 32, 20,  50,  50000),
                S("Indy Open Wheel",      "IOW",  1, Discipline.OpenWheel,  "USA", 27, 17,  90, 350000),
                S("Open Wheel Lights",    "OWL",  2, Discipline.OpenWheel,  "USA", 24, 14,  55,  80000),
                S("Outlaw Dirt Sprints",  "ODS",  1, Discipline.Dirt,       "USA", 26, 30,  60,  60000),
            };
        }
    }
}
