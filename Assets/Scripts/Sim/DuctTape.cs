using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // Where the crew stick tape on a car they have just straightened. A repaired panel is never quite the
    // panel it was: the dent comes out, and a few grey strips of duct tape hold whatever is left together.
    // The tape goes where the metal actually moved back — not where the car was hit hardest, but where the
    // repair did its work — so a car that took a shunt in the left rear comes out of the pits taped up in
    // the left rear.
    //
    // Pure placement maths, pinned in EditMode. VehicleDamage feeds Accumulate on every repair and asks Place
    // for any new strips; DuctTapePatches draws them.
    //
    // All positions are in the bodywork's local space, which is the sprite's: x runs along the car, y across.
    public static class DuctTape
    {
        public struct Strip
        {
            public Vector2 center;   // undeformed local position
            public float angleDeg;   // 0 = along the car (local x)
            public float length;
            public float width;
            public int anchor;       // mesh vertex the strip rides on when the panel is hit again
            public float shade;      // 0..1, picks between the tape greys
        }

        // Add how far each vertex was pulled back toward where it was built. Call with the mesh as it was
        // before the repair and as it is after.
        public static void Accumulate(float[] repaired, Vector3[] baseVerts, Vector3[] before, Vector3[] after)
        {
            if (repaired == null || baseVerts == null || before == null || after == null) return;
            int n = Mathf.Min(repaired.Length, Mathf.Min(baseVerts.Length, Mathf.Min(before.Length, after.Length)));
            for (int i = 0; i < n; i++)
            {
                float gained = ((Vector2)(before[i] - baseVerts[i])).magnitude - ((Vector2)(after[i] - baseVerts[i])).magnitude;
                if (gained > 0f) repaired[i] += gained;
            }
        }

        public struct Settings
        {
            public float threshold;     // repaired distance a vertex needs before it gets tape
            public float spacing;       // minimum distance between two patches
            public int maxPatches;
            public float pixel;         // local units per sprite pixel; strips are sized and snapped in pixels
            public Rect area;           // the whole of every strip stays inside this: the car's collider, cut to the sprite
            public int minLengthPx, maxLengthPx, widthPx, maxStripsPerPatch;
        }

        // Turn accumulated repair into patches of tape, most-repaired spot first. Each patch is one to
        // `maxStripsPerPatch` parallel strips laid side by side. `onBody` (optional) rejects spots over
        // empty sprite pixels: every pixel a strip covers has to be car, so tape never hangs off the outline.
        // A strip that would is nudged in toward the middle of the car, and dropped if that doesn't land it.
        // Returns strips added.
        public static int Place(List<Strip> strips, int patchCount, float[] repaired, Vector3[] baseVerts,
                                in Settings s, System.Random rng, System.Func<Vector2, bool> onBody,
                                out int patchesAdded)
        {
            patchesAdded = 0;
            if (strips == null || repaired == null || baseVerts == null || rng == null) return 0;
            int n = Mathf.Min(repaired.Length, baseVerts.Length);
            int added = 0;
            float pixel = Mathf.Max(1e-5f, s.pixel);

            while (patchCount + patchesAdded < s.maxPatches)
            {
                int best = -1;
                float bestR = s.threshold;
                for (int i = 0; i < n; i++)
                {
                    if (repaired[i] < bestR) continue;
                    if (NearAny(strips, baseVerts[i], s.spacing)) continue;
                    if (onBody != null && !onBody(baseVerts[i])) continue;
                    bestR = repaired[i];
                    best = i;
                }
                if (best < 0) break;

                int got = AddPatch(strips, baseVerts, best, s, pixel, rng, onBody);
                repaired[best] = 0f;
                if (got == 0) continue;
                added += got;
                patchesAdded++;
            }
            return added;
        }

        static bool NearAny(List<Strip> strips, Vector2 p, float spacing)
        {
            float sq = spacing * spacing;
            for (int i = 0; i < strips.Count; i++)
                if ((strips[i].center - p).sqrMagnitude < sq) return true;
            return false;
        }

        // How far (pixels) a strip that hangs off the outline may be slid in toward the middle of the car.
        const int MaxNudgePx = 6;

        static int AddPatch(List<Strip> strips, Vector3[] baseVerts, int vertex, in Settings s, float pixel,
                            System.Random rng, System.Func<Vector2, bool> onBody)
        {
            // Mostly along the panel or straight across it, the odd one on the slant — the way tape actually
            // goes on in a hurry.
            double roll = rng.NextDouble();
            float angle = roll < 0.45 ? 0f : roll < 0.8 ? 90f : (rng.NextDouble() < 0.5 ? 30f : -30f);
            angle += (float)(rng.NextDouble() * 10.0 - 5.0);

            int count = 1 + rng.Next(Mathf.Max(1, s.maxStripsPerPatch));
            int lenPx = s.minLengthPx + rng.Next(Mathf.Max(1, s.maxLengthPx - s.minLengthPx + 1));
            float length = Mathf.Max(1, lenPx) * pixel;
            float width = Mathf.Max(1, s.widthPx) * pixel;

            float rad = angle * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var across = new Vector2(-along.y, along.x);
            Vector2 center = baseVerts[vertex];
            int placed = 0;

            for (int k = 0; k < count; k++)
            {
                // Side by side, each a little overlapping the last, and a pixel or two staggered so the
                // ends don't line up like a printed stripe.
                float offset = (k - (count - 1) * 0.5f) * width * 0.85f;
                float stagger = (rng.Next(3) - 1) * pixel;
                Vector2 c = center + across * offset + along * stagger;
                c = Snap(c, pixel);
                c = Clamp(c, along, across, length, width, s.area);

                bool fits = Covered(c, along, across, length, width, pixel, onBody);
                for (int step = 0; !fits && step < MaxNudgePx; step++)
                {
                    Vector2 toward = s.area.center - c;
                    if (toward.sqrMagnitude < pixel * pixel) break;
                    c = Clamp(c + toward.normalized * pixel, along, across, length, width, s.area);
                    fits = Covered(c, along, across, length, width, pixel, onBody);
                }
                if (!fits) continue;

                placed++;
                strips.Add(new Strip
                {
                    center = c,
                    angleDeg = angle,
                    length = length,
                    width = width,
                    anchor = Nearest(baseVerts, c),
                    shade = (float)rng.NextDouble(),
                });
            }
            return placed;
        }

        static Vector2 Snap(Vector2 p, float pixel) =>
            new(Mathf.Round(p.x / pixel) * pixel, Mathf.Round(p.y / pixel) * pixel);

        // Keep the whole strip inside the area rectangle.
        static Vector2 Clamp(Vector2 c, Vector2 along, Vector2 across, float length, float width, Rect area)
        {
            float ex = Mathf.Abs(along.x) * length * 0.5f + Mathf.Abs(across.x) * width * 0.5f;
            float ey = Mathf.Abs(along.y) * length * 0.5f + Mathf.Abs(across.y) * width * 0.5f;
            float mx = Mathf.Max(0f, area.width * 0.5f - ex), my = Mathf.Max(0f, area.height * 0.5f - ey);
            Vector2 mid = area.center;
            return new Vector2(Mathf.Clamp(c.x, mid.x - mx, mid.x + mx), Mathf.Clamp(c.y, mid.y - my, mid.y + my));
        }

        // Whether every pixel under a strip is car. Samples a half-pixel grid over it — fine enough that a
        // slanted strip can't straddle a clear pixel between samples — a hair in from the edges, so an edge
        // lying exactly on a pixel boundary isn't read as the clear pixel beyond it.
        static bool Covered(Vector2 c, Vector2 along, Vector2 across, float length, float width, float pixel,
                            System.Func<Vector2, bool> onBody)
        {
            if (onBody == null) return true;
            float inset = pixel * 0.05f;
            float hl = Mathf.Max(0f, length * 0.5f - inset), hw = Mathf.Max(0f, width * 0.5f - inset);
            int nl = Mathf.Max(1, Mathf.CeilToInt(2f * length / pixel)), nw = Mathf.Max(1, Mathf.CeilToInt(2f * width / pixel));
            for (int i = 0; i <= nl; i++)
            {
                float t = -hl + 2f * hl * i / nl;
                for (int j = 0; j <= nw; j++)
                {
                    float u = -hw + 2f * hw * j / nw;
                    if (!onBody(c + along * t + across * u)) return false;
                }
            }
            return true;
        }

        public static int Nearest(Vector3[] verts, Vector2 p)
        {
            int best = 0;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < verts.Length; i++)
            {
                float d = ((Vector2)verts[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // The four corners of a strip, counter-clockwise, at `center` (pass the strip's centre shifted by
        // however its anchor vertex has since been dented).
        public static void Corners(in Strip strip, Vector2 center, Vector2[] four)
        {
            float rad = strip.angleDeg * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (strip.length * 0.5f);
            var across = new Vector2(-along.y, along.x).normalized * (strip.width * 0.5f);
            four[0] = center - along - across;
            four[1] = center + along - across;
            four[2] = center + along + across;
            four[3] = center - along + across;
        }
    }
}
