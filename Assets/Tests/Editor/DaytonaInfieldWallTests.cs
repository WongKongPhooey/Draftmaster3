using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Daytona's infield wall three track widths in from the infield edge, the scenery behind it, and the paved back
// straight (Assets/Editor/DaytonaInfieldWall.cs, applied by Author Daytona or the Daytona > Infield Wall + Paved
// Back Straight menu). The checks read what that left in the environment asset and the package prefab.
//
// Assembly-CSharp cannot be referenced from an asmdef, so its types are reached by reflection, as in
// TexasPackageTests.
public class DaytonaInfieldWallTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/Daytona.prefab";
    const string EnvironmentPath = "Assets/Resources/Tracks/DaytonaEnvironment.asset";
    const string GeometryPath = "Assets/Resources/Tracks/Daytona.asset";
    const float TrackWidthsBack = 3f;

    GameObject _contents;

    [TearDown]
    public void TearDown()
    {
        if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
        _contents = null;
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

    static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

    struct Sample { public Vector2 position, normal; public float width, distance; }

    static float RoadWidth()
    {
        var geometry = AssetDatabase.LoadMainAssetAtPath(GeometryPath);
        var segs = (Array)Field(geometry, "segments");
        return (float)Field(segs.GetValue(0), "width");
    }

    static float WallOffset() => TrackWidthsBack * RoadWidth();

    Component Builder()
    {
        _contents = PrefabUtility.LoadPrefabContents(PackagePath);
        var builder = _contents.GetComponentInChildren(Runtime("TrackBuilder"), true);
        Assert.IsNotNull(builder, "Daytona package has no TrackBuilder.");
        return builder;
    }

    static List<Sample> Centerline(Component builder)
    {
        var list = (IList)builder.GetType().GetMethod("SampleCenterline").Invoke(builder, null);
        var samples = new List<Sample>();
        foreach (var s in list)
            samples.Add(new Sample
            {
                position = (Vector2)Field(s, "position"),
                normal = (Vector2)Field(s, "normal"),
                width = (float)Field(s, "width"),
                distance = (float)Field(s, "distance"),
            });
        return samples;
    }

    // How far into the infield (left of travel; sample normals point right) a point lies past the road's edge.
    static float InfieldDepth(List<Sample> samples, Vector2 local)
    {
        float best = float.MaxValue;
        Sample near = samples[0];
        foreach (var s in samples)
        {
            float d = (s.position - local).sqrMagnitude;
            if (d < best) { best = d; near = s; }
        }
        return -Vector2.Dot(local - near.position, near.normal) - near.width * 0.5f;
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    [Test]
    public void InfieldWall_IsOn_ThreeTrackWidthsIn()
    {
        var env = AssetDatabase.LoadMainAssetAtPath(EnvironmentPath);
        Assert.IsNotNull(env, "no DaytonaEnvironment asset");
        Assert.IsTrue((bool)Field(env, "outerSideBarrier"), "Daytona's infield wall (BarrierSide.Outer) is off.");
        Assert.AreEqual(WallOffset(), (float)Field(env, "outerEdgeOffset"), 0.01f,
            "The infield wall should stand three track widths off the infield edge.");
    }

    [Test]
    public void InfieldWallPieces_StandAtTheOffset_AndOffPitRoad()
    {
        var builder = Builder();
        var samples = Centerline(builder);
        var tf = builder.transform;
        float offset = WallOffset();
        int pieces = 0;
        foreach (var t in _contents.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Barrier_Outer")) continue;
            var col = t.GetComponent<PolygonCollider2D>();
            if (col == null) continue;
            pieces++;
            for (int p = 0; p < col.pathCount; p++)
                foreach (var v in col.GetPath(p))
                {
                    Vector2 local = tf.InverseTransformPoint(t.TransformPoint(v));
                    float depth = InfieldDepth(samples, local);
                    Assert.That(depth, Is.InRange(offset - 1f, offset + 1f), $"{t.name} has a point {depth:F1} m into the infield.");
                    Vector3 world = t.TransformPoint(v);
                    Assert.IsFalse((bool)builder.GetType().GetMethod("IsOnPitSurface").Invoke(builder, new object[] { world }),
                        $"{t.name} stands on pit road at {world}.");
                }
        }
        Assert.Greater(pieces, 20, "Expected the infield wall all the way round, cut only where pit road crosses it.");
    }

    [Test]
    public void Scenery_StandsBehindTheInfieldWall()
    {
        var builder = Builder();
        var samples = Centerline(builder);
        var tf = builder.transform;
        float wall = WallOffset();
        int checkedPieces = 0;
        foreach (var piece in _contents.GetComponentsInChildren(Runtime("SceneryPiece"), true))
        {
            Vector2 local = tf.InverseTransformPoint(piece.transform.position);
            float depth = InfieldDepth(samples, local);
            checkedPieces++;
            if (depth <= 0f) continue;   // outside the track
            Assert.Greater(depth, wall + 2f, $"{piece.name} at {piece.transform.position} is {depth:F1} m into the infield - in front of the wall.");
        }
        Assert.Greater(checkedPieces, 0, "Daytona has no scattered scenery to check.");
    }

    [Test]
    public void BackStraightInfield_IsPavedInTheTrackAsphalt()
    {
        var builder = Builder();
        var samples = Centerline(builder);
        var envBuilder = _contents.GetComponentInChildren(Runtime("TrackEnvironmentBuilder"), true);
        var env = Field(envBuilder, "environment");
        object paving = null;
        foreach (var area in (Array)Field(env, "runoffAreas"))
            if ((string)Field(area, "label") == "Back straight infield (paved)") paving = area;
        Assert.IsNotNull(paving, "No back-straight paving run-off area.");
        Assert.AreEqual("TarmacRunoff", Field(paving, "surface").ToString(), "The paving should drive as tarmac.");
        Assert.AreEqual(Field(builder, "surfaceMaterial"), Field(paving, "materialOverride"),
            "The paving should be the racing surface's asphalt.");

        // The back straight is segment 0 (the longest): every point between the road's edge and the wall is paved.
        var segs = (Array)Field(Field(builder, "track"), "segments");
        float length = (float)Field(segs.GetValue(0), "length");
        var poly = (Vector2[])Field(paving, "points");
        float wall = WallOffset();
        foreach (var s in samples)
        {
            if (s.distance < 2f || s.distance > length - 2f) continue;
            for (float d = 0.5f; d < wall; d += 4f)
            {
                Vector2 p = s.position - s.normal * (s.width * 0.5f + d);
                Assert.IsTrue(Inside(poly, p), $"Back straight infield not paved {d:F1} m in at {s.distance:F0} m.");
            }
        }
    }
}
