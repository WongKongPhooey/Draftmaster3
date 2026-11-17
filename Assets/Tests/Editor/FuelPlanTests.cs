using Draftmaster.Sim;
using NUnit.Framework;

// Race fuel: a full tank lasts the race (or one stint of it) plus the spare laps flat out, less with a lift.
public class FuelPlanTests
{
    const float Tank = 100f, Lap = 4023f;   // Daytona

    [Test]
    public void FlatOutAFullTankLastsTheRacePlusTheSpare()
    {
        float perMetre = FuelPlan.LitresPerMetreFull(Tank, 60, Lap, 0, 2f);
        float laps = Tank / (perMetre * Lap);
        Assert.AreEqual(62f, laps, 1e-3f);
        // Any race length: the tank is sized to it.
        perMetre = FuelPlan.LitresPerMetreFull(Tank, 5, Lap, 0, 2f);
        Assert.AreEqual(7f, Tank / (perMetre * Lap), 1e-3f);
    }

    [Test]
    public void OneRequiredStopHalvesTheStint()
    {
        Assert.AreEqual(32f, FuelPlan.StintLaps(60, 1, 2f), 1e-4f);
        Assert.Less(FuelPlan.StintLaps(60, 1, 2f), 60f, "a stop must be needed to make the distance");
    }

    [Test]
    public void LiftingBurnsLessButNeverNothing()
    {
        Assert.AreEqual(1f, FuelPlan.BurnShare(1f), 1e-6f);
        Assert.AreEqual(FuelPlan.IdleShare, FuelPlan.BurnShare(0f), 1e-6f);
        Assert.Less(FuelPlan.BurnShare(0.7f), 1f);
    }

    [Test]
    public void NoSavingWhileTheTankMakesIt()
    {
        float perLap = 2f;
        Assert.AreEqual(1f, FuelPlan.ThrottleCapToFinish(30f, 10f, perLap, 0.5f), 1e-6f);
    }

    [Test]
    public void ShortOfTheFinishTheDriverLiftsJustEnough()
    {
        float perLap = 2f;
        // 18 litres = 9 laps flat out; 10 laps + 0.5 to go.
        float cap = FuelPlan.ThrottleCapToFinish(18f, 10f, perLap, 0.5f);
        Assert.Less(cap, 1f);
        Assert.Greater(cap, 0.8f);
        // That throttle makes it: burn share at the cap times laps needed = what is in the tank.
        Assert.AreEqual(18f, FuelPlan.BurnShare(cap) * perLap * 10.5f, 1e-3f);
        // Hopelessly short: held at the floor, not crawling.
        Assert.AreEqual(0.5f, FuelPlan.ThrottleCapToFinish(2f, 10f, perLap, 0.5f), 1e-6f);
    }
}
