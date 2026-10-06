using System.Collections.Generic;
using Draftmaster.Tracks;
using NUnit.Framework;

// The travel map's drawn route: the shortest road from the car through every tapped stop to the race,
// redrawn whenever a stop is added, and never passing through the race on the way to a stop.
public class TravelRoutePlanTests
{
    // A line S - A - B - C - D with a spur B - H, and a loop C - E - D so D can be avoided on the way to E.
    static readonly Dictionary<string, string[]> Roads = new()
    {
        ["S"] = new[] { "A" },
        ["A"] = new[] { "S", "B" },
        ["B"] = new[] { "A", "C", "H" },
        ["H"] = new[] { "B" },
        ["C"] = new[] { "B", "D", "E" },
        ["D"] = new[] { "C", "F" },
        ["E"] = new[] { "C", "F" },
        ["F"] = new[] { "D", "E" },
        ["X"] = new string[0],
    };

    static IEnumerable<string> Neighbors(string id) => Roads.TryGetValue(id, out var n) ? n : new string[0];

    [Test]
    public void Path_IsTheShortestRoad_EndingAtTheTarget()
    {
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, TravelRoutePlan.Path("S", "D", Neighbors));
        CollectionAssert.IsEmpty(TravelRoutePlan.Path("S", "S", Neighbors));
        Assert.IsNull(TravelRoutePlan.Path("S", "X", Neighbors));
    }

    [Test]
    public void Route_WithNoStops_IsTheDirectRoad()
    {
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, TravelRoutePlan.Route("S", null, "D", Neighbors));
    }

    [Test]
    public void Route_GoesThroughEveryStop_InOrder()
    {
        CollectionAssert.AreEqual(new[] { "A", "B", "H", "B", "C", "D" },
                                  TravelRoutePlan.Route("S", new[] { "H" }, "D", Neighbors));
    }

    [Test]
    public void Route_NeverPassesTheDestination_OnTheWayToAStop()
    {
        // F is shortest via D, but D is where the trip ends, so the road to F goes round by E.
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "E", "F", "D" },
                                  TravelRoutePlan.Route("S", new[] { "F" }, "D", Neighbors));
    }

    [Test]
    public void Route_IsNull_WhenAStopCannotBeReached()
    {
        Assert.IsNull(TravelRoutePlan.Route("S", new[] { "X" }, "D", Neighbors));
    }

    [Test]
    public void InsertStop_PutsTheNewStopWhereItCostsLeast()
    {
        // Already calling at E; H is on the way out, so it goes first rather than after E.
        CollectionAssert.AreEqual(new[] { "H", "E" },
                                  TravelRoutePlan.InsertStop("S", new[] { "E" }, "H", "D", Neighbors));
        // From the far side the order flips.
        CollectionAssert.AreEqual(new[] { "E", "H" },
                                  TravelRoutePlan.InsertStop("F", new[] { "E" }, "H", "D", Neighbors));
        Assert.IsNull(TravelRoutePlan.InsertStop("S", new string[0], "X", "D", Neighbors));
    }
}
