using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the in-car track map following the road the race is on. The bug: the map baked
// whatever TrackBuilder it found first and never looked again while that builder lived, so every track
// showed Watkins Glen. MiniMapBinding.NeedsRebake is the decision the map's poll makes each second; plain
// GameObjects and ScriptableObjects stand in for the TrackBuilders and TrackInfoV2s it is handed.
public class TrackMiniMapTests
{
    GameObject _roadA, _roadB;
    ScriptableObject _glen, _daytona;

    [SetUp]
    public void SetUp()
    {
        _roadA = new GameObject("RoadA");
        _roadB = new GameObject("RoadB");
        _glen = ScriptableObject.CreateInstance<ScriptableObject>();
        _daytona = ScriptableObject.CreateInstance<ScriptableObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_roadA != null) Object.DestroyImmediate(_roadA);
        if (_roadB != null) Object.DestroyImmediate(_roadB);
        Object.DestroyImmediate(_glen);
        Object.DestroyImmediate(_daytona);
    }

    [Test]
    public void SameRoadSameGeometry_KeepsMap()
    {
        Assert.IsFalse(MiniMapBinding.NeedsRebake(_roadA, _glen, _roadA, _glen, haveMap: true));
    }

    [Test]
    public void DifferentRoad_Rebakes()
    {
        Assert.IsTrue(MiniMapBinding.NeedsRebake(_roadA, _glen, _roadB, _daytona, haveMap: true));
    }

    [Test]
    public void SameRoadNewGeometry_Rebakes()
    {
        Assert.IsTrue(MiniMapBinding.NeedsRebake(_roadA, _glen, _roadA, _daytona, haveMap: true));
    }

    [Test]
    public void NoMapYet_BakesOnceGeometryArrives()
    {
        Assert.IsFalse(MiniMapBinding.NeedsRebake(_roadA, null, _roadA, null, haveMap: false));
        Assert.IsTrue(MiniMapBinding.NeedsRebake(_roadA, null, _roadA, _daytona, haveMap: false));
    }

    [Test]
    public void RoadGone_WaitsForTheNextOne()
    {
        var gone = _roadA;
        Object.DestroyImmediate(_roadA);
        // The destroyed road now compares equal to null: nothing to redraw until a new road turns up.
        Assert.IsFalse(MiniMapBinding.NeedsRebake(gone, _glen, null, null, haveMap: true));
        Assert.IsTrue(MiniMapBinding.NeedsRebake(gone, _glen, _roadB, _daytona, haveMap: true));
    }
}
