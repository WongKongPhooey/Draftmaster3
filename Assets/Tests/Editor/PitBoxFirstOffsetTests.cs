using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// TrackBuilder.pitBoxFirstOffset: the pit boxes placed from the pit entry line instead of fitted back from
// the exit. The runtime lives in Assembly-CSharp, which a test assembly cannot reference, so it is reached
// by reflection (as PitBoxGuideTests does).
public class PitBoxFirstOffsetTests
{
    const float PitLength = 600f;
    const float BoxLaneEndOffset = 50f;

    GameObject _go;
    ScriptableObject _info;
    Component _builder;

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " is missing from Assembly-CSharp.");
        return type;
    }

    [SetUp]
    public void SetUp()
    {
        var infoType = Runtime("TrackInfoV2");
        _info = ScriptableObject.CreateInstance(infoType);
        infoType.GetField("hasPitLane").SetValue(_info, true);
        infoType.GetField("pitDefaultWidth").SetValue(_info, 10f);

        // One straight pit segment of PitLength metres.
        var segType = infoType.GetNestedType("TrackSegment");
        var segs = Array.CreateInstance(segType, 1);
        object seg = Activator.CreateInstance(segType);
        segType.GetField("length").SetValue(seg, PitLength);
        segs.SetValue(seg, 0);
        infoType.GetField("pitSegments").SetValue(_info, segs);

        // Inactive, so the [ExecuteAlways] builder never runs a Build.
        _go = new GameObject("PitBoxFirstOffsetTests");
        _go.SetActive(false);
        var builderType = Runtime("TrackBuilder");
        _builder = _go.AddComponent(builderType);
        builderType.GetField("track").SetValue(_builder, _info);
        builderType.GetField("pitBoxLaneEndOffset").SetValue(_builder, BoxLaneEndOffset);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        Object.DestroyImmediate(_info);
    }

    void Set(string field, float value) => _builder.GetType().GetField(field).SetValue(_builder, value);
    void SetEntryLine(float d) => _info.GetType().GetField("pitEntryLineDistance").SetValue(_info, d);

    object Fit(int boxes) =>
        Runtime("PitLane").GetMethod("FitBoxes").Invoke(null, new object[] { _builder, PitLength, boxes });

    static float F(object fit, string field) => (float)fit.GetType().GetField(field).GetValue(fit);

    static float BoxCentre(object fit, int idx) =>
        (float)Runtime("PitLane").GetMethod("BoxDistance", new[] { typeof(int), typeof(float), fit.GetType().MakeByRefType() })
            .Invoke(null, new object[] { idx, PitLength, fit });

    [Test]
    public void EntryMostBoxStartsAtEntryLinePlusOffset()
    {
        SetEntryLine(80f);
        Set("pitBoxFirstOffset", 20f);
        const int boxes = 10;
        var fit = Fit(boxes);

        float spacing = F(fit, "spacing");
        float entryMostBackLine = BoxCentre(fit, boxes - 1) - spacing * 0.5f;
        Assert.AreEqual(100f, entryMostBackLine, 0.01f, "the first box's back line should sit on entry line + offset");
        Assert.AreEqual(10f, spacing, 0.01f, "a long strip spaces the boxes at MaxSpacing");
        Assert.Greater(BoxCentre(fit, 0), BoxCentre(fit, boxes - 1), "box 0 stays nearest the exit");
    }

    [Test]
    public void BoxesFillTheStripFromTheFirstBoxToItsEnd()
    {
        SetEntryLine(0f);
        Set("pitBoxFirstOffset", 50f);
        const int boxes = 60;   // 500 m strip / 60 = 8.33 m, inside the clamp
        var fit = Fit(boxes);

        float spacing = F(fit, "spacing");
        Assert.AreEqual(500f / boxes, spacing, 0.01f);
        Assert.AreEqual(PitLength - BoxLaneEndOffset, BoxCentre(fit, 0) + spacing * 0.5f, 0.01f,
            "box 0's front line should land on the strip end");
        Assert.AreEqual(0f, (float)fit.GetType().GetProperty("Overflow").GetValue(fit), 0.01f);
    }

    [Test]
    public void StripStartFollowsTheFirstBox_AndNegativeOffsetLeavesTheOldFit()
    {
        var builderType = _builder.GetType();
        var from = builderType.GetMethod("PitBoxLaneFrom");
        Set("pitBoxLaneStartOffset", 200f);
        SetEntryLine(30f);

        Set("pitBoxFirstOffset", 15f);
        Assert.AreEqual(45f, (float)from.Invoke(_builder, new object[] { PitLength }), 0.01f);

        Set("pitBoxFirstOffset", -1f);
        Assert.AreEqual(200f, (float)from.Invoke(_builder, new object[] { PitLength }), 0.01f,
            "unset, the strip starts at pitBoxLaneStartOffset as before");
    }
}
