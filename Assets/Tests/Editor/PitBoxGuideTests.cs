using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// The "YOUR BOX" objective marker hung on the player's pit box while they drive down pit road.
//
// The runtime lives in Assembly-CSharp, which a test assembly cannot reference, so it is reached by
// reflection (as TowToPitsTests does).
public class PitBoxGuideTests
{
    const float HalfLength = 4.25f;

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " is missing from Assembly-CSharp.");
        return type;
    }

    static bool BoxAhead(Vector3 car, Vector3 box, Vector3 along) =>
        (bool)Runtime("PlayerPitBoxMarker").GetMethod("BoxAhead")
            .Invoke(null, new object[] { car, box, along, HalfLength });

    [Test]
    public void BoxAhead_TrueOnApproach_FalseOncePastTheFarEnd()
    {
        var box = new Vector3(100f, 20f, 0f);
        var along = Vector3.right;

        Assert.IsTrue(BoxAhead(new Vector3(0f, 20f, 0f), box, along), "coming down the lane");
        Assert.IsTrue(BoxAhead(new Vector3(100f, 20f, 0f), box, along), "sat in the box");
        Assert.IsTrue(BoxAhead(new Vector3(103f, 22f, 0f), box, along), "overshot inside the box");
        Assert.IsFalse(BoxAhead(new Vector3(110f, 20f, 0f), box, along), "pulled away");
    }

    [Test]
    public void BoxAhead_FollowsTheLaneDirection_NotWorldAxes()
    {
        var along = new Vector3(0f, -1f, 0f);   // lane running down the screen
        Assert.IsTrue(BoxAhead(new Vector3(0f, 50f, 0f), Vector3.zero, along));
        Assert.IsFalse(BoxAhead(new Vector3(0f, -50f, 0f), Vector3.zero, along));
    }

    [Test]
    public void MarkerMeasuredFromTheCar_RegistersAndClears()
    {
        var ui = Runtime("SpawnIntroUI");
        var marker = Runtime("PlayerPitBoxMarker");
        var instance = ui.GetProperty("Instance");
        bool had = (Object)instance.GetValue(null) != null;

        var target = new GameObject("BoxTarget").transform;
        var car = new GameObject("Car").transform;
        try
        {
            var a = ui.GetMethod("Ensure").Invoke(null, null);
            Assert.AreSame(a, ui.GetMethod("Ensure").Invoke(null, null), "one marker UI, not one per call");

            string label = (string)marker.GetField("GuideLabel").GetValue(null);
            int priority = (int)marker.GetField("GuidePriority").GetValue(null);
            ui.GetMethod("AddMarker").Invoke(a, new object[] { target, null, 2.5f, label, priority, car });
            Assert.IsTrue((bool)ui.GetMethod("HasMarker").Invoke(a, new object[] { target }));

            ui.GetMethod("RemoveMarker").Invoke(a, new object[] { target });
            Assert.IsFalse((bool)ui.GetMethod("HasMarker").Invoke(a, new object[] { target }));
        }
        finally
        {
            var made = (Component)instance.GetValue(null);
            if (!had && made != null) Object.DestroyImmediate(made.gameObject);
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(car.gameObject);
        }
    }
}
