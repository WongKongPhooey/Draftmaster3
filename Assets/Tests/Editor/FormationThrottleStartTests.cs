using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for FormationDirector.ThrottleStartsFormation — the gate that keeps the safety car and the
// parked field still after the player climbs in, until the player first presses the accelerator.
//
// FormationDirector and PlayerVehicleController live in Assembly-CSharp, which an asmdef can't reference, so
// they're reached by reflection.
public class FormationThrottleStartTests
{
    static System.Type T(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    GameObject _go;
    Component _car;

    [SetUp]
    public void Make()
    {
        _go = new GameObject("FormationThrottleStartTest_Car");
        _car = _go.AddComponent(T("PlayerVehicleController"));
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    void Throttle(float value) =>
        _car.GetType().GetField("_lastThrottleIn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_car, value);

    bool Starts(Component car, float threshold = 0.1f) =>
        (bool)T("FormationDirector").GetMethod("ThrottleStartsFormation").Invoke(null, new object[] { car, threshold });

    [Test]
    public void ParkedUntilThePlayerPressesTheAccelerator()
    {
        Throttle(0f);
        Assert.IsFalse(Starts(_car), "the field rolled off with the player's foot off the pedal");
        Throttle(0.05f);
        Assert.IsFalse(Starts(_car), "a brush under the threshold started the formation lap");
        Throttle(1f);
        Assert.IsTrue(Starts(_car), "full throttle didn't start the formation lap");
    }

    [Test]
    public void AnAIDrivenPlayerCarStartsAtOnce()
    {
        // The AI's controller is pinned until the formation lap begins, so it would never press the pedal.
        Throttle(0f);
        _car.GetType().GetField("externalInput").SetValue(_car, true);
        Assert.IsTrue(Starts(_car));
    }

    [Test]
    public void NoCarNeverDeadlocksTheStart()
    {
        Assert.IsTrue(Starts(null));
        Throttle(0f);
        _go.SetActive(false);
        Assert.IsTrue(Starts(_car));
    }
}
