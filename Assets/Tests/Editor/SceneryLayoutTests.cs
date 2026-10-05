using System.Collections.Generic;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// The trackside scenery scatter: it fills the grass, and it never puts anything where it would be in the way —
// on the road, in the pit lane, in a blocked area, on top of another piece, or off the ground.
public class SceneryLayoutTests
{
    const float Radius = 300f;     // a round "oval": ~1.9 km lap, 15 m wide
    const float HalfWidth = 7.5f;

    static SceneryRequest Circle(int seed = 7)
    {
        var req = new SceneryRequest { seed = seed, roadClearance = 12f, campCoverage = 0.5f, campScale = 80f };
        int n = 900;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
            var t = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            req.road.Add(new SceneryEdge(p, t, HalfWidth));
        }
        req.lapLength = 2f * Mathf.PI * Radius;
        req.area = Rect.MinMaxRect(-Radius - 150f, -Radius - 150f, Radius + 150f, Radius + 150f);
        req.kinds = new[]
        {
            new SceneryKind
            {
                perKm = 20f, length = 10f, width = 3.75f, minSetback = 30f, maxSetback = 120f, spacing = 2.5f,
                campsOnly = true, facing = SceneryFacing.AlongTrack, angleJitter = 6f, turnChance = 0.4f,
                followerKind = 1, followersMin = 0, followersMax = 3, followerReach = 3f,
            },
            new SceneryKind
            {
                perKm = 15f, length = 0.6f, width = 0.6f, minSetback = 14f, maxSetback = 40f, spacing = 0.4f,
                facing = SceneryFacing.FaceTrack, angleJitter = 0f,
                followerKind = 1, followersMin = 1, followersMax = 4, followerReach = 2.5f,
            },
        };
        return req;
    }

    static float SetbackOf(Vector2 p) => Mathf.Abs(p.magnitude - Radius) - HalfWidth;

    // The corners of a placed footprint, for overlap checks.
    static Vector2[] Corners(SceneryKind k, SceneryPlacement p)
    {
        float r = p.angle * Mathf.Deg2Rad;
        var ax = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * k.length * 0.5f;
        var ay = new Vector2(-Mathf.Sin(r), Mathf.Cos(r)) * k.width * 0.5f;
        return new[] { p.position - ax - ay, p.position + ax - ay, p.position + ax + ay, p.position - ax + ay };
    }

    [Test]
    public void ItFillsTheGrassToBudget()
    {
        var req = Circle();
        var result = SceneryLayout.Solve(req);
        Assert.Greater(result.placements.Count, 0);
        Assert.AreEqual(Mathf.RoundToInt(20f * req.lapLength / 1000f), result.wanted[0]);
        int leaders0 = 0, leaders1 = 0;
        foreach (var p in result.placements)
            if (p.leader < 0) { if (p.kind == 0) leaders0++; else leaders1++; }
        Assert.AreEqual(result.wanted[0], leaders0, "plenty of open ground: every motorhome should find a spot");
        Assert.AreEqual(result.wanted[1], leaders1, "plenty of fence: every group of spectators should find a spot");
        Assert.Greater(result.placed[1], leaders1, "spectator groups and motorhome campers add people on top");
    }

    [Test]
    public void APerHectareKindFillsTheGroundItMayUse()
    {
        var req = Circle();
        req.kinds[0].perKm = 0f;
        req.kinds[0].perHectare = 5f;
        req.kinds[1].perKm = 0f;
        var result = SceneryLayout.Solve(req);
        // Camps cover roughly half of a ring ~2 x 90 m wide round a 1.9 km lap: some tens of hectares.
        Assert.Greater(result.wanted[0], 40);
        Assert.Less(result.wanted[0], 5 * 40);
        Assert.Greater(result.placed[0], result.wanted[0] / 2, "the camps should take most of what they were asked for");
    }

    [Test]
    public void NothingIsPlacedNearTheRoad()
    {
        var req = Circle();
        var result = SceneryLayout.Solve(req);
        foreach (var p in result.placements)
        {
            var k = req.kinds[p.kind];
            foreach (var c in Corners(k, p))
                Assert.GreaterOrEqual(SetbackOf(c), req.roadClearance - 2.1f,   // a cell's slack
                    $"kind {p.kind} at {p.position} reaches within {SetbackOf(c):F1} m of the road edge");
        }
    }

    [Test]
    public void LeadersStayInsideTheirSetbackBand()
    {
        var req = Circle();
        var result = SceneryLayout.Solve(req);
        foreach (var p in result.placements)
        {
            if (p.leader >= 0) continue;
            var k = req.kinds[p.kind];
            float s = SetbackOf(p.position);
            Assert.GreaterOrEqual(s, k.minSetback - 2f, $"kind {p.kind} at {p.position}");
            Assert.LessOrEqual(s, k.maxSetback + 2f, $"kind {p.kind} at {p.position}");
        }
    }

    [Test]
    public void FootprintsDoNotOverlap()
    {
        var req = Circle();
        var result = SceneryLayout.Solve(req);
        var list = result.placements;
        for (int i = 0; i < list.Count; i++)
        for (int j = i + 1; j < list.Count; j++)
        {
            var a = list[i]; var b = list[j];
            var ka = req.kinds[a.kind]; var kb = req.kinds[b.kind];
            // Separating-axis test on the two oriented rectangles.
            Assert.IsFalse(Overlap(Corners(ka, a), Corners(kb, b)),
                $"piece {i} (kind {a.kind}) at {a.position} overlaps piece {j} (kind {b.kind}) at {b.position}");
        }
    }

    [Test]
    public void BlockedGroundAndThePitLaneStayEmpty()
    {
        var req = Circle();
        var blocked = Rect.MinMaxRect(Radius + 20f, -100f, Radius + 140f, 100f);   // a paddock on the outside
        req.blockedRects.Add(blocked);
        var hole = new SceneryCircle(new Vector2(0f, -Radius - 60f), 50f);
        req.blockedCircles.Add(hole);
        // A pit lane inside the oval along the top, kept clear 30 m.
        for (float x = -150f; x <= 150f; x += 2f)
            req.keepClear.Add(new SceneryEdge(new Vector2(x, Radius - 40f), Vector2.right, 6f + 30f));

        // A gravel trap on the outside of the left-hand side, drawn as an outline.
        var trap = new[] { new Vector2(-Radius - 10f, -80f), new Vector2(-Radius - 120f, 0f), new Vector2(-Radius - 10f, 80f) };
        req.blockedPolygons.Add(new SceneryPolygon(trap, 3f));

        var result = SceneryLayout.Solve(req);
        foreach (var p in result.placements)
        {
            Assert.IsFalse(InTriangle(p.position, trap[0], trap[1], trap[2]), $"piece in the gravel trap at {p.position}");
            Assert.IsFalse(blocked.Contains(p.position), $"piece in the blocked rect at {p.position}");
            Assert.Greater((p.position - hole.centre).magnitude, hole.radius - 1.5f, $"piece in the blocked circle at {p.position}");
            bool inPitBand = Mathf.Abs(p.position.x) <= 150f && Mathf.Abs(p.position.y - (Radius - 40f)) < 36f - 1.5f;
            Assert.IsFalse(inPitBand, $"piece in the pit lane's clearance at {p.position}");
        }
    }

    [Test]
    public void EverythingStaysOnTheGround()
    {
        var req = Circle();
        req.area = Rect.MinMaxRect(-Radius - 60f, -Radius - 60f, Radius + 60f, Radius + 60f);   // tight ground
        var result = SceneryLayout.Solve(req);
        Assert.Greater(result.placements.Count, 0);
        foreach (var p in result.placements)
            foreach (var c in Corners(req.kinds[p.kind], p))
                Assert.IsTrue(req.area.Contains(c), $"kind {p.kind} at {p.position} hangs off the ground");
    }

    [Test]
    public void SpectatorsFaceTheTrack()
    {
        var req = Circle();
        var result = SceneryLayout.Solve(req);
        int checkedCount = 0;
        foreach (var p in result.placements)
        {
            if (p.kind != 1) continue;
            float r = p.angle * Mathf.Deg2Rad;
            Vector2 front = new Vector2(Mathf.Sin(r), -Mathf.Cos(r));   // local -Y
            Vector2 towardRoad = p.position.magnitude > Radius ? -p.position.normalized : p.position.normalized;
            Assert.Greater(Vector2.Dot(front, towardRoad), 0.8f, $"spectator at {p.position} looks away from the track");
            checkedCount++;
        }
        Assert.Greater(checkedCount, 0);
    }

    [Test]
    public void SameSeedSameLayoutAndADifferentSeedMovesIt()
    {
        var a = SceneryLayout.Solve(Circle(3)).placements;
        var b = SceneryLayout.Solve(Circle(3)).placements;
        var c = SceneryLayout.Solve(Circle(4)).placements;
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].position, b[i].position);
        bool moved = a.Count != c.Count;
        for (int i = 0; !moved && i < a.Count; i++) moved = a[i].position != c[i].position;
        Assert.IsTrue(moved);
    }

    [Test]
    public void TheCapIsHonoured()
    {
        var req = Circle();
        req.maxPieces = 25;
        Assert.LessOrEqual(SceneryLayout.Solve(req).placements.Count, 25);
    }

    [Test]
    public void FollowersStandRoundTheirLeader()
    {
        var req = Circle();
        var result = SceneryLayout.Solve(req);
        foreach (var p in result.placements)
        {
            if (p.leader < 0) continue;
            var lead = result.placements[p.leader];
            var k = req.kinds[lead.kind];
            float most = 0.5f * new Vector2(k.length, k.width).magnitude + 0.5f + 0.3f + k.followerReach + 0.01f;
            Assert.LessOrEqual((p.position - lead.position).magnitude, most);
        }
    }

    [Test]
    public void NoKindsOrNoRoadPlacesNothing()
    {
        var req = Circle();
        req.kinds = new SceneryKind[0];
        Assert.AreEqual(0, SceneryLayout.Solve(req).placements.Count);
        var empty = new SceneryRequest { area = new Rect(0, 0, 100, 100), kinds = Circle().kinds, lapLength = 1000f };
        Assert.AreEqual(0, SceneryLayout.Solve(empty).placements.Count);
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float Cross(Vector2 u, Vector2 v, Vector2 w) => (v.x - u.x) * (w.y - u.y) - (v.y - u.y) * (w.x - u.x);
        float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static bool Overlap(Vector2[] a, Vector2[] b)
    {
        foreach (var poly in new[] { a, b })
            for (int i = 0; i < 4; i++)
            {
                Vector2 edge = poly[(i + 1) % 4] - poly[i];
                Vector2 axis = new Vector2(-edge.y, edge.x).normalized;
                float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
                foreach (var v in a) { float d = Vector2.Dot(v, axis); aMin = Mathf.Min(aMin, d); aMax = Mathf.Max(aMax, d); }
                foreach (var v in b) { float d = Vector2.Dot(v, axis); bMin = Mathf.Min(bMin, d); bMax = Mathf.Max(bMax, d); }
                if (aMax <= bMin + 1e-3f || bMax <= aMin + 1e-3f) return false;
            }
        return true;
    }
}
