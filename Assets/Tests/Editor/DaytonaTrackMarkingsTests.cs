using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Daytona's paint and paving (Assets/Editor/DaytonaTrackMarkings.cs + DaytonaInfieldWall.cs, applied by Author
// Daytona or Daytona > Apron Lines, Wall Paving + Tri-oval Crowd): the double yellow at the bottom of the track, the
// white line at the apron's grass edge, the infield paved from the end of turn 3 to pit entry, asphalt under the
// outside wall, and the tri-oval crowd pulled in, twice as deep, with asphalt in front of it. The checks read what
// that left in the environment asset and the package prefab.
//
// Assembly-CSharp / Assembly-CSharp-Editor cannot be referenced from an asmdef, so their types are reached by
// reflection, as in DaytonaInfieldWallTests.
public class DaytonaTrackMarkingsTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/Daytona.prefab";
    const string EnvironmentPath = "Assets/Resources/Tracks/DaytonaEnvironment.asset";
    const string GeometryPath = "Assets/Resources/Tracks/Daytona.asset";
    const float ApronMetres = 3.5f;
    const float StandSetback = 5f;
    const float StandDepth = 28f;

    GameObject _contents;

    [TearDown]
    public void TearDown()
    {
        if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
        _contents = null;
    }

    static Type TypeIn(string assembly, string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != assembly) continue;
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        Assert.Fail($"{name} is missing from {assembly}.");
        return null;
    }

    static Type Runtime(string name) => TypeIn("Assembly-CSharp", name);

    static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

    struct Sample { public Vector2 position, normal; public float width, distance; }

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

    static Sample Nearest(List<Sample> samples, Vector2 local)
    {
        float best = float.MaxValue;
        Sample near = samples[0];
        foreach (var s in samples)
        {
            float d = (s.position - local).sqrMagnitude;
            if (d < best) { best = d; near = s; }
        }
        return near;
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

    static object Strip(object env, string label)
    {
        object found = null;
        foreach (var s in (Array)Field(env, "strips"))
            if ((string)Field(s, "label") == label)
            {
                Assert.IsNull(found, $"Two strips labelled '{label}'.");
                found = s;
            }
        Assert.IsNotNull(found, $"No strip labelled '{label}'.");
        return found;
    }

    [Test]
    public void BottomOfTrack_IsADoubleYellow_AndTheWhiteLineIsAtTheApronsGrassEdge()
    {
        var env = AssetDatabase.LoadMainAssetAtPath(EnvironmentPath);
        var geometry = AssetDatabase.LoadMainAssetAtPath(GeometryPath);
        Assert.IsFalse((bool)Field(geometry, "drawLeftEdgeLine"), "TrackBuilder's own white infield edge line should be off.");

        var inner = Strip(env, "Yellow line (track side)");
        var outer = Strip(env, "Yellow line (apron side)");
        foreach (var line in new[] { inner, outer })
        {
            Assert.AreEqual("LeftEdge", Field(line, "anchor").ToString());
            var mat = (Material)Field(line, "material");
            Assert.IsNotNull(mat, "Yellow line has no material.");
            Color c = mat.color;
            Assert.IsTrue(c.r > 0.6f && c.g > 0.5f && c.b < 0.4f, $"Yellow line material {mat.name} is not yellow: {c}.");
            Assert.That((float)Field(line, "width"), Is.InRange(0.05f, 0.25f), "Yellow lines should be thin.");
        }
        float a = (float)Field(inner, "lateralOffset"), b = (float)Field(outer, "lateralOffset");
        float w = (float)Field(inner, "width");
        Assert.Greater(a, 0f, "One yellow line on the racing surface side of the edge.");
        Assert.Less(b, 0f, "One on the apron side.");
        Assert.Greater(a - b, w * 1.5f, "The two yellow lines should have a gap between them.");

        var white = Strip(env, "White line (apron to grass)");
        Assert.AreEqual("LeftEdge", Field(white, "anchor").ToString());
        float outerEdge = (float)Field(white, "lateralOffset") - (float)Field(white, "width") * 0.5f;
        Assert.AreEqual(-ApronMetres, outerEdge, 0.01f, "The white line's outer edge should sit on the apron's grass edge.");

        var builder = Builder();
        foreach (Transform child in builder.transform)
            Assert.AreNotEqual("LeftEdgeLine", child.name, "The package still carries TrackBuilder's white infield edge line.");
    }

    [Test]
    public void OutsideWall_StandsOnAsphalt()
    {
        var builder = Builder();
        var env = AssetDatabase.LoadMainAssetAtPath(EnvironmentPath);
        var paving = Strip(env, "Outside wall (paved)");
        Assert.AreEqual("RightEdge", Field(paving, "anchor").ToString(), "Right of travel is the outside on Daytona.");
        Assert.AreEqual(Field(builder, "surfaceMaterial"), Field(paving, "material"), "Should be the racing surface's asphalt.");
        float width = (float)Field(paving, "width");
        Assert.AreEqual(width * 0.5f, (float)Field(paving, "lateralOffset"), 0.01f, "Should start at the road's edge.");

        // From the road's edge out past the back of the SAFER barrier and its fence.
        float wallLine = (float)Field(env, "innerEdgeOffset");
        float reach = 0f;
        var style = Runtime("SaferBarrierStyle").GetProperty("Default").GetValue(null);
        foreach (var layer in (IEnumerable)style.GetType().GetMethod("Layers").Invoke(style, null))
            reach = Mathf.Max(reach, (float)Field(layer, "to"));
        Assert.Greater(width, wallLine - 0.5f + reach, "The paving stops short of the back of the outside wall.");

        // Every lap: whole-lap span.
        var segs = (Array)Field(Field(builder, "track"), "segments");
        Assert.AreEqual(0, (int)Field(paving, "startSegmentIndex"));
        Assert.AreEqual(segs.Length - 1, (int)Field(paving, "endSegmentIndex"));
    }

    [Test]
    public void TurnFourInfield_IsPavedFromTheEndOfTurnThreeToPitEntry()
    {
        var builder = Builder();
        var samples = Centerline(builder);
        var env = AssetDatabase.LoadMainAssetAtPath(EnvironmentPath);
        object paving = null;
        foreach (var area in (Array)Field(env, "runoffAreas"))
            if ((string)Field(area, "label") == "Turn 3 to pit entry infield (paved)") paving = area;
        Assert.IsNotNull(paving, "No turn 3 to pit entry paving.");
        Assert.AreEqual("TarmacRunoff", Field(paving, "surface").ToString());
        Assert.AreEqual(Field(builder, "surfaceMaterial"), Field(paving, "materialOverride"));

        var wallType = TypeIn("Assembly-CSharp-Editor", "DaytonaInfieldWall");
        var track = Field(builder, "track");
        int back = (int)wallType.GetMethod("LongestStraight").Invoke(null, new[] { track });
        var args = new object[] { builder, back, 0f, 0f };
        Assert.IsTrue((bool)wallType.GetMethod("TryTurnFourSpan").Invoke(null, args), "No turn 3 to pit entry span.");
        float from = (float)args[2], to = (float)args[3];
        float pitEntry = (float)track.GetType().GetProperty("PitEntryDistanceOnLap").GetValue(track);
        Assert.AreEqual(pitEntry, to, 0.01f, "The paving should run to pit entry.");

        // Turns 3 and 4 are one corner of ~160 degrees after the back straight: the end of turn 3 is well into it.
        float straightEnd = 0f;
        var segs = (Array)Field(track, "segments");
        for (int i = 0; i <= back; i++) straightEnd += (float)Field(segs.GetValue(i), "length");
        Assert.That(from, Is.InRange(straightEnd + 200f, to - 300f), "The end of turn 3 should sit mid-corner.");

        float wall = (float)Field(env, "outerEdgeOffset");
        var poly = (Vector2[])Field(paving, "points");
        int checkedPoints = 0;
        foreach (var s in samples)
        {
            if (s.distance < from + 2f || s.distance > to - 2f) continue;
            for (float d = 0.5f; d < wall; d += 4f)
            {
                Vector2 p = s.position - s.normal * (s.width * 0.5f + d);
                Assert.IsTrue(Inside(poly, p), $"Turn 4 infield not paved {d:F1} m in at {s.distance:F0} m.");
                checkedPoints++;
            }
        }
        Assert.Greater(checkedPoints, 100);
    }

    [Test]
    public void TriOvalCrowd_IsCloserDeeper_AndFrontedByAsphalt()
    {
        var builder = Builder();
        var samples = Centerline(builder);
        var tf = builder.transform;
        var standType = Runtime("Grandstand");
        int joined = 0;
        foreach (var stand in _contents.GetComponentsInChildren(standType, true))
        {
            // The back straight's separate blocks keep the standard 14 m; only the joined tri-oval stand is 28.
            if (Mathf.Abs((float)Field(stand, "depth") - StandDepth) > 0.01f) continue;
            joined++;
            // The joined stand's origin is the middle of its arc: the road's edge + setback + half the depth.
            Vector2 local = tf.InverseTransformPoint(stand.transform.position);
            var near = Nearest(samples, local);
            float outside = Vector2.Dot(local - near.position, near.normal) - near.width * 0.5f;
            Assert.AreEqual(StandSetback + StandDepth * 0.5f, outside, 1.5f,
                $"{stand.name}'s middle is {outside:F1} m off the road's outside edge.");
        }
        Assert.Greater(joined, 8, "Expected the tri-oval stand in pieces, 28 m deep.");

        var env = AssetDatabase.LoadMainAssetAtPath(EnvironmentPath);
        var frontage = Strip(env, "Tri-oval frontage (paved)");
        Assert.AreEqual("RightEdge", Field(frontage, "anchor").ToString());
        float width = (float)Field(frontage, "width");
        Assert.AreEqual(width * 0.5f, (float)Field(frontage, "lateralOffset"), 0.01f, "Should start at the road's edge.");
        Assert.GreaterOrEqual(width, StandSetback, "The asphalt should reach the stand's front row.");
        Assert.AreEqual(Field(builder, "surfaceMaterial"), Field(frontage, "material"));
    }
}
