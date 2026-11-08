using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The road's own racing line (MinCurvatureLine): out-in-out through a corner, with nothing authored.
public class MinCurvatureLineTests
{
    const float Half = 5f;   // corridor half-width: a 12.8 m road less RoadEdgeMargin

    // A straight, a 90-degree right-hander of radius `radius`, another straight; samples every `step` metres.
    static void RightHander(float radius, float straight, float step,
                            out Vector2[] c, out Vector2[] r, out float[] lo, out float[] hi, out float arcStart, out float arcEnd)
    {
        var cs = new List<Vector2>();
        var ts = new List<Vector2>();
        for (float s = 0f; s < straight; s += step) { cs.Add(new Vector2(s, 0f)); ts.Add(Vector2.right); }
        arcStart = cs.Count * step;
        float arc = radius * Mathf.PI * 0.5f;
        for (float s = 0f; s < arc; s += step)
        {
            float a = s / radius;   // clockwise about (straight, -radius)
            cs.Add(new Vector2(straight + radius * Mathf.Sin(a), -radius + radius * Mathf.Cos(a)));
            ts.Add(new Vector2(Mathf.Cos(a), -Mathf.Sin(a)));
        }
        arcEnd = cs.Count * step;
        for (float s = 0f; s < straight; s += step) { cs.Add(new Vector2(straight + radius, -radius - s)); ts.Add(Vector2.down); }

        int n = cs.Count;
        c = cs.ToArray();
        r = new Vector2[n];
        lo = new float[n];
        hi = new float[n];
        for (int i = 0; i < n; i++)
        {
            r[i] = new Vector2(ts[i].y, -ts[i].x);   // right of travel, as SplineDriver
            lo[i] = -Half;
            hi[i] = Half;
        }
    }

    [Test]
    public void ARightHanderIsTakenOutsideInsideOutside()
    {
        const float step = 2f;
        RightHander(40f, 200f, step, out var c, out var r, out var lo, out var hi, out float arcStart, out float arcEnd);
        var l = MinCurvatureLine.Solve(c, r, lo, hi, null, loop: false);

        int apex = Mathf.RoundToInt((arcStart + arcEnd) * 0.5f / step);
        int entry = Mathf.RoundToInt((arcStart - 30f) / step);
        int exit = Mathf.RoundToInt((arcEnd + 30f) / step);
        Assert.Greater(l[apex], Half - 0.3f, $"apex should clip the inside (right) edge, was {l[apex]:0.00} m");
        Assert.Less(l[entry], -Half + 1.5f, $"turn-in should come from the outside (left), was {l[entry]:0.00} m");
        Assert.Less(l[exit], -Half + 1.5f, $"the exit should run out to the outside (left), was {l[exit]:0.00} m");
    }

    [Test]
    public void TheLineStaysInsideTheCorridor()
    {
        RightHander(25f, 150f, 2f, out var c, out var r, out var lo, out var hi, out _, out _);
        var l = MinCurvatureLine.Solve(c, r, lo, hi, null, loop: false);
        for (int i = 0; i < l.Length; i++)
            Assert.That(l[i], Is.InRange(lo[i], hi[i]), $"sample {i}");
    }

    [Test]
    public void ItsTightestRadiusIsWiderThanTheCentrelines()
    {
        RightHander(40f, 200f, 2f, out var c, out var r, out var lo, out var hi, out _, out _);
        var l = MinCurvatureLine.Solve(c, r, lo, hi, null, loop: false);
        float line = TightestRadius(c, r, l), centre = TightestRadius(c, r, new float[l.Length]);
        Assert.Greater(line, 1.3f * centre, $"tightest radius {line:0.0} m against the centreline's {centre:0.0} m");
    }

    // A hairpin is where a first-order model gives up: linearised once, it put the whole arc out on the outside
    // edge. Re-linearising about each answer must still find out-in-out, and open the radius right up.
    [Test]
    public void AHairpinIsStillOutsideInsideOutside()
    {
        const float step = 2f;
        RightHander(15f, 150f, step, out var c, out var r, out var lo, out var hi, out float arcStart, out float arcEnd);
        var l = MinCurvatureLine.Solve(c, r, lo, hi, null, loop: false);

        int apex = Mathf.RoundToInt((arcStart + arcEnd) * 0.5f / step);
        Assert.Greater(l[apex], Half - 0.5f, $"apex should clip the inside edge, was {l[apex]:0.00} m");
        Assert.Less(l[Mathf.RoundToInt((arcStart - 30f) / step)], -Half + 1.5f, "turn-in from the outside");
        Assert.Less(l[Mathf.RoundToInt((arcEnd + 30f) / step)], -Half + 1.5f, "out to the outside on the exit");
        float line = TightestRadius(c, r, l), centre = TightestRadius(c, r, new float[l.Length]);
        Assert.Greater(line, 1.8f * centre, $"tightest radius {line:0.0} m against the centreline's {centre:0.0} m");
    }

    // Smallest circle through three points 6 m apart, anywhere on the line.
    static float TightestRadius(Vector2[] c, Vector2[] r, float[] l)
    {
        float best = float.MaxValue;
        for (int i = 3; i < c.Length - 3; i++)
        {
            Vector2 a = c[i - 3] + r[i - 3] * l[i - 3], b = c[i] + r[i] * l[i], d = c[i + 3] + r[i + 3] * l[i + 3];
            float cross = Mathf.Abs((b.x - a.x) * (d.y - a.y) - (b.y - a.y) * (d.x - a.x));
            if (cross < 1e-5f) continue;
            best = Mathf.Min(best, (a - b).magnitude * (b - d).magnitude * (d - a).magnitude / (2f * cross));
        }
        return best;
    }
}
