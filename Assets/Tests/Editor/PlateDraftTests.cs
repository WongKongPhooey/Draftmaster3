using Draftmaster.Sim;
using NUnit.Framework;

// Plate-track draft follows NR2003's (v / vTop)^10, with vTop the plate speed: nothing off a rolling start, all of
// it where the pack races.
public class PlateDraftTests
{
    const float Plate = 190f / 2.237f;
    static float Share(float mph) => PlateDraft.SpeedShare(mph / 2.237f, Plate);

    [Test]
    public void FullAtAndAbovePlateSpeed()
    {
        Assert.AreEqual(1f, Share(190f), 1e-5f);
        Assert.AreEqual(1f, Share(200f), 1e-5f);   // a tow carries the car past the plate
    }

    [Test]
    public void NothingWorthHavingOffARollingStart()
    {
        Assert.Less(Share(50f), 1e-5f);
        Assert.Less(Share(100f), 0.002f);   // NR2003: about 0.1% at half of top speed
        Assert.Less(Share(130f), 0.03f);
    }

    [Test]
    public void BuildsSteeplyNearTheTop()
    {
        Assert.AreEqual(0.107f, Share(152f), 0.01f);   // 80%: ~11%
        Assert.AreEqual(0.599f, Share(180.5f), 0.01f); // 95%: ~60%
        float prev = 0f;
        for (float mph = 60f; mph <= 190f; mph += 10f)
        {
            float s = Share(mph);
            Assert.GreaterOrEqual(s, prev);
            prev = s;
        }
    }

    [Test]
    public void NoPlateLeavesTheDraftAlone()
    {
        Assert.AreEqual(1f, PlateDraft.SpeedShare(20f, 0f), 1e-6f);
    }
}
