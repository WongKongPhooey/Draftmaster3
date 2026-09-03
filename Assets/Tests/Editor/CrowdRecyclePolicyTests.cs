using Draftmaster.Crowd;
using NUnit.Framework;
using UnityEngine;

// Rules for taking a filler NPC out of the far end of the paddock and putting it back just out of shot,
// so the crowd stays clustered around the player. All pure — no scene, no play mode.
public class CrowdRecyclePolicyTests
{
    static CrowdRecycleTuning Tuning => CrowdRecycleTuning.Default;   // 100m out, 14-45m back, cap 280

    // A paddock the shape PaddockSpawner builds: a long strip alongside the pit lane. Axis-aligned here
    // so the expected answers are readable; the rotated case gets its own test.
    static CrowdRect Paddock(float halfLength = 200f, float halfDepth = 15f) =>
        new CrowdRect(Vector2.zero, Vector2.right, Vector2.up, halfLength, halfDepth);

    // ---------------------------------------------------------------- who gets recycled

    [Test]
    public void NobodyIsRecycledUntilTheyAreClearOfTheDespawnRadius()
    {
        var t = Tuning;
        Assert.IsFalse(CrowdRecyclePolicy.ShouldRecycle(true, 0f, 0, t), "stood on the player");
        Assert.IsFalse(CrowdRecyclePolicy.ShouldRecycle(true, t.despawnRadius, 0, t), "boundary counts as near");
        Assert.IsTrue(CrowdRecyclePolicy.ShouldRecycle(true, t.despawnRadius + 0.01f, 0, t));
        Assert.IsTrue(CrowdRecyclePolicy.ShouldRecycle(true, 500f, 0, t));
    }

    [Test]
    public void NothingIsRecycledWhileThePlayerIsNotOnFoot()
    {
        // The driving camera is far wider than the on-foot one, so a respawn could land in frame — and
        // a crowd nobody can walk up to has nothing to gain from being clustered anyway.
        Assert.IsFalse(CrowdRecyclePolicy.ShouldRecycle(false, 500f, 0, Tuning));
    }

    [Test]
    public void TheClusterCapStopsTheWholePaddockPilingOntoThePlayer()
    {
        var t = Tuning;
        Assert.IsTrue(CrowdRecyclePolicy.ShouldRecycle(true, 500f, t.targetNearPlayer - 1, t));
        Assert.IsFalse(CrowdRecyclePolicy.ShouldRecycle(true, 500f, t.targetNearPlayer, t), "cluster full");
        Assert.IsFalse(CrowdRecyclePolicy.ShouldRecycle(true, 500f, t.targetNearPlayer + 50, t));
    }

    [Test]
    public void ACapOfZeroMeansNoCap()
    {
        var t = Tuning;
        t.targetNearPlayer = 0;
        Assert.IsTrue(CrowdRecyclePolicy.ShouldRecycle(true, 500f, 10000, t));
    }

    [Test]
    public void DisablingTheRecyclerLeavesTheCrowdWhereItWasSpawned()
    {
        var t = Tuning;
        t.enabled = false;
        Assert.IsFalse(CrowdRecyclePolicy.ShouldRecycle(true, 500f, 0, t));
    }

    // ---------------------------------------------------------------- staying out of shot

    [Test]
    public void OutOfShotRadiusClearsTheCornerOfTheFrame()
    {
        // The on-foot camera is a 3.5 orthographic size: 3.5m to the top edge, 6.22m to the side at 16:9.
        const float ortho = 3.5f, aspect = 16f / 9f;
        float corner = Mathf.Sqrt(ortho * ortho + (ortho * aspect) * (ortho * aspect));

        Assert.AreEqual(corner, CrowdRecyclePolicy.OutOfShotRadius(ortho, aspect, 0f), 0.001f);
        Assert.Greater(CrowdRecyclePolicy.OutOfShotRadius(ortho, aspect, 3f), corner, "margin is added on top");
    }

    [Test]
    public void NoRespawnCanLandInsideTheCameraFrame()
    {
        // Somebody has typed a silly inner radius into the inspector. Every radius the band can produce
        // must still be outside the frame.
        var t = Tuning;
        t.respawnMinRadius = 0.5f;

        var clamped = CrowdRecyclePolicy.ClampedToCamera(t, 3.5f, 16f / 9f);
        float corner = CrowdRecyclePolicy.OutOfShotRadius(3.5f, 16f / 9f, 0f);

        for (float u = 0f; u <= 1f; u += 0.05f)
            Assert.Greater(CrowdRecyclePolicy.RadiusFor(u, clamped), corner,
                           $"a respawn at u={u} would be on screen");
    }

    [Test]
    public void AWiderCameraPushesTheRespawnBandFurtherOut()
    {
        var a = CrowdRecyclePolicy.ClampedToCamera(Tuning, 3.5f, 16f / 9f);
        var b = CrowdRecyclePolicy.ClampedToCamera(Tuning, 12f, 16f / 9f);   // zoomed right out
        Assert.Greater(b.respawnMinRadius, a.respawnMinRadius);
    }

    [Test]
    public void ClampingToACameraNeverInvertsTheBand()
    {
        // A camera wide enough to swallow the whole band collapses it against the despawn radius rather
        // than producing a max below the min (which would respawn NPCs straight back out again).
        var clamped = CrowdRecyclePolicy.ClampedToCamera(Tuning, 400f, 16f / 9f);
        Assert.LessOrEqual(clamped.respawnMinRadius, clamped.respawnMaxRadius);
        Assert.LessOrEqual(clamped.respawnMaxRadius, Tuning.despawnRadius);
    }

    // ---------------------------------------------------------------- where they land

    [Test]
    public void EveryRespawnLandsInsideTheRecycleBand()
    {
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        for (float u = 0f; u <= 1f; u += 0.01f)
        {
            float r = CrowdRecyclePolicy.RadiusFor(u, t);
            Assert.GreaterOrEqual(r, t.respawnMinRadius - 0.001f);
            Assert.LessOrEqual(r, t.respawnMaxRadius + 0.001f);
        }
    }

    [Test]
    public void ARespawnIsNeverPutBeyondTheRadiusThatWouldSendItStraightBack()
    {
        var t = Tuning;
        t.respawnMinRadius = 500f;
        t.respawnMaxRadius = 900f;
        var s = CrowdRecyclePolicy.Sanitised(t);
        Assert.LessOrEqual(s.respawnMaxRadius, t.despawnRadius);
        Assert.LessOrEqual(CrowdRecyclePolicy.RadiusFor(1f, s), t.despawnRadius + 0.001f);
    }

    [Test]
    public void TheBandIsSampledEvenlyByAreaSoNobodyRingsTheInnerEdge()
    {
        // Half the rolls must land in the half of the band that holds half its AREA, not half its width.
        // Lerping the radius straight would put ~64% of a 14-45m band inside 30m.
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        float min = t.respawnMinRadius, max = t.respawnMaxRadius;
        float equalArea = Mathf.Sqrt((min * min + max * max) * 0.5f);

        Assert.AreEqual(equalArea, CrowdRecyclePolicy.RadiusFor(0.5f, t), 0.001f,
                        "the median roll should split the band's area, not its width");

        int inner = 0, samples = 0;
        for (float u = 0f; u < 1f; u += 0.001f) { if (CrowdRecyclePolicy.RadiusFor(u, t) < equalArea) inner++; samples++; }
        Assert.AreEqual(0.5f, inner / (float)samples, 0.02f);
    }

    [Test]
    public void RadiusIsMonotonicAndCoversTheWholeBand()
    {
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        Assert.AreEqual(t.respawnMinRadius, CrowdRecyclePolicy.RadiusFor(0f, t), 0.001f);
        Assert.AreEqual(t.respawnMaxRadius, CrowdRecyclePolicy.RadiusFor(1f, t), 0.001f);

        float prev = -1f;
        for (float u = 0f; u <= 1f; u += 0.05f)
        {
            float r = CrowdRecyclePolicy.RadiusFor(u, t);
            Assert.GreaterOrEqual(r, prev);
            prev = r;
        }
    }

    // ---------------------------------------------------------------- staying in the paddock

    [Test]
    public void AcceptedCandidatesAreInsideThePaddockAndInsideTheBand()
    {
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        var area = Paddock();
        Vector2 player = new(0f, 0f);

        int accepted = 0;
        for (float a = 0f; a < 1f; a += 0.017f)
            for (float r = 0f; r <= 1f; r += 0.1f)
            {
                if (!CrowdRecyclePolicy.TryCandidate(player, area, t, a, r, out Vector2 p)) continue;
                accepted++;
                Assert.IsTrue(area.Contains(p), $"accepted a point outside the paddock: {p}");
                float d = Vector2.Distance(p, player);
                Assert.GreaterOrEqual(d, t.respawnMinRadius - 0.001f, "landed inside the camera frame");
                Assert.LessOrEqual(d, t.respawnMaxRadius + 0.001f);
            }

        Assert.Greater(accepted, 0, "a player in the middle of the paddock should have somewhere to put people");
    }

    [Test]
    public void TheSamplerGivesUpRatherThanPutAnNpcOutsideTheWalkableArea()
    {
        // Player parked way off the end of the paddock — out on the racetrack, say. Nothing within the
        // band is walkable, so every candidate must be rejected and the NPC left where it is.
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        var area = Paddock();
        Vector2 player = new(area.halfLength + 300f, 0f);

        for (float a = 0f; a < 1f; a += 0.01f)
            for (float r = 0f; r <= 1f; r += 0.1f)
                Assert.IsFalse(CrowdRecyclePolicy.TryCandidate(player, area, t, a, r, out _),
                               "nothing near this player is inside the paddock");
    }

    [Test]
    public void ARotatedPaddockIsHandledInItsOwnAxes()
    {
        // Real paddocks run along whatever heading the pit straight has, not along X.
        Vector2 along = new Vector2(1f, 1f).normalized;
        Vector2 outward = new Vector2(-1f, 1f).normalized;
        var area = new CrowdRect(new Vector2(50f, -20f), along, outward, 200f, 15f);
        var t = CrowdRecyclePolicy.Sanitised(Tuning);

        int accepted = 0;
        for (float a = 0f; a < 1f; a += 0.017f)
            for (float r = 0f; r <= 1f; r += 0.25f)
            {
                if (!CrowdRecyclePolicy.TryCandidate(area.center, area, t, a, r, out Vector2 p)) continue;
                accepted++;
                // Straight from the definition: the point projects inside both half-extents.
                Vector2 d = p - area.center;
                Assert.LessOrEqual(Mathf.Abs(Vector2.Dot(d, along)), area.halfLength);
                Assert.LessOrEqual(Mathf.Abs(Vector2.Dot(d, outward)), area.halfDepth);
            }

        Assert.Greater(accepted, 0);
    }

    [Test]
    public void CandidatesAreKeptClearOfThePaddockEdge()
    {
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        var area = Paddock(halfDepth: 15f);
        // Player against the far edge: anything accepted must still be inside the inset, not on the line.
        Vector2 player = new(0f, area.halfDepth);

        float limit = area.halfDepth * (1f - t.edgeInset);
        for (float a = 0f; a < 1f; a += 0.01f)
            for (float r = 0f; r <= 1f; r += 0.1f)
                if (CrowdRecyclePolicy.TryCandidate(player, area, t, a, r, out Vector2 p))
                    Assert.LessOrEqual(Mathf.Abs(p.y), limit + 0.001f);
    }

    // ---------------------------------------------------------------- the rectangle itself

    [Test]
    public void AnUnsetRectangleIsNeverConsideredValidOrContainingAnything()
    {
        var empty = default(CrowdRect);
        Assert.IsFalse(empty.IsValid);
        Assert.IsFalse(empty.Contains(Vector2.zero), "a zero-size paddock must not accept the origin");
        Assert.IsFalse(CrowdRecyclePolicy.TryCandidate(Vector2.zero, empty, Tuning, 0.25f, 0.5f, out _));
    }

    [Test]
    public void TheRectangleTestMatchesItsHalfExtents()
    {
        var area = Paddock(100f, 10f);
        Assert.IsTrue(area.Contains(new Vector2(99f, 9f)));
        Assert.IsTrue(area.Contains(new Vector2(-100f, -10f)), "on the corner counts as inside");
        Assert.IsFalse(area.Contains(new Vector2(101f, 0f)));
        Assert.IsFalse(area.Contains(new Vector2(0f, 11f)));
        Assert.IsFalse(area.Contains(new Vector2(100f, 0f), 0.1f), "the inset pulls the edge in");
    }

    // ---------------------------------------------------------------- sizing the cluster

    [Test]
    public void TheDefaultCapKeepsTheClusterDenserThanTheOpenPaddockButNotACrush()
    {
        // A full house (CrowdPolicy.ComfortableMaxPopulation) spread over a 400m x 30m paddock is one
        // person per 30 m², and about half of them are inside a 100m radius of a player stood in the
        // middle of it. The cap has to sit above THAT, or it fires before the recycler has added anybody
        // — and far enough below shoulder-to-shoulder that the paddock still reads as a paddock.
        var t = Tuning;
        const float paddockLength = 400f, paddockDepth = 30f;
        float openDensity = CrowdPolicy.ComfortableMaxPopulation / (paddockLength * paddockDepth);

        // The area the cap is measured over: the despawn disc, clipped to the paddock strip.
        float clustered = Mathf.Min(2f * t.despawnRadius, paddockLength) * paddockDepth;
        float alreadyNearby = openDensity * clustered;
        float clusterDensity = t.targetNearPlayer / clustered;

        Assert.Greater(t.targetNearPlayer, alreadyNearby,
                       "the cap is below the headcount that would be nearby anyway — nothing would recycle");
        Assert.Greater(clusterDensity, openDensity, "clustering should actually make it busier");
        Assert.Greater(1f / clusterDensity, 8f, "several square metres of elbow room per person");
        Assert.Less(t.targetNearPlayer, CrowdPolicy.ComfortableMaxPopulation,
                    "leave some of the crowd frozen out in the paddock as headroom");
    }

    // ---------------------------------------------------------------- the whole loop

    // The director's per-frame decision, without the director: recycle anyone past the despawn radius
    // into a legal spot near the player, stopping when the cluster is full. Returns how many are inside
    // the despawn radius at the end. Deterministic — same seed, same paddock, same answer.
    static int RunMigration(Vector2[] crowd, Vector2 player, CrowdRect area, in CrowdRecycleTuning tuning,
                            int seed, int passes = 40)
    {
        var rng = new System.Random(seed);
        int near = 0;
        for (int i = 0; i < crowd.Length; i++)
            if (Vector2.Distance(crowd[i], player) <= tuning.despawnRadius) near++;

        for (int pass = 0; pass < passes; pass++)
            for (int i = 0; i < crowd.Length; i++)
            {
                float d = Vector2.Distance(crowd[i], player);
                if (!CrowdRecyclePolicy.ShouldRecycle(true, d, near, tuning)) continue;

                for (int s = 0; s < tuning.samplesPerRecycle; s++)
                {
                    if (!CrowdRecyclePolicy.TryCandidate(player, area, tuning,
                                                         (float)rng.NextDouble(), (float)rng.NextDouble(),
                                                         out Vector2 point)) continue;
                    crowd[i] = point;
                    near++;
                    break;
                }
            }
        return near;
    }

    static Vector2[] SpreadOverPaddock(CrowdRect area, int count, int seed)
    {
        var rng = new System.Random(seed);
        var crowd = new Vector2[count];
        for (int i = 0; i < count; i++)
            crowd[i] = area.center
                     + area.along * (float)((rng.NextDouble() * 2.0 - 1.0) * area.halfLength)
                     + area.outward * (float)((rng.NextDouble() * 2.0 - 1.0) * area.halfDepth);
        return crowd;
    }

    [Test]
    public void APlayerAtTheEndOfThePaddockEndsUpWithACrowdAroundThem()
    {
        // The case the whole feature exists for: walk to one end of a 400m paddock and three quarters of
        // the crowd is behind you, doing nothing for the scene.
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        var area = Paddock(200f, 15f);
        var crowd = SpreadOverPaddock(area, CrowdPolicy.ComfortableMaxPopulation - 10, seed: 1234);
        Vector2 player = new(-180f, 0f);

        int before = 0;
        foreach (var c in crowd) if (Vector2.Distance(c, player) <= t.despawnRadius) before++;

        int after = RunMigration(crowd, player, area, t, seed: 1234);

        Assert.Less(before, t.targetNearPlayer, "the natural spread should leave room to top up");
        Assert.Greater(after, before, "the paddock around the player should have got busier");
        Assert.AreEqual(t.targetNearPlayer, after, "the cluster should fill to the cap and stop");
    }

    [Test]
    public void TheClusterNeverOverfillsAndNobodyEndsUpOutsideThePaddock()
    {
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        var area = Paddock(200f, 15f);
        var crowd = SpreadOverPaddock(area, CrowdPolicy.ComfortableMaxPopulation - 10, seed: 99);
        Vector2 player = new(-180f, 0f);

        int after = RunMigration(crowd, player, area, t, seed: 99);

        Assert.LessOrEqual(after, t.targetNearPlayer);
        foreach (var c in crowd)
            Assert.IsTrue(area.Contains(c), $"an NPC ended up outside the paddock at {c}");
    }

    [Test]
    public void NobodyIsEverPutDownInsideTheCameraFrame()
    {
        var t = CrowdRecyclePolicy.ClampedToCamera(Tuning, 3.5f, 16f / 9f);
        var area = Paddock(200f, 15f);
        var crowd = SpreadOverPaddock(area, 300, seed: 7);
        Vector2 player = new(-180f, 0f);

        // Anyone already standing next to the player at spawn is not the recycler's doing, so only the
        // ones it moved are checked — track them by starting the crowd well clear of the player.
        for (int i = 0; i < crowd.Length; i++)
            if (Vector2.Distance(crowd[i], player) < t.despawnRadius)
                crowd[i] = new Vector2(180f, crowd[i].y);

        RunMigration(crowd, player, area, t, seed: 7);

        float frameCorner = CrowdRecyclePolicy.OutOfShotRadius(3.5f, 16f / 9f, 0f);
        foreach (var c in crowd)
            Assert.Greater(Vector2.Distance(c, player), frameCorner,
                           "an NPC was put down inside the camera frame");
    }

    [Test]
    public void APlayerOutsideThePaddockPullsNobodyOutOfIt()
    {
        // Stood on the racetrack, or in a scene where the paddock rectangle never resolved. Every
        // candidate is rejected, so the crowd must be exactly where it started.
        var t = CrowdRecyclePolicy.Sanitised(Tuning);
        var area = Paddock(200f, 15f);
        var crowd = SpreadOverPaddock(area, 200, seed: 42);
        var before = (Vector2[])crowd.Clone();

        RunMigration(crowd, new Vector2(0f, 400f), area, t, seed: 42);

        for (int i = 0; i < crowd.Length; i++)
            Assert.AreEqual(before[i], crowd[i], "the crowd should not have moved");
    }

    [Test]
    public void TheRespawnBandStartsOutsideTheFullyAwakeRadius()
    {
        // Otherwise an NPC would materialise inside the band where it is talking and being talked to,
        // which is close enough for the player to notice something appearing at the edge of the screen.
        Assert.GreaterOrEqual(Tuning.respawnMinRadius, CrowdTuning.Default.fullRadius);
        Assert.Less(Tuning.respawnMaxRadius, Tuning.despawnRadius, "respawns must not land on the despawn line");
    }
}
