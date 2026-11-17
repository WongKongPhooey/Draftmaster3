using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The pit lane is not a loop: a point beyond either end of it, in line with the lane, is not on pit road.
//
// TrackBuilder.IsOnPitSurface measured only how far a point sat sideways of its nearest pit-lane sample. Past the
// end of the lane that nearest sample is the last one, so the infield 50 m beyond Daytona's pit exit read as pit
// road. AIRetirement never retires a car on pit road, so two cars broken down there stayed on the circuit and the
// caution queue, frozen in order behind them, stopped for good. Engine-free of the game assembly: TrackBuilder is
// reached through the package's component by name.
public class PitSurfaceEndsTests
{
    GameObject _package;

    [SetUp]
    public void Spawn()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/Daytona.prefab");
        Assert.IsNotNull(prefab, "Daytona package missing");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    [TearDown]
    public void Despawn()
    {
        if (_package != null) Object.DestroyImmediate(_package);
    }

    [Test]
    public void BeyondTheEndOfThePitLaneIsNotPitRoad()
    {
        var tb = (Component)_package.GetComponentInChildren(System.Type.GetType("TrackBuilder, Assembly-CSharp"), true);
        Assert.IsNotNull(tb, "no TrackBuilder in the package");
        var t = tb.GetType();
        var pit = (System.Collections.IList)t.GetMethod("SamplePitCenterline").Invoke(tb, null);
        Assert.Greater(pit.Count, 2, "Daytona has no pit lane samples");
        var onPit = t.GetMethod("IsOnPitSurface");

        Vector3 World(object sample, float along, float side)
        {
            var st = sample.GetType();
            Vector2 p = (Vector2)st.GetField("position").GetValue(sample);
            Vector2 tan = (Vector2)st.GetField("tangent").GetValue(sample);
            Vector2 nrm = (Vector2)st.GetField("normal").GetValue(sample);
            return tb.transform.TransformPoint(p + tan * along + nrm * side);
        }
        bool On(Vector3 w) => (bool)onPit.Invoke(tb, new object[] { w });

        Assert.IsTrue(On(World(pit[pit.Count / 2], 0f, 0f)), "the middle of the pit lane is pit road");
        Assert.IsFalse(On(World(pit[pit.Count - 1], 50f, -7f)), "50 m past the pit exit is not pit road");
        Assert.IsFalse(On(World(pit[pit.Count - 1], 8f, -2f)), "8 m past the pit exit is not pit road");
        Assert.IsFalse(On(World(pit[0], -50f, 0f)), "50 m before the pit entry is not pit road");
    }
}
