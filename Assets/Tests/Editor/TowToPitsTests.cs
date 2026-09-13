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
            // TrackBuilder re-derives a sample's normal from its tangent when it interpolates between two of
            // them (normal = (tangent.y, -tangent.x)), so an authored normal that disagrees is thrown away.
            // Authoring the one it is going to compute anyway keeps the arithmetic below honest.
            sampleType.GetField("normal").SetValue(s, new Vector2(0f, -1f));
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

            // Box 5 on a 200 m lane: 20 m of exit gap, then five boxes back, parked out on the box lane —
            // which sits along the pit lane's normal, and on this straight that points at -Y.
            var expected = new Vector2(PitLength - ExitGap - PlayerBox * Spacing, -ParkLateral);
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
    public void TheTowedCarIsPinnedOnItsBox()
    {
        // Placing a car is not the same as keeping it there. Nothing owns a parked car's pose — its
        // controller is switched off and the crew are about to spend minutes on it — so anything that
        // touches it in the meantime (a depenetration push off the next box, a body that kept a little of
        // the speed it hit the wall with) walks it out of the box over the following seconds, and the
        // driver comes back to an empty patch of tarmac.
        ConfigurePitBoxes();
        using (var rig = new Rig())
        {
            rig.Tow();
            Vector3 box = rig.CarGo.transform.position;

            var pin = rig.CarGo.GetComponent(Runtime("ParkedCarPin"));
            Assert.IsNotNull(pin, "a towed car is pinned on its box until somebody drives it");

            // Shoved out of the box by something else in the scene, then the pin gets its frame.
            rig.CarGo.transform.position = box + new Vector3(4f, 3f, 0f);
            rig.CarGo.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(6f, 0f);
            Reassert(pin);

            Assert.Less(Vector2.Distance(rig.CarGo.transform.position, box), 0.01f,
                        "the car is put back on the box it was towed to");
            Assert.AreEqual(Vector2.zero, rig.CarGo.GetComponent<Rigidbody2D>().linearVelocity,
                            "and it is not still trying to drive off");
        }
    }

    [Test]
    public void ThePinComesOffTheMomentTheCarIsDrivenAgain()
    {
        ConfigurePitBoxes();
        using (var rig = new Rig())
        {
            rig.Tow();
            var pin = rig.CarGo.GetComponent(Runtime("ParkedCarPin"));

            // The driver is back in it: the controller owns the pose again and the pin must let go rather
            // than fight it back onto the box every frame.
            ((Behaviour)rig.Car).enabled = true;
            Reassert(pin);

            Assert.IsTrue(pin == null, "the pin takes itself off when the car is being driven");
        }
    }

    [Test]
    public void TheTowSwitchesOffAnyAiBrainDrivingThePlayersCar()
    {
        // The broadcast cut and the crew chief's headset both hand the player's own car to the AI. A brain
        // left running through a tow does not park the car in its box, it drives it back out of the pit lane.
        ConfigurePitBoxes();
        using (var rig = new Rig())
        {
            var spline = (Behaviour)rig.CarGo.AddComponent(Runtime("SplineDriver"));
            var aiInput = (Behaviour)rig.CarGo.AddComponent(Runtime("SplineInputDriver"));
            spline.enabled = true;
            aiInput.enabled = true;

            rig.Tow();

            Assert.IsFalse(spline.enabled, "the spline brain is off");
            Assert.IsFalse(aiInput.enabled, "and so is the thing feeding it to the car");
            Assert.IsFalse((bool)GetField(rig.Car, "externalInput"), "the car is taking nobody's inputs");
        }
    }

    [Test]
    public void TheTowTellsTheRunningOrderTheCarWasMovedNotDriven()
    {
        // Laps are counted by watching a car's distance along the track wrap. A tow is a bigger jump than a
        // line crossing, so unless the history is dropped the drive home is scored as a lap.
        var trackerGo = new GameObject("RacePositionTracker");
        var carTf = new GameObject("Car").transform;
        try
        {
            var tracker = trackerGo.AddComponent(Runtime("RacePositionTracker"));
            Type entryType = Runtime("RacePositionTracker+Entry");

            object entry = Activator.CreateInstance(entryType);
            entryType.GetField("tf").SetValue(entry, carTf);
            entryType.GetField("hasPrev").SetValue(entry, true);
            entryType.GetField("prevDist").SetValue(entry, 2500f);

            var byTf = (IDictionary)GetField(tracker, "_byTf");
            byTf[carTf] = entry;

            // The static NoteTeleport goes through the singleton, which is set in Awake — and EditMode never
            // runs one. Drive the instance it would have found.
            Assert.IsNotNull(tracker.GetType().GetMethod("NoteTeleport", BindingFlags.Static | BindingFlags.Public),
                             "the tow calls this without having to find the tracker itself");
            tracker.GetType().GetMethod("ForgetProgressHistory").Invoke(tracker, new object[] { carTf });

            Assert.IsFalse((bool)entryType.GetField("hasPrev").GetValue(entry),
                           "the next distance sample starts a new history rather than being compared to the old one");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carTf.gameObject);
            UnityEngine.Object.DestroyImmediate(trackerGo);
        }
    }

    // Give a pin the frame it would get from FixedUpdate/LateUpdate. EditMode runs no game loop, so the
    // component's own callbacks never fire and the re-assert has to be asked for by hand.
    static void Reassert(Component pin)
    {
        pin.GetType().GetMethod("Reassert", BindingFlags.Instance | BindingFlags.NonPublic)
           .Invoke(pin, null);
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
