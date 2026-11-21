using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// A bump draft gone a little wrong at Daytona, with no play mode.
//
// Phone, 2026-11-21 (patch 20): Dillon, flat out, touched the back of Bell, who had lifted in the line - about 1 m/s
// of closing speed on the straight at 2010 m, a little off-centre. The tap yawed Bell ~8 degrees; when his reaction
// hold ended his wheel stepped straight to the pack steering cap (0.40, the slew state had run on under the hold),
// and the 1.5/s slew was then too slow to take it back off: nose -9 -> +34 degrees, spun across the track and
// the big one. Josh: the AI should take a mild bump without crashing most of the time.
//
// One AI car on the real Daytona package with Daytona's tuning (pack racing, flat out, plate), driven as in
// AIImpactSimTests, then pushed from behind at one rear corner on three consecutive steps the way VehicleCollision
// delivers a bump. Runtime types live in Assembly-CSharp, so everything is by reflection.
public class PackBumpSimTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/Daytona.prefab";

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
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "Daytona" });
    }

    [TearDown]
    public void Despawn()
    {
        if (_car != null) UnityEngine.Object.DestroyImmediate(_car);
        if (_package != null) UnityEngine.Object.DestroyImmediate(_package);
        Runtime("AIPaceCalibration").GetMethod("ApplyFor").Invoke(null, new object[] { "" });
    }

    public struct Outcome
    {
        public float hitAt, hitSpeed, maxSlip, maxSteer, maxDrift;
        public bool spun, off;
        public bool Lost => spun || off || maxSlip > 12f;
        public override string ToString() =>
            $"d{hitAt,5:0} v{hitSpeed,5:0.0} maxSlip {maxSlip,4:0.0}° steer {maxSteer:0.00} " +
            $"drift {maxDrift:0.0} m{(spun ? " SPUN" : "")}{(off ? " OFF" : "")}";
    }

    // Seconds into the run each bump lands: straights, the tri-oval and the banking.
    static readonly float[] HitTimes = { 30f, 34f, 38f, 42f, 46f, 50f, 54f, 58f, 62f, 66f, 70f, 74f };

    // A mild bump - the phone's: 1.5 m/s over three steps, a rear corner 0.8-1.2 m off centre, either side. Before the
    // fix 13 of these 24 spun (the hold ended in a step to the steering cap, then the slow slew lagged the yaw).
    [Test]
    public void AMildBumpFromBehindIsCaughtMostOfTheTime()
    {
        var sb = new StringBuilder();
        int lost = 0, total = 0;
        foreach (float t in HitTimes)
        foreach (float off in new[] { 0.8f, -1.2f })
        {
            var o = Bump(t, 0.5f, off);
            total++;
            if (o.Lost) lost++;
            sb.Append("\n  ").Append(o);
        }
        Debug.Log($"[PackBump] mild bumps: {lost} of {total} lost{sb}");
        Assert.LessOrEqual(lost, total / 10, $"{lost} of {total} mild bumps lost the car:{sb}");
    }

    // ...and a real punt still turns a car round: the AI is not made of rubber.
    [Test]
    public void AHardPuntStillSpinsTheCar()
    {
        var sb = new StringBuilder();
        int lost = 0;
        foreach (float t in HitTimes)
        {
            var o = Bump(t, 1f, 1.2f);
            if (o.Lost) lost++;
            sb.Append("\n  ").Append(o);
        }
        Assert.GreaterOrEqual(lost, HitTimes.Length * 3 / 4, $"Only {lost} of {HitTimes.Length} hard punts lost the car:{sb}");
    }

    [Test]
    [Explicit("Diagnostic: bump strength x offset table at Daytona.")]
    public void BumpTable()
    {
        var sb = new StringBuilder("[PackBump] Daytona rear bumps (forward dv per step x 3 steps, corner offset)");
        foreach (float dv in new[] { 0.35f, 0.5f, 0.75f, 1f })
        foreach (float off in new[] { 0.8f, 1.2f })
        {
            int lost = 0;
            var rows = new StringBuilder();
            foreach (float t in HitTimes)
            {
                var o = Bump(t, dv, off);
                if (o.Lost) lost++;
                rows.Append("\n      ").Append(o);
            }
            sb.Append($"\n  dv {dv:0.00} offset {off:0.0}: {lost}/{HitTimes.Length} lost").Append(rows);
        }
        Debug.Log(sb.ToString());
    }

    // Drive for `hitTime` seconds, then push the car from behind at a rear corner `offset` metres off its centreline
    // (+ = left) by `dvPerStep` m/s along its nose on three consecutive steps, and watch 4 s.
    Outcome Bump(float hitTime, float dvPerStep, float offset)
    {
        if (_car != null) UnityEngine.Object.DestroyImmediate(_car);
        var trackType = Runtime("TrackBuilder");
        var track = _package.GetComponentInChildren(trackType, true);
        Assert.IsNotNull(track, "package has no TrackBuilder");
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo.");

        _car = new GameObject("BumpCar");
        var splineType = Runtime("SplineDriver");
        var pvcType = Runtime("PlayerVehicleController");
        var inputType = Runtime("SplineInputDriver");

        var spline = _car.AddComponent(splineType);
        Set(spline, "track", track);
        Set(spline, "vehicleInfo", vehicleInfo);
        Set(spline, "spriteFacesUp", false);
        Set(spline, "angleOffsetDeg", 180f);
        Set(spline, "speed", 80f);
        Set(spline, "startDistance", 0f);
        Set(spline, "externalMotionController", true);

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
        var lastSteer = inputType.GetProperty("LastSteer");
        var cross = inputType.GetProperty("LastCrossTrackMetres");
        var onLegal = Runtime("LapTimingManager").GetMethod("OnLegalSurface", BindingFlags.Public | BindingFlags.Static);

        float dt = Time.fixedDeltaTime;
        int hitStep = Mathf.RoundToInt(hitTime / dt);
        int endStep = hitStep + Mathf.RoundToInt(4f / dt);
        var o = new Outcome();
        float crossAtHit = 0f;
        for (int step = 0; step < endStep; step++)
        {
            inputStep.Invoke(input, null);
            pvcStep.Invoke(pvc, null);
            if (step >= hitStep && step < hitStep + 3)
            {
                float h = (float)heading.GetValue(pvc) * Mathf.Deg2Rad;
                Vector2 fwd = new Vector2(Mathf.Cos(h), Mathf.Sin(h));
                Vector2 left = new Vector2(-fwd.y, fwd.x);
                Vector2 corner = (Vector2)_car.transform.position - fwd * 2.5f + left * offset;
                if (step == hitStep)
                {
                    o.hitAt = (float)distanceOnTrack.GetValue(spline);
                    o.hitSpeed = (float)speedMps.GetValue(pvc);
                    crossAtHit = cross != null ? (float)cross.GetValue(input) : 0f;
                }
                impact.Invoke(pvc, new object[] { Vector2.zero, corner, fwd * dvPerStep, 0.2f });
            }
            splineStep.Invoke(spline, null);

            if (step < hitStep) continue;
            float slip = Mathf.Abs((float)slipDeg.GetValue(pvc));
            o.maxSlip = Mathf.Max(o.maxSlip, slip);
            o.maxSteer = Mathf.Max(o.maxSteer, Mathf.Abs((float)lastSteer.GetValue(input)));
            if (cross != null) o.maxDrift = Mathf.Max(o.maxDrift, Mathf.Abs((float)cross.GetValue(input) - crossAtHit));
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
