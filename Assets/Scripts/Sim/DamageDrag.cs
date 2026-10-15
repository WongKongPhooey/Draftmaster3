using UnityEngine;

namespace Draftmaster.Sim
{
    // When a car is battered enough that its bodywork hangs down onto the track, it drags along the asphalt
    // and throws a stream of sparks behind it. This holds the numbers for that, lifted out of the FX component
    // so they can be pinned in EditMode:
    //
    //   * DentAmount — how dented the car is, read off the deformed mesh itself rather than off the handling
    //     damage figure. 0 = as built, 1 = every panel that can fold folded as far as it can go. The tub is
    //     left out (it never moves), so the number is a share of the metal that is ABLE to dent.
    //   * Intensity — none until the denting passes a threshold, then a smooth ramp to full. A few scrapes
    //     don't drag; a front end pushed back into the wheels does.
    //   * Rate — sparks per second. Metal only sparks while it's being dragged, so a crawl makes nothing and
    //     the stream grows with speed.
    public static class DamageDrag
    {
        // Mean fold depth over the deformable vertices, each as a fraction of how far it could fold.
        // `limit[i]` is that vertex's maximum (0 or less = rigid, skipped). Also reports which vertex is
        // folded furthest in absolute terms — where the bodywork hangs lowest, so where it drags.
        public static float DentAmount(Vector3[] baseVerts, Vector3[] current, float[] limit, out int deepest)
        {
            deepest = -1;
            if (baseVerts == null || current == null || limit == null) return 0f;
            int n = Mathf.Min(baseVerts.Length, Mathf.Min(current.Length, limit.Length));

            float sum = 0f, deepestD = 0f;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (limit[i] <= 0f) continue;
                float d = (current[i] - baseVerts[i]).magnitude;
                sum += Mathf.Clamp01(d / limit[i]);
                count++;
                if (d > deepestD) { deepestD = d; deepest = i; }
            }
            return count > 0 ? sum / count : 0f;
        }

        // 0 below `start`, 1 at `full` and beyond, smooth in between.
        public static float Intensity(float dent01, float start, float full)
        {
            if (full <= start) return dent01 >= start ? 1f : 0f;
            float t = Mathf.Clamp01((dent01 - start) / (full - start));
            return t * t * (3f - 2f * t);
        }

        // Sparks per second for a drag of this intensity at this speed. Nothing below `minSpeed`; the stream
        // reaches `maxRate` at `fullSpeed`.
        public static float Rate(float intensity, float speedMps, float minSpeed, float fullSpeed, float maxRate)
        {
            if (intensity <= 0f || speedMps <= minSpeed || maxRate <= 0f) return 0f;
            float s = fullSpeed > minSpeed ? Mathf.Clamp01((speedMps - minSpeed) / (fullSpeed - minSpeed)) : 1f;
            return maxRate * Mathf.Clamp01(intensity) * s;
        }
    }
}
