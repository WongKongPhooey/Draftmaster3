using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// On an oval of a mile or more a car coming in to pit drops below the racing surface onto the apron BEFORE it
// slows, and a car leaving pit road stays down there until the back straight before it merges back up.
public class OvalPitApronTests
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

    static string Surface(Vector3 world)
    {
        var args = new object[] { (Vector2)world, null };
        bool hit = (bool)Runtime("SurfaceField").GetMethod("TryGetSurface").Invoke(null, args);
        return hit ? args[1].ToString() : "-";
    }

    [Test]
    [Explicit("Diagnostic: pit lane against the main track at a venue - where it leaves, where it rejoins, the apron.")]
    public void PitGeometry([Values("Daytona", "Charlotte", "LongPond")] string id)
    {
        var pkg = SpawnPackage(id);
        try
        {
            var trackType = Runtime("TrackBuilder");
            var track = (Component)pkg.GetComponentInChildren(trackType, true);
            var info = trackType.GetField("track").GetValue(track);
            var infoType = info.GetType();
            var main = (IList)trackType.GetMethod("SampleCenterline").Invoke(track, null);
            var pit = (IList)trackType.GetMethod("SamplePitCenterline").Invoke(track, null);
            var st = main[0].GetType();
            float Dist(object s) => (float)st.GetField("distance").GetValue(s);
            Vector2 Pos(object s) => (Vector2)st.GetField("position").GetValue(s);
            Vector2 Tan(object s) => (Vector2)st.GetField("tangent").GetValue(s);
            float Wid(object s) => (float)st.GetField("width").GetValue(s);
            MethodInfo sampleAt = null, samplePitAt = null;
            foreach (var m in trackType.GetMethods())
            {
                if (m.Name == "SampleAt" && m.GetParameters().Length == 2) sampleAt = m;
                if (m.Name == "SamplePitAt" && m.GetParameters().Length == 2) samplePitAt = m;
            }
            float lap = Dist(main[main.Count - 1]);
            float pitLen = Dist(pit[pit.Count - 1]);
            var sb = new StringBuilder($"[PitApron] {id}: lap {lap:0} entry {(float)infoType.GetProperty("PitEntryDistanceOnLap").GetValue(info):0} " +
                $"exit {(float)infoType.GetField("pitExitDistance").GetValue(info):0} pit {pitLen:0} exitLine {(float)infoType.GetProperty("PitExitLineDistance").GetValue(info):0} " +
                $"s/f {(float)infoType.GetField("startFinishDistance").GetValue(info):0}");
            for (float pd = 0f; pd <= pitLen + 0.1f; pd += Mathf.Max(10f, pitLen / 40f))
            {
                var ps = samplePitAt.Invoke(track, new object[] { pd, pit });
                Vector3 world = track.transform.TransformPoint(Pos(ps));
                float md = (float)trackType.GetMethod("NearestCenterlineDistance").Invoke(track, new object[] { world });
                var ms = sampleAt.Invoke(track, new object[] { md, main });
                Vector2 t = Tan(ms);
                float lat = Vector2.Dot(Pos(ps) - Pos(ms), new Vector2(t.y, -t.x));
                sb.Append($"\n  pit {pd,6:0} -> main {md,6:0} lat {lat,6:0.0} w {Wid(ms):0.0}");
            }
            // Heading every 50 m round the lap.
            sb.Append("\n  headings:");
            for (float d = 0f; d < lap; d += 50f)
            {
                var ms = sampleAt.Invoke(track, new object[] { d, main });
                Vector2 t = Tan(ms);
                Vector3 w = track.transform.TransformPoint(Pos(ms) + new Vector2(t.y, -t.x) * -(Wid(ms) * 0.5f + 1.8f));
                Vector3 w2 = track.transform.TransformPoint(Pos(ms) + new Vector2(t.y, -t.x) * (Wid(ms) * 0.5f + 1.8f));
                string s1 = Surface(w), s2 = Surface(w2);
                sb.Append($"\n   {d,6:0} {Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg,7:0.0} L:{s1} R:{s2}");
            }
            Debug.Log(sb.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(pkg); }
    }

    // ------------------------------------------------------------------ the rules

    [Test]
    public void AppliesToOvalsOfAMileOrMore_NotShortTracksOrRoadCourses()
    {
        Assert.IsTrue(OvalPitApron.AppliesTo("Daytona"));
        Assert.IsTrue(OvalPitApron.AppliesTo("Talladega"));
        Assert.IsTrue(OvalPitApron.AppliesTo("Charlotte"));
        Assert.IsTrue(OvalPitApron.AppliesTo("LongPond"));
        Assert.IsFalse(OvalPitApron.AppliesTo("Martinsville"), "half-mile short track");
        Assert.IsFalse(OvalPitApron.AppliesTo("Bristol"), "half-mile short track");
        Assert.IsFalse(OvalPitApron.AppliesTo("Richmond"), "three-quarter-mile short track");
        Assert.IsFalse(OvalPitApron.AppliesTo("WatkinsGlen"), "road course");
        Assert.IsFalse(OvalPitApron.AppliesTo("NoSuchTrack"));
        Assert.IsFalse(OvalPitApron.AppliesTo(null));
        Assert.IsTrue(OvalPitApron.Applies(TrackKind.ShortTrack, 1.02f), "the rule is the length: a mile or more");
        Assert.IsFalse(OvalPitApron.Applies(TrackKind.Speedway, 0.99f));
        Assert.IsFalse(OvalPitApron.Applies(TrackKind.RoadCourse, 2.4f));
    }

    [Test]
    public void LaneSitsOnThePavedApron_OrHugsTheInsideEdgeWithoutOne()
    {
        Assert.AreEqual(-7.3f, OvalPitApron.LaneLateral(-1f, 11f, true, 1.8f, 1.2f), 1e-4f, "left infield, on the apron");
        Assert.AreEqual(7.3f, OvalPitApron.LaneLateral(1f, 11f, true, 1.8f, 1.2f), 1e-4f, "right infield");
        Assert.AreEqual(-4.3f, OvalPitApron.LaneLateral(-1f, 11f, false, 1.8f, 1.2f), 1e-4f, "no apron: inside edge of the road");
    }

    [Test]
    public void BackStraightStartsWhereTheTurnsAfterThePitExitEnd()
    {
        // A 4000 m stadium oval, anticlockwise: front straight 0-1000, turns 1-2 1000-2000 (180 deg), back
        // straight 2000-3000, turns 3-4 3000-4000.
        int n = 401;
        var dist = new float[n];
        var heading = new float[n];
        for (int i = 0; i < n; i++)
        {
            float d = i * 10f;
            dist[i] = d;
            float h = d < 1000f ? 0f : d < 2000f ? (d - 1000f) * 0.18f : d < 3000f ? 180f : 180f + (d - 3000f) * 0.18f;
            heading[i] = Mathf.DeltaAngle(0f, h);
        }
        float start = OvalPitApron.BackStraightStart(dist, heading, 4000f, 600f);
        Assert.AreEqual(2000f, start, 30f, "out of the pits on the front straight: the back straight follows turns 1-2");

        start = OvalPitApron.BackStraightStart(dist, heading, 4000f, 1500f);
        Assert.AreEqual(2000f, start, 30f, "out of the pits mid-turn: still the end of that turn");

        start = OvalPitApron.BackStraightStart(dist, heading, 4000f, 3500f);
        Assert.That(start < 30f || start > 3970f, $"wraps the lap (got {start})");
    }

    // ------------------------------------------------------------------ Daytona, driven

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static object Get(object target, string field)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        return f.GetValue(target);
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        m?.Invoke(target, null);
    }

    // One dynamic-AI car (the real PVC + SplineInputDriver + SplineDriver stack) called in to pit from the back
    // straight. It must be down on the apron before it brakes, and after pit road it must stay below the racing
    // surface until the back straight, then come back up onto it.
    [Test]
    public void Daytona_PitsFromTheApron_AndRejoinsOnTheBackStraight()
    {
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);   // green, as in a race
        var pkg = SpawnPackage("Daytona");
        var go = new GameObject("PitCar");
        try
        {
            var trackType = Runtime("TrackBuilder");
            var track = (Component)pkg.GetComponentInChildren(trackType, true);
            var info = trackType.GetField("track").GetValue(track);
            var vehicleInfo = Resources.Load("Vehicles/Cup24");
            Assert.IsNotNull(vehicleInfo);

            var splineType = Runtime("SplineDriver");
            var pvcType = Runtime("PlayerVehicleController");
            var inputType = Runtime("SplineInputDriver");
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
            Set(spline, "speed", 45f);
            Set(spline, "startDistance", 0f);
            Set(spline, "externalMotionController", true);
            var pvc = go.AddComponent(pvcType);
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
            var input = go.AddComponent(inputType);
            Call(spline, "Awake");
            Call(input, "Awake");
            Call(input, "OnEnable");
            Call(pvc, "Start");
            Call(spline, "Start");

            Assert.IsTrue((bool)splineType.GetProperty("ApronPitRoad").GetValue(spline), "Daytona pits along the apron.");

            var inputStep = inputType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var pvcStep = pvcType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var splineStep = splineType.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var approach = splineType.GetMethod("ApproachPitEntry");
            var isOnPit = splineType.GetProperty("IsOnPit");
            var distOn = splineType.GetProperty("DistanceOnTrack");
            var speedMps = pvcType.GetProperty("SpeedMps");
            var slipDeg = pvcType.GetProperty("SlipAngleDeg");
            var nearest = trackType.GetMethod("NearestCenterlineDistance");
            MethodInfo sampleAt = null;
            foreach (var m in trackType.GetMethods())
                if (m.Name == "SampleAt" && m.GetParameters().Length == 2) sampleAt = m;

            var mainSamples = trackType.GetMethod("SampleCenterline").Invoke(track, null);
            var targetMph = splineType.GetProperty("TargetMph");
            var desiredMph = splineType.GetProperty("DesiredMph");
            float lap = (float)splineType.GetProperty("TrackLength").GetValue(spline);
            float entry = (float)info.GetType().GetProperty("PitEntryDistanceOnLap").GetValue(info);
            float pitLimitMph = (int)info.GetType().GetField("pitSpeedLimit").GetValue(info);

            // Where the car physically is across the main track (+ right), off its transform.
            float PhysicalLateral(out float along)
            {
                along = (float)nearest.Invoke(track, new object[] { go.transform.position });
                var s = sampleAt.Invoke(track, new object[] { along, mainSamples });
                var st = s.GetType();
                Vector2 p = (Vector2)st.GetField("position").GetValue(s);
                Vector2 t = (Vector2)st.GetField("tangent").GetValue(s);
                Vector2 local = track.transform.InverseTransformPoint(go.transform.position);
                return Vector2.Dot(local - p, new Vector2(t.y, -t.x));
            }

            float dt = Time.fixedDeltaTime;
            int stage = 0;                 // 0 run-in, 1 pit road, 2 back out
            float approachTopMps = 0f, latAtBraking = float.NaN, mphAtCommit = -1f;
            float exitMain = -1f, minBelowAfterExit = float.MaxValue, maxSlip = 0f;
            float backStraight = -1f, side = 0f, ranAfterExit = 0f, prevMain = -1f;
            bool merged = false;
            var log = new StringBuilder("[PitApron] Daytona drive");
            const float half = 5.5f;       // 11 m of racing surface

            for (int step = 0; step < Mathf.RoundToInt(200f / dt) && !merged; step++)
            {
                bool onPit = (bool)isOnPit.GetValue(spline);
                if (stage == 0)
                {
                    if (onPit) stage = 1;
                    else
                    {
                        float gap = entry - (float)distOn.GetValue(spline);
                        if (gap < 0f) gap += lap;
                        if (gap < 25f && mphAtCommit < 0f)
                        {
                            Set(spline, "usePitLane", true);
                            mphAtCommit = (float)speedMps.GetValue(pvc) * 2.237f;
                        }
                        approach.Invoke(spline, new object[] { 7f, 200f });
                    }
                }
                else if (stage == 1 && !onPit)
                {
                    stage = 2;
                    backStraight = (float)Get(spline, "_apronBackStraight");
                    side = (float)Get(spline, "_apronInsideSide");
                }

                inputStep.Invoke(input, null);
                pvcStep.Invoke(pvc, null);
                splineStep.Invoke(spline, null);

                float v = (float)speedMps.GetValue(pvc);
                maxSlip = Mathf.Max(maxSlip, Mathf.Abs((float)slipDeg.GetValue(pvc)));
                float lat = PhysicalLateral(out float along);
                if (stage == 0)
                {
                    float gap = entry - along;
                    if (gap < 0f) gap += lap;
                    // The first step the brain asks for less than the track allows: the pit-road braking.
                    if (float.IsNaN(latAtBraking) && gap < 1500f
                        && (float)targetMph.GetValue(spline) < (float)desiredMph.GetValue(spline) - 1f)
                    {
                        latAtBraking = lat;
                        approachTopMps = v;
                        log.Append($"\n  braking for pit road from {gap:0} m out at lateral {lat:0.0}");
                    }
                }
                else if (stage == 2)
                {
                    if (exitMain < 0f)
                    {
                        exitMain = along;
                        prevMain = along;
                        log.Append($"\n  off pit road at {along:0} lat {lat:0.0}, back straight from {backStraight:0}");
                    }
                    float stepM = along - prevMain;
                    if (stepM < -lap * 0.5f) stepM += lap;
                    ranAfterExit += Mathf.Max(0f, stepM);
                    prevMain = along;
                    float toBack = backStraight - exitMain;
                    if (toBack < 0f) toBack += lap;
                    if (ranAfterExit < toBack) minBelowAfterExit = Mathf.Min(minBelowAfterExit, lat * side);
                    else if (Mathf.Abs(lat) < half - 1f)
                    {
                        merged = true;
                        log.Append($"\n  back on the racing surface {ranAfterExit - toBack:0} m into the back straight, at {along:0}");
                    }
                }
                if (step % 100 == 0)
                    log.Append($"\n  s{stage} d{along,6:0} lat{lat,6:0.0} {v * 2.237f,5:0} mph tgt {(float)targetMph.GetValue(spline),5:0}/{(float)desiredMph.GetValue(spline),5:0}" +
                               $" entry {Get(spline, "_apronEntryActive")} {(float)Get(spline, "_apronEntryLat"):0.0} exit {(float)Get(spline, "_apronExitBlend"):0.00}");
            }
            Debug.Log(log.ToString());

            Assert.IsFalse(float.IsNaN(latAtBraking), "The car never braked for pit road.");
            Assert.Greater(approachTopMps * 2.237f, 120f, "The car was not at speed when the pit-road braking began.");
            Assert.That(-latAtBraking, Is.GreaterThan(half + 0.5f),
                $"Braking for pit road began at lateral {latAtBraking:0.0} m - still on the racing surface (inside edge -{half}).\n{log}");
            Assert.That(mphAtCommit, Is.LessThan(pitLimitMph + 15f), $"Came onto pit road at {mphAtCommit:0} mph.");
            Assert.AreEqual(-1f, side, "Daytona runs anticlockwise: the infield is on the left.");
            Assert.That(backStraight > 3700f || (backStraight >= 0f && backStraight < 200f), $"back straight found at {backStraight:0}");
            Assert.GreaterOrEqual(exitMain, 0f, "The car never left pit road.");
            Assert.That(minBelowAfterExit, Is.GreaterThan(half),
                $"Between the pit exit and the back straight the car came up to {-minBelowAfterExit:0.0} m - onto the racing surface.\n{log}");
            Assert.IsTrue(merged, "The car never came back up onto the racing surface on the back straight.\n" + log);
            Assert.Less(maxSlip, 20f, "The car got out of shape.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(pkg);
        }
    }
}
