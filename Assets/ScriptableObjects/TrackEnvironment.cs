using UnityEngine;

[CreateAssetMenu(fileName = "TrackEnvironment", menuName = "Racetrack/Track Environment", order = 3)]
public class TrackEnvironment : ScriptableObject
{
    [Header("Strip Decorations (kerbs, gravel, white lines, pit-wall ribbons)")]
    public Strip[] strips;

    [Header("Point Decorations (prefabs placed along the spline)")]
    public Decoration[] decorations;

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
