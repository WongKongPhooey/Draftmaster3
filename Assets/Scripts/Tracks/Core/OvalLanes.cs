using UnityEngine;

namespace Draftmaster.Tracks
{
    // The two fixed lanes an oval is raced in (Docs/Oval-Lanes.md). The inside lane is the bottom groove: a car
    // width and a little clearance up from the inside edge of the racing surface, so the car's inside wheels stay
    // on the banking, clear of the apron and pit road. The outside lane sits one car width plus clearance above
    // it, and never closer to the outside wall than the same margin. Both are plain lateral offsets off the track
    // centreline, worked out from the road's own width at every sample - no per-track authoring, so every oval
    // gets them. Road courses never do.
    //
    // Pure: TrackBuilder feeds it the sampled centreline, SplineDriver drives the result, the gizmo and the
    // in-game overlay draw it, and the EditMode tests check it without a scene.
    public static class OvalLanes
    {
        // Ideal: single file on the car's own racing line. Inside / Outside: two cars side by side, the one nearer
        // the infield on the inside line and the other on the outside line (BuildFromLines: the authored leftmost /
        // rightmost AI lines on an anticlockwise oval).
        public enum Lane { None = 0, Inside = 1, Outside = 2, Ideal = 3 }

        [System.Serializable]
        public struct Spec
        {
            // Metres from the inside edge of the racing surface to the inside lane's centre line.
            public float insideEdgeMargin;
            // Metres between the two lanes' centre lines.
            public float laneSpacing;
            // The least room (m) the outside lane's centre line keeps from the outside edge of the racing surface.
            public float outsideEdgeMargin;
            // Metres of road either side a lane is averaged over, so a step in the road's width becomes a gentle
            // drift rather than a kink in the line the car steers at.
            public float smoothingMetres;
            // BuildFromLines only (lanes from the authored AI lines): metres added between the two lanes, and metres
            // both are moved up the banking, all round the lap...
            public float extraSpacing;
            public float outwardShift;
            // ...and on top of that on the front stretch (the trioval at Texas, Charlotte, Atlanta...), where the
            // field takes the green two wide: FrontStretchWeight, 1 there and easing to 0 before turn 1 / after
            // turn 4.
            public float frontStretchExtraSpacing;
            public float frontStretchOutwardShift;
            // ...and the outside lane may come this much closer to the outside edge there than outsideEdgeMargin, so
            // the shift up the banking is not used up by the edge (Texas: the trioval pair was already held off it).
            public float frontStretchOutsideEdgeRelief;
        }

        // A Cup car is about 2.0 m wide. Inside lane: half a car (1.0) + 0.8 m to the edge, so the inside tyres
        // run 0.8 m up from the apron. Spacing: a car width + 1.2 m, so two cars side by side have 1.2 m between
        // their doors. Outside: the same 1.8 m to the edge as the inside lane - the outside wall stands 1 m past
        // the edge on a generated oval, so the car keeps 1.8 m from it.
        public static readonly Spec Default = new Spec
        {
            insideEdgeMargin = 1.8f,
            laneSpacing = 3.2f,
            outsideEdgeMargin = 1.8f,
            smoothingMetres = 40f,
            // The drawn lanes ran a little tight and low: two-wide at the green the field stacked up. Moved apart
            // and up the banking slightly all round, more through the trioval (2026-11-06; opened a little more the
            // same night, and the outside lane let up to 1.4 m off the edge on the trioval - SplineDriver.RoadEdgeMargin,
            // the AI's own bound - where at 1.8 m the pair had been pinned and only got +0.78 m of its +1.2 m shift).
            extraSpacing = 0.6f,
            outwardShift = 0.5f,
            frontStretchExtraSpacing = 0.8f,
            frontStretchOutwardShift = 1.1f,
            frontStretchOutsideEdgeRelief = 0.4f,
        };

        // Whether the track with this id is raced in lanes: any kind but a road course, from the published
        // dimensions table. An unknown id (a test track, a hand-built scene) is never an oval.
        public static bool AppliesTo(string trackId) =>
            TrackDimensions.TryGet(trackId, out var row) && row.kind != TrackKind.RoadCourse;

        // Which side of travel the infield is on: -1 left (an anticlockwise lap), +1 right. Decided by the lap's
        // net turning, from the centreline tangents in order.
        public static float InsideSide(Vector2[] tangents)
        {
            float turn = 0f;
            if (tangents != null)
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector2 a = tangents[i], b = tangents[(i + 1) % tangents.Length];
                    turn += Mathf.Atan2(a.x * b.y - a.y * b.x, Vector2.Dot(a, b));
                }
            return turn >= 0f ? -1f : 1f;   // turning left (anticlockwise): the infield is on the left
        }

        // Height (m) of a lane's centre above the inside edge, on a road `width` wide. The outside lane gives way
        // first where the road is too narrow for both; on a road too narrow even for one lane both sit on the
        // centreline.
        public static float HeightAboveInsideEdge(Lane lane, float width, Spec spec)
        {
            float lo = spec.insideEdgeMargin, hi = width - spec.outsideEdgeMargin;
            if (hi < lo) return width * 0.5f;
            float h = lane == Lane.Outside ? spec.insideEdgeMargin + spec.laneSpacing : spec.insideEdgeMargin;
            return Mathf.Clamp(h, lo, hi);
        }

        // Lateral (m, + right of travel) of the lane's centre line on a road `width` wide.
        public static float Lateral(Lane lane, float width, float insideSide, Spec spec) =>
            insideSide * (width * 0.5f - HeightAboveInsideEdge(lane, width, spec));

        // Both lanes along the lap. `distance` and `width` are the centreline samples (distances increasing;
        // `loopLength` > 0 for a closed lap, which wraps the smoothing across the start/finish seam).
        public static void Build(float[] distance, float[] width, float insideSide, float loopLength, Spec spec,
                                 out float[] inside, out float[] outside)
        {
            int n = distance != null ? distance.Length : 0;
            inside = new float[n];
            outside = new float[n];
            if (n == 0) return;
            float[] w = Smooth(distance, width, loopLength, spec.smoothingMetres);
            for (int i = 0; i < n; i++)
            {
                inside[i] = Lateral(Lane.Inside, w[i], insideSide, spec);
                outside[i] = Lateral(Lane.Outside, w[i], insideSide, spec);
            }
        }

        // Both lanes from two authored lines (the AI's leftmost and rightmost racing lines, laterals per sample, in
        // either order): per sample, whichever is further toward the infield is the inside lane and the other the
        // outside. Each is held insideEdgeMargin / outsideEdgeMargin off the edges of the (smoothed) road and, where
        // the two come closer than laneSpacing, pushed apart about their middle so two cars side by side still have
        // room between their doors. Then smoothed over half the usual window, so a line clamped at a step in the road
        // drifts rather than kinks. Road too narrow for both: both on the centreline, as in Build.
        // `lineSmoothingMetres` < 0 = half the spec's window; 0 = none (lines that are already smooth, such as trained
        // ones, keep their points exactly).
        // `frontStretch` (optional, one per sample, 0-1: FrontStretchWeight) scales the spec's front-stretch spread,
        // shift and outside-edge relief; extraSpacing / outwardShift apply everywhere. The drawn gap (never less than laneSpacing) widens by the
        // spread about its middle, and the pair moves up the banking by the shift, before both are held off the edges.
        public static void BuildFromLines(float[] distance, float[] width, float[] lineA, float[] lineB, float insideSide,
                                          float loopLength, Spec spec, out float[] inside, out float[] outside,
                                          float lineSmoothingMetres = -1f, float[] frontStretch = null)
        {
            int n = distance != null ? distance.Length : 0;
            inside = new float[n];
            outside = new float[n];
            if (n == 0) return;
            // Unsmoothed lines are clamped to the road as it is, not to the averaged width (which would pull a line in
            // where the road widens).
            float[] w = lineSmoothingMetres == 0f ? width : Smooth(distance, width, loopLength, spec.smoothingMetres);
            for (int i = 0; i < n; i++)
            {
                float a = lineA[i], b = lineB[i];
                bool aIn = insideSide * a >= insideSide * b;
                float inLat = aIn ? a : b, outLat = aIn ? b : a;
                float half = w[i] * 0.5f;
                // Height above the inside edge: 0 at the inside edge, width at the outside edge.
                float hIn = half - insideSide * inLat, hOut = half - insideSide * outLat;
                float front = frontStretch != null && i < frontStretch.Length ? Mathf.Clamp01(frontStretch[i]) : 0f;
                float lo = spec.insideEdgeMargin;
                float hi = w[i] - Mathf.Max(0f, spec.outsideEdgeMargin - front * spec.frontStretchOutsideEdgeRelief);
                if (hi < lo)
                {
                    hIn = hOut = half;
                }
                else
                {
                    hIn = Mathf.Clamp(hIn, lo, hi);
                    hOut = Mathf.Clamp(hOut, lo, hi);
                    float spread = Mathf.Max(0f, spec.extraSpacing + front * spec.frontStretchExtraSpacing);
                    float shift = spec.outwardShift + front * spec.frontStretchOutwardShift;
                    float gap = Mathf.Max(hOut - hIn, spec.laneSpacing) + spread;
                    if (gap > hOut - hIn || shift != 0f)
                    {
                        float mid = (hIn + hOut) * 0.5f + shift;
                        hIn = mid - gap * 0.5f;
                        hOut = mid + gap * 0.5f;
                        if (hIn < lo) { hOut += lo - hIn; hIn = lo; }
                        if (hOut > hi) { hIn -= hOut - hi; hOut = hi; }
                        hIn = Mathf.Max(lo, hIn);   // too narrow for the full spacing: the outside gives way
                    }
                }
                inside[i] = insideSide * (half - hIn);
                outside[i] = insideSide * (half - hOut);
            }
            float window = lineSmoothingMetres < 0f ? spec.smoothingMetres * 0.5f : lineSmoothingMetres;
            if (window > 0f)
            {
                inside = Smooth(distance, inside, loopLength, window);
                outside = Smooth(distance, outside, loopLength, window);
            }
        }

        // How much each centreline sample is "the front stretch": the stretch of road through the start/finish line
        // (sample 0) between the last turn and the first, found from the lap's own curvature - every sample whose
        // curvature, averaged over +/-`smoothingMetres`, is under `turnShare` of the lap's sharpest. A trioval's
        // dog-legs are short (Texas: kinks 100-150 m long, as sharp as the turns at their peak) and the turns long
        // (500 m+), so averaged over 240 m the kinks fall well under the turns and the whole trioval counts. 1 inside it, easing (smoothstep)
        // to 0 over the `easeMetres` at either end, so nothing changes by turn 1 or after turn 4. All 0 if the line
        // is on a turn, or the track hardly turns at all.
        public static float[] FrontStretchWeight(float[] distance, Vector2[] tangent, float loopLength,
                                                 float smoothingMetres = 120f, float turnShare = 0.6f, float easeMetres = 80f)
        {
            int n = distance != null ? distance.Length : 0;
            var weight = new float[n];
            if (n < 3 || tangent == null || tangent.Length != n) return weight;
            bool loop = loopLength > 0f;
            var k = new float[n];
            for (int i = 0; i < n; i++)
            {
                int j = i + 1;
                if (j >= n) { if (!loop) { k[i] = k[i - 1]; continue; } j = 0; }
                float ds = distance[j] - distance[i];
                if (ds <= 0f) ds += loopLength;
                Vector2 a = tangent[i], b = tangent[j];
                k[i] = Mathf.Abs(Mathf.Atan2(a.x * b.y - a.y * b.x, Vector2.Dot(a, b))) / Mathf.Max(0.01f, ds);
            }
            k = Smooth(distance, k, loopLength, smoothingMetres);
            float max = 0f;
            for (int i = 0; i < n; i++) max = Mathf.Max(max, k[i]);
            float limit = turnShare * max;
            if (max < 1e-4f || k[0] >= limit) return weight;
            // Out from the line both ways while the road is not a turn.
            int fwd = 0;
            while (fwd + 1 < n && k[fwd + 1] < limit) fwd++;
            int back = 0;   // samples behind sample 0, wrapping round the lap
            if (loop) while (back + 1 < n - fwd - 1 && k[n - 1 - back] < limit) back++;
            float ahead = distance[fwd] - distance[0];
            float behind = back > 0 ? loopLength - (distance[n - back] - distance[0]) : 0f;
            for (int i = 0; i <= fwd; i++)
            {
                float s = distance[i] - distance[0];
                weight[i] = Ease(Mathf.Min(s + behind, ahead - s));
            }
            for (int b = 1; b <= back; b++)
            {
                int i = n - b;
                float s = distance[i] - distance[0] - loopLength;   // metres before the line (negative)
                weight[i] = Ease(Mathf.Min(s + behind, ahead - s));
            }
            return weight;

            float Ease(float metresIn) => Mathf.SmoothStep(0f, 1f, easeMetres > 0f ? metresIn / easeMetres : 1f);
        }

        // `values` averaged over the samples within `halfWindow` metres either side of each sample. Wraps on a
        // closed lap.
        public static float[] Smooth(float[] distance, float[] values, float loopLength, float halfWindow)
        {
            int n = values.Length;
            var result = new float[n];
            if (halfWindow <= 0f || n < 3) { System.Array.Copy(values, result, n); return result; }
            bool loop = loopLength > 0f;
            for (int i = 0; i < n; i++)
            {
                double sum = values[i], weight = 1.0;
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    for (int k = 1; k < n; k++)
                    {
                        int j = i + dir * k;
                        if (loop) j = ((j % n) + n) % n;
                        else if (j < 0 || j >= n) break;
                        float gap = Mathf.Abs(distance[j] - distance[i]);
                        if (loop) gap = Mathf.Min(gap, loopLength - gap);
                        if (gap > halfWindow) break;
                        sum += values[j];
                        weight += 1.0;
                    }
                }
                result[i] = (float)(sum / weight);
            }
            return result;
        }
    }
}
