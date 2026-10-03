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
}
