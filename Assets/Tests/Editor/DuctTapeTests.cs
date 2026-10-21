using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Draftmaster.Sim;

// EditMode coverage for where the crew stick duct tape after a repair: only where the metal was pulled
// back, never on an untouched panel, spaced out, capped, and always inside the car's outline.
public class DuctTapeTests
{
    const float Pixel = 1f / 12.8f;

    // A 5 m x 2.5 m car body, 11 x 17 vertices — the shape VehicleDamage builds.
    static Vector3[] Body()
    {
        const int vx = 11, vy = 17;
        var v = new Vector3[vx * vy];
        for (int y = 0; y < vy; y++)
            for (int x = 0; x < vx; x++)
                v[y * vx + x] = new Vector3(-2.5f + 5f * x / (vx - 1), -1.25f + 2.5f * y / (vy - 1), 0f);
        return v;
    }

    static DuctTape.Settings Settings(int maxPatches = 7) => new()
    {
        threshold = 0.1f,
        spacing = 0.8f,
        maxPatches = maxPatches,
        pixel = Pixel,
        halfSize = new Vector2(2.5f, 1.25f),
        minLengthPx = 5,
        maxLengthPx = 10,
        widthPx = 2,
        maxStripsPerPatch = 3,
    };

    // Dent the vertices within `radius` of `at` by `depth`, then repair them straight.
    static float[] RepairDent(Vector3[] b, Vector2 at, float radius, float depth)
    {
        var dented = (Vector3[])b.Clone();
        for (int i = 0; i < b.Length; i++)
            if (((Vector2)b[i] - at).magnitude <= radius) dented[i] = b[i] + Vector3.right * depth;
        var repaired = new float[b.Length];
        DuctTape.Accumulate(repaired, b, dented, (Vector3[])b.Clone());
        return repaired;
    }

    [Test]
    public void Accumulate_CountsOnlyMetalPulledBackTowardBuilt()
    {
        var b = new Vector3[3];
        var before = new[] { new Vector3(0.4f, 0f, 0f), Vector3.zero, new Vector3(0.1f, 0f, 0f) };
        var after = new[] { new Vector3(0.1f, 0f, 0f), Vector3.zero, new Vector3(0.3f, 0f, 0f) };
        var r = new float[3];
        DuctTape.Accumulate(r, b, before, after);
        Assert.AreEqual(0.3f, r[0], 1e-5f, "a dent pulled 0.3 m back straight");
        Assert.AreEqual(0f, r[1], "untouched");
        Assert.AreEqual(0f, r[2], "moved further out, not repaired");
    }

    [Test]
    public void Tape_GoesWhereTheRepairWas()
    {
        var b = Body();
        var dentAt = new Vector2(-2f, 0.8f);
        var repaired = RepairDent(b, dentAt, 0.4f, 0.5f);

        var strips = new List<DuctTape.Strip>();
        int added = DuctTape.Place(strips, 0, repaired, b, Settings(), new System.Random(1), null, out int patches);

        Assert.Greater(added, 0);
        Assert.AreEqual(strips.Count, added);
        Assert.GreaterOrEqual(patches, 1);
        foreach (var s in strips)
            Assert.Less((s.center - dentAt).magnitude, 1.0f, $"strip at {s.center} is nowhere near the repaired dent");
    }

    [Test]
    public void NoRepair_NoTape()
    {
        var b = Body();
        var strips = new List<DuctTape.Strip>();
        int added = DuctTape.Place(strips, 0, new float[b.Length], b, Settings(), new System.Random(1), null, out _);
        Assert.AreEqual(0, added);
        Assert.IsEmpty(strips);
    }

    [Test]
    public void LightScuff_BelowThreshold_NoTape()
    {
        var b = Body();
        var repaired = RepairDent(b, Vector2.zero, 0.5f, 0.05f);
        var strips = new List<DuctTape.Strip>();
        Assert.AreEqual(0, DuctTape.Place(strips, 0, repaired, b, Settings(), new System.Random(1), null, out _));
    }

    [Test]
    public void WreckedCar_PatchesAreCappedAndSpaced_AndStayOnTheBody()
    {
        var b = Body();
        var repaired = RepairDent(b, Vector2.zero, 10f, 0.6f);   // every panel straightened
        var strips = new List<DuctTape.Strip>();
        var s = Settings(maxPatches: 5);
        DuctTape.Place(strips, 0, repaired, b, s, new System.Random(7), null, out int patches);

        Assert.AreEqual(5, patches);
        Assert.LessOrEqual(strips.Count, 5 * s.maxStripsPerPatch);

        var corners = new Vector2[4];
        foreach (var strip in strips)
        {
            Assert.Greater(strip.length, strip.width, "tape is a strip, longer than it is wide");
            DuctTape.Corners(strip, strip.center, corners);
            foreach (var c in corners)
            {
                Assert.LessOrEqual(Mathf.Abs(c.x), 2.5f + 0.02f, $"corner {c} hangs off the nose or tail");
                Assert.LessOrEqual(Mathf.Abs(c.y), 1.25f + 0.02f, $"corner {c} hangs off the side");
            }
        }

        // A second repair can't add patches past the cap.
        int more = DuctTape.Place(strips, patches, RepairDent(b, Vector2.zero, 10f, 0.6f), b, s, new System.Random(8), null, out int morePatches);
        Assert.AreEqual(0, more);
        Assert.AreEqual(0, morePatches);
    }

    [Test]
    public void OffBodySpots_AreRejected()
    {
        var b = Body();
        var repaired = RepairDent(b, new Vector2(2.3f, 1.1f), 0.4f, 0.5f);
        var strips = new List<DuctTape.Strip>();
        // Pretend the whole sprite is clear pixels.
        int added = DuctTape.Place(strips, 0, repaired, b, Settings(), new System.Random(1), _ => false, out _);
        Assert.AreEqual(0, added);
    }
}
