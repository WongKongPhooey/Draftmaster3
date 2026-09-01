using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The man with the stop/go board.
//
// A pit box needs someone holding the lollipop, and the whole value of that person is his TIMING: the board
// has to be down over the box while the car is still coming, because it is what the driver stops against,
// and it has to come back up the moment the crew are off the car, because that is the go signal. A board
// that only arrives once the car has parked, or that stays down after the stop, tells the driver nothing.
//
// These tests pin that sequence, plus the wiring that makes both sides of a stop (the AI's PitStopController
// and the human's PlayerPitService) announce themselves before they arrive. The crew runtime lives in
// Assembly-CSharp, which this assembly cannot reference, so it is reached by reflection — the same approach
// as PitCrewUniformTests.
public class PitCrewSignTests
{
    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    // A sign man on his own GameObject, holding a board renderer at scale 1 so the swing reads as a number.
    class Rig : IDisposable
    {
        public readonly GameObject Root;
        public readonly SpriteRenderer Board;
        public readonly Sprite StopFace, GoFace;
        public readonly Component SignMan, Member;

        public Rig()
        {
            StopFace = Chip("STOP");
            GoFace = Chip("GO");
            Root = new GameObject("SignMan");
            Member = Root.AddComponent(Runtime("PitCrewMember"));
            Assert.IsNotNull(Member, "A sign man is a crew member first — he has to be able to walk out there.");
            SignMan = Root.AddComponent(Runtime("PitCrewSignMan"));
            Assert.IsNotNull(SignMan, "PitCrewSignMan would not attach — check the script name matches the class.");

            var boardGo = new GameObject("Sign");
            boardGo.transform.SetParent(Root.transform, false);
            Board = boardGo.AddComponent<SpriteRenderer>();
            Invoke(SignMan, "Init", Board, StopFace, GoFace);
        }

        public void Step(float dt) => Invoke(SignMan, "Step", dt);
        public void Lower() => Invoke(SignMan, "Lower");
        public void Raise() => Invoke(SignMan, "Raise");
        public bool IsDown => (bool)Property(SignMan, "IsDown");
        public float Down01 => (float)Property(SignMan, "Down01");
        public bool MemberWorking => (bool)Property(Member, "IsWorking");
        public float Reach => Board.transform.localScale.y;

        public float Swing => Knob("swingSeconds");
        public float Hold => Knob("holdAfterRaiseSeconds");
        public float RaisedReach => Knob("raisedReach");

        float Knob(string field)
        {
            var fi = SignMan.GetType().GetField(field);
            Assert.IsNotNull(fi, $"PitCrewSignMan.{field} is gone; this test is written against it.");
            return (float)fi.GetValue(SignMan);
        }

        // Run the swing (and any hold) out in small steps, the way the real component is ticked.
        public void Settle(float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) Step(0.05f);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Root);
            UnityEngine.Object.DestroyImmediate(StopFace);
            UnityEngine.Object.DestroyImmediate(GoFace);
        }
    }

    [Test]
    public void The_board_starts_up_with_the_crew_on_the_wall()
    {
        using var rig = new Rig();
        Assert.IsFalse(rig.IsDown, "Nothing is happening yet, so the board is up.");
        Assert.AreEqual(0f, rig.Down01, 1e-4f);
        Assert.AreSame(rig.GoFace, rig.Board.sprite, "A raised board is the GO face.");
        Assert.IsFalse(rig.MemberWorking, "He is on the wall, not out in the box.");
        Assert.AreEqual(rig.RaisedReach, rig.Reach, 1e-4f,
                        "Seen from above, a board held up is a stub — it must not be drawn at full reach.");
    }

    [Test]
    public void The_board_comes_down_for_a_car_on_its_way_in()
    {
        using var rig = new Rig();
        rig.Lower();

        Assert.IsTrue(rig.IsDown, "The signal changes the instant he is called, not when the swing finishes.");
        Assert.AreSame(rig.StopFace, rig.Board.sprite, "Coming down, the driver has to be reading STOP.");
        Assert.IsTrue(rig.MemberWorking, "He walks out to the box — the board is no use to anybody on the wall.");

        rig.Settle(rig.Swing + 0.1f);
        Assert.AreEqual(1f, rig.Down01, 1e-3f, "The board should have swung all the way out over the car.");
        Assert.AreEqual(1f, rig.Reach, 1e-3f, "Full reach: his hands to over the nose.");
    }

    [Test]
    public void The_board_goes_up_when_the_crew_are_finished()
    {
        using var rig = new Rig();
        rig.Lower();
        rig.Settle(rig.Swing + 0.1f);

        rig.Raise();
        Assert.IsFalse(rig.IsDown);
        Assert.AreSame(rig.GoFace, rig.Board.sprite, "The lift IS the go signal, so it reads GO from the first frame.");
        Assert.IsTrue(rig.MemberWorking, "He holds the board up at the car for a beat before leaving.");

        rig.Settle(rig.Swing + rig.Hold + 0.2f);
        Assert.AreEqual(0f, rig.Down01, 1e-3f);
        Assert.IsFalse(rig.MemberWorking, "Once the board is up and seen, he walks back to the wall.");
    }

    [Test]
    public void A_box_drops_the_board_on_approach_and_lifts_it_when_the_stop_ends()
    {
        using var rig = new Rig();
        var boxGo = new GameObject("PitCrewBox");
        var carGo = new GameObject("Car");
        try
        {
            var box = boxGo.AddComponent(Runtime("PitCrewBox"));
            Invoke(box, "SetSignMan", rig.SignMan);

            Invoke(box, "SignalApproach", carGo.transform);
            Assert.IsTrue((bool)Property(box, "IsSignDown"), "Board down while the car is still coming.");

            Invoke(box, "BeginService", carGo.transform);
            Assert.IsTrue((bool)Property(box, "IsSignDown"), "It stays down for the whole stop.");

            Invoke(box, "EndService");
            Assert.IsFalse((bool)Property(box, "IsSignDown"), "Crew off the car: board up, and the driver goes.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carGo);
            UnityEngine.Object.DestroyImmediate(boxGo);
        }
    }

    [Test]
    public void A_stop_nobody_announced_still_gets_its_board_down()
    {
        using var rig = new Rig();
        var boxGo = new GameObject("PitCrewBox");
        try
        {
            var box = boxGo.AddComponent(Runtime("PitCrewBox"));
            Invoke(box, "SetSignMan", rig.SignMan);

            Invoke(box, "BeginService", (object)null);   // straight to servicing, no approach call
            Assert.IsTrue((bool)Property(box, "IsSignDown"),
                          "A car that crawled in unannounced is still being stopped by that board.");
        }
        finally { UnityEngine.Object.DestroyImmediate(boxGo); }
    }

    [Test]
    public void A_car_that_never_arrives_does_not_strand_him_holding_it_down()
    {
        using var rig = new Rig();
        var boxGo = new GameObject("PitCrewBox");
        try
        {
            var box = boxGo.AddComponent(Runtime("PitCrewBox"));
            Invoke(box, "SetSignMan", rig.SignMan);
            var timeout = box.GetType().GetField("approachTimeout");
            Assert.IsNotNull(timeout, "PitCrewBox.approachTimeout is gone; this test is written against it.");
            timeout.SetValue(box, 0f);   // the car is overdue the moment it is called

            Invoke(box, "SignalApproach", (object)null);
            Assert.IsTrue((bool)Property(box, "IsSignDown"), "A called-in car gets the board down before it arrives.");

            Invoke(box, "Update");
            Assert.IsFalse((bool)Property(box, "IsSignDown"),
                           "Nobody came: the board goes back up rather than leaving a man in the lane holding it.");
        }
        finally { UnityEngine.Object.DestroyImmediate(boxGo); }
    }

    // ---- the wiring -------------------------------------------------------------------------------

    [Test]
    public void Both_kinds_of_driver_call_the_box_before_they_get_there()
    {
        StringAssert.Contains("SignalApproach", Source("Scripts/AI/PitStopController.cs"),
                              "An AI heading for its box has to warn the crew, or the board arrives after it does.");
        StringAssert.Contains("SignalApproach", Source("Scripts/OnFoot/PlayerPitService.cs"),
                              "The human gets the same treatment — that board is the player's stop mark.");
    }

    [Test]
    public void Every_pit_box_is_built_with_a_sign_man()
    {
        string source = Source("Scripts/AI/PitCrewSpawner.cs");
        StringAssert.Contains("PitCrewSignMan", source, "The spawner builds the crew, and he is one of them.");
        StringAssert.Contains("SetSignMan", source, "...and his box has to be told who he is before it can drive him.");
    }

    // ---- plumbing ---------------------------------------------------------------------------------

    static object Invoke(object target, string method, params object[] args)
    {
        var mi = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(mi, $"{target.GetType().Name}.{method} is gone; this test is written against it.");
        return mi.Invoke(target, args);
    }

    static object Property(object target, string name)
    {
        var pi = target.GetType().GetProperty(name);
        Assert.IsNotNull(pi, $"{target.GetType().Name}.{name} is gone; this test is written against it.");
        return pi.GetValue(target);
    }

    static string Source(string relative)
    {
        string path = Path.Combine(Application.dataPath, relative);
        Assert.IsTrue(File.Exists(path), $"{relative} has moved; this test is written against it.");
        return File.ReadAllText(path);
    }

    static Sprite Chip(string name)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.HideAndDontSave };
        var white = new Color32(255, 255, 255, 255);
        tex.SetPixels32(new[] { white, white, white, white });
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 2f);
        sprite.name = name;
        return sprite;
    }
}
