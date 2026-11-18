using System.Collections.Generic;
using UnityEngine;

// How far a car may leave the road onto the grass or the infield to get round a wreck that fills the whole road
// (SplineDriver.allowGrass), per main-centreline sample: from the road's edge out to grassEscapeMetres, stopped half a
// car and a metre short of the first solid thing in the way - a wall, a building, a fence - and of the pit lane.
// Laterals are the SplineDriver's (m from the centreline, + right of travel): Lo is the left limit, Hi the right.
//
// Worked out once per track the first time a car needs it (a ray each side per sample against the 2D scenery) and
// shared by every car on it. Where nothing is found the full grassEscapeMetres is open: a track with no walls in
// the infield has the infield.
public sealed class GrassEscapeBand
{
    // Half a car plus a little: how far a car's centre stays off whatever stops the band.
    public const float CarClearance = 2f;
    // The rays start this far in from the road's edge, on the road, so a wall standing right on the edge is found.
    const float RayStartInside = 2f;

    public readonly float[] Lo;
    public readonly float[] Hi;
    readonly TrackBuilder _track;
    readonly int _count;
    readonly float _length;
    readonly float _metres;

    static readonly Dictionary<TrackBuilder, GrassEscapeBand> Cache = new Dictionary<TrackBuilder, GrassEscapeBand>();
    static readonly RaycastHit2D[] Hits = new RaycastHit2D[32];

    GrassEscapeBand(TrackBuilder track, List<TrackBuilder.Sample> samples, float metres)
    {
        _track = track;
        _count = samples.Count;
        _length = _count > 0 ? samples[_count - 1].distance : 0f;
        _metres = metres;
        Lo = new float[_count];
        Hi = new float[_count];
    }

    public bool Matches(TrackBuilder track, List<TrackBuilder.Sample> samples, float metres)
        => track == _track && samples != null && samples.Count == _count
           && Mathf.Approximately(_length, samples[_count - 1].distance) && Mathf.Approximately(metres, _metres);

    // The band for this track and centreline, built if it has not been (or the track was rebuilt since).
    public static GrassEscapeBand For(TrackBuilder track, List<TrackBuilder.Sample> samples, float metres)
    {
        if (track == null || samples == null || samples.Count < 2) return null;
        if (Cache.TryGetValue(track, out var band) && band != null && band.Matches(track, samples, metres)) return band;
        PruneDestroyed();
        band = new GrassEscapeBand(track, samples, metres);
        band.Build(samples);
        Cache[track] = band;
        return band;
    }

    // Forget every band (tests that move scenery about, a rebuilt package).
    public static void ClearCache() => Cache.Clear();

    static void PruneDestroyed()
    {
        List<TrackBuilder> dead = null;
        foreach (var k in Cache.Keys)
            if (k == null) (dead ??= new List<TrackBuilder>()).Add(k);
        if (dead != null) foreach (var k in dead) Cache.Remove(k);
    }

    void Build(List<TrackBuilder.Sample> samples)
    {
        int n = samples.Count;
        float[] reachLeft = new float[n], reachRight = new float[n];
        for (int i = 0; i < n; i++) { reachLeft[i] = _metres; reachRight[i] = _metres; }

        PitLaneLimits(samples, reachLeft, reachRight);

        var tf = _track.transform;
        var physics = _track.gameObject.scene.IsValid() ? _track.gameObject.scene.GetPhysicsScene2D() : Physics2D.defaultPhysicsScene;
        for (int i = 0; i < n; i++)
        {
            var s = samples[i];
            float half = Mathf.Max(0f, s.width * 0.5f);
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            for (int k = 0; k < 2; k++)
            {
                float dir = k == 0 ? 1f : -1f;
                ref float reach = ref (k == 0 ? ref reachRight[i] : ref reachLeft[i]);
                if (reach <= 0f) continue;
                Vector2 startLocal = s.position + right * dir * Mathf.Max(0f, half - RayStartInside);
                Vector2 origin = tf.TransformPoint(new Vector3(startLocal.x, startLocal.y, 0f));
                Vector2 outWorld = ((Vector2)tf.TransformDirection(new Vector3(right.x * dir, right.y * dir, 0f))).normalized;
                float startToEdge = Mathf.Min(RayStartInside, half);
                float wall = FirstSolid(physics, origin, outWorld, startToEdge + reach + CarClearance);
                if (wall < float.MaxValue) reach = Mathf.Min(reach, wall - startToEdge - CarClearance);
            }
            Lo[i] = -half - Mathf.Max(0f, reachLeft[i]);
            Hi[i] = half + Mathf.Max(0f, reachRight[i]);
        }
    }

    // Distance (m) along the ray to the first solid piece of scenery, float.MaxValue if none. Cars (they carry a
    // rigidbody or a collision responder), triggers and anything the ray starts inside (a ground plate under the road)
    // do not count.
    static float FirstSolid(PhysicsScene2D physics, Vector2 origin, Vector2 dir, float distance)
    {
        int count = physics.Raycast(origin, dir, distance, Hits);
        float best = float.MaxValue;
        for (int h = 0; h < count; h++)
        {
            var c = Hits[h].collider;
            if (c == null || c.isTrigger) continue;
            var rb = c.attachedRigidbody;
            if (rb != null && rb.bodyType != RigidbodyType2D.Static) continue;
            if (c.GetComponentInParent<ICollisionResponder>() != null) continue;
            if (c.OverlapPoint(origin)) continue;
            if (Hits[h].distance < best) best = Hits[h].distance;
        }
        return best;
    }

    // The pit lane is no way round a wreck (cars stopped in their boxes, crews over the wall): where it runs beside
    // the road the band stops short of it on that side.
    void PitLaneLimits(List<TrackBuilder.Sample> main, float[] reachLeft, float[] reachRight)
    {
        if (_track.track == null || !_track.track.hasPitLane) return;
        var pit = _track.SamplePitCenterline();
        if (pit == null || pit.Count < 2) return;
        float boxLane = _track.HasPitBoxLane ? _track.pitBoxLaneWidth : 0f;
        int n = main.Count;
        for (int p = 0; p < pit.Count; p++)
        {
            Vector2 pos = pit[p].position;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = (main[i].position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            float pitHalf = pit[p].width * 0.5f + boxLane;
            float spacing = Vector2.Distance(pit[Mathf.Max(0, p - 1)].position, pit[Mathf.Min(pit.Count - 1, p + 1)].position);
            // Every main sample level with this stretch of pit lane.
            for (int off = -8; off <= 8; off++)
            {
                int i = ((best + off) % n + n) % n;
                var s = main[i];
                Vector2 rel = pos - s.position;
                if (Mathf.Abs(Vector2.Dot(rel, s.tangent)) > Mathf.Max(spacing, 2f)) continue;
                Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
                float lat = Vector2.Dot(rel, right);
                float nearEdge = Mathf.Abs(lat) - pitHalf - s.width * 0.5f;   // m of grass between road and pit lane
                float room = Mathf.Max(0f, nearEdge - CarClearance);
                if (lat > 0f) reachRight[i] = Mathf.Min(reachRight[i], room);
                else reachLeft[i] = Mathf.Min(reachLeft[i], room);
            }
        }
    }
}
