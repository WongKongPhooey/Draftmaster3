using SQLite;

namespace Draftmaster.Data
{
    // One driver's career in one championship. A driver can race in more than one series (a Cup regular
    // running Xfinity on Saturday, a truck driver stepping up), so their numbers are kept per series rather than
    // on the Drivers row. Keyed by name and series short code (CUP / NSS / TRK), because the National and
    // Truck fields have no rows in the Drivers table.
    //
    // Seeded from Draftmaster.Weekend.SeriesCareers2025 — real careers to the end of the 2025 season — and the
    // place a season's results are added to from here on.
    [Table("DriverSeriesStats")]
    public class DriverSeriesStats
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Draftmaster.Weekend.SeriesCareers2025.Key of the driver's name: lower case, letters only, no accents.
        [Indexed] public string DriverKey { get; set; }
        public string DriverName { get; set; }
        [Indexed] public string SeriesShortName { get; set; }

        public int Starts { get; set; }
        public int Wins { get; set; }
        public int Top10s { get; set; }
        public int Poles { get; set; }
        // Championship years, comma separated ("2021,2025"). Empty = none.
        public string TitleYears { get; set; } = "";
    }
}
