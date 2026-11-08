using Draftmaster.Sim;
using NUnit.Framework;

// The NR2003 rating model AIDriverBinding rolls each AI driver's event ratings from: strength blends raw speed
// with the track-type aptitude, and is rolled inside a band whose width is the driver's consistency.
public class AIRatingsTests
{
    const float Tol = 1e-4f;
    const int Max = 20;

    [Test]
    public void ASpecialistOutrunsAnEquallyQuickDriverAtTheirKindOfTrack()
    {
        // Same raw speed; one is a road-course ace, the other has never turned right.
        float ace = AIRatings.StrengthMean(qualifying: 14, trackAptitude: 20, Max);
        float novice = AIRatings.StrengthMean(qualifying: 14, trackAptitude: 4, Max);
        Assert.Greater(ace, novice + 0.3f);
    }

    [Test]
    public void AptitudeAndRawSpeedWeighEqually()
    {
        Assert.AreEqual(0.5f, AIRatings.StrengthMean(20, 0, Max), Tol);
        Assert.AreEqual(0.5f, AIRatings.StrengthMean(0, 20, Max), Tol);
        Assert.AreEqual(1f, AIRatings.StrengthMean(20, 20, Max), Tol);
    }

    [Test]
    public void ConsistencyIsTheWidthOfTheBand()
    {
        Assert.AreEqual(AIRatings.MaxStrengthDeviation, AIRatings.StrengthDeviation(0, Max), Tol);
        Assert.AreEqual(AIRatings.MinStrengthDeviation, AIRatings.StrengthDeviation(Max, Max), Tol);
        Assert.Less(AIRatings.StrengthDeviation(15, Max), AIRatings.StrengthDeviation(5, Max));
    }

    [Test]
    public void TheRollSpansTheBandAndCentresOnTheMean()
    {
        Assert.AreEqual(0.4f, AIRatings.Roll(0.5f, 0.1f, 0f), Tol);
        Assert.AreEqual(0.5f, AIRatings.Roll(0.5f, 0.1f, 0.5f), Tol);
        Assert.AreEqual(0.6f, AIRatings.Roll(0.5f, 0.1f, 1f), Tol);
    }

    [Test]
    public void TheRollNeverLeavesTheZeroToOneScale()
    {
        Assert.AreEqual(1f, AIRatings.Roll(0.98f, 0.1f, 1f), Tol);
        Assert.AreEqual(0f, AIRatings.Roll(0.02f, 0.1f, 0f), Tol);
    }

    [Test]
    public void AMetronomeRunsToTheirRatingAJourneymanHasGoodAndBadDays()
    {
        var steadyBad = AIRatings.ForEvent(12, 12, Max, 10, Max, 0f, 0.5f);
        var steadyGood = AIRatings.ForEvent(12, 12, Max, 10, Max, 1f, 0.5f);
        var wildBad = AIRatings.ForEvent(12, 12, 0, 10, Max, 0f, 0.5f);
        var wildGood = AIRatings.ForEvent(12, 12, 0, 10, Max, 1f, 0.5f);

        float steadySpread = steadyGood.strength01 - steadyBad.strength01;
        float wildSpread = wildGood.strength01 - wildBad.strength01;
        Assert.AreEqual(2f * AIRatings.MinStrengthDeviation, steadySpread, Tol);
        Assert.AreEqual(2f * AIRatings.MaxStrengthDeviation, wildSpread, Tol);
    }

    [Test]
    public void AggressionWobblesLessThanPaceAndConsistencyIsNotRolled()
    {
        var low = AIRatings.ForEvent(10, 10, 0, 10, Max, 0.5f, 0f);
        var high = AIRatings.ForEvent(10, 10, 0, 10, Max, 0.5f, 1f);
        Assert.AreEqual(2f * AIRatings.AggressionDeviation, high.aggression01 - low.aggression01, Tol);
        Assert.Less(AIRatings.AggressionDeviation, AIRatings.MaxStrengthDeviation);
        Assert.AreEqual(0f, low.consistency01, Tol);
        Assert.AreEqual(low.consistency01, high.consistency01, Tol);
    }
    [Test]
    public void TyreManagementStretchesASetEitherWay()
    {
        Assert.AreEqual(1.15f, AIRatings.TyreWearScale(0f), Tol);
        Assert.AreEqual(1f, AIRatings.TyreWearScale(0.5f), Tol);
        Assert.AreEqual(0.85f, AIRatings.TyreWearScale(1f), Tol);
    }

    [Test]
    public void AnAwareDriverWantsMoreRoomBeforeTakingALane()
    {
        Assert.Greater(AIRatings.AwarenessMargin(1f), AIRatings.AwarenessMargin(0f));
        Assert.AreEqual(1f, AIRatings.AwarenessMargin(0.5f), Tol);
    }

    [Test]
    public void AdaptabilityOnlyCostsPaceEarlyInTheSession()
    {
        Assert.AreEqual(1f - AIRatings.AdaptPenalty, AIRatings.AdaptationPace(0f, 0f), Tol);
        Assert.AreEqual(1f, AIRatings.AdaptationPace(0f, AIRatings.AdaptSeconds), Tol);
        Assert.AreEqual(1f, AIRatings.AdaptationPace(1f, 0f), Tol);
        Assert.Less(AIRatings.AdaptationPace(0.2f, 60f), AIRatings.AdaptationPace(0.8f, 60f));
    }

    [Test]
    public void ATrackProneToCautionsMakesMoreMistakes()
    {
        Assert.AreEqual(1f, AIRatings.MistakeScale(0.5f), Tol);
        Assert.Greater(AIRatings.MistakeScale(0.9f), AIRatings.MistakeScale(0.35f));
    }

    [Test]
    public void TheAISlidersStepWithinTheirRangeAndCycleRound()
    {
        Assert.AreEqual(AIDifficultyRules.StrengthMax, AIDifficultyRules.StepStrength(AIDifficultyRules.StrengthMax, +1));
        Assert.AreEqual(AIDifficultyRules.StrengthMin, AIDifficultyRules.CycleStrength(AIDifficultyRules.StrengthMax));
        Assert.AreEqual(AIDifficultyRules.AggressionMin, AIDifficultyRules.StepAggression(AIDifficultyRules.AggressionMin, -1));
        Assert.AreEqual(1f, AIDifficultyRules.StrengthScale(AIDifficultyRules.StrengthDefault), Tol);
        Assert.AreEqual(0.6f, AIDifficultyRules.Aggression(0.6f, AIDifficultyRules.AggressionDefault), Tol);
        Assert.AreEqual(1f, AIDifficultyRules.Aggression(0.8f, AIDifficultyRules.AggressionMax), Tol);
    }
}
