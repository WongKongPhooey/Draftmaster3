using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// After the chequered flag every car runs a cool-down lap and then comes down pit road to park in its own box:
// the AI field through each car's PitStopController, the player's car through RaceEndPitIn, which hands it to
// the AI at the end of the cool-down lap the player drove. RaceDirector.TookChequer is what both key off.
//
// The runtime lives in Assembly-CSharp, which this assembly cannot reference, so it is reached by reflection —
// the same approach as PitCrewSignTests. The drive itself needs a track and ticking physics, which EditMode
// cannot give it, so what is pinned here is the contract and the wiring.
public class RaceEndPitInTests
{
    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    static string Source(string relative)
    {
        string path = Path.Combine(Application.dataPath, relative);
        Assert.IsTrue(File.Exists(path), $"{relative} has moved; this test is written against it.");
        return File.ReadAllText(path);
    }

    [Test]
    public void NoCarHasTakenTheChequer_WithoutARaceDirector()
    {
        var took = Runtime("RaceDirector").GetMethod("TookChequer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(took, "RaceDirector.TookChequer(Transform) is what the post-race pit-in keys off.");

        var go = new GameObject("Car");
        try
        {
            Assert.IsFalse((bool)took.Invoke(null, new object[] { go.transform }),
                "Outside a race (practice, qualifying, multiplayer) no car may be sent in to park.");
            Assert.IsFalse((bool)took.Invoke(null, new object[] { null }));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void AIField_ParksAfterTheRaceByDefault()
    {
        var pit = Runtime("PitStopController");
        var go = new GameObject("AICar");
        try
        {
            go.AddComponent(Runtime("SplineDriver"));
            var c = go.AddComponent(pit);
            Assert.IsTrue((bool)pit.GetField("parkAfterRace").GetValue(c), "The field comes in after the race unless told not to.");
            float frac = (float)pit.GetField("coolDownLapFrac").GetValue(c);
            Assert.That(frac, Is.InRange(0.01f, 1f), "A car must run some of a lap after the flag before it may pit, or there is no cool-down lap.");
            Assert.IsFalse((bool)pit.GetProperty("IsParkedAfterRace").GetValue(c), "A fresh car is racing, not parked.");
            Assert.IsNotNull(pit.GetMethod("ParkNow", BindingFlags.Public | BindingFlags.Instance),
                "RaceEndPitIn brings the player's car straight in through ParkNow.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void RaceDirector_InstallsThePlayersPitIn()
    {
        Runtime("RaceEndPitIn");
        StringAssert.Contains("AddComponent<RaceEndPitIn>", Source("Scripts/RaceDirector.cs"),
            "Without RaceEndPitIn the player's car is the one car left circulating after the race.");
    }

    [Test]
    public void ParkingIsNotAPitStop()
    {
        string src = Source("Scripts/AI/PitStopController.cs");
        StringAssert.Contains("if (_parking)", src, "Arriving to park must not start a service.");
        StringAssert.Contains("!_parking", src, "No stop board for a car coming in to park.");
        StringAssert.Contains("State.Parked", src);
    }
}
