using System;
using System.Collections.Generic;

namespace Draftmaster.Tracks
{
    // The satnav's roadside stops, as rules: what kind of shop a stop is, how its interior is laid out, what a
    // junkyard has in its yard this week and how a fabricator's work compares with your own. Pure (no scene),
    // so it is tested headless; RoadsideShopBuilder turns a layout into the on-foot room, and the shop panel
    // sells from the stock rolls here. Guide: Docs/Travel-Map.md, "Roadside shops".
    public enum ShopKind { Fabricator, EngineBuilder, Junkyard }

    // ------------------------------------------------------------------ layouts

    // One interior. Metres, room centred on the origin, +y = away from the door (the door is on the bottom
    // wall). JsonUtility-shaped: public fields, no nulls needed. Tools/shop_layouts.py generates them,
    // Draftmaster > Travel Map > Shop Layouts loads one into the Editor to tweak and saves it back `authored`.
    [Serializable]
    public class ShopSpot
    {
        public float x, y, rotation;
    }

    [Serializable]
    public class ShopBay
    {
        public float x, y, rotation;
        public string state = "finished";   // "finished" | "working" — a working bay has someone at it
    }

    [Serializable]
    public class ShopProp
    {
        public string kind;                 // "bench" | "toolbox" | "rack" | "tyres" | "crate" | "welder"
        public float x, y, rotation;
        public float width = 1.5f, height = 1f;
    }

    [Serializable]
    public class ShopLayout
    {
        public string name;
        public bool authored;               // hand-made or tweaked: the generator keeps it as it is
        public float width = 22f, height = 16f;
        public float doorX;                 // along the bottom wall
        public ShopSpot counter = new ShopSpot();
        public ShopSpot clerk = new ShopSpot();
        public ShopBay[] bays = Array.Empty<ShopBay>();
        public ShopSpot[] workers = Array.Empty<ShopSpot>();
        public ShopProp[] props = Array.Empty<ShopProp>();
    }

    [Serializable]
    public class ShopLayoutFile
    {
        public string kind;
        public ShopLayout[] variants = Array.Empty<ShopLayout>();
    }

    public static class ShopLayouts
    {
        public const string Folder = "Travel/Shops";        // Resources/Travel/Shops/<Kind>.json or <locationId>.json
        public const float CounterWidth = 3.6f, CounterDepth = 1.2f;
        public const float DoorWidth = 2.4f;
        public const float DoorClear = 3.5f;                  // open floor kept inside the door
        public const float WallMargin = 0.6f;
        public const float Clear = 0.8f;                      // walkway kept round every piece

        // A bay's footprint: a whole car with room to work round it, or one engine on its stand.
        public static (float w, float h) BayFootprint(ShopKind kind) =>
            kind == ShopKind.EngineBuilder ? (2.6f, 2.6f) : (6.2f, 3.4f);

        // Which variant a visit uses: the same all week (junkyard stock rerolls weekly too), different by
        // location so two yards on one trip don't share a floor plan.
        public static int Pick(string locationId, int week, int count)
        {
            if (count <= 0) return -1;
            unchecked
            {
                int h = 23;
                foreach (char c in locationId ?? "") h = h * 37 + c;
                h = h * 37 + week;
                return (int)((uint)h % (uint)count);
            }
        }

        // Every piece's footprint (centre, size, rotation), labelled. Workers are a 0.8 m disc as a square.
        public static List<(string what, float x, float y, float w, float h, float rot)> Footprints(ShopLayout l, ShopKind kind)
        {
            var list = new List<(string, float, float, float, float, float)>();
            list.Add(("counter", l.counter.x, l.counter.y, CounterWidth, CounterDepth, l.counter.rotation));
            var (bw, bh) = BayFootprint(kind);
            for (int i = 0; i < l.bays.Length; i++)
                list.Add(($"bay {i}", l.bays[i].x, l.bays[i].y, bw, bh, l.bays[i].rotation));
            for (int i = 0; i < l.props.Length; i++)
                list.Add(($"{l.props[i].kind} {i}", l.props[i].x, l.props[i].y, l.props[i].width, l.props[i].height, l.props[i].rotation));
            for (int i = 0; i < l.workers.Length; i++)
                list.Add(($"worker {i}", l.workers[i].x, l.workers[i].y, 0.8f, 0.8f, 0f));
            return list;
        }

        // What's wrong with a layout: pieces overlapping each other (with a walkway between), off the floor,
        // blocking the door, a clerk not behind the counter, a working bay with nobody at it. Empty = fine.
        public static List<string> Problems(ShopLayout l, ShopKind kind)
        {
            var problems = new List<string>();
            if (l == null) { problems.Add("no layout"); return problems; }
            if (l.width < 10f || l.height < 8f) problems.Add($"room {l.width}x{l.height} m is too small");
            if (l.bays.Length == 0) problems.Add("no bays");
            var fp = Footprints(l, kind);
            float hw = l.width / 2f - WallMargin, hh = l.height / 2f - WallMargin;

            foreach (var (what, x, y, w, h, rot) in fp)
            {
                foreach (var (cx, cy) in Corners(x, y, w, h, rot))
                    // 1 cm of slack: a piece set exactly on the margin must not fail on float rounding.
                    if (Math.Abs(cx) > hw + 0.01f || Math.Abs(cy) > hh + 0.01f) { problems.Add($"{what} is off the floor"); break; }
                // The door: a clear patch inside it, so the player isn't spawned into something.
                var door = Corners(l.doorX, -l.height / 2f + DoorClear / 2f, DoorWidth + 1f, DoorClear, 0f);
                if (Overlap(Corners(x, y, w, h, rot), door)) problems.Add($"{what} blocks the door");
            }
            if (Math.Abs(l.doorX) > l.width / 2f - DoorWidth) problems.Add("door is off the bottom wall");

            for (int i = 0; i < fp.Count; i++)
                for (int j = i + 1; j < fp.Count; j++)
                {
                    var a = fp[i]; var b = fp[j];
                    // A worker stands at a bay, so a worker may touch one; nothing else may.
                    bool workerAtBay = (a.what.StartsWith("worker") && b.what.StartsWith("bay")) ||
                                       (b.what.StartsWith("worker") && a.what.StartsWith("bay"));
                    float pad = workerAtBay ? -0.3f : Clear;
                    if (Overlap(Corners(a.x, a.y, a.w + pad, a.h + pad, a.rot), Corners(b.x, b.y, b.w + pad, b.h + pad, b.rot)))
                        problems.Add($"{a.what} overlaps {b.what}");
                }

            // The clerk stands behind the counter: within reach of it, on the side away from the door.
            float dx = l.clerk.x - l.counter.x, dy = l.clerk.y - l.counter.y;
            if (Math.Sqrt(dx * dx + dy * dy) > 2.5f) problems.Add("clerk is not at the counter");

            // Every working bay has someone at it.
            var (bw, bh) = BayFootprint(kind);
            float reach = Math.Max(bw, bh) / 2f + 1.6f;
            foreach (var bay in l.bays)
            {
                if (bay.state != "working") continue;
                bool manned = false;
                foreach (var w in l.workers)
                    if (Math.Sqrt((w.x - bay.x) * (w.x - bay.x) + (w.y - bay.y) * (w.y - bay.y)) <= reach) { manned = true; break; }
                if (!manned) problems.Add($"working bay at ({bay.x:0.#}, {bay.y:0.#}) has nobody at it");
            }
            return problems;
        }

        public static (float x, float y)[] Corners(float x, float y, float w, float h, float rotDeg)
        {
            double r = rotDeg * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            var pts = new (float, float)[4];
            int k = 0;
            foreach (var (ox, oy) in new[] { (-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2) })
                pts[k++] = ((float)(x + ox * c - oy * s), (float)(y + ox * s + oy * c));
            return pts;
        }

        // Separating-axis test for two convex quads.
        public static bool Overlap((float x, float y)[] a, (float x, float y)[] b)
        {
            foreach (var poly in new[] { a, b })
                for (int i = 0; i < poly.Length; i++)
                {
                    var p = poly[i]; var q = poly[(i + 1) % poly.Length];
                    float nx = p.y - q.y, ny = q.x - p.x;
                    float amin = float.MaxValue, amax = float.MinValue, bmin = float.MaxValue, bmax = float.MinValue;
                    foreach (var v in a) { float d = nx * v.x + ny * v.y; amin = Math.Min(amin, d); amax = Math.Max(amax, d); }
                    foreach (var v in b) { float d = nx * v.x + ny * v.y; bmin = Math.Min(bmin, d); bmax = Math.Max(bmax, d); }
                    if (amax < bmin || bmax < amin) return false;
                }
            return true;
        }
    }

    // ------------------------------------------------------------------ junkyard project cars

    // The other ladders: a wreck pulled out of a junkyard and rebuilt is the way into a different kind of
    // racing. Placeholder art until the sprites exist (RoadsideShopBuilder draws a tinted silhouette).
    public class AltSeries
    {
        public string id, name, blurb;
        public int powerTier;          // 1 = grassroots .. 3 = feeder series
        public int rebuiltValue;       // what a sound car of this kind is worth
        public float r, g, b;          // placeholder paint
    }

    public class Wreck
    {
        public string id;              // stable within a (yard, week)
        public AltSeries series;
        public int damagePercent;      // 0-100: how much of it needs rebuilding
        public int price;              // as it sits
        public int repairCost;         // to get it race-ready
        public string condition;       // one line for the shelf card
    }

    public static class AltSeriesCatalog
    {
        public static readonly AltSeries[] All =
        {
            new AltSeries { id = "rookie_stocks", name = "Rookie Stocks", powerTier = 1, rebuiltValue = 6500,
                blurb = "Front-wheel-drive saloons with a cage in. Where half the paddock started.", r = 0.85f, g = 0.75f, b = 0.30f },
            new AltSeries { id = "dirt_stocks", name = "Dirt Stocks", powerTier = 1, rebuiltValue = 9500,
                blurb = "Late models built to slide. The bodywork is a suggestion.", r = 0.65f, g = 0.45f, b = 0.30f },
            new AltSeries { id = "single_make_sports", name = "Single-Make Sports Cars", powerTier = 2, rebuiltValue = 19000,
                blurb = "Identical coupés, identical tyres. The driver is the only variable.", r = 0.30f, g = 0.55f, b = 0.85f },
            new AltSeries { id = "sprint_cars", name = "Sprint Cars", powerTier = 2, rebuiltValue = 24000,
                blurb = "Nine hundred horsepower, a wing the size of a door, no starter motor.", r = 0.85f, g = 0.30f, b = 0.25f },
            new AltSeries { id = "indy_lights", name = "Indy Lights", powerTier = 3, rebuiltValue = 48000,
                blurb = "An open-wheel ladder rung. Somebody's season ended against a wall in this one.", r = 0.40f, g = 0.80f, b = 0.45f },
        };

        public static AltSeries Get(string id)
        {
            foreach (var s in All) if (s.id == id) return s;
            return null;
        }

        // What's in a yard this week: 2-4 wrecks, deterministic per (yard, week) like the salvage shelf. The
        // worse the damage the cheaper the car and the dearer the rebuild — a bad wreck is a cheap way in
        // only if you can afford the second bill.
        public static List<Wreck> Wrecks(string yardId, int week)
        {
            var rng = new Random(StableHash(yardId) * 31 + week * 7919);
            int n = 2 + rng.Next(3);
            var list = new List<Wreck>();
            for (int i = 0; i < n; i++)
            {
                var s = All[rng.Next(All.Length)];
                int damage = 25 + rng.Next(66);                                   // 25-90%
                int price = RoundTo(s.rebuiltValue * (1f - damage / 100f) * (0.45f + (float)rng.NextDouble() * 0.25f), 50);
                int repair = RoundTo(s.rebuiltValue * damage / 100f * (0.7f + (float)rng.NextDouble() * 0.3f), 50);
                list.Add(new Wreck
                {
                    id = $"wreck_{week}_{i}_{s.id}", series = s, damagePercent = damage,
                    price = Math.Max(250, price), repairCost = Math.Max(250, repair),
                    condition = damage < 40 ? "Mostly straight. Somebody gave up early."
                              : damage < 65 ? "Bent where it matters, but the bones are good."
                              : "More project than car. Bring a welder and a sense of humour.",
                });
            }
            return list;
        }

        static int RoundTo(float v, int step) => (int)Math.Round(v / step) * step;

        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s ?? "") h = h * 31 + c;
                return h;
            }
        }
    }

    // ------------------------------------------------------------------ fabricators

    // A fabricator sells body shells and chassis at up to its own tier (1 standard .. 3 elite). What it is worth
    // to you depends on your own team's fabricator: each tier builds to a quality, and the shop panel shows how
    // far that is above (or below) what your people turn out. Buying from the road makes sense when your own
    // fabricator is weak, or when the money is there for something your shop can't build at all.
    public static class FabricatorTiers
    {
        public static readonly string[] Names = { "", "Standard", "Pro", "Elite" };
        public static readonly int[] Quality = { 0, 62, 78, 94 };

        public static string Name(int tier) => Names[Clamp(tier)];
        public static int QualityOf(int tier) => Quality[Clamp(tier)];
        static int Clamp(int t) => Math.Max(1, Math.Min(3, t));

        // "+16 on your fabricator" / "-4 on your fabricator" / "level with your fabricator".
        public static string VersusTeam(int tier, int teamRating)
        {
            int d = QualityOf(tier) - teamRating;
            return d == 0 ? "level with your fabricator" : $"{(d > 0 ? "+" : "")}{d} on your fabricator";
        }
    }
}
