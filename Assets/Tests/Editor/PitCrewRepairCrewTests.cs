using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// A wrecked car towed back to its box is repaired over minutes (PitCrewRepair), and the crew should be seen
// doing it: out round the car, one man to each corner and one at the nose, square to it with nothing in their
// hands, for as long as the work runs — then back to the wall when it is done or the driver gets back in.
//
// The crew used to stay stood on the wall holding wheels while the damage readout ticked down on its own.
//
// Same reflection rig as PitCrewSideOrderTests: the crew runtime lives in Assembly-CSharp.
public class PitCrewRepairCrewTests
{
    const float WheelLateral = 1.2f;
    const float WheelLongitudinal = 1.8f;
    const float Tolerance = 0.05f;

    static readonly Vector3[] Standby =
    {
        new(4.8f,  2.4f, 0f),
        new(4.8f,  1.2f, 0f),
        new(4.8f,  0.0f, 0f),
        new(4.8f, -1.2f, 0f),
        new(4.8f, -2.6f, 0f),
    };

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    // A box at the origin, unrotated (box-local IS world), five members on the wall at +X, and a car parked
    // square in it with its nose up the lane (+Y) — right-hand side on +X.
    class Rig : IDisposable
    {
        public readonly GameObject BoxGo, CarGo;
        public readonly Component Box;
        public readonly Component[] Members = new Component[5];
        public readonly SpriteRenderer[] Gear = new SpriteRenderer[5];

        public Rig()
        {
            BoxGo = new GameObject("PitCrewBox");
            Box = BoxGo.AddComponent(Runtime("PitCrewBox"));
            SetField(Box, "wheelLateral", WheelLateral);
            SetField(Box, "wheelLongitudinal", WheelLongitudinal);

            CarGo = new GameObject("Car");
            CarGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 0f, 90f));

            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject(i == 4 ? "Fueller" : "WheelMan");
                go.transform.SetParent(BoxGo.transform, false);
                Members[i] = go.AddComponent(Runtime("PitCrewMember"));

                var gearGo = new GameObject("Gear");
                gearGo.transform.SetParent(go.transform, false);
                Gear[i] = gearGo.AddComponent<SpriteRenderer>();

                Invoke(Members[i], "Init", Standby[i], Standby[i], null, Gear[i], i == 4);
                Invoke(Box, "AddMember", Members[i]);
            }
        }

        public bool BeginRepair() => (bool)Invoke(Box, "BeginRepair", CarGo.transform);
        public void EndRepair() => Invoke(Box, "EndRepair");
        public void BeginService() => Invoke(Box, "BeginService", CarGo.transform);
        public bool Repairing => (bool)Property(Box, "IsRepairing");

        public Vector3 Station(int i) => (Vector3)Property(Members[i], "WorkStation");
        public bool Working(int i) => (bool)Property(Members[i], "IsWorking");
        public bool MemberRepairing(int i) => (bool)Property(Members[i], "IsRepairing");
        public bool Fitted(int i) => (bool)Property(Members[i], "WheelFitted");
        public Vector3 Where(int i) => Members[i].transform.localPosition;

        public void Run(float seconds, float dt = 0.02f)
        {
            for (float t = 0f; t < seconds; t += dt)
                for (int i = 0; i < Members.Length; i++) Invoke(Members[i], "Step", dt);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(CarGo);
            UnityEngine.Object.DestroyImmediate(BoxGo);
        }
    }

    [Test]
    public void The_crew_go_out_to_all_four_corners_and_the_nose()
    {
        using var rig = new Rig();
        Assert.IsTrue(rig.BeginRepair(), "Nothing else is running in the box; the repair should start.");
        Assert.IsTrue(rig.Repairing);

        Near(new Vector3( WheelLateral,  WheelLongitudinal, 0f), rig.Station(0), "Right front.");
        Near(new Vector3( WheelLateral, -WheelLongitudinal, 0f), rig.Station(1), "Right rear.");
        Near(new Vector3(-WheelLateral,  WheelLongitudinal, 0f), rig.Station(2), "Left front.");
        Near(new Vector3(-WheelLateral, -WheelLongitudinal, 0f), rig.Station(3), "Left rear.");
        Assert.AreEqual(0f, rig.Station(4).x, Tolerance, "The fifth man works the nose, on the centreline...");
        Assert.Greater(rig.Station(4).y, WheelLongitudinal, "...in front of it.");

        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(rig.Working(i), $"Member {i} should be out at the car.");
            Assert.IsTrue(rig.MemberRepairing(i), $"Member {i} should know it is a repair, not a stop.");
        }
    }

    [Test]
    public void They_get_there_and_stay_round_the_car_with_empty_hands()
    {
        using var rig = new Rig();
        rig.BeginRepair();
        rig.Run(12f);

        float shuffle = (float)GetField(rig.Members[0], "repairShuffle");
        for (int i = 0; i < 5; i++)
        {
            Assert.Less(Vector3.Distance(rig.Where(i), rig.Station(i)), shuffle + 0.2f,
                        $"Member {i} should be at the car, working round his station — not back on the wall.");
            Assert.IsFalse(rig.Gear[i].enabled, $"Member {i} is holding a wheel or a can while beating a panel.");
            Assert.IsFalse(rig.Fitted(i), "Nothing is fitted on a repair.");
        }
        Near(new Vector3(WheelLateral, WheelLongitudinal, 0f), rig.Station(0), "Nobody changes sides on a repair.");
    }

    [Test]
    public void When_the_repair_ends_they_walk_back_and_pick_their_gear_up()
    {
        using var rig = new Rig();
        rig.BeginRepair();
        rig.Run(6f);
        rig.EndRepair();
        Assert.IsFalse(rig.Repairing);
        rig.Run(6f);

        for (int i = 0; i < 5; i++)
        {
            Assert.IsFalse(rig.Working(i));
            Assert.Less(Vector3.Distance(rig.Where(i), Standby[i]), 0.15f, $"Member {i} should be back on the wall.");
            Assert.IsTrue(rig.Gear[i].enabled, $"Member {i} should be stood ready with his gear again.");
        }
    }

    [Test]
    public void A_stop_in_progress_is_never_interrupted_by_a_repair()
    {
        using var rig = new Rig();
        rig.BeginService();
        Assert.IsFalse(rig.BeginRepair(), "The crew must not walk off a car mid-stop.");
        rig.EndRepair();
        for (int i = 0; i < 5; i++)
            Assert.IsTrue(rig.Working(i), "Ending a repair that never started must not stand down a stop.");
    }

    [Test]
    public void A_stop_takes_the_crew_off_a_repair()
    {
        using var rig = new Rig();
        rig.BeginRepair();
        rig.BeginService();
        Assert.IsFalse(rig.Repairing);
        for (int i = 0; i < 5; i++)
        {
            Assert.IsFalse(rig.MemberRepairing(i), $"Member {i} should be on stop duty now.");
            Assert.IsTrue(rig.Gear[i].enabled, $"Member {i} needs his gear back for the stop.");
        }
    }

    [Test]
    public void The_repair_calls_the_crew_out_and_stands_them_down()
    {
        // PitCrewRepair is the only thing that knows a repair is running; it is what has to call the crew.
        string src = Source("Scripts/OnFoot/PitCrewRepair.cs");
        StringAssert.Contains("BeginRepair(", src);
        StringAssert.Contains("EndRepair(", src);
    }

    static void Near(Vector3 expected, Vector3 actual, string message) =>
        Assert.Less(Vector3.Distance(expected, actual), Tolerance, $"{message} Expected {expected}, got {actual}.");

    // ---- reflection ---------------------------------------------------------------------------------

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

    static object GetField(object target, string name)
    {
        var fi = target.GetType().GetField(name);
        Assert.IsNotNull(fi, $"{target.GetType().Name}.{name} is gone; this test is written against it.");
        return fi.GetValue(target);
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
