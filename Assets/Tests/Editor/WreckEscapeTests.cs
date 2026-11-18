using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;

// WreckEscape: when a car is stuck behind a stopped car, and which way round a pile-up there is.
// Laterals in metres (+ right), gaps centre to centre (+ ahead). Cars 2 m wide, 4.8 m long.
public class WreckEscapeTests
{
    static WreckEscape.Settings S => WreckEscape.Settings.Default;   // clear 1.2 m
    static WreckEscape.Self Me(float lat) => new WreckEscape.Self { Lateral = lat, HalfWidth = 1f, HalfLength = 2.4f };
    static WreckEscape.Car Car(float gap, float lat, float mph = 0f) =>
        new WreckEscape.Car { Gap = gap, Lateral = lat, HalfWidth = 1f, HalfLength = 2.4f, SpeedMph = mph };

    [Test]
    public void AStoppedCarInMyLaneAheadHasMeStuck()
    {
        var cars = new List<WreckEscape.Car> { Car(7f, 0.5f) };
        Assert.AreEqual(0, WreckEscape.StoppedBlocker(Me(0f), cars, S));
    }

    [Test]
    public void AMovingCarOrOneInAnotherLaneDoesNot()
    {
        var cars = new List<WreckEscape.Car> { Car(7f, 0f, 30f), Car(7f, 3f), Car(-7f, 0f) };
        Assert.AreEqual(-1, WreckEscape.StoppedBlocker(Me(0f), cars, S));
    }

    [Test]
    public void AStoppedCarBeyondTheScanIsNotYet()
    {
        var cars = new List<WreckEscape.Car> { Car(40f, 0f) };
        Assert.AreEqual(-1, WreckEscape.StoppedBlocker(Me(0f), cars, S));
    }

    [Test]
    public void OneStoppedCarIsGoneRoundOnTheNearerSideWithDaylight()
    {
        var cars = new List<WreckEscape.Car> { Car(7f, 0.5f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -6f, 6f, S, out float target, out float past);
        Assert.AreEqual(-1, side);
        Assert.AreEqual(0.5f - 1f - 1f - 1.2f, target, 1e-4f);
        Assert.AreEqual(7f + 2.4f + 2.4f + 1.2f, past, 1e-4f);
    }

    [Test]
    public void APileUpIsWalkedAcrossToTheFarEdgeOfIt()
    {
        // Three cars side by side, a car's width of daylight short of fitting between any two of them.
        var cars = new List<WreckEscape.Car> { Car(7f, 0f), Car(9f, 3f), Car(12f, -3f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -20f, 20f, S, out float target, out float past);
        Assert.AreNotEqual(0, side);
        float expected = side > 0 ? 3f + 1f + 1f + 1.2f : -3f - 1f - 1f - 1.2f;
        Assert.AreEqual(expected, target, 1e-4f);
        Assert.Greater(past, (side > 0 ? 9f : 12f) + 4.8f);
    }

    [Test]
    public void APileUpThatFillsTheLimitsHasNoWayRound()
    {
        var cars = new List<WreckEscape.Car> { Car(7f, 0f), Car(9f, 3f), Car(12f, -3f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -6f, 6f, S, out _, out _);
        Assert.AreEqual(0, side);
    }

    [Test]
    public void WiderLimitsOnOneSideOpenThatSide()
    {
        // The road is full; the grass on the left (down to -12) is not.
        var cars = new List<WreckEscape.Car> { Car(7f, 0f), Car(9f, 3f), Car(12f, -3f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -12f, 6f, S, out float target, out _);
        Assert.AreEqual(-1, side);
        Assert.AreEqual(-6.2f, target, 1e-4f);
    }

    [Test]
    public void AGapBetweenTwoStoppedCarsIsNotTakenWhenTooNarrow()
    {
        // 3.5 m between the two (edges at 1 and 4.5): a car needs 2 m and 1.2 m daylight each side.
        var cars = new List<WreckEscape.Car> { Car(7f, 0f), Car(7f, 5.5f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -2f, 20f, S, out float target, out _);
        Assert.AreEqual(1, side);
        Assert.AreEqual(5.5f + 1f + 1f + 1.2f, target, 1e-4f);
    }

    [Test]
    public void AMovingCarAtTheSpotShutsThatSide()
    {
        var cars = new List<WreckEscape.Car> { Car(7f, 0f), Car(1f, -3.2f, 10f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -10f, 10f, S, out _, out _);
        Assert.AreEqual(1, side);
    }

    [Test]
    public void AStoppedCarAlongsideMeShutsThatSide()
    {
        // Pinned on the left by a car stopped door to door: the way round is to the right.
        var cars = new List<WreckEscape.Car> { Car(7f, 0f), Car(0f, -2.5f) };
        int side = WreckEscape.FindWayRound(Me(0f), cars, 0, -20f, 4.5f, S, out _, out _);
        Assert.AreEqual(1, side);
        side = WreckEscape.FindWayRound(Me(0f), cars, 0, -20f, 3f, S, out _, out _);
        Assert.AreEqual(0, side, "the right is outside the limits and the left is blocked by the car beside me");
    }
}
