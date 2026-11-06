using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Texas (FortWorth) pit boxes: a full field's ladder sits on the grey box strip, fills it (no empty grey at the
// entry end) and stays inside the walkable paddock, so the player can reach every crew on foot. The package used
// to fit the boxes back from the exit at the 10 m spacing cap, which left ~60 m of empty strip at the entry end
// and ran the two exit-most boxes ~15 m past the paddock boundary - and past the end of the real stall row.
// The runtime lives in Assembly-CSharp, which a test assembly cannot reference, so it is reached by reflection.
public class TexasPitBoxesFitTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/FortWorth.prefab";
    const int FullField = 44;   // GridSpawner.count 43 + the player's reserved box

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " is missing from Assembly-CSharp.");
        return type;
    }

    GameObject _prefab;
    Component _builder;
    Type _builderType;
    IList _pit;
    float _pitLen;
    object _fit;

    [SetUp]
    public void SetUp()
    {
        _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath);
        Assert.IsNotNull(_prefab, "no Texas package at " + PackagePath);
        _builderType = Runtime("TrackBuilder");
        _builder = _prefab.GetComponentInChildren(_builderType, true);
        Assert.IsNotNull(_builder, "the Texas package has no TrackBuilder");
        _pit = (IList)_builderType.GetMethod("SamplePitCenterline").Invoke(_builder, null);
        Assert.Greater(_pit.Count, 1, "Texas has no pit centreline");
        _pitLen = SampleDistance(_pit[_pit.Count - 1]);
        _fit = Runtime("PitLane").GetMethod("FitBoxes").Invoke(null, new object[] { _builder, _pitLen, FullField });
    }

    static float SampleDistance(object s) => (float)s.GetType().GetField("distance").GetValue(s);

    float FitField(string name) => (float)_fit.GetType().GetField(name).GetValue(_fit);

    float BoxCentre(int idx) =>
        (float)Runtime("PitLane").GetMethod("BoxDistance", new[] { typeof(int), typeof(float), _fit.GetType().MakeByRefType() })
            .Invoke(null, new object[] { idx, _pitLen, _fit });

    float StripFrom => (float)_builderType.GetMethod("PitBoxLaneFrom").Invoke(_builder, new object[] { _pitLen });
    float StripTo => (float)_builderType.GetMethod("PitBoxLaneTo").Invoke(_builder, new object[] { _pitLen });

    // World point on the box lane's centre line (where the car parks) at a distance along pit road.
    Vector2 OnBoxLane(float d)
    {
        var s = _builderType.GetMethod("SamplePitAt").Invoke(_builder, new object[] { d, _pit });
        var st = s.GetType();
        float lateral = (float)_builderType.GetProperty("PitBoxLaneCenterLateral").GetValue(_builder);
        Vector2 local = (Vector2)st.GetField("position").GetValue(s) + (Vector2)st.GetField("normal").GetValue(s) * lateral;
        return _builder.transform.TransformPoint(local);
    }

    [Test]
    public void FullFieldFitsTheBoxStrip()
    {
        float spacing = FitField("spacing");
        float entryMostBack = BoxCentre(FullField - 1) - spacing * 0.5f;
        float exitMostFront = BoxCentre(0) + spacing * 0.5f;

        Assert.GreaterOrEqual(entryMostBack, StripFrom - 0.05f, "the entry-most box hangs off the start of the grey strip");
        Assert.LessOrEqual(exitMostFront, StripTo + 0.05f, "the exit-most box runs past the end of the grey strip");
        Assert.AreEqual(0f, (float)_fit.GetType().GetProperty("Overflow").GetValue(_fit), 0.01f);
        // The boxes fill the strip: no stretch of empty grey left at either end.
        Assert.AreEqual(StripFrom, entryMostBack, 0.5f, "empty strip before the entry-most box");
        Assert.AreEqual(StripTo, exitMostFront, 0.5f, "empty strip after the exit-most box");
        // A box still holds a car with room to work round it.
        Assert.GreaterOrEqual(spacing, 8.5f, "boxes too short for a car and its crew");
    }

    [Test]
    public void EveryBoxIsInsideTheWalkablePaddock()
    {
        var boundaryType = Runtime("PaddockBoundary");
        var boundary = _prefab.GetComponentInChildren(boundaryType, true);
        Assert.IsNotNull(boundary, "the Texas package has no PaddockBoundary");
        var poly = boundary.GetComponent<PolygonCollider2D>();
        var outline = new List<Vector2>();
        foreach (var p in poly.GetPath(0)) outline.Add(boundary.transform.TransformPoint(p + poly.offset));

        float spacing = FitField("spacing");
        // Both ends of every box, nudged half a metre in, so a box straddling the fence fails.
        for (int i = 0; i < FullField; i++)
        {
            float c = BoxCentre(i);
            foreach (float d in new[] { c - spacing * 0.5f + 0.5f, c, c + spacing * 0.5f - 0.5f })
            {
                Vector2 w = OnBoxLane(d);
                Assert.IsTrue(Inside(outline, w), $"box {i} at {d:0.0} m ({w}) is outside the paddock boundary");
            }
        }
    }

    static bool Inside(List<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
}
