using Draftmaster.Weekend;
using UnityEngine;

// Which championship the player is entered in, when nobody has said.
//
// SeriesCatalog holds the answer and two screens can set it — the single-race menu and the schedule's own
// SERIES toggle — but a career that has never touched either falls back to the bottom rung of the ladder,
// the Trucks. Everything else about the player says Cup: they are sat in a Cup car in Cup paint, with a Cup
// number, a Cup team and a garage on the Cup roster.
//
// That disagreement is not cosmetic. The weekend builds one sheet per championship: YOUR practice, and two
// other people's to watch. Believing the player is a truck driver made the Truck practice their own hour in
// the car, so the objective marker pointed at their Cup car and told them to go and drive it — instead of
// sending them to the grandstand to watch somebody else's session, which is what a Cup driver does while
// the trucks are out.
//
// So the entry follows the car when nothing has chosen it. The car is the thing the player can see.
public static class PlayerEntry
{
    // Carset prefixes, when there is no GridSpawner in the scene to ask. These are the defaults on it.
    const string TruckCarset = "cts";
    const string NationalCarset = "xfi";
    const string CupCarset = "cup";

    // Read the player's car and, if they have never chosen a championship, enter them in the one it races
    // in. Does nothing once a choice exists: picking Trucks in the single-race menu and then finding
    // yourself in Cup would be worse than the bug this fixes.
    public static void EnsureFromTheCar()
    {
        if (SeriesCatalog.HasEntry) return;

        var car = CarIdentity.FindPlayerCar();
        var label = car != null ? car.GetComponent<DriverLabel>() : null;
        if (label == null || string.IsNullOrEmpty(label.carset)) return;

        if (!TrySeriesFromCarset(label.carset, out var series)) return;

        SeriesCatalog.PlayerSeries = series;
        Debug.Log($"PlayerEntry: no championship chosen, so entered the player in " +
                  $"{SeriesCatalog.Name(series)} — the series their car ({label.carset}) races in.");
    }

    // The championship a carset belongs to. Asks the scene's GridSpawner first, because the prefixes are
    // serialized on it and a scene is free to run a different set of paint.
    public static bool TrySeriesFromCarset(string carset, out RacingSeries series)
    {
        series = RacingSeries.Trucks;
        if (string.IsNullOrEmpty(carset)) return false;

        var grid = Object.FindFirstObjectByType<GridSpawner>();
        if (grid != null)
        {
            if (Matches(carset, grid.trucksCarsetPrefix)) { series = RacingSeries.Trucks; return true; }
            if (Matches(carset, grid.nationalCarsetPrefix)) { series = RacingSeries.National; return true; }
            if (Matches(carset, grid.cupCarsetPrefix)) { series = RacingSeries.Cup; return true; }
        }

        if (Matches(carset, TruckCarset)) { series = RacingSeries.Trucks; return true; }
        if (Matches(carset, NationalCarset)) { series = RacingSeries.National; return true; }
        if (Matches(carset, CupCarset)) { series = RacingSeries.Cup; return true; }

        return false;
    }

    static bool Matches(string carset, string prefix) =>
        !string.IsNullOrEmpty(prefix) && carset.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);
}
