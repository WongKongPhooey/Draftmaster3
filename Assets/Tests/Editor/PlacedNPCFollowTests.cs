using System;
using NUnit.Framework;

// A placed NPC anchored to the player's car (the crew chief) keeps up with the car only while it is parked.
//
// Following exists for GridSpawner fitting the car into its pit box a few frames into the scene, and for a
// tow or an in-lap putting it back there. Following a car that is being DRIVEN had the chief walking
// alongside it down pit road until the paddock fence stopped him, stuck there for the session.
//
// PlacedNPC lives in Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection.
public class PlacedNPCFollowTests
{
    static readonly Type PlacedType = Type.GetType("PlacedNPC, Assembly-CSharp");

    static bool KeepsUp(bool talking, bool driven)
    {
        Assert.IsNotNull(PlacedType, "PlacedNPC is gone from Assembly-CSharp; this test is out of date.");
        var method = PlacedType.GetMethod("KeepsUpWithCar");
        Assert.IsNotNull(method, "PlacedNPC has no KeepsUpWithCar rule.");
        return (bool)method.Invoke(null, new object[] { talking, driven });
    }

    [Test]
    public void CarAnchoredNPC_FollowsTheParkedCar_NotTheDrivenOne()
    {
        Assert.IsTrue(KeepsUp(false, false), "The chief no longer goes with the car when it is re-parked into its box.");
        Assert.IsFalse(KeepsUp(false, true), "The chief walks alongside the car as it is driven out of the pits.");
        Assert.IsFalse(KeepsUp(true, false), "The chief was moved mid-conversation.");
        Assert.IsFalse(KeepsUp(true, true), "The chief was moved mid-conversation.");
    }

    static bool Driven(bool controllerOn, bool aiSplineOn)
    {
        Assert.IsNotNull(PlacedType, "PlacedNPC is gone from Assembly-CSharp; this test is out of date.");
        var method = PlacedType.GetMethod("CarIsDriven");
        Assert.IsNotNull(method, "PlacedNPC has no CarIsDriven rule.");
        return (bool)method.Invoke(null, new object[] { controllerOn, aiSplineOn });
    }

    // The broadcast cut, the crew chief's headset and the end-of-session pit-in hand the car to the AI's
    // SplineDriver with the controller switched OFF. Counting only the controller took that for a parked car,
    // and the chief walked out of the pits beside it and stood wherever it was on the circuit.
    [Test]
    public void CarDrivenByTheAISpline_CountsAsDriven()
    {
        Assert.IsTrue(Driven(true, false), "A car the player is driving reads as parked.");
        Assert.IsTrue(Driven(false, true), "A car the AI's spline is driving (headset / broadcast cut) reads as parked.");
        Assert.IsFalse(Driven(false, false), "A car nobody is driving does not read as parked.");
    }
}
