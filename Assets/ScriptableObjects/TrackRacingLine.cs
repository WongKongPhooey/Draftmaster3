using UnityEngine;

[CreateAssetMenu(fileName = "TrackRacingLine", menuName = "Racetrack/Track Racing Line", order = 4)]
public class TrackRacingLine : ScriptableObject
{
    [Tooltip("Default lateral offset used outside the waypoint range. 0 = centerline.")]
    public float defaultLateralOffset = 0f;
    [Tooltip("Default target speed in mph for AI sampling. 0 = no target.")]
    public float defaultSpeed = 0f;
    [Tooltip("If true, the waypoint list is treated as a closed loop. Add an explicit waypoint at distance == trackLength to author the join. If false, values are clamped to the first/last waypoint outside the authored range.")]
    public bool closedLoop = true;

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

        if (closedLoop && trackLength > 0f) distance = ((distance % trackLength) + trackLength) % trackLength;

        ResolveNeighbours(distance, trackLength,
            out float v0, out float v1, out float v2, out float v3,
            out float d0, out float d1, out float d2, out float d3,
            wp => wp.lateralOffset);

        float t = (d2 - d1) > 0f ? Mathf.Clamp01((distance - d1) / (d2 - d1)) : 0f;
        return CatmullRomNonUniform(v0, v1, v2, v3, d0, d1, d2, d3, t);
    }

    public float GetSpeedAt(float distance, float trackLength)
    {
        if (waypoints == null || waypoints.Length == 0) return defaultSpeed;
        if (waypoints.Length == 1) return waypoints[0].speed > 0f ? waypoints[0].speed : defaultSpeed;

        if (closedLoop && trackLength > 0f) distance = ((distance % trackLength) + trackLength) % trackLength;

        ResolveNeighbours(distance, trackLength,
            out float v0, out float v1, out float v2, out float v3,
            out float d0, out float d1, out float d2, out float d3,
            wp => wp.speed > 0f ? wp.speed : defaultSpeed);

        float t = (d2 - d1) > 0f ? Mathf.Clamp01((distance - d1) / (d2 - d1)) : 0f;
        return CatmullRomNonUniform(v0, v1, v2, v3, d0, d1, d2, d3, t);
    }

    delegate float WaypointValue(RacingLineWaypoint wp);

    void ResolveNeighbours(float distance, float trackLength,
        out float v0, out float v1, out float v2, out float v3,
        out float d0, out float d1, out float d2, out float d3,
        WaypointValue pick)
    {
        int n = waypoints.Length;
        int i1 = 0;
        for (int i = 0; i < n; i++)
        {
            if (waypoints[i].distance <= distance) i1 = i;
            else break;
        }
        int i2 = i1 + 1;

        if (closedLoop)
        {
            // Wrap with synthesised distances so segment lengths reflect the real arc between authored waypoints.
            int i0w = (i1 - 1 + n) % n;
            int i2w = i2 % n;
            int i3w = (i2 + 1) % n;

            d1 = waypoints[i1].distance;
            d2 = waypoints[i2w].distance;
            if (i2w <= i1) d2 += Mathf.Max(trackLength, d2 + 1f);
            d0 = waypoints[i0w].distance;
            if (i0w >= i1) d0 -= Mathf.Max(trackLength, waypoints[i0w].distance + 1f);
            d3 = waypoints[i3w].distance;
            // d3 must be >= d2 in the unrolled parameter space.
            while (d3 < d2) d3 += Mathf.Max(trackLength, 1f);

            v0 = pick(waypoints[i0w]);
            v1 = pick(waypoints[i1]);
            v2 = pick(waypoints[i2w]);
            v3 = pick(waypoints[i3w]);
        }
        else
        {
            // Open spline: clamp i2 inside the array and synthesise reflected end neighbours so the curve still has C1 continuity.
            if (i2 >= n) i2 = n - 1;
            if (i1 >= n - 1) i1 = n - 2;
            if (i1 < 0) { i1 = 0; i2 = Mathf.Min(1, n - 1); }

            d1 = waypoints[i1].distance;
            d2 = waypoints[i2].distance;

            int i0 = i1 - 1;
            if (i0 < 0)
            {
                // Reflect waypoints[1] about waypoints[0] in both distance and value.
                d0 = 2f * d1 - waypoints[Mathf.Min(1, n - 1)].distance;
                v0 = 2f * pick(waypoints[i1]) - pick(waypoints[Mathf.Min(1, n - 1)]);
            }
            else
            {
                d0 = waypoints[i0].distance;
                v0 = pick(waypoints[i0]);
            }

            int i3 = i2 + 1;
            if (i3 >= n)
            {
                d3 = 2f * d2 - waypoints[Mathf.Max(n - 2, 0)].distance;
                v3 = 2f * pick(waypoints[i2]) - pick(waypoints[Mathf.Max(n - 2, 0)]);
            }
            else
            {
                d3 = waypoints[i3].distance;
                v3 = pick(waypoints[i3]);
            }

            v1 = pick(waypoints[i1]);
            v2 = pick(waypoints[i2]);
        }
    }

    // Centripetal-style Catmull-Rom over non-uniform parameter values (here: distance along track).
    // C1 continuous at every waypoint regardless of segment length, so the wrap join matches the surrounding curve.
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
