using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The formation lap runs two-wide: grid slots 2k and 2k+1 share a row, side by side. PaceLapAssist held the player
// behind the car directly ahead in GRID order, which for the right-hand column is the row partner alongside - drawn
// level with it the gap jittered either side of zero and the "out of position" box flashed with the player exactly
// in their slot. Under Auto it flashed too: the AI drives the car on its own SplineDriver, the controller stops
// projecting itself onto the track, and the hold read a distance frozen where Auto took over.
public class PaceLapAssistTwoWideTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, All);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static object Get(object target, string field)
    {
        var f = target.GetType().GetField(field, All);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        return f.GetValue(target);
    }

    static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, All, null, Type.EmptyTypes, null) ?? target.GetType().GetMethod(method, All);
        Assert.IsNotNull(m, $"{target.GetType().Name}.{method} is gone.");
        m.Invoke(target, args);
    }

    static void SetPhase(string phase)
    {
        var raceStart = Runtime("RaceStart");
        var phaseType = raceStart.GetNestedType("Phase");
        raceStart.GetProperty("Current").SetValue(null, Enum.Parse(phaseType, phase));
    }

    GameObject _pkg;
    Component _track;
    UnityEngine.Object _vehicleInfo;
    readonly List<GameObject> _cars = new();

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/Daytona.prefab");
        Assert.IsNotNull(prefab, "no Daytona package");
        _pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        _track = (Component)_pkg.GetComponentInChildren(Runtime("TrackBuilder"), true);
        _vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(_vehicleInfo);
        SetPhase("Formation");
    }

    [TearDown]
    public void TearDown()
    {
        // Edit mode never calls OnDisable on these: take the cars out of the static field lists by hand.
        foreach (var go in _cars)
        {
            if (go == null) continue;
            var sd = go.GetComponent(Runtime("SplineDriver"));
            if (sd != null) Call(sd, "OnDisable");
            var pvc = go.GetComponent(Runtime("PlayerVehicleController"));
            if (pvc != null) Call(pvc, "OnDisable");
            UnityEngine.Object.DestroyImmediate(go);
        }
        _cars.Clear();
        UnityEngine.Object.DestroyImmediate(_pkg);
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
    }

    // An AI car on the main track at `distance`, in grid slot `slot`, at formation pace.
    Component AddAI(string name, int slot, float distance, GameObject onto = null)
    {
        var go = onto != null ? onto : new GameObject(name);
        if (onto == null) _cars.Add(go);
        var spline = go.AddComponent(Runtime("SplineDriver"));
        Set(spline, "track", _track);
        Set(spline, "vehicleInfo", _vehicleInfo);
        Set(spline, "startDistance", distance);
        Set(spline, "speed", 60f / 2.237f);
        Set(spline, "qualifyingPosition", slot);
        Call(spline, "Awake");
        Call(spline, "OnEnable");
        Call(spline, "Start");
        Assert.IsFalse((bool)spline.GetType().GetProperty("IsOnPit").GetValue(spline), $"{name} is on pit road.");
        return spline;
    }

    Vector3 MainTrackPoint(float distance)
    {
        var trackType = _track.GetType();
        var samples = trackType.GetMethod("SampleCenterline").Invoke(_track, null);
        var s = trackType.GetMethod("SampleAt").Invoke(_track, new object[] { distance, samples });
        Vector2 local = (Vector2)s.GetType().GetField("position").GetValue(s);
        return _track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
    }

    // The player's car in grid slot `slot`, `distance` along the main track; its controller believes it is at
    // `controllerDistance` (where it last projected itself).
    (GameObject go, Component pvc) AddPlayer(int slot, float distance, float controllerDistance)
    {
        var go = new GameObject("Player");
        _cars.Add(go);
        go.transform.position = MainTrackPoint(distance);
        var pvc = go.AddComponent(Runtime("PlayerVehicleController"));
        Set(pvc, "vehicleInfo", _vehicleInfo);
        Set(pvc, "track", _track);
        Call(pvc, "SetFormationGrid", slot);
        Set(pvc, "<TrackDistance>k__BackingField", controllerDistance);
        return (go, pvc);
    }

    Component Assist(GameObject player, Component pvc)
    {
        var assist = player.AddComponent(Runtime("PaceLapAssist"));
        Set(assist, "pvc", pvc);
        Call(assist, "Update");
        return assist;
    }

    // Right-hand column (slot 3), drawn level with the row partner (slot 2) a couple of metres up the road, the car
    // a row ahead in the same column (slot 1) 12 m in front: in position, held behind slot 1, not slot 2.
    [TestCase(2.5f, TestName = "RightColumn_PartnerAlongsideJustAhead_IsInPosition")]
    [TestCase(-3f, TestName = "RightColumn_PartnerAlongsideJustBehind_IsInPosition")]
    public void TwoWide_RowPartnerAlongside_IsNotTheCarToFollow(float partnerAhead)
    {
        var rowAhead = AddAI("RowAhead", 1, 1012f);
        AddAI("Partner", 2, 1000f + partnerAhead);
        var (player, pvc) = AddPlayer(3, 1000f, 1000f);

        var assist = Assist(player, pvc);

        Assert.IsFalse((bool)Get(assist, "_outOfPosition"),
            $"Level with the row partner, the player was told: {Get(assist, "_promptText")}");
        Assert.AreSame(rowAhead.transform, Get(assist, "_leader"), "The player is not held behind the car a row ahead in their column.");
        float governorMph = (float)pvc.GetType().GetField("speedGovernorMps").GetValue(pvc) * 2.237f;
        Assert.That(governorMph, Is.GreaterThan(55f), $"The hold capped the player at {governorMph:0} mph off the car alongside.");
    }

    // Under Auto the car carries an enabled SplineDriver (left at grid 0, so it is in the formation order as a
    // phantom slot-0 car) and its controller's track distance is frozen where Auto took over - here two and a half
    // kilometres of lap ago. In its slot (2, left column) 12 m behind slot 0 with slot 3 alongside, it is in position.
    [Test]
    public void Auto_StaleControllerDistance_AndOwnSpline_DoNotReadOutOfPosition()
    {
        var (player, pvc) = AddPlayer(2, 1000f, 2523f);
        var ownSpline = AddAI("Player", 0, 1000f, player); // registered first: the phantom slot-0 car comes up first
        var rowAhead = AddAI("RowAhead", 0, 1012f);
        AddAI("Partner", 3, 1001f);
        Assert.IsTrue(((Behaviour)ownSpline).isActiveAndEnabled);

        var assist = Assist(player, pvc);

        Assert.IsFalse((bool)Get(assist, "_outOfPosition"),
            $"In Auto with the formation right, the player was told: {Get(assist, "_promptText")}");
        Assert.AreSame(rowAhead.transform, Get(assist, "_leader"), "Under Auto the hold did not follow the car a row ahead.");
    }
}
