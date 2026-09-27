using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The player's car has to be sat in the player's own pit box when a practice session opens.
//
// PitLaneStart parks the car part-way down pit road at the scene open, before any boxes exist; GridSpawner
// fits the box ladder when the field arrives and moves the car into the box it reserved. A car left on the
// opening pose sits beside the box lane, off the paddock's walkable area, where the player can't reach it —
// a session nobody can start.
public class PracticeCarInBoxTests
{
    const string Race = "RaceScene";
    const float Tolerance = 1.5f;   // metres

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.Public | BindingFlags.NonPublic;

    static readonly string[] SavedIntPrefs = { "raceweekend.sessionlive" };
    readonly Dictionary<string, int?> _prefs = new();
    object _session;

    [OneTimeSetUp]
    public void BorrowTheSession()
    {
        foreach (string key in SavedIntPrefs)
            _prefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;

        var raceWeekend = PlayModeScenes.GameType("RaceWeekend");
        var field = raceWeekend.GetField("Current", Any);
        _session = field.GetValue(null);
    }

    [OneTimeTearDown]
    public void GiveItBack()
    {
        foreach (var pair in _prefs)
        {
            if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
            else PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
        }
        PlayerPrefs.Save();
        PlayModeScenes.GameType("RaceWeekend").GetField("Current", Any).SetValue(null, _session);
    }

    [UnityTest]
    public IEnumerator PracticeOpensWithTheCarInTheBox()
    {
        var raceWeekend = PlayModeScenes.GameType("RaceWeekend");
        var sessionEnum = raceWeekend.GetNestedType("Session");
        raceWeekend.GetField("Current", Any).SetValue(null, System.Enum.Parse(sessionEnum, "Practice"));
        PlayerPrefs.SetInt("raceweekend.sessionlive", 1);
        PlayerPrefs.Save();

        PlayModeScenes.Go(Race);
        yield return PlayModeScenes.WaitForScene(Race);

        var gridSpawner = PlayModeScenes.GameType("GridSpawner");
        var pitLane = PlayModeScenes.GameType("PitLane");
        yield return PlayModeScenes.WaitFor(
            () => (bool)gridSpawner.GetProperty("FieldReady", Any).GetValue(null)
                   && (int)pitLane.GetProperty("PlayerBox", Any).GetValue(null) >= 0,
            "the practice field never arrived or never reserved the player a box", 60f);

        // Give physics a second to have its say about where the car is.
        float until = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < until) yield return new WaitForFixedUpdate();

        var plsType = PlayModeScenes.GameType("PitLaneStart");
        var pls = Object.FindFirstObjectByType(plsType);
        Assert.IsNotNull(pls, "No PitLaneStart in the race scene.");
        var car = (Component)plsType.GetField("car", Any).GetValue(pls);

        var args = new object[] { Vector3.zero, 0f };
        plsType.GetMethod("CurrentBoxPose", Any).Invoke(pls, args);
        var box = (Vector3)args[0];

        float off = Vector2.Distance(car.transform.position, box);
        var body = car.GetComponent<Rigidbody2D>();
        float bodyOff = body != null ? Vector2.Distance(body.position, box) : 0f;
        Debug.Log($"[PracticeCarInBox] box {box} car {car.transform.position} body {(body != null ? body.position : Vector2.zero)} " +
                  $"off {off:0.00} m, body off {bodyOff:0.00} m, interp {(body != null ? body.interpolation.ToString() : "-")}");

        Assert.Less(off, Tolerance, $"The car opened practice {off:0.0} m from its pit box.");
        Assert.Less(bodyOff, Tolerance, $"The car's physics body is {bodyOff:0.0} m from its pit box.");
    }
}
