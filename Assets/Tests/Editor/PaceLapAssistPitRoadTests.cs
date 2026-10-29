using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The formation lap starts with the field filing down pit road, and on pit road the player's speed is the pit
// limiter's business. PaceLapAssist's no-overtake hold used to run there too, reading a "gap" between the player's
// distance projected onto the main lap and the car ahead's pit-lane distance - nonsense that put the car far
// behind and backed the player off to 80% of its speed, well under the limit while the AI ran the lane at it.
public class PaceLapAssistPitRoadTests
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

    static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, All);
        Assert.IsNotNull(m, $"{target.GetType().Name}.{method} is gone.");
        m.Invoke(target, args);
    }

    static void SetPhase(string phase)
    {
        var raceStart = Runtime("RaceStart");
        var phaseType = raceStart.GetNestedType("Phase");
        raceStart.GetProperty("Current").SetValue(null, Enum.Parse(phaseType, phase));
    }

    [TestCase(true, TestName = "PlayerOnPitRoad_IsLeftToThePitLimiter")]
    [TestCase(false, TestName = "CarAheadStillOnPitRoad_DoesNotHoldThePlayerOnTrack")]
    public void FormationHold_IgnoresPitRoad(bool playerOnPitRoad)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/Daytona.prefab");
        Assert.IsNotNull(prefab, "no Daytona package");
        var pkg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var aiGo = new GameObject("PitLaneAI");
        var playerGo = new GameObject("Player");
        try
        {
            SetPhase("Formation");

            var trackType = Runtime("TrackBuilder");
            var track = (Component)pkg.GetComponentInChildren(trackType, true);
            var vehicleInfo = Resources.Load("Vehicles/Cup24");
            Assert.IsNotNull(vehicleInfo);

            // The car ahead in grid order, rolling down pit road at the limit 300 m along the lane.
            var spline = aiGo.AddComponent(Runtime("SplineDriver"));
            Set(spline, "track", track);
            Set(spline, "vehicleInfo", vehicleInfo);
            Set(spline, "usePitLane", true);
            Set(spline, "startDistance", 300f);
            Set(spline, "speed", 55f / 2.237f);
            Set(spline, "qualifyingPosition", 0);
            Call(spline, "Awake");
            Call(spline, "OnEnable");
            Call(spline, "Start");
            Assert.IsTrue((bool)spline.GetType().GetProperty("IsOnPit").GetValue(spline), "The AI car is not on pit road.");

            // The player, next on the grid: 50 m behind it on pit road, or out on the main track.
            Vector3 at;
            if (playerOnPitRoad)
            {
                var pitSamples = trackType.GetMethod("SamplePitCenterline").Invoke(track, null);
                var s = trackType.GetMethod("SamplePitAt").Invoke(track, new object[] { 250f, pitSamples });
                Vector2 local = (Vector2)s.GetType().GetField("position").GetValue(s);
                at = track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
            }
            else
            {
                var mainSamples = trackType.GetMethod("SampleCenterline").Invoke(track, null);
                var s = trackType.GetMethod("SampleAt").Invoke(track, new object[] { 1000f, mainSamples });
                Vector2 local = (Vector2)s.GetType().GetField("position").GetValue(s);
                at = track.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
            }
            playerGo.transform.position = at;
            bool onPit = (bool)trackType.GetMethod("IsOnPitSurface").Invoke(track, new object[] { at });
            Assert.AreEqual(playerOnPitRoad, onPit, "The player is not where the test put it.");

            var pvcType = Runtime("PlayerVehicleController");
            var pvc = playerGo.AddComponent(pvcType);
            Set(pvc, "vehicleInfo", vehicleInfo);
            Set(pvc, "track", track);
            Call(pvc, "SetFormationGrid", 1);
            float mainDistance = (float)trackType.GetMethod("NearestCenterlineDistance").Invoke(track, new object[] { at });
            Set(pvc, "<TrackDistance>k__BackingField", mainDistance);

            var assist = playerGo.AddComponent(Runtime("PaceLapAssist"));
            Set(assist, "pvc", pvc);
            Call(assist, "Update");

            float governor = (float)pvcType.GetField("speedGovernorMps").GetValue(pvc);
            Assert.IsTrue(float.IsPositiveInfinity(governor),
                $"The formation hold capped the player at {governor * 2.237f:0} mph " +
                (playerOnPitRoad ? "on pit road, under the pit limit." : "behind a car still on pit road."));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerGo);
            // Edit mode never calls OnDisable on these: take the car out of the static field lists by hand.
            var ai = aiGo.GetComponent(Runtime("SplineDriver"));
            if (ai != null) Call(ai, "OnDisable");
            UnityEngine.Object.DestroyImmediate(aiGo);
            UnityEngine.Object.DestroyImmediate(pkg);
            Runtime("RaceStart").GetMethod("ResetToDefault").Invoke(null, null);
        }
    }
}
