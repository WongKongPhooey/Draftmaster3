using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// A career track left live with nothing booked must not keep the player going round in sessions.
//
// A save held raceweekend.sessionlive = 1 with weekend.route empty. Every load of the race scene then put the
// player's field out. Ending the session took PracticeDirector's old standalone path: flip the session kind and
// reload. That stood the player back in the RV at the start of another session the sheet never booked, and the
// booking never got its result. These tests check that each way out of that state turns the track off:
// CONTINUE (ResumeRoutedSession), a session ending unrouted (FinishUnroutedSession) and a race classified
// unrouted (FinishRoutedSession).
//
// WeekendDirector and RaceWeekend live in Assembly-CSharp, which an asmdef cannot reference, so they are reached
// by reflection. The prefs touched are the editor's real career prefs, so every one is put back afterwards.
public class UnroutedSessionTests
{
    static readonly string[] StringKeys = { "weekend.route", "weekend.appointment", "career.savedat" };
    static readonly string[] IntKeys = { "raceweekend.sessionlive" };

    static readonly Type Director = Type.GetType("WeekendDirector, Assembly-CSharp");
    static readonly Type Weekend = Type.GetType("RaceWeekend, Assembly-CSharp");
    static readonly Type Session = Type.GetType("RaceWeekend+Session, Assembly-CSharp");
    static readonly Type GameSessionType = Type.GetType("GameSession, Assembly-CSharp");

    const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

    readonly string[] _strings = new string[StringKeys.Length];
    readonly bool[] _hadString = new bool[StringKeys.Length];
    readonly int[] _ints = new int[IntKeys.Length];
    readonly bool[] _hadInt = new bool[IntKeys.Length];
    object _mode;

    [SetUp]
    public void Save()
    {
        Assert.IsNotNull(Director, "WeekendDirector is missing from Assembly-CSharp.");
        Assert.IsNotNull(Weekend, "RaceWeekend is missing from Assembly-CSharp.");
        Assert.IsNotNull(GameSessionType, "GameSession is missing from Assembly-CSharp.");

        for (int i = 0; i < StringKeys.Length; i++)
        {
            _hadString[i] = PlayerPrefs.HasKey(StringKeys[i]);
            _strings[i] = PlayerPrefs.GetString(StringKeys[i], "");
        }
        for (int i = 0; i < IntKeys.Length; i++)
        {
            _hadInt[i] = PlayerPrefs.HasKey(IntKeys[i]);
            _ints[i] = PlayerPrefs.GetInt(IntKeys[i], 0);
        }

        // A career: the live flag is only read off disk while one is active.
        var mode = GameSessionType.GetField("CurrentMode", Static);
        _mode = mode.GetValue(null);
        mode.SetValue(null, Enum.Parse(mode.FieldType, "SinglePlayer"));

        // The leftover state: track live, nothing routed, nothing booked.
        PlayerPrefs.SetString("weekend.route", "");
        PlayerPrefs.SetString("weekend.appointment", "");
        SessionLive = true;
        Assume.That(SessionLive, Is.True, "Could not put the track live for the test.");
    }

    [TearDown]
    public void Restore()
    {
        for (int i = 0; i < StringKeys.Length; i++)
            if (_hadString[i]) PlayerPrefs.SetString(StringKeys[i], _strings[i]);
            else PlayerPrefs.DeleteKey(StringKeys[i]);
        for (int i = 0; i < IntKeys.Length; i++)
            if (_hadInt[i]) PlayerPrefs.SetInt(IntKeys[i], _ints[i]);
            else PlayerPrefs.DeleteKey(IntKeys[i]);
        PlayerPrefs.Save();

        GameSessionType.GetField("CurrentMode", Static).SetValue(null, _mode);
        var invalidate = Type.GetType("Draftmaster.Weekend.FramePrefs, Draftmaster.Weekend")
                             ?.GetMethod("Invalidate", Static);
        invalidate?.Invoke(null, null);
    }

    static bool SessionLive
    {
        get => (bool)Weekend.GetProperty("SessionLive", Static).GetValue(null);
        set => Weekend.GetProperty("SessionLive", Static).SetValue(null, value);
    }

    static string Route => (string)Director.GetProperty("PendingRouteId", Static).GetValue(null);

    // WeekendOutcome.Nothing, read off the parameter type so the test needs no reference to its assembly.
    static object NoOutcome(MethodInfo m)
    {
        Type t = null;
        foreach (var p0 in m.GetParameters())
            if (p0.ParameterType.Name == "WeekendOutcome") t = p0.ParameterType;
        Assert.IsNotNull(t, m.Name + " no longer takes a WeekendOutcome.");
        var f = t.GetField("Nothing", Static);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty("Nothing", Static);
        return p != null ? p.GetValue(null) : Activator.CreateInstance(t);
    }

    [Test]
    public void Continue_WithTrackLiveAndNothingRouted_TurnsTheTrackOff()
    {
        Director.GetMethod("ResumeRoutedSession", Static).Invoke(null, null);
        Assert.IsFalse(SessionLive, "CONTINUE left the track live with no booking behind it.");
    }

    [Test]
    public void UnroutedSessionEnd_WithNothingBooked_TurnsTheTrackOffWithoutRouting()
    {
        var m = Director.GetMethod("FinishUnroutedSession", Static);
        Assert.IsNotNull(m, "WeekendDirector.FinishUnroutedSession is missing.");
        m.Invoke(null, new[] { Enum.Parse(Session, "Qualifying"), NoOutcome(m) });

        Assert.IsFalse(SessionLive, "Ending an unrouted qualifying session left the track live.");
        Assert.AreEqual("", Route, "Nothing was booked, so nothing should have been routed.");
    }

    [Test]
    public void RaceClassifiedUnrouted_TurnsTheTrackOff()
    {
        var m = Director.GetMethod("FinishRoutedSession", Static);
        Assert.IsNotNull(m, "WeekendDirector.FinishRoutedSession is missing.");
        m.Invoke(null, new[] { NoOutcome(m), (object)false });

        Assert.IsFalse(SessionLive, "A race classified with nothing routed left the track live.");
    }
}
