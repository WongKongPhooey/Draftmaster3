using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// An AI car moving off from a standstill must not fishtail.
//
// The cars are rear-wheel drive with front-wheel steering: at crawling speed the rear tyres grip and roll where they
// point, so the back of the car follows the front through a turn and never whips from side to side. Players saw AI
// cars that had stopped (in a wreck, a queue, a spin) swing their tails back and forth as they moved off. This sets an
// AI car down at rest on Daytona, pointing straight down the road or turned across it the way a stopped car is left,
// lets the real AI drive it away (SplineDriver brain, PlayerVehicleController physics, SplineInputDriver between
// them), and watches the rear axle while it is under 10 m/s.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class AILowSpeedSwingTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/Daytona.prefab";
    GameObject _package, _car;

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
    public void SpawnTrack()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath);
        Assert.IsNotNull(prefab, $"track package missing at {PackagePath}");
        _package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = _package.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
    }

    [TearDown]
    public void Despawn()
    {
        if (_car != null) UnityEngine.Object.DestroyImmediate(_car);
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
    }

    public struct Swing
    {
        public float turned;        // degrees across its line it was left at
        public float maxRearSlide;  // fastest the rear axle moved sideways (m/s) while under 5 m/s
        public int yawReversals;    // times it swapped which way it was rotating (|yaw| > 10 deg/s each way)
        public int steerReversals;  // times the AI swapped lock (|steer| > 0.2 each way)
        public float speedAfter;    // m/s after the run
        public override string ToString() =>
            $"turned {turned,4:0}°: rear slide {maxRearSlide,5:0.00} m/s, yaw reversals {yawReversals,2}, " +
            $"steer reversals {steerReversals,2}, speed after {speedAfter,5:0.0}";
    }

    Swing MoveOff(float startDistance, float turnedDeg, float seconds = 8f)
    {
        if (_car != null) UnityEngine.Object.DestroyImmediate(_car);
        var trackType = Runtime("TrackBuilder");
        var track = _package.GetComponentInChildren(trackType, true);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        _car = new GameObject("SwingCar");
        var splineType = Runtime("SplineDriver");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        var spline = _car.AddComponent(splineType);
        Set(spline, "track", track);
        Set(spline, "vehicleInfo", vehicleInfo);
        Set(spline, "spriteFacesUp", false);
        Set(spline, "angleOffsetDeg", 180f);
        Set(spline, "speed", 0f);
        Set(spline, "startDistance", startDistance);
        Set(spline, "externalMotionController", true);

        var pvc = _car.AddComponent(pvcType);
        Set(pvc, "vehicleInfo", vehicleInfo);
        Set(pvc, "track", track);
        Set(pvc, "spriteFacesUp", false);
        Set(pvc, "angleOffsetDeg", 180f);
        Set(pvc, "grassTrails", false);
        Set(pvc, "enableWheelspin", false);   // as GridSpawner builds every AI car
        Set(pvc, "externalInput", true);
        Set(pvc, "damageImpairsHandling", false);
        Set(pvc, "surfaceSpray", false);
        Set(pvc, "impactDebris", false);
        var input = _car.AddComponent(inputType);

        Call(spline, "Awake");
        Call(input, "Awake");
        Call(input, "OnEnable");
        Call(pvc, "Start");
        Call(spline, "Start");

        var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        // One step first: the car seeds its pose off the spline on its first physics step, which would undo a
        // turn set before it.
        inputStep.Invoke(input, null);
        pvcStep.Invoke(pvc, null);
        splineStep.Invoke(spline, null);
        var heading = pvcType.GetProperty("HeadingDeg");
        float h0 = (float)heading.GetValue(pvc);
        pvcType.GetMethod("SeedPose").Invoke(pvc, new object[] { (Vector2)_car.transform.position, h0 + turnedDeg, 0f });
        var f = BindingFlags.Instance | BindingFlags.NonPublic;
        var vxF = pvcType.GetField("_vx", f); var vyF = pvcType.GetField("_vy", f);
        var rF = pvcType.GetField("_r", f); var bF = pvcType.GetField("_b", f);
        var lastSteer = inputType.GetProperty("LastSteer");

        var o = new Swing { turned = turnedDeg };
        int yawSign = 0, steerSign = 0;
        int steps = Mathf.RoundToInt(seconds / Time.fixedDeltaTime);
        for (int s = 0; s < steps; s++)
        {
            inputStep.Invoke(input, null);
            pvcStep.Invoke(pvc, null);
            splineStep.Invoke(spline, null);

            float vx = (float)vxF.GetValue(pvc), vy = (float)vyF.GetValue(pvc), r = (float)rF.GetValue(pvc);
            float b = (float)bF.GetValue(pvc);
            float speed = Mathf.Sqrt(vx * vx + vy * vy);
            o.speedAfter = speed;
            if (speed >= 10f) continue;
            // The tail at a crawl. Nearer 10 m/s, accelerating hard through a turn, the rear runs a normal tyre slip.
            if (speed < 5f) o.maxRearSlide = Mathf.Max(o.maxRearSlide, Mathf.Abs(vy - b * r));
            float yawDeg = r * Mathf.Rad2Deg;
            if (Mathf.Abs(yawDeg) > 10f)
            {
                int sgn = yawDeg > 0f ? 1 : -1;
                if (yawSign != 0 && sgn != yawSign) o.yawReversals++;
                yawSign = sgn;
            }
            float steer = (float)lastSteer.GetValue(input);
            if (Mathf.Abs(steer) > 0.2f)
            {
                int sgn = steer > 0f ? 1 : -1;
                if (steerSign != 0 && sgn != steerSign) o.steerReversals++;
                steerSign = sgn;
            }
        }
        return o;
    }

    static readonly float[] Starts = { 600f, 2000f, 3000f };
    static readonly float[] Turned = { 0f, 15f, -30f, 60f, -90f, 135f };

    [Test]
    [Explicit("Diagnostic: AI cars moving off from rest on Daytona, straight and turned across the road.")]
    public void MoveOffTable()
    {
        var sb = new StringBuilder("[SwingSim] AI moving off from rest at Daytona (measured under 10 m/s)");
        foreach (float d in Starts)
        foreach (float t in Turned)
            sb.Append($"\n  d{d,5:0} ").Append(MoveOff(d, t));
        Debug.Log(sb.ToString());
    }

    [Test]
    public void AnAICarMovingOffFromRestDoesNotFishtail()
    {
        var fails = new StringBuilder();
        foreach (float d in Starts)
        foreach (float t in Turned)
        {
            var o = MoveOff(d, t);
            // A gripping rear axle at crawl rolls where it points: no more than a scuff sideways, and the car turns
            // one way to line up - it doesn't rock its tail left-right-left.
            if (o.maxRearSlide > 0.5f || o.yawReversals > 1) fails.Append($"\n  d{d:0} {o}");
        }
        Assert.IsEmpty(fails.ToString(), "AI cars fishtailed moving off:" + fails);
    }

    // The same move-off for a car the spline places itself (a caution queue, the pace laps, a watched field): stopped,
    // told to be somewhere else across the road (a lane change, a yield, a nudge), then rolling away.
    public struct PlacedSwing
    {
        public float slidWhileStopped;  // metres it moved sideways while standing still
        public float maxAcross;         // most its heading pointed away from the road (deg) while under 5 m/s
        public float worstYawRatio;     // its fastest turn as a share of what full lock allows at that speed
        public override string ToString() =>
            $"slid {slidWhileStopped:0.00} m stopped, heading up to {maxAcross:0.0}° off the road under 5 m/s, " +
            $"turn rate up to {worstYawRatio:0.00} x full lock";
    }

    PlacedSwing PlacedMoveOff(float startDistance, float shiftMetres, float pathAngleDeg)
    {
        if (_car != null) UnityEngine.Object.DestroyImmediate(_car);
        var track = _package.GetComponentInChildren(Runtime("TrackBuilder"), true);
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        _car = new GameObject("PlacedCar");
        var splineType = Runtime("SplineDriver");
        var spline = _car.AddComponent(splineType);
        Set(spline, "track", track);
        Set(spline, "vehicleInfo", vehicleInfo);
        Set(spline, "spriteFacesUp", false);
        Set(spline, "angleOffsetDeg", 0f);
        Set(spline, "speed", 0f);
        Set(spline, "startDistance", startDistance);
        Set(spline, "maxPathAngleDeg", pathAngleDeg);
        Call(spline, "Awake");
        Call(spline, "Start");
        var step = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var roadHeading = splineType.GetMethod("MainTangentWorldDeg");
        var distance = splineType.GetProperty("DistanceOnTrack");
        var speedF = splineType.GetField("speed");
        var wheelbase = (float)vehicleInfo.GetType().GetField("wheelbase").GetValue(vehicleInfo);
        var lock_ = (float)vehicleInfo.GetType().GetField("maxSteeringAngle").GetValue(vehicleInfo);

        var o = new PlacedSwing();
        float dt = Time.fixedDeltaTime;
        // Held at a standstill (a speed cap of nothing, as a queue holds it), settled, then its line shifted for a
        // second while it still sits there.
        Set(spline, "aiMaxSpeedMph", 0f);
        for (int s = 0; s < 10; s++) step.Invoke(spline, null);
        Vector2 stoppedAt = _car.transform.position;
        Set(spline, "lateralOffset", shiftMetres);
        for (int s = 0; s < Mathf.RoundToInt(1f / dt); s++)
        {
            step.Invoke(spline, null);
            o.slidWhileStopped = Mathf.Max(o.slidWhileStopped, Vector2.Distance(stoppedAt, _car.transform.position));
        }
        // Let it go: the queue moves off at a crawl.
        Set(spline, "aiMaxSpeedMph", 15f);
        float prevHeading = _car.transform.eulerAngles.z;
        for (int s = 0; s < Mathf.RoundToInt(6f / dt); s++)
        {
            step.Invoke(spline, null);
            float v = (float)speedF.GetValue(spline);
            float h = _car.transform.eulerAngles.z;
            float yawRate = Mathf.Abs(Mathf.DeltaAngle(prevHeading, h)) / dt * Mathf.Deg2Rad;
            prevHeading = h;
            if (v >= 5f) continue;
            if (roadHeading != null)
            {
                float road = (float)roadHeading.Invoke(spline, new object[] { (float)distance.GetValue(spline) });
                o.maxAcross = Mathf.Max(o.maxAcross, Mathf.Abs(Mathf.DeltaAngle(road, h)));
            }
            float allowed = Mathf.Max(v, 0.05f) * Mathf.Tan(lock_ * Mathf.Deg2Rad) / wheelbase;
            o.worstYawRatio = Mathf.Max(o.worstYawRatio, yawRate / allowed);
        }
        return o;
    }

    [Test]
    [Explicit("Diagnostic: spline-placed cars told to move across while stopped, then rolling off; constraint on and off.")]
    public void PlacedMoveOffTable()
    {
        var sb = new StringBuilder("[SwingSim] spline-placed car, line shifted while stopped, then rolling off (Daytona)");
        foreach (float angle in new[] { 0f, 30f })
        foreach (float shift in new[] { 1.5f, -3f })
            sb.Append($"\n  path limit {angle,2:0}°, shift {shift,4:0.0} m: ").Append(PlacedMoveOff(600f, shift, angle));
        Debug.Log(sb.ToString());
    }

    [Test]
    public void ASplinePlacedCarNeitherSlidesSidewaysNorWhipsRoundMovingOff()
    {
        foreach (float shift in new[] { 1.5f, -3f })
        {
            var o = PlacedMoveOff(600f, shift, 30f);
            Assert.Less(o.slidWhileStopped, 0.05f, $"Shift {shift}: a stopped car moved sideways. {o}");
            Assert.LessOrEqual(o.worstYawRatio, 1.05f, $"Shift {shift}: turned faster than its steering allows. {o}");
            Assert.LessOrEqual(o.maxAcross, 31f, $"Shift {shift}: pointed further across the road than it can drive. {o}");
        }
    }

    static void Set(object target, string field, object value)
    {
        var fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"{target.GetType().Name}.{field} is gone.");
        fi.SetValue(target, value);
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(target, null);
    }
}
