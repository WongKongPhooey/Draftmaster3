using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Leaving pit road onto the racing surface must be one continuous move: no step where the car - or the point the
// dynamic AI steers at - jumps sideways from the pit lane's line onto the track's. Texas (FortWorth) has a
// hand-drawn pit exit that runs under the apron through turns 1-2 and comes up onto the track on the back
// straight, metres below the racing line.
public class PitExitContinuityTests
{
    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    static GameObject SpawnPackage(string id)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{id}.prefab");
        Assert.IsNotNull(prefab, $"no package for {id}");
        var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var envType = Runtime("TrackEnvironmentBuilder");
        var env = pkg.GetComponentInChildren(envType, true);
        if (env != null) envType.GetMethod("Build").Invoke(env, null);
        return pkg;
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

    public struct Measure
    {
        public bool exited;
        public float exitAt;            // main-track distance the car came off pit road at
        public float worstAimJump;      // m: most the steering aim point moved in one step beyond the car's own travel
        public float worstCarJump;      // m: most the car moved in one step beyond its speed
        public float worstSideways;     // m/s: fastest the car moved across its own heading
        public string log;
    }

    // A car leaving a pit box at the green, driven down pit road and out onto the track until a few seconds past
    // the back straight. dynamic = the real AI stack (PlayerVehicleController + SplineInputDriver steering at
    // SplineDriver.PathPointAhead); otherwise the kinematic brain writes the transform itself.
    // lanes: the oval lane director runs too, as it does in a race on a lane-AI oval - it hands the car its lane
    // the step it is off pit road.
    static Measure Drive(string trackId, bool dynamic, int box, bool lanes)
    {
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);   // green, as in a race
        var pkg = SpawnPackage(trackId);
        var go = new GameObject("PitOutCar");
        GameObject laneDirGo = null;
        var m = new Measure { worstAimJump = 0f };
        try
        {
            var trackType = Runtime("TrackBuilder");
            var track = (Component)pkg.GetComponentInChildren(trackType, true);
            var vehicleInfo = Resources.Load("Vehicles/Cup24");
            Assert.IsNotNull(vehicleInfo);

            var splineType = Runtime("SplineDriver");
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "spawnInPit", true);
            Set(spline, "qualifyingPosition", box);
            Set(spline, "externalMotionController", dynamic);

            Component pvc = null, input = null;
            Type pvcType = Runtime("PlayerVehicleController"), inputType = Runtime("SplineInputDriver");
            if (dynamic)
            {
                pvc = go.AddComponent(pvcType);
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
                Set(pvc, "corneringStiffness", 15f);
                input = go.AddComponent(inputType);
            }
            Call(spline, "Awake");
            if (dynamic)
            {
                Call(input, "Awake");
                Call(input, "OnEnable");
                Call(pvc, "Start");
            }
            Call(spline, "OnEnable");   // into RaceField, where the lane director finds it
            Call(spline, "Start");

            Component laneDir = null;
            MethodInfo laneDirStep = null;
            if (lanes)
            {
                var laneDirType = Runtime("OvalLaneDirector");
                laneDirGo = new GameObject("OvalLaneDirector");
                laneDir = laneDirGo.AddComponent(laneDirType);
                Call(laneDir, "Awake");
                laneDirType.GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 1);
                laneDirStep = laneDirType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var isOnPit = splineType.GetProperty("IsOnPit");
            var aimAt = splineType.GetMethod("PathPointAhead");
            var commanded = splineType.GetProperty("CommandedLocalPos");
            var mphProp = splineType.GetProperty("CurrentMph");
            var nearest = trackType.GetMethod("NearestCenterlineDistance");
            MethodInfo sampleAt = null;
            foreach (var mi in trackType.GetMethods())
                if (mi.Name == "SampleAt" && mi.GetParameters().Length == 2) sampleAt = mi;
            var mainSamples = trackType.GetMethod("SampleCenterline").Invoke(track, null);

            Vector2 CarLocal() => dynamic
                ? (Vector2)track.transform.InverseTransformPoint(go.transform.position)
                : (Vector2)commanded.GetValue(spline);
            float CarMps() => dynamic ? (float)pvcType.GetProperty("SpeedMps").GetValue(pvc) : (float)mphProp.GetValue(spline) / 2.237f;
            float LateralOf(Vector2 local, out float along)
            {
                Vector3 world = track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
                along = (float)nearest.Invoke(track, new object[] { world });
                var s = sampleAt.Invoke(track, new object[] { along, mainSamples });
                var st = s.GetType();
                Vector2 p = (Vector2)st.GetField("position").GetValue(s);
                Vector2 t = (Vector2)st.GetField("tangent").GetValue(s);
                return Vector2.Dot(local - p, new Vector2(t.y, -t.x));
            }

            const float ahead = 20f;
            float dt = Time.fixedDeltaTime;
            var log = new StringBuilder($"[PitExit] {trackId} {(dynamic ? "dynamic" : "kinematic")}{(lanes ? " lanes" : "")} box {box}");
            var laneProp = splineType.GetField("ovalLane");
            Vector2 prevCar = CarLocal(), prevAim = (Vector2)aimAt.Invoke(spline, new object[] { ahead });
            float prevHeading = float.NaN;
            float sinceExit = -1f;
            bool wasOnPit = true;
            for (int step = 0; step < Mathf.RoundToInt(150f / dt); step++)
            {
                laneDirStep?.Invoke(laneDir, null);   // execution order -60: first
                if (dynamic)
                {
                    inputStep.Invoke(input, null);
                    pvcStep.Invoke(pvc, null);
                }
                splineStep.Invoke(spline, null);

                bool onPit = (bool)isOnPit.GetValue(spline);
                if (wasOnPit && !onPit)
                {
                    m.exited = true;
                    sinceExit = 0f;
                    LateralOf(CarLocal(), out m.exitAt);
                    log.Append($"\n  off pit road at {m.exitAt:0}, {CarMps() * 2.237f:0} mph");
                }
                wasOnPit = onPit;

                Vector2 car = CarLocal();
                Vector2 aim = (Vector2)aimAt.Invoke(spline, new object[] { ahead });
                float v = CarMps();
                Vector2 move = car - prevCar;
                float heading = move.sqrMagnitude > 1e-6f ? Mathf.Atan2(move.y, move.x) : prevHeading;
                // Measured from 2 s before the exit to 10 s after it: the whole seam either side.
                bool window = onPit ? false : sinceExit >= 0f && sinceExit <= 10f;
                if (onPit || window)
                {
                    float carJump = move.magnitude - v * dt;
                    // Across the car's own direction of travel: what turns the steering. (Along it the aim point
                    // slides with the car and the look-ahead, which steers nothing.)
                    Vector2 aimMove = aim - prevAim;
                    Vector2 dir = float.IsNaN(prevHeading) ? Vector2.zero : new Vector2(Mathf.Cos(prevHeading), Mathf.Sin(prevHeading));
                    float aimJump = float.IsNaN(prevHeading) ? 0f : Mathf.Abs(aimMove.x * dir.y - aimMove.y * dir.x) - 0.05f;
                    if (!onPit || (float)mphProp.GetValue(spline) > 5f)
                    {
                        if (aimJump > m.worstAimJump)
                        {
                            m.worstAimJump = aimJump;
                            LateralOf(aim, out float aimAlong);
                            log.Append($"\n  aim jumped {aimJump:0.00} m {(onPit ? "on pit road" : $"{sinceExit:0.00}s off it")} at {aimAlong:0}");
                        }
                        if (carJump > m.worstCarJump) m.worstCarJump = carJump;
                        if (!float.IsNaN(prevHeading) && move.sqrMagnitude > 1e-6f)
                        {
                            Vector2 f = new Vector2(Mathf.Cos(prevHeading), Mathf.Sin(prevHeading));
                            float side = Mathf.Abs(move.x * f.y - move.y * f.x) / dt;
                            if (side > m.worstSideways) m.worstSideways = side;
                        }
                    }
                }
                if (sinceExit >= 0f)
                {
                    if (step % 25 == 0)
                    {
                        float lat = LateralOf(car, out float along);
                        float aimLat = LateralOf(aim, out _);
                        log.Append($"\n  t+{sinceExit,5:0.00} d{along,6:0} car lat {lat,6:0.00} aim lat {aimLat,6:0.00} {v * 2.237f,4:0} mph lane {laneProp.GetValue(spline)}");
                    }
                    sinceExit += dt;
                    if (sinceExit > 10f) break;
                }
                prevCar = car;
                prevAim = aim;
                prevHeading = heading;
            }
            log.Append($"\n  worst: aim jump {m.worstAimJump:0.00} m, car jump {m.worstCarJump:0.00} m, sideways {m.worstSideways:0.0} m/s");
            m.log = log.ToString();
            Debug.Log(m.log);
            return m;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (laneDirGo != null) UnityEngine.Object.DestroyImmediate(laneDirGo);
            UnityEngine.Object.DestroyImmediate(pkg);
        }
    }

    [TestCase("FortWorth", true, false)]
    [TestCase("FortWorth", false, false)]
    [TestCase("FortWorth", true, true)]
    [TestCase("FortWorth", false, true)]
    public void LeavingPitRoad_IsOneContinuousMove(string trackId, bool dynamic, bool lanes)
    {
        var m = Drive(trackId, dynamic, 10, lanes);
        Assert.IsTrue(m.exited, "The car never left pit road.\n" + m.log);
        // Before (2026-11-10): the aim point jumped 13.9 m up onto the racing line the step the car left pit road and
        // the dynamic car darted 11 m across the track in a second; a kinematic car kinked 18.5 m/s sideways. What is
        // left at the seam (~1 m at 20 m ahead) is pit road bending differently from the track beyond the join.
        Assert.Less(m.worstCarJump, 0.1f, "The car jumped leaving pit road.\n" + m.log);
        Assert.Less(m.worstAimJump, 1.5f, "The steering aim point jumped across leaving pit road.\n" + m.log);
        Assert.Less(m.worstSideways, 4f, "The car slid sideways off pit road faster than any steer.\n" + m.log);
    }
}
