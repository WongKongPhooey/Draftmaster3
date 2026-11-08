using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// SplineDriver.SwapLaneLine: a switch between oval lanes (Ideal / Inside / Outside) after the first lane-car build
// only swaps the driven line instead of rebuilding the line, bounds, curvature and speed plan. A full rebuild cost
// ~80 ms at Texas, once per lane change per car - the race's recurring profiler spike (2026-11-05). This pins that
// the swap lays exactly what a full build would, and that it stays cheap.
public class OvalLaneSwapTests
{
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

    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, Flags);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    [Test]
    public void ALaneSwapLaysWhatAFullBuildWould()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/FortWorth.prefab");
        Assert.IsNotNull(prefab, "FortWorth package missing");
        var package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var go = new GameObject("LaneSwapProbe");
        try
        {
            var splineType = Runtime("SplineDriver");
            var spline = go.AddComponent(splineType);
            Set(spline, "track", package.GetComponentInChildren(Runtime("TrackBuilder"), true));
            Set(spline, "vehicleInfo", Resources.Load("Vehicles/Cup24"));
            splineType.GetMethod("Rebuild").Invoke(spline, null);

            var lane = splineType.GetField("ovalLane");
            var relay = splineType.GetMethod("RelayLaneNow");
            var buildLat = splineType.GetMethod("BuildLateralProfile", Flags);
            var buildCurv = splineType.GetMethod("BuildCurvatureProfile", Flags);
            var buildSpeed = splineType.GetMethod("BuildSpeedProfile", Flags);
            var trainedProp = splineType.GetProperty("TrainedLineInUse");
            var followingProp = splineType.GetProperty("LaneFollowing");
            string[] arrays = { "_lateralProfile", "_leftBoundProfile", "_rightBoundProfile", "_lineLo", "_lineHi",
                                "_roadLo", "_roadHi", "_escapeLo", "_escapeHi", "_curvatureProfile", "_speedProfile" };

            lane.SetValue(spline, Enum.Parse(lane.FieldType, "Inside"));
            relay.Invoke(spline, null);   // the first lane: a full build
            Assert.IsTrue((bool)followingProp.GetValue(spline), "Texas should give the car its lane.");

            var sw = new Stopwatch();
            double slowestSwapMs = 0;
            foreach (var l in new[] { "Outside", "Ideal", "Inside", "Ideal", "Outside" })
            {
                lane.SetValue(spline, Enum.Parse(lane.FieldType, l));
                sw.Restart();
                relay.Invoke(spline, null);
                sw.Stop();
                slowestSwapMs = Math.Max(slowestSwapMs, sw.Elapsed.TotalMilliseconds);

                var swapped = new Dictionary<string, float[]>();
                foreach (var a in arrays) swapped[a] = (float[])((float[])splineType.GetField(a, Flags).GetValue(spline)).Clone();
                bool swappedTrained = (bool)trainedProp.GetValue(spline);

                Set(spline, "_laneSwapReady", false);   // force the full build for comparison
                buildLat.Invoke(spline, null); buildCurv.Invoke(spline, null); buildSpeed.Invoke(spline, null);
                foreach (var a in arrays)
                {
                    var full = (float[])splineType.GetField(a, Flags).GetValue(spline);
                    Assert.AreEqual(full.Length, swapped[a].Length, $"{l}: {a} length");
                    for (int i = 0; i < full.Length; i++)
                        Assert.AreEqual(full[i], swapped[a][i], 1e-5f, $"{l}: {a}[{i}] differs between the swap and a full build");
                }
                Assert.AreEqual((bool)trainedProp.GetValue(spline), swappedTrained, $"{l}: TrainedLineInUse");
            }
            // ~2.5 ms measured (speed plan), against ~80 ms for a full build; generous for a slow machine.
            Assert.Less(slowestSwapMs, 25.0, $"A lane swap took {slowestSwapMs:F1} ms - it is rebuilding in full again.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(package);
        }
    }
}
