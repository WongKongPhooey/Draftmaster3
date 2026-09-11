using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The tow home, for a car that is not going anywhere on its own.
//
// Two things it has to get right, both of which it used to get wrong. It has to put the car in the box the
// driver has THIS session — the boxes are fitted to the entry list once the field arrives, so the spot the
// car was parked on when the scene opened is not that box and can be most of a pit lane away from it — and
// it has to stop the lap clock, because that lap ended in the wall and nothing else was going to end it for
// a car with no spline running.
//
// The runtime lives in Assembly-CSharp, which a test assembly cannot reference, so everything here goes
// through reflection — the same approach as PitCrewSideOrderTests and TrackLimitsTests.
public class TowToPitsTests
{
    const float PitLength = 200f;
    const float ExitGap = 20f;
    const float Spacing = 10f;
    const int Boxes = 40;
    const float ParkLateral = 6f;
    const int PlayerBox = 5;

    // Nowhere near the box ladder: a car that lands here was put back by the opening snapshot.
    static readonly Vector3 OpeningPose = new Vector3(999f, 999f, 0f);

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " is missing from Assembly-CSharp.");
        return type;
    }

    static void SetField(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, target.GetType().Name + "." + field + " is missing.");
        f.SetValue(target, value);
    }

    static object GetField(object target, string field)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, target.GetType().Name + "." + field + " is missing.");
        return f.GetValue(target);
    }

    // A straight 200 m pit lane along +X with its normal on +Y, as a list of TrackBuilder.Sample. Straight
    // and axis-aligned so where the boxes land is arithmetic rather than something to be taken on trust.
    static IList BuildPitSamples()
    {
        Type sampleType = Runtime("TrackBuilder+Sample");
        var list = (IList)Activator.CreateInstance(
            typeof(System.Collections.Generic.List<>).MakeGenericType(sampleType));

        for (int i = 0; i <= 20; i++)
        {
            float d = PitLength * i / 20f;
            object s = Activator.CreateInstance(sampleType);
            sampleType.GetField("position").SetValue(s, new Vector2(d, 0f));
            sampleType.GetField("tangent").SetValue(s, new Vector2(1f, 0f));
            sampleType.GetField("normal").SetValue(s, new Vector2(0f, 1f));
            sampleType.GetField("width").SetValue(s, 12f);
            sampleType.GetField("distance").SetValue(s, d);
            list.Add(s);
        }
        return list;
    }

    // A pit lane, a wrecked car, a driver sat in it, and a PitLaneStart that believes it handed the controls
    // over. Everything the tow reads and nothing it doesn't.
    class Rig : IDisposable
    {
        public readonly GameObject TrackGo, CarGo, PlayerGo, StartGo;
        public readonly Component Track, Car, Start;

        public Rig()
        {
            TrackGo = new GameObject("TrackBuilder");
            Track = TrackGo.AddComponent(Runtime("TrackBuilder"));

            CarGo = new GameObject("PlayerCar");
            CarGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Car = CarGo.AddComponent(Runtime("PlayerVehicleController"));

            PlayerGo = new GameObject("OnFootPlayer");
            PlayerGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            PlayerGo.SetActive(false);            // they are in the car

            StartGo = new GameObject("PitLaneStart");
            Start = StartGo.AddComponent(Runtime("PitLaneStart"));

            SetField(Start, "track", Track);
            SetField(Start, "car", Car);
            SetField(Start, "_player", PlayerGo);
            SetField(Start, "_pitSamples", BuildPitSamples());
            SetField(Start, "_usedPit", true);
            SetField(Start, "_boxPosition", OpeningPose);
            SetField(Start, "_boxRotation", Quaternion.identity);
            SetField(Start, "_boxHeadingDeg", 0f);
            SetField(Start, "_boxKnown", true);

            Type phase = Start.GetType().GetNestedType("EntryPhase", BindingFlags.NonPublic);
            Assert.IsNotNull(phase, "PitLaneStart.EntryPhase is missing.");
            SetField(Start, "_phase", Enum.Parse(phase, "Driving"));

            // Where it ended up: in the wall, most of a lap from home, still pointing where it spun to and
            // with the dynamic model still holding the moment of the impact.
            CarGo.transform.SetPositionAndRotation(new Vector3(-400f, 120f, 0f), Quaternion.Euler(0f, 0f, 37f));
            SetField(Car, "_headingDeg", 37f);
            SetField(Car, "_vx", 60f);
        }

        public bool Tow() => (bool)Start.GetType().GetMethod("TowToPits").Invoke(Start, null);

        public object CarProperty(string name) => Car.GetType().GetProperty(name).GetValue(Car);

        public void Dispose()
        {
            foreach (var go in new[] { TrackGo, CarGo, PlayerGo, StartGo })
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void ConfigurePitBoxes()
    {
        Type pitLane = Runtime("PitLane");
        pitLane.GetMethod("Configure").Invoke(null, new object[] { ExitGap, Spacing, Boxes, ParkLateral });
        pitLane.GetMethod("SetPlayerBox").Invoke(null, new object[] { PlayerBox });
    }

    [TearDown]
    public void ForgetTheBoxes() => Runtime("PitLane").GetMethod("Clear").Invoke(null, null);

    [Test]
    public void TheCarIsTowedToTheBoxThePlayerHasThisSession()
    {
        ConfigurePitBoxes();
        using (var rig = new Rig())
        {
            Assert.IsTrue(rig.Tow(), "a driving player with a known box can be towed");

            // Box 5 on a 200 m lane: 20 m of exit gap, then five boxes back, parked out on the box lane.
            var expected = new Vector2(PitLength - ExitGap - PlayerBox * Spacing, ParkLateral);
            Vector3 landed = rig.CarGo.transform.position;

            Assert.AreEqual(expected.x, landed.x, 0.01f, "parked at its own box along the lane");
            Assert.AreEqual(expected.y, landed.y, 0.01f, "parked out on the box lane, not on the driving line");
            Assert.Greater(Vector2.Distance(landed, OpeningPose), 100f,
                           "the tow must not use the pose the car was parked on when the scene opened");
        }
    }

    [Test]
    public void TheDriverIsPutOutBesideTheirOwnCar()
    {
        ConfigurePitBoxes();
        using (var rig = new Rig())
        {
            rig.Tow();

            Assert.IsTrue(rig.PlayerGo.activeSelf, "the driver is out of the car and on their feet");
            Assert.Less(Vector2.Distance(rig.PlayerGo.transform.position, rig.CarGo.transform.position), 3f,
                        "stood at the car's door, not left behind where it crashed");

            // A Transform is only half of a move: these bodies are simulated, and the pose physics holds is
            // the one that wins.
            var walker = rig.PlayerGo.GetComponent<Rigidbody2D>();
            Assert.Less(Vector2.Distance(walker.position, rig.PlayerGo.transform.position), 0.01f,
                        "the on-foot body agrees with where the driver was put");

            var carBody = rig.CarGo.GetComponent<Rigidbody2D>();
            Assert.Less(Vector2.Distance(carBody.position, rig.CarGo.transform.position), 0.01f,
                        "the car's body agrees with where the car was put");
        }
    }

    [Test]
    public void ACarTowedInIsStoppedAndSquareInItsBox()
    {
        ConfigurePitBoxes();
        using (var rig = new Rig())
        {
            rig.Tow();

            // Nose down the lane (heading 0 on this straight), carrying none of the speed it hit the wall
            // with — otherwise the driver climbs back into a car still travelling and pointing at the wall.
            Assert.AreEqual(0f, Mathf.DeltaAngle((float)rig.CarProperty("HeadingDeg"), 0f), 0.01f);
            Assert.AreEqual(0f, (float)rig.CarProperty("SpeedMps"), 0.01f, "stopped");
        }
    }

    [Test]
    public void WithNoBoxesFittedTheTowFallsBackToWhereTheCarWasParked()
    {
        // A session that fitted no box ladder at all — no field out, nobody racing. The opening pose is all
        // there is, and it beats leaving the car in the wall.
        using (var rig = new Rig())
        {
            Assert.IsTrue(rig.Tow());
            Assert.Less(Vector2.Distance(rig.CarGo.transform.position, OpeningPose), 0.01f);
        }
    }

    [Test]
    public void TheLapTheDriverWasOnIsThrownAway()
    {
        var go = new GameObject("LapTimingManager");
        var car = new GameObject("Car").transform;
        try
        {
            var timing = go.AddComponent(Runtime("LapTimingManager"));
            Type timesType = Runtime("LapTimingManager+CarTimes");

            object times = Activator.CreateInstance(timesType);
            timesType.GetField("tf").SetValue(times, car);
            timesType.GetField("lapStarted").SetValue(times, true);
            timesType.GetField("valid").SetValue(times, true);
            timesType.GetField("lapStartTime").SetValue(times, 0f);

            var cars = (IDictionary)GetField(timing, "_cars");
            cars[car] = times;

            timing.GetType().GetMethod("AbandonLap").Invoke(timing, new object[] { car });

            Assert.IsFalse((bool)timesType.GetField("lapStarted").GetValue(times), "the clock is stopped");
            Assert.IsFalse((bool)timesType.GetField("valid").GetValue(times), "and the lap does not count");

            float running = (float)timesType.GetMethod("CurrentLapTime").Invoke(times, new object[] { 90f });
            Assert.Less(running, 0f, "a stopped clock reads as no lap running, not as a 90 second one");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(car.gameObject);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
