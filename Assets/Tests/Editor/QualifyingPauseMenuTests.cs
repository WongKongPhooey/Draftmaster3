using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The pause menu's way out of qualifying is END SESSION. It used to read START RACE, which flipped the session
// to the race and reloaded the scene. In a career the weekend sheet still had qualifying live, so the reload put
// the player back at the start of qualifying instead of starting the race.
//
// PracticeDirector lives in Assembly-CSharp, which an asmdef cannot reference, so it is reached by reflection.
// AddComponent in edit mode does not run Awake, so the instance and session kind are set directly.
public class QualifyingPauseMenuTests
{
    static readonly Type Director = Type.GetType("PracticeDirector, Assembly-CSharp");
    static readonly Type GameSessionType = Type.GetType("GameSession, Assembly-CSharp");

    GameObject _go;
    object _mode;

    [SetUp]
    public void Make()
    {
        Assert.IsNotNull(Director, "PracticeDirector is missing from Assembly-CSharp.");
        Assert.IsNotNull(GameSessionType, "GameSession is missing from Assembly-CSharp.");

        var mode = GameSessionType.GetField("CurrentMode", BindingFlags.Public | BindingFlags.Static);
        _mode = mode.GetValue(null);
        mode.SetValue(null, Enum.Parse(mode.FieldType, "SinglePlayer"));

        _go = new GameObject("PracticeDirectorTest");
        var d = _go.AddComponent(Director);
        Director.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, d);
    }

    [TearDown]
    public void Clean()
    {
        Director.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).SetValue(null, null);
        UnityEngine.Object.DestroyImmediate(_go);
        GameSessionType.GetField("CurrentMode", BindingFlags.Public | BindingFlags.Static).SetValue(null, _mode);
    }

    static string Label =>
        (string)Director.GetProperty("PauseMenuExitLabel", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    void SetQualifying(bool q) =>
        Director.GetField("_isQualifying", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_go.GetComponent(Director), q);

    [Test]
    public void Qualifying_OffersEndSession_NotStartRace()
    {
        SetQualifying(true);
        Assert.AreEqual("End Session", Label, "Qualifying's pause menu row must end the session, not reload into a race.");
    }

    [Test]
    public void Practice_OffersEndSession()
    {
        SetQualifying(false);
        Assert.AreEqual("End Session", Label);
    }
}
