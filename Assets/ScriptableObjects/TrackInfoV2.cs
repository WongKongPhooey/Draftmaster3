using UnityEngine;

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
    [Tooltip("Centerline samples per segment. Higher = smoother arcs.")]
    public int samplesPerSegment = 16;
    [Tooltip("If true, the final segment connects back to startPosition. Segments should sum back to start; mismatch will show visually.")]
    public bool closedLoop = true;

    [Header("Segments (in order around the lap)")]
    public TrackSegment[] segments;

    public enum SegmentType { Straight, Turn }

    [System.Serializable]
    public struct TrackSegment
    {
        public SegmentType type;
        [Tooltip("Length in metres along the centerline.")]
        public float length;
        [Tooltip("Turn angle in degrees. Positive = left (CCW), negative = right (CW). Ignored for straights.")]
        public float angle;
        [Tooltip("Banking angle in degrees. Informational for physics/AI.")]
        public float banking;
        [Tooltip("Lead-in distance for racing-line calc (metres). Geometry ignores this.")]
        public float leadIn;
        [Tooltip("Lead-out distance for racing-line calc (metres). Geometry ignores this.")]
        public float leadOut;
        [Tooltip("Max segment speed in mph. Informational.")]
        public int maxSpeed;
        [Tooltip("Width override at this segment (metres). 0 = use defaultWidth.")]
        public float width;
    }
}
