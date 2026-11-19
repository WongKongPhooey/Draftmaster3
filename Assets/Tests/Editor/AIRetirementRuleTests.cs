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
    public void ACarWithAWheelOffBreaksDownWhateverItsDamage()
    {
        Assert.IsTrue(AIRetirementRule.BrokenDown(0.1f, 0.6f, 1, true), "a wheel off is the end of it");
        Assert.IsFalse(AIRetirementRule.BrokenDown(0.1f, 0.6f, 0, true));
        Assert.IsFalse(AIRetirementRule.BrokenDown(0.1f, 0.6f, 2, false), "switched off, wheels do not count");
        Assert.IsTrue(AIRetirementRule.BrokenDown(0.7f, 0.6f, 0, true), "the damage threshold still stands");
    }

    [Test]
    public void ACarInsideTheFrameIsInShot()
    {
        // 16:9 frame, 30 m half-height: corners ~60 m from centre.
        Assert.IsFalse(AIRetirementRule.OutOfShot(new Vector2(10f, 5f), 3f, Vector2.zero, 30f, 16f / 9f, 10f));
        Assert.IsFalse(AIRetirementRule.OutOfShot(new Vector2(50f, 0f), 3f, Vector2.zero, 30f, 16f / 9f, 10f));
    }

    [Test]
    public void ACarPastTheFramesCornersAndMarginIsOutOfShot()
    {
        float reach = Mathf.Sqrt(30f * 30f + (30f * 16f / 9f) * (30f * 16f / 9f)) + 3f + 10f;
        var centre = new Vector2(100f, -40f);
        Assert.IsFalse(AIRetirementRule.OutOfShot(centre + Vector2.up * (reach - 0.5f), 3f, centre, 30f, 16f / 9f, 10f));
        Assert.IsTrue(AIRetirementRule.OutOfShot(centre + Vector2.up * (reach + 0.5f), 3f, centre, 30f, 16f / 9f, 10f));
        // Whichever way the view is rolled: the corner circle covers every direction alike.
        Assert.IsTrue(AIRetirementRule.OutOfShot(centre + new Vector2(-1f, 1f).normalized * (reach + 0.5f), 3f,
                                                 centre, 30f, 16f / 9f, 10f));
    }

    [Test]
    public void ARetiringCarMovesOnlyOnceNeitherItNorItsSpotIsInShot()
    {
        Assert.IsTrue(AIRetirementRule.MoveOutOfSight(false, false, 0f, 90f));
        Assert.IsFalse(AIRetirementRule.MoveOutOfSight(true, false, 10f, 90f));
        Assert.IsFalse(AIRetirementRule.MoveOutOfSight(false, true, 10f, 90f));
        Assert.IsFalse(AIRetirementRule.MoveOutOfSight(true, true, 89f, 90f));
    }

    [Test]
    public void AViewThatNeverLooksAwayOnlyHoldsTheCarSoLong()
    {
        Assert.IsTrue(AIRetirementRule.MoveOutOfSight(true, true, 90f, 90f));
        Assert.IsFalse(AIRetirementRule.MoveOutOfSight(true, true, 10000f, 0f));   // 0 = wait for ever
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
