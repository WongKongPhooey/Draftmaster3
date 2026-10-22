using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Weekend;

// The National and Truck fields as Drivers rows, so the RV laptop can show them and scouting has something to
// uncover. Those two championships run every weekend off their real 2025 entry lists (SeriesEntryLists2025)
// with no rows in the Drivers table; this gives each driver a stat line, and their real number, team, make
// and age.
//
// Built in memory, never written to the database, and the same every time: each driver's ratings come from a
// hash of their name, and roster order is talent order (the simulator seeds its grid off it), so the top of
// the list rates higher. Trucks sit a rung below National, and both below the Cup field's real ratings.
public static class SimulatedField
{
    // Laptop series rows (ShortName) for the two championships that only exist as names.
    public static bool TryRacingSeries(Draftmaster.Data.Series row, out RacingSeries series)
    {
        series = RacingSeries.Cup;
        if (row == null) return false;
        if (row.ShortName == SeriesCatalog.ShortCode(RacingSeries.National)) { series = RacingSeries.National; return true; }
        if (row.ShortName == SeriesCatalog.ShortCode(RacingSeries.Trucks)) { series = RacingSeries.Trucks; return true; }
        return false;
    }

    public static List<Driver> Drivers(RacingSeries series)
    {
        var names = SeriesSimulator.RosterFor(series);
        var entries = SeriesEntryLists2025.For(series);
        var list = new List<Driver>(names.Length);
        // Ability band for the series: the bottom of the field to the top of it.
        int floor = series == RacingSeries.National ? 44 : 36;
        int ceiling = series == RacingSeries.National ? 72 : 64;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            var rng = new System.Random(DriverScouting.Seed(name) ^ ((int)series * 7919));
            float talent = names.Length > 1 ? 1f - i / (float)(names.Length - 1) : 1f;   // 1 = top of the list

            int ability = Clamp(floor + (int)((ceiling - floor) * talent) + rng.Next(-4, 5), 1, Driver.AbilityMax);
            int Stat() => Clamp((int)(ability / 5f) + rng.Next(-4, 5), 1, Driver.StatMax);

            int space = name.IndexOf(' ');
            bool real = i < entries.Count;
            int age = 19 + rng.Next(0, 22);
            if (real && entries[i].BirthYear > 0) age = SeasonYear - entries[i].BirthYear;
            var d = new Driver
            {
                FirstName = real ? entries[i].First : space > 0 ? name.Substring(0, space) : name,
                LastName = real ? entries[i].Last : space > 0 ? name.Substring(space + 1) : "",
                CarNumber = SeriesSimulator.NumberFor(series, i),
                Age = age,
                TeamName = real ? entries[i].Team : "",
                Manufacturer = real ? entries[i].Manufacturer : "",
                CurrentAbility = ability,
                PotentialAbility = Clamp(ability + rng.Next(0, 21), ability, Driver.AbilityMax),
            };
            d.ShortTracks = Stat(); d.Speedways = Stat(); d.Superspeedways = Stat();
            d.RoadCourses = Stat(); d.DirtCourses = Stat(); d.OpenWheel = Clamp(Stat() - 4, 1, Driver.StatMax);
            d.Qualifying = Stat(); d.Consistency = Stat(); d.Aggression = Stat(); d.Awareness = Stat();
            d.Adaptability = Stat(); d.TyreManagement = Stat(); d.FuelManagement = Stat();
            d.SponsorAppeal = Clamp(Stat() - 3, 1, Driver.StatMax);
            d.FanSupport = Clamp(Stat() - 3, 1, Driver.StatMax);
            d.Prestige = Clamp(Stat() - 4, 1, Driver.StatMax);
            list.Add(d);
        }
        list.Sort((a, b) => a.CarNumber.CompareTo(b.CarNumber));
        return list;
    }

    // The season the game opens on (CupRoster2026), which ages are counted to.
    const int SeasonYear = 2026;

    static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
}
