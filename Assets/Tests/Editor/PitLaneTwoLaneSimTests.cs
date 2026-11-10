using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Two cars come in to pit side by side on a real track (Daytona) and run pit road as two lanes (PitLanes): the one
// that is alongside takes the far lane, nobody touches anybody, both stop in their own boxes, and each pulls out
// into the far lane for the run to the exit. Kinematic cars (SplineDriver + PitStopController), stepped by hand.
public class PitLaneTwoLaneSimTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

    // Two 4.8 x 2 m car bodies overlapping (separating-axis test on the four edge normals).
    static bool Overlap(Vector2 pa, float ha, Vector2 pb, float hb)
    {
        Vector2 fa = new Vector2(Mathf.Cos(ha * Mathf.Deg2Rad), Mathf.Sin(ha * Mathf.Deg2Rad));
        Vector2 fb = new Vector2(Mathf.Cos(hb * Mathf.Deg2Rad), Mathf.Sin(hb * Mathf.Deg2Rad));
        var axes = new[] { fa, new Vector2(-fa.y, fa.x), fb, new Vector2(-fb.y, fb.x) };
        Vector2 d = pb - pa;
        foreach (var ax in axes)
        {
            float ra = 2.4f * Mathf.Abs(Vector2.Dot(fa, ax)) + 1f * Mathf.Abs(Vector2.Dot(new Vector2(-fa.y, fa.x), ax));
            float rb = 2.4f * Mathf.Abs(Vector2.Dot(fb, ax)) + 1f * Mathf.Abs(Vector2.Dot(new Vector2(-fb.y, fb.x), ax));
            if (Mathf.Abs(Vector2.Dot(d, ax)) > ra + rb) return false;
        }
        return true;
    }

    [Test]
    public void TwoCarsPittingSideBySide_TakeALaneEach_StopInTheirBoxes_AndLeaveByTheFarLane()
    {
        var raceStart = Runtime("RaceStart");
        var phase = raceStart.GetNestedType("Phase");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/Daytona.prefab");
        Assert.IsNotNull(prefab, "no Daytona package");
        var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var goA = new GameObject("PitA");
        var goB = new GameObject("PitB");
        var pitLane = Runtime("PitLane");
        try
        {
            raceStart.GetProperty("Current").SetValue(null, Enum.Parse(phase, "Green"));
            var trackType = Runtime("TrackBuilder");
            var track = (Component)pkg.GetComponentInChildren(trackType, true);
            var info = trackType.GetField("track").GetValue(track);
            var vehicleInfo = Resources.Load("Vehicles/Cup24");
            Assert.IsNotNull(vehicleInfo);
            float pitWidth = (float)Get(track, "PitDrivingWidth");
            float park = (float)Get(track, "PitBoxLaneCenterLateral");
            Assert.IsTrue(Draftmaster.Sim.PitLanes.TwoLanes(pitWidth, park), $"Daytona's pit road ({pitWidth:0.0} m) is one lane.");
            float farLat = Draftmaster.Sim.PitLanes.Lateral(Draftmaster.Sim.PitLanes.FarSide, pitWidth, park);

            var splineType = Runtime("SplineDriver");
            var pscType = Runtime("PitStopController");
            float entry = (float)info.GetType().GetProperty("PitEntryDistanceOnLap").GetValue(info);

            // Car A has the box nearer the entrance and comes in on the inside; car B, a car's length ahead and
            // alongside, has a box further down.
            Component Make(GameObject go, int box, float start, float lateral)
            {
                var s = go.AddComponent(splineType);
                Set(s, "track", track);
                Set(s, "vehicleInfo", vehicleInfo);
                Set(s, "qualifyingPosition", box);
                Set(s, "startDistance", start);
                Set(s, "speed", 30f);
                Set(s, "tacticalLateralOffset", lateral);
                Call(s, "Awake");
                Call(s, "OnEnable");
                Call(s, "Start");
                return s;
            }
            var a = Make(goA, 30, Mathf.Repeat(entry - 60f, 1e9f), -1.6f);
            var b = Make(goB, 26, Mathf.Repeat(entry - 58f, 1e9f), 1.6f);
            float lap = (float)Get(a, "TrackLength");
            Set(a, "startDistance", Mathf.Repeat(entry - 60f, lap)); Call(a, "PlaceAtStartDistance");
            Set(b, "startDistance", Mathf.Repeat(entry - 58f, lap)); Call(b, "PlaceAtStartDistance");

            float pitLen = (float)Get(a, "PitLength");
            Assert.Greater(pitLen, 0f, "no pit lane");
            var fit = pitLane.GetMethod("FitBoxes").Invoke(null, new object[] { track, pitLen, 44 });
            float exitGap = (float)fit.GetType().GetField("exitGap").GetValue(fit);
            float spacing = (float)fit.GetType().GetField("spacing").GetValue(fit);
            pitLane.GetMethod("Configure").Invoke(null, new object[] { exitGap, spacing, 44, park });

            Component Psc(GameObject go)
            {
                var p = go.AddComponent(pscType);
                Call(p, "Awake");
                Call(p, "Start");
                Set(p, "_forcedThreshold", 0f);
                Set(p, "serviceSeconds", 2f);
                return p;
            }
            var pa = Psc(goA);
            var pb = Psc(goB);

            var splineStep = splineType.GetMethod("FixedUpdate", All);
            var pscStep = pscType.GetMethod("FixedUpdate", All);
            float dt = Time.fixedDeltaTime;
            bool split = false, aPitted = false, bPitted = false, aDone = false, bDone = false;
            float aOutWorst = 0f, bOutWorst = 0f;
            string contact = null;
            var log = new StringBuilder("[PitLanes] Daytona two cars");
            for (int step = 0; step < Mathf.RoundToInt(120f / dt) && !(aDone && bDone); step++)
            {
                pscStep.Invoke(pa, null);
                pscStep.Invoke(pb, null);
                splineStep.Invoke(a, null);
                splineStep.Invoke(b, null);

                bool aOn = (bool)Get(a, "IsOnPit"), bOn = (bool)Get(b, "IsOnPit");
                int la = (int)Get(pa, "PitLaneIndex"), lb = (int)Get(pb, "PitLaneIndex");
                if (aOn && bOn && la >= 0 && lb >= 0 && la != lb) split = true;
                if ((bool)Get(a, "pitStopHold")) aPitted = true;
                if ((bool)Get(b, "pitStopHold")) bPitted = true;
                if (aPitted && !aOn) aDone = true;
                if (bPitted && !bOn) bDone = true;
                // Leaving the box: once the pull-out is done, on the far lane.
                if ((bool)Get(pa, "_pulledOut") && aOn) aOutWorst = Mathf.Max(aOutWorst, Mathf.Abs((float)Get(a, "lateralOffset") - farLat));
                if ((bool)Get(pb, "_pulledOut") && bOn) bOutWorst = Mathf.Max(bOutWorst, Mathf.Abs((float)Get(b, "lateralOffset") - farLat));

                if (contact == null && (aOn || bOn)
                    && Overlap((Vector2)Get(a, "CommandedLocalPos"), (float)Get(a, "CommandedHeadingDeg"),
                               (Vector2)Get(b, "CommandedLocalPos"), (float)Get(b, "CommandedHeadingDeg")))
                    contact = $"t{(step + 1) * dt:0.0} A pit {(float)Get(a, "PitProgress01") * pitLen:0} lat {(float)Get(a, "lateralOffset"):0.0} lane {la}, " +
                              $"B pit {(float)Get(b, "PitProgress01") * pitLen:0} lat {(float)Get(b, "lateralOffset"):0.0} lane {lb}";
                if (step % 50 == 0)
                    log.Append($"\n  t{(step + 1) * dt,5:0.0} A {(aOn ? "pit" : "trk")} d{(float)Get(a, "DistanceOnTrack"),6:0} lat{(float)Get(a, "lateralOffset"),5:0.0} lane {la,2} {(float)Get(a, "CurrentMph"),3:0} mph" +
                               $" | B {(bOn ? "pit" : "trk")} d{(float)Get(b, "DistanceOnTrack"),6:0} lat{(float)Get(b, "lateralOffset"),5:0.0} lane {lb,2} {(float)Get(b, "CurrentMph"),3:0} mph");
            }
            Debug.Log(log.ToString());

            Assert.IsTrue(split, "The two cars never ran pit road a lane each.\n" + log);
            Assert.IsNull(contact, "The cars touched on pit road: " + contact + "\n" + log);
            Assert.IsTrue(aPitted && bPitted, "A car never stopped in its box.\n" + log);
            Assert.IsTrue(aDone && bDone, "A car never left pit road.\n" + log);
            Assert.Less(Mathf.Max(aOutWorst, bOutWorst), 0.3f, "Out of the box, a car was not in the far lane.\n" + log);
        }
        finally
        {
            foreach (var go in new[] { goA, goB })
            {
                var s = go != null ? go.GetComponent(Runtime("SplineDriver")) : null;
                if (s != null) Call(s, "OnDisable");
                UnityEngine.Object.DestroyImmediate(go);
            }
            UnityEngine.Object.DestroyImmediate(pkg);
            pitLane.GetMethod("Clear").Invoke(null, null);
            raceStart.GetMethod("ResetToDefault").Invoke(null, null);
        }
    }
}
