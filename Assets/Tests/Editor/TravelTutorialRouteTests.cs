using System.Collections.Generic;
using Draftmaster.Tracks;
using NUnit.Framework;

// The travel map's tutorial leg: the route has to take in every waypoint, the destination waits until it
// has, and no hop is allowed that would leave a waypoint out of reach on the stops remaining.
public class TravelTutorialRouteTests
{
    // A line S - A - B - C - D with a spur B - H:  H is "HQ", C is "Garage", D the destination.
    static readonly Dictionary<string, string[]> Roads = new()
    {
        ["S"] = new[] { "A" },
        ["A"] = new[] { "S", "B" },
        ["B"] = new[] { "A", "C", "H" },
        ["H"] = new[] { "B" },
        ["C"] = new[] { "B", "D" },
        ["D"] = new[] { "C" },
    };

    static int Hops(string from, string to)
    {
        if (from == to) return 0;
        var dist = new Dictionary<string, int> { [from] = 0 };
        var q = new Queue<string>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nb in Roads[cur])
            {
                if (dist.ContainsKey(nb)) continue;
                dist[nb] = dist[cur] + 1;
                if (nb == to) return dist[nb];
                q.Enqueue(nb);
            }
        }
        return -1;
    }

    static readonly string[] Both = { "H", "C" };

    [Test]
    public void Waypoint_IsTeamHQ_OnTheWayToDaytona()
    {
        Assert.AreEqual("WatkinsGlen", TravelTutorialRoute.From);
        Assert.AreEqual("Daytona", TravelTutorialRoute.To);
        CollectionAssert.AreEqual(new[] { "team_factory" }, TravelTutorialRoute.Waypoints);
        Assert.AreEqual("Stop off at Team HQ to apply new parts and sponsors.", TravelTutorialRoute.Prompt);
    }

    [Test]
    public void RouteHops_TakesTheShortestOrderThroughEveryWaypoint()
    {
        // S-A-B-H-B-C-D = 6, whichever order is named.
        Assert.AreEqual(6, TravelTutorialRoute.RouteHops("S", Both, "D", Hops));
        Assert.AreEqual(6, TravelTutorialRoute.RouteHops("S", new[] { "C", "H" }, "D", Hops));
        Assert.AreEqual(4, TravelTutorialRoute.RouteHops("S", new string[0], "D", Hops));
    }

    [Test]
    public void CanStep_RefusesTheDestination_UntilTheWaypointsAreDone()
    {
        Assert.IsFalse(TravelTutorialRoute.CanStep("D", new[] { "H" }, "D", 10, Hops));
        Assert.IsTrue(TravelTutorialRoute.CanStep("D", new string[0], "D", 1, Hops));
    }

    [Test]
    public void CanStep_RefusesAHopThatLeavesAWaypointOutOfReach()
    {
        // At B with 4 stops: B-H-B-C-D is exactly 4, so heading to C first (C-B-H-B-C-D = 5 after) is out.
        Assert.IsTrue(TravelTutorialRoute.CanStep("H", Both, "D", 4, Hops));
        Assert.IsFalse(TravelTutorialRoute.CanStep("C", Both, "D", 4, Hops));
        // With slack the long way round is fine.
        Assert.IsTrue(TravelTutorialRoute.CanStep("C", Both, "D", 6, Hops));
    }

    [Test]
    public void CanStep_RefusesEverything_WithNoStopsLeft()
    {
        Assert.IsFalse(TravelTutorialRoute.CanStep("H", Both, "D", 0, Hops));
    }
}
