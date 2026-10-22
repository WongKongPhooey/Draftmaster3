using System.Collections.Generic;
using System.Linq;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// AI retirement (AIRetirement): when a car is too damaged to keep going, when a stopped car is retired, and the
// checks that decide where off the circuit it is parked.
public class AIRetirementRuleTests
{
    [Test]
    public void ACarBreaksDownOnlyPastTheDamageThreshold()
    {
        Assert.IsFalse(AIRetirementRule.BrokenDown(0.59f, 0.6f));
        Assert.IsTrue(AIRetirementRule.BrokenDown(0.6f, 0.6f));
        Assert.IsTrue(AIRetirementRule.BrokenDown(1f, 0.6f));
    }

    [Test]
    public void AZeroThresholdMeansCarsNeverBreakDown()
    {
        Assert.IsFalse(AIRetirementRule.BrokenDown(1f, 0f));
    }

    [Test]
    public void ABrokenCarRetiresSoonerThanAStuckOne()
    {
        Assert.IsTrue(AIRetirementRule.Retire(4f, brokenDown: true, brokenWait: 4f, stuckWait: 15f));
        Assert.IsFalse(AIRetirementRule.Retire(4f, brokenDown: false, brokenWait: 4f, stuckWait: 15f));
        Assert.IsTrue(AIRetirementRule.Retire(15f, brokenDown: false, brokenWait: 4f, stuckWait: 15f));
    }

    [Test]
    public void AMovingCarNeverRetires()
    {
        Assert.IsFalse(AIRetirementRule.Retire(-1f, true, 0f, 0f));
    }

    [Test]
    public void SpotsAreTriedBesideTheCarFirstThenAlternatelyAheadAndBehind()
    {
        CollectionAssert.AreEqual(new[] { 0f, 10f, -10f, 20f, -20f }, AIRetirementRule.AlongOffsets(10f, 20f).ToArray());
        CollectionAssert.AreEqual(new[] { 0f }, AIRetirementRule.AlongOffsets(0f, 20f).ToArray());
    }

    [Test]
    public void ASpotMustClearEveryPartOfTheRoad()
    {
        // A straight 15 m wide along x, and a second stretch of the same circuit 40 m away (a road course infield).
        var centres = new List<Vector2>();
        var halves = new List<float>();
        for (int x = 0; x <= 100; x += 2) { centres.Add(new Vector2(x, 0f)); halves.Add(7.5f); }
        for (int x = 0; x <= 100; x += 2) { centres.Add(new Vector2(x, 40f)); halves.Add(7.5f); }

        Assert.IsFalse(AIRetirementRule.ClearOfRoad(new Vector2(50f, 9f), centres, halves, 4f), "beside the first road");
        Assert.IsTrue(AIRetirementRule.ClearOfRoad(new Vector2(50f, 20f), centres, halves, 4f), "between the two");
        Assert.IsFalse(AIRetirementRule.ClearOfRoad(new Vector2(50f, 30f), centres, halves, 4f), "beside the second road");
    }

    [Test]
    public void RetiredCarsKeepTheirDistanceFromEachOther()
    {
        var taken = new List<Vector2> { new Vector2(0f, 0f) };
        Assert.IsFalse(AIRetirementRule.ClearOfOthers(new Vector2(5f, 0f), taken, 8f));
        Assert.IsTrue(AIRetirementRule.ClearOfOthers(new Vector2(9f, 0f), taken, 8f));
        Assert.IsTrue(AIRetirementRule.ClearOfOthers(new Vector2(5f, 0f), null, 8f));
    }

    [Test]
    public void NearestIndexFindsTheClosestSample()
    {
        var centres = new List<Vector2> { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(20f, 0f) };
        Assert.AreEqual(1, AIRetirementRule.NearestIndex(new Vector2(11f, 3f), centres));
        Assert.AreEqual(-1, AIRetirementRule.NearestIndex(Vector2.zero, new List<Vector2>()));
    }
}
