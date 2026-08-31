using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // What colour is that car, in two colours?
    //
    // Every car in the game already has its paint: a 64x32 livery sprite. Asking an author to type the same
    // colours into a table again is data entry that will drift from the art, so the table is SEEDED from the
    // art and only hand-corrected where the machine picks badly (see CarColours / the editor tool that
    // fills it in).
    //
    // The picking rules are the whole trick, and they are the reason this is a pure function with tests
    // rather than a few lines inside the importer:
    //
    //   * transparent pixels are not paint,
    //   * near-black is not paint either — it is tyres, glass, shadow and the outline every sprite is drawn
    //     with, and it is the most common colour on most liveries if you let it count,
    //   * the primary is simply the most common colour that survives those two rules,
    //   * the secondary has to be VISIBLY different from the primary, or a car painted in two blues reports
    //     two colours nobody can tell apart; when there is no such colour, a shade of the primary is a
    //     better answer than a wrong one.
    public static class LiveryPalette
    {
        public struct Pair
        {
            public Color primary;
            public Color secondary;
        }

        // Alpha at or below this is not paint.
        const byte MinAlpha = 128;

        // Brightest channel at or below this is outline / tyre / glass rather than bodywork.
        const int MinChannel = 44;

        // How far apart (0..1 RGB distance) two colours must be to count as a second colour rather than a
        // shade of the first. Roughly: white vs. cream fails, white vs. mid-blue passes.
        const float DistinctEnough = 0.28f;

        // A colour has to cover this much of the paint to be the secondary; below it, it is a detail — a
        // sponsor patch, a number outline — not the car's other colour.
        const float MinSecondaryShare = 0.04f;

        // Colours are bucketed 5 bits per channel: two pixels of almost the same red should count together,
        // and 32 levels is fine enough that two deliberately different colours never share a bucket.
        const int Shift = 3;

        public static Pair Extract(Color32[] pixels)
        {
            var counts = new Dictionary<int, int>();
            var sums = new Dictionary<int, Vector3>();
            int total = 0;

            if (pixels != null)
            {
                foreach (var p in pixels)
                {
                    if (p.a < MinAlpha) continue;
                    if (Mathf.Max(p.r, Mathf.Max(p.g, p.b)) <= MinChannel) continue;

                    int key = ((p.r >> Shift) << 10) | ((p.g >> Shift) << 5) | (p.b >> Shift);
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                    sums.TryGetValue(key, out Vector3 s);
                    sums[key] = s + new Vector3(p.r, p.g, p.b);
                    total++;
                }
            }

            if (total == 0) return new Pair { primary = Color.white, secondary = new Color(0.6f, 0.6f, 0.6f) };

            int firstKey = 0, firstCount = 0;
            foreach (var kv in counts)
                if (kv.Value > firstCount) { firstCount = kv.Value; firstKey = kv.Key; }

            Color primary = Average(sums[firstKey], firstCount);

            // The other colour: the most common bucket that actually looks different from the primary.
            int secondKey = 0, secondCount = 0;
            foreach (var kv in counts)
            {
                if (kv.Key == firstKey || kv.Value <= secondCount) continue;
                if (kv.Value < total * MinSecondaryShare) continue;
                if (Distance(Average(sums[kv.Key], kv.Value), primary) < DistinctEnough) continue;
                secondCount = kv.Value;
                secondKey = kv.Key;
            }

            Color secondary = secondCount > 0 ? Average(sums[secondKey], secondCount) : Shade(primary);
            return new Pair { primary = primary, secondary = secondary };
        }

        static Color Average(Vector3 sum, int count) =>
            new Color(sum.x / count / 255f, sum.y / count / 255f, sum.z / count / 255f);

        public static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db) / Mathf.Sqrt(3f);
        }

        // A one-colour car still needs a trim colour. Go darker off a light car and lighter off a dark one,
        // which is what a painter would do and always reads as deliberate.
        public static Color Shade(Color c)
        {
            float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            return luma > 0.5f
                ? new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f)
                : new Color(Mathf.Lerp(c.r, 1f, 0.55f), Mathf.Lerp(c.g, 1f, 0.55f), Mathf.Lerp(c.b, 1f, 0.55f), 1f);
        }
    }
}
