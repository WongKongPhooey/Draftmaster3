using System;
using System.Reflection;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Banked turns: the tyres loaded harder by cornering into the bank, plus gravity down it.
public class BankedGripTests
{
    [Test]
    public void LevelGroundChangesNothing()
    {
        Assert.AreEqual(20f, BankedGrip.Capacity(20f, 0f), 1e-4f);
        Assert.AreEqual(1f, BankedGrip.LoadFactor(0f, 15f), 1e-4f);
        Assert.AreEqual(0f, BankedGrip.DownslopeAccel(0f), 1e-4f);
    }

    [Test]
    public void MoreBankHoldsMore()
    {
        float flat = 18f;   // ~1.8 g, the game's AI grip
        float bristol = BankedGrip.Capacity(flat, 26f);
        float daytona = BankedGrip.Capacity(flat, 31f);
        float backStretch = BankedGrip.Capacity(flat, 3f);
        Assert.Greater(backStretch, flat);
        Assert.Greater(bristol, backStretch);
        Assert.Greater(daytona, bristol);
    }

    [Test]
    public void DaytonaIsFlatOutButNotLimitless()
    {
        // A Cup car at 190 mph (85 m/s) round Daytona's ~300 m turns needs ~24 m/s².
        float daytona = BankedGrip.Capacity(18f, 31f);
        Assert.Greater(daytona, 24f, "Daytona's banking should hold a Cup car flat out");
        Assert.Less(daytona, 18f * BankedGrip.MaxLoadFactor + BankedGrip.G, "the load factor is capped");
    }

    [Test]
    public void LoadFactorIsCapped()
    {
        Assert.AreEqual(BankedGrip.MaxLoadFactor, BankedGrip.LoadFactor(31f, 1000f), 1e-4f);
    }
}

// Progressive banking (the top groove is steeper) and running off the banking (the top of the road comes down as a
// turn's banking eases onto the straight, so a car up there gains speed on the exit).
public class ProgressiveBankingTests
{
    [Test]
    public void TheMiddleOfTheRoadIsThePublishedBanking()
    {
        Assert.AreEqual(26f, BankedGrip.BankAcross(26f, 0.5f), 1e-4f);
        Assert.Greater(BankedGrip.BankAcross(26f, 1f), 26f, "the top groove is steeper");
        Assert.Less(BankedGrip.BankAcross(26f, 0f), 26f, "the bottom groove is flatter");
        Assert.AreEqual(0f, BankedGrip.BankAcross(0f, 1f), 1e-4f, "a flat road stays flat all the way across");
    }

    [Test]
    public void TheSteeperTheTrackTheMoreTheHighLineGains()
    {
        float flat = 18f;
        float Gain(float bank) => BankedGrip.Capacity(flat, BankedGrip.BankAcross(bank, 1f))
                                - BankedGrip.Capacity(flat, BankedGrip.BankAcross(bank, 0f));
        Assert.Greater(Gain(12f), 0f, "the high line holds more than the low one");
        Assert.Greater(Gain(24f), Gain(12f), "a steeper track gives the high line more");
        Assert.Greater(Gain(31f), Gain(24f));
    }

    [Test]
    public void TheRoadRisesFromTheApronToTheWall()
    {
        Assert.AreEqual(0f, BankedGrip.SurfaceHeight(24f, 0f, 18f), 1e-4f, "the bottom edge is level with the apron");
        float mid = BankedGrip.SurfaceHeight(24f, 0.5f, 18f);
        float top = BankedGrip.SurfaceHeight(24f, 1f, 18f);
        Assert.Greater(mid, 0f);
        Assert.Greater(top, mid);
        // Steeper toward the top: the upper half of the road climbs more than the lower half.
        Assert.Greater(top - mid, mid);
        Assert.AreEqual(0f, BankedGrip.SurfaceHeight(0f, 1f, 18f), 1e-4f);
    }

    [Test]
    public void ACarHighUpGainsSpeedOffTheBankingOnTheExit()
    {
        const float width = 18f, step = 8f;
        // Exiting: 24 degrees behind, 20 ahead.
        float top = BankedGrip.AlongTrackAccel(24f, 20f, step, 1f, width);
        float mid = BankedGrip.AlongTrackAccel(24f, 20f, step, 0.5f, width);
        float bottom = BankedGrip.AlongTrackAccel(24f, 20f, step, 0f, width);
        Assert.Greater(top, 0f, "the top of the road comes down on the exit: downhill, forward");
        Assert.Greater(top, mid, "the higher up, the more it runs off the banking");
        Assert.AreEqual(0f, bottom, 1e-4f, "the bottom edge stays level with the apron");
        // Running in high costs the same.
        Assert.AreEqual(-top, BankedGrip.AlongTrackAccel(20f, 24f, step, 1f, width), 1e-4f);
        Assert.AreEqual(0f, BankedGrip.AlongTrackAccel(24f, 24f, step, 1f, width), 1e-4f, "mid-corner nothing changes");
    }

    [Test]
    public void TheSpeedGainedOffTheBankingIsTheHeightLost()
    {
        // A car at the wall at 150 mph rolls off 24 degrees of banking onto a 5 degree straight over 60 m.
        const float width = 18f, length = 60f, ds = 0.5f;
        float Bank(float s) => Mathf.Lerp(24f, 5f, Mathf.Clamp01(s / length));
        float v = 67f, v0 = v;
        for (float s = 0f; s < length; s += ds)
        {
            float a = BankedGrip.AlongTrackAccel(Bank(s), Bank(s + ds), ds, 1f, width);
            v = Mathf.Sqrt(v * v + 2f * a * ds);   // v dv = a ds
        }
        float drop = BankedGrip.SurfaceHeight(24f, 1f, width) - BankedGrip.SurfaceHeight(5f, 1f, width);
        Assert.Greater(drop, 3f);
        Assert.AreEqual(Mathf.Sqrt(v0 * v0 + 2f * BankedGrip.G * drop), v, 0.05f, "energy: v² gained = 2g x height lost");
        Assert.Greater(v - v0, 0.4f, "a real, if small, exit-speed gain (~1-2 mph)");
    }
}

// The same rules where the game applies them: TrackBuilder.BankAt (the physics) and SplineDriver's corner plan (the AI).
public class ProgressiveBankingTrackTests
{
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

    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static T Get<T>(object o, string member)
    {
        var t = o.GetType();
        var f = t.GetField(member, Flags);
        if (f != null) return (T)f.GetValue(o);
        var p = t.GetProperty(member, Flags);
        Assert.IsNotNull(p, $"{t.Name}.{member} is gone.");
        return (T)p.GetValue(o);
    }

    [Test]
    public void BristolBanksTheTopGrooveSteeperAndRunsItOffTheBankingOnTheExit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TrackPackages/Bristol.prefab");
        Assert.IsNotNull(prefab, "Bristol package missing");
        var package = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var probe = new GameObject("BankProbe");
        try
        {
            var builderType = Runtime("TrackBuilder");
            var builder = (Component)package.GetComponentInChildren(builderType, true);
            Assert.IsNotNull(builder, "Bristol package has no TrackBuilder");
            var info = Get<ScriptableObject>(builder, "track");
            var bankingAt = info.GetType().GetMethod("BankingAt");
            float BankingAt(float d) => (float)bankingAt.Invoke(info, new object[] { d, 40f });
            bool turnsLeft = (bool)info.GetType().GetMethod("LapTurnsLeft").Invoke(info, null);
            float lap = (float)info.GetType().GetMethod("TotalLength").Invoke(info, null);

            // Mid-turn: the steepest point of the lap. Exit: where the banking falls fastest.
            float midTurn = 0f, exit = 0f, steepest = 0f, fastestDrop = 0f;
            for (float d = 0f; d < lap; d += 2f)
            {
                float b = BankingAt(d);
                if (b > steepest) { steepest = b; midTurn = d; }
                float drop = BankingAt(d - 4f) - BankingAt(d + 4f);
                if (drop > fastestDrop) { fastestDrop = drop; exit = d; }
            }
            Assert.Greater(steepest, 20f, "Bristol's turns should be banked");
            Assert.Greater(fastestDrop, 1f, "Bristol's banking should ease onto the straights");

            var sampleAt = builderType.GetMethod("SampleAt");
            var bankAt = builderType.GetMethod("BankAt", new[] { typeof(float), typeof(Vector3), typeof(float).MakeByRefType(),
                typeof(Vector2).MakeByRefType(), typeof(Vector2).MakeByRefType() });
            Assert.IsNotNull(bankAt, "TrackBuilder.BankAt with the slope pull is gone");

            // Banking and along-track pull at `across` of the way up the road (0 bottom, 1 top).
            void At(float d, float across, out float bank, out float along)
            {
                object s = sampleAt.Invoke(builder, new object[] { d, null });
                Vector2 pos = Get<Vector2>(s, "position"), nrm = Get<Vector2>(s, "normal"), tan = Get<Vector2>(s, "tangent");
                float w = Get<float>(s, "width");
                Vector2 up = turnsLeft ? nrm : -nrm;
                Vector2 local = pos + up * ((across - 0.5f) * (w - 1f));
                Vector3 world = builder.transform.TransformPoint(new Vector3(local.x, local.y, 0f));
                var args = new object[] { d, world, 0f, Vector2.zero, Vector2.zero };
                bankAt.Invoke(builder, args);
                bank = (float)args[2];
                Vector2 fwd = builder.transform.TransformDirection(new Vector3(tan.x, tan.y, 0f));
                along = Vector2.Dot((Vector2)args[4], fwd.normalized);
            }

            At(midTurn, 0.05f, out float bankLow, out _);
            At(midTurn, 0.5f, out float bankMid, out _);
            At(midTurn, 0.95f, out float bankHigh, out _);
            Assert.Greater(bankHigh, bankMid, "the top groove should sit on steeper banking");
            Assert.Greater(bankMid, bankLow);
            Assert.AreEqual(BankingAt(midTurn), bankMid, 0.5f, "the middle of the road is the published banking");

            At(exit, 0.05f, out _, out float alongLow);
            At(exit, 0.95f, out _, out float alongHigh);
            Assert.Greater(alongHigh, 0.2f, "a car up the banking should be pulled forward as the banking eases on the exit");
            Assert.Greater(alongHigh, alongLow * 2f, "the higher up, the more it runs off the banking");

            // The AI plans what the physics holds: same curvature, higher up the bank = faster.
            var splineType = Runtime("SplineDriver");
            var spline = probe.AddComponent(splineType);
            splineType.GetField("track", Flags).SetValue(spline, builder);
            splineType.GetField("vehicleInfo", Flags).SetValue(spline, Resources.Load("Vehicles/Cup24"));
            splineType.GetMethod("Rebuild").Invoke(spline, null);
            int segIdx = (int)splineType.GetMethod("SegmentIndexAt", Flags).Invoke(spline, new object[] { midTurn });
            var target = splineType.GetMethod("ComputeTargetSpeedForCurvature", Flags);
            float Plan(float across) => (float)target.Invoke(spline, new object[] { 1f / 60f, segIdx, true, across });
            Assert.Greater(Plan(1f), Plan(0.5f), "the AI should plan the high line faster on the same radius");
            Assert.Greater(Plan(0.5f), Plan(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
            UnityEngine.Object.DestroyImmediate(package);
        }
    }
}
