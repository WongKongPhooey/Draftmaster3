using System.Collections.Generic;
using System.Linq;
using Draftmaster.Data;
using Draftmaster.MasterDraft;

// Turns the game's own data into the plain sources Master Draft's catalogue is built from: driver cards from
// the Drivers table, crew chief cards from the Staff table. Read-only — nothing here writes the career DB.
//
// When the database isn't up (Master Draft can be entered before DatabaseManager has finished), the drivers
// come from the same seed list the database is filled from (DummyDrivers.Build), numbered in insertion order
// so their ids match the rows the database would give them.
public static class MasterDraftSources
{
    public static CardCatalog BuildCatalog() => new CardCatalog(Drivers(), CrewChiefs());

    public static List<DriverSource> Drivers()
    {
        List<Driver> rows = null;
        var db = Connection();
        if (db != null)
        {
            try { rows = db.Table<Driver>().ToList().Where(d => d != null && !d.Retired).ToList(); }
            catch { rows = null; }
        }

        if (rows == null || rows.Count == 0)
        {
            rows = DummyDrivers.Build();
            for (int i = 0; i < rows.Count; i++) if (rows[i].Id == 0) rows[i].Id = i + 1;
        }

        return rows.Select(ToSource).ToList();
    }

    public static DriverSource ToSource(Driver d) => new DriverSource
    {
        id = d.Id,
        firstName = d.FirstName,
        lastName = d.LastName,
        carNumber = d.CarNumber,
        team = d.TeamName,
        manufacturer = d.Manufacturer,
        currentAbility = d.CurrentAbility,
        shortTracks = d.ShortTracks,
        speedways = d.Speedways,
        superspeedways = d.Superspeedways,
        roadCourses = d.RoadCourses,
        dirtCourses = d.DirtCourses,
        qualifying = d.Qualifying,
        consistency = d.Consistency,
        aggression = d.Aggression,
        awareness = d.Awareness,
        tyreManagement = d.TyreManagement,
    };

    // Crew chiefs on file. Empty when the Staff table is (CardCatalog then generates its own).
    public static List<CrewChiefSource> CrewChiefs()
    {
        var list = new List<CrewChiefSource>();
        var db = Connection();
        if (db == null) return list;
        try
        {
            foreach (var s in db.Table<Staff>().ToList())
                if (s != null && s.Role == StaffRole.CrewChief && s.Active) list.Add(new CrewChiefSource { name = s.Name, rating = s.Rating });
        }
        catch { list.Clear(); }
        return list;
    }

    // Draftmaster.Data.TrackType and Core's TrackKind are numbered alike.
    public static TrackKind KindOf(TrackType type) => (TrackKind)(int)type;

    static SQLite.SQLiteConnection Connection()
    {
        var dbm = DatabaseManager.Instance;
        return dbm != null && dbm.IsReady ? dbm.Connection : null;
    }
}
