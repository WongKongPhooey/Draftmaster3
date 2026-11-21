using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// END RACE after a win: the car goes onto the start/finish line with the driver in it and the controls live
// (PitLaneStart.StartVictoryLap, driven by WinnerVictoryLap), instead of into the box. When the scene cannot do
// that, it must refuse without touching anything, so RaceDirector.EndRace falls back to the box.
//
// The runtime lives in Assembly-CSharp, which a test assembly cannot reference, so this goes through reflection
// the way TowToPitsTests does.
public class WinnerVictoryLapTests
{
    const float LineDistance = 100f;

    // A solo career: the mode with an on-foot body to climb out with.
    object _mode;
    FieldInfo ModeField => Runtime("GameSession").GetField("CurrentMode", BindingFlags.Static | BindingFlags.Public);

    [SetUp]
    public void SoloCareer()
    {
        _mode = ModeField.GetValue(null);
        ModeField.SetValue(null, Enum.Parse(Runtime("GameSession+Mode"), "SinglePlayer"));
    }

    [TearDown]
    public void RestoreMode() => ModeField.SetValue(null, _mode);

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

    // A 300 m straight heading north (+Y) from (10, 20), its line 100 m along it — so the line is at (10, 120),
    // facing 90°. A car parked in its box elsewhere, a driver either in it or stood beside it.
    class Rig : IDisposable
    {
        public readonly GameObject TrackGo, CarGo, PlayerGo, StartGo;
        public readonly ScriptableObject Info;
        public readonly Component Track, Car, Start;

        public Rig(bool driving)
        {
            Type infoType = Runtime("TrackInfoV2");
            Info = ScriptableObject.CreateInstance(infoType);
            Type segType = Runtime("TrackInfoV2+TrackSegment");
            Type segKind = Runtime("TrackInfoV2+SegmentType");
            object seg = Activator.CreateInstance(segType);
            segType.GetField("type").SetValue(seg, Enum.Parse(segKind, "Straight"));
            segType.GetField("length").SetValue(seg, 300f);
            var segs = Array.CreateInstance(segType, 1);
            segs.SetValue(seg, 0);
            SetField(Info, "segments", segs);
            SetField(Info, "startPosition", new Vector2(10f, 20f));
            SetField(Info, "startHeading", 90f);
            SetField(Info, "closedLoop", false);
            SetField(Info, "startFinishDistance", LineDistance);

            TrackGo = new GameObject("TrackBuilder");
            Track = TrackGo.AddComponent(Runtime("TrackBuilder"));
            SetField(Track, "track", Info);

            CarGo = new GameObject("PlayerCar");
            CarGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Car = CarGo.AddComponent(Runtime("PlayerVehicleController"));
            CarGo.transform.SetPositionAndRotation(new Vector3(-50f, -30f, 0f), Quaternion.Euler(0f, 0f, 200f));
            SetField(Car, "_headingDeg", 200f);
            SetField(Car, "_vx", 12f);
            ((Behaviour)Car).enabled = false;   // the AI or the box had it

            PlayerGo = new GameObject("OnFootPlayer");
            PlayerGo.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            PlayerGo.SetActive(!driving);

            StartGo = new GameObject("PitLaneStart");
            Start = StartGo.AddComponent(Runtime("PitLaneStart"));
            SetField(Start, "track", Track);
            SetField(Start, "car", Car);
            SetField(Start, "_player", PlayerGo);
            SetField(Start, "_boxPosition", new Vector3(-50f, -30f, 0f));
            SetField(Start, "_boxKnown", true);

            Type phase = Start.GetType().GetNestedType("EntryPhase", BindingFlags.NonPublic);
            Assert.IsNotNull(phase, "PitLaneStart.EntryPhase is missing.");
            SetField(Start, "_phase", Enum.Parse(phase, driving ? "Driving" : "Walking"));
        }

        public bool StartVictoryLap() => (bool)Start.GetType().GetMethod("StartVictoryLap").Invoke(Start, null);
        public bool IsDriving => (bool)Start.GetType().GetProperty("IsDriving").GetValue(Start);
        public object CarProperty(string name) => Car.GetType().GetProperty(name).GetValue(Car);

        public void Dispose()
        {
            foreach (var go in new[] { TrackGo, CarGo, PlayerGo, StartGo })
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            if (Info != null) UnityEngine.Object.DestroyImmediate(Info);
        }
    }

    [Test]
    public void TheWinnersCarGoesOntoTheStartFinishLineStoppedAndFacingTheRaceDirection()
    {
        using (var rig = new Rig(driving: true))
        {
            Assert.IsTrue(rig.StartVictoryLap());

            Vector3 at = rig.CarGo.transform.position;
            Assert.AreEqual(10f, at.x, 0.05f, "on the line's centre");
            Assert.AreEqual(20f + LineDistance, at.y, 0.05f, "on the start/finish line, not in the box");
            Assert.AreEqual(0f, Mathf.DeltaAngle((float)rig.CarProperty("HeadingDeg"), 90f), 0.5f,
                            "pointing the way the race ran");
            Assert.AreEqual(0f, (float)rig.CarProperty("SpeedMps"), 0.01f, "stopped");

            var body = rig.CarGo.GetComponent<Rigidbody2D>();
            Assert.Less(Vector2.Distance(body.position, at), 0.01f, "the car's body agrees with where it was put");
        }
    }

    [Test]
    public void TheWinnerIsHandedTheControls()
    {
        using (var rig = new Rig(driving: true))
        {
            rig.StartVictoryLap();
            Assert.IsTrue(((Behaviour)rig.Car).enabled, "the controller is live");
            Assert.IsTrue(rig.IsDriving);
            Assert.IsFalse((bool)rig.Car.GetType().GetField("externalInput").GetValue(rig.Car),
                           "nobody else is steering it");
        }
    }

    [Test]
    public void ADriverAlreadyOutOfTheCarIsPutBackInIt()
    {
        using (var rig = new Rig(driving: false))
        {
            Assert.IsTrue(rig.StartVictoryLap());
            Assert.IsFalse(rig.PlayerGo.activeSelf, "the on-foot body is put away");
            Assert.IsTrue(rig.IsDriving);
            Assert.IsTrue(((Behaviour)rig.Car).enabled);
        }
    }

    [Test]
    public void WithNoBoxToComeBackToItRefusesAndLeavesTheCarAlone()
    {
        using (var rig = new Rig(driving: true))
        {
            SetField(rig.Start, "_boxKnown", false);
            Assert.IsFalse(rig.StartVictoryLap());
            Assert.AreEqual(-50f, rig.CarGo.transform.position.x, 0.01f, "not moved");
            Assert.IsFalse(((Behaviour)rig.Car).enabled, "not handed over");
        }
    }

    // A single race (or Master Draft) never stood a body up. The winner is lent one for the lap — made from the
    // scene's on-foot prefab and put away in the seat — so they can climb out on the track.
    [Test]
    public void AOneOffRaceWinnerIsGivenABodyKeptInTheSeat()
    {
        ModeField.SetValue(null, Enum.Parse(Runtime("GameSession+Mode"), "SingleRace"));
        var template = new GameObject("OnFootTemplate");
        template.AddComponent<Rigidbody2D>();
        GameObject spawned = null;
        try
        {
            using (var rig = new Rig(driving: true))
            {
                SetField(rig.Start, "_player", null);
                SetField(rig.Start, "onFootPrefab", template);

                Assert.IsTrue(rig.StartVictoryLap());
                spawned = (GameObject)rig.Start.GetType()
                    .GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig.Start);
                Assert.IsNotNull(spawned, "a body to climb out with");
                Assert.IsFalse(spawned.activeSelf, "kept in the seat until they get out");
                Assert.IsTrue(rig.IsDriving);
                Assert.IsTrue(((Behaviour)rig.Car).enabled, "the controller is live");
                Assert.AreEqual(20f + LineDistance, rig.CarGo.transform.position.y, 0.05f, "on the start/finish line");
            }
        }
        finally
        {
            if (spawned != null) UnityEngine.Object.DestroyImmediate(spawned);
            UnityEngine.Object.DestroyImmediate(template);
        }
    }

    [Test]
    public void AOneOffRaceWithNoOnFootPrefabRefusesAndLeavesTheCarAlone()
    {
        ModeField.SetValue(null, Enum.Parse(Runtime("GameSession+Mode"), "SingleRace"));
        using (var rig = new Rig(driving: true))
        {
            SetField(rig.Start, "_player", null);
            Assert.IsFalse(rig.StartVictoryLap());
            Assert.AreEqual(-50f, rig.CarGo.transform.position.x, 0.01f, "not moved");
            Assert.IsFalse(((Behaviour)rig.Car).enabled, "not handed over");
        }
    }

    // Heading back in a one-off race: the car goes in its box and the borrowed body is put away again — the driver
    // is sat in the car, as that mode always has them, not stood in the pit lane.
    [Test]
    public void AOneOffRaceHeadingBackPutsTheDriverBackInTheParkedCar()
    {
        ModeField.SetValue(null, Enum.Parse(Runtime("GameSession+Mode"), "SingleRace"));
        using (var rig = new Rig(driving: false))
        {
            rig.PlayerGo.SetActive(true);
            Assert.IsTrue((bool)rig.Start.GetType().GetMethod("EndRaceInBox").Invoke(rig.Start, null));
            Assert.IsFalse(rig.PlayerGo.activeSelf, "the body is put away");
            Assert.IsTrue(rig.IsDriving, "sat in the car");
            Assert.IsFalse(((Behaviour)rig.Car).enabled, "with the controls off");
            Assert.AreEqual(-50f, rig.CarGo.transform.position.x, 0.05f, "in the box");
        }
    }

    [Test]
    public void BeginWithoutAPitLaneStartRefuses()
    {
        var host = new GameObject("RaceDirectorHost");
        try
        {
            Type lapType = Runtime("WinnerVictoryLap");
            var begin = lapType.GetMethod("Begin");
            Assert.IsFalse((bool)begin.Invoke(null, new object[] { host, null, null }));
            Assert.IsNull(host.GetComponent(lapType));
            Assert.IsFalse((bool)lapType.GetProperty("Active").GetValue(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
