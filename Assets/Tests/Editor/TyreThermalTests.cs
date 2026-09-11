using NUnit.Framework;
using UnityEngine;
using Draftmaster.Sim;

// EditMode coverage for the tyre temperature maths. A tyre can only really be judged by driving on it, so
// these pin the numbers that used to be wrong: how LONG the tyre takes to move, that slowing it down didn't
// quietly move the temperatures it settles at, and that steering warms the fronts faster than the rears.
public class TyreThermalTests
{
    // These mirror TireModel's shipped defaults, so the calibration tests below say something about the tyres
    // you actually drive on rather than about numbers invented for the test.
    const float Ambient = 25f;
    const float OptimalC = 90f;
    const float HeatRate = 60f;
    const float CoolRate = 0.225f;
    const float AirCool = 0.015f;
    const float Inertia = 8f;
    const float ScrubHeatFront = 0.35f;
    const float ScrubFullLockDeg = 5f;
    const float ScrubHeatRearShare = 0.3f;

    // A racing speed — 60 m/s is about 134 mph, which is roughly what a lap averages.
    const float RaceMps = 60f;
    const float Dt = 1f / 50f; // one FixedUpdate

    static float Step(float temp, float work, float speedMps = RaceMps, float inertia = Inertia)
        => TyreThermal.Step(temp, Ambient, work, speedMps, HeatRate, CoolRate, AirCool, inertia, Dt);

    static float Equilibrium(float work, float speedMps = RaceMps, float inertia = Inertia)
        => TyreThermal.EquilibriumC(Ambient, work, speedMps, HeatRate, CoolRate, AirCool, inertia);

    // Run the tyre forward from `from` at a fixed work level and report the temperature after `seconds`.
    static float Soak(float from, float work, float seconds, float speedMps = RaceMps, float inertia = Inertia)
    {
        float t = from;
        int steps = Mathf.RoundToInt(seconds / Dt);
        for (int i = 0; i < steps; i++) t = Step(t, work, speedMps, inertia);
        return t;
    }

    // ---- Thermal mass: the tyre has to be slow ----

    [Test]
    public void TyresTakeMostOfALapToComeIn()
    {
        // Work held at the level that settles the tyre in its window, so this measures time, not temperature.
        float work = WorkForEquilibrium(OptimalC);

        float afterOneCorner = Soak(Ambient, work, 4f);
        float afterHalfALap = Soak(Ambient, work, 20f);
        float afterALap = Soak(Ambient, work, 45f);

        Assert.Less(afterOneCorner, Ambient + 0.25f * (OptimalC - Ambient),
            "four seconds of load should barely move the tyre — this is the snap the model used to have");
        Assert.Greater(afterHalfALap, Ambient + 0.4f * (OptimalC - Ambient), "half a lap should be well on the way");
        Assert.Less(afterHalfALap, OptimalC, "half a lap should not have got there yet");
        Assert.Greater(afterALap, Ambient + 0.8f * (OptimalC - Ambient), "a lap of load should have the tyre nearly in");
    }

    [Test]
    public void TyresCoolAsSlowlyAsTheyHeat()
    {
        float hot = 120f;
        float afterFourSeconds = Soak(hot, 0f, 4f);
        float afterHalfAMinute = Soak(hot, 0f, 30f);

        Assert.Greater(afterFourSeconds, hot - 0.25f * (hot - Ambient), "a few seconds off the throttle shouldn't dump the heat");
        Assert.Less(afterHalfAMinute, hot - 0.4f * (hot - Ambient), "half a minute of cooling should bite properly");
        Assert.Greater(afterHalfAMinute, Ambient, "and it still shouldn't be stone cold");
    }

    [Test]
    public void ResponseIsTensOfSecondsNotSingleSeconds()
    {
        float atSpeed = TyreThermal.ResponseSeconds(RaceMps, CoolRate, AirCool, Inertia);
        float parked = TyreThermal.ResponseSeconds(0f, CoolRate, AirCool, Inertia);

        Assert.Greater(atSpeed, 10f, "a tyre that reacts in single-digit seconds tracks the steering instead of the stint");
        Assert.Less(atSpeed, 40f, "and one that takes a minute per corner would never reach temperature at all");
        Assert.Greater(parked, atSpeed, "airflow is what carries the heat away, so a slow car cools slower");
    }

    [Test]
    public void InertiaSlowsTheSwingWithoutMovingWhereItSettles()
    {
        const float work = 0.4f;
        float lazy = Equilibrium(work, RaceMps, Inertia);
        float snappy = Equilibrium(work, RaceMps, 1f);

        Assert.AreEqual(snappy, lazy, 1e-3f, "inertia divides heating and cooling together, so equilibrium can't move");

        float lazyAfter10s = Soak(Ambient, work, 10f, RaceMps, Inertia);
        float snappyAfter10s = Soak(Ambient, work, 10f, RaceMps, 1f);
        Assert.Less(lazyAfter10s, snappyAfter10s, "the point of inertia is that the same ten seconds gets less far");
    }

    [Test]
    public void EquilibriumMatchesTheRatesItWasTunedFrom()
    {
        // heatRate/coolRate was the pair the old model settled on. Slowing the tyre down must not have
        // re-tuned the car underneath it, so the equilibrium still has to come out of that same ratio.
        const float work = 0.4f;
        float expectedRise = (HeatRate * work * TyreThermal.SpeedHeatFactor(RaceMps))
                           / (CoolRate * (1f + RaceMps * AirCool));

        Assert.AreEqual(Ambient + expectedRise, Equilibrium(work), 0.01f);
    }

    // ---- Basic direction-of-travel sanity ----

    [Test]
    public void WorkingTheTyreHeatsItAndLeavingItAloneCoolsIt()
    {
        Assert.Greater(Step(Ambient, 0.5f), Ambient, "a loaded tyre gains heat");
        Assert.Less(Step(120f, 0f), 120f, "an unloaded hot tyre loses it");
    }

    [Test]
    public void TyreNeverCoolsBelowTheAirItSitsIn()
    {
        // A huge step at ambient is the case that used to undershoot: cooling is proportional to the gap,
        // which is zero here, so the only way out is the floor.
        float t = TyreThermal.Step(Ambient, Ambient, 0f, RaceMps, HeatRate, CoolRate, AirCool, Inertia, 5f);
        Assert.GreaterOrEqual(t, Ambient);
    }

    [Test]
    public void FasterCarsCoolHarder()
    {
        float slow = Soak(120f, 0f, 10f, 10f);
        float fast = Soak(120f, 0f, 10f, RaceMps);
        Assert.Less(fast, slow, "airflow over the wheel is the cooling, so speed cools");
    }

    // ---- Scrub heat: weaving warms the fronts ----

    [Test]
    public void SteeringStraightAheadAddsNoScrub()
    {
        Assert.AreEqual(0f, TyreThermal.Scrub01(0f, ScrubFullLockDeg), 1e-6f);
    }

    [Test]
    public void ScrubIgnoresWhichWayTheWheelIsTurnedAndSaturatesAtFullLock()
    {
        Assert.AreEqual(TyreThermal.Scrub01(3f, ScrubFullLockDeg), TyreThermal.Scrub01(-3f, ScrubFullLockDeg), 1e-6f,
            "weaving turns both ways and both of them scrub");
        Assert.AreEqual(1f, TyreThermal.Scrub01(ScrubFullLockDeg, ScrubFullLockDeg), 1e-6f);
        Assert.AreEqual(1f, TyreThermal.Scrub01(ScrubFullLockDeg * 4f, ScrubFullLockDeg), 1e-6f, "and past it, it stops counting");
    }

    [Test]
    public void ScrubRisesWithHowFarTheWheelIsTurned()
    {
        float gentle = TyreThermal.Scrub01(1f, ScrubFullLockDeg);
        float hard = TyreThermal.Scrub01(4f, ScrubFullLockDeg);
        Assert.Greater(hard, gentle);
        Assert.Greater(gentle, 0f, "a degree of lock is a real amount of steering on an oval, not nothing");
    }

    // A tyre's total heat input is its lateral work plus its share of the scrub, which is how TireModel
    // combines them before handing the pair to TyreThermal.
    static float HeatWork(float work, float steerDeg, bool front)
    {
        float scrub = ScrubHeatFront * TyreThermal.Scrub01(steerDeg, ScrubFullLockDeg);
        if (!front) scrub *= ScrubHeatRearShare;
        return work + scrub;
    }

    [Test]
    public void WeavingDownAStraightWarmsTheTyresUp()
    {
        // Running straight on a warm-up lap: almost no lateral load, so almost no heat.
        const float cruiseWork = 0.02f;
        float straight = Soak(Ambient, HeatWork(cruiseWork, 0f, front: true), 45f);
        // Sawing at the wheel: same speed, same cornering load, just steering.
        float weaving = Soak(Ambient, HeatWork(cruiseWork, 3f, front: true), 45f);

        Assert.Less(straight, Ambient + 10f, "cruising in a straight line shouldn't warm tyres at all");
        Assert.Greater(weaving, straight + 20f, "weaving is how a driver gets heat into cold tyres");
    }

    [Test]
    public void WeavingWarmsTheFrontsFasterThanTheRears()
    {
        const float cruiseWork = 0.02f;
        float front = Soak(Ambient, HeatWork(cruiseWork, 3f, front: true), 30f);
        float rear = Soak(Ambient, HeatWork(cruiseWork, 3f, front: false), 30f);

        Assert.Greater(front, rear, "the steered axle does the scrubbing");
        Assert.Greater(front - Ambient, 2f * (rear - Ambient),
            "and by a margin a driver can read off the board, not a rounding difference");
    }

    [Test]
    public void ScrubDoesNotCookTheFrontsOnItsOwn()
    {
        // Full lock held for a minute with no cornering load at all: hot, but not past the point where the
        // front tyres are going off. Warming up must not be a way to ruin the tyre.
        float front = Soak(Ambient, HeatWork(0f, ScrubFullLockDeg, front: true), 60f);
        Assert.Less(front, 110f, "scrub alone shouldn't push the fronts into the overheat-wear band");
        Assert.Greater(front, OptimalC * 0.5f, "but it should be doing real work");
    }

    [Test]
    public void ScrubHeatStillScalesWithSpeed()
    {
        float heatWork = HeatWork(0f, 3f, front: true);
        float slow = TyreThermal.HeatPerSecond(heatWork, 5f, HeatRate, Inertia);
        float fast = TyreThermal.HeatPerSecond(heatWork, RaceMps, HeatRate, Inertia);
        Assert.Greater(fast, slow, "sawing at the wheel in the pit lane isn't a warm-up lap");
    }

    // The work level whose equilibrium is `targetC`, so a timing test can hold the tyre's destination fixed.
    static float WorkForEquilibrium(float targetC)
    {
        float k = TyreThermal.CoolCoefficient(RaceMps, CoolRate, AirCool, Inertia);
        float perUnitWork = TyreThermal.HeatPerSecond(1f, RaceMps, HeatRate, Inertia);
        return (targetC - Ambient) * k / perUnitWork;
    }
}
