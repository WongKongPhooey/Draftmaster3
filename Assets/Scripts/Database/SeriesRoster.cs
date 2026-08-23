using System.Collections.Generic;
using System.Linq;
using Draftmaster.Data;

// Who races in which championship — the lookup behind the garage sheet's SERIES and DRIVER dropdowns.
//
// "Which drivers are in a series" has two answers in this game and this picks whichever one exists:
//
//   1. The Entries table, once a season has been generated. That is the real answer: one row per seat,
//      keyed to a series and a season, so a driver who moved teams mid-career is in the series their
//      entry says they are.
//   2. Nothing has generated a season yet on most saves, and no menu scene has even opened the database
//      (DatabaseManager lives in the race scene), so the fallback is the seeded field: the Drivers table
//      is CupRoster2026, which IS the top stock-car championship's entry list. Every other series is
//      honestly empty rather than being filled with borrowed drivers.
//
// Series come from the Series table when it is open and DummySeries — the same rows the table is seeded
// from — when it is not, so the dropdown reads the same in a menu scene as it does mid-career.
//
// Nothing here writes: the garage sheet browses the field, it does not sign anybody.
public static class SeriesRoster
{
    // Every championship the world runs, in table order. Never null, never empty (the seed list is the
    // floor), so a caller can always build a dropdown out of it.
    public static List<Draftmaster.Data.Series> AllSeries()
    {
        var db = Connection();
        if (db != null)
        {
            try
            {
                var rows = db.Table<Draftmaster.Data.Series>().ToList()
                             .Where(s => s != null && s.Active)
                             .OrderBy(s => s.Id)
                             .ToList();
                if (rows.Count > 0) return rows;
            }
            catch { /* fall through to the seed list — a browsable dropdown beats an exception */ }
        }

        // The seed rows are built in memory, so they carry no primary keys. Number them in list order:
        // ids are only ever used to pair a series with its own roster inside one session.
        var seeded = DummySeries.Build();
        for (int i = 0; i < seeded.Count; i++)
            if (seeded[i].Id == 0) seeded[i].Id = i + 1;
        return seeded;
    }

    // The field for one series, by car number. Empty means exactly that: nobody is entered yet.
    public static List<Driver> Drivers(Draftmaster.Data.Series series)
    {
        if (series == null) return new List<Driver>();

        var db = Connection();
        if (db != null)
        {
            try
            {
                // Latest season on file for this series — an old season's entries shouldn't outvote it.
                var entries = db.Table<Draftmaster.Data.Entry>().ToList()
                                .Where(e => e != null && e.Active && e.SeriesId == series.Id)
                                .ToList();
                if (entries.Count > 0)
                {
                    int season = entries.Max(e => e.Season);
                    var seats = new HashSet<int>(entries.Where(e => e.Season == season).Select(e => e.DriverId));
                    var entered = db.Table<Driver>().ToList().Where(d => d != null && seats.Contains(d.Id)).ToList();
                    if (entered.Count > 0) return ByCarNumber(entered);
                }
            }
            catch { /* fall through */ }
        }

        if (!IsSeededField(series)) return new List<Driver>();

        if (db != null)
        {
            try
            {
                var table = db.Table<Driver>().ToList()
                              .Where(d => d != null && !d.Retired && d.CarNumber > 0)
                              .ToList();
                if (table.Count > 0) return ByCarNumber(table);
            }
            catch { /* fall through */ }
        }

        return ByCarNumber(CupRoster2026.BuildDrivers());
    }

    // The series the seeded Drivers table belongs to: the top tier of the stock-car ladder, which is the
    // championship CupRoster2026 lists. Everything else has no entry list until a season generates one.
    public static bool IsSeededField(Draftmaster.Data.Series series) =>
        series != null && series.Tier <= 1 && series.Discipline == Discipline.StockCar;

    // Which series to open the sheet on: the one the player's car number races in, else the first.
    public static int SeriesIndexFor(List<Draftmaster.Data.Series> series, int carNumber)
    {
        if (series == null || series.Count == 0) return -1;
        if (carNumber > 0)
            for (int i = 0; i < series.Count; i++)
                if (Drivers(series[i]).Any(d => d.CarNumber == carNumber)) return i;
        return 0;
    }

    public static int DriverIndexFor(List<Driver> drivers, int carNumber)
    {
        if (drivers == null || drivers.Count == 0) return -1;
        if (carNumber > 0)
            for (int i = 0; i < drivers.Count; i++)
                if (drivers[i] != null && drivers[i].CarNumber == carNumber) return i;
        return 0;
    }

    // ------------------------------------------------------------------ labels

    // What a series reads as on a 168px dropdown: its full name, which is what the player calls it.
    public static string Label(Draftmaster.Data.Series series)
    {
        if (series == null) return "";
        if (!string.IsNullOrWhiteSpace(series.Name)) return series.Name.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(series.ShortName)) return series.ShortName.Trim().ToUpperInvariant();
        return "SERIES " + series.Id;
    }

    // A driver reads as the car first, the way a timing tower does — the number is what the player is
    // looking for when they scroll a 38-car field.
    public static string Label(Driver driver)
    {
        if (driver == null) return "";
        string name = string.Join(" ", new[] { driver.FirstName, driver.LastName }
                                       .Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(name)) name = RosterLookup.LabelName(driver);
        name = name.Trim().ToUpperInvariant();
        return driver.CarNumber > 0 ? $"#{driver.CarNumber} {name}" : name;
    }

    // ------------------------------------------------------------------ plumbing

    // The open database, or null when there isn't one — every menu scene in the editor is that case.
    static SQLite.SQLiteConnection Connection()
    {
        var dbm = DatabaseManager.Instance;
        return dbm != null && dbm.IsReady ? dbm.Connection : null;
    }

    static List<Driver> ByCarNumber(List<Driver> drivers) =>
        drivers.OrderBy(d => d.CarNumber).ToList();
}
