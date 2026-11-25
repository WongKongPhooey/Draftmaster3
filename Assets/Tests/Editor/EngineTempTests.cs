using Draftmaster.Sim;
using NUnit.Framework;

// Engine temperature (EngineTemp): pushing overheats the engine in about two laps of Daytona, three quarters of a car
// length back holds it, more room or an empty lane cools it, and slowing down (a caution) cools everything.
public class EngineTempTests
{
    const float Mph = 1f / 2.237f;
    const float PlateMps = 190f * Mph;                 // Daytona's plate speed
    const float DaytonaLapSeconds = 2.5f * 3600f / 190f;  // 2.5 miles at pack speed: ~47 s
    const float Dt = 0.02f;

    // Centre-to-centre gap for a bumper-to-bumper gap of this many car lengths.
    static float Centre(float carLengths) => EngineTemp.CarLength * (1f + carLengths);

    static float SecondsToReach(float fromC, float toC, float cover, float speedShare)
    {
        float t = fromC, s = 0f;
        while (s < 1000f)
        {
            if (fromC < toC ? t >= toC : t <= toC) return s;
            t = EngineTemp.Step(t, cover, speedShare, Dt);
            s += Dt;
        }
        return float.PositiveInfinity;
    }

    [Test]
    public void PushingOverheatsTheEngineInAboutTwoLaps()
    {
        float share = EngineTemp.SpeedShare(PlateMps, PlateMps);
        Assert.AreEqual(1f, share, 1e-5f);
        // On the bumper (touching) and a half metre off it: both bump drafting.
        foreach (float bumper in new[] { 0f, 0.5f / EngineTemp.CarLength })
        {
            float laps = SecondsToReach(EngineTemp.NormalC, EngineTemp.OverheatC,
                                        EngineTemp.Cover(Centre(bumper), 0f), share) / DaytonaLapSeconds;
            Assert.That(laps, Is.InRange(1.5f, 2.6f), $"bumper gap {bumper:0.00} car lengths: overheated in {laps:0.00} laps");
        }
    }

    [Test]
    public void ThreeQuartersOfACarLengthHoldsTheTemperature()
    {
        float cover = EngineTemp.Cover(Centre(0.75f), 0f);
        Assert.AreEqual(EngineTemp.HoldCover, cover, 1e-4f);
        Assert.AreEqual(0f, EngineTemp.Rate(cover, 1f), 1e-4f);
        // Held where it is, hot or not.
        Assert.AreEqual(120f, EngineTemp.Step(120f, cover, 1f, 10f), 1e-3f);
        // A touch closer heats, a touch further cools.
        Assert.Greater(EngineTemp.Rate(EngineTemp.Cover(Centre(0.6f), 0f), 1f), 0f);
        Assert.Less(EngineTemp.Rate(EngineTemp.Cover(Centre(0.9f), 0f), 1f), 0f);
    }

    [Test]
    public void ACarLengthBackCools_MoreCleanAirCoolsQuicker_AnEmptyLaneQuickest()
    {
        float oneLength = EngineTemp.Rate(EngineTemp.Cover(Centre(1f), 0f), 1f);
        float twoLengths = EngineTemp.Rate(EngineTemp.Cover(Centre(2f), 0f), 1f);
        float emptyLane = EngineTemp.Rate(0f, 1f);
        Assert.Less(oneLength, 0f, "a car length back should cool");
        Assert.Less(twoLengths, oneLength, "more room should cool quicker");
        Assert.Less(emptyLane, twoLengths, "an empty lane should cool quickest");
        Assert.AreEqual(-EngineTemp.CoolRate, emptyLane, 1e-5f);

        // A car a lane over leaves our grille in clean air; nobody ahead within clean-air range either.
        Assert.AreEqual(0f, EngineTemp.Cover(Centre(0.1f), 3f), 1e-5f);
        Assert.AreEqual(0f, EngineTemp.Cover(Centre(EngineTemp.CleanCarLengths + 0.5f), 0f), 1e-5f);
        // A car behind us covers nothing.
        Assert.AreEqual(0f, EngineTemp.Cover(-Centre(0.1f), 0f), 1e-5f);

        // Pulled out into an empty lane, an overheating engine is back to normal running inside about a lap.
        float s = SecondsToReach(EngineTemp.OverheatC, EngineTemp.CooledC, 0f, 1f);
        Assert.Less(s, DaytonaLapSeconds, $"took {s:0} s to cool in clean air");
    }

    [Test]
    public void UnderCautionEverythingCools_EvenNoseToTail()
    {
        float share = EngineTemp.SpeedShare(55f * Mph, PlateMps);
        Assert.Less(share, 0.1f);
        float noseToTail = EngineTemp.Rate(EngineTemp.Cover(Centre(0f), 0f), share);
        Assert.Less(noseToTail, -0.6f * EngineTemp.CoolRate, $"nose to tail at caution speed: {noseToTail:0.000} °C/s");
        // Heat goes with speed: the same push at a short track's speed heats far slower than at pack speed.
        float slow = EngineTemp.Rate(1f, EngineTemp.SpeedShare(120f * Mph, PlateMps));
        Assert.Less(slow, EngineTemp.Rate(1f, 1f) * 0.5f);
    }

    [Test]
    public void TemperatureStaysBetweenNormalAndMax()
    {
        Assert.AreEqual(EngineTemp.NormalC, EngineTemp.Step(EngineTemp.NormalC, 0f, 1f, 60f), 1e-4f);
        Assert.AreEqual(EngineTemp.MaxC, EngineTemp.Step(EngineTemp.MaxC, 1f, 1f, 600f), 1e-4f);
        Assert.AreEqual(0f, EngineTemp.SpeedShare(0f, PlateMps));
        Assert.AreEqual(1f, EngineTemp.SpeedShare(2f * PlateMps, PlateMps));
    }

    [Test]
    public void AnOverheatedEngineLosesPower()
    {
        Assert.AreEqual(1f, EngineTemp.PowerFactor(EngineTemp.NormalC), 1e-6f);
        Assert.AreEqual(1f, EngineTemp.PowerFactor(EngineTemp.HotC), 1e-6f);
        Assert.AreEqual(1f, EngineTemp.TopSpeedFactor(EngineTemp.OverheatC), 1e-6f);
        Assert.AreEqual(1f - EngineTemp.MaxPowerLoss, EngineTemp.PowerFactor(EngineTemp.MaxC), 1e-6f);
        Assert.AreEqual(1f - EngineTemp.MaxTopSpeedLoss, EngineTemp.TopSpeedFactor(EngineTemp.MaxC), 1e-6f);
        Assert.Less(EngineTemp.PowerFactor(130f), 1f);
        Assert.Greater(EngineTemp.PowerFactor(130f), EngineTemp.PowerFactor(EngineTemp.MaxC));
    }

    // ------------------------------------------------------------------ gauge, warm-up and blowing up

    [Test]
    public void Gauge_ThreeBarsIdling_FullAtMax()
    {
        Assert.AreEqual(3, (int)System.Math.Round(EngineTemp.Gauge01(EngineTemp.NormalC) * 10f));
        Assert.AreEqual(1f, EngineTemp.Gauge01(EngineTemp.MaxC), 1e-6f);
        Assert.Less(EngineTemp.Gauge01(EngineTemp.HotC), EngineTemp.Gauge01(EngineTemp.OverheatC));
        Assert.Less(EngineTemp.Gauge01(EngineTemp.OverheatC), 1f);
    }

    [Test]
    public void IdlingInTheBox_StaysAtNormal()
    {
        float t = EngineTemp.NormalC;
        for (int i = 0; i < 6000; i++) t = EngineTemp.Step(t, 0f, 0f, Dt, GrilleTape.Default, load: 0f);
        Assert.AreEqual(EngineTemp.NormalC, t, 1e-4f);
    }

    [Test]
    public void FlatOutInCleanAir_WarmsUpButStaysOutOfTheAmber()
    {
        float t = EngineTemp.NormalC, after30 = 0f;
        for (int i = 0; i < 6000; i++)   // two minutes at 50 Hz
        {
            t = EngineTemp.Step(t, 0f, 1f, Dt, GrilleTape.Default, load: 1f);
            if (i == 1499) after30 = t;
        }
        Assert.Greater(after30, EngineTemp.NormalC + 6f, "climbs noticeably in the first half minute");
        Assert.AreEqual(EngineTemp.Settle(1f), t, 0.5f);
        Assert.Less(t, EngineTemp.HotC);
    }

    [Test]
    public void WarmEngine_CoolsBackToWhatItsWorkHoldsWhenLoadDrops()
    {
        float t = EngineTemp.Settle(1f);
        for (int i = 0; i < 6000; i++) t = EngineTemp.Step(t, 0f, 0.2f, Dt, GrilleTape.Default, load: 0f);
        Assert.AreEqual(EngineTemp.NormalC, t, 0.5f);
    }

    [Test]
    public void PinnedAtMax_BlowsAfterTenSecondsOfFullThrottle()
    {
        float strain = 0f, s = 0f;
        while (!EngineTemp.Blown(strain) && s < 60f) { strain = EngineTemp.StepStrain(strain, EngineTemp.MaxC, 1f, Dt); s += Dt; }
        Assert.AreEqual(EngineTemp.BlowSeconds, s, 0.05f);

        // Half throttle takes twice as long; off the throttle it never goes.
        strain = 0f;
        for (int i = 0; i < (int)(EngineTemp.BlowSeconds / Dt); i++) strain = EngineTemp.StepStrain(strain, EngineTemp.MaxC, 0.5f, Dt);
        Assert.IsFalse(EngineTemp.Blown(strain));
        Assert.AreEqual(0f, EngineTemp.StepStrain(0f, EngineTemp.MaxC, 0f, 1f));
    }

    [Test]
    public void Strain_BleedsAwayOnceCooled_HoldsWhileStillHot()
    {
        Assert.AreEqual(5f, EngineTemp.StepStrain(5f, EngineTemp.OverheatC + 1f, 1f, 1f), 1e-6f);
        Assert.AreEqual(5f - EngineTemp.StrainRecoverRate, EngineTemp.StepStrain(5f, EngineTemp.HotC, 1f, 1f), 1e-6f);
    }

    // ------------------------------------------------------------------ grille tape (the setup sheet's Tape slider)

    static float SecondsToReach(float fromC, float toC, float cover, float speedShare, float tape)
    {
        float t = fromC, s = 0f;
        while (s < 1000f)
        {
            if (t >= toC) return s;
            t = EngineTemp.Step(t, cover, speedShare, Dt, tape);
            s += Dt;
        }
        return float.PositiveInfinity;
    }

    [Test]
    public void HalfTapedIsTheStockCar()
    {
        Assert.AreEqual(0.5f, GrilleTape.Default, 1e-6f);
        foreach (float cover in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
            foreach (float share in new[] { 0.3f, 1f })
                Assert.AreEqual(EngineTemp.Rate(cover, share), EngineTemp.Rate(cover, share, GrilleTape.Default), 1e-6f);
        Assert.AreEqual(1f, GrilleTape.TopSpeedFactor(GrilleTape.Default), 1e-6f);
        Assert.AreEqual("50%", GrilleTape.Label(GrilleTape.Default));
    }

    [Test]
    public void NoTapeNeverOverheats_ButLosesStraightLineSpeed()
    {
        foreach (float cover in new[] { 0f, 0.5f, 0.8f, 1f })
            Assert.LessOrEqual(EngineTemp.Rate(cover, 1f, 0f), 0f, $"cover {cover}: an untaped engine heated");
        // Bumper to bumper at pack speed for the whole 1000 s window and it never gets off normal running.
        Assert.IsTrue(float.IsPositiveInfinity(SecondsToReach(EngineTemp.NormalC, EngineTemp.NormalC + 0.1f, 1f, 1f, 0f)));
        Assert.Less(GrilleTape.TopSpeedFactor(0f), 1f);
        Assert.AreEqual(1f - GrilleTape.NoTapeTopSpeedLoss, GrilleTape.TopSpeedFactor(0f), 1e-6f);
    }

    [Test]
    public void FullTapeOverheatsVeryQuicklyOutOfCleanAir()
    {
        float push = EngineTemp.Cover(Centre(0f), 0f);
        float stock = SecondsToReach(EngineTemp.NormalC, EngineTemp.OverheatC, push, 1f, GrilleTape.Default);
        float taped = SecondsToReach(EngineTemp.NormalC, EngineTemp.OverheatC, push, 1f, 1f);
        Assert.Less(taped, stock / 3f, $"fully taped overheated in {taped:0} s against the stock {stock:0} s");
        Assert.Less(taped / DaytonaLapSeconds, 0.6f, $"fully taped overheated in {taped / DaytonaLapSeconds:0.00} laps");

        // Two car lengths back the stock car cools; fully taped it is still heating - anything short of clean air.
        float twoBack = EngineTemp.Cover(Centre(2f), 0f);
        Assert.Less(EngineTemp.Rate(twoBack, 1f), 0f);
        Assert.Greater(EngineTemp.Rate(twoBack, 1f, 1f), 0f);

        // In clean air it still cools as well as any.
        Assert.AreEqual(-EngineTemp.CoolRate, EngineTemp.Rate(0f, 1f, 1f), 1e-5f);
    }

    [Test]
    public void MoreTapeIsMoreSpeed_QualifyingTapeIsWorthItAlone()
    {
        float last = 0f;
        for (int i = 0; i <= 20; i++)
        {
            float f = GrilleTape.TopSpeedFactor(i / 20f);
            Assert.Greater(f, last, $"tape {i * 5}%: no faster than {(i - 1) * 5}%");
            last = f;
        }
        Assert.Greater(GrilleTape.TopSpeedFactor(GrilleTape.Qualifying), 1f);
        // A qualifying run is alone on the track: in clean air the taped engine cools, never heats.
        Assert.Less(EngineTemp.Rate(0f, 1f, GrilleTape.Qualifying), 0f);
        // And more tape heats quicker in a wake.
        float push = EngineTemp.Cover(Centre(0f), 0f);
        Assert.Greater(EngineTemp.Rate(push, 1f, GrilleTape.Qualifying), EngineTemp.Rate(push, 1f));
    }
}
