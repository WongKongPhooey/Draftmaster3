using System.Linq;
using Draftmaster.Data;

// Resolves a car number to the driver who races it. Prefers the Drivers table so edits made in
// Window ▸ Draftmaster ▸ Driver Database take effect, and falls back to the code-defined roster when
// the database hasn't finished opening (callers that run before DatabaseManager.IsReady still get an answer).
public static class RosterLookup
{
    public static Driver ByCarNumber(int carNumber)
    {
        if (carNumber <= 0) return null;

        var dbm = DatabaseManager.Instance;
        if (dbm != null && dbm.IsReady)
        {
            var row = dbm.Connection.Table<Driver>().FirstOrDefault(d => d.CarNumber == carNumber);
            if (row != null) return row;
        }

        foreach (var e in CupRoster2026.Entries)
            if (e.Number == carNumber) return CupRoster2026.BuildDriver(e);
        return null;
    }

    // The name a driver competes under: what GridSpawner writes into their DriverLabel, what the position
    // tracker shows, and — crucially — the identity DriverRelationships files rivalries under. Anything that
    // needs to look a driver up in that system (the paddock's RivalDriverNPC, quests, the dossier) must use
    // this and not their full name, or it keys a second, empty relationship for the same person.
    public static string LabelName(Driver d)
    {
        if (d == null) return "";
        if (!string.IsNullOrEmpty(d.ShortName)) return d.ShortName;
        return d.LastName ?? "";
    }
}
