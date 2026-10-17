using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;

// The NR2003-style lateral planner on its own: the behaviours a pack should get from it without any rule saying so.
public class LateralFieldTests
{
    const float Top = 85f;   // m/s, about plate speed
    const float Lo = -4f, Hi = 4f;

    static FieldCar Car(float gap, float lat, float speed, float latRate = 0f) =>
        new FieldCar { Gap = gap, Lateral = lat, Speed = speed, LateralRate = latRate, Heading = float.NaN };

    static LateralField.Result Solve(float myLat, float mySpeed, params FieldCar[] cars) =>
        new LateralField().Solve(myLat, mySpeed, Top, Lo, Hi, 0f, float.NaN, new List<FieldCar>(cars));

    [Test]
    public void EmptyRoadHoldsTheLine()
    {
        var r = Solve(1f, Top);
        Assert.AreEqual(0f, r.TargetLateral, 0.3f, "alone, the car should drift to its line");
        Assert.AreEqual(float.MaxValue, r.SpeedCap);
    }

    [Test]
    public void TucksIntoTheTowOfACarAhead()
    {
        // Same speed, 15 m up the road, one lane over: the tow behind it beats clean air.
        var r = Solve(0f, Top, Car(15f, 2.4f, Top));
        Assert.AreEqual(2.4f, r.TargetLateral, 0.5f);
    }

    [Test]
    public void PullsOutRoundASlowerCar()
    {
        // 15 mph slower and 12 m ahead in our slot: stuck behind it is worth less than the open road beside it.
        var r = Solve(0f, Top, Car(12f, 0f, Top - 7f));
        Assert.Greater(System.Math.Abs(r.TargetLateral), 1.8f, "should move out of its corridor");
    }

    [Test]
    public void StaysInLineBehindACarOnTheBumperAtTheSameSpeed()
    {
        // Bump drafting in a line: the car ahead is towed by the one ahead of it, so the line runs quicker than clean
        // air. Pulling out loses the tow - stay put. (Two cars alone are different: the follower has the slingshot.)
        float line = Top * 1.08f;
        var r = Solve(0f, line, Car(6.2f, 0f, line), Car(12.4f, 0f, line));
        Assert.AreEqual(0f, r.TargetLateral, 0.5f);
    }

    [Test]
    public void NeverMovesIntoACarAlongside()
    {
        // A car door to door on our + side, and a slow car ahead making the + side tempting: the wall holds.
        var r = Solve(0f, Top, Car(1f, 2.6f, Top), Car(10f, 0f, Top - 10f));
        Assert.LessOrEqual(r.TargetLateral, 2.6f - 2.4f + 0.01f);
    }

    [Test]
    public void TheWallIsWhereTheCarAlongsideIsGoing()
    {
        // The car alongside is moving toward us at 2 m/s: half a second on it is a metre closer.
        var r = Solve(-1f, Top, Car(0f, 2.6f, Top, latRate: -2f), Car(10f, -1f, Top - 10f));
        Assert.LessOrEqual(r.WallHigh, 2.6f - 1f - 2.4f + 0.01f);
    }

    [Test]
    public void DoesNotCutInFrontOfACarClosingFromBehind()
    {
        // A slow car ahead in our slot; the + side is open, but a car 10 m back in it is closing at 10 m/s.
        var r = Solve(0f, Top - 5f, Car(10f, 0f, Top - 12f), Car(-10f, 2.4f, Top + 5f));
        Assert.Less(r.TargetLateral, 1.2f, "should not pull out in front of the closing car");
    }

    [Test]
    public void ThreeWideInTheMiddleAimsForTheGap()
    {
        var r = Solve(0f, Top, Car(0.5f, 2.2f, Top), Car(-0.5f, -2.2f, Top));
        Assert.IsTrue(r.Squeezed);
        Assert.AreEqual(0f, r.TargetLateral, 0.01f);
    }

    [Test]
    public void JoinsTheFasterLineBeside()
    {
        // Our line ahead is slowing; the line beside, a car's length further up, is running quicker, with a hole in it.
        var r = Solve(0f, Top, Car(8f, 0f, Top - 2f), Car(14f, -2.4f, Top + 2f), Car(-14f, -2.4f, Top + 2f));
        Assert.AreEqual(-2.4f, r.TargetLateral, 0.6f);
    }

    [Test]
    public void InGroovesItOnlyEverAimsForAGroove()
    {
        // A slower car ahead in our groove: the field moves us, but to the groove beside it, never between two.
        var lanes = new List<float> { -3.9f, -1.3f, 1.3f, 3.9f };
        var r = new LateralField().Solve(-1.3f, Top, Top, Lo, Hi, -1.3f, float.NaN,
                                         new List<FieldCar> { Car(12f, -1.3f, Top - 7f) }, lanes);
        Assert.IsTrue(lanes.Contains(r.TargetLateral), $"aimed for {r.TargetLateral}, not a groove");
        Assert.AreNotEqual(-1.3f, r.TargetLateral);
    }

    [Test]
    public void DoesNotDropIntoAGrooveRightOnSomebodysNose()
    {
        // The groove beside is open ahead, but a car sits a metre off our rear bumper in it, at our speed.
        var lanes = new List<float> { -1.3f, 1.3f };
        var r = new LateralField().Solve(-1.3f, Top, Top, Lo, Hi, -1.3f, float.NaN,
                                         new List<FieldCar> { Car(12f, -1.3f, Top - 7f), Car(-5.8f, 1.3f, Top) }, lanes);
        Assert.AreEqual(-1.3f, r.TargetLateral);
    }

    [Test]
    public void ACarCommittedToOurLaneCapsUsBeforeItArrives()
    {
        // 9 m ahead, still a lane over, but committed to moving into ours and slower: follow it from now.
        var f = new LateralField();
        var cut = Car(9f, 2.6f, Top - 5f);
        cut.Heading = 0f;
        var r = f.Solve(0f, Top, Top, Lo, Hi, 0f, float.NaN, new List<FieldCar> { cut });
        Assert.LessOrEqual(r.SpeedCap, f.FollowSpeed(Top - 5f, 9f - f.S.FollowGap) + 0.01f);
    }

    [Test]
    public void ACarDiagonallyAheadInOurCorridorStillCapsUs()
    {
        // 4.5 m ahead and 1.2 m across - a wall to the side, but its rear bumper is in front of our nose. The pack
        // sim's hard contacts were exactly this: a towed car with no cap running into the quarter of a slower one.
        var f = new LateralField();
        var r = f.Solve(0f, Top + 4f, Top, Lo, Hi, 0f, float.NaN, new List<FieldCar> { Car(4.5f, 1.2f, Top - 4f) });
        Assert.LessOrEqual(r.SpeedCap, Top - 4f + 0.01f);
    }

    [Test]
    public void SpeedCapIsTheFollowCapOnTheWayAcross()
    {
        // Cutting across behind a slow car to reach the open side: the cap holds us to what we can shed behind it.
        var f = new LateralField();
        var r = f.Solve(0f, Top, Top, Lo, Hi, 0f, float.NaN, new List<FieldCar> { Car(8f, 0f, Top - 10f) });
        Assert.LessOrEqual(r.SpeedCap, f.FollowSpeed(Top - 10f, 8f - f.S.FollowGap) + 0.01f);
    }
}
