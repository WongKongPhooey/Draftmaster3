using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

// Cars must never come out the far side of a wall.
//
// Track walls are thin polygons (1 m thick, TrackEnvironmentBuilder.BarrierColliderThickness). VehicleCollision
// pushes an overlapping car out by the shortest way, so a car whose centre was shoved past the middle of the wall
// was pushed out of the BACK of it, into the crowd. In a wreck that happens: cars pinned against the wall by the
// cars piling into them from the inside. This builds a straight wall and spins cars into it, alone and with a
// second car shoving them, the way VehicleCollision and PlayerVehicleController meet in a race.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class WallPassThroughTests
{
    // Wall face at y = 6, back at y = 7, along x. The track is y < 6.
    const float WallFace = 6f, WallThickness = 1f;

    readonly List<GameObject> _made = new List<GameObject>();

    static Type Runtime(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        Assert.Fail($"{name} is missing from Assembly-CSharp.");
        return null;
    }

    [SetUp]
    public void BuildWall()
    {
        var wall = new GameObject("Barrier_Test");
        var poly = wall.AddComponent<PolygonCollider2D>();
        poly.points = new[]
        {
            new Vector2(-2000f, WallFace), new Vector2(2000f, WallFace),
            new Vector2(2000f, WallFace + WallThickness), new Vector2(-2000f, WallFace + WallThickness),
        };
        _made.Add(wall);
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void Clear()
    {
        foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _made.Clear();
    }

    class Car
    {
        public GameObject go;
        public Component pvc, vc;
    }

    Car MakeCar(string name, Vector2 pos, float headingDeg, float speed, Vector2 sideVel = default)
    {
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");
        var go = new GameObject(name);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        _made.Add(go);
        var pvcType = Runtime("PlayerVehicleController");
        var vcType = Runtime("VehicleCollision");
        var pvc = go.AddComponent(pvcType);
        Set(pvc, "vehicleInfo", vehicleInfo);
        Set(pvc, "spriteFacesUp", false);
        Set(pvc, "angleOffsetDeg", 0f);
        Set(pvc, "grassTrails", false);
        Set(pvc, "enableWheelspin", false);
        Set(pvc, "externalInput", true);
        Set(pvc, "damageImpairsHandling", false);
        Set(pvc, "surfaceSpray", false);
        Set(pvc, "impactDebris", false);
        Set(pvc, "applyBanking", false);
        Call(pvc, "Start");
        pvcType.GetMethod("SeedPose").Invoke(pvc, new object[] { pos, headingDeg, speed });
        if (sideVel != default)
        {
            // A shove already under way: add it as a car hit would.
            pvcType.GetMethod("ApplyCarImpact").Invoke(pvc, new object[] { Vector2.zero, pos, sideVel, 0.3f });
        }
        var vc = go.AddComponent(vcType);
        Call(vc, "Awake");
        Call(vc, "OnEnable");
        return new Car { go = go, pvc = pvc, vc = vc };
    }

    // Steps the cars as a race does: every controller, then every collision pass. Returns the deepest any car's
    // centre got past the wall face (m; negative = never reached it) and whether any came out behind the wall.
    static float Run(List<Car> cars, int steps, float throttle, out bool through)
    {
        var pvcType = Runtime("PlayerVehicleController");
        var vcType = Runtime("VehicleCollision");
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var vcStep = vcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var setInput = pvcType.GetMethod("SetInput");
        float deepest = float.MinValue;
        through = false;
        for (int s = 0; s < steps; s++)
        {
            foreach (var c in cars)
            {
                setInput.Invoke(c.pvc, new object[] { 0f, throttle, 0f });
                pvcStep.Invoke(c.pvc, null);
            }
            Physics2D.SyncTransforms();
            foreach (var c in cars) { vcStep.Invoke(c.vc, null); Physics2D.SyncTransforms(); }
            foreach (var c in cars)
            {
                float y = c.go.transform.position.y;
                deepest = Mathf.Max(deepest, y - WallFace);
                if (y > WallFace + WallThickness * 0.5f) through = true;
            }
        }
        return deepest;
    }

    // One car spun into the wall at an angle, or a pair: the outer car pinned and the inner car slammed sideways
    // into it, the pile-up at the wall a wreck makes.
    static IEnumerable<(string label, Func<WallPassThroughTests, List<Car>> build)> Cases()
    {
        foreach (float v in new[] { 20f, 40f, 60f, 85f })
        foreach (float ang in new[] { 15f, 45f, 90f, 135f })
        {
            yield return ($"alone v{v} at {ang}°", t => new List<Car>
            {
                t.MakeCar("A", new Vector2(0f, 3f), ang, v),
            });
            yield return ($"pinned v{v} hit at {ang}°", t => new List<Car>
            {
                t.MakeCar("A", new Vector2(0f, WallFace - 1.05f), 0f, v),
                t.MakeCar("B", new Vector2(-1.5f, WallFace - 3.3f), ang, v, new Vector2(0f, 0.25f * v)),
            });
            yield return ($"three-wide v{v} hit at {ang}°", t => new List<Car>
            {
                t.MakeCar("A", new Vector2(0f, WallFace - 1.05f), 0f, v),
                t.MakeCar("B", new Vector2(0.5f, WallFace - 3.2f), 0f, v),
                t.MakeCar("C", new Vector2(-1.5f, WallFace - 5.5f), ang, v, new Vector2(0f, 0.3f * v)),
            });
        }
    }

    [Test]
    [Explicit("Diagnostic: cars spun and shoved into a straight wall; how deep each case got and whether it came out the back.")]
    public void WallTable()
    {
        var sb = new StringBuilder("[WallSim] cars into a 1 m wall (deepest centre past the face)");
        foreach (var (label, build) in Cases())
        foreach (float thr in new[] { 0f, 1f })
        {
            Clear(); BuildWall();
            float deep = Run(build(this), 150, thr, out bool through);
            sb.Append($"\n  {label,-28} thr {thr:0}: deepest {deep,6:0.00}{(through ? "  THROUGH" : "")}");
        }
        Debug.Log(sb.ToString());
    }

    [Test]
    public void NoCarComesOutTheBackOfTheWall()
    {
        var failures = new StringBuilder();
        foreach (var (label, build) in Cases())
        foreach (float thr in new[] { 0f, 1f })
        {
            Clear(); BuildWall();
            Run(build(this), 150, thr, out bool through);
            if (through) failures.Append($"\n  {label} thr {thr:0}");
        }
        Assert.IsEmpty(failures.ToString(), "Cars came out behind the wall:" + failures);
    }

    [Test]
    public void ACarShovedPastTheMiddleOfTheWallIsPutBackOnTheTrack()
    {
        // However it got there - a pile-up's pushes in one step - a car whose centre is past the middle of the wall
        // came from the track and goes back to it, not out into the crowd.
        var car = MakeCar("A", new Vector2(0f, WallFace - 1.5f), 0f, 30f);
        Run(new List<Car> { car }, 1, 0f, out _);
        car.go.transform.position = new Vector3(0f, WallFace + 0.7f, 0f);
        Physics2D.SyncTransforms();
        Run(new List<Car> { car }, 3, 0f, out _);
        Assert.Less(car.go.transform.position.y, WallFace, "The car was pushed out of the back of the wall.");
    }

    [Test]
    public void ACarThatGoesStraightThroughTheWallInOneStepIsStoppedAtIt()
    {
        // Clean through, overlapping nothing at the end of the step: only the path it took says it crossed.
        var car = MakeCar("A", new Vector2(0f, WallFace - 1.5f), 0f, 30f);
        Run(new List<Car> { car }, 1, 0f, out _);
        car.go.transform.position = new Vector3(0.6f, WallFace + WallThickness + 2.5f, 0f);
        Physics2D.SyncTransforms();
        Run(new List<Car> { car }, 3, 0f, out _);
        Assert.Less(car.go.transform.position.y, WallFace, "The car went through the wall and stayed out there.");
    }

    [Test]
    public void ACarPlacedOnAPathBehindTheWallIsStoppedAtIt()
    {
        // A kinematic car (SplineDriver owns the transform: caution engage, takeover, pit-in) is put back on its path
        // every step whatever the wall did to it. Its path here is a merge arc that swings 8 m out past the wall and
        // back - the "large arc off the track" - and the car must ride the wall face instead of driving out round it,
        // then pick its path up again once it is back on the track side.
        var go = new GameObject("Placed");
        _made.Add(go);
        var spline = go.AddComponent(Runtime("SplineDriver"));
        Set(spline, "vehicleInfo", Resources.Load("Vehicles/Cup24"));
        var vcType = Runtime("VehicleCollision");
        go.transform.position = new Vector3(0f, 3f, 0f);
        var vc = go.AddComponent(vcType);
        Call(vc, "Awake");
        Call(vc, "OnEnable");
        var vcStep = vcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        // y along the arc: 3 m off the wall face, out to 14 (8 m behind its back), and in again, 1.6 m a step (80 m/s).
        float PathY(float x) => 3f + 11f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(x / 160f));
        float worst = float.MinValue;
        Vector2 last = Vector2.zero;
        for (int s = 0; s <= 120; s++)
        {
            float x = s * 1.6f;
            float y = PathY(x), dy = PathY(x + 0.5f) - y;
            go.transform.SetPositionAndRotation(new Vector3(x, y, 0f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, 0.5f) * Mathf.Rad2Deg));
            Physics2D.SyncTransforms();
            vcStep.Invoke(vc, null);
            Physics2D.SyncTransforms();
            last = go.transform.position;
            worst = Mathf.Max(worst, last.y);
        }
        Assert.Less(worst, WallFace, $"A placed car got {worst - WallFace:0.00} m past the wall face.");
        Assert.AreEqual(PathY(120 * 1.6f), last.y, 0.01f, "The car didn't pick its path up again once it was clear of the wall.");
    }

    [Test]
    public void AGlancingTouchOnTheWallIsUnchanged()
    {
        // The everyday case must come out exactly as it did before the wall was made one-sided: a car brushing the
        // wall at a shallow angle, at speed. (With nobody steering it, it is turned away and drives off across
        // the track; that is the wall's existing response, not this fix.) wallSideJumpLimit 0 switches the
        // one-sided push and the crossing backstop off.
        Vector2 Final(float limit)
        {
            Clear(); BuildWall();
            var car = MakeCar("A", new Vector2(0f, WallFace - 1.2f), 4f, 50f);
            Set(car.vc, "wallSideJumpLimit", limit);
            Run(new List<Car> { car }, 100, 1f, out bool through);
            Assert.IsFalse(through);
            return car.go.transform.position;
        }
        Vector2 before = Final(0f), after = Final(8f);
        Assert.Less(Vector2.Distance(before, after), 0.25f, $"A glancing touch now ends at {after}, was {before}.");
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(target, null);
    }
}
