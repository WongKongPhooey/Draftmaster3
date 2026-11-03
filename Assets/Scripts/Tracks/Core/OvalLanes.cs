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
        public enum Lane { None = 0, Inside = 1, Outside = 2 }

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
