using UnityEngine;

namespace Draftmaster.Tracks
{
    // Pit road on an oval is reached along the apron. A car coming in drops below the racing surface first and
    // only then slows; a car leaving runs the apron through the next pair of turns and comes back up onto the
    // racing surface on the back straight. Doing either on the racing line - slowing from 190 mph in the groove,
    // or popping out of pit road into the middle of the track short of turn 1 - is how pit stops caused crashes.
    //
    // Short tracks (under a mile) are the exception: there is no apron worth the name and the field is going
    // slowly enough for the car to slow on the track, as it does in real life. The pure rules live here so they
    // can be tested without a scene; SplineDriver applies them.
    public static class OvalPitApron
    {
        public const float MetresPerMile = 1609.344f;

        // Whether a track of this kind and lap length uses the apron for pit in and out.
        public static bool Applies(TrackKind kind, float lapMiles)
        {
            bool oval = kind == TrackKind.ShortTrack || kind == TrackKind.Speedway || kind == TrackKind.Superspeedway;
            return oval && lapMiles >= 1f;
        }

        // By track id, from the published dimensions. An unknown id (a test track, a hand-built scene) is never
        // treated as an oval.
        public static bool AppliesTo(string trackId) =>
            TrackDimensions.TryGet(trackId, out var row) && Applies(row.kind, row.lapMiles);

        // Lateral (m, + right of travel) of the lane a car runs on the apron, given which side the infield is on
        // (+1 right, -1 left) and the road width here. With a paved apron below the racing surface the car sits
        // `apronCarOffset` beyond the inside edge (on the apron); without one it hugs the inside edge of the road
        // (`edgeInset` in from it), the lowest it can go without putting wheels on the grass.
        public static float LaneLateral(float insideSide, float roadWidth, bool hasApron, float apronCarOffset, float edgeInset)
        {
            float half = roadWidth * 0.5f;
            float fromCentre = hasApron ? half + apronCarOffset : Mathf.Max(0f, half - edgeInset);
            return Mathf.Sign(insideSide == 0f ? -1f : insideSide) * fromCentre;
        }

        // Where the back straight begins after `from` (m along the lap): walk forward until the road has turned
        // through at least `minTurnDeg` (the turns after the pit exit), then stop at the first point from which it
        // runs straight - heading within `straightTolDeg` - for the next `straightLookM`. `distance` and
        // `headingDeg` are the lap's centreline samples (distances increasing, headings in degrees). Returns -1
        // when no such point exists within `maxScanM`.
        public static float BackStraightStart(float[] distance, float[] headingDeg, float lap, float from,
                                              float minTurnDeg = 60f, float straightLookM = 150f,
                                              float straightTolDeg = 4f, float maxScanM = -1f, float stepM = 10f)
        {
            if (distance == null || headingDeg == null || distance.Length < 2 || distance.Length != headingDeg.Length || lap <= 0f)
                return -1f;
            if (maxScanM <= 0f) maxScanM = lap * 0.75f;
            stepM = Mathf.Max(1f, stepM);

            float turned = 0f;
            float prev = HeadingAt(distance, headingDeg, lap, from);
            for (float s = stepM; s <= maxScanM; s += stepM)
            {
                float d = from + s;
                float h = HeadingAt(distance, headingDeg, lap, d);
                turned += Mathf.Abs(Mathf.DeltaAngle(prev, h));
                prev = h;
                if (turned < minTurnDeg) continue;

                bool straight = true;
                for (float t = stepM; t <= straightLookM; t += stepM)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(h, HeadingAt(distance, headingDeg, lap, d + t))) > straightTolDeg)
                    {
                        straight = false;
                        break;
                    }
                }
                if (straight) return Wrap(d, lap);
            }
            return -1f;
        }

        // How far (m) round the lap `to` is from `from`, in [0, lap).
        public static float Ahead(float from, float to, float lap)
        {
            if (lap <= 0f) return to - from;
            return Wrap(to - from, lap);
        }

        public static float Wrap(float d, float lap) => lap > 0f ? ((d % lap) + lap) % lap : d;

        // Heading (deg) at a distance, interpolated between samples the short way round.
        static float HeadingAt(float[] distance, float[] headingDeg, float lap, float d)
        {
            d = Wrap(d, lap);
            int n = distance.Length;
            int lo = 0, hi = n - 1;
            if (d <= distance[0]) return headingDeg[0];
            if (d >= distance[n - 1]) return headingDeg[n - 1];
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (distance[mid] <= d) lo = mid; else hi = mid;
            }
            float span = distance[hi] - distance[lo];
            float t = span > 0f ? (d - distance[lo]) / span : 0f;
            return headingDeg[lo] + Mathf.DeltaAngle(headingDeg[lo], headingDeg[hi]) * t;
        }
    }
}
