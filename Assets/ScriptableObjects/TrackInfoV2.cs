using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "TrackInfoV2", menuName = "Racetrack/Track Info V2", order = 2)]
public class TrackInfoV2 : ScriptableObject
{
    [Header("Identity")]
    public string trackName;
    public int trackId;
    public int trackLaps = 1;
    public int topSpeed;
    [Tooltip("Distance (m) from the start of segment[0] to the start/finish line. Cars spawn from this anchor. Lap timing rolls here.")]
    public float startFinishDistance = 0f;

    [Header("Geometry")]
    [Tooltip("World position of the start/finish line.")]
    public Vector2 startPosition;
    [Tooltip("Initial heading in degrees. 0 = +X (east), 90 = +Y (north).")]
    public float startHeading;
    [Tooltip("Default road width in metres if a segment's width is 0.")]
    public float defaultWidth = 12f;
    [Range(1, 64)]
    [Tooltip("Minimum centerline samples per segment. Acts as a floor.")]
    public int samplesPerSegment = 4;
    [Tooltip("Maximum spacing between centerline samples in metres. Longer segments emit proportionally more samples, so curves stay smooth regardless of length.")]
    public float maxArcStepMetres = 2f;
    [Tooltip("If true, the final segment connects back to startPosition. Segments should sum back to start; mismatch will show visually.")]
    public bool closedLoop = true;

    [Header("Segments (in order around the lap)")]
    public TrackSegment[] segments;

    [Header("Racing Line Shaping (ideal line)")]
    [Tooltip("A straight (or run of straights / shallow bends) shorter than this between two corners carries one arc from the first corner's apex to the next one's: the first corner's exit point and the next corner's entry point are left out of the IDEAL line. Longer gaps keep both, and the line holds the exit side down the straight.")]
    public float racingLineMergeGap = 100f;
    [Tooltip("A run of same-direction Turn segments turning less than this in total is a shallow bend, not a corner: like a straight, it puts no point of its own on the IDEAL line.")]
    public float racingLineCornerMinDegrees = 12f;
    [Tooltip("Pull the IDEAL line back inside the leftmost/rightmost lines wherever the minimum-curvature line would leave them (it bends to meet the side line rather than being clipped). Off: the ideal is the pure minimum-curvature line through its points; the AI still clamps to the road edge when it drives.")]
    public bool racingLineKeepIdealInsideSideLines = false;
    [Tooltip("Keep the IDEAL line off the apron: never inside the inside line (the leftmost line on a lap that turns left, the rightmost on one that turns right). A long corner's minimum-curvature line otherwise cuts across the infield between its apex and exit points. It stays free to run wide. Ignored when racingLineKeepIdealInsideSideLines (both sides) is on.")]
    public bool racingLineKeepIdealOffApron = true;
    [Tooltip("A straight (or run of straights / shallow bends) at least this long between two corners is driven STRAIGHT: from the first corner's exit point to the next one's entry point the ideal line is a straight line, and each corner's arc finishes lined up with it. Shorter gaps carry the minimum-curvature arc across (a tri-oval front stretch).")]
    public float racingLineFlatStraightMin = 300f;

    [Header("Edge Lines (painted boundary, e.g. white line)")]
    public bool drawEdgeLines;
    [Tooltip("Material for the painted edge line. Use a Material with the appropriate texture (e.g. solid white or dashed) and tiling.")]
    public Material edgeLineMaterial;
    [Tooltip("Width of the painted edge line in metres.")]
    public float edgeLineWidth = 0.15f;
    [Tooltip("How far the line's centre sits inboard of the track edge, in metres. Set to edgeLineWidth/2 so the line's outer edge meets the track edge exactly.")]
    public float edgeLineInset = 0.075f;
    [Tooltip("Sorting order for the line meshes. Higher draws above the track surface.")]
    public int edgeLineSortingOrder = 1;
    public bool drawLeftEdgeLine = true;
    public bool drawRightEdgeLine = true;

    [Header("Pit Lane")]
    public bool hasPitLane;
    [HideInInspector] public Vector2 pitStartPosition;
    [HideInInspector] public float pitStartHeading;
    [Tooltip("Additive angle (deg) added to the auto-derived pit start heading. Use to angle the pit entry away from the main spline tangent.")]
    public float pitStartHeadingOffset = 0f;
    [Tooltip("Default width for the pit lane, in metres. 0 = inherit defaultWidth.")]
    public float pitDefaultWidth;
    [Tooltip("Pit-lane segments. Same format as the main spline.")]
    public TrackSegment[] pitSegments;
    [Tooltip("Main-spline segment whose EXIT node is the pit entry point (pit diverges at the end of this segment).")]
    public int pitEntrySegmentIndex = 0;
    [Tooltip("Fine-tune shift (m) along main spline applied to the pit entry node. Positive = further round the lap.")]
    public float pitEntryOffset = 0f;
    [Tooltip("Main-spline segment whose EXIT node is the pit rejoin point (pit merges back at the end of this segment).")]
    public int pitExitSegmentIndex = 1;
    [Tooltip("Fine-tune shift (m) along main spline applied to the pit exit node. Positive = further round the lap.")]
    public float pitExitOffset = 0f;
    [HideInInspector] public float pitEntryDistance;
    [HideInInspector] public float pitExitDistance;
    [Tooltip("Pit-lane speed limit in mph. Enforced on the AI (SplineDriver) and on the player (PitLimiter) between the pit entry and the pit exit line.")]
    public int pitSpeedLimit = 50;
    [Tooltip("Distance along the PIT LANE (m) of the pit exit line — where the speed limit ends and the driver may accelerate. 0 = the end of the pit lane. The painted line (a strip labelled 'PitExitLine') anchors here too.")]
    public float pitExitLineDistance = 0f;

    void OnValidate() => RebakePitDistances();

    // Recompute where the pit lane leaves and rejoins the main spline, from the segment indices and the
    // fine-tune offsets. Runs on every inspector edit; public so a generator (OvalTrackFactory) can bake
    // the same values when it builds an asset in code, where OnValidate doesn't fire.
    public void RebakePitDistances()
    {
        if (segments == null || segments.Length == 0) return;
        if (pitEntrySegmentIndex >= 0 && pitEntrySegmentIndex < segments.Length)
            pitEntryDistance = SegmentStartDistance(pitEntrySegmentIndex) + segments[pitEntrySegmentIndex].length + pitEntryOffset;
        if (pitExitSegmentIndex >= 0 && pitExitSegmentIndex < segments.Length)
            pitExitDistance = SegmentStartDistance(pitExitSegmentIndex) + segments[pitExitSegmentIndex].length + pitExitOffset;
    }

    float SegmentStartDistance(int idx)
    {
        float d = 0f;
        for (int i = 0; i < idx; i++) d += segments[i].length;
        return d;
    }

    // Total authored length of the pit lane (m), 0 when the track has no pit lane.
    public float PitLaneLength
    {
        get
        {
            float d = 0f;
            if (pitSegments != null) for (int i = 0; i < pitSegments.Length; i++) d += pitSegments[i].length;
            return d;
        }
    }

    // Where the speed limit ends, as a distance along the pit lane. An unauthored (0) value means the very
    // end of the lane, which is the safe default — the limiter then holds until the car is back on track.
    public float PitExitLineDistance =>
        pitExitLineDistance > 0.01f ? Mathf.Min(pitExitLineDistance, PitLaneLength) : PitLaneLength;

    // pitEntryDistance folded into [0, lap). The bake anchors to the END of the entry segment plus the
    // offset, so an entry authored on the final segment lands past the lap total — raw gap maths against
    // DistanceOnTrack (which wraps) then never come inside the entry window and the car circles forever.
    public float PitEntryDistanceOnLap
    {
        get
        {
            float lap = 0f;
            if (segments != null) for (int i = 0; i < segments.Length; i++) lap += segments[i].length;
            return lap > 0f ? ((pitEntryDistance % lap) + lap) % lap : pitEntryDistance;
        }
    }

    // Walk authored segments only (no seam closure) to find world position + heading at a given distance along main spline.
    public void SampleAuthoredSpline(float distance, out Vector2 pos, out float headingDeg)
    {
        pos = startPosition;
        headingDeg = startHeading;
        if (segments == null || segments.Length == 0) return;
        float remaining = distance;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (remaining <= seg.length || i == segments.Length - 1)
            {
                AdvanceAlongSegment(seg, ref pos, ref headingDeg, Mathf.Min(remaining, seg.length));
                return;
            }
            AdvanceAlongSegment(seg, ref pos, ref headingDeg, seg.length);
            remaining -= seg.length;
        }
    }

    static void AdvanceAlongSegment(TrackSegment seg, ref Vector2 pos, ref float headingDeg, float dist)
    {
        if (seg.type == SegmentType.Straight || Mathf.Approximately(seg.angle, 0f))
        {
            float hRad = headingDeg * Mathf.Deg2Rad;
            pos += new Vector2(Mathf.Cos(hRad), Mathf.Sin(hRad)) * dist;
            return;
        }
        float angleRad = seg.angle * Mathf.Deg2Rad;
        float fraction = dist / Mathf.Max(seg.length, 1e-4f);
        float radius = seg.length / Mathf.Abs(angleRad);
        Vector2 forward = new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        Vector2 toCentre = seg.angle >= 0f ? new Vector2(-forward.y, forward.x) : new Vector2(forward.y, -forward.x);
        Vector2 centre = pos + toCentre * radius;
        Vector2 startRadial = pos - centre;
        float startAngleRad = Mathf.Atan2(startRadial.y, startRadial.x);
        float endAngleRad = startAngleRad + angleRad * fraction;
        pos = centre + new Vector2(Mathf.Cos(endAngleRad), Mathf.Sin(endAngleRad)) * radius;
        headingDeg += seg.angle * fraction;
    }

    public enum SegmentType { Straight, Turn }

    [System.Serializable]
    public struct TrackSegment
    {
        public string label;
        public SegmentType type;
        [Tooltip("Length in metres along the centerline.")]
        public float length;
        [Tooltip("Turn angle in degrees. Positive = left (CCW), negative = right (CW). Ignored for straights.")]
        public float angle;
        [Tooltip("Banking angle in degrees. Informational for physics/AI.")]
        public float banking;
        [Tooltip("Lead-in distance for racing-line calc (metres). The entry anchor sits this far before the segment starts.")]
        public float leadIn;
        [Tooltip("Lead-out distance for racing-line calc (metres). The exit anchor sits this far after the segment ends.")]
        public float leadOut;
        [Tooltip("Max segment speed in mph. Informational.")]
        public int maxSpeed;
        [Tooltip("Width override at this segment (metres). 0 = use defaultWidth.")]
        public float width;
        [Range(-0.5f, 0.5f)]
        [Tooltip("Straights only: shifts the single racing-line anchor along the segment. 0 = exact middle, -0.5 = at segment start, +0.5 = at segment end. Use this to bias the straight's anchor away from a previous turn that forces an unusual exit.")]
        public float straightMidpointOffset;
        [Tooltip("Racing-line lateral offsets. For Turns: entry/apex/exit. For Straights: only the apex values are used (positioned by straightMidpoint).")]
        public SegmentRacingLine racingLine;
    }

    [System.Serializable]
    public struct SegmentRacingLine
    {
        [Header("Ideal Line")]
        [Tooltip("Lateral offset at the entry point (segment start minus leadIn). Positive = right of travel direction.")]
        public float idealEntry;
        [Tooltip("Lateral offset at the apex (midpoint of the segment).")]
        public float idealApex;
        [Tooltip("Lateral offset at the exit point (segment end plus leadOut).")]
        public float idealExit;

        [Header("Leftmost AI Line (visual left of the track)")]
        [FormerlySerializedAs("maxEntry")] public float leftEntry;
        [FormerlySerializedAs("maxApex")]  public float leftApex;
        [FormerlySerializedAs("maxExit")]  public float leftExit;

        [Header("Rightmost AI Line (visual right of the track)")]
        [FormerlySerializedAs("minEntry")] public float rightEntry;
        [FormerlySerializedAs("minApex")]  public float rightApex;
        [FormerlySerializedAs("minExit")]  public float rightExit;

        [Header("Anchor Skipping (smoother chicanes)")]
        [Tooltip("Skip the entry anchor for this turn. Useful in chicanes where the previous corner's exit already shapes the line.")]
        public bool skipEntry;
        [Tooltip("Skip the exit anchor for this turn. Useful in chicanes where the next corner's entry takes over.")]
        public bool skipExit;
        [Tooltip("Skip the apex anchor (straights only). Useful for very short straights linking two turns where the neighbours already shape the line.")]
        public bool skipApex;
        [Tooltip("Straights and shallow bends only: make the IDEAL line pass through this segment's own point(s) (a straight's idealApex) instead of carrying the corners' arcs across it. Off by default - a straight normally puts no point on the ideal line; its apex values still shape the leftmost/rightmost lines.")]
        public bool keepOnStraight;
    }

    public struct RacingLineAnchor
    {
        public float distance;
        public float ideal;
        public float left;
        public float right;
        // Which lines the point shapes. Default (both false) = all three. A straight's apex, or a corner exit/entry
        // merged across a short straight, shapes only the leftmost/rightmost lines (noIdeal); an ideal point moved
        // off its authored spot, or a hold point, shapes only the ideal (noSides).
        public bool noIdeal;
        public bool noSides;
        // A point the generator added (not drawn by anyone) to keep the ideal between the side lines: shapes the
        // ideal line but is not pinned by training.
        public bool shapeOnly;
        // The ideal line's minimum-curvature spline at this point (SolveIdealCurve): the offset less the road's own
        // bend, and the spline's second derivative.
        internal float idealResidual;
        internal float idealCurve;
        // A corner exit point with a long straight after it (flatAfter) / an entry point with one before it
        // (flatBefore): between such a pair the ideal is a straight line (racingLineFlatStraightMin).
        internal bool flatAfter;
        internal bool flatBefore;

        public bool InIdeal => !noIdeal;
        public bool InSides => !noSides;
    }

    enum LineChannel { Ideal, Left, Right }

    // Banking (degrees) at a distance round the main lap, eased across each segment join over blendMetres so a
    // car running into a banked turn feels it build rather than step - BankedGrip turns it into grip.
    public float BankingAt(float distance, float blendMetres = 40f)
    {
        if (segments == null || segments.Length == 0) return 0f;
        float lap = TotalLength();
        if (lap <= 0f) return 0f;
        distance = ((distance % lap) + lap) % lap;
        int n = segments.Length;
        float start = 0f;
        for (int i = 0; i < n; i++)
        {
            float len = segments[i].length;
            if (distance < start + len || i == n - 1)
            {
                float into = distance - start, left = start + len - distance;
                float here = segments[i].banking;
                float half = Mathf.Max(0.01f, blendMetres * 0.5f);
                if (into < half && into <= left)
                    return Mathf.Lerp(segments[(i - 1 + n) % n].banking, here, 0.5f + 0.5f * into / half);
                if (left < half)
                    return Mathf.Lerp(segments[(i + 1) % n].banking, here, 0.5f + 0.5f * left / half);
                return here;
            }
            start += len;
        }
        return 0f;
    }

    // True when the lap turns left overall (anticlockwise): the inside of the track, and the bottom of any banking,
    // is on the left of travel.
    public bool LapTurnsLeft()
    {
        if (segments == null) return true;
        float total = 0f;
        for (int i = 0; i < segments.Length; i++) total += segments[i].angle;
        return total >= 0f;
    }

    public float TotalLength()
    {
        if (segments == null) return 0f;
        float total = 0f;
        for (int i = 0; i < segments.Length; i++) total += segments[i].length;
        return total;
    }

    // The racing-line points round the lap, sorted by distance.
    //
    // The leftmost/rightmost lines are exactly the authored points (every entry/apex/exit and straight apex that is
    // not skipped), joined by Catmull-Rom. The IDEAL line is built from corners, not from every segment:
    //   - a straight, or a shallow bend (a same-direction run of turns under racingLineCornerMinDegrees), puts no
    //     point on it (unless keepOnStraight) - its apex used to drag the line back to the centre mid-straight;
    //   - between two corners less than racingLineMergeGap apart, the first corner's exit and the next one's entry
    //     drop out, so the line is one arc from apex to apex (a chicane, a dogleg, a tri-oval kink pair);
    //   - between corners further apart, the exit and entry are kept but held inside the gap (so they never cross);
    //   - across a gap of racingLineFlatStraightMin or more the line runs dead straight from that exit to that entry.
    // The ideal is then the minimum-curvature line through its points (SolveIdealCurve): the line that turns as
    // little as it can in the WORLD, not the one that holds a steady offset from the centre - an offset held down
    // a straight between two kinks is just the road's own straight-kink-straight. Finally it is kept between the
    // side lines (KeepIdealInsideSideLines) when racingLineKeepIdealInsideSideLines is on, or off the apron side
    // only (racingLineKeepIdealOffApron).
    public List<RacingLineAnchor> BuildRacingLineAnchors()
    {
        var list = new List<RacingLineAnchor>();
        if (segments == null || segments.Length == 0) return list;
        int n = segments.Length;
        var start = new float[n];
        float cum = 0f;
        for (int i = 0; i < n; i++) { start[i] = cum; cum += segments[i].length; }
        float trackLen = cum;

        bool[] corner = IdealLineCorners();
        bool Bounds(int k) => corner[k] || segments[k].racingLine.keepOnStraight;

        for (int i = 0; i < n; i++)
        {
            var seg = segments[i];
            var rl = seg.racingLine;
            float s0 = start[i], s1 = start[i] + seg.length;
            if (seg.type == SegmentType.Turn)
            {
                bool isCorner = corner[i];
                bool idealHere = isCorner || rl.keepOnStraight;

                if (!rl.skipEntry)
                {
                    var a = new RacingLineAnchor { distance = s0 - seg.leadIn, ideal = rl.idealEntry, left = rl.leftEntry, right = rl.rightEntry };
                    if (!isCorner) { a.noIdeal = !idealHere; list.Add(a); }
                    else
                    {
                        float gap = GapToNeighbour(i, -1, Bounds);
                        if (gap >= 0f && gap < racingLineMergeGap) { a.noIdeal = true; list.Add(a); }
                        else
                        {
                            int j = NeighbourIndex(i, -1, Bounds);
                            a.flatBefore = gap >= racingLineFlatStraightMin && j >= 0 && corner[j] && !segments[j].racingLine.skipExit;
                            AddSplit(list, a, gap >= 0f ? s0 - Mathf.Min(seg.leadIn, gap * 0.4f) : a.distance);
                        }
                    }
                }

                list.Add(new RacingLineAnchor { distance = s0 + seg.length * 0.5f, ideal = rl.idealApex, left = rl.leftApex, right = rl.rightApex, noIdeal = !idealHere });

                if (!rl.skipExit)
                {
                    var a = new RacingLineAnchor { distance = s1 + seg.leadOut, ideal = rl.idealExit, left = rl.leftExit, right = rl.rightExit };
                    if (!isCorner) { a.noIdeal = !idealHere; list.Add(a); }
                    else
                    {
                        float gap = GapToNeighbour(i, +1, Bounds);
                        if (gap >= 0f && gap < racingLineMergeGap) { a.noIdeal = true; list.Add(a); }
                        else
                        {
                            int j = NeighbourIndex(i, +1, Bounds);
                            a.flatAfter = gap >= racingLineFlatStraightMin && j >= 0 && corner[j] && !segments[j].racingLine.skipEntry;
                            AddSplit(list, a, gap >= 0f ? s1 + Mathf.Min(seg.leadOut, gap * 0.4f) : a.distance);
                        }
                    }
                }
            }
            else if (!rl.skipApex)
            {
                float midFrac = Mathf.Clamp(0.5f + seg.straightMidpointOffset, 0f, 1f);
                list.Add(new RacingLineAnchor { distance = s0 + seg.length * midFrac, ideal = rl.idealApex, left = rl.leftApex, right = rl.rightApex, noIdeal = !rl.keepOnStraight });
            }
        }

        if (closedLoop && trackLen > 0f)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                a.distance = ((a.distance % trackLen) + trackLen) % trackLen;
                list[i] = a;
            }
        }
        list.Sort((a, b) => a.distance.CompareTo(b.distance));
        SolveIdealCurve(list, trackLen);
        if (racingLineKeepIdealInsideSideLines) KeepIdealInsideSideLines(list, trackLen, CorridorSide.Both);
        else if (racingLineKeepIdealOffApron) KeepIdealInsideSideLines(list, trackLen, LapTurnsLeft() ? CorridorSide.Low : CorridorSide.High);
        return list;
    }

    // ---------------------------------------------------------------- minimum-curvature ideal line
    //
    // With n the lateral offset (positive right) and k(s) the road's curvature (positive left), a line's curvature
    // in the world is about k - n''. The line through the ideal points that bends least - least integral of
    // (k - n'')^2 - is n = P + S, where P'' = k (the road's own bend, integrated twice) and S is the cubic spline
    // (periodic on a closed lap) through what is left at each point, n_i - P(s_i). On a closed lap P is taken
    // against k less its lap mean, so P comes round periodic; that shifts the integrand by a constant and leaves the
    // minimiser unchanged.

    const float IdealPointMinSpacing = 0.5f;

    [System.NonSerialized] RoadBend _bend;

    sealed class RoadBend
    {
        public SegmentType[] types; public float[] lengths; public float[] angles; public bool loop;
        public float[] start, g, q, k;
        public float lap, meanSlope;

        public bool Matches(TrackSegment[] segs, bool closed)
        {
            if (types == null || segs.Length != types.Length || closed != loop) return false;
            for (int i = 0; i < segs.Length; i++)
                if (segs[i].type != types[i] || segs[i].length != lengths[i] || segs[i].angle != angles[i]) return false;
            return true;
        }
    }

    RoadBend Bend()
    {
        if (_bend != null && _bend.Matches(segments, closedLoop)) return _bend;
        int n = segments.Length;
        var b = new RoadBend
        {
            types = new SegmentType[n], lengths = new float[n], angles = new float[n], loop = closedLoop,
            start = new float[n], g = new float[n], q = new float[n], k = new float[n]
        };
        float lap = 0f, total = 0f;
        for (int i = 0; i < n; i++)
        {
            b.types[i] = segments[i].type; b.lengths[i] = segments[i].length; b.angles[i] = segments[i].angle;
            lap += Mathf.Max(0f, segments[i].length);
            if (segments[i].type == SegmentType.Turn) total += segments[i].angle * Mathf.Deg2Rad;
        }
        float mean = closedLoop && lap > 0f ? total / lap : 0f;
        float s = 0f, g = 0f, q = 0f;
        for (int i = 0; i < n; i++)
        {
            float len = Mathf.Max(0f, segments[i].length);
            float k = (segments[i].type == SegmentType.Turn && len > 1e-4f ? segments[i].angle * Mathf.Deg2Rad / len : 0f) - mean;
            b.start[i] = s; b.g[i] = g; b.q[i] = q; b.k[i] = k;
            q += g * len + 0.5f * k * len * len;
            g += k * len;
            s += len;
        }
        b.lap = lap;
        b.meanSlope = closedLoop && lap > 0f ? q / lap : 0f;   // mean of P' over the lap, taken out so P is periodic
        _bend = b;
        return b;
    }

    // P at a distance round the lap: the road's bend integrated twice (see above).
    float RoadBendAt(float s)
    {
        if (segments == null || segments.Length == 0) return 0f;
        var b = Bend();
        if (b.lap <= 0f) return 0f;
        s = Mathf.Clamp(s, 0f, b.lap);
        int i = segments.Length - 1;
        for (int j = 1; j < segments.Length; j++) if (b.start[j] > s) { i = j - 1; break; }
        float x = s - b.start[i];
        float q = b.q[i] + b.g[i] * x + 0.5f * b.k[i] * x * x;
        return q - b.meanSlope * s;
    }

    // P' at a distance round the lap.
    float RoadBendSlopeAt(float s)
    {
        if (segments == null || segments.Length == 0) return 0f;
        var b = Bend();
        if (b.lap <= 0f) return 0f;
        s = Mathf.Clamp(s, 0f, b.lap);
        int i = segments.Length - 1;
        for (int j = 1; j < segments.Length; j++) if (b.start[j] > s) { i = j - 1; break; }
        return b.g[i] + b.k[i] * (s - b.start[i]) - b.meanSlope;
    }

    // The cubic spline's second derivative at each ideal point, stored on the points. Points closer together than
    // IdealPointMinSpacing are one point (the first kept).
    void SolveIdealCurve(List<RacingLineAnchor> list, float trackLen)
    {
        var idx = new List<int>();
        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].InIdeal) continue;
            if (idx.Count > 0 && list[i].distance - list[idx[idx.Count - 1]].distance < IdealPointMinSpacing)
            {
                var dup = list[i]; dup.noIdeal = true; list[i] = dup;
                continue;
            }
            idx.Add(i);
        }
        bool loop = closedLoop && trackLen > 0f;
        if (loop && idx.Count > 1 && list[idx[0]].distance + trackLen - list[idx[idx.Count - 1]].distance < IdealPointMinSpacing)
        {
            int last = idx[idx.Count - 1];
            var dup = list[last]; dup.noIdeal = true; list[last] = dup;
            idx.RemoveAt(idx.Count - 1);
        }

        int m = idx.Count;
        var d = new float[m];
        var y = new float[m];
        for (int k = 0; k < m; k++)
        {
            d[k] = list[idx[k]].distance;
            y[k] = list[idx[k]].ideal - RoadBendAt(d[k]);
        }
        // Which spans between consecutive ideal points are long straights, driven straight.
        var flat = new bool[m];
        bool anyFlat = false;
        for (int k = 0; k < m; k++)
        {
            int kn = k + 1 < m ? k + 1 : (loop ? 0 : -1);
            flat[k] = m >= 2 && kn >= 0 && kn != k && list[idx[k]].flatAfter && list[idx[kn]].flatBefore;
            anyFlat |= flat[k];
        }

        var curve = new float[m];
        if (!anyFlat)
        {
            if (m >= 3) curve = loop ? PeriodicSplineCurvature(d, y, trackLen) : NaturalSplineCurvature(d, y);
        }
        else
        {
            // The lap breaks at each long straight into pieces running from one straight's end to the next one's
            // start. Each piece is a cubic spline whose ends leave/join the straight at its slope (an open track's
            // own ends are natural).
            float Gap(int from, int to) { float g = d[to] - d[from]; if (g <= 0f && loop) g += trackLen; return Mathf.Max(1e-3f, g); }
            float StraightSlope(int k) { int kn = (k + 1) % m; return (list[idx[kn]].ideal - list[idx[k]].ideal) / Gap(k, kn); }

            for (int start = 0; start < m; start++)
            {
                bool afterFlat = loop ? flat[(start - 1 + m) % m] : start > 0 && flat[start - 1];
                if (!(afterFlat || (!loop && start == 0))) continue;
                var piece = new List<int> { start };
                int e = start;
                while (!flat[e] && (loop || e < m - 1) && piece.Count <= m)
                {
                    e = (e + 1) % m;
                    piece.Add(e);
                }
                int count = piece.Count;
                if (count < 2) continue;
                var pd = new double[count];
                var py = new double[count];
                pd[0] = d[piece[0]];
                for (int q = 0; q < count; q++)
                {
                    if (q > 0) pd[q] = pd[q - 1] + Gap(piece[q - 1], piece[q]);
                    py[q] = y[piece[q]];
                }
                double? startSlope = afterFlat ? StraightSlope((start - 1 + m) % m) - RoadBendSlopeAt(d[start]) : (double?)null;
                double? endSlope = flat[e] ? StraightSlope(e) - RoadBendSlopeAt(d[e]) : (double?)null;
                var pm = ClampedSplineCurvature(pd, py, startSlope, endSlope);
                for (int q = 0; q < count; q++) curve[piece[q]] = (float)pm[q];
            }
        }
        for (int k = 0; k < m; k++)
        {
            var a = list[idx[k]];
            a.idealResidual = y[k];
            a.idealCurve = curve[k];
            list[idx[k]] = a;
        }
    }

    // Second derivatives of the periodic cubic spline through (d, y) with period `period`.
    static float[] PeriodicSplineCurvature(float[] d, float[] y, float period)
    {
        int m = d.Length;
        var h = new double[m];
        for (int k = 0; k < m; k++)
            h[k] = System.Math.Max(1e-3, (k + 1 < m ? d[k + 1] : d[0] + period) - d[k]);
        var a = new double[m]; var b = new double[m]; var c = new double[m]; var r = new double[m];
        for (int k = 0; k < m; k++)
        {
            int kp = (k - 1 + m) % m, kn = (k + 1) % m;
            a[k] = h[kp];
            b[k] = 2.0 * (h[kp] + h[k]);
            c[k] = h[k];
            r[k] = 6.0 * ((y[kn] - y[k]) / h[k] - (y[k] - y[kp]) / h[kp]);
        }
        // Cyclic tridiagonal by Sherman-Morrison: the corner terms are c[m-1] (row m-1, column 0) and a[0] (row 0,
        // column m-1).
        double alpha = c[m - 1], beta = a[0];
        double gamma = -b[0];
        var bb = (double[])b.Clone();
        bb[0] = b[0] - gamma;
        bb[m - 1] = b[m - 1] - alpha * beta / gamma;
        var x = Thomas(a, bb, c, r);
        var u = new double[m]; u[0] = gamma; u[m - 1] = alpha;
        var z = Thomas(a, bb, c, u);
        double fact = (x[0] + beta * x[m - 1] / gamma) / (1.0 + z[0] + beta * z[m - 1] / gamma);
        var result = new float[m];
        for (int k = 0; k < m; k++) result[k] = (float)(x[k] - fact * z[k]);
        return result;
    }

    // Second derivatives of the natural cubic spline through (d, y): zero at both ends.
    static float[] NaturalSplineCurvature(float[] d, float[] y)
    {
        int m = d.Length;
        var result = new float[m];
        int inner = m - 2;
        var a = new double[inner]; var b = new double[inner]; var c = new double[inner]; var r = new double[inner];
        for (int k = 1; k <= inner; k++)
        {
            double h0 = System.Math.Max(1e-3, d[k] - d[k - 1]), h1 = System.Math.Max(1e-3, d[k + 1] - d[k]);
            a[k - 1] = h0; b[k - 1] = 2.0 * (h0 + h1); c[k - 1] = h1;
            r[k - 1] = 6.0 * ((y[k + 1] - y[k]) / h1 - (y[k] - y[k - 1]) / h0);
        }
        var x = Thomas(a, b, c, r);
        for (int k = 1; k <= inner; k++) result[k] = (float)x[k - 1];
        return result;
    }

    // Second derivatives of the cubic spline through (d, y) whose end slopes are given (null = natural end, zero
    // second derivative).
    static double[] ClampedSplineCurvature(double[] d, double[] y, double? startSlope, double? endSlope)
    {
        int m = d.Length;
        var a = new double[m]; var b = new double[m]; var c = new double[m]; var r = new double[m];
        var h = new double[m - 1];
        for (int k = 0; k < m - 1; k++) h[k] = System.Math.Max(1e-3, d[k + 1] - d[k]);

        if (startSlope.HasValue) { b[0] = 2.0 * h[0]; c[0] = h[0]; r[0] = 6.0 * ((y[1] - y[0]) / h[0] - startSlope.Value); }
        else { b[0] = 1.0; c[0] = 0.0; r[0] = 0.0; }
        for (int k = 1; k < m - 1; k++)
        {
            a[k] = h[k - 1]; b[k] = 2.0 * (h[k - 1] + h[k]); c[k] = h[k];
            r[k] = 6.0 * ((y[k + 1] - y[k]) / h[k] - (y[k] - y[k - 1]) / h[k - 1]);
        }
        int l = m - 1;
        if (endSlope.HasValue) { a[l] = h[l - 1]; b[l] = 2.0 * h[l - 1]; r[l] = 6.0 * (endSlope.Value - (y[l] - y[l - 1]) / h[l - 1]); }
        else { a[l] = 0.0; b[l] = 1.0; r[l] = 0.0; }
        return Thomas(a, b, c, r);
    }

    // Tridiagonal solve: a = sub-diagonal (a[0] unused), b = diagonal, c = super-diagonal (c[n-1] unused).
    static double[] Thomas(double[] a, double[] b, double[] c, double[] r)
    {
        int n = b.Length;
        var cp = new double[n]; var rp = new double[n]; var x = new double[n];
        if (n == 0) return x;
        cp[0] = c[0] / b[0]; rp[0] = r[0] / b[0];
        for (int i = 1; i < n; i++)
        {
            double den = b[i] - a[i] * cp[i - 1];
            cp[i] = i < n - 1 ? c[i] / den : 0.0;
            rp[i] = (r[i] - a[i] * rp[i - 1]) / den;
        }
        x[n - 1] = rp[n - 1];
        for (int i = n - 2; i >= 0; i--) x[i] = rp[i] - cp[i] * x[i + 1];
        return x;
    }

    const float CorridorSampleMetres = 5f;
    const float CorridorTolerance = 0.1f;
    const int CorridorPasses = 12;

    // Which side(s) of the corridor the ideal is held to: Low = not below min(left, right), High = not above max.
    enum CorridorSide { Both, Low, High }

    // The ideal line stays between the leftmost and rightmost lines. With straights carrying no ideal point, a
    // corner's line can run on down a straight where the side lines were drawn to pull in (Texas: the tri-oval
    // kinks apex at -5 m, the start/finish straight between them has its inside lane at 0) and leave the corridor.
    // Wherever it does, the ideal gets a point on the line it crossed, at the worst spot of each stretch outside,
    // and the pass repeats until the line stays in. A smooth bend to meet the side line rather than a clamp.
    void KeepIdealInsideSideLines(List<RacingLineAnchor> list, float trackLen, CorridorSide side)
    {
        if (trackLen <= 0f) return;
        int ideal = 0, sides = 0;
        for (int i = 0; i < list.Count; i++) { if (list[i].InIdeal) ideal++; if (list[i].InSides) sides++; }
        if (ideal < 2 || sides == 0) return;

        int steps = Mathf.Max(8, Mathf.CeilToInt(trackLen / CorridorSampleMetres));
        float step = trackLen / steps;
        var added = new List<RacingLineAnchor>();

        // A stretch outside the corridor around a drawn ideal point that is itself outside the side lines is the
        // drawing's call (put there on purpose); only the line between drawn points is pulled in.
        bool HoldsDrawnPoint(float from, float to)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (!a.InIdeal || a.shapeOnly) continue;
                if (a.distance < from - step || a.distance > to + step) continue;
                float l = ChannelAt(list, a.distance, trackLen, LineChannel.Left);
                float r = ChannelAt(list, a.distance, trackLen, LineChannel.Right);
                if ((side != CorridorSide.High && a.ideal < Mathf.Min(l, r) - CorridorTolerance) ||
                    (side != CorridorSide.Low && a.ideal > Mathf.Max(l, r) + CorridorTolerance)) return true;
            }
            return false;
        }

        for (int pass = 0; pass < CorridorPasses; pass++)
        {
            added.Clear();
            bool inRun = false;
            float runFrom = 0f, worst = 0f, worstAt = 0f, worstBound = 0f;
            void CloseRun(float runTo)
            {
                if (!HoldsDrawnPoint(runFrom, runTo))
                    added.Add(new RacingLineAnchor { distance = worstAt, ideal = worstBound, left = worstBound, right = worstBound, noSides = true, shapeOnly = true });
                inRun = false;
                worst = 0f;
            }
            for (int s = 0; s < steps; s++)
            {
                float d = step * s;
                float v = ChannelAt(list, d, trackLen, LineChannel.Ideal);
                float l = ChannelAt(list, d, trackLen, LineChannel.Left);
                float r = ChannelAt(list, d, trackLen, LineChannel.Right);
                float lo = Mathf.Min(l, r), hi = Mathf.Max(l, r);
                bool low = side != CorridorSide.High && v < lo - CorridorTolerance;
                bool high = side != CorridorSide.Low && v > hi + CorridorTolerance;
                float over = low ? lo - v : high ? v - hi : 0f;
                if (over > 0f)
                {
                    if (!inRun) runFrom = d;
                    if (over > worst) { worst = over; worstAt = d; worstBound = v < lo ? lo : hi; }
                    inRun = true;
                }
                else if (inRun) CloseRun(d);
            }
            if (inRun) CloseRun(trackLen);
            if (added.Count == 0) return;
            list.AddRange(added);
            list.Sort((a, b) => a.distance.CompareTo(b.distance));
            SolveIdealCurve(list, trackLen);
        }
    }

    // One authored point, used by every line where the ideal sits where it was drawn; otherwise the drawn point
    // stays for the side lines and the ideal gets its own copy at `idealAt`.
    static void AddSplit(List<RacingLineAnchor> list, RacingLineAnchor a, float idealAt)
    {
        if (Mathf.Abs(idealAt - a.distance) < 0.01f) { list.Add(a); return; }
        var sides = a; sides.noIdeal = true;
        var ideal = a; ideal.noSides = true; ideal.distance = idealAt;
        list.Add(sides);
        list.Add(ideal);
    }

    // Which Turn segments are corners for the ideal line: a run of consecutive same-direction turns (wrapping
    // the lap on a closed loop) totalling at least racingLineCornerMinDegrees.
    bool[] IdealLineCorners()
    {
        int n = segments.Length;
        var mask = new bool[n];
        bool IsTurn(int k) => segments[k].type == SegmentType.Turn && Mathf.Abs(segments[k].angle) > 0.01f;
        bool Continues(int k)
        {
            if (!closedLoop && k == 0) return false;
            int p = (k - 1 + n) % n;
            return IsTurn(k) && IsTurn(p) && Mathf.Sign(segments[k].angle) == Mathf.Sign(segments[p].angle);
        }

        int from = 0;
        if (closedLoop)
        {
            from = -1;
            for (int k = 0; k < n; k++) if (!Continues(k)) { from = k; break; }
            if (from < 0)
            {
                // The whole lap is one run of turns the same way (a circle).
                float all = 0f;
                for (int k = 0; k < n; k++) all += Mathf.Abs(segments[k].angle);
                for (int k = 0; k < n; k++) mask[k] = all >= racingLineCornerMinDegrees;
                return mask;
            }
        }

        int w = 0;
        while (w < n)
        {
            if (!IsTurn((from + w) % n)) { w++; continue; }
            int runStart = w;
            float total = 0f;
            do { total += Mathf.Abs(segments[(from + w) % n].angle); w++; }
            while (w < n && Continues((from + w) % n));
            bool isCorner = total >= racingLineCornerMinDegrees;
            for (int r = runStart; r < w; r++) mask[(from + r) % n] = isCorner;
        }
        return mask;
    }

    // The nearest segment from i in direction dir (+1 / -1) that bounds a gap, or -1 if there is none (open track
    // end, or the walk came back round to i).
    int NeighbourIndex(int i, int dir, System.Func<int, bool> bounds)
    {
        int n = segments.Length, k = i;
        for (int step = 0; step < n; step++)
        {
            k += dir;
            if (k < 0 || k >= n) { if (!closedLoop) return -1; k = (k + n) % n; }
            if (k == i) return -1;
            if (bounds(k)) return k;
        }
        return -1;
    }

    // Metres of straight / shallow bend between segment i and its neighbouring corner in direction dir, or -1 when
    // there is no neighbour. Adjacent corners are 0 apart.
    float GapToNeighbour(int i, int dir, System.Func<int, bool> bounds)
    {
        int j = NeighbourIndex(i, dir, bounds);
        if (j < 0) return -1f;
        int n = segments.Length;
        float gap = 0f;
        for (int k = (i + dir + n) % n; k != j; k = (k + dir + n) % n) gap += segments[k].length;
        return gap;
    }

    // lineFactor: -1 = leftmost AI line, 0 = ideal, +1 = rightmost. Smoothly blended.
    // loopLengthOverride: the TRUE sampled loop length (incl. the seam TrackBuilder stitches back to the start).
    // Pass it so the wrap happens at the real loop end — otherwise the line jumps at the authored-segment total
    // (which sits near the start/finish) and the smoothing spreads that jump into a big sweep.
    //
    // Each line is interpolated through its own points (see BuildRacingLineAnchors) and the result blended. The
    // ideal is the minimum-curvature line through its points (SolveIdealCurve); the side lines keep the Catmull-Rom
    // they were drawn against.
    public float GetLateralAt(float distance, float lineFactor, List<RacingLineAnchor> anchors = null, float loopLengthOverride = 0f)
    {
        if (anchors == null) anchors = BuildRacingLineAnchors();
        if (anchors.Count == 0) return 0f;

        float trackLength = loopLengthOverride > 0f ? loopLengthOverride : TotalLength();
        if (closedLoop && trackLength > 0f)
            distance = ((distance % trackLength) + trackLength) % trackLength;

        lineFactor = Mathf.Clamp(lineFactor, -1f, 1f);
        float ideal = ChannelAt(anchors, distance, trackLength, LineChannel.Ideal);
        if (Mathf.Abs(lineFactor) < 1e-5f) return ideal;
        float side = ChannelAt(anchors, distance, trackLength, lineFactor > 0f ? LineChannel.Right : LineChannel.Left);
        return Mathf.Lerp(ideal, side, Mathf.Abs(lineFactor));
    }

    static bool InChannel(in RacingLineAnchor a, LineChannel c) => c == LineChannel.Ideal ? a.InIdeal : a.InSides;

    static float ValueOf(in RacingLineAnchor a, LineChannel c) =>
        c == LineChannel.Ideal ? a.ideal : c == LineChannel.Left ? a.left : a.right;

    // The next point of channel c from index i in direction dir, or -1 (only when not wrapping).
    static int StepInChannel(List<RacingLineAnchor> anchors, int i, int dir, LineChannel c, bool wrap)
    {
        int n = anchors.Count, k = i;
        for (int step = 0; step < n; step++)
        {
            k += dir;
            if (k < 0 || k >= n) { if (!wrap) return -1; k = (k + n) % n; }
            if (InChannel(anchors[k], c)) return k;
        }
        return -1;
    }

    float ChannelAt(List<RacingLineAnchor> anchors, float distance, float trackLength, LineChannel c)
    {
        int n = anchors.Count, count = 0, first = -1, last = -1, i1 = -1;
        for (int i = 0; i < n; i++)
        {
            if (!InChannel(anchors[i], c)) continue;
            count++;
            if (first < 0) first = i;
            last = i;
            if (anchors[i].distance <= distance) i1 = i;
        }
        if (count == 0) return 0f;
        if (count == 1) return ValueOf(anchors[first], c);

        int i0, i2, i3;
        float d0, d1, d2, d3;
        if (closedLoop && trackLength > 0f)
        {
            bool beforeFirst = i1 < 0;
            if (beforeFirst) i1 = last;
            i2 = StepInChannel(anchors, i1, +1, c, true);
            i0 = StepInChannel(anchors, i1, -1, c, true);
            i3 = StepInChannel(anchors, i2, +1, c, true);

            d1 = anchors[i1].distance;
            if (beforeFirst) d1 -= trackLength; // wrapped past zero; i1 is one lap behind
            d2 = anchors[i2].distance;
            if (i2 <= i1 || beforeFirst) { while (d2 <= d1) d2 += trackLength; }
            d0 = anchors[i0].distance;
            while (d0 > d1) d0 -= trackLength;
            d3 = anchors[i3].distance;
            while (d3 < d2) d3 += trackLength;
        }
        else
        {
            if (i1 < 0) i1 = first;
            i2 = StepInChannel(anchors, i1, +1, c, false); if (i2 < 0) i2 = i1;
            i0 = StepInChannel(anchors, i1, -1, c, false); if (i0 < 0) i0 = i1;
            i3 = StepInChannel(anchors, i2, +1, c, false); if (i3 < 0) i3 = i2;
            d0 = anchors[i0].distance; d1 = anchors[i1].distance; d2 = anchors[i2].distance; d3 = anchors[i3].distance;
            if (i0 == i1) d0 = 2f * d1 - d2;
            if (i3 == i2) d3 = 2f * d2 - d1;
        }

        float t = (d2 - d1) > 0f ? Mathf.Clamp01((distance - d1) / (d2 - d1)) : 0f;
        if (c == LineChannel.Ideal)
        {
            // The minimum-curvature line: the road's own bend plus the cubic spline through what is left.
            var p1 = anchors[i1]; var p2 = anchors[i2];
            if (p1.flatAfter && p2.flatBefore) return Mathf.Lerp(p1.ideal, p2.ideal, t);   // a long straight, driven straight
            float h = d2 - d1, u = 1f - t;
            float spline = u * p1.idealResidual + t * p2.idealResidual
                         + h * h / 6f * ((u * u * u - u) * p1.idealCurve + (t * t * t - t) * p2.idealCurve);
            return spline + RoadBendAt(distance);
        }
        float v0 = ValueOf(anchors[i0], c), v1 = ValueOf(anchors[i1], c);
        float v2 = ValueOf(anchors[i2], c), v3 = ValueOf(anchors[i3], c);
        return CatmullRomNonUniform(v0, v1, v2, v3, d0, d1, d2, d3, t);
    }

    static float CatmullRomNonUniform(
        float v0, float v1, float v2, float v3,
        float d0, float d1, float d2, float d3,
        float t)
    {
        float dt0 = Mathf.Max(d1 - d0, 1e-4f);
        float dt1 = Mathf.Max(d2 - d1, 1e-4f);
        float dt2 = Mathf.Max(d3 - d2, 1e-4f);

        float m1 = (v1 - v0) / dt0 - (v2 - v0) / (dt0 + dt1) + (v2 - v1) / dt1;
        float m2 = (v2 - v1) / dt1 - (v3 - v1) / (dt1 + dt2) + (v3 - v2) / dt2;
        m1 *= dt1;
        m2 *= dt1;

        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * v1
             + (t3 - 2f * t2 + t) * m1
             + (-2f * t3 + 3f * t2) * v2
             + (t3 - t2) * m2;
    }
}
