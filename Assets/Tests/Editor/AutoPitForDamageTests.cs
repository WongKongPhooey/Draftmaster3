using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The player's car on Auto comes in for repairs: DriveModeController fits it a PitStopController the first time Auto
// takes it in a race - damage stops only (no tyre strategy, no demo stop), in the player's box - and the AI field's
// own pit brains keep racing on bent under green unless told otherwise.
//
// The runtime lives in Assembly-CSharp, which this assembly cannot reference, so it is reached by reflection (as
// RaceEndPitInTests). WantsPit is asked directly under green; the caution path needs a safety car and a track.
public class AutoPitForDamageTests
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

    const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // A car with a pit brain and bodywork bent to `damage`, asked under green whether it wants to pit.
    static bool WantsPitUnderGreen(float damage, Action<Type, Component> configure)
    {
        var pitType = Runtime("PitStopController");
        var damageType = Runtime("VehicleDamage");
        var phaseProp = Runtime("RaceStart").GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
        object before = phaseProp.GetValue(null);
        var go = new GameObject("Car");
        try
        {
            phaseProp.SetValue(null, Enum.Parse(phaseProp.PropertyType, "Green"));
            go.AddComponent(Runtime("SplineDriver"));
            var pit = go.AddComponent(pitType);
            pitType.GetField("forcedPit").SetValue(pit, false);   // the demo stop is not what is under test
            configure(pitType, pit);

            var body = go.AddComponent(damageType);
            damageType.GetProperty("DamageLevel").GetSetMethod(true).Invoke(body, new object[] { damage });
            pitType.GetField("_spline", Inst).SetValue(pit, go.GetComponent(Runtime("SplineDriver")));
            pitType.GetField("_bodywork", Inst).SetValue(pit, body);

            return (bool)pitType.GetMethod("WantsPit", Inst).Invoke(pit, null);
        }
        finally
        {
            phaseProp.SetValue(null, before);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Field_RacesOnBentUnderGreen_ByDefault()
    {
        Assert.IsFalse(WantsPitUnderGreen(0.5f, (t, c) => { }),
            "The AI field is repaired at a caution, not sent in under green: greenRepairDamage defaults to off.");
    }

    [Test]
    public void AutoCar_ComesInUnderGreen_OnceBadlyDamaged()
    {
        Action<Type, Component> auto = (t, c) =>
        {
            t.GetField("tyreStops").SetValue(c, false);
            t.GetField("greenRepairDamage").SetValue(c, 0.15f);
        };
        Assert.IsTrue(WantsPitUnderGreen(0.2f, auto), "Bent past greenRepairDamage under green: in for repairs.");
        Assert.IsFalse(WantsPitUnderGreen(0.05f, auto), "A scrape under green is not worth a stop.");
        Assert.IsFalse(WantsPitUnderGreen(0f, auto), "Undamaged, with tyre stops off: no stop.");
    }

    [Test]
    public void FreshPitBrain_IsNotRunningAStop_AndReleaseIsANoOp()
    {
        var pitType = Runtime("PitStopController");
        var go = new GameObject("Car");
        try
        {
            go.AddComponent(Runtime("SplineDriver"));
            var pit = go.AddComponent(pitType);
            Assert.IsFalse((bool)pitType.GetProperty("RunningServiceStop").GetValue(pit),
                "A racing car is not making a stop, so PlayerPitService must not stand aside for it.");
            pitType.GetMethod("ReleaseForHuman").Invoke(pit, null);
            Assert.IsFalse((bool)pitType.GetProperty("IsPitting").GetValue(pit), "Releasing a racing car leaves it racing.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void Auto_FitsTheDamagePitBrain_AndHandsBackCleanly()
    {
        string drive = Source("Scripts/DriveModeController.cs");
        StringAssert.Contains("EnsureAutoPitBrain();", drive, "EnterAuto must give the player's car a pit brain.");
        StringAssert.Contains("pit.tyreStops = false;", drive, "Auto stops the player's car for damage, not tyres.");
        StringAssert.Contains("pit.forcedPit = false;", drive, "No demo stop for the player's car.");
        StringAssert.Contains("pit.boxOverride = PitLane.PlayerBox;", drive, "The player's car stops in the player's box.");
        StringAssert.Contains("pit.ReleaseForHuman();", drive, "Taking the car back mid-stop must drop the stop.");
        StringAssert.Contains("RunningServiceStop", Source("Scripts/OnFoot/PlayerPitService.cs"),
            "PlayerPitService must not service the car a second time while Auto runs the stop.");
    }
}
