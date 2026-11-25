using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// A dynamic car (the race field, the player's car on Auto: PlayerVehicleController driven by SplineInputDriver) comes
// in for a stop at Watkins Glen and must stop IN its box: inside the glowing box marker (PlayerPitBoxMarker, 4.6 x
// 8.5 m) and clear of the cars already parked in the boxes it passes.
//
// Before (2026-11-25) every car stopped 2.2 m short and 3.6 m out towards the lane - the whole car on pit road, not on
// the box strip - and the crew serviced it there. The brain's pit-road distance ran 2+ m ahead of the car (no resync on
// pit road), and the cut into the box was an 8 m S-curve for 5.5 m across - a 2 m turning circle - that a car steering
// at a crawl, aimed at a point several metres too far ahead, could not follow.
public class PitBoxArrivalTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const float HalfWidth = 1f, HalfLength = 2.4f;               // the race cars' VehicleCollision box
    const float MarkerHalfWidth = 2.3f, MarkerHalfLength = 4.25f; // PlayerPitBoxMarker 4.6 x 8.5 m

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, All);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }

    static object Get(object target, string member)
    {
        var t = target.GetType();
        var f = t.GetField(member, All);
        if (f != null) return f.GetValue(target);
        var p = t.GetProperty(member, All);
        Assert.IsNotNull(p, $"{t.Name}.{member} is gone.");
        return p.GetValue(target);
    }

    static void Call(object target, string method)
    {
        var m = target.GetType().GetMethod(method, All, null, Type.EmptyTypes, null);
        m?.Invoke(target, null);
    }

    // Gap (m) between two car bodies, separating-axis; negative = overlapping by that much.
    static float Gap(Vector2 pa, Vector2 fa, Vector2 pb, Vector2 fb)
    {
        var axes = new[] { fa, new Vector2(-fa.y, fa.x), fb, new Vector2(-fb.y, fb.x) };
        Vector2 d = pb - pa;
        float best = float.NegativeInfinity;
        foreach (var ax in axes)
        {
            float ra = HalfLength * Mathf.Abs(Vector2.Dot(fa, ax)) + HalfWidth * Mathf.Abs(Vector2.Dot(new Vector2(-fa.y, fa.x), ax));
            float rb = HalfLength * Mathf.Abs(Vector2.Dot(fb, ax)) + HalfWidth * Mathf.Abs(Vector2.Dot(new Vector2(-fb.y, fb.x), ax));
            best = Mathf.Max(best, Mathf.Abs(Vector2.Dot(d, ax)) - ra - rb);
        }
        return best;
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
    }

    [TestCase(0)]    // nearest the exit
    [TestCase(20)]
    [TestCase(43)]   // nearest the entrance
    public void DynamicCar_StopsInsideItsBox_AtWatkinsGlen(int box)
    {
        Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);   // green
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/WatkinsGlen.prefab");
        Assert.IsNotNull(prefab, "no WatkinsGlen package");
        var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var go = new GameObject("PitBoxArrivalCar");
        var pitLane = Runtime("PitLane");
        try
        {
            var trackType = Runtime("TrackBuilder");
            var track = (Component)pkg.GetComponentInChildren(trackType, true);
            var info = trackType.GetField("track").GetValue(track);
            var vehicleInfo = Resources.Load("Vehicles/Cup24");
            Assert.IsNotNull(vehicleInfo);

            var collision = (Behaviour)go.AddComponent(Runtime("VehicleCollision"));
            collision.enabled = false;   // only its size is read (PitStopController.HalfExtents)
            Set(collision, "halfExtents", new Vector2(HalfWidth, HalfLength));

            Type splineType = Runtime("SplineDriver"), pvcType = Runtime("PlayerVehicleController"),
                 inputType = Runtime("SplineInputDriver"), pscType = Runtime("PitStopController");
            var spline = go.AddComponent(splineType);
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "spriteFacesUp", false);
            Set(spline, "angleOffsetDeg", 180f);
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
            Call(spline, "OnEnable");
            Call(spline, "Start");

            // Out on the track, 300 m before pit entry, at speed.
            float entry = (float)info.GetType().GetProperty("PitEntryDistanceOnLap").GetValue(info);
            float lap = (float)Get(spline, "TrackLength");
            Set(spline, "startDistance", Mathf.Repeat(entry - 300f, lap));
            Set(spline, "speed", 40f);
            Call(spline, "PlaceAtStartDistance");

            float pitLen = (float)Get(spline, "PitLength");
            Assert.Greater(pitLen, 0f, "no pit lane");
            var fit = pitLane.GetMethod("FitBoxes").Invoke(null, new object[] { track, pitLen, 44 });
            float exitGap = (float)fit.GetType().GetField("exitGap").GetValue(fit);
            float spacing = (float)fit.GetType().GetField("spacing").GetValue(fit);
            float park = (float)Get(track, "PitBoxLaneCenterLateral");
            pitLane.GetMethod("Configure").Invoke(null, new object[] { exitGap, spacing, 44, park });

            var psc = go.AddComponent(pscType);
            Call(psc, "Awake");
            Call(psc, "Start");
            Set(psc, "forcedPit", false);
            Set(psc, "boxOverride", box);
            Set(psc, "serviceSeconds", 1f);    // the stop itself is not under test
            Set(psc, "crewWaitSeconds", 0f);
            Call(psc, "RequestStop");

            // Where the box is, and the cars already parked in the boxes either side: square on their marks, and as a
            // dynamic car actually parks (a little out towards the lane, nose in).
            var pit = trackType.GetMethod("SamplePitCenterline").Invoke(track, null);
            MethodInfo samplePitAt = null;
            foreach (var mi in trackType.GetMethods())
                if (mi.Name == "SamplePitAt" && mi.GetParameters().Length == 2) samplePitAt = mi;
            var boxDistance = pitLane.GetMethod("BoxDistance", new[] { typeof(int), typeof(float) });
            void BoxFrame(int idx, out Vector2 centre, out Vector2 along, out Vector2 across)
            {
                float d = (float)boxDistance.Invoke(null, new object[] { idx, pitLen });
                var s = samplePitAt.Invoke(track, new object[] { d, pit });
                var st = s.GetType();
                along = (Vector2)st.GetField("tangent").GetValue(s);
                across = (Vector2)st.GetField("normal").GetValue(s);
                centre = (Vector2)st.GetField("position").GetValue(s) + across * park;
            }
            BoxFrame(box, out Vector2 boxCentre, out Vector2 boxAlong, out Vector2 boxAcross);
            var parked = new List<(Vector2 pos, Vector2 fwd)>();
            for (int k = box - 1; k <= box + 5; k++)
            {
                if (k == box || k < 0 || k >= 44) continue;
                BoxFrame(k, out Vector2 c, out Vector2 a, out Vector2 n);
                parked.Add((c, a));
                parked.Add((c - n * 0.4f, Rotate(a, -12f * Mathf.Sign(park))));
            }

            var inputStep = inputType.GetMethod("FixedUpdate", All);
            var pvcStep = pvcType.GetMethod("FixedUpdate", All);
            var splineStep = splineType.GetMethod("FixedUpdate", All);
            var pscStep = pscType.GetMethod("FixedUpdate", All);
            var isOnPit = splineType.GetProperty("IsOnPit");
            var speedProp = pvcType.GetProperty("SpeedMps");
            float dt = Time.fixedDeltaTime;
            float closest = float.PositiveInfinity;
            bool stopped = false;
            Vector2 Local(Vector3 w) => track.transform.InverseTransformPoint(w);
            Vector2 Forward() => -(Vector2)track.transform.InverseTransformDirection(go.transform.right);   // angleOffsetDeg 180
            for (int step = 0; step < Mathf.RoundToInt(90f / dt); step++)
            {
                inputStep.Invoke(input, null);
                pvcStep.Invoke(pvc, null);
                splineStep.Invoke(spline, null);
                pscStep.Invoke(psc, null);
                if ((bool)isOnPit.GetValue(spline))
                {
                    Vector2 c = Local(go.transform.position), f = Forward();
                    foreach (var p in parked) closest = Mathf.Min(closest, Gap(c, f, p.pos, p.fwd));
                }
                if (Get(psc, "_state").ToString() == "Servicing" && (float)speedProp.GetValue(pvc) < 0.05f) { stopped = true; break; }
            }
            Assert.IsTrue(stopped, $"Box {box}: the car never came to a stop for service.");

            Vector2 rel = Local(go.transform.position) - boxCentre;
            Vector2 fwd = Forward();
            float alongErr = Vector2.Dot(rel, boxAlong), acrossErr = Vector2.Dot(rel, boxAcross);
            float angle = Vector2.Angle(boxAlong, fwd);
            string where = $"Box {box}: stopped {alongErr:0.00} m along, {acrossErr:0.00} m across its box centre, {angle:0} deg to the lane; " +
                           $"closest to a parked car {closest:0.00} m.";
            Debug.Log("[PitBoxArrival] " + where);

            Vector2 side = new Vector2(-fwd.y, fwd.x);
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                Vector2 corner = rel + fwd * HalfLength * a + side * HalfWidth * b;
                Assert.LessOrEqual(Mathf.Abs(Vector2.Dot(corner, boxAlong)), MarkerHalfLength, "Car hangs out of the box lengthways. " + where);
                Assert.LessOrEqual(Mathf.Abs(Vector2.Dot(corner, boxAcross)), MarkerHalfWidth, "Car hangs out of the box sideways. " + where);
            }
            Assert.Less(Mathf.Abs(alongErr), 0.75f, where);
            Assert.Less(Mathf.Abs(acrossErr), 0.75f, where);
            Assert.Less(angle, 20f, where);
            Assert.Greater(closest, 0.1f, "The car touched a car parked in another box on the way in. " + where);

            // ...and out again: across the lane clear of the car parked in the next box down, and away without
            // sitting in the box. A car stopped in its box (not out in the lane, as they used to) has the car ahead to
            // turn out round.
            float outClosest = float.PositiveInfinity, released = -1f, clearedAt = -1f;
            for (int step = 0; step < Mathf.RoundToInt(30f / dt) && (bool)isOnPit.GetValue(spline); step++)
            {
                inputStep.Invoke(input, null);
                pvcStep.Invoke(pvc, null);
                splineStep.Invoke(spline, null);
                pscStep.Invoke(psc, null);
                if (released < 0f && !(bool)Get(spline, "pitStopHold")) released = step * dt;
                if (released < 0f) continue;
                Vector2 c = Local(go.transform.position), f = Forward();
                foreach (var p in parked) outClosest = Mathf.Min(outClosest, Gap(c, f, p.pos, p.fwd));
                if (clearedAt < 0f && Vector2.Dot(c - boxCentre, boxAlong) > spacing + 10f) { clearedAt = step * dt - released; break; }
            }
            string leaving = $"Box {box}: leaving, closest to a parked car {outClosest:0.00} m, past the next box in {clearedAt:0.0} s.";
            Debug.Log("[PitBoxArrival] " + leaving);
            Assert.GreaterOrEqual(released, 0f, $"Box {box}: the car was never released from its box.");
            Assert.Greater(clearedAt, 0f, "The car never got clear of its box. " + leaving);
            Assert.Less(clearedAt, 8f, "The car crept out of its box. " + leaving);
            Assert.Greater(outClosest, 0.1f, "The car touched a car parked in another box on the way out. " + leaving);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(pkg);
            pitLane.GetMethod("Clear").Invoke(null, null);
        }
    }
}
