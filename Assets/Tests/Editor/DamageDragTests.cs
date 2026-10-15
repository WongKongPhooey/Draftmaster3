using NUnit.Framework;
using UnityEngine;
using Draftmaster.Sim;

// EditMode coverage for the damaged-bodywork drag: how dented a car reads, when that turns into dragging
// metal, and that the sparks only come while the car is moving.
public class DamageDragTests
{
    static Vector3[] Grid(int n) => new Vector3[n];

    [Test]
    public void CleanCar_HasNoDent()
    {
        var b = Grid(10);
        var lim = new float[10];
        for (int i = 0; i < 10; i++) lim[i] = 0.8f;
        Assert.AreEqual(0f, DamageDrag.DentAmount(b, (Vector3[])b.Clone(), lim, out int deepest), 1e-6f);
        Assert.AreEqual(-1, deepest);
    }

    [Test]
    public void DentAmount_IsMeanShareOfFoldableMetal_AndSkipsTheTub()
    {
        var b = Grid(4);
        var cur = Grid(4);
        var lim = new[] { 1f, 1f, 0f, 1f };            // vertex 2 is the rigid cell
        cur[0] = new Vector3(0.5f, 0f, 0f);             // half folded
        cur[1] = new Vector3(0f, 2f, 0f);               // past its limit — counts as fully folded
        cur[2] = new Vector3(5f, 0f, 0f);               // rigid: ignored even if it moved
        float dent = DamageDrag.DentAmount(b, cur, lim, out int deepest);
        Assert.AreEqual((0.5f + 1f + 0f) / 3f, dent, 1e-5f);
        Assert.AreEqual(1, deepest, "the drag point is the deepest fold on metal that can fold");
    }

    [Test]
    public void DentAmount_NullSafe()
    {
        Assert.AreEqual(0f, DamageDrag.DentAmount(null, null, null, out int deepest));
        Assert.AreEqual(-1, deepest);
    }

    [Test]
    public void Intensity_NothingBelowStart_FullAtFull_RisesBetween()
    {
        Assert.AreEqual(0f, DamageDrag.Intensity(0.05f, 0.12f, 0.35f));
        Assert.AreEqual(0f, DamageDrag.Intensity(0.12f, 0.12f, 0.35f));
        Assert.AreEqual(1f, DamageDrag.Intensity(0.35f, 0.12f, 0.35f), 1e-6f);
        Assert.AreEqual(1f, DamageDrag.Intensity(0.9f, 0.12f, 0.35f), 1e-6f);
        float lo = DamageDrag.Intensity(0.18f, 0.12f, 0.35f);
        float hi = DamageDrag.Intensity(0.28f, 0.12f, 0.35f);
        Assert.Greater(lo, 0f);
        Assert.Greater(hi, lo);
        Assert.Less(hi, 1f);
    }

    [Test]
    public void Intensity_DegenerateRange_IsAStep()
    {
        Assert.AreEqual(0f, DamageDrag.Intensity(0.1f, 0.2f, 0.2f));
        Assert.AreEqual(1f, DamageDrag.Intensity(0.2f, 0.2f, 0.2f));
    }

    [Test]
    public void Rate_NeedsDamageAndSpeed_AndGrowsWithSpeed()
    {
        Assert.AreEqual(0f, DamageDrag.Rate(0f, 40f, 4f, 45f, 160f), "undamaged car doesn't drag");
        Assert.AreEqual(0f, DamageDrag.Rate(1f, 2f, 4f, 45f, 160f), "parked or crawling car doesn't spark");
        Assert.AreEqual(160f, DamageDrag.Rate(1f, 60f, 4f, 45f, 160f), 1e-4f);
        float slow = DamageDrag.Rate(1f, 15f, 4f, 45f, 160f);
        float fast = DamageDrag.Rate(1f, 35f, 4f, 45f, 160f);
        Assert.Greater(slow, 0f);
        Assert.Greater(fast, slow);
        Assert.AreEqual(fast * 0.5f, DamageDrag.Rate(0.5f, 35f, 4f, 45f, 160f), 1e-4f);
    }
}
