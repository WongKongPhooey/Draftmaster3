using NUnit.Framework;
using Draftmaster.Sim;

// The race HUD's rev counter numbering, built from each car's own rev range.
public class RevDialScaleTests
{
    [Test]
    public void CupEngine_ThousandsToTen_RedFromTheShiftPoint()
    {
        var s = RevDialScale.For(9000f, 8600f);
        Assert.AreEqual(1000f, s.MajorStep);
        Assert.AreEqual(10000f, s.TopRpm, "max on a numeral gets one more step so the needle isn't on the stop");
        Assert.AreEqual(11, s.MajorCount);
        Assert.AreEqual(8100f, s.RedFromRpm, 1f, "band starts no later than 90% of max");
    }

    [Test]
    public void LowRevEngine_HalfThousandSteps()
    {
        var s = RevDialScale.For(3000f, 2700f);
        Assert.AreEqual(500f, s.MajorStep);
        Assert.AreEqual(3500f, s.TopRpm);
        Assert.AreEqual("1.5", s.Label(1500f));
        Assert.AreEqual("2", s.Label(2000f));
    }

    [Test]
    public void HighRevEngine_NeverMoreThanTenNumeralSteps()
    {
        foreach (float max in new[] { 2000f, 4500f, 7300f, 12000f, 18000f, 25000f })
        {
            var s = RevDialScale.For(max, max * 0.95f);
            Assert.LessOrEqual(s.TopRpm / s.MajorStep, 10f, $"max {max}");
            Assert.GreaterOrEqual(s.TopRpm, max, $"max {max}");
            Assert.AreEqual(0f, s.TopRpm % s.MajorStep, 1e-3f, $"max {max}");
            Assert.AreEqual(0f, s.MajorStep % s.MinorStep, 1e-3f, $"max {max}");
        }
    }

    [Test]
    public void Needle_SweepsTwoSeventyAndClamps()
    {
        var s = RevDialScale.For(9000f, 8600f);
        Assert.AreEqual(-135f, s.Degrees(0f), 1e-3f);
        Assert.AreEqual(0f, s.Degrees(5000f), 1e-3f);
        Assert.AreEqual(135f, s.Degrees(10000f), 1e-3f);
        Assert.AreEqual(135f, s.Degrees(99999f), 1e-3f);
        Assert.AreEqual(-135f, s.Degrees(-50f), 1e-3f);
    }

    [Test]
    public void MissingRange_FallsBackToADefaultDial()
    {
        var s = RevDialScale.For(0f, 0f);
        Assert.Greater(s.TopRpm, 0f);
        Assert.Greater(s.MajorStep, 0f);
    }
}
