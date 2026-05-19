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
    [Tooltip("Minimum centerline samples per segment. Acts as a floor.")]
    public int samplesPerSegment = 4;
    [Tooltip("Maximum spacing between centerline samples in metres. Longer segments emit proportionally more samples, so curves stay smooth regardless of length.")]
    public float maxArcStepMetres = 2f;
    [Tooltip("If true, the final segment connects back to startPosition. Segments should sum back to start; mismatch will show visually.")]
    public bool closedLoop = true;

    [Header("Segments (in order around the lap)")]
    public TrackSegment[] segments;

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
