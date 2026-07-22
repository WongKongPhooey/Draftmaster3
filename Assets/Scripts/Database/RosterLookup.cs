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
}
