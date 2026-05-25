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
    [Tooltip("World position where the pit lane starts.")]
    public Vector2 pitStartPosition;
    [Tooltip("Initial heading of the pit lane, in degrees.")]
    public float pitStartHeading;
    [Tooltip("Default width for the pit lane, in metres. 0 = inherit defaultWidth.")]
    public float pitDefaultWidth;
    [Tooltip("Pit-lane segments. Same format as the main spline.")]
    public TrackSegment[] pitSegments;
    [Tooltip("Distance along the main spline (metres) where the pit-lane entry diverges.")]
    public float pitEntryDistance;
    [Tooltip("Distance along the main spline (metres) where the pit lane rejoins.")]
    public float pitExitDistance;
    [Tooltip("Pit-lane speed limit in mph. Informational for now.")]
    public int pitSpeedLimit = 50;

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
    }

    public struct RacingLineAnchor
    {
        public float distance;
        public float ideal;
        public float left;
        public float right;
    }

    public float TotalLength()
    {
        if (segments == null) return 0f;
        float total = 0f;
        for (int i = 0; i < segments.Length; i++) total += segments[i].length;
        return total;
    }

    public List<RacingLineAnchor> BuildRacingLineAnchors()
    {
        var list = new List<RacingLineAnchor>();
        if (segments == null) return list;
        float cum = 0f;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var rl = seg.racingLine;
            if (seg.type == SegmentType.Turn)
            {
                list.Add(new RacingLineAnchor { distance = cum - seg.leadIn, ideal = rl.idealEntry, left = rl.leftEntry, right = rl.rightEntry });
                list.Add(new RacingLineAnchor { distance = cum + seg.length * 0.5f, ideal = rl.idealApex, left = rl.leftApex, right = rl.rightApex });
                list.Add(new RacingLineAnchor { distance = cum + seg.length + seg.leadOut, ideal = rl.idealExit, left = rl.leftExit, right = rl.rightExit });
            }
            else
            {
                float midFrac = Mathf.Clamp(0.5f + seg.straightMidpointOffset, 0f, 1f);
                list.Add(new RacingLineAnchor { distance = cum + seg.length * midFrac, ideal = rl.idealApex, left = rl.leftApex, right = rl.rightApex });
            }
            cum += seg.length;
        }
        list.Sort((a, b) => a.distance.CompareTo(b.distance));
        return list;
    }

    // lineFactor: -1 = leftmost AI line, 0 = ideal, +1 = rightmost. Smoothly blended.
    public float GetLateralAt(float distance, float lineFactor, List<RacingLineAnchor> anchors = null)
    {
        if (anchors == null) anchors = BuildRacingLineAnchors();
        if (anchors.Count == 0) return 0f;
        if (anchors.Count == 1) return BlendAnchor(anchors[0], lineFactor);

        float trackLength = TotalLength();
        if (closedLoop && trackLength > 0f)
            distance = ((distance % trackLength) + trackLength) % trackLength;

        ResolveAnchorNeighbours(anchors, distance, trackLength,
            out var a0, out var a1, out var a2, out var a3,
            out float d0, out float d1, out float d2, out float d3);

        float v0 = BlendAnchor(a0, lineFactor);
        float v1 = BlendAnchor(a1, lineFactor);
        float v2 = BlendAnchor(a2, lineFactor);
        float v3 = BlendAnchor(a3, lineFactor);

        float t = (d2 - d1) > 0f ? Mathf.Clamp01((distance - d1) / (d2 - d1)) : 0f;
        return CatmullRomNonUniform(v0, v1, v2, v3, d0, d1, d2, d3, t);
    }

    static float BlendAnchor(RacingLineAnchor a, float lineFactor)
    {
        lineFactor = Mathf.Clamp(lineFactor, -1f, 1f);
        if (lineFactor >= 0f) return Mathf.Lerp(a.ideal, a.right, lineFactor);
        return Mathf.Lerp(a.ideal, a.left, -lineFactor);
    }

    void ResolveAnchorNeighbours(List<RacingLineAnchor> anchors, float distance, float trackLength,
        out RacingLineAnchor a0, out RacingLineAnchor a1, out RacingLineAnchor a2, out RacingLineAnchor a3,
        out float d0, out float d1, out float d2, out float d3)
    {
        int n = anchors.Count;
        int i1 = 0;
        for (int i = 0; i < n; i++)
        {
            if (anchors[i].distance <= distance) i1 = i;
            else break;
        }

        if (closedLoop)
        {
            int i0w = (i1 - 1 + n) % n;
            int i2w = (i1 + 1) % n;
            int i3w = (i1 + 2) % n;

            a0 = anchors[i0w]; a1 = anchors[i1]; a2 = anchors[i2w]; a3 = anchors[i3w];

            d1 = a1.distance;
            d2 = a2.distance;
            if (i2w <= i1) d2 += Mathf.Max(trackLength, d2 + 1f);
            d0 = a0.distance;
            if (i0w >= i1) d0 -= Mathf.Max(trackLength, a0.distance + 1f);
            d3 = a3.distance;
            while (d3 < d2) d3 += Mathf.Max(trackLength, 1f);
        }
        else
        {
            int i2 = Mathf.Min(i1 + 1, n - 1);
            int i0 = Mathf.Max(i1 - 1, 0);
            int i3 = Mathf.Min(i2 + 1, n - 1);
            a0 = anchors[i0]; a1 = anchors[i1]; a2 = anchors[i2]; a3 = anchors[i3];
            d0 = a0.distance; d1 = a1.distance; d2 = a2.distance; d3 = a3.distance;
            if (i0 == i1) { d0 = 2f * d1 - d2; }
            if (i3 == i2) { d3 = 2f * d2 - d1; }
        }
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
