using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The racecraft rules AIRacingBehaviour reads every FixedUpdate: how a driver's aggression and following
// margin change across a race, how likely they are to make a mistake under pressure on worn tyres, and
// when a lapped car gets out of the leaders' way.
//
// Racing itself can only really be judged in Play Mode, which is not always available (the editor pauses
// when it is unfocused). RaceCraft is deliberately free of MonoBehaviour state so the decisions underneath
// the racing can be pinned down here instead.
public class RaceCraftTests
{
    const float Tol = 1e-4f;

    // ---------------------------------------------------------------- race phase

    [Test]
    public void AnUnknownRaceDistanceRunsTheNeutralMidRaceEnvelope()
    {
        // Practice, qualifying and multiplayer have no race distance. They must not read as an eternal
        // opening lap — that would leave the whole field permanently half a second too cautious.
        Assert.AreEqual(RaceCraft.NeutralProgress,
                        RaceCraft.NormaliseProgress(RaceCraft.UnknownProgress), Tol);
        Assert.AreEqual(1f, RaceCraft.PhaseFollowMargin(RaceCraft.NormaliseProgress(-1f)), Tol);
    }

    [Test]
    public void ProgressIsClampedIntoTheLap()
    {
        Assert.AreEqual(0f, RaceCraft.NormaliseProgress(0f), Tol);
        Assert.AreEqual(1f, RaceCraft.NormaliseProgress(4f), Tol);
    }

    [Test]
    public void DriversSettleInAtTheStartAndThrowEverythingAtItAtTheEnd()
    {
        const float baseAggression = 0.5f;

        float green = RaceCraft.PhaseAggression(baseAggression, 0f);
        float middle = RaceCraft.PhaseAggression(baseAggression, 0.5f);
        float flag = RaceCraft.PhaseAggression(baseAggression, 1f);

        Assert.Less(green, middle, "the opening laps should be more cautious than the middle stint");
        Assert.Greater(flag, middle, "the closing laps should be more aggressive than the middle stint");
        Assert.AreEqual(baseAggression, middle, Tol, "the middle of the race is the driver's own number");
    }

    [Test]
    public void TheFollowingMarginMirrorsTheAggressionEnvelope()
    {
        // Cautious early has to mean a longer safety margin, not just fewer passes — otherwise the field
        // still runs nose-to-tail on lap one and still piles into the first slow car.
        Assert.Greater(RaceCraft.PhaseFollowMargin(0f), 1f, "wider gaps at the green");
        Assert.AreEqual(1f, RaceCraft.PhaseFollowMargin(0.5f), Tol);
        Assert.Less(RaceCraft.PhaseFollowMargin(1f), 1f, "tighter gaps at the flag");
    }

    [Test]
    public void ThePhaseRampIsContinuousAndFlatThroughTheMiddleStint()
    {
        // A step change in how hard the field races would be visible as the whole pack suddenly closing up.
        float prev = RaceCraft.PhaseAggression(0.5f, 0f);
        for (int i = 1; i <= 100; i++)
        {
            float here = RaceCraft.PhaseAggression(0.5f, i / 100f);
            Assert.Less(Mathf.Abs(here - prev), 0.05f, $"aggression jumped at progress {i / 100f:0.00}");
            prev = here;
        }

        // Everything between the settling-in phase and the charge is the driver's own number.
        Assert.AreEqual(RaceCraft.PhaseAggression(0.5f, RaceCraft.SettleFraction + 0.01f),
                        RaceCraft.PhaseAggression(0.5f, 1f - RaceCraft.ChargeFraction - 0.01f), Tol);
    }

    [Test]
    public void PhaseAggressionStaysAValidZeroToOne()
    {
        // ClosingAggressionScale is >1, so a driver already near the top of the scale must not be pushed
        // past it — everything downstream lerps with this value.
        foreach (float a in new[] { 0f, 0.25f, 0.9f, 1f })
            for (int i = 0; i <= 20; i++)
            {
                float v = RaceCraft.PhaseAggression(a, i / 20f);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
    }

    // ---------------------------------------------------------------- pressure and mistakes

    [Test]
    public void PressureRisesAsTheCarBehindCloses()
    {
        Assert.AreEqual(0f, RaceCraft.Pressure01(18f, 18f), Tol, "at the edge of range, nothing");
        Assert.AreEqual(0.5f, RaceCraft.Pressure01(9f, 18f), Tol);
        Assert.AreEqual(1f, RaceCraft.Pressure01(0f, 18f), Tol, "on the bumper");
        Assert.AreEqual(1f, RaceCraft.Pressure01(-3f, 18f), Tol, "alongside counts as full pressure");
        Assert.AreEqual(0f, RaceCraft.Pressure01(40f, 18f), Tol, "out of range");
        Assert.AreEqual(0f, RaceCraft.Pressure01(1f, 0f), Tol, "no range configured, no pressure");
    }

    [Test]
    public void BeingLeanedOnAndWornOutBothMakeAMistakeLikelier()
    {
        const float rate = 0.06f, consistency = 0.8f;

        float clean = RaceCraft.MistakeChancePerSecond(rate, consistency, 0f, 0f);
        float pressured = RaceCraft.MistakeChancePerSecond(rate, consistency, 1f, 0f);
        float worn = RaceCraft.MistakeChancePerSecond(rate, consistency, 0f, 1f);
        float both = RaceCraft.MistakeChancePerSecond(rate, consistency, 1f, 1f);

        Assert.Greater(pressured, clean, "a rival in the mirrors should make an error likelier");
        Assert.Greater(worn, clean, "worn tyres should make an error likelier");
        Assert.Greater(both, pressured);
        Assert.Greater(both, worn);
    }

    [Test]
    public void ConsistencyBuysMostOfTheErrorRateAwayButNeverAllOfIt()
    {
        const float rate = 0.06f;
        Assert.Greater(RaceCraft.MistakeChancePerSecond(rate, 0f, 0f, 0f),
                       RaceCraft.MistakeChancePerSecond(rate, 1f, 0f, 0f));
        Assert.Greater(RaceCraft.MistakeChancePerSecond(rate, 1f, 0f, 0f), 0f,
                       "even a metronome has to crack eventually");
    }

    [Test]
    public void TheWorstCaseErrorRateIsStillAnOccasionalWobbleNotAConstantOne()
    {
        // The multipliers have to be dramatic without being twitchy. The default driver on the worst
        // tyres the model produces (TireState bottoms out near 0.88 grip) with a car on the bumper should
        // be a wobble every ten seconds or so at the very most — not one every second.
        float worst = RaceCraft.MistakeChancePerSecond(0.06f, 0.8f, 1f, 0.12f);
        Assert.Less(worst, 0.1f, $"{1f / worst:0}s between mistakes is too twitchy");
        Assert.Greater(worst, 0.02f, "being hounded on dead tyres should visibly cost something");
    }

    [Test]
    public void AZeroBaseRateSwitchesMistakesOffEntirely()
    {
        Assert.AreEqual(0f, RaceCraft.MistakeChancePerSecond(0f, 0f, 1f, 1f), Tol);
    }

    // ---------------------------------------------------------------- blue flags

    [Test]
    public void OnlyACarFurtherRoundTheRaceGetsLetPast()
    {
        const float range = 45f;
        Assert.IsTrue(RaceCraft.ShouldYield(myLap: 3, lapperLap: 4, gapBehindMetres: 20f, range));
        Assert.IsFalse(RaceCraft.ShouldYield(3, 3, 20f, range), "a car on our lap is racing us");
        Assert.IsFalse(RaceCraft.ShouldYield(4, 3, 20f, range), "we are the one doing the lapping");
        Assert.IsFalse(RaceCraft.ShouldYield(3, 4, 80f, range), "too far back to be waved past yet");
        Assert.IsFalse(RaceCraft.ShouldYield(3, 4, -5f, range), "already alongside or ahead");
        Assert.IsFalse(RaceCraft.ShouldYield(3, 4, 20f, 0f), "blue flags switched off");
    }

    [Test]
    public void YieldingFirmsUpAsTheLapperArrives()
    {
        const float range = 45f;
        Assert.AreEqual(0f, RaceCraft.YieldStrength01(range, range), Tol, "nothing at the edge of the range");
        Assert.AreEqual(1f, RaceCraft.YieldStrength01(0f, range), Tol, "full commitment on the bumper");
        Assert.Greater(RaceCraft.YieldStrength01(10f, range), RaceCraft.YieldStrength01(30f, range));
    }

    [Test]
    public void TheLappedCarStepsAwayFromTheLineTheLapperIsUsing()
    {
        Assert.AreEqual(1f, RaceCraft.YieldDirection(myLateral: 2f, lapperLateral: -1f), Tol,
                        "they are inside us, so we go further out");
        Assert.AreEqual(-1f, RaceCraft.YieldDirection(-2f, 1f), Tol);
    }

    [Test]
    public void TwoLappedCarsOnTheSameLineDoNotBothPickTheMiddle()
    {
        // A dead heat has to break by where each car already sits, or a pair of lapped cars converge on
        // each other trying to get out of the way — the opposite of the point.
        Assert.AreEqual(1f, RaceCraft.YieldDirection(1.5f, 1.5f), Tol);
        Assert.AreEqual(-1f, RaceCraft.YieldDirection(-1.5f, -1.5f), Tol);
    }

    [Test]
    public void YieldingLiftsEnoughToCompleteThePassButNeverParksTheCar()
    {
        const float lift = 0.94f;
        Assert.AreEqual(1f, RaceCraft.YieldSpeedFactor(0f, lift), Tol, "no lift at the edge of the range");
        Assert.AreEqual(lift, RaceCraft.YieldSpeedFactor(1f, lift), Tol);
        Assert.Greater(RaceCraft.YieldSpeedFactor(1f, lift), 0.5f, "a yield is a lift, not a stop");

        // An inspector typo must not be able to park a lapped car in front of the leaders.
        Assert.GreaterOrEqual(RaceCraft.YieldSpeedFactor(1f, 0f), 0.5f);
    }
}
