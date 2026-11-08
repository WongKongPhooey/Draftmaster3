using UnityEngine;

namespace Draftmaster.Sim
{
    // The racing line as the road's geometry gives it: the path through a corridor with the least total curvature
    // (sum of curvature squared over distance), so the gentlest corners the road allows. It turns in from the
    // outside, clips the inside edge at the apex and runs out to the outside edge on the exit by itself, at every
    // corner of any shape, with nobody authoring it.
    //
    // Why not smooth the lateral offset: relaxing each sample's offset toward its neighbours' (what SplineDriver did
    // to the authored ideal) flattens the OFFSET, and a flat offset is the centreline's own arc. Through a turn that
    // holds the car off the inside, which is why the AI apexed metres wide. Curvature lives in the path.
    //
    // The solve, as motorsport minimum-curvature solvers do it. Moving a path sideways by u (along its own right
    // normal) changes its curvature, to first order, to k - k²u - u'' (k signed, + = turning left; ' = d/ds). That
    // is linear in u, so the sum of its squares is a convex quadratic, minimised here sample by sample with each
    // sample clamped into the corridor (projected Gauss-Seidel). It is only good for small moves - it does not see
    // the heading an out-in-out sweep adds, and on a hairpin it put the whole arc on the outside - so it is
    // re-linearised about the line it produced and solved again (Gauss-Newton), a handful of times.
    //
    // Long smooth bends need information to travel hundreds of samples, which plain sweeps do glacially, so each
    // linear solve runs coarse to fine: every 32nd sample first, that interpolated onto every 16th as the starting
    // guess, and so on down to every sample.
    //
    // Pure maths, no Unity objects, so EditMode tests can drive it (MinCurvatureLineTests).
    public static class MinCurvatureLine
    {
        const float MinGap = 0.05f;   // m; a sample sat on top of its neighbour (a closing duplicate) is not a zero step

        // Lateral offsets (m, + right of travel) for every sample. `centre`/`right` are the centreline samples'
        // positions and unit right normals in one 2D frame, `lo`/`hi` the corridor the line must stay in, `start`
        // the starting guess (null = the centreline, clamped into the corridor). A loop wraps round; an open spline
        // keeps its two end samples where the guess puts them.
        public static float[] Solve(Vector2[] centre, Vector2[] right, float[] lo, float[] hi, float[] start, bool loop,
                                    int outerIterations = 8, int coarsePasses = 300, int finePasses = 150)
        {
            int n = centre.Length;
            var l = new float[n];
            for (int i = 0; i < n; i++) l[i] = Mathf.Clamp(start != null ? start[i] : 0f, lo[i], hi[i]);
            if (n < 5) return l;

            var p = new Vector2[n];
            var dist = new float[n + 1];   // dist[n] = the lap, on a loop
            var k = new float[n];
            var cosPhi = new float[n];
            var u = new float[n];
            var ulo = new float[n];
            var uhi = new float[n];

            for (int it = 0; it < outerIterations; it++)
            {
                for (int i = 0; i < n; i++) p[i] = centre[i] + right[i] * l[i];
                Geometry(p, right, loop, dist, k, cosPhi);

                // Moves along the line's own normal; the corridor is along the centreline's, cos(phi) between them.
                for (int i = 0; i < n; i++)
                {
                    u[i] = 0f;
                    ulo[i] = (lo[i] - l[i]) * cosPhi[i];
                    uhi[i] = (hi[i] - l[i]) * cosPhi[i];
                }
                SolveLinear(dist, k, ulo, uhi, u, loop, coarsePasses, finePasses);
                for (int i = 0; i < n; i++) l[i] = Mathf.Clamp(l[i] + u[i] / cosPhi[i], lo[i], hi[i]);
            }
            return l;
        }

        // Distance along the path, its signed curvature, and how square its normal sits to the corridor's.
        static void Geometry(Vector2[] p, Vector2[] right, bool loop, float[] dist, float[] k, float[] cosPhi)
        {
            int n = p.Length;
            dist[0] = 0f;
            for (int i = 1; i < n; i++) dist[i] = dist[i - 1] + Mathf.Max(MinGap, (p[i] - p[i - 1]).magnitude);
            dist[n] = dist[n - 1] + Mathf.Max(MinGap, (p[0] - p[n - 1]).magnitude);

            var heading = new float[n];
            for (int i = 0; i < n; i++)
            {
                int a = Prev(i, n, loop), b = Next(i, n, loop);
                Vector2 t = p[b] - p[a];
                if (t.sqrMagnitude < 1e-8f) t = new Vector2(-right[i].y, right[i].x);   // tangent from the normal
                t.Normalize();
                heading[i] = Mathf.Atan2(t.y, t.x);
                // Normal of the path (right of travel) against the corridor's; never so oblique the move blows up.
                cosPhi[i] = Mathf.Max(0.3f, Vector2.Dot(new Vector2(t.y, -t.x), right[i]));
            }
            for (int i = 0; i < n; i++)
            {
                int a = Prev(i, n, loop), b = Next(i, n, loop);
                float ds = Gap(dist, a, b, n, loop);
                k[i] = ds > 1e-4f ? Mathf.DeltaAngle(heading[a] * Mathf.Rad2Deg, heading[b] * Mathf.Rad2Deg) * Mathf.Deg2Rad / ds : 0f;
            }
        }

        // Least squares on the linearised curvature, u clamped into [ulo, uhi], coarse to fine.
        static void SolveLinear(float[] dist, float[] k, float[] ulo, float[] uhi, float[] u, bool loop, int coarsePasses, int finePasses)
        {
            int n = u.Length;
            int stride = 1;
            while (stride < 32 && n / (stride * 2) >= 16) stride *= 2;
            for (; stride >= 1; stride /= 2)
            {
                Relax(dist, k, ulo, uhi, u, loop, stride, stride == 1 ? finePasses : coarsePasses);
                if (stride > 1) Fill(ulo, uhi, u, loop, stride);
            }
        }

        // Sweeps over every stride-th sample as if they were the whole line. Point j's curvature error is
        //   e_j = k_j - k_j² u_j - (a_j u_(j-1) + b_j u_j + c_j u_(j+1))
        // with a, b, c the second-difference weights for its uneven gaps; u_i appears in e_(i-1), e_i and e_(i+1),
        // and the u_i that minimises their squares with the rest held still is -sum(g·rest) / sum(g²).
        static void Relax(float[] dist, float[] k, float[] lo, float[] hi, float[] u, bool loop, int stride, int passes)
        {
            int n = u.Length;
            int m = (n + stride - 1) / stride;   // points at this level: 0, stride, 2*stride, ...
            if (m < 5) return;

            var wa = new float[m];
            var wb = new float[m];
            var wc = new float[m];
            for (int j = 0; j < m; j++)
            {
                float h1 = j > 0 || loop ? Gap(dist, Index(j - 1, m, stride, loop), j * stride, n, loop) : 0f;
                float h2 = j < m - 1 || loop ? Gap(dist, j * stride, Index(j + 1, m, stride, loop), n, loop) : 0f;
                if (h1 <= 0f) h1 = h2;
                if (h2 <= 0f) h2 = h1;
                h1 = Mathf.Max(h1, MinGap);
                h2 = Mathf.Max(h2, MinGap);
                wa[j] = 2f / (h1 * (h1 + h2));
                wc[j] = 2f / (h2 * (h1 + h2));
                wb[j] = -(wa[j] + wc[j]);
            }

            for (int pass = 0; pass < passes; pass++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (!loop && (j < 1 || j > m - 2)) continue;   // an open line's ends stay put
                    int i = j * stride;
                    float num = 0f, den = 0f;
                    for (int jj = j - 1; jj <= j + 1; jj++)
                    {
                        if (!loop && (jj < 1 || jj > m - 2)) continue;
                        int w = loop ? ((jj % m) + m) % m : jj;
                        int q = w * stride;
                        float kq = k[q];
                        float e = kq - kq * kq * u[q]
                                  - (wa[w] * u[Index(w - 1, m, stride, loop)] + wb[w] * u[q] + wc[w] * u[Index(w + 1, m, stride, loop)]);
                        float g = jj == j ? -(kq * kq) - wb[w] : jj == j - 1 ? -wc[w] : -wa[w];
                        float rest = e - g * u[i];
                        num += g * rest;
                        den += g * g;
                    }
                    if (den > 0f) u[i] = Mathf.Clamp(-num / den, lo[i], hi[i]);
                }
            }
        }

        // The samples between this level's points, given the straight run of offset between them as the next
        // level's starting guess.
        static void Fill(float[] lo, float[] hi, float[] u, bool loop, int stride)
        {
            int n = u.Length;
            for (int i0 = 0; i0 < n; i0 += stride)
            {
                int i1 = i0 + stride;
                if (i1 >= n) i1 = loop ? 0 : n - 1;
                int span = i1 > i0 ? i1 - i0 : n - i0;
                for (int s = 1; s < span && i0 + s < n; s++)
                {
                    int i = i0 + s;
                    u[i] = Mathf.Clamp(Mathf.Lerp(u[i0], u[i1], s / (float)span), lo[i], hi[i]);
                }
            }
        }

        // Path distance from sample a forward to sample b (across the wrap on a loop).
        static float Gap(float[] dist, int a, int b, int n, bool loop)
        {
            float g = dist[b] - dist[a];
            if (g <= 0f && loop) g += dist[n];
            return Mathf.Max(0f, g);
        }

        static int Index(int j, int m, int stride, bool loop)
        {
            if (loop) j = ((j % m) + m) % m;
            else j = Mathf.Clamp(j, 0, m - 1);
            return j * stride;
        }

        static int Prev(int i, int n, bool loop) => i > 0 ? i - 1 : (loop ? n - 1 : 0);
        static int Next(int i, int n, bool loop) => i < n - 1 ? i + 1 : (loop ? 0 : n - 1);
    }
}
