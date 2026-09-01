using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// A NASCAR stop is worked one side of the car at a time. The crew go over the wall onto the RIGHT-hand
// corners, two men to a wheel, get that pair on, and only then run round the car — past the nose or the
// tail — for the left-hand pair. The fueller never moves: the filler is on the left rear.
//
// The crew used to appear at all four corners at once, which is not a pit stop anybody would recognise.
// These tests pin the order, the hand-off between the two sides, and the backstops around it (a stop cut
// short, and a man who never reaches his corner).
//
// The crew runtime lives in Assembly-CSharp, which this assembly cannot reference, so it is reached by
// reflection — the same approach as PitCrewSignTests.
public class PitCrewSideOrderTests
{
    const float WheelLateral = 1.2f;
    const float WheelLongitudinal = 1.8f;
    const float Tolerance = 0.05f;

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    // A box with its five members and a car parked square in it.
    //
    // The box sits at the origin unrotated, so box-local IS world: +Y runs up the lane and +X is across it.
    // The car is turned a quarter turn so its forward (its local +X) points up the lane — which puts its
    // RIGHT-hand side on box +X and its left on box -X, the same way round as a car parked in a real box.
    class Rig : IDisposable
    {
        public readonly GameObject BoxGo, CarGo;
        public readonly Component Box;
        public readonly Component[] Members = new Component[5];
        public readonly SpriteRenderer[] Gear = new SpriteRenderer[5];

        public Rig(float carrierOffset = 0.7f)
        {
            BoxGo = new GameObject("PitCrewBox");
            Box = BoxGo.AddComponent(Runtime("PitCrewBox"));
            SetField(Box, "wheelLateral", WheelLateral);
            SetField(Box, "wheelLongitudinal", WheelLongitudinal);
            SetField(Box, "carrierOffset", carrierOffset);

            CarGo = new GameObject("Car");
            CarGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 0f, 90f));

            // Standby line on the wall side, clear of the parked car — the same shape of layout the
            // spawner builds.
            var standby = new[]
            {
                new Vector3(4.8f,  2.4f, 0f),
                new Vector3(4.8f,  1.2f, 0f),
                new Vector3(4.8f,  0.0f, 0f),
                new Vector3(4.8f, -1.2f, 0f),
                new Vector3(4.8f, -2.6f, 0f),
            };
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject(i == 4 ? "Fueller" : "WheelMan");
                go.transform.SetParent(BoxGo.transform, false);
                Members[i] = go.AddComponent(Runtime("PitCrewMember"));

                var gearGo = new GameObject("Gear");
                gearGo.transform.SetParent(go.transform, false);
                Gear[i] = gearGo.AddComponent<SpriteRenderer>();

                Invoke(Members[i], "Init", standby[i], standby[i], null, Gear[i], i == 4);
                Invoke(Box, "AddMember", Members[i]);
            }
        }

        public void Begin() => Invoke(Box, "BeginService", CarGo.transform);
        public void End() => Invoke(Box, "EndService");
        public void StepBox(float dt) => Invoke(Box, "StepService", dt);
        public bool OnLeftSide => (bool)Property(Box, "WorkingLeftSide");

        public Vector3 Station(int i) => (Vector3)Property(Members[i], "WorkStation");
        public bool Fitted(int i) => (bool)Property(Members[i], "WheelFitted");
        public bool Working(int i) => (bool)Property(Members[i], "IsWorking");
        public Vector3 Where(int i) => Members[i].transform.localPosition;

        // Run the crew (and the box's own sequencing) forward, the way they are ticked in a frame.
        public void Run(float seconds, float dt = 0.02f)
        {
            for (float t = 0f; t < seconds; t += dt) Tick(dt);
        }

        // Same, but stops on the frame the crew are sent round the car — the second side is quick enough
        // that running a fixed span past it catches them with the left-hand wheels already on.
        public bool RunUntilLeftSide(float maxSeconds, float dt = 0.02f)
        {
            for (float t = 0f; t < maxSeconds; t += dt)
            {
                Tick(dt);
                if (OnLeftSide) return true;
            }
            return false;
        }

        void Tick(float dt)
        {
            for (int i = 0; i < Members.Length; i++) Invoke(Members[i], "Step", dt);
            StepBox(dt);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(CarGo);
            UnityEngine.Object.DestroyImmediate(BoxGo);
        }
    }

    // ---- the order --------------------------------------------------------------------------------

    [Test]
    public void All_four_wheel_men_start_on_the_car_s_right()
    {
        using var rig = new Rig();
        rig.Begin();

        for (int i = 0; i < 4; i++)
            Assert.AreEqual(WheelLateral, rig.Station(i).x, Tolerance,
                            $"Wheel man {i} should open the stop on the car's right-hand side, not its left.");

        Assert.IsFalse(rig.OnLeftSide, "Nothing is fitted yet — nobody has gone round.");
    }

    [Test]
    public void They_work_the_corners_in_pairs_a_changer_and_a_carrier()
    {
        using var rig = new Rig();
        rig.Begin();

        // 0/2 share the front corner, 1/3 the rear, offset along the car so they don't stand in one place.
        Assert.AreEqual(WheelLongitudinal, rig.Station(0).y, Tolerance, "Front changer is on the front wheel.");
        Assert.AreEqual(-WheelLongitudinal, rig.Station(1).y, Tolerance, "Rear changer is on the rear wheel.");
        Assert.Greater(rig.Station(2).y, rig.Station(0).y + 0.3f,
                       "The front carrier stands a step outboard of his changer, towards the nose.");
        Assert.Less(rig.Station(3).y, rig.Station(1).y - 0.3f,
                    "The rear carrier stands a step outboard of his changer, towards the tail.");
    }

    [Test]
    public void The_fueller_works_the_left_rear_and_never_changes_sides()
    {
        using var rig = new Rig();
        rig.Begin();
        Vector3 opening = rig.Station(4);

        Assert.AreEqual(-WheelLateral, opening.x, Tolerance, "The filler is on the left rear — so is he.");
        Assert.Less(opening.y, -WheelLongitudinal, "...and behind the rear wheel.");

        rig.Run(8f);
        Assert.IsTrue(rig.OnLeftSide, "The wheel men should have gone round by now.");
        Assert.AreEqual(opening, rig.Station(4), "The fueller stays put for the whole stop.");
    }

    [Test]
    public void Once_the_right_side_is_on_they_run_round_for_the_left()
    {
        using var rig = new Rig();
        rig.Begin();

        // Out to the right-hand corners and long enough there to get those wheels on.
        Assert.IsTrue(rig.RunUntilLeftSide(6f), "Right-hand wheels are on: that is the cue to change sides.");

        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(-WheelLateral, rig.Station(i).x, Tolerance,
                            $"Wheel man {i} should now be heading for the car's left-hand side.");
            Assert.IsFalse(rig.Fitted(i), "He carries a fresh wheel round for the second corner.");
            Assert.IsTrue(rig.Gear[i].enabled, "...and it is in his hands, visibly, on the way.");
        }

        // And they actually get there and fit the second pair.
        rig.Run(5f);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(-WheelLateral, rig.Where(i).x, 0.2f, $"Wheel man {i} finished the run round.");
            Assert.IsTrue(rig.Fitted(i), "The left-hand wheels go on too.");
        }
    }

    [Test]
    public void Nobody_crosses_early_while_the_right_side_is_still_being_worked()
    {
        using var rig = new Rig();
        rig.Begin();

        // Half a second in they are still running out to the right-hand corners.
        rig.Run(0.4f);
        Assert.IsFalse(rig.OnLeftSide, "The switch waits on the wheels, not on a stopwatch.");
        for (int i = 0; i < 4; i++)
            Assert.GreaterOrEqual(rig.Where(i).x, 0f, $"Wheel man {i} has no business on the left yet.");
    }

    // ---- the backstops ----------------------------------------------------------------------------

    [Test]
    public void A_man_who_never_reaches_his_corner_does_not_strand_the_stop_on_one_side()
    {
        using var rig = new Rig();
        SetField(rig.Box, "sideChangeTimeout", 0.5f);
        // Park a wheel man's fitting beat past the timeout so he never reports his wheel on.
        SetField(rig.Members[1], "wheelFitSeconds", 999f);

        rig.Begin();
        rig.Run(1.5f);

        Assert.IsTrue(rig.OnLeftSide, "The other three still get their left-hand wheels.");
    }

    [Test]
    public void A_stop_that_ends_puts_everyone_back_on_the_wall_ready_for_the_next_one()
    {
        using var rig = new Rig();
        rig.Begin();
        Assert.IsTrue(rig.RunUntilLeftSide(6f));
        rig.Run(5f);

        rig.End();
        Assert.IsFalse(rig.OnLeftSide, "The next car starts on the right-hand side again.");
        for (int i = 0; i < 5; i++) Assert.IsFalse(rig.Working(i), $"Member {i} is off the car.");

        rig.Begin();
        for (int i = 0; i < 4; i++)
            Assert.AreEqual(WheelLateral, rig.Station(i).x, Tolerance,
                            $"Wheel man {i} opens the second stop on the right, same as the first.");
    }

    [Test]
    public void The_sequence_can_be_turned_off_for_a_four_corners_at_once_stop()
    {
        using var rig = new Rig();
        SetField(rig.Box, "rightSideFirst", false);
        rig.Begin();

        Assert.AreEqual(WheelLateral, rig.Station(0).x, Tolerance);
        Assert.AreEqual(WheelLateral, rig.Station(1).x, Tolerance);
        Assert.AreEqual(-WheelLateral, rig.Station(2).x, Tolerance, "Off: one man on each of the four corners.");
        Assert.AreEqual(-WheelLateral, rig.Station(3).x, Tolerance);

        rig.Run(3f);
        Assert.IsFalse(rig.OnLeftSide, "Nobody changes sides when the sequence is off.");
    }

    // ---- the wiring -------------------------------------------------------------------------------

    [Test]
    public void The_spawner_still_builds_five_over_the_wall_and_hands_the_box_its_corner_spacing()
    {
        string source = Source("Scripts/AI/PitCrewSpawner.cs");
        StringAssert.Contains("box.carrierOffset", source,
                              "The two men on a corner have to be spaced by the box that moves them.");
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

    static void SetField(object target, string name, object value)
    {
        var fi = target.GetType().GetField(name);
        Assert.IsNotNull(fi, $"{target.GetType().Name}.{name} is gone; this test is written against it.");
        fi.SetValue(target, value);
    }

    static string Source(string relative)
    {
        string path = Path.Combine(Application.dataPath, relative);
        Assert.IsTrue(File.Exists(path), $"{relative} has moved; this test is written against it.");
        return File.ReadAllText(path);
    }
}
