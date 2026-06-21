using System.Collections.Generic;

namespace Draftmaster.Data
{
    public static class DummySponsors
    {
        static Sponsor Sp(string name, string industry, int wealth, int prestige,
            int seasonValue, int bonusWin, int bonusPodium, int minPrestige)
        {
            return new Sponsor
            {
                Name = name, Industry = industry, Wealth = wealth, Prestige = prestige,
                SeasonValue = seasonValue, BonusPerWin = bonusWin, BonusPerPodium = bonusPodium,
                MinPrestige = minPrestige
            };
        }

        public static List<Sponsor> Build()
        {
            return new List<Sponsor>
            {
                Sp("Voltage Energy",    "Energy",   95, 90, 3000000, 100000, 40000, 85),
                Sp("Apateq Telecom",    "Telecom",  90, 88, 2600000,  90000, 35000, 80),
                Sp("MaxiMart",          "Retail",   85, 75, 1800000,  60000, 25000, 65),
                Sp("TorqueParts",       "Auto",     70, 65, 1100000,  50000, 20000, 55),
                Sp("Summit Bank",       "Bank",     92, 85, 2400000,  80000, 30000, 78),
                Sp("Nexus Tech",        "Tech",     88, 82, 2200000,  85000, 32000, 75),
                Sp("Roadhouse Grill",   "Food",     60, 55,  700000,  30000, 12000, 45),
                Sp("Pioneer Oil",       "Oil",      80, 70, 1500000,  55000, 22000, 60),
                Sp("Sureguard Insure",  "Insurance",75, 68, 1300000,  45000, 18000, 58),
                Sp("Hydro Spring Water","Beverage",  55, 50,  500000,  20000,  8000, 35),
                Sp("Ironclad Tools",    "Auto",     65, 58,  850000,  35000, 14000, 50),
                Sp("Skyline Airlines",  "Travel",   82, 78, 1700000,  60000, 24000, 70),
            };
        }
    }
}
