using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// GrassEscapeBand on the real Daytona package: the room beside the road a car may use to get round a wreck that fills
// it. Daytona turns left, so the infield is on the left (negative laterals). The outside is the SAFER wall at the
// road's edge - no room; the infield is open grass (its wall stands three track widths in) except where the pit lane
// runs beside the front stretch.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class GrassEscapeBandTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/Daytona.prefab";
    GameObject _package;

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
    public void Despawn()
    {
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        _package = null;
        Runtime("GrassEscapeBand").GetMethod("ClearCache").Invoke(null, null);
    }

    [Test]
    public void Daytona_InfieldOpen_OutsideWalled_PitLaneKeptClear()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath);
        Assert.IsNotNull(prefab, $"track package missing at {PackagePath}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Physics2D.SyncTransforms();

        var trackType = Runtime("TrackBuilder");
        var bandType = Runtime("GrassEscapeBand");
        var track = _package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "Daytona package has no TrackBuilder.");
        bandType.GetMethod("ClearCache").Invoke(null, null);
        var samples = (IList)trackType.GetMethod("SampleCenterline").Invoke(track, null);
        const float metres = 15f;
        var band = bandType.GetMethod("For").Invoke(null, new object[] { track, samples, metres });
        Assert.IsNotNull(band, "no band built");
        var lo = (float[])bandType.GetField("Lo").GetValue(band);
        var hi = (float[])bandType.GetField("Hi").GetValue(band);
        Assert.AreEqual(samples.Count, lo.Length);

        var inside = new List<float>();
        var outside = new List<float>();
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            float half = 0.5f * (float)s.GetType().GetField("width").GetValue(s);
            inside.Add(-lo[i] - half);
            outside.Add(hi[i] - half);
        }
        float Median(List<float> v) { var c = new List<float>(v); c.Sort(); return c[c.Count / 2]; }
        float Share(List<float> v, Func<float, bool> f) { int k = 0; foreach (var x in v) if (f(x)) k++; return (float)k / v.Count; }
        float insideOpen = Share(inside, x => x >= metres - 0.01f);
        float insideShut = Share(inside, x => x < 1f);
        string report = $"inside median {Median(inside):0.0} m, open {insideOpen:P0}, shut {insideShut:P0}; " +
                        $"outside median {Median(outside):0.0} m, max {Mathf.Max(outside.ToArray()):0.0} m";
        Debug.Log("[GrassBand] Daytona " + report);

        Assert.Less(Median(outside), 1f, "the outside of Daytona is the wall at the road's edge. " + report);
        Assert.Greater(insideOpen, 0.6f, "most of the infield should be open to a car going round a wreck. " + report);
        Assert.Greater(insideShut, 0.02f, "the pit lane beside the front stretch should shut the infield there. " + report);
    }
}
