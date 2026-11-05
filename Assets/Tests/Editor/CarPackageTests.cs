using System;
using System.Reflection;
using NUnit.Framework;
using Draftmaster.Tracks;

// The oval and road-course car packages (TrackTuning.packageGrip / packageYawInertia). The road-course car is the
// one every handling number was tuned on - Watkins Glen's AI calibration, the trained lines, the player's balance -
// so the first thing pinned here is that a road course gets exactly that car back.
public class CarPackageTests
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

    static float Package(TrackTuningData t) => t.packageGrip > 0f ? t.packageGrip : 1f;
    static float Inertia(TrackTuningData t) => t.packageYawInertia > 0f ? t.packageYawInertia : 1f;

    [Test]
    public void RoadCoursesKeepTheCarTheyWereTunedOn()
    {
        foreach (var id in new[] { "WatkinsGlen", "Sonoma", "COTA", "CharlotteRoval", "RoadAmerica" })
        {
            var t = TrackTuning.ForTrack(id, TrackKind.RoadCourse);
            Assert.AreEqual(1f, Package(t), 1e-6f, $"{id}: road-course grip must be untouched");
            Assert.AreEqual(1f, Inertia(t), 1e-6f, $"{id}: road-course yaw inertia must be untouched");
        }
    }

    [Test]
    public void UnplatedOvalsRunALowerGripHeavierPackage()
    {
        var speedway = TrackTuning.For(TrackKind.Speedway);
        var shortTrack = TrackTuning.For(TrackKind.ShortTrack);
        Assert.Less(Package(speedway), 1f, "intermediates: less grip than the road car");
        Assert.Less(Package(shortTrack), 1f, "short tracks: less grip than the road car");
        Assert.Greater(Inertia(speedway), 1f, "an oval car feels heavier");
        Assert.Greater(Inertia(shortTrack), 1f);
        // Indianapolis is typed a superspeedway but runs unplated, like an intermediate.
        Assert.AreEqual(Package(speedway), Package(TrackTuning.ForTrack("Indianapolis", TrackKind.Superspeedway)), 1e-6f);
    }

    [Test]
    public void PlatedSuperspeedwaysLeaveTheirGripToThePlateSolve()
    {
        Assert.AreEqual(1f, Package(TrackTuning.ForTrack("Daytona", TrackKind.Superspeedway)), 1e-6f);
        Assert.AreEqual(1f, Package(TrackTuning.ForTrack("Talladega", TrackKind.Superspeedway)), 1e-6f);
    }

    [Test]
    public void LoadingATrackAppliesItsPackageToEveryCarsGrip()
    {
        var c = Runtime("TrackConditions");
        var apply = c.GetMethod("ApplyTrackTuning", BindingFlags.Public | BindingFlags.Static);
        var reset = c.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static);
        var effective = c.GetProperty("Effective", BindingFlags.Public | BindingFlags.Static);
        var aiEffective = c.GetProperty("AiEffective", BindingFlags.Public | BindingFlags.Static);
        var packageGrip = c.GetField("PackageGripScale", BindingFlags.Public | BindingFlags.Static);
        var inertia = c.GetField("PackageYawInertiaScale", BindingFlags.Public | BindingFlags.Static);
        try
        {
            reset.Invoke(null, null);
            apply.Invoke(null, new object[] { "" });
            float neutral = (float)effective.GetValue(null);
            float neutralAi = (float)aiEffective.GetValue(null);

            apply.Invoke(null, new object[] { "WatkinsGlen" });
            Assert.AreEqual(neutral, (float)effective.GetValue(null), 1e-6f, "Watkins Glen's grip must not move");
            Assert.AreEqual(neutralAi, (float)aiEffective.GetValue(null), 1e-6f, "Watkins Glen's AI grip must not move");
            Assert.AreEqual(1f, (float)inertia.GetValue(null), 1e-6f);

            apply.Invoke(null, new object[] { "Charlotte" });
            Assert.AreEqual(TrackTuning.OvalPackageGrip, (float)packageGrip.GetValue(null), 1e-6f);
            Assert.AreEqual(neutral * TrackTuning.OvalPackageGrip, (float)effective.GetValue(null), 1e-5f);
            Assert.AreEqual(neutralAi * TrackTuning.OvalPackageGrip, (float)aiEffective.GetValue(null), 1e-5f,
                            "the AI plan and physics read the same package");
            Assert.AreEqual(TrackTuning.OvalPackageYawInertia, (float)inertia.GetValue(null), 1e-6f);

            apply.Invoke(null, new object[] { "Martinsville" });
            Assert.AreEqual(TrackTuning.ShortTrackPackageGrip, (float)packageGrip.GetValue(null), 1e-6f);

            apply.Invoke(null, new object[] { "Daytona" });
            Assert.AreEqual(1f, (float)packageGrip.GetValue(null), 1e-6f, "the plate solve owns Daytona's grip");

            // Back to a road course: nothing carried over from the oval.
            apply.Invoke(null, new object[] { "WatkinsGlen" });
            Assert.AreEqual(neutral, (float)effective.GetValue(null), 1e-6f);
            Assert.AreEqual(1f, (float)inertia.GetValue(null), 1e-6f);

            apply.Invoke(null, new object[] { "NoSuchTrack" });
            Assert.AreEqual(1f, (float)packageGrip.GetValue(null), 1e-6f, "an unknown track keeps the road car");
        }
        finally
        {
            reset.Invoke(null, null);
        }
    }
}
