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

    // The player's Auto car with the player on the pit wall: no PIT button to press, so the chief calls the stop.
    // Phone/editor, Daytona 2026-11-25: 14.3 L a lap flat out, the tank ran dry under the chief and the car stopped on
    // the circuit.
    [Test]
    public void ChiefCallsTheStopWhenTheTankIsLowAndWillNotMakeIt()
    {
        const float perLap = 14.3f;
        Assert.IsFalse(FuelPlan.ChiefCallsStop(50f, perLap, 8f), "three laps in the tank: not yet");
        Assert.IsTrue(FuelPlan.ChiefCallsStop(20f, perLap, 8f), "1.4 laps left in it, 8 to go: in");
        Assert.IsTrue(FuelPlan.ChiefCallsStop(0f, perLap, 8f), "dry: in, if it can get there");
    }

    [Test]
    public void ChiefLeavesACarThatMakesTheFinishOut()
    {
        const float perLap = 14.3f;
        // 1.4 laps of fuel, one lap to go: it makes it with margin, so no stop.
        Assert.IsFalse(FuelPlan.ChiefCallsStop(20f, perLap, 1f));
        // Just short of the margin: it would coast over the line, if at all - call it.
        Assert.IsTrue(FuelPlan.ChiefCallsStop(15f, perLap, 1f));
        // Race distance not known yet: the tank alone decides.
        Assert.IsTrue(FuelPlan.ChiefCallsStop(15f, perLap, -1f));
        Assert.IsFalse(FuelPlan.ChiefCallsStop(30f, perLap, -1f));
        // No fuel plan (practice, no race): never.
        Assert.IsFalse(FuelPlan.ChiefCallsStop(0f, 0f, 8f));
    }
}
