using System;
using System.Reflection;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// A real VehicleDamage body (a 64x32 livery at 12.8 px/m, Cup safety cell) folded against a wall: a hard enough hit
// over a wheel crushes the outline past the hub and the wheel comes off; lighter ones dent the panel and leave it
// on; RepairFull puts it back. The pure outline test is WheelHubsTests.
//
// VehicleDamage lives in Assembly-CSharp, which this assembly cannot reference, so it is reached by reflection.
public class VehicleDamageWheelTests
{
    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    GameObject _go;
    Texture2D _tex;
    Component _body;
    Type _type;

    [SetUp]
    public void SetUp()
    {
        _type = Runtime("VehicleDamage");
        _tex = new Texture2D(64, 32);
        var sprite = Sprite.Create(_tex, new Rect(0, 0, 64, 32), new Vector2(0.5f, 0.5f), 12.8f);
        _go = new GameObject("WheelTestCar");
        _body = _go.AddComponent(_type);
        _type.GetField("sourceSprite").SetValue(_body, sprite);
        _type.GetField("overrideSeries").SetValue(_body, true);
        var seriesField = _type.GetField("series");
        seriesField.SetValue(_body, Enum.Parse(seriesField.FieldType, "Cup"));
        _type.GetMethod("Build").Invoke(_body, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        if (_tex != null) UnityEngine.Object.DestroyImmediate(_tex);
    }

    int WheelsOff => (int)_type.GetProperty("WheelsOff").GetValue(_body);
    bool WheelOff(int i) => (bool)_type.GetMethod("WheelOff").Invoke(_body, new object[] { i });

    // A wall along the car's -y side (the sprite's y = 0 edge, hubs 0 and 2), pressing in. It touches the bodywork
    // where it stands now — the collision solver has pushed the car back out until the wall rests on the furthest
    // metal — so repeated hits fold on into the dents already there, as they do in a race.
    void WallOnSide(float severity)
    {
        var current = (Vector3[])_type.GetProperty("CurrentVertices").GetValue(_body);
        float face = float.MaxValue;
        foreach (var v in current) face = Mathf.Min(face, v.y);
        var striker = BodyDeform.Striker.Plane(new Vector2(0f, face), Vector2.up);
        var method = _type.GetMethod("OnImpact",
            new[] { typeof(BodyDeform.Striker).MakeByRefType(), typeof(float), typeof(float) });
        Assert.IsNotNull(method);
        method.Invoke(_body, new object[] { striker, severity, BodyDeform.RigidPartner });
    }

    [Test]
    public void ALightWallHitDentsTheSideButKeepsEveryWheel()
    {
        WallOnSide(0.3f);
        Assert.Greater((float)_type.GetProperty("DamageLevel").GetValue(_body), 0f, "the hit never landed");
        Assert.AreEqual(0, WheelsOff);
    }

    // A wall square on the nose (local -x, the livery's sprite x = 0), pressing back.
    void WallOnNose(float severity)
    {
        var current = (Vector3[])_type.GetProperty("CurrentVertices").GetValue(_body);
        float face = float.MaxValue;
        foreach (var v in current) face = Mathf.Min(face, v.x);
        var striker = BodyDeform.Striker.Plane(new Vector2(face, 0f), Vector2.right);
        var method = _type.GetMethod("OnImpact",
            new[] { typeof(BodyDeform.Striker).MakeByRefType(), typeof(float), typeof(float) });
        method.Invoke(_body, new object[] { striker, severity, BodyDeform.RigidPartner });
    }

    [Test]
    public void AFullSeverityBroadsideTakesTheFrontWheelOnThatSideOnly()
    {
        int lost = 0;
        var evt = _type.GetEvent("WheelLost");
        Action<int, Vector2> handler = (i, p) => lost++;
        evt.AddEventHandler(_body, handler);

        WallOnSide(1f);

        // The front corner folds furthest (the ends are softer than the flanks); the rear wheel sits beside the
        // tub, where the Cup cell keeps the side stiff, so one hit is not enough for it.
        Assert.IsTrue(WheelOff(0), "front hub on the wall side still covered after a full-severity broadside");
        Assert.IsFalse(WheelOff(1), "a wheel came off the side that never touched the wall");
        Assert.IsFalse(WheelOff(3), "a wheel came off the side that never touched the wall");
        Assert.AreEqual(WheelsOff, lost, "WheelLost should fire once per wheel");

        // Another hit does not lose the same wheel twice.
        int before = WheelsOff;
        WallOnSide(1f);
        Assert.IsTrue(WheelOff(0));
        Assert.AreEqual(WheelsOff, lost);
        Assert.GreaterOrEqual(WheelsOff, before);
    }

    [Test]
    public void MediumWallHitsBendTheSideButNeverReachAHub()
    {
        // Once the wall rests on the stiff middle of the car it cannot reach any further into the arches.
        for (int i = 0; i < 5; i++) WallOnSide(0.5f);
        Assert.Greater((float)_type.GetProperty("DamageLevel").GetValue(_body), 0.3f, "the hits never landed");
        Assert.AreEqual(0, WheelsOff);
    }

    [Test]
    public void TwoHeadOnHitsCrushTheNosePastTheFrontAxle()
    {
        WallOnNose(1f);
        Assert.AreEqual(0, WheelsOff, "one head-on folds the bonnet but stops short of the front axle");
        WallOnNose(1f);
        Assert.IsTrue(WheelOff(0) && WheelOff(1), "a second head-on should crush the nose past both front hubs");
        Assert.IsFalse(WheelOff(2) || WheelOff(3), "a head-on took a rear wheel");
    }

    // Another car already `buried` metres inside our flank on the hub side when the contact registers: cars in a
    // pack lean on each other, so the solver reads a real overlap from a closing speed that is barely a nudge.
    void BuriedSideContact(float severity, float buried)
    {
        var current = (Vector3[])_type.GetProperty("CurrentVertices").GetValue(_body);
        float face = float.MaxValue;
        foreach (var v in current) face = Mathf.Min(face, v.y);
        var striker = BodyDeform.Striker.Plane(new Vector2(0f, face + buried), Vector2.up);
        var method = _type.GetMethod("OnImpact",
            new[] { typeof(BodyDeform.Striker).MakeByRefType(), typeof(float), typeof(float) });
        method.Invoke(_body, new object[] { striker, severity, BodyDeform.RigidPartner });
    }

    // Daytona 2026-11-21: a 3 m/s nudge took a wheel off five seconds after the green, the car stopped dead in the
    // pack and the field piled into it. The hub is a full wheel width inside the body, so a fold that only reaches
    // the tyre leaves the wheel on...
    [Test]
    public void AFoldShortOfAWheelWidthKeepsTheWheel()
    {
        BuriedSideContact(0.2f, 0.2f);
        Assert.Greater((float)_type.GetProperty("DamageLevel").GetValue(_body), 0f, "the nudge never landed");
        Assert.AreEqual(0, WheelsOff, "a fold that only reached the tyre took a wheel off");
    }

    // ...and one that crushes the side in past a whole wheel width takes it, however gently it got there.
    [Test]
    public void AFoldPastAWheelWidthTakesTheWheel()
    {
        BuriedSideContact(0.2f, 0.5f);
        Assert.IsTrue(WheelOff(0), "the side is folded in past the hub and the wheel is still on");
    }

    [Test]
    public void AFullRepairPutsTheWheelsBackOn()
    {
        WallOnSide(1f);
        Assert.Greater(WheelsOff, 0);
        _type.GetMethod("RepairFull").Invoke(_body, null);
        Assert.AreEqual(0, WheelsOff);
        for (int i = 0; i < 4; i++) Assert.IsFalse(WheelOff(i));
    }
}
