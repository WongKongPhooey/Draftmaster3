using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// What a hit in the rear corner does to an AI car, with no play mode.
//
// The AI's controller used to catch every hit on the very next physics step — yaw damping and countersteer at
// full strength the instant the car was knocked — so a car punted in the rear quarter snapped back onto its line
// as if on elastic. SplineInputDriver's Impact Upset now holds the driver's inputs for a reaction time and brings
// the catch back progressively. This builds one AI car the way AILapSimTests does (SplineDriver brain,
// PlayerVehicleController physics, SplineInputDriver between them) on the real Watkins Glen package, lets it
// settle into its lap, then shoves its rear corner sideways the way VehicleCollision would and watches what the
// car does next: a tap should be shrugged off, a real punt should spin it or put it off the road.
//
// Everything is by reflection: the runtime types live in Assembly-CSharp, which a test assembly can't reference.
public class AIImpactSimTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/WatkinsGlen.prefab";

    GameObject _package;
    GameObject _car;

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

    public struct Outcome
    {
        public float hitAt;       // track distance of the hit
        public float hitSpeed;    // m/s when it landed
        public float maxSlip;     // worst body slip (deg) in the seconds after
        public bool spun;         // the controller gave up and went into spin recovery
        public bool off;          // left the legal surface
        public override string ToString() =>
            $"d{hitAt,5:0} v{hitSpeed,5:0.0} maxSlip {maxSlip,5:0.0}°{(spun ? " SPUN" : "")}{(off ? " OFF" : "")}";
    }

    // Seconds into the run each hit lands — spread so they fall on straights and in corners.
    static readonly float[] HitTimes = { 20f, 26f, 32f, 38f, 44f, 50f, 56f, 62f };

    [Test]
    [Explicit("Diagnostic: rear-corner hits of three strengths, upset off and on, at points round Watkins Glen.")]
    public void RearCornerHitTable()
    {
        var sb = new StringBuilder("[ImpactSim] rear-corner hits at Watkins Glen (lateral dv per step x 3 steps)");
        foreach (float dv in new[] { 0.3f, 0.8f, 1.6f })
        foreach (bool upset in new[] { false, true })
        {
            sb.Append($"\n  dv {dv:0.0} upset {(upset ? "on " : "off")}:");
            int spun = 0, off = 0;
            foreach (float t in HitTimes)
            {
                var o = Hit(t, dv, upset);
                if (o.spun) spun++;
                if (o.off) off++;
                sb.Append("\n    ").Append(o);
            }
            sb.Append($"\n    => {spun} spun, {off} off of {HitTimes.Length}");
        }
        Debug.Log(sb.ToString());
    }

    [Test]
    public void ATapIsShruggedOff()
    {
        // Light contact — a nudge while racing side by side — must not throw the car away.
        foreach (float t in HitTimes)
        {
            var o = Hit(t, 0.3f, true);
            Assert.IsFalse(o.spun || o.off, $"A light tap at {o} lost the AI car.");
        }
    }

    [Test]
    public void APuntInTheRearCornerLosesTheCar()
    {
        // A real hit in the rear quarter at racing speed is how cars are spun. Most of them must be lost — not
        // caught on the next step and pulled back onto the line.
        int lost = 0;
        var sb = new StringBuilder();
        foreach (float t in HitTimes)
        {
            var o = Hit(t, 1.6f, true);
            if (o.spun || o.off) lost++;
            sb.Append("\n  ").Append(o);
        }
        Assert.GreaterOrEqual(lost, HitTimes.Length * 3 / 4, $"Only {lost} of {HitTimes.Length} hard punts lost the car:{sb}");
    }

    // Drive from the line for `hitTime` seconds, then shove the rear-left corner rightwards by `dvPerStep` m/s on
    // three consecutive steps (a push that stays in contact, as VehicleCollision delivers it), and watch 4 s.
    Outcome Hit(float hitTime, float dvPerStep, bool upset)
    {
        if (_car != null) UnityEngine.Object.DestroyImmediate(_car);
        var trackType = Runtime("TrackBuilder");
        var track = _package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        _car = new GameObject("ImpactCar");
        var splineType = Runtime("SplineDriver");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        var spline = _car.AddComponent(splineType);
        Set(spline, "track", track);
        Set(spline, "vehicleInfo", vehicleInfo);
        Set(spline, "cornerSpeedScale", 0.95f);
        Set(spline, "spriteFacesUp", false);
        Set(spline, "angleOffsetDeg", 180f);
        Set(spline, "speed", 45f);
        Set(spline, "startDistance", 0f);
        Set(spline, "externalMotionController", true);
        Set(spline, "paceMultiplier", 1.2f);

        var pvc = _car.AddComponent(pvcType);
        Set(pvc, "vehicleInfo", vehicleInfo);
        Set(pvc, "track", track);
        Set(pvc, "spriteFacesUp", false);
        Set(pvc, "angleOffsetDeg", 180f);
        Set(pvc, "grassTrails", false);
        Set(pvc, "enableWheelspin", false);
        Set(pvc, "externalInput", true);
        Set(pvc, "damageImpairsHandling", false);
        Set(pvc, "surfaceSpray", false);
        Set(pvc, "impactDebris", false);

        var input = _car.AddComponent(inputType);
        Set(input, "impactUpset", upset);

        Call(spline, "Awake");
        Call(input, "Awake");
        Call(input, "OnEnable");
        Call(pvc, "Start");
        Call(spline, "Start");

        var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var impact = pvcType.GetMethod("ApplyCarImpact");
        var distanceOnTrack = splineType.GetProperty("DistanceOnTrack");
        var speedMps = pvcType.GetProperty("SpeedMps");
        var slipDeg = pvcType.GetProperty("SlipAngleDeg");
        var heading = pvcType.GetProperty("HeadingDeg");
        var recovering = inputType.GetProperty("IsRecovering");
        var onLegal = Runtime("LapTimingManager").GetMethod("OnLegalSurface", BindingFlags.Public | BindingFlags.Static);

        float dt = Time.fixedDeltaTime;
        int hitStep = Mathf.RoundToInt(hitTime / dt);
        int endStep = hitStep + Mathf.RoundToInt(4f / dt);
        var o = new Outcome();
        for (int step = 0; step < endStep; step++)
        {
            inputStep.Invoke(input, null);
            pvcStep.Invoke(pvc, null);
            if (step >= hitStep && step < hitStep + 3)
            {
                // VehicleCollision runs after the car controllers: the hit lands between physics and the brain.
                float h = (float)heading.GetValue(pvc) * Mathf.Deg2Rad;
                Vector2 fwd = new Vector2(Mathf.Cos(h), Mathf.Sin(h));
                Vector2 left = new Vector2(-fwd.y, fwd.x);
                Vector2 corner = (Vector2)_car.transform.position - fwd * 2.2f + left * 0.95f;
                if (step == hitStep)
                {
                    o.hitAt = (float)distanceOnTrack.GetValue(spline);
                    o.hitSpeed = (float)speedMps.GetValue(pvc);
                }
                impact.Invoke(pvc, new object[] { Vector2.zero, corner, -left * dvPerStep, 0.3f });
            }
            splineStep.Invoke(spline, null);

            if (step < hitStep) continue;
            o.maxSlip = Mathf.Max(o.maxSlip, Mathf.Abs((float)slipDeg.GetValue(pvc)));
            if ((bool)recovering.GetValue(input)) o.spun = true;
            if (!(bool)onLegal.Invoke(null, new object[] { track, _car.transform.position, 1f })) o.off = true;
        }
        return o;
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
