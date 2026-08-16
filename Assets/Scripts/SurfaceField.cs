using System.Collections.Generic;
using UnityEngine;

// Runtime lookup of which runoff surface (grass / gravel / tarmac) a world point sits on. TrackEnvironmentBuilder
// registers each runoff polygon (in WORLD space) here on Build; the car physics queries it each FixedUpdate to
// pick the right grip/drag. Polygons that overlap resolve to the LAST-registered one (drawn on top).
public static class SurfaceField
{
    struct Poly
    {
        public Vector2[] pts;
        public Vector2 min, max; // AABB, so a query skips the crossing test for polys it can't be inside
        public TrackEnvironment.SurfaceType surface;
    }

    static readonly List<Poly> _polys = new();

    public static void Clear() => _polys.Clear();

    public static void Add(Vector2[] worldPoints, TrackEnvironment.SurfaceType surface)
    {
        if (worldPoints == null || worldPoints.Length < 3) return;
        var min = worldPoints[0];
        var max = worldPoints[0];
        for (int i = 1; i < worldPoints.Length; i++)
        {
            min = Vector2.Min(min, worldPoints[i]);
            max = Vector2.Max(max, worldPoints[i]);
        }
        _polys.Add(new Poly { pts = worldPoints, min = min, max = max, surface = surface });
    }

    public static bool TryGetSurface(Vector2 worldPos, out TrackEnvironment.SurfaceType surface)
    {
        surface = default;
        bool found = false;
        for (int i = 0; i < _polys.Count; i++)
        {
            var p = _polys[i];
            if (worldPos.x < p.min.x || worldPos.x > p.max.x || worldPos.y < p.min.y || worldPos.y > p.max.y) continue;
            if (PointInPolygon(p.pts, worldPos)) { surface = p.surface; found = true; }
        }
        return found;
    }

    // Standard even-odd crossing test.
    static bool PointInPolygon(Vector2[] v, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if (((v[i].y > p.y) != (v[j].y > p.y)) &&
                (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x))
                inside = !inside;
        }
        return inside;
    }
}
