using UnityEngine;

[CreateAssetMenu(fileName = "TrackRacingLine", menuName = "Racetrack/Track Racing Line", order = 4)]
public class TrackRacingLine : ScriptableObject
{
    [Tooltip("Default lateral offset used outside the waypoint range. 0 = centerline.")]
    public float defaultLateralOffset = 0f;
    [Tooltip("Default target speed in mph for AI sampling. 0 = no target.")]
    public float defaultSpeed = 0f;

    [Header("Waypoints (ordered by distance along the main spline)")]
    public RacingLineWaypoint[] waypoints;

    [System.Serializable]
    public struct RacingLineWaypoint
    {
        public string label;
        [Tooltip("Distance along the main spline, in metres.")]
        public float distance;
        [Tooltip("Lateral offset from the centerline at this waypoint, metres. Positive = right of travel direction.")]
        public float lateralOffset;
        [Tooltip("Target speed in mph at this waypoint. 0 = use defaultSpeed.")]
        public float speed;
    }

    public float GetLateralAt(float distance, float trackLength)
    {
        if (waypoints == null || waypoints.Length == 0) return defaultLateralOffset;
        if (waypoints.Length == 1) return waypoints[0].lateralOffset;

        if (trackLength > 0f) distance = ((distance % trackLength) + trackLength) % trackLength;

        FindBracketingWaypoints(distance, trackLength, out var a, out var b, out float t);
        return Mathf.Lerp(a.lateralOffset, b.lateralOffset, t);
    }

    public float GetSpeedAt(float distance, float trackLength)
    {
        if (waypoints == null || waypoints.Length == 0) return defaultSpeed;
        if (waypoints.Length == 1) return waypoints[0].speed > 0f ? waypoints[0].speed : defaultSpeed;

        if (trackLength > 0f) distance = ((distance % trackLength) + trackLength) % trackLength;
        FindBracketingWaypoints(distance, trackLength, out var a, out var b, out float t);
        float sa = a.speed > 0f ? a.speed : defaultSpeed;
        float sb = b.speed > 0f ? b.speed : defaultSpeed;
        return Mathf.Lerp(sa, sb, t);
    }

    void FindBracketingWaypoints(float distance, float trackLength, out RacingLineWaypoint a, out RacingLineWaypoint b, out float t)
    {
        // Assumes waypoints are sorted by distance ascending.
        int n = waypoints.Length;
        int lo = 0;
        for (int i = 0; i < n; i++)
        {
            if (waypoints[i].distance <= distance) lo = i;
            else break;
        }
        int hi = (lo + 1) % n;

        a = waypoints[lo];
        b = waypoints[hi];
        float da = a.distance;
        float db = b.distance;
        if (hi < lo) db += Mathf.Max(trackLength, db + 1f); // wrap-around for closed loop
        float denom = db - da;
        t = denom > 0f ? Mathf.Clamp01((distance - da) / denom) : 0f;
    }
}
