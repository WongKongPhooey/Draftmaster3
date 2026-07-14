using NUnit.Framework;
using UnityEngine;
using Draftmaster.Fans;

// EditMode coverage for the fan-appeal decision math (the part that can't be play-tested unfocused).
// Verifies persistence/clamping and the "more appeal -> more fans" count curve.
public class FanAppealTests
{
    const string Key = "fan.appeal"; // must match FanAppeal's private key

    [SetUp]
    public void ClearBefore() => PlayerPrefs.DeleteKey(Key);

    [TearDown]
    public void ClearAfter() => PlayerPrefs.DeleteKey(Key);

    [Test]
    public void Unset_ReturnsDefault()
    {
        Assert.AreEqual(FanAppeal.Default, FanAppeal.Value, 1e-4f);
    }

    [Test]
    public void Value_ClampsToRange()
    {
        FanAppeal.Value = 999f;
        Assert.AreEqual(FanAppeal.Max, FanAppeal.Value, 1e-4f);
        FanAppeal.Value = -50f;
        Assert.AreEqual(FanAppeal.Min, FanAppeal.Value, 1e-4f);
    }

    [Test]
    public void Add_RaisesAndClampsAtMax()
    {
        FanAppeal.Value = 99f;
        float result = FanAppeal.Add(10f);
        Assert.AreEqual(FanAppeal.Max, result, 1e-4f);
        Assert.AreEqual(FanAppeal.Max, FanAppeal.Value, 1e-4f);
    }

    [Test]
    public void Add_ReducesAndClampsAtMin()
    {
        FanAppeal.Value = 2f;
        float result = FanAppeal.Add(-10f);
        Assert.AreEqual(FanAppeal.Min, result, 1e-4f);
        Assert.AreEqual(FanAppeal.Min, FanAppeal.Value, 1e-4f);
    }

    [Test]
    public void FanCount_ScalesFromMinToMax()
    {
        Assert.AreEqual(0, FanAppeal.FanCountForAppeal(0f, 0, 6), "0 appeal -> min fans");
        Assert.AreEqual(6, FanAppeal.FanCountForAppeal(100f, 0, 6), "100 appeal -> max fans");
        Assert.AreEqual(3, FanAppeal.FanCountForAppeal(50f, 0, 6), "half appeal -> midpoint");
    }

    [Test]
    public void FanCount_IsMonotonicNonDecreasing()
    {
        int prev = -1;
        for (float a = 0f; a <= 100f; a += 5f)
        {
            int c = FanAppeal.FanCountForAppeal(a, 0, 6);
            Assert.GreaterOrEqual(c, prev, $"count should not drop as appeal rises (appeal {a})");
            prev = c;
        }
    }

    [Test]
    public void FanCount_ClampsAppealAndHandlesSwappedBounds()
    {
        Assert.AreEqual(0, FanAppeal.FanCountForAppeal(-20f, 0, 6), "below-range appeal clamps to min");
        Assert.AreEqual(6, FanAppeal.FanCountForAppeal(200f, 0, 6), "above-range appeal clamps to max");
        // Swapped bounds shouldn't explode — treated as [min,max].
        Assert.AreEqual(6, FanAppeal.FanCountForAppeal(100f, 6, 0));
    }
}
