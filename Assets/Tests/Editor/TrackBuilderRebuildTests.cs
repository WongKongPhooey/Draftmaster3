using System;
using NUnit.Framework;
using UnityEngine;

// TrackBuilder rebuilds its generated pieces (pit lane, box lines, brake boards) by tearing the old ones down and
// building new ones. It used to tear down only the first child of each name, so a stray copy - a package's own saved
// piece it could not remove, or a second build in one frame - stayed and a new one went in beside it on every
// rebuild: Texas's RaceScene copy had collected eight pit lanes, and in play the old box lines and the new ones both
// showed. Every copy goes now.
public class TrackBuilderRebuildTests
{
    GameObject _go;

    static Type Runtime(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        Assert.Fail($"{name} is missing from Assembly-CSharp.");
        return null;
    }

    [TearDown]
    public void Clean()
    {
        if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
    }

    static int Count(Transform t, string name)
    {
        int n = 0;
        for (int i = 0; i < t.childCount; i++) if (t.GetChild(i).name == name) n++;
        return n;
    }

    [Test]
    public void RebuildingLeavesOnePitLaneAndOneSetOfBrakeBoards()
    {
        var info = Resources.Load("Tracks/FortWorth");
        Assert.IsNotNull(info, "Resources/Tracks/FortWorth is missing.");
        var type = Runtime("TrackBuilder");
        _go = new GameObject("RebuildTrack");
        var builder = (Component)_go.AddComponent(type);
        type.GetField("track").SetValue(builder, info);

        // Stale copies left over from earlier builds, as the scene had them.
        for (int i = 0; i < 3; i++)
        {
            new GameObject("PitLane").transform.SetParent(_go.transform, false);
            new GameObject("BrakeMarkers").transform.SetParent(_go.transform, false);
        }

        var build = type.GetMethod("Build");
        for (int i = 0; i < 3; i++) build.Invoke(builder, null);

        Assert.AreEqual(1, Count(_go.transform, "PitLane"), "Pit lanes after three rebuilds.");
        Assert.AreEqual(1, Count(_go.transform, "BrakeMarkers"), "Brake board sets after three rebuilds.");
        var pit = _go.transform.Find("PitLane");
        Assert.AreEqual(1, Count(pit, "PitBoxLines"), "Box line sets under the pit lane.");
    }
}
