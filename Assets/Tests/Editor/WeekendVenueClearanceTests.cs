using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The venue cluster keeping off the paddock's parked lots.
//
// The weekend's places — the drivers' room, the winner's circle, the fan fence, the intro stage — are laid
// out along the paddock starting just past the end of the motorhome row. That was measured off the
// motorhome lot alone, which misses the OTHER block parked in the same paddock: the team garages, one
// body-plus-canopy rig per entry, more than twice as wide per place as an RV and stacked in whatever
// direction the player's own motorhome faces. Where that block ran past the end of the RVs, the drivers'
// meeting was built inside somebody's garage — walls, chairs and a top table through a team's awning.
//
// So this is the arithmetic that stops it: fold every parked rig into spans along the paddock, start the
// cluster past all of them, and push any venue that still lands on one further along. It is all pure
// static maths so it can be checked without standing a paddock up; the scene half of it (that the rigs
// are found at all) is the runtime's job.
//
// Assembly-CSharp can't be referenced by an asmdef, so the methods are reached by reflection the same way
// PaddockLotAreaTests and PopupGarageTests reach theirs.
public class WeekendVenueClearanceTests
{
    static readonly System.Type SitesType = System.Type.GetType("WeekendVenueSites, Assembly-CSharp");

    const float RoomHalf = 8f;      // WeekendVenueSites.RoomWidth * 0.5f
    const float FenceHalf = 13f;    // FenceLength * 0.5f

    static MethodInfo Method(string name)
    {
        Assert.IsNotNull(SitesType, "WeekendVenueSites is missing from Assembly-CSharp.");
        var m = SitesType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, $"WeekendVenueSites.{name} is gone — the venue layout has been rewritten.");
        return m;
    }

    static float Clearance =>
        (float)SitesType.GetField("LotClearance", BindingFlags.NonPublic | BindingFlags.Static)
                        .GetRawConstantValue();

    static List<Vector2> Merge(params Vector2[] spans) =>
        (List<Vector2>)Method("MergeSpans").Invoke(null, new object[] { new List<Vector2>(spans) });

    static float[] LayOut(float[] wanted, float[] halves, float step, float halfLen, List<Vector2> taken) =>
        (float[])Method("LayOutCluster").Invoke(null, new object[] { wanted, halves, step, halfLen, taken });

    static float FirstFreeSpace(float halfLen, List<Vector2> parked, out float step)
    {
        var args = new object[] { halfLen, parked, null };
        float at = (float)Method("FirstFreeSpaceBeside").Invoke(null, args);
        step = (float)args[2];
        return at;
    }

    // One parked rectangle, projected onto the paddock's axes. Local min/max rather than a half-size,
    // because a garage rig is not centred on its own transform — the canopy hangs off one side.
    static Vector2 Span(Vector3 along, Vector3 outward, float band, Vector3 at, Quaternion rot,
                        Vector2 localMin, Vector2 localMax)
    {
        var spans = new List<Vector2>();
        Method("AddSpan").Invoke(null, new object[]
        {
            spans, Vector3.zero, along, outward, band, at, rot, localMin, localMax
        });
        return spans.Count > 0 ? spans[0] : new Vector2(float.NaN, float.NaN);
    }

    static bool Overlaps(float at, float half, IEnumerable<Vector2> spans)
    {
        foreach (var span in spans)
            if (at + half > span.x && at - half < span.y) return true;
        return false;
    }

    // ------------------------------------------------------------------ folding the lots into spans

    [Test]
    public void OverlappingSpansFoldIntoOne()
    {
        var merged = Merge(new Vector2(40f, 60f), new Vector2(10f, 45f), new Vector2(-5f, 5f));

        Assert.AreEqual(2, merged.Count, "Two touching runs and one apart should fold to two.");
        Assert.AreEqual(-5f, merged[0].x, 0.001f);
        Assert.AreEqual(5f, merged[0].y, 0.001f);
        Assert.AreEqual(10f, merged[1].x, 0.001f);
        Assert.AreEqual(60f, merged[1].y, 0.001f, "The far edge of the run is the furthest of its parts.");
    }

    [Test]
    public void SpansComeBackSortedAlongThePaddock()
    {
        var merged = Merge(new Vector2(100f, 110f), new Vector2(-40f, -30f), new Vector2(0f, 10f));

        for (int i = 1; i < merged.Count; i++)
            Assert.LessOrEqual(merged[i - 1].x, merged[i].x, "Spans must run up the paddock in order.");
    }

    // ------------------------------------------------------------------ what a parked rig eats

    // The garage rig is the whole point: its body sits on the transform and its canopy hangs off one side,
    // so the footprint reaches 6.5m further one way than the other. Measuring it as a centred body is how
    // a third of every rig went uncounted.
    [Test]
    public void AGarageRigsCanopyIsCountedWithItsBody()
    {
        // A rig with its length up the paddock and its canopy pitched to +X, which is the paddock's own
        // long axis here — so the canopy is exactly what decides the span.
        var min = new Vector2(-3.95f * 0.5f, -9.93f * 0.5f);
        var max = new Vector2(3.95f * 0.5f + 6.5f, 9.93f * 0.5f);
        var span = Span(Vector3.right, Vector3.up, 40f, Vector3.zero, Quaternion.identity, min, max);

        Assert.AreEqual(min.x - Clearance, span.x, 0.01f, "The body's far side is the near edge of the span.");
        Assert.AreEqual(max.x + Clearance, span.y, 0.01f,
                        "The canopy is part of the rig — a span that stops at the body leaves 6.5m of " +
                        "awning for a venue to be built inside.");
    }

    [Test]
    public void ARigParkedOffThePaddockIsNotCounted()
    {
        var half = new Vector2(2f, 5f);
        var span = Span(Vector3.right, Vector3.up, 15f, new Vector3(0f, 400f, 0f), Quaternion.identity,
                        -half, half);

        Assert.IsNaN(span.x, "A block parked hundreds of metres off the paddock cannot be collided with, " +
                             "and counting it would push the cluster off the end for nothing.");
    }

    [Test]
    public void ATurnedRigIsMeasuredAcrossItsRealFootprint()
    {
        var min = new Vector2(-2f, -5f);
        var max = new Vector2(2f, 5f);
        var turned = Span(Vector3.right, Vector3.up, 40f, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), min, max);

        // Turned a quarter, the body's 10m length is what now runs up the paddock.
        Assert.AreEqual(5f + Clearance, turned.y, 0.01f);
        Assert.AreEqual(-5f - Clearance, turned.x, 0.01f);
    }

    // ------------------------------------------------------------------ where the cluster starts

    [Test]
    public void TheClusterStartsPastTheFurthestParkedRig()
    {
        // The motorhome row, and the garage block that runs 80m past the end of it. The whole thing sits
        // down one end of the paddock, so the cluster runs forward, away from both.
        var parked = Merge(new Vector2(-200f, -140f), new Vector2(-160f, -60f));

        float basis = FirstFreeSpace(300f, parked, out float step);

        Assert.Greater(step, 0f, "With far more paddock ahead of the block the cluster should run forward.");
        Assert.Greater(basis, -60f,
                       "The cluster has to start past the GARAGES. Measured off the motorhome row alone it " +
                       "starts at -126m, which is 80m deep inside the garage block.");
    }

    [Test]
    public void TheClusterTurnsRoundWhenTheBlockIsAgainstOneEnd()
    {
        var parked = Merge(new Vector2(60f, 140f));

        float basis = FirstFreeSpace(150f, parked, out float step);

        Assert.Less(step, 0f, "Nearly all the paddock is behind the block, so the cluster runs that way.");
        Assert.Less(basis, 60f);
    }

    [Test]
    public void AnEmptyPaddockLaysTheClusterOutFromTheMiddle()
    {
        float basis = FirstFreeSpace(200f, new List<Vector2>(), out float step);

        Assert.Greater(step, 0f);
        Assert.AreEqual(14f, basis, 0.01f, "With nothing parked, the cluster starts a gap out from centre.");
    }

    // ------------------------------------------------------------------ laying the venues out

    [Test]
    public void WithNothingParkedTheVenuesGoWhereTheyWereAsked()
    {
        var wanted = new[] { 20f, 46f, 72f, 98f };
        var halves = new[] { RoomHalf, 6.25f, FenceHalf, 6f };

        float[] at = LayOut(wanted, halves, 26f, 400f, new List<Vector2>());

        for (int i = 0; i < wanted.Length; i++)
            Assert.AreEqual(wanted[i], at[i], 0.01f, $"Venue {i} moved for no reason.");
    }

    // The bug, in the shape it actually happened in: the cluster is laid out from the end of the motorhome
    // row, and the garage block reaches straight across the first two places.
    [Test]
    public void AVenueLandingOnAGarageIsPushedPastTheWholeBlock()
    {
        var parked = Merge(new Vector2(-30f, 30f), new Vector2(25f, 120f));
        var wanted = new[] { 44f, 70f, 96f, 122f };          // basis 30 + 14, then 26 apart
        var halves = new[] { RoomHalf, 6.25f, FenceHalf, 6f };

        float[] at = LayOut(wanted, halves, 26f, 400f, parked);

        for (int i = 0; i < at.Length; i++)
            Assert.IsFalse(Overlaps(at[i], halves[i], parked),
                           $"Venue {i} is still standing on a parked rig at {at[i]:0.0}m.");
    }

    [Test]
    public void TwoVenuesPushedOutOfTheSameBlockDoNotStackUp()
    {
        var parked = Merge(new Vector2(-10f, 100f));
        var wanted = new[] { 10f, 36f, 62f, 88f };           // every one of them inside the block
        var halves = new[] { RoomHalf, 6.25f, FenceHalf, 6f };

        float[] at = LayOut(wanted, halves, 26f, 400f, parked);

        for (int i = 1; i < at.Length; i++)
            Assert.GreaterOrEqual(at[i] - halves[i], at[i - 1] + halves[i - 1] - 0.01f,
                                  $"Venues {i - 1} and {i} were both pushed to the same edge and are now " +
                                  "built on top of each other.");
    }

    [Test]
    public void ThePushRunsTheWayTheClusterDoes()
    {
        var parked = Merge(new Vector2(-100f, 10f));
        var wanted = new[] { -4f, -30f, -56f, -82f };
        var halves = new[] { RoomHalf, 6.25f, FenceHalf, 6f };

        float[] at = LayOut(wanted, halves, -26f, 400f, parked);

        Assert.Less(at[0], -100f, "Running the other way, a venue is pushed off the NEAR edge of the block.");
        for (int i = 0; i < at.Length; i++)
            Assert.IsFalse(Overlaps(at[i], halves[i], parked), $"Venue {i} is still on the lot.");
    }

    [Test]
    public void NoVenueIsPushedOffTheEndOfThePaddock()
    {
        var parked = Merge(new Vector2(-20f, 40f));
        var wanted = new[] { 54f, 80f, 106f, 132f };
        var halves = new[] { RoomHalf, 6.25f, FenceHalf, 6f };

        const float HalfLen = 90f;
        float[] at = LayOut(wanted, halves, 26f, HalfLen, parked);

        for (int i = 0; i < at.Length; i++)
            Assert.LessOrEqual(Mathf.Abs(at[i]) + halves[i], HalfLen + 0.01f,
                               $"Venue {i} was placed outside the paddock rectangle.");
    }
}
