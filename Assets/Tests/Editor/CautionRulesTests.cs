using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// Full-course caution rules (FullCourseCaution / SafetyCar.DeployForCaution): when one comes out, when pit road is open,
// and how far the safety car runs from the pickup before it pits.
public class CautionRulesTests
{
    const float Lap = 4000f;

    // The caution comes out: cars drive on at most of their racing pace and come down onto the train gently, off the
    // speed of whatever is really in front of them.
    [Test]
    public void ApproachCap_AFarCarRunsOn_ACloseCarSettlesAtTheSpeedOfTheOneInFront()
    {
        // Right on station behind a car doing 30 m/s: its speed.
        Assert.AreEqual(30f, CautionRules.ApproachCapMps(20f, 20f, 30f, 2f), 1e-4f);
        Assert.AreEqual(30f, CautionRules.ApproachCapMps(5f, 20f, 30f, 2f), 1e-4f, "inside the station: no faster than it");
        // 400 m of room behind a car at 27 m/s (60 mph), shedding 2 m/s^2: sqrt(27^2 + 1600) = 48 m/s.
        Assert.AreEqual(Mathf.Sqrt(27f * 27f + 1600f), CautionRules.ApproachCapMps(420f, 20f, 27f, 2f), 1e-3f);
        // A car in front still running 70 m/s 15 m up the road holds a car behind at 70 m/s, not at the train's pace.
        Assert.GreaterOrEqual(CautionRules.ApproachCapMps(15f, 20f, 70f, 2f), 70f);
    }

    [Test]
    public void EaseDown_RisesAtOnce_FallsNoFasterThanTheEase()
    {
        Assert.AreEqual(40f, CautionRules.EaseDown(float.PositiveInfinity, 40f, 2f, 0.02f), 1e-4f, "no cap before: straight to it");
        Assert.AreEqual(60f, CautionRules.EaseDown(50f, 60f, 2f, 0.02f), 1e-4f, "rises at once");
        Assert.AreEqual(80f - 0.04f, CautionRules.EaseDown(80f, 40f, 2f, 0.02f), 1e-4f, "falls 2 m/s^2");
        Assert.AreEqual(40f, CautionRules.EaseDown(40.01f, 40f, 2f, 0.02f), 1e-4f, "never past the target");
    }

    [Test]
    public void ApproachPace_IsEightyToNinetyPercent()
    {
        Assert.That(CautionRules.ApproachPace, Is.InRange(0.8f, 0.9f));
    }

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

    // The plan from the pickup: round to pit road once with pit road open, then in the next time by.
    [Test]
    public void ThePitsOpenPassIsTheNextPitEntryAtLeastHalfALapOn()
    {
        Assert.That(CautionRules.PitsOpenPass(Lap, 3000f), Is.EqualTo(3000f).Within(0.01f));
        Assert.That(CautionRules.PitsOpenPass(Lap, 2000f), Is.EqualTo(2000f).Within(0.01f));
        // Picked up just short of pit road: the field has not fallen in yet - the pass after.
        Assert.That(CautionRules.PitsOpenPass(Lap, 300f), Is.EqualTo(300f + Lap).Within(0.01f));
        Assert.That(CautionRules.PitsOpenPass(Lap, -1f), Is.LessThan(0f));
    }

    [Test]
    public void TheSafetyCarDrivesByOnThePitsOpenPassAndPitsTheTimeAfter()
    {
        const float window = 25f;
        foreach (float toEntry in new[] { 0f, 300f, 1999f, 2000f, 3000f, 3990f })
        {
            float pass = CautionRules.PitsOpenPass(Lap, toEntry);
            float floor = CautionRules.PitInFloor(Lap, toEntry);
            // Not on the pits-open pass, even at the far end of the commit window...
            Assert.That(pass + window, Is.LessThan(floor), $"to entry {toEntry}");
            // ...but on the next arrival, from the start of the window.
            Assert.That(pass + Lap - window, Is.GreaterThanOrEqualTo(floor), $"to entry {toEntry}");
            // Never more than two and a half laps behind it from the pickup to the pit-in.
            Assert.That(pass + Lap, Is.LessThan(2.5f * Lap), $"to entry {toEntry}");
        }
    }

    [Test]
    public void PitRoadShutsBeforeTheGreenFlagLapComesBackToIt()
    {
        foreach (float toEntry in new[] { 300f, 2000f, 3000f })
        {
            float pass = CautionRules.PitsOpenPass(Lap, toEntry);
            float until = CautionRules.PitsOpenUntil(Lap, toEntry);
            Assert.That(until, Is.GreaterThan(pass), "open as the safety car goes by pit road");
            Assert.That(until, Is.LessThan(pass + Lap - 500f), "shut well before it comes round to it again");
        }
        Assert.AreEqual(float.MaxValue, CautionRules.PitsOpenUntil(Lap, -1f), "no pit entry known: never shut");
        Assert.Less(CautionRules.PitInFloor(Lap, -1f), 0f);
    }

    // ---- Overtime (RaceDirector): a caution out with under two laps left extends the race for two laps of green.

    [Test]
    public void ACautionWithUnderTwoLapsLeftGoesToOvertime()
    {
        Assert.IsTrue(OvertimeRules.Triggers(scheduledLaps: 10, leaderLaps: 8.4f, overtimeRun: false));
        Assert.IsTrue(OvertimeRules.Triggers(10, 9.9f, false));
        // Leader already at the scheduled distance under the caution.
        Assert.IsTrue(OvertimeRules.Triggers(10, 10.3f, false));
    }

    [Test]
    public void ACautionWithTwoOrMoreLapsLeftIsNoOvertime()
    {
        Assert.IsFalse(OvertimeRules.Triggers(10, 7.5f, false));
        Assert.IsFalse(OvertimeRules.Triggers(10, 8f, false));
    }

    [Test]
    public void OvertimeOnlyHappensOnce()
    {
        // A caution after the overtime green finishes the race under caution.
        Assert.IsFalse(OvertimeRules.Triggers(10, 11.2f, overtimeRun: true));
        Assert.IsFalse(OvertimeRules.Triggers(10, 8.4f, overtimeRun: true));
    }

    [Test]
    public void AnUnknownRaceDistanceNeverGoesToOvertime()
    {
        Assert.IsFalse(OvertimeRules.Triggers(0, 0.5f, false));
    }

    [Test]
    public void TheGreenAtTheLineGivesTwoLapsToTheChequer()
    {
        // Leader just short of the line as the green waves: that crossing starts the two laps.
        Assert.AreEqual(12, OvertimeRules.Distance(10, 9.98f));
        // Leader just over it — the same crossing, not counted again.
        Assert.AreEqual(12, OvertimeRules.Distance(10, 10.02f));
    }

    [Test]
    public void AGreenWavedMidLapCountsFromTheNextCrossing()
    {
        // At least two full laps of racing, never fewer.
        Assert.AreEqual(13, OvertimeRules.Distance(10, 10.4f));
        Assert.That(OvertimeRules.Distance(10, 10.4f) - 10.4f, Is.GreaterThanOrEqualTo(2f));
    }

    [Test]
    public void UnderAnOvertimeCautionNobodyTakesTheChequer()
    {
        // However far the leader runs behind the safety car, the distance stays at least two laps past the laps it
        // has completed, so the finish check (completed laps >= distance) can never fire.
        for (float led = 8.1f; led < 15f; led += 0.01f)
        {
            int completed = (int)System.Math.Floor(led);
            Assert.That(OvertimeRules.Distance(10, led), Is.GreaterThanOrEqualTo(completed + 2), $"leader at {led}");
        }
    }

    [Test]
    public void OvertimeNeverShortensTheRace()
    {
        Assert.AreEqual(10, OvertimeRules.Distance(10, 6.5f));
    }

    [Test]
    public void TheSafetyCarComesOutFarEnoughForTheLeaderToSlowToItsPace()
    {
        // From 190 mph to a 60 mph safety car at 6 m/s^2 takes ~540 m; a fixed 80 m had the leader drive past it.
        float gap = CautionRules.PickupGap(190f / 2.237f, 60f / 2.237f, 6f, 80f);
        Assert.Greater(gap, 500f);
        // The leader can brake to the safety car's pace inside that gap.
        Assert.LessOrEqual((190f / 2.237f) * (190f / 2.237f) - (60f / 2.237f) * (60f / 2.237f), 2f * 6f * gap);
    }

    [Test]
    public void ALeaderAlreadyAtTheSafetyCarsPaceStillGetsTheMinimumGap()
    {
        Assert.AreEqual(80f, CautionRules.PickupGap(60f / 2.237f, 60f / 2.237f, 6f, 80f), 0.01f);
        Assert.AreEqual(80f, CautionRules.PickupGap(0f, 60f / 2.237f, 6f, 80f), 0.01f);
    }
}
