using System;
using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // How a scattered piece is turned. Piece frame: local +X is its length, local -Y its front (the way the
    // paper-doll NPC art faces), so "facing the track" means -Y points at the nearest bit of road.
    public enum SceneryFacing
    {
        AlongTrack,   // +X parallel to the nearest road, either way round — parked rigs
        FaceTrack,    // -Y at the nearest road — spectators
        Random,       // any angle — bushes, litter
        Fixed,        // angle 0 (plus jitter) — art that must stay upright on screen
    }

    // One kind of thing to scatter, in plain numbers. The editor fills these from a SceneryPalette entry;
    // nothing here knows about sprites or prefabs.
    [Serializable]
    public struct SceneryKind
    {
        public float perKm;          // how many per kilometre of lap (leaders only — followers come on top)
        public float perHectare;     // plus how many per hectare of the ground this kind may stand on
        public float length;         // footprint along local +X, metres
        public float width;          // footprint along local Y, metres
        public float minSetback;     // from the road EDGE, metres
        public float maxSetback;
        public float spacing;        // clear ground kept round the footprint
        public bool campsOnly;       // only inside the noise-field "camping" patches
        public SceneryFacing facing;
        public float angleJitter;    // +/- degrees
        public float turnChance;     // AlongTrack: chance a whole camp parks nose-in (turned 90) instead
        public int followerKind;     // -1 = none. A kind placed in a little group round each one of these
        public int followersMin;
        public int followersMax;
        public float followerReach;  // how far past the leader's footprint the group stands, metres
    }

    // A point on a drivable centreline. halfWidth is half the surface width (edge = centre +/- halfWidth).
    public struct SceneryEdge
    {
        public Vector2 position;
        public Vector2 tangent;
        public float halfWidth;

        public SceneryEdge(Vector2 position, Vector2 tangent, float halfWidth)
        {
            this.position = position; this.tangent = tangent; this.halfWidth = halfWidth;
        }
    }

    public struct SceneryCircle
    {
        public Vector2 centre;
        public float radius;
        public SceneryCircle(Vector2 centre, float radius) { this.centre = centre; this.radius = radius; }
    }

    public struct SceneryPolygon
    {
        public Vector2[] points;
        public float pad;
        public SceneryPolygon(Vector2[] points, float pad) { this.points = points; this.pad = pad; }
    }

    public struct SceneryPlacement
    {
        public int kind;
        public Vector2 position;
        public float angle;      // degrees about +Z
        public int seed;         // per-piece dice for the look (sprite variant, tint, outfit)
        public int leader;       // index of the placement this one is grouped round, -1 for a leader
        public float setback;    // metres from the nearest road edge (+inf when out of reach of every road)
    }

    public class SceneryRequest
    {
        // Every road the scenery keeps clear of AND is measured from (setback bands are off these).
        public readonly List<SceneryEdge> road = new List<SceneryEdge>();
        // Ribbons that are only kept clear (pit lane, service roads): halfWidth must already include the
        // clearance wanted, nothing is measured from them.
        public readonly List<SceneryEdge> keepClear = new List<SceneryEdge>();
        public readonly List<Rect> blockedRects = new List<Rect>();
        public readonly List<SceneryCircle> blockedCircles = new List<SceneryCircle>();
        // Outlines (run-off areas, gravel traps) kept clear, each with its own padding.
        public readonly List<SceneryPolygon> blockedPolygons = new List<SceneryPolygon>();

        public Rect area;                     // the ground; nothing is placed outside it
        public float lapLength;               // metres — budgets are per km of this
        public SceneryKind[] kinds = new SceneryKind[0];
        public int seed;
        public float cellSize = 2f;
        public float roadClearance = 12f;     // nothing at all (followers included) nearer the road edge than this
        public float campScale = 90f;         // metres across a typical camping patch
        public float campCoverage = 0.35f;    // roughly what share of the ground the patches cover, 0..1
        public int maxPieces = 1200;
    }

    public class SceneryResult
    {
        public readonly List<SceneryPlacement> placements = new List<SceneryPlacement>();
        public int[] wanted = new int[0];     // leader budget per kind
        public int[] placed = new int[0];     // everything placed per kind, followers included
        public int usableCells;
        public float cellSize;
    }

    // Scatters trackside scenery — fans' motorhomes, people, greenery — over the open grass round a track.
    //
    // Pure maths over a coarse occupancy grid laid on the ground: each road sample stamps its distance into
    // the cells round it (so every cell knows how far it is from the nearest road edge and which way the road
    // is), the pit lane, paddock and hand-placed pieces stamp themselves as blocked, then each kind takes
    // seeded random cells inside its setback band until its per-km budget is met or the ground runs out.
    // Every placed footprint blocks its own cells, which is all that stops two pieces overlapping.
    //
    // Same seed + same inputs = the same layout, so a package can be re-scattered without everything moving.
    public static class SceneryLayout
    {
        const int MaxCells = 6_000_000;

        public static SceneryResult Solve(SceneryRequest req)
        {
            var result = new SceneryResult();
            int kindCount = req.kinds != null ? req.kinds.Length : 0;
            result.wanted = new int[kindCount];
            result.placed = new int[kindCount];
            if (kindCount == 0 || req.road.Count < 2 || req.area.width <= 0f || req.area.height <= 0f) return result;

            var grid = new Grid(req.area, req.cellSize);
            result.cellSize = grid.cell;

            float reach = req.roadClearance;
            foreach (var k in req.kinds) reach = Mathf.Max(reach, k.maxSetback + k.length + k.followerReach);
            StampRoad(grid, req.road, reach);

            for (int i = 0; i < grid.blocked.Length; i++)
                if (grid.setback[i] < req.roadClearance) grid.blocked[i] = true;

            StampKeepClear(grid, req.keepClear);
            foreach (var r in req.blockedRects) grid.BlockRect(r);
            foreach (var c in req.blockedCircles) grid.BlockCircle(c.centre, c.radius);
            foreach (var poly in req.blockedPolygons) grid.BlockPolygon(poly.points, poly.pad);

            for (int i = 0; i < grid.blocked.Length; i++) if (!grid.blocked[i]) result.usableCells++;

            var rng = new System.Random(req.seed);
            Vector2 noiseOffset = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
            float campThreshold = Mathf.Lerp(0.75f, 0.25f, Mathf.Clamp01(req.campCoverage));
            float campScale = Mathf.Max(1f, req.campScale);

            bool InCamp(Vector2 p) =>
                Mathf.PerlinNoise((p.x + noiseOffset.x) / campScale, (p.y + noiseOffset.y) / campScale) >= campThreshold;

            bool CampTurned(Vector2 p, float chance)
            {
                if (chance <= 0f) return false;
                int cx = Mathf.FloorToInt(p.x / campScale), cy = Mathf.FloorToInt(p.y / campScale);
                uint h = (uint)(cx * 73856093) ^ (uint)(cy * 19349663) ^ (uint)(req.seed * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h % 10000) / 10000f < chance;
            }

            float AngleFor(in SceneryKind kind, Vector2 p, int roadIndex, Vector2 fallbackTarget)
            {
                float jitter = ((float)rng.NextDouble() * 2f - 1f) * kind.angleJitter;
                switch (kind.facing)
                {
                    case SceneryFacing.AlongTrack:
                    {
                        Vector2 t = roadIndex >= 0 ? req.road[roadIndex].tangent : Vector2.right;
                        float a = Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg;
                        if (rng.NextDouble() < 0.5) a += 180f;
                        if (CampTurned(p, kind.turnChance)) a += 90f;
                        return a + jitter;
                    }
                    case SceneryFacing.FaceTrack:
                    {
                        Vector2 target = roadIndex >= 0 ? req.road[roadIndex].position : fallbackTarget;
                        Vector2 d = target - p;
                        if (d.sqrMagnitude < 1e-6f) return jitter;
                        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f + jitter;
                    }
                    case SceneryFacing.Random:
                        return (float)rng.NextDouble() * 360f;
                    default:
                        return jitter;
                }
            }

            float lapKm = Mathf.Max(0f, req.lapLength) / 1000f;
            int total = 0;

            for (int k = 0; k < kindCount && total < req.maxPieces; k++)
            {
                var kind = req.kinds[k];
                // Candidate cells: usable, centre inside this kind's band, in a camp if it must be.
                var candidates = new List<int>();
                for (int i = 0; i < grid.blocked.Length; i++)
                {
                    if (grid.blocked[i]) continue;
                    float s = grid.setback[i];
                    if (s < kind.minSetback || s > kind.maxSetback) continue;
                    if (kind.campsOnly && !InCamp(grid.Centre(i))) continue;
                    candidates.Add(i);
                }

                // Budget: a line's worth per km of lap (people at the fence) plus a fill per hectare of the
                // ground this kind could use (camps) — so a short track with a lot of grass still fills it.
                float hectares = candidates.Count * grid.cell * grid.cell / 10000f;
                int budget = Mathf.RoundToInt(Mathf.Max(0f, kind.perKm) * lapKm + Mathf.Max(0f, kind.perHectare) * hectares);
                result.wanted[k] = budget;
                if (budget <= 0) continue;

                int leaders = 0;
                for (int n = candidates.Count; n > 0 && leaders < budget && total < req.maxPieces; n--)
                {
                    // Fisher-Yates as we go: only as much shuffling as there are tries.
                    int pick = rng.Next(n);
                    int cellIndex = candidates[pick];
                    candidates[pick] = candidates[n - 1];
                    if (grid.blocked[cellIndex]) continue;

                    Vector2 p = grid.Centre(cellIndex) + new Vector2(
                        ((float)rng.NextDouble() - 0.5f) * grid.cell, ((float)rng.NextDouble() - 0.5f) * grid.cell);
                    int roadIndex = grid.nearest[cellIndex];
                    float angle = AngleFor(kind, p, roadIndex, p);
                    if (!grid.Fits(p, angle, kind.length, kind.width, kind.spacing)) continue;

                    grid.Occupy(p, angle, kind.length, kind.width);
                    int leaderIndex = result.placements.Count;
                    result.placements.Add(new SceneryPlacement
                    {
                        kind = k, position = p, angle = angle, seed = rng.Next(), leader = -1,
                        setback = grid.setback[cellIndex],
                    });
                    result.placed[k]++;
                    leaders++;
                    total++;

                    total += PlaceFollowers(req, grid, rng, result, kind, leaderIndex, roadIndex, AngleFor,
                                            req.maxPieces - total);
                }
            }
            return result;
        }

        delegate float AngleRule(in SceneryKind kind, Vector2 p, int roadIndex, Vector2 fallbackTarget);

        // A little group stood round a leader: fans outside their motorhome, a knot of people at the fence.
        static int PlaceFollowers(SceneryRequest req, Grid grid, System.Random rng, SceneryResult result,
                                  SceneryKind leader, int leaderIndex, int leaderRoad, AngleRule angleFor, int room)
        {
            int f = leader.followerKind;
            if (f < 0 || f >= req.kinds.Length || leader.followersMax <= 0 || room <= 0) return 0;

            var kind = req.kinds[f];
            var lead = result.placements[leaderIndex];
            Vector2 roadTarget = leaderRoad >= 0 ? req.road[leaderRoad].position : lead.position;
            int want = Mathf.Min(room, rng.Next(Mathf.Max(0, leader.followersMin), Mathf.Max(leader.followersMin, leader.followersMax) + 1));
            float leaderRadius = 0.5f * Mathf.Sqrt(leader.length * leader.length + leader.width * leader.width);
            float footprint = 0.5f * Mathf.Max(kind.length, kind.width);

            int placed = 0;
            for (int n = 0; n < want; n++)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = leaderRadius + footprint + 0.3f + (float)rng.NextDouble() * Mathf.Max(0.1f, leader.followerReach);
                    Vector2 p = lead.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    int cellIndex = grid.IndexOf(p);
                    if (cellIndex < 0 || grid.blocked[cellIndex]) continue;

                    int road = grid.nearest[cellIndex];
                    float angle = angleFor(kind, p, road, roadTarget);
                    if (!grid.Fits(p, angle, kind.length, kind.width, kind.spacing)) continue;

                    grid.Occupy(p, angle, kind.length, kind.width);
                    result.placements.Add(new SceneryPlacement
                    {
                        kind = f, position = p, angle = angle, seed = rng.Next(), leader = leaderIndex,
                        setback = grid.setback[cellIndex],
                    });
                    result.placed[f]++;
                    placed++;
                    break;
                }
            }
            return placed;
        }

        // Every cell within `reach` of the road edge learns its distance to that edge and which sample is
        // nearest. Samples closer together than a cell add nothing, so they are skipped.
        static void StampRoad(Grid grid, List<SceneryEdge> road, float reach)
        {
            Vector2 last = new Vector2(float.MaxValue, float.MaxValue);
            float step = grid.cell;
            for (int i = 0; i < road.Count; i++)
            {
                var e = road[i];
                bool lastSample = i == road.Count - 1;
                if (!lastSample && (e.position - last).sqrMagnitude < step * step) continue;
                last = e.position;

                float radius = e.halfWidth + reach;
                grid.Range(e.position, radius, out int x0, out int y0, out int x1, out int y1);
                float r2 = radius * radius;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 c = grid.Centre(x, y);
                    float d2 = (c - e.position).sqrMagnitude;
                    if (d2 > r2) continue;
                    int idx = y * grid.nx + x;
                    float s = Mathf.Sqrt(d2) - e.halfWidth;
                    if (s < grid.setback[idx]) { grid.setback[idx] = s; grid.nearest[idx] = i; }
                }
            }
        }

        static void StampKeepClear(Grid grid, List<SceneryEdge> ribbon)
        {
            Vector2 last = new Vector2(float.MaxValue, float.MaxValue);
            for (int i = 0; i < ribbon.Count; i++)
            {
                var e = ribbon[i];
                if (i != ribbon.Count - 1 && (e.position - last).sqrMagnitude < grid.cell * grid.cell) continue;
                last = e.position;
                grid.BlockCircle(e.position, e.halfWidth);
            }
        }

        sealed class Grid
        {
            public readonly Rect area;
            public readonly float cell;
            public readonly int nx, ny;
            public readonly bool[] blocked;
            public readonly float[] setback;
            public readonly int[] nearest;

            public Grid(Rect area, float cellSize)
            {
                this.area = area;
                cell = Mathf.Max(0.25f, cellSize);
                while ((double)Mathf.CeilToInt(area.width / cell) * Mathf.CeilToInt(area.height / cell) > MaxCells) cell *= 1.5f;
                nx = Mathf.Max(1, Mathf.CeilToInt(area.width / cell));
                ny = Mathf.Max(1, Mathf.CeilToInt(area.height / cell));
                blocked = new bool[nx * ny];
                setback = new float[nx * ny];
                nearest = new int[nx * ny];
                for (int i = 0; i < setback.Length; i++) { setback[i] = float.PositiveInfinity; nearest[i] = -1; }

                // The last row and column usually poke past the ground's edge; nothing may stand on them.
                for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    if (area.xMin + (x + 1) * cell > area.xMax + 1e-3f || area.yMin + (y + 1) * cell > area.yMax + 1e-3f)
                        blocked[y * nx + x] = true;
            }

            public Vector2 Centre(int x, int y) => new Vector2(area.xMin + (x + 0.5f) * cell, area.yMin + (y + 0.5f) * cell);
            public Vector2 Centre(int index) => Centre(index % nx, index / nx);

            public int IndexOf(Vector2 p)
            {
                int x = Mathf.FloorToInt((p.x - area.xMin) / cell), y = Mathf.FloorToInt((p.y - area.yMin) / cell);
                if (x < 0 || y < 0 || x >= nx || y >= ny) return -1;
                return y * nx + x;
            }

            // Cell index range whose centres could lie within `radius` of p (clamped to the grid).
            public void Range(Vector2 p, float radius, out int x0, out int y0, out int x1, out int y1)
            {
                x0 = Mathf.Max(0, Mathf.FloorToInt((p.x - radius - area.xMin) / cell));
                y0 = Mathf.Max(0, Mathf.FloorToInt((p.y - radius - area.yMin) / cell));
                x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((p.x + radius - area.xMin) / cell));
                y1 = Mathf.Min(ny - 1, Mathf.FloorToInt((p.y + radius - area.yMin) / cell));
            }

            public void BlockCircle(Vector2 p, float radius)
            {
                Range(p, radius, out int x0, out int y0, out int x1, out int y1);
                float r2 = radius * radius;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if ((Centre(x, y) - p).sqrMagnitude <= r2) blocked[y * nx + x] = true;
                int own = IndexOf(p);
                if (own >= 0) blocked[own] = true;
            }

            public void BlockRect(Rect r)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - area.xMin) / cell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - area.yMin) / cell));
                int x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((r.xMax - area.xMin) / cell));
                int y1 = Mathf.Min(ny - 1, Mathf.FloorToInt((r.yMax - area.yMin) / cell));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    blocked[y * nx + x] = true;
            }

            // Cells whose centre is inside the outline or within `pad` of its edge.
            public void BlockPolygon(Vector2[] pts, float pad)
            {
                if (pts == null || pts.Length < 3) return;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var q in pts)
                {
                    minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
                    minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
                }
                var r = Rect.MinMaxRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
                int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - area.xMin) / cell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - area.yMin) / cell));
                int x1 = Mathf.Min(nx - 1, Mathf.FloorToInt((r.xMax - area.xMin) / cell));
                int y1 = Mathf.Min(ny - 1, Mathf.FloorToInt((r.yMax - area.yMin) / cell));
                float pad2 = pad * pad;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int idx = y * nx + x;
                    if (blocked[idx]) continue;
                    Vector2 c = Centre(x, y);
                    bool inside = false, near = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    {
                        Vector2 a = pts[i], b = pts[j];
                        if ((a.y > c.y) != (b.y > c.y) && c.x < (b.x - a.x) * (c.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                        if (!near && pad > 0f)
                        {
                            Vector2 ab = b - a;
                            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
                            near = (a + ab * t - c).sqrMagnitude <= pad2;
                        }
                    }
                    if (inside || near) blocked[idx] = true;
                }
            }

            // Is the footprint (plus spacing all round) on usable ground, inside the grid, clear of everything?
            // The cell under the centre is always checked, so a piece smaller than a cell still can't land on
            // top of another.
            public bool Fits(Vector2 p, float angleDeg, float length, float width, float spacing)
            {
                int own = IndexOf(p);
                if (own < 0 || blocked[own]) return false;
                bool ok = true;
                ForCells(p, angleDeg, length * 0.5f + spacing, width * 0.5f + spacing, idx =>
                {
                    if (idx < 0 || blocked[idx]) ok = false;
                });
                return ok;
            }

            public void Occupy(Vector2 p, float angleDeg, float length, float width)
            {
                int own = IndexOf(p);
                if (own >= 0) blocked[own] = true;
                ForCells(p, angleDeg, length * 0.5f, width * 0.5f, idx => { if (idx >= 0) blocked[idx] = true; });
            }

            // Every cell the oriented rectangle touches at all (separating axes: the rectangle's two and the
            // grid's two); -1 for one that falls off the grid. Touching rather than centre-inside is what makes
            // the occupancy sound — two footprints that overlap must share a cell — at the cost of keeping
            // pieces up to a cell further apart than their spacing asks.
            void ForCells(Vector2 p, float angleDeg, float hx, float hy, Action<int> visit)
            {
                float rad = angleDeg * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
                float ex = Mathf.Abs(cos) * hx + Mathf.Abs(sin) * hy;
                float ey = Mathf.Abs(sin) * hx + Mathf.Abs(cos) * hy;
                float half = cell * 0.5f;
                float cellOnAxis = half * (Mathf.Abs(cos) + Mathf.Abs(sin));

                int x0 = Mathf.FloorToInt((p.x - ex - area.xMin) / cell), x1 = Mathf.FloorToInt((p.x + ex - area.xMin) / cell);
                int y0 = Mathf.FloorToInt((p.y - ey - area.yMin) / cell), y1 = Mathf.FloorToInt((p.y + ey - area.yMin) / cell);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 d = Centre(x, y) - p;
                    float lx = d.x * cos + d.y * sin;
                    float ly = -d.x * sin + d.y * cos;
                    if (Mathf.Abs(lx) > hx + cellOnAxis || Mathf.Abs(ly) > hy + cellOnAxis) continue;
                    bool inside = x >= 0 && y >= 0 && x < nx && y < ny;
                    visit(inside ? y * nx + x : -1);
                }
            }
        }
    }
}
