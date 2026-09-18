using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

// A skid pad for the car model the AI drives.
//
// The AI plans its corner speeds from v = sqrt(R * aLat), with aLat taken from maxLateralG and the track's
// grip multipliers. That is the tyre's PEAK friction. Whether the car can actually hold that in a steady
// corner is a different question — it depends on the steering lock left at speed, the balance between the
// axles and the load on each — and when the two disagree the AI turns in at a speed the car cannot corner
// at and slides off the road. That is what practice at Watkins Glen showed: cars asked for ~3 g, holding ~2.3.
//
// This drives the real PlayerVehicleController (by reflection: it lives in Assembly-CSharp) round steady
// circles at a sweep of speeds and steering inputs, and reports the most lateral acceleration it holds
// without the rear letting go.
public class AIGripCalibrationTests
{
    static Type Runtime(string name)
    {
        var t = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(t, $"{name} is missing from Assembly-CSharp.");
        return t;
    }

    [Test]
    [Explicit("Diagnostic: prints the car's real steady-state cornering limit against what the AI assumes.")]
    public void SkidPad()
    {
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo in Resources/Vehicles.");

        var conditions = Runtime("TrackConditions");
        float aiEffective = (float)conditions.GetProperty("AiEffective").GetValue(null);
        float maxLatG = (float)vehicleInfo.GetType().GetField("maxLateralG").GetValue(vehicleInfo);
        float assumed = maxLatG * aiEffective * 9.81f;

        var sb = new StringBuilder();
        sb.AppendLine($"[SkidPad] Cup24 maxLateralG {maxLatG:0.00} x AiEffective {aiEffective:0.00} -> AI assumes {assumed:0.0} m/s² ({assumed / 9.81f:0.00} g)");
        foreach (float v in new[] { 25f, 35f, 45f, 55f, 65f })
        {
            float best = 0f, bestSteer = 0f, bestSlip = 0f;
            for (float steer = 0.05f; steer <= 1.001f; steer += 0.05f)
            {
                if (!Circle(vehicleInfo, v, steer, out float aLat, out float slip, out float speedHeld)) continue;
                if (aLat > best) { best = aLat; bestSteer = steer; bestSlip = slip; }
            }
            sb.AppendLine($"  {v:0} m/s: holds {best:0.0} m/s² ({best / 9.81f:0.00} g, {best / assumed:0%} of assumed) at steer {bestSteer:0.00}, slip {bestSlip:0.0}°");
        }
        Debug.Log(sb.ToString());
    }

    [Test]
    public void TheAIPlansCornersOnGripTheCarCanActuallyHold()
    {
        // AIGrip's table is what the speed profile and the grip governor corner on. If it ever claims more
        // of the peak than the car holds on the skid pad, the AI are back to turning in faster than the car
        // can go round — and practice fills up with cars in the run-off again. If it claims far less, the AI
        // are needlessly slow. Either means the physics changed and the table needs re-measuring.
        var vehicleInfo = Resources.Load("Vehicles/Cup24");
        Assert.IsNotNull(vehicleInfo, "No Cup24 VehicleInfo in Resources/Vehicles.");
        var conditions = Runtime("TrackConditions");
        float peak = (float)vehicleInfo.GetType().GetField("maxLateralG").GetValue(vehicleInfo)
                   * (float)conditions.GetProperty("AiEffective").GetValue(null) * 9.81f;
        var usable = Runtime("AIGrip").GetMethod("UsableFraction");

        foreach (float v in new[] { 25f, 45f, 65f })
        {
            float best = 0f;
            for (float steer = 0.05f; steer <= 1.001f; steer += 0.05f)
                if (Circle(vehicleInfo, v, steer, out float aLat, out _, out _)) best = Mathf.Max(best, aLat);

            float measured = best / peak;
            float planned = (float)usable.Invoke(null, new object[] { v });
            Assert.LessOrEqual(planned, measured,
                               $"At {v} m/s the AI plans on {planned:0%} of peak grip but the car only holds {measured:0%}.");
            Assert.Greater(planned, measured - 0.12f,
                           $"At {v} m/s the AI plans on {planned:0%} of peak grip, far under the {measured:0%} the car holds.");
        }
    }

    // Drive one steady circle: fixed steering input, throttle/brake holding `speed`. False if the car
    // spins, can't hold the speed, or never settles.
    static bool Circle(UnityEngine.Object vehicleInfo, float speed, float steer, out float aLat, out float slip, out float held)
    {
        aLat = slip = held = 0f;
        var go = new GameObject("SkidPadCar");
        try
        {
            var type = Runtime("PlayerVehicleController");
            var pvc = go.AddComponent(type);
            Set(pvc, "vehicleInfo", vehicleInfo);
            Set(pvc, "externalInput", true);
            Set(pvc, "enableWear", false);
            Set(pvc, "enableDraft", false);
            Set(pvc, "grassTrails", false);
            Set(pvc, "damageImpairsHandling", false);

            var fixedUpdate = type.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var setInput = type.GetMethod("SetInput");
            var seed = type.GetMethod("SeedPose");
            var speedProp = type.GetProperty("SpeedMps");
            var slipProp = type.GetProperty("SlipAngleDeg");
            var headingProp = type.GetProperty("HeadingDeg");

            seed.Invoke(pvc, new object[] { Vector2.zero, 0f, speed });
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.RoundToInt(6f / dt);
            int window = Mathf.RoundToInt(1.5f / dt);
            float headingStart = 0f, speedSum = 0f, slipSum = 0f;

            for (int i = 0; i < steps; i++)
            {
                float v = (float)speedProp.GetValue(pvc);
                float err = speed - v;
                setInput.Invoke(pvc, new object[] { steer, Mathf.Clamp01(err * 2f), Mathf.Clamp01(-err * 2f) });
                fixedUpdate.Invoke(pvc, null);

                float s = (float)slipProp.GetValue(pvc);
                if (Mathf.Abs(s) > 25f) return false;   // let go
                if (i == steps - window) headingStart = (float)headingProp.GetValue(pvc);
                if (i >= steps - window) { speedSum += (float)speedProp.GetValue(pvc); slipSum += s; }
            }

            held = speedSum / window;
            if (Mathf.Abs(held - speed) > speed * 0.05f) return false;   // couldn't hold the speed
            float yawRate = Mathf.Abs(Mathf.DeltaAngle(headingStart, (float)headingProp.GetValue(pvc))) * Mathf.Deg2Rad / (window * dt);
            aLat = held * yawRate;
            slip = slipSum / window;
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"{target.GetType().Name}.{field} is gone.");
        f.SetValue(target, value);
    }
}
