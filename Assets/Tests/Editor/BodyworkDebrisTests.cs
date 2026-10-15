using NUnit.Framework;
using Draftmaster.Sim;

// EditMode coverage for bits falling off a damaged car: how many a dent knocks off, that a crush shedding a
// little every step adds up to the same as one hit, and that a knocked-loose panel's trickle dies away and
// needs the car moving.
public class BodyworkDebrisTests
{
    [Test]
    public void Pieces_NoDentNoBits()
    {
        float carry = 0f;
        Assert.AreEqual(0, BodyworkDebris.Pieces(ref carry, 0f, 260f, 18));
        Assert.AreEqual(0, BodyworkDebris.Pieces(ref carry, -0.1f, 260f, 18), "a repair sheds nothing");
        Assert.AreEqual(0f, carry);
    }

    [Test]
    public void Pieces_ScaleWithDent_AndCap()
    {
        float carry = 0f;
        Assert.AreEqual(5, BodyworkDebris.Pieces(ref carry, 0.05f, 100f, 18));
        carry = 0f;
        Assert.AreEqual(18, BodyworkDebris.Pieces(ref carry, 0.5f, 100f, 18));
        Assert.AreEqual(0f, carry, "a capped burst doesn't bank the overflow for later frames");
    }

    [Test]
    public void Pieces_CrushInSmallStepsAddsUpToOneHit()
    {
        float carry = 0f;
        int total = 0;
        for (int i = 0; i < 40; i++) total += BodyworkDebris.Pieces(ref carry, 1f / 256f, 64f, 18); // 0.25 a step
        Assert.AreEqual(10, total);
    }

    [Test]
    public void Loosen_GrowsWithDent_CapsAtOne()
    {
        Assert.AreEqual(0.4f, BodyworkDebris.Loosen(0f, 0.05f, 8f), 1e-5f);
        Assert.AreEqual(1f, BodyworkDebris.Loosen(0.8f, 0.1f, 8f), 1e-5f);
        Assert.AreEqual(0.3f, BodyworkDebris.Loosen(0.3f, 0f, 8f), 1e-5f);
    }

    [Test]
    public void Settle_HalvesEachHalfLife()
    {
        Assert.AreEqual(0.5f, BodyworkDebris.Settle(1f, 1.2f, 1.2f), 1e-5f);
        Assert.AreEqual(0.25f, BodyworkDebris.Settle(1f, 2.4f, 1.2f), 1e-5f);
        Assert.AreEqual(0f, BodyworkDebris.Settle(0f, 1f, 1.2f));
        Assert.AreEqual(0f, BodyworkDebris.Settle(1f, 1f, 0f));
    }

    [Test]
    public void TrickleRate_NeedsLoosenessAndSpeed()
    {
        Assert.AreEqual(0f, BodyworkDebris.TrickleRate(0f, 40f, 3f, 35f, 14f), "a tight panel sheds nothing");
        Assert.AreEqual(0f, BodyworkDebris.TrickleRate(1f, 2f, 3f, 35f, 14f), "a parked car sheds nothing");
        Assert.AreEqual(14f, BodyworkDebris.TrickleRate(1f, 50f, 3f, 35f, 14f), 1e-4f);
        float slow = BodyworkDebris.TrickleRate(1f, 10f, 3f, 35f, 14f);
        float fast = BodyworkDebris.TrickleRate(1f, 30f, 3f, 35f, 14f);
        Assert.Greater(slow, 0f);
        Assert.Greater(fast, slow);
        Assert.AreEqual(fast * 0.5f, BodyworkDebris.TrickleRate(0.5f, 30f, 3f, 35f, 14f), 1e-4f);
    }
}
