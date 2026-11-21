using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Grudges outlive a race only in the career (Josh, 2026-11-21): a single race starts every pair at 0 and writes nothing
// to PlayerPrefs, while the career keeps what contact did. Runtime types by reflection - the test assembly does not
// reference Assembly-CSharp.
public class DriverRelationshipsScopeTests
{
    const string A = "ScopeTest Alpha", B = "ScopeTest Bravo";
    const string PrefKey = "rel.scopetest alpha|scopetest bravo";
    object _mode;

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " is missing from Assembly-CSharp.");
        return type;
    }

    static FieldInfo ModeField => Runtime("GameSession").GetField("CurrentMode", BindingFlags.Static | BindingFlags.Public);
    static void SetMode(string mode) => ModeField.SetValue(null, Enum.Parse(Runtime("GameSession+Mode"), mode));

    static float Get() =>
        (float)Runtime("DriverRelationships").GetMethod("Get").Invoke(null, new object[] { A, B });
    static void Modify(float delta) =>
        Runtime("DriverRelationships").GetMethod("Modify").Invoke(null, new object[] { A, B, delta });

    [SetUp]
    public void SetUp()
    {
        _mode = ModeField.GetValue(null);
        PlayerPrefs.DeleteKey(PrefKey);
    }

    [TearDown]
    public void TearDown()
    {
        SetMode("SinglePlayer");
        Modify(-Get());   // back to neutral in the career table
        PlayerPrefs.DeleteKey(PrefKey);
        ModeField.SetValue(null, _mode);
    }

    [Test]
    public void ASingleRaceKeepsItsGrudgesToItselfAndStartsFromNeutral()
    {
        SetMode("SinglePlayer");
        Modify(-80f);
        Assert.AreEqual(-80f, Get(), 0.01f, "the career keeps contact's grudge");

        SetMode("SingleRace");
        Assert.AreEqual(0f, Get(), 0.01f, "a single race does not see the career's grudge");
        Modify(-20f);
        Assert.AreEqual(-20f, Get(), 0.01f, "contact in the race still counts within it");

        SetMode("SinglePlayer");
        Assert.AreEqual(-80f, Get(), 0.01f, "the single race wrote nothing to the career table");
        Assert.AreEqual(-80f, PlayerPrefs.GetFloat(PrefKey, 0f), 0.01f);
    }
}
