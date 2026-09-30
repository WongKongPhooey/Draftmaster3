using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// SINGLE RACE used to put the player in the #89 whoever they picked: the pick was saved, but RaceScene's
// PlayerCar is painted as the 89 and nothing repainted it. PlayerCarLivery does, and everything else reads
// the number off that paint — so these check the repaint itself. Reached by reflection because this
// assembly can't reference Assembly-CSharp.
public class PlayerCarLiveryTests
{
    GameObject _car;

    [TearDown]
    public void TearDown()
    {
        if (_car != null) Object.DestroyImmediate(_car);
    }

    [Test]
    public void LiveryName_KeepsTheCarsetAndSwapsTheNumber()
    {
        Assert.AreEqual("cup26livery24", LiveryName("cup26livery89", 24));
        Assert.AreEqual("cup26livery8", LiveryName("cup26livery89", 8));
        Assert.AreEqual("xfi25livery7", LiveryName("xfi25livery21alt1", 7));
        Assert.IsNull(LiveryName("PlayerCarSprite", 24), "not a livery, so no carset to keep");
        Assert.IsNull(LiveryName("", 24));
    }

    [Test]
    public void Apply_RepaintsThePlayerCarAsTheChosenNumber()
    {
        var car = BuildCar("cup26livery89");

        Assert.IsTrue(Apply(car, 24));
        Assert.AreEqual("cup26livery24", Damage(car).sourceSprite.name);
        Assert.AreEqual(24, NumberOf(car), "CarIdentity must read the chosen number off the new paint");
    }

    [Test]
    public void Apply_LeavesTheCarAloneWhenAlreadyThatNumberOrNoPaintExists()
    {
        var car = BuildCar("cup26livery89");

        Assert.IsFalse(Apply(car, 89));
        Assert.AreEqual("cup26livery89", Damage(car).sourceSprite.name);

        Assert.IsFalse(Apply(car, 999), "no cup26livery999 exists");
        Assert.AreEqual("cup26livery89", Damage(car).sourceSprite.name);
    }

    // ------------------------------------------------------------------ helpers

    GameObject BuildCar(string livery)
    {
        var sprite = Resources.Load<Sprite>(livery);
        Assert.IsNotNull(sprite, $"Resources/{livery} missing");

        _car = new GameObject("PlayerCarLiveryTest");
        var damage = _car.AddComponent(RuntimeType("VehicleDamage"));
        damage.GetType().GetField("sourceSprite").SetValue(damage, sprite);
        return _car;
    }

    static SpriteHolder Damage(GameObject car) => new SpriteHolder(car.GetComponent(RuntimeType("VehicleDamage")));

    static string LiveryName(string spriteName, int number) =>
        (string)RuntimeType("PlayerCarLivery").GetMethod("LiveryName").Invoke(null, new object[] { spriteName, number });

    static bool Apply(GameObject car, int number) =>
        (bool)RuntimeType("PlayerCarLivery").GetMethod("Apply").Invoke(null, new object[] { car, number });

    static int NumberOf(GameObject car) =>
        (int)RuntimeType("CarIdentity").GetMethod("NumberOf").Invoke(null, new object[] { car });

    static System.Type RuntimeType(string name)
    {
        var type = System.AppDomain.CurrentDomain.GetAssemblies()
                         .Select(a => a.GetType(name, false))
                         .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, $"{name} not found");
        return type;
    }

    sealed class SpriteHolder
    {
        public readonly Sprite sourceSprite;
        public SpriteHolder(Component damage) =>
            sourceSprite = (Sprite)damage.GetType().GetField("sourceSprite").GetValue(damage);
    }
}
