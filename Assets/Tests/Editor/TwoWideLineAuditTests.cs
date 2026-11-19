using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The two lines a field runs on when it is two-wide, at the three finished tracks, measured as geometry: how far apart
// the two lines are (door to door, for two 2 m cars), and how close each one brings its car to the wall. Where two
// lines come within a car width and a metre of each other, two cars alongside rub doors; where a line runs its car
// close to a barrier, the car outside has nowhere to go. Both were behind the AI crashing two-wide.
//
// Which lines are the two-wide lines:
//   * Texas (FortWorth): the oval lanes the AI races in (TrackBuilder.TryGetOvalLanes, built from the authored leftmost
//     and rightmost lines) and the two start lanes the field runs on through the pace lap and the green.
//   * Daytona: the plate pack's fixed grooves (AIRacingBehaviour.LaneLateral: TrackConditions.AiPackLanes grooves
//     AiLaneSpacing apart round the centreline, each clamped inside the AI's bounds - the authored leftmost and
//     rightmost lines, kept on the road). Two-wide is two neighbouring grooves.
//   * Watkins Glen: the authored leftmost and rightmost lines, kept on the road (SplineDriver.ClampToRoad) - where
//     lineFactor spreads the field and where two cars sit side by side through a corner.
//
// Walls are found the way AIRetirement finds them: TrackEnvironmentBuilder's "Barrier_*" colliders, ray cast across
// the road from the centreline. Where no barrier is found the road edge stands in.
public class TwoWideLineAuditTests
{
    const float CarWidth = 2f;
    // Two cars alongside want a car width and 1.2 m between the doors (OvalLanes.Default.laneSpacing, 3.2 m).
    const float MinCentreGap = 3.2f;
    // A car's side should be at least this far from a barrier face.
    const float MinWallClearance = 1.0f;
    // Where the edge is open (no barrier), a car's side should still be on the road.
    const float MinEdgeClearance = 0.3f;
    const float SampleStep = 2f;

    GameObject _package;

    [TearDown]
    public void Despawn()
    {
        if (_package != null) Object.DestroyImmediate(_package);
        _package = null;
        Runtime("TrackConditions").GetMethod("Reset")?.Invoke(null, null);
    }

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

    // One line pair's measurement, by sample.
    sealed class Pair
    {
        public string name;
        public float[] a, b;          // laterals (m, + right of travel)
        public bool assert = true;
    }

    [TestCase("FortWorth")]
    [TestCase("Daytona")]
    [TestCase("WatkinsGlen")]
    public void TheTwoWideLinesKeepACarWidthApartAndOffTheWalls(string trackId)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab");
        Assert.IsNotNull(prefab, $"track package missing for {trackId}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
        Runtime("TrackConditions").GetMethod("ApplyTrackTuning", new[] { typeof(string) }).Invoke(null, new object[] { trackId });

        var builderType = Runtime("TrackBuilder");
        var builder = (Component)_package.GetComponentInChildren(builderType, true);
        var samples = (IList)builderType.GetMethod("SampleCenterline", Type.EmptyTypes).Invoke(builder, null);
        int n = samples.Count;
        var sType = samples[0].GetType();
        var dist = new float[n]; var width = new float[n]; var pos = new Vector2[n]; var tan = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            dist[i] = (float)sType.GetField("distance").GetValue(samples[i]);
            width[i] = (float)sType.GetField("width").GetValue(samples[i]);
            pos[i] = (Vector2)sType.GetField("position").GetValue(samples[i]);
            tan[i] = ((Vector2)sType.GetField("tangent").GetValue(samples[i])).normalized;
        }
        float lap = dist[n - 1];

        var track = (ScriptableObject)builderType.GetField("track").GetValue(builder);
        var infoType = track.GetType();
        var anchors = infoType.GetMethod("BuildRacingLineAnchors").Invoke(track, null);
        var lateralAt = infoType.GetMethod("GetLateralAt");
        var clamp = Runtime("SplineDriver").GetMethod("ClampToRoad");
        var left = new float[n]; var right = new float[n];
        for (int i = 0; i < n; i++)
        {
            float l = (float)lateralAt.Invoke(track, new object[] { dist[i], -1f, anchors, lap });
            float r = (float)lateralAt.Invoke(track, new object[] { dist[i], +1f, anchors, lap });
            left[i] = (float)clamp.Invoke(null, new object[] { l, width[i] });
            right[i] = (float)clamp.Invoke(null, new object[] { r, width[i] });
        }

        var pairs = new List<Pair>();
        if (trackId == "Daytona")
        {
            var tc = Runtime("TrackConditions");
            int grooves = (int)tc.GetField("AiPackLanes").GetValue(null);
            float spacing = (float)tc.GetField("AiLaneSpacing").GetValue(null);
            Assert.GreaterOrEqual(grooves, 2, "Daytona should race in pack grooves.");
            var g = new float[grooves][];
            float hiG = (grooves - 1) * 0.5f * spacing;
            for (int k = 0; k < grooves; k++)
            {
                g[k] = new float[n];
                float lat = Mathf.Lerp(-hiG, hiG, k / (float)(grooves - 1));
                for (int i = 0; i < n; i++)
                    g[k][i] = Mathf.Clamp(lat, Mathf.Min(left[i], right[i]), Mathf.Max(left[i], right[i]));
            }
            for (int k = 0; k + 1 < grooves; k++)
                pairs.Add(new Pair { name = $"grooves {k}-{k + 1}", a = g[k], b = g[k + 1] });
            pairs.Add(new Pair { name = "authored leftmost/rightmost", a = left, b = right, assert = false });
        }
        else if (trackId == "FortWorth")
        {
            var lanesArgs = new object[] { samples, null, null };
            Assert.IsTrue((bool)builderType.GetMethod("TryGetOvalLanes").Invoke(builder, lanesArgs), "Texas has no lanes.");
            pairs.Add(new Pair { name = "oval lanes", a = (float[])lanesArgs[1], b = (float[])lanesArgs[2] });
            var startArgs = new object[] { samples, null, null };
            Assert.IsTrue((bool)builderType.GetMethod("TryGetOvalStartLanes").Invoke(builder, startArgs), "Texas has no start lanes.");
            pairs.Add(new Pair { name = "start lanes", a = (float[])startArgs[1], b = (float[])startArgs[2] });
            pairs.Add(new Pair { name = "authored leftmost/rightmost", a = left, b = right, assert = false });
        }
        else
        {
            pairs.Add(new Pair { name = "leftmost/rightmost", a = left, b = right });
        }

        // The walls either side of every sample: distance from the centreline to the nearest barrier face (m), or the
        // road edge when there is none within reach.
        Physics2D.SyncTransforms();
        var tf = builder.transform;
        var wallL = new float[n]; var wallR = new float[n]; var wallLFound = new bool[n]; var wallRFound = new bool[n];
        int barrierHits = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 rightDir = new Vector2(tan[i].y, -tan[i].x);
            Vector2 origin = tf.TransformPoint(pos[i]);
            Vector2 rWorld = ((Vector2)tf.TransformDirection(rightDir)).normalized;
            float reach = width[i] * 0.5f + 12f;
            wallR[i] = FindBarrier(origin, rWorld, reach, out wallRFound[i]) ?? width[i] * 0.5f;
            wallL[i] = FindBarrier(origin, -rWorld, reach, out wallLFound[i]) ?? width[i] * 0.5f;
            if (wallRFound[i]) barrierHits++;
            if (wallLFound[i]) barrierHits++;
        }

        var segStart = SegmentStarts(track, out var segLabels);
        var report = new StringBuilder($"[TwoWideAudit] {trackId}: lap {lap:0} m, {n} samples, barrier found on {barrierHits * 50 / n}% of sides");
        int failures = 0;
        foreach (var p in pairs)
        {
            float minGap = float.MaxValue, minGapAt = 0f, minWall = float.MaxValue, minWallAt = 0f;
            var bad = new SortedDictionary<int, string>();
            var badGap = new Dictionary<int, float>(); var badWall = new Dictionary<int, float>();
            var worst = new Dictionary<int, int>();
            float lastD = -1e9f;
            for (int i = 0; i < n; i++)
            {
                if (dist[i] - lastD < SampleStep && i != n - 1) continue;
                lastD = dist[i];
                float gap = Mathf.Abs(p.a[i] - p.b[i]);
                if (gap < minGap) { minGap = gap; minGapAt = dist[i]; }
                // Each car's side against the barrier on its own side of the centreline. Where there is no barrier
                // (an apron, a grass verge) the road edge is open: a car only has to stay on the road
                // (MinEdgeClearance), not a metre off it.
                float wall = float.MaxValue, margin = 0f;
                foreach (var lat in new[] { p.a[i], p.b[i] })
                {
                    bool rightSide = lat >= 0f;
                    float toWall = (rightSide ? wallR[i] - lat : wallL[i] + lat) - CarWidth * 0.5f;
                    bool barrier = rightSide ? wallRFound[i] : wallLFound[i];
                    float m = toWall - (barrier ? MinWallClearance : MinEdgeClearance);
                    margin = Mathf.Min(margin, m);
                    if (barrier) wall = Mathf.Min(wall, toWall);
                }
                if (wall < minWall) { minWall = wall; minWallAt = dist[i]; }
                int seg = SegmentAt(segStart, dist[i]);
                if (gap < MinCentreGap)
                {
                    if (!badGap.ContainsKey(seg) || gap < badGap[seg]) { badGap[seg] = gap; worst[seg] = i; }
                }
                if (margin < 0f)
                {
                    float w = margin;
                    if (!badWall.ContainsKey(seg) || w < badWall[seg]) { badWall[seg] = w; if (!badGap.ContainsKey(seg)) worst[seg] = i; }
                }
            }
            foreach (var kv in badGap) bad[kv.Key] = $"lines {kv.Value:0.00} m apart";
            foreach (var kv in badWall)
                bad[kv.Key] = (bad.TryGetValue(kv.Key, out var s) ? s + ", " : "") + $"{-kv.Value:0.00} m short of the wall/edge clearance";
            foreach (var kv in worst)
            {
                int i = kv.Value;
                bad[kv.Key] += $" [worst at {dist[i]:0} m: a {p.a[i]:0.00} b {p.b[i]:0.00} | wallL {(wallLFound[i] ? "" : "~")}{wallL[i]:0.00}" +
                               $" wallR {(wallRFound[i] ? "" : "~")}{wallR[i]:0.00} half {width[i] * 0.5f:0.00}]";
            }
            report.Append($"\n  {p.name}: closest {minGap:0.00} m apart at {minGapAt:0} m (doors {minGap - CarWidth:0.00} m); " +
                          $"closest to a barrier {minWall:0.00} m at {minWallAt:0} m");
            foreach (var kv in bad)
                report.Append($"\n    seg {kv.Key} '{segLabels[Mathf.Min(kv.Key, segLabels.Length - 1)]}' " +
                              $"({segStart[Mathf.Min(kv.Key, segStart.Length - 1)]:0} m): {kv.Value}");
            if (p.assert && bad.Count > 0) failures++;
        }

        // Per 50 m: both lines and the walls, for the assertable pairs - what a tweak is judged against.
        foreach (var p in pairs)
        {
            if (!p.assert) continue;
            report.Append($"\n  {p.name} by 50 m (a | b | apart | wallL | wallR | width):");
            for (float x = 0f; x < lap; x += 50f)
            {
                int i = IndexAt(dist, x);
                report.Append($"\n    {x,5:0}: {p.a[i],6:0.00} {p.b[i],6:0.00} {Mathf.Abs(p.a[i] - p.b[i]),5:0.00} " +
                              $"{(wallLFound[i] ? "" : "~")}{wallL[i]:0.0} {(wallRFound[i] ? "" : "~")}{wallR[i]:0.0} {width[i]:0.0}");
            }
        }

        // Every sample to Temp/TwoWideAudit_<track>.csv: the walls and every pair, for tuning the lines against.
        var csv = new StringBuilder("distance,width,wallL,barrierL,wallR,barrierR");
        foreach (var p in pairs) csv.Append($",{p.name.Replace(' ', '_')}_a,{p.name.Replace(' ', '_')}_b");
        for (int i = 0; i < n; i++)
        {
            csv.Append($"\n{dist[i]:0.00},{width[i]:0.00},{wallL[i]:0.00},{(wallLFound[i] ? 1 : 0)},{wallR[i]:0.00},{(wallRFound[i] ? 1 : 0)}");
            foreach (var p in pairs) csv.Append($",{p.a[i]:0.000},{p.b[i]:0.000}");
        }
        System.IO.File.WriteAllText($"Temp/TwoWideAudit_{trackId}.csv", csv.ToString());

        Debug.Log(report.ToString());
        Assert.AreEqual(0, failures, report.ToString());
    }

    static float? FindBarrier(Vector2 origin, Vector2 dir, float reach, out bool found)
    {
        found = false;
        var hits = Physics2D.RaycastAll(origin, dir, reach);
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.isTrigger || !IsBarrier(c.transform)) continue;
            found = true;
            return hits[i].distance;
        }
        return null;
    }

    static bool IsBarrier(Transform t)
    {
        for (int depth = 0; t != null && depth < 3; depth++, t = t.parent)
            if (t.name.StartsWith("Barrier")) return true;
        return false;
    }

    static float[] SegmentStarts(ScriptableObject track, out string[] labels)
    {
        var segs = (Array)track.GetType().GetField("segments").GetValue(track);
        var starts = new float[segs.Length];
        labels = new string[segs.Length];
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            var s = segs.GetValue(i);
            starts[i] = cum;
            labels[i] = (string)s.GetType().GetField("label").GetValue(s);
            cum += (float)s.GetType().GetField("length").GetValue(s);
        }
        return starts;
    }

    static int SegmentAt(float[] starts, float d)
    {
        int k = 0;
        while (k + 1 < starts.Length && starts[k + 1] <= d) k++;
        return k;
    }

    static int IndexAt(float[] dist, float x)
    {
        int i = 0;
        while (i + 1 < dist.Length && dist[i + 1] <= x) i++;
        return i;
    }
}
