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
    [Tooltip("Collider thickness (m) extending outward, away from the track. Make this larger than a car's per-frame travel to prevent tunnelling. ~6m is safe.")]
    public float barrierColliderThickness = 6f;
    [Tooltip("Sections switched from auto-follow to hand-drawn. Each targets one segment + side.")]
    public ManualBarrierSection[] manualSections;

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
    public struct ManualBarrierSection
    {
        [Tooltip("Main-spline segment index this manual section replaces.")]
        public int segmentIndex;
        public BarrierSide side;
        [Tooltip("Hand-drawn track-local points. Endpoints auto-seed to neighbour barrier ends if empty.")]
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
