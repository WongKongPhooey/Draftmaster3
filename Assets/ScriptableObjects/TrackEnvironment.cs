using UnityEngine;

[CreateAssetMenu(fileName = "TrackEnvironment", menuName = "Racetrack/Track Environment", order = 3)]
public class TrackEnvironment : ScriptableObject
{
    [Header("Strip Decorations (kerbs, gravel, white lines, pit-wall ribbons)")]
    public Strip[] strips;

    [Header("Point Decorations (prefabs placed along the spline)")]
    public Decoration[] decorations;

    [Header("Barriers — auto-generated per segment, both sides")]
    public bool generateBarriers = true;
    [Tooltip("Offset (m) from the RIGHT track edge for the inner barrier. Positive = further right (outboard).")]
    public float innerEdgeOffset = 0f;
    [Tooltip("Offset (m) from the LEFT track edge for the outer barrier. Positive = further left (outboard).")]
    public float outerEdgeOffset = 10f;
    public float barrierWidth = 1f;
    public Material barrierMaterial;
    public int barrierSortingOrder = 2;
    public float barrierUvLengthScale = 1f;
    [Tooltip("Add a solid collider to every barrier section.")]
    public bool barrierColliders = true;
    [Tooltip("Hand-drawn barrier spans. Each replaces the auto barrier on one side from a start segment boundary to an end segment boundary, drawing straight lines through the anchors and any hand-placed points between.")]
    public ManualBarrierSection[] manualSections;

    [Header("Barrier Gaps — openings for pit lane, service roads, etc.")]
    [Tooltip("Cut openings out of a specific barrier. Pick the barrier (side + segment index, e.g. Inner 2 = Barrier_Inner_2) and the start/end position in metres from the START of that barrier segment. Removes mesh + collider. Works on auto and manual (hand-drawn) sections.")]
    public BarrierGap[] barrierGaps;

    [Tooltip("Spacing between strip vertex rows in metres. Lower = smoother strips on tight curves, more triangles.")]
    public float stripSampleSpacing = 2f;

    public enum SplineRef { Main, Pit }
    public enum LateralAnchor { Centerline, LeftEdge, RightEdge }

    [System.Serializable]
    public struct Strip
    {
        public string label;
        public SplineRef useSpline;
        [Tooltip("Anchor for the lateralOffset value: Centerline = distance from track centre; LeftEdge / RightEdge = distance from that edge of the track, so the strip tracks the track edge when width varies.")]
        public LateralAnchor anchor;
        [Tooltip("Where the strip starts, in metres along the spline.")]
        public float startDistance;
        [Tooltip("Where the strip ends, in metres along the spline.")]
        public float endDistance;
        [Tooltip("Lateral offset from the chosen anchor, metres. Positive = right of travel direction.")]
        public float lateralOffset;
        [Tooltip("Strip width in metres.")]
        public float width;
        [Tooltip("Sorting order. Higher draws above the track surface.")]
        public int sortingOrder;
        public Material material;
        [Tooltip("Texture tiling along the strip length. 1 = one repeat per metre.")]
        public float uvLengthScale;
    }

    public enum BarrierSide { Inner, Outer }

    [System.Serializable]
    public struct BarrierGap
    {
        [Tooltip("Optional name, e.g. \"Pit entry\" or \"Garage access\".")]
        public string label;
        [Tooltip("Which side's barrier to open: Inner or Outer.")]
        public BarrierSide side;
        [Tooltip("Barrier segment to open — the N in Barrier_Inner_N / Barrier_Outer_N (the track segment index).")]
        public int segmentIndex;
        [Tooltip("Gap start, metres from the START of that barrier segment.")]
        public float startDistance;
        [Tooltip("Gap end, metres from the start of that barrier segment.")]
        public float endDistance;
    }

    // Which boundary of a segment an anchor sits on.
    public enum SegmentEnd { Start, End }

    [System.Serializable]
    public struct ManualBarrierSection
    {
        [Tooltip("Optional name, e.g. \"Pit wall\" or \"Turn 5 tyre stack\".")]
        public string label;
        [Tooltip("Which side's barrier this span replaces: Inner or Outer.")]
        public BarrierSide side;

        [Header("Start anchor")]
        [Tooltip("Segment whose boundary the manual barrier starts at.")]
        public int startSegmentIndex;
        [Tooltip("Which end of the start segment to anchor to.")]
        public SegmentEnd startEnd;

        [Header("End anchor")]
        [Tooltip("Segment whose boundary the manual barrier ends at.")]
        public int endSegmentIndex;
        [Tooltip("Which end of the end segment to anchor to.")]
        public SegmentEnd endEnd;

        [Tooltip("Hand-placed track-local points between the two anchors, in order. The barrier is straight lines: startAnchor → points[0] → … → endAnchor.")]
        public Vector2[] manualPoints;
    }

    [System.Serializable]
    public struct Decoration
    {
        public string label;
        public SplineRef useSpline;
        [Tooltip("Anchor for the lateralOffset value: Centerline, LeftEdge or RightEdge.")]
        public LateralAnchor anchor;
        [Tooltip("Distance along the spline, in metres.")]
        public float distance;
        [Tooltip("Lateral offset from the chosen anchor. Positive = right of travel direction.")]
        public float lateralOffset;
        [Tooltip("Extra rotation in degrees applied on top of the spline tangent.")]
        public float rotationOffset;
        [Tooltip("Optional scale override. Leave (0,0) to use the prefab's own scale.")]
        public Vector2 scale;
        public GameObject prefab;
    }
}
