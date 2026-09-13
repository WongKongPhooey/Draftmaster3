using Draftmaster.Sim;
using NUnit.Framework;

// Where an unqualified driver lines up.
//
// SINGLE RACE has no qualifying in front of it, so the grid is drawn rather than earned — without that,
// every exhibition started 44th and was the same climb through the field. The career rule is the opposite
// and must not move: a driver who had a qualifying session and set no time starts from the back.
public class StartingGridTests
{
    const int FullField = 44;   // RaceScene spawns 43 AI + the player

    [Test]
    public void AMissedQualifyingSessionStillStartsFromTheBack()
    {
        Assert.AreEqual(FullField - 1, StartingGrid.UnqualifiedSlot(FullField, false));
        Assert.AreEqual(FullField - 1, StartingGrid.BackOfTheField(FullField));
    }

    [Test]
    public void ADrawAlwaysLandsInsideTheField()
    {
        var rng = new System.Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            int slot = StartingGrid.UnqualifiedSlot(FullField, true, rng);
            Assert.GreaterOrEqual(slot, 0, "A grid slot below pole is not a slot.");
            Assert.Less(slot, FullField, "A grid slot past the back of the field has no pit box.");
        }
    }

    // The point of the change: a single race is a different race each time, not the same one.
    [Test]
    public void ADrawIsNotAlwaysTheSameSlot()
    {
        var rng = new System.Random(7);
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 500; i++) seen.Add(StartingGrid.UnqualifiedSlot(FullField, true, rng));

        Assert.Greater(seen.Count, 10, "The draw is barely moving — that is not a random grid.");
        Assert.IsTrue(seen.Contains(0) || seen.Count > 30,
                      "500 draws over 44 slots should reach the front of the field.");
    }

    // Seeded, so a failure here is reproducible rather than a coin toss.
    [Test]
    public void TheDrawIsDeterministicForAGivenSeed()
    {
        var a = new System.Random(99);
        var b = new System.Random(99);
        for (int i = 0; i < 50; i++)
            Assert.AreEqual(StartingGrid.UnqualifiedSlot(FullField, true, a),
                            StartingGrid.UnqualifiedSlot(FullField, true, b));
    }

    // A one-car "field" has one slot, and nothing here may hand back a negative box index — that would be
    // a pit box that does not exist and a car parked off the end of the lane.
    [Test]
    public void DegenerateFieldsFallBackToPole()
    {
        foreach (bool drawn in new[] { true, false })
        {
            Assert.AreEqual(0, StartingGrid.UnqualifiedSlot(1, drawn));
            Assert.AreEqual(0, StartingGrid.UnqualifiedSlot(0, drawn));
            Assert.AreEqual(0, StartingGrid.UnqualifiedSlot(-5, drawn));
        }
    }

    // Production passes no rng and takes the shared draw; it must behave like the seeded one.
    [Test]
    public void TheSharedDrawAlsoStaysInsideTheField()
    {
        for (int i = 0; i < 500; i++)
        {
            int slot = StartingGrid.UnqualifiedSlot(FullField, true);
            Assert.GreaterOrEqual(slot, 0);
            Assert.Less(slot, FullField);
        }
    }
}
