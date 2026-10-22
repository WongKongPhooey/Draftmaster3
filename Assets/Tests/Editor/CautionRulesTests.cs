using Draftmaster.Sim;
using NUnit.Framework;

// Full-course caution rules (FullCourseCaution / SafetyCar.DeployForCaution): when one comes out, when the safety car
// is held out, and how far it must run before it may pit.
public class CautionRulesTests
{
    const float Lap = 4000f;

    [Test]
    public void ACarStoppedLongEnoughOnAnOvalBringsOutTheCaution()
    {
        Assert.IsTrue(CautionRules.Declare(oval: true, racing: true, raceOver: false, secondsSinceGreen: 60f,
                                           minSecondsAfterGreen: 5f, stoppedSeconds: 2.5f, declareAfterSeconds: 2f));
    }

    [Test]
    public void NoCautionOnARoadCourse()
    {
        Assert.IsFalse(CautionRules.Declare(false, true, false, 60f, 5f, 10f, 2f));
    }

    [Test]
    public void NoCautionOffTheGreen()
    {
        // Pace lap or already under caution.
        Assert.IsFalse(CautionRules.Declare(true, false, false, -1f, 5f, 10f, 2f));
        // The chequer is out.
        Assert.IsFalse(CautionRules.Declare(true, true, true, 60f, 5f, 10f, 2f));
        // Straight after a green.
        Assert.IsFalse(CautionRules.Declare(true, true, false, 3f, 5f, 10f, 2f));
    }

    [Test]
    public void ABriefStopOrNothingStoppedIsNoCaution()
    {
        Assert.IsFalse(CautionRules.Declare(true, true, false, 60f, 5f, 1f, 2f));
        Assert.IsFalse(CautionRules.Declare(true, true, false, 60f, 5f, -1f, 2f));
    }

    [Test]
    public void TheSafetyCarStaysOutUntilTheTrackHasBeenClearLongEnough()
    {
        Assert.IsTrue(CautionRules.HoldOut(clearForSeconds: -1f, clearNeededSeconds: 3f, lapsRun: 1f, maxLaps: 5f));
        Assert.IsTrue(CautionRules.HoldOut(1f, 3f, 1f, 5f));
        Assert.IsFalse(CautionRules.HoldOut(3f, 3f, 1f, 5f));
    }

    [Test]
    public void AStuckCarNeverHoldsTheCautionPastTheMaximum()
    {
        Assert.IsFalse(CautionRules.HoldOut(-1f, 3f, 5f, 5f));
    }

    [Test]
    public void TheGreenComesAfterAtLeastTheMinimumLaps()
    {
        // Pit entry 300 m before the line: the safety car commits within 20 m of the entry, then the leader runs on to
        // the line, so travel at the green is at least floor + 20 + 300 = 2 laps.
        float pitEntry = 3700f, line = 0f, window = 20f;
        float floor = CautionRules.PitFloor(Lap, pitEntry, line, 2f, window);
        Assert.That(floor + window + 300f, Is.GreaterThanOrEqualTo(2f * Lap - 0.01f));
        Assert.That(floor, Is.LessThan(2f * Lap));
    }

    [Test]
    public void ThePitFloorIsNeverUnderHalfALap()
    {
        Assert.That(CautionRules.PitFloor(Lap, 100f, 50f, 0f, 20f), Is.EqualTo(Lap * 0.5f).Within(0.01f));
    }

    [Test]
    public void WithNoPitEntryTheFloorIsTheWholeLaps()
    {
        Assert.That(CautionRules.PitFloor(Lap, -1f, 0f, 2f, 20f), Is.EqualTo(2f * Lap).Within(0.01f));
    }
}
