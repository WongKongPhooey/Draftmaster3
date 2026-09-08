using System;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // Offline racing-line trainer.
    //
    // The AI line has always been the authored ideal relaxed toward minimum curvature. Minimum curvature is
    // not minimum lap time: it rounds every corner symmetrically, where a quick lap sacrifices entry to
    // straighten the exit, brakes in a straight line and gets back on the power early. That difference is a
    // large part of what reads as "the AI drive unnaturally" — every car traces the same geometric arc through
    // a corner instead of the shape a driver would actually use, and none of them is near the pace the car is
    // capable of.
    //
    // So rather than assert a better line, drive one. Simulate a lap: the DRIVEN line's real curvature sets a
    // cornering speed at every point, then friction-circle-limited acceleration and braking passes relax that
    // into speeds the car can actually reach and leave. That is the same model SplineDriver bakes for its
    // speed profile, so a line that is quicker here is quicker in the game. Train() then drives that lap over
    // and over, each pass nudging one stretch of the line a little wider or a little tighter and keeping the
    // change only when the lap came out faster. A few hundred laps later the line has found its own late
    // apexes and straight-line braking zones — nobody authored them, and they are quick because they were
    // measured, not guessed.
    //
    // Pure maths: no scene, no components, no assets. The editor tool feeds it geometry and car limits and
    // writes the winning line out; EditMode tests drive it directly.
    public static class RacingLineTrainer
    {
        // --- Inputs -------------------------------------------------------------------------------------

        // The road, sampled. Everything is per-sample and index-aligned; lateral offsets are metres along
        // `right` (positive is right of travel, matching TrackBuilder.Sample.normal and SplineDriver).
        public sealed class Course
        {
            public Vector2[] centre;          // centreline position
            public Vector2[] right;           // unit lateral basis at that point
            public float[] minLateral;        // one edge of the corridor the line may use
            public float[] maxLateral;        // the other edge
            public float[] speedCapMps;       // optional hard cap per sample (top speed / authored segment cap)
            public float[] bankingBonusMps;   // optional cornering-speed bonus per sample (banking)
            public bool loop = true;

            public int Count => centre != null ? centre.Length : 0;

            public bool IsValid(out string why)
            {
                why = null;
                int n = Count;
                if (n < 8) { why = "course needs at least 8 samples"; return false; }
                if (right == null || right.Length != n) { why = "right[] length must match centre[]"; return false; }
                if (minLateral == null || minLateral.Length != n) { why = "minLateral[] length must match centre[]"; return false; }
                if (maxLateral == null || maxLateral.Length != n) { why = "maxLateral[] length must match centre[]"; return false; }
                if (speedCapMps != null && speedCapMps.Length != n) { why = "speedCapMps[] length must match centre[]"; return false; }
                if (bankingBonusMps != null && bankingBonusMps.Length != n) { why = "bankingBonusMps[] length must match centre[]"; return false; }
                return true;
            }
        }

        // What the car can do. Longitudinal authority is a function of speed so the real accel/decel curves
        // can be handed straight in; if they are missing, the flat fallbacks stand in.
        public struct CarLimits
        {
            public float lateralAccelMps2;    // maxLateralG x grip x 9.81 — the cornering ceiling
            public float topSpeedMps;
            public Func<float, float> AccelAtMps;   // longitudinal accel (m/s^2) available at this speed
            public Func<float, float> DecelAtMps;   // braking (m/s^2) available at this speed
            public float fallbackAccelMps2;
            public float fallbackDecelMps2;
            // Margin the AI keeps off the theoretical cornering limit — SplineDriver's cornerSpeedScale. It
            // trims the commanded corner speed but NOT the real grip ceiling, so the friction circle stays
            // honest and the trained line is the one that is quick for a car driving with that margin.
            public float cornerSpeedScale;

            public float CornerScale => cornerSpeedScale > 0.01f ? cornerSpeedScale : 1f;

            public float Accel(float vMps)
            {
                float a = AccelAtMps != null ? AccelAtMps(vMps) : fallbackAccelMps2;
                return Mathf.Max(0.05f, a);
            }

            public float Decel(float vMps)
            {
                float d = DecelAtMps != null ? DecelAtMps(vMps) : fallbackDecelMps2;
                return Mathf.Max(0.05f, d);
            }

            public static CarLimits Simple(float lateralAccelMps2, float topSpeedMps, float accel, float decel)
                => new CarLimits
                {
                    lateralAccelMps2 = lateralAccelMps2,
                    topSpeedMps = topSpeedMps,
                    fallbackAccelMps2 = accel,
                    fallbackDecelMps2 = decel
                };
        }

        // How hard to train, and at what scale.
        //
        // The scale matters more than the effort. A trial that moves 20m of road cannot discover the shape of
        // a corner: widening the entry on its own lengthens the lap and slows it down, and only pays once the
        // apex has come in to meet it, so a greedy search working one short stretch at a time never takes the
        // first step. Working coarse-to-fine fixes that — early rounds move a whole corner's worth of road at
        // once and find the in-apex-out shape, later rounds shrink down to polish where the apex sits. Both
        // the span and the amplitude anneal, so this is one knob turned in two places.
        public struct Settings
        {
            public int rounds;
            public int maxPassesPerRound;     // a round keeps sweeping until a whole pass finds nothing
            public float startAmplitude;      // metres of lateral movement a trial bump asks for
            public float endAmplitude;
            public float startHalfWidth;      // metres of road a trial bump reaches either side, first round
            public float endHalfWidth;        // ...and last round
            public float controlSpacingFactor;// control points sit this fraction of the bump width apart
            public float minGainSeconds;      // a trial must beat the current lap by this much to be kept
            public float edgeMargin;          // metres held back from the corridor edge

            public static Settings Default => new Settings
            {
                rounds = 6,
                maxPassesPerRound = 4,
                startAmplitude = 2.5f,
                endAmplitude = 0.1f,
                startHalfWidth = 70f,
                endHalfWidth = 14f,
                controlSpacingFactor = 0.4f,
                minGainSeconds = 0.0005f,
                edgeMargin = 0.3f
            };

            // Bristol is 858m and Talladega is 4281m; one absolute span cannot serve both — on the short
            // track the coarse pass would swallow the whole lap, on the superspeedway it would be a ripple.
            // Scaling with the lap keeps "a corner's worth of road" meaning the same thing everywhere.
            public static Settings For(float trackLengthMetres)
            {
                var s = Default;
                s.startHalfWidth = Mathf.Clamp(trackLengthMetres / 12f, 35f, 220f);
                s.endHalfWidth = Mathf.Clamp(trackLengthMetres / 70f, 6f, 30f);
                return s;
            }

            public float HalfWidthAt(float t01)
            {
                float start = Mathf.Max(1f, startHalfWidth);
                float end = Mathf.Clamp(endHalfWidth, 1f, start);
                return start * Mathf.Pow(end / start, Mathf.Clamp01(t01));  // geometric: each round halves in
            }

            public float AmplitudeAt(float t01) => Mathf.Lerp(startAmplitude, endAmplitude, Mathf.Clamp01(t01));
        }

        // --- Outputs ------------------------------------------------------------------------------------

        public struct Report
        {
            public float[] lateral;           // the trained line, per course sample
            public float seedLapTime;
            public float trainedLapTime;
            public int lapsSimulated;
            public int improvements;
            public float drivenLength;        // length of the trained line itself (m)

            public float GainSeconds => seedLapTime - trainedLapTime;
        }

        public struct Telemetry
        {
            public float lapTimeSeconds;
            public float drivenLength;
            public Vector2[] points;
            public float[] speedMps;
            public float[] curvature;
        }

        // Scratch arrays, reused across the thousands of laps a training run drives so the optimiser does not
        // spend its time in the garbage collector.
        public sealed class Workspace
        {
            public Vector2[] pts;
            public float[] ds;        // distance from sample i to sample i+1 along the DRIVEN line
            public float[] kappa;
            public float[] kappaTmp;
            public float[] v;
            public float[] candidate;

            public void Ensure(int n)
            {
                if (pts != null && pts.Length == n) return;
                pts = new Vector2[n];
                ds = new float[n];
                kappa = new float[n];
                kappaTmp = new float[n];
                v = new float[n];
                candidate = new float[n];
            }
        }

        // --- Lap simulation -----------------------------------------------------------------------------

        // Seconds for one lap of `lateral`. This is the whole fitness function: everything else in here just
        // decides which lateral profile to hand it next.
        public static float LapTime(Course course, float[] lateral, CarLimits limits, Workspace ws = null)
        {
            int n = course != null ? course.Count : 0;
            if (n < 3 || lateral == null || lateral.Length != n) return float.MaxValue;
            if (ws == null) ws = new Workspace();
            ws.Ensure(n);

            BuildDrivenPath(course, lateral, ws);
            BuildCurvature(course, ws);
            BuildSpeeds(course, limits, ws);

            // Trapezoidal in 1/v: over a step this short the speed is near enough linear, so the average
            // speed over the step is the mean of its endpoints.
            float t = 0f;
            int last = course.loop ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                int next = (i + 1) % n;
                float vAvg = 0.5f * (ws.v[i] + ws.v[next]);
                if (vAvg < 0.5f) vAvg = 0.5f;
                t += ws.ds[i] / vAvg;
            }
            return t;
        }

        // The same lap, with the numbers kept — for tests, reports and anything that wants to draw it.
        public static Telemetry Analyse(Course course, float[] lateral, CarLimits limits)
        {
            var tel = new Telemetry();
            int n = course != null ? course.Count : 0;
            if (n < 3 || lateral == null || lateral.Length != n) return tel;

            var ws = new Workspace();
            ws.Ensure(n);
            tel.lapTimeSeconds = LapTime(course, lateral, limits, ws);
            tel.points = (Vector2[])ws.pts.Clone();
            tel.speedMps = (float[])ws.v.Clone();
            tel.curvature = (float[])ws.kappa.Clone();
            float len = 0f;
            int last = course.loop ? n : n - 1;
            for (int i = 0; i < last; i++) len += ws.ds[i];
            tel.drivenLength = len;
            return tel;
        }

        static void BuildDrivenPath(Course course, float[] lateral, Workspace ws)
        {
            int n = course.Count;
            for (int i = 0; i < n; i++) ws.pts[i] = course.centre[i] + course.right[i] * lateral[i];
            for (int i = 0; i < n; i++)
            {
                int next = i + 1;
                if (next >= n)
                {
                    if (!course.loop) { ws.ds[i] = 0f; continue; }
                    next = 0;
                }
                ws.ds[i] = Vector2.Distance(ws.pts[i], ws.pts[next]);
            }
        }

        // Menger curvature of the driven line. Segment joins can emit near-coincident samples, so step
        // outward until the neighbours are far enough apart for the three-point estimate to be stable — the
        // same guard SplineDriver's profile uses, for the same reason.
        static void BuildCurvature(Course course, Workspace ws)
        {
            int n = course.Count;
            for (int i = 0; i < n; i++)
            {
                int prev = StepDistinct(ws.pts, i, -1, 0.5f, course.loop);
                int next = StepDistinct(ws.pts, i, +1, 0.5f, course.loop);
                ws.kappa[i] = (prev == i || next == i || prev == next)
                    ? 0f
                    : MengerCurvature(ws.pts[prev], ws.pts[i], ws.pts[next]);
            }

            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    int prev = i == 0 ? (course.loop ? n - 1 : 0) : i - 1;
                    int next = i == n - 1 ? (course.loop ? 0 : n - 1) : i + 1;
                    ws.kappaTmp[i] = (ws.kappa[prev] + ws.kappa[i] + ws.kappa[next]) / 3f;
                }
                var swap = ws.kappa; ws.kappa = ws.kappaTmp; ws.kappaTmp = swap;
            }
        }

        static void BuildSpeeds(Course course, CarLimits limits, Workspace ws)
        {
            int n = course.Count;
            float aLat = Mathf.Max(0.1f, limits.lateralAccelMps2);
            float top = limits.topSpeedMps > 0.1f ? limits.topSpeedMps : 90f;

            float cornerScale = limits.CornerScale;
            for (int i = 0; i < n; i++)
            {
                float k = ws.kappa[i];
                float corner = float.MaxValue;
                if (k > 1e-5f)
                {
                    corner = Mathf.Sqrt(aLat / k);
                    if (course.bankingBonusMps != null) corner += course.bankingBonusMps[i];
                    corner *= cornerScale;
                }
                float cap = (course.speedCapMps != null && course.speedCapMps[i] > 0.1f)
                    ? Mathf.Min(top, course.speedCapMps[i])
                    : top;
                ws.v[i] = Mathf.Clamp(Mathf.Min(corner, cap), 2f, cap);
            }

            // Two wrap-aware passes per direction so the limits settle across the loop seam.
            int passes = course.loop ? 2 : 1;
            for (int p = 0; p < passes; p++)
            {
                for (int i = 1; i < n; i++) AccelLimit(limits, ws, i, i - 1, aLat);
                if (course.loop) AccelLimit(limits, ws, 0, n - 1, aLat);
            }
            for (int p = 0; p < passes; p++)
            {
                for (int i = n - 2; i >= 0; i--) BrakeLimit(limits, ws, i, i + 1, aLat);
                if (course.loop) BrakeLimit(limits, ws, n - 1, 0, aLat);
            }
        }

        static void AccelLimit(CarLimits limits, Workspace ws, int i, int prev, float aLat)
        {
            float d = ws.ds[prev];
            // Coincident samples are the same place, so they are the same speed. Skipping instead would
            // decouple the two ends of the lap — a sampler that closes the loop by repeating the start point
            // leaves exactly that gap, and the car gets to change speed across start/finish for free.
            if (d <= 0f)
            {
                if (ws.v[prev] < ws.v[i]) ws.v[i] = ws.v[prev];
                return;
            }
            float vPrev = ws.v[prev];
            float a = limits.Accel(vPrev) * Headroom(ws, prev, vPrev, aLat);
            float vMax = Mathf.Sqrt(vPrev * vPrev + 2f * a * d);
            if (vMax < ws.v[i]) ws.v[i] = vMax;
        }

        static void BrakeLimit(CarLimits limits, Workspace ws, int i, int next, float aLat)
        {
            float d = ws.ds[i];
            if (d <= 0f)
            {
                if (ws.v[next] < ws.v[i]) ws.v[i] = ws.v[next];
                return;
            }
            float vNext = ws.v[next];
            float b = limits.Decel(ws.v[i]) * Headroom(ws, i, ws.v[i], aLat);
            float vMax = Mathf.Sqrt(vNext * vNext + 2f * b * d);
            if (vMax < ws.v[i]) ws.v[i] = vMax;
        }

        // Friction circle: longitudinal grip is whatever the corner has not already spent. This is what pushes
        // the braking zone back up the straight instead of assuming full retardation mid-corner, and it is why
        // a line that straightens its exit is worth time — it frees up drive, not just cornering speed.
        static float Headroom(Workspace ws, int idx, float vMps, float aLat)
        {
            float frac = vMps * vMps * ws.kappa[idx] / aLat;
            return Mathf.Max(0.15f, Mathf.Sqrt(Mathf.Clamp01(1f - frac * frac)));
        }

        static int StepDistinct(Vector2[] pts, int from, int dir, float minDist, bool loop)
        {
            int n = pts.Length;
            int idx = from;
            for (int k = 0; k < 8; k++)
            {
                int cand = idx + dir;
                if (loop) cand = ((cand % n) + n) % n;
                else if (cand < 0 || cand >= n) return idx;
                idx = cand;
                if (Vector2.Distance(pts[from], pts[idx]) >= minDist) return idx;
            }
            return idx;
        }

        static float MengerCurvature(Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
            float denom = ab * bc * ca;
            if (denom < 1e-6f) return 0f;
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            return 2f * Mathf.Abs(cross) / denom;
        }

        // --- Training -----------------------------------------------------------------------------------

        // Drive the lap over and over, keeping every change that made it quicker. Deterministic: the same
        // course, seed and settings always produce the same line, so a retrain is reviewable and a test can
        // assert on it.
        public static Report Train(Course course, float[] seedLateral, CarLimits limits, Settings settings)
        {
            var report = new Report();
            int n = course != null ? course.Count : 0;
            if (course == null || !course.IsValid(out _) || seedLateral == null || seedLateral.Length != n)
            {
                report.lateral = seedLateral;
                report.seedLapTime = report.trainedLapTime = float.MaxValue;
                return report;
            }

            var ws = new Workspace();
            ws.Ensure(n);

            var current = (float[])seedLateral.Clone();
            ClampToCorridor(course, current, settings.edgeMargin);

            float best = LapTime(course, current, limits, ws);
            report.seedLapTime = best;
            report.lapsSimulated = 1;

            float[] arc = CentreArcLengths(course, out float totalArc);
            int rounds = Mathf.Max(1, settings.rounds);
            int maxPasses = Mathf.Max(1, settings.maxPassesPerRound);

            for (int round = 0; round < rounds; round++)
            {
                float t01 = rounds > 1 ? round / (float)(rounds - 1) : 1f;
                float amp = settings.AmplitudeAt(t01);
                float halfWidth = settings.HalfWidthAt(t01);
                if (amp <= 0.0001f) continue;

                float spacing = Mathf.Max(1f, halfWidth * Mathf.Max(0.05f, settings.controlSpacingFactor));

                for (int pass = 0; pass < maxPasses; pass++)
                {
                    // Half a spacing of stagger on alternate passes, so a corner never gets worked from the
                    // same handful of points twice running and the sweep can slide an apex between them.
                    float offset = (pass % 2 == 0) ? 0f : spacing * 0.5f;
                    int[] controls = ControlIndices(arc, totalArc, spacing, offset);
                    if (controls.Length == 0) break;

                    int keptThisPass = 0;
                    for (int c = 0; c < controls.Length; c++)
                    {
                        for (int s = 0; s < 2; s++)
                        {
                            float signed = s == 0 ? amp : -amp;
                            Array.Copy(current, ws.candidate, n);
                            if (!ApplyBump(course, ws.candidate, arc, totalArc, controls[c], halfWidth, signed, settings.edgeMargin))
                                continue;

                            float t = LapTime(course, ws.candidate, limits, ws);
                            report.lapsSimulated++;
                            if (t < best - settings.minGainSeconds)
                            {
                                best = t;
                                keptThisPass++;
                                report.improvements++;
                                var swap = current; current = ws.candidate; ws.candidate = swap;
                                break; // this stretch moved the right way; on to the next one
                            }
                        }
                    }

                    // A pass that found nothing at this scale will not find anything on a repeat either.
                    if (keptThisPass == 0 && pass > 0) break;
                }
            }

            report.lateral = current;
            report.trainedLapTime = best;
            report.drivenLength = Analyse(course, current, limits).drivenLength;
            return report;
        }

        // A raised-cosine nudge centred on one sample. Smooth to its first derivative, so however many of
        // these stack up over a training run the line never develops a kink — the car still has to be able to
        // drive what the optimiser finds.
        static bool ApplyBump(Course course, float[] lat, float[] arc, float totalArc, int centreIdx,
                              float halfWidth, float amplitude, float margin)
        {
            int n = lat.Length;
            bool moved = false;
            for (int dir = -1; dir <= 1; dir += 2)
            {
                int maxSteps = course.loop ? n / 2 : n;
                for (int step = (dir < 0 ? 1 : 0); step <= maxSteps; step++)
                {
                    int i = centreIdx + dir * step;
                    if (course.loop) i = ((i % n) + n) % n;
                    else if (i < 0 || i >= n) break;

                    float gap = ArcGap(arc, totalArc, centreIdx, i, course.loop);
                    if (gap > halfWidth) break;

                    float w = 0.5f * (1f + Mathf.Cos(Mathf.PI * Mathf.Clamp01(gap / halfWidth)));
                    float clamped = ClampSample(course, i, lat[i] + amplitude * w, margin);
                    if (Mathf.Abs(clamped - lat[i]) > 1e-5f) moved = true;
                    lat[i] = clamped;
                }
            }
            return moved;
        }

        static float ArcGap(float[] arc, float totalArc, int a, int b, bool loop)
        {
            float d = Mathf.Abs(arc[a] - arc[b]);
            if (loop && totalArc > 0f) d = Mathf.Min(d, totalArc - d);
            return d;
        }

        static float ClampSample(Course course, int i, float value, float margin)
        {
            float lo = Mathf.Min(course.minLateral[i], course.maxLateral[i]);
            float hi = Mathf.Max(course.minLateral[i], course.maxLateral[i]);
            float m = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, (hi - lo) * 0.5f - 0.01f));
            return Mathf.Clamp(value, lo + m, hi - m);
        }

        public static void ClampToCorridor(Course course, float[] lat, float margin)
        {
            for (int i = 0; i < lat.Length; i++) lat[i] = ClampSample(course, i, lat[i], margin);
        }

        // Cumulative centreline distance. The bump width is measured along the road rather than in samples so
        // it means the same thing wherever the sampler happened to bunch up.
        static float[] CentreArcLengths(Course course, out float totalArc)
        {
            int n = course.Count;
            var arc = new float[n];
            float cum = 0f;
            for (int i = 1; i < n; i++)
            {
                cum += Vector2.Distance(course.centre[i - 1], course.centre[i]);
                arc[i] = cum;
            }
            totalArc = course.loop
                ? cum + Vector2.Distance(course.centre[n - 1], course.centre[0])
                : cum;
            return arc;
        }

        static int[] ControlIndices(float[] arc, float totalArc, float spacing, float offset)
        {
            int n = arc.Length;
            var list = new System.Collections.Generic.List<int>(Mathf.Max(4, (int)(totalArc / spacing) + 1));
            float nextAt = offset;
            for (int i = 0; i < n; i++)
            {
                if (arc[i] >= nextAt - 1e-4f)
                {
                    list.Add(i);
                    nextAt = arc[i] + spacing;
                }
            }
            return list.ToArray();
        }
    }
}
