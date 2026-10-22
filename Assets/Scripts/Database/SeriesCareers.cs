using System.Collections.Generic;
using System.Linq;
using Draftmaster.Data;
using Draftmaster.Weekend;

// A driver's career in one championship, read the way the rest of the database layer reads things: the
// DriverSeriesStats table when the database is open (it is where results get added), the 2025 seed it was
// filled from when it is not — every menu scene in the editor is that case.
public static class SeriesCareers
{
    public static bool TryGet(Driver driver, RacingSeries series, out SeriesCareers2025.Record record)
    {
        record = default;
        if (driver == null) return false;
        string name = ((driver.FirstName ?? "") + " " + (driver.LastName ?? "")).Trim();

        var dbm = DatabaseManager.Instance;
        if (dbm != null && dbm.IsReady)
        {
            try
            {
                string key = SeriesCareers2025.Key(name);
                string code = SeriesCatalog.ShortCode(series);
                var row = dbm.Connection.Table<DriverSeriesStats>()
                             .Where(r => r.DriverKey == key && r.SeriesShortName == code)
                             .FirstOrDefault();
                if (row != null)
                {
                    record = new SeriesCareers2025.Record(row.Starts, row.Wins, row.Top10s, row.Poles, Years(row.TitleYears));
                    return true;
                }
            }
            catch { /* fall through to the seed — a sheet with numbers beats an exception */ }
        }

        return SeriesCareers2025.TryGet(name, series, out record);
    }

    // The table's rows, one per driver per series, from the 2025 seed.
    public static List<DriverSeriesStats> SeedRows()
    {
        var rows = new List<DriverSeriesStats>();
        foreach (var (name, series, r) in SeriesCareers2025.All())
            rows.Add(new DriverSeriesStats
            {
                DriverKey = SeriesCareers2025.Key(name),
                DriverName = name,
                SeriesShortName = SeriesCatalog.ShortCode(series),
                Starts = r.Starts, Wins = r.Wins, Top10s = r.Top10s, Poles = r.Poles,
                TitleYears = string.Join(",", r.TitleYears),
            });
        return rows;
    }

    static int[] Years(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new int[0];
        var years = new List<int>();
        foreach (var part in csv.Split(','))
            if (int.TryParse(part.Trim(), out int y)) years.Add(y);
        return years.ToArray();
    }
}
