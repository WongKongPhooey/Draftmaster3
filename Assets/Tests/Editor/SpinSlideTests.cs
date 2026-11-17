using Draftmaster.Sim;
using NUnit.Framework;

// Spin slide: a car spun by a hit lets go past an angle to its direction of travel, and the faster it is going the
// smaller that angle is.
public class SpinSlideTests
{
    const float LowMps = 13.4f, LowDeg = 60f, HighMps = 80.5f, HighDeg = 12f;

    static float Break(float mps) => SpinSlide.BreakAngleDeg(mps, LowMps, LowDeg, HighMps, HighDeg);

    [Test]
    public void TravelAngleReadsNoseAgainstTravel()
    {
        Assert.AreEqual(0f, SpinSlide.TravelAngleDeg(30f, 0f), 1e-4f);
        Assert.AreEqual(90f, SpinSlide.TravelAngleDeg(0f, 20f), 1e-4f);
        Assert.AreEqual(90f, SpinSlide.TravelAngleDeg(0f, -20f), 1e-4f);   // either side
        Assert.AreEqual(180f, SpinSlide.TravelAngleDeg(-25f, 0f), 1e-4f);  // backwards
        Assert.AreEqual(45f, SpinSlide.TravelAngleDeg(10f, -10f), 1e-4f);
        Assert.AreEqual(0f, SpinSlide.TravelAngleDeg(0f, 0f), 1e-4f);      // parked
    }

    [Test]
    public void FasterNeedsLessAngle()
    {
        Assert.AreEqual(LowDeg, Break(5f), 1e-4f);     // flat below the low point
        Assert.AreEqual(HighDeg, Break(100f), 1e-4f);  // flat above the high point
        float prev = Break(LowMps);
        for (float v = LowMps + 5f; v <= HighMps; v += 5f)
        {
            float a = Break(v);
            Assert.Less(a, prev, $"break angle should fall with speed at {v} m/s");
            prev = a;
        }
        Assert.AreEqual((LowDeg + HighDeg) * 0.5f, Break((LowMps + HighMps) * 0.5f), 1e-3f);
    }

    [Test]
    public void SameAngleBreaksAFastCarButNotASlowOne()
    {
        const float angle = 25f;
        Assert.IsTrue(SpinSlide.ShouldBreak(angle, 70f, Break(70f), 6.7f));
        Assert.IsFalse(SpinSlide.ShouldBreak(angle, 20f, Break(20f), 6.7f));
    }

    [Test]
    public void NoBreakawayAtACrawl()
    {
        Assert.IsFalse(SpinSlide.ShouldBreak(170f, 3f, Break(3f), 6.7f));
    }

    [Test]
    public void RegripsWhenSlowOrBackNearStraightAndSettled()
    {
        float b = Break(50f);
        // Still well out of line: keeps sliding.
        Assert.IsFalse(SpinSlide.ShouldRegrip(b, 50f, b, 0.5f, 10f, 45f, 5.4f));
        // Back within half the break angle and barely rotating: bites.
        Assert.IsTrue(SpinSlide.ShouldRegrip(b * 0.4f, 50f, b, 0.5f, 10f, 45f, 5.4f));
        // Near straight but mid-360: keeps sliding.
        Assert.IsFalse(SpinSlide.ShouldRegrip(b * 0.4f, 50f, b, 0.5f, 200f, 45f, 5.4f));
        Assert.IsFalse(SpinSlide.ShouldRegrip(b * 0.4f, 50f, b, 0.5f, -200f, 45f, 5.4f));
        // Backwards but scrubbed down to a crawl: bites.
        Assert.IsTrue(SpinSlide.ShouldRegrip(180f, 5f, Break(5f), 0.5f, 120f, 45f, 5.4f));
    }
}
