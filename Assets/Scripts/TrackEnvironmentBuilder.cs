using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class TrackEnvironmentBuilder : MonoBehaviour
{
    public TrackBuilder track;
    public TrackEnvironment environment;
    public bool rebuildOnValidate = true;

    [Header("Scene Editor")]
    [Tooltip("Index into environment.manualSections currently edited in the Scene view. -1 = none.")]
    public int editManualSectionIndex = -1;
    [Tooltip("Index into environment.runoffAreas currently edited in the Scene view. -1 = none.")]
    public int editRunoffIndex = -1;

    void OnEnable() { Build(); }

    // Right-click the component header → Rebuild. Use after editing the TrackEnvironment asset,
    // since changes to the referenced SO don't trigger this component's OnValidate.
    [ContextMenu("Rebuild")]
    public void Rebuild() => Build();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!rebuildOnValidate) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Build();
        };
    }
#endif

    public void Build()
    {
        if (track == null || environment == null) return;
        ClearChildren();

        var samples = track.SampleCenterline();
        var pitSamples = track.SamplePitCenterline();
        if (samples.Count < 2) return;

        BuildRunoff();
        BuildStrips(samples, pitSamples);
        BuildBarriers(samples, pitSamples);
        BuildDecorations(samples, pitSamples);
    }

    void BuildRunoff()
    {
        // Always reset the physics surface lookup so deleting/clearing areas takes effect.
        SurfaceField.Clear();
        if (environment.runoffAreas == null || environment.runoffAreas.Length == 0) return;
        var root = new GameObject("Runoff");
        root.transform.SetParent(transform, false);

        for (int i = 0; i < environment.runoffAreas.Length; i++)
        {
            var area = environment.runoffAreas[i];
            if (area.points == null || area.points.Length < 3) continue;

            var mesh = BuildPolygonMesh(area.points);
            if (mesh == null) continue;

            var go = new GameObject(string.IsNullOrEmpty(area.label) ? $"Runoff_{area.surface}_{i}" : area.label);
            go.transform.SetParent(root.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sortingOrder = environment.runoffSortingOrder;
            mr.sharedMaterial = area.materialOverride != null ? area.materialOverride : DefaultSurfaceMaterial(area.surface);
            mf.sharedMesh = mesh;

            // Register the polygon (in world space) so the car physics can sample its surface type.
            var worldPts = new Vector2[area.points.Length];
            for (int k = 0; k < area.points.Length; k++)
            {
                Vector3 w = track.transform.TransformPoint(new Vector3(area.points[k].x, area.points[k].y, 0f));
                worldPts[k] = new Vector2(w.x, w.y);
            }
            SurfaceField.Add(worldPts, area.surface);
        }
    }

    Material DefaultSurfaceMaterial(TrackEnvironment.SurfaceType surface)
    {
        switch (surface)
        {
            case TrackEnvironment.SurfaceType.Grass: return environment.grassMaterial;
            case TrackEnvironment.SurfaceType.Gravel: return environment.gravelMaterial;
            default: return environment.tarmacRunoffMaterial;
        }
    }

    // Triangulate a simple (non-self-intersecting) polygon by ear clipping. UVs map world metres 1:1.
    static Mesh BuildPolygonMesh(Vector2[] points)
    {
        var poly = new List<Vector2>(points);
        // Drop duplicate consecutive points.
        for (int i = poly.Count - 1; i > 0; i--)
            if ((poly[i] - poly[i - 1]).sqrMagnitude < 1e-8f) poly.RemoveAt(i);
        if (poly.Count < 3) return null;

        // Ensure counter-clockwise winding so the ear test signs are consistent.
        if (SignedArea(poly) < 0f) poly.Reverse();

        var indices = new List<int>();
        var remaining = new List<int>();
        for (int i = 0; i < poly.Count; i++) remaining.Add(i);

        int guard = 0;
        int maxGuard = poly.Count * poly.Count + 16;
        while (remaining.Count > 3 && guard++ < maxGuard)
        {
            bool clipped = false;
            for (int r = 0; r < remaining.Count; r++)
            {
                int i0 = remaining[(r - 1 + remaining.Count) % remaining.Count];
                int i1 = remaining[r];
                int i2 = remaining[(r + 1) % remaining.Count];
                Vector2 a = poly[i0], b = poly[i1], c = poly[i2];

                if (Cross(b - a, c - a) <= 0f) continue; // reflex vertex, not an ear

                bool hasInside = false;
                for (int k = 0; k < remaining.Count; k++)
                {
                    int idx = remaining[k];
                    if (idx == i0 || idx == i1 || idx == i2) continue;
                    if (PointInTriangle(poly[idx], a, b, c)) { hasInside = true; break; }
                }
                if (hasInside) continue;

                indices.Add(i0); indices.Add(i1); indices.Add(i2);
                remaining.RemoveAt(r);
                clipped = true;
                break;
            }
            if (!clipped) break; // degenerate / self-intersecting — bail with what we have
        }
        if (remaining.Count == 3)
        {
            indices.Add(remaining[0]); indices.Add(remaining[1]); indices.Add(remaining[2]);
        }
        if (indices.Count < 3) return null;

        var verts = new List<Vector3>(poly.Count);
        var uvs = new List<Vector2>(poly.Count);
        var normals = new List<Vector3>(poly.Count);
        for (int i = 0; i < poly.Count; i++)
        {
            verts.Add(new Vector3(poly[i].x, poly[i].y, 0f));
            uvs.Add(poly[i]); // world metres → UV, so tiling matches the track scale
            normals.Add(new Vector3(0f, 0f, -1f)); // face the camera (orthographic, looking +z)
        }

        // Double-sided: emit the triangles both windings so the surface shows regardless of the material's
        // cull mode or which way the polygon happens to be wound relative to the camera.
        var tris = new List<int>(indices.Count * 2);
        tris.AddRange(indices);
        for (int i = 0; i < indices.Count; i += 3)
        {
            tris.Add(indices[i]); tris.Add(indices[i + 2]); tris.Add(indices[i + 1]);
        }

        var mesh = new Mesh { name = "RunoffPolygon" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        return mesh;
    }

    static float SignedArea(List<Vector2> p)
    {
        float a = 0f;
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 cur = p[i], next = p[(i + 1) % p.Count];
            a += cur.x * next.y - next.x * cur.y;
        }
        return a * 0.5f;
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    void BuildBarriers(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (!environment.generateBarriers) return;
        if (track.track == null || track.track.segments == null || track.track.segments.Length == 0) return;

        var root = new GameObject("Barriers");
        root.transform.SetParent(transform, false);

        var segs = track.track.segments;
        float spacing = Mathf.Max(0.25f, environment.stripSampleSpacing);

        // Per-segment distance ranges.
        float[] segStart = new float[segs.Length];
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++) { segStart[i] = cum; cum += segs[i].length; }

        // Auto barriers per segment, with any gaps AND hand-drawn manual spans cut out.
        for (int i = 0; i < segs.Length; i++)
        {
            float dStart = segStart[i];
            float dEnd = dStart + segs[i].length;
            BuildAutoBarrierSide(root.transform, i, TrackEnvironment.BarrierSide.Inner, dStart, dEnd, mainSamples, spacing);
            BuildAutoBarrierSide(root.transform, i, TrackEnvironment.BarrierSide.Outer, dStart, dEnd, mainSamples, spacing);
        }

        // Close the loop. TrackBuilder stitches a seam from the last authored point back to the start, so the
        // centerline runs past the authored segment total. Build barriers across that seam too — otherwise the
        // last segment's barrier stops short of the first and leaves an open gap at the start/finish.
        if (track.track.closedLoop)
        {
            float authoredTotal = cum;
            float fullTotal = mainSamples[mainSamples.Count - 1].distance;
            if (fullTotal - authoredTotal > 0.05f)
            {
                int seamIndex = segs.Length; // names the pieces Barrier_*_<segCount>
                BuildAutoBarrierSide(root.transform, seamIndex, TrackEnvironment.BarrierSide.Inner, authoredTotal, fullTotal, mainSamples, spacing);
                BuildAutoBarrierSide(root.transform, seamIndex, TrackEnvironment.BarrierSide.Outer, authoredTotal, fullTotal, mainSamples, spacing);
            }
        }

        // Hand-drawn manual spans replace the auto stretches removed above.
        BuildManualSpans(root.transform, mainSamples);
    }

    // Build one segment's AUTO barrier for a side, split into pieces with any overlapping gap ranges and
    // hand-drawn manual spans cut out (so a manual barrier fully replaces the auto one across its span).
    void BuildAutoBarrierSide(Transform root, int segIndex, TrackEnvironment.BarrierSide side,
        float dStart, float dEnd, List<TrackBuilder.Sample> mainSamples, float spacing)
    {
        var spans = SubtractGaps(segIndex, dStart, dEnd, side);
        spans = SubtractManualSpans(spans, side);
        for (int k = 0; k < spans.Count; k++)
        {
            float a = spans[k].x, b = spans[k].y;
            if (b - a < 0.05f) continue; // skip slivers left by a cut touching a boundary
            BuildBarrierPieceMesh(root, $"Barrier_{side}_{segIndex}_{k}", side,
                BuildAutoEdgeCenterline(side, a, b, mainSamples, spacing));
        }
    }

    // Remaining barrier intervals within [dStart, dEnd] after removing gaps targeting this segment + side.
    // Gap start/end are LOCAL: metres from the start of this barrier segment.
    List<Vector2> SubtractGaps(int segIndex, float dStart, float dEnd, TrackEnvironment.BarrierSide side)
    {
        var spans = new List<Vector2>();
        if (dEnd <= dStart) return spans;
        spans.Add(new Vector2(dStart, dEnd));
        if (environment.barrierGaps == null) return spans;

        float segLen = dEnd - dStart;
        for (int g = 0; g < environment.barrierGaps.Length; g++)
        {
            var gap = environment.barrierGaps[g];
            if (gap.segmentIndex != segIndex || gap.side != side) continue;
            float gs = dStart + Mathf.Clamp(Mathf.Min(gap.startDistance, gap.endDistance), 0f, segLen);
            float ge = dStart + Mathf.Clamp(Mathf.Max(gap.startDistance, gap.endDistance), 0f, segLen);
            spans = RemoveInterval(spans, gs, ge);
        }
        return spans;
    }

    // Remove the global distance ranges covered by hand-drawn manual barriers on this side.
    List<Vector2> SubtractManualSpans(List<Vector2> spans, TrackEnvironment.BarrierSide side)
    {
        if (environment.manualSections == null) return spans;
        for (int m = 0; m < environment.manualSections.Length; m++)
        {
            var sec = environment.manualSections[m];
            if (sec.side != side) continue;
            if (!TryManualSpanRange(sec, out float lo, out float hi)) continue;
            spans = RemoveInterval(spans, lo, hi);
        }
        return spans;
    }

    // Cut [gs, ge] out of a list of [start, end] intervals.
    static List<Vector2> RemoveInterval(List<Vector2> spans, float gs, float ge)
    {
        if (ge - gs < 1e-4f) return spans;
        var next = new List<Vector2>();
        for (int k = 0; k < spans.Count; k++)
        {
            float a = spans[k].x, b = spans[k].y;
            if (ge <= a || gs >= b) { next.Add(spans[k]); continue; } // no overlap
            if (gs > a) next.Add(new Vector2(a, gs));                  // left remainder
            if (ge < b) next.Add(new Vector2(ge, b));                  // right remainder
        }
        return next;
    }

    // Build every hand-drawn manual barrier as a single straight-line polyline: startAnchor → points → endAnchor.
    void BuildManualSpans(Transform root, List<TrackBuilder.Sample> mainSamples)
    {
        if (environment.manualSections == null) return;
        for (int m = 0; m < environment.manualSections.Length; m++)
        {
            var sec = environment.manualSections[m];
            if (!TryGetManualAnchorPoints(sec, mainSamples, out Vector2 startA, out Vector2 endA)) continue;

            var line = new List<Vector2> { startA };
            if (sec.manualPoints != null) line.AddRange(sec.manualPoints);
            line.Add(endA);
            if (line.Count < 2) continue;

            BuildBarrierPieceMesh(root, $"Barrier_{sec.side}_Manual_{m}", sec.side, line);
        }
    }

    void BuildBarrierPieceMesh(Transform root, string name, TrackEnvironment.BarrierSide side, List<Vector2> centerline)
    {
        if (centerline == null || centerline.Count < 2) return;

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sortingOrder = environment.barrierSortingOrder;
        if (environment.barrierMaterial != null) mr.sharedMaterial = environment.barrierMaterial;

        mf.sharedMesh = BuildPolylineRibbon(centerline, Mathf.Max(0.05f, environment.barrierWidth),
            environment.barrierUvLengthScale > 0f ? environment.barrierUvLengthScale : 1f);

        if (environment.barrierColliders)
        {
            var col = go.AddComponent<PolygonCollider2D>();
            col.points = BuildBarrierColliderPath(centerline, 1f);
            col.offset = Vector2.zero;
        }
    }

    // Distance (m) along the main spline of a segment boundary.
    bool TryAnchorDistance(int segIndex, TrackEnvironment.SegmentEnd end, out float distance)
    {
        distance = 0f;
        var segs = track != null && track.track != null ? track.track.segments : null;
        if (segs == null || segIndex < 0 || segIndex >= segs.Length) return false;
        float d = 0f;
        for (int i = 0; i < segIndex; i++) d += segs[i].length;
        if (end == TrackEnvironment.SegmentEnd.End) d += segs[segIndex].length;
        distance = d;
        return true;
    }

    // Ordered [lo, hi] main-spline distance range a manual section covers (used to cut the auto barrier).
    bool TryManualSpanRange(TrackEnvironment.ManualBarrierSection sec, out float lo, out float hi)
    {
        lo = hi = 0f;
        if (!TryAnchorDistance(sec.startSegmentIndex, sec.startEnd, out float ds)) return false;
        if (!TryAnchorDistance(sec.endSegmentIndex, sec.endEnd, out float de)) return false;
        lo = Mathf.Min(ds, de);
        hi = Mathf.Max(ds, de);
        return hi - lo > 1e-3f;
    }

    bool TryGetManualAnchorPoints(TrackEnvironment.ManualBarrierSection sec, List<TrackBuilder.Sample> mainSamples,
        out Vector2 startAnchor, out Vector2 endAnchor)
    {
        startAnchor = endAnchor = Vector2.zero;
        if (!TryAnchorDistance(sec.startSegmentIndex, sec.startEnd, out float ds)) return false;
        if (!TryAnchorDistance(sec.endSegmentIndex, sec.endEnd, out float de)) return false;
        startAnchor = EdgePoint(sec.side, ds, mainSamples);
        endAnchor = EdgePoint(sec.side, de, mainSamples);
        return true;
    }

    // Editor helper: the two fixed anchor points of a manual section, in track-local space.
    public bool TryGetManualAnchors(int sectionIndex, out Vector2 startLocal, out Vector2 endLocal)
    {
        startLocal = endLocal = Vector2.zero;
        if (track == null || environment == null || environment.manualSections == null) return false;
        if (sectionIndex < 0 || sectionIndex >= environment.manualSections.Length) return false;
        var samples = track.SampleCenterline();
        if (samples == null || samples.Count < 2) return false;
        return TryGetManualAnchorPoints(environment.manualSections[sectionIndex], samples, out startLocal, out endLocal);
    }

    // Thin wall centred on the barrier centerline. Both faces offset ±thickness/2 along the PER-POINT normal,
    // so the collider stays glued to the curving barrier instead of drifting (fixed-axis offset broke on turns).
    Vector2[] BuildBarrierColliderPath(List<Vector2> centerline, float thickness)
    {
        int n = centerline.Count;
        var path = new Vector2[n * 2];
        float half = thickness * 0.5f;
        for (int i = 0; i < n; i++)
        {
            Vector2 tangent;
            if (i == 0) tangent = centerline[1] - centerline[0];
            else if (i == n - 1) tangent = centerline[i] - centerline[i - 1];
            else tangent = centerline[i + 1] - centerline[i - 1];
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector2.right;
            tangent.Normalize();
            Vector2 normal = new Vector2(tangent.y, -tangent.x);
            path[i] = centerline[i] + normal * half;                 // near face
            path[n * 2 - 1 - i] = centerline[i] - normal * half;     // far face, reversed
        }
        return path;
    }

    // Auto: sample the track edge across the segment span, offset outboard by inner/outer offset.
    List<Vector2> BuildAutoEdgeCenterline(TrackEnvironment.BarrierSide side, float dStart, float dEnd,
        List<TrackBuilder.Sample> mainSamples, float spacing)
    {
        var list = new List<Vector2>();
        if (mainSamples == null || mainSamples.Count < 2) return list;
        float length = dEnd - dStart;
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            float d = dStart + length * t;
            list.Add(EdgePoint(side, d, mainSamples));
        }
        return list;
    }

    Vector2 EdgePoint(TrackEnvironment.BarrierSide side, float distance, List<TrackBuilder.Sample> mainSamples)
    {
        var sample = track.SampleAt(distance, mainSamples);
        float offset = SegmentOffset(side, distance);
        // normal = right of travel. Inner = right edge + innerOffset (outboard right). Outer = left edge - outerOffset (outboard left).
        if (side == TrackEnvironment.BarrierSide.Inner)
            return sample.position + sample.normal * (sample.width * 0.5f + offset);
        return sample.position - sample.normal * (sample.width * 0.5f + offset);
    }

    // Per-segment barrier offset, falling back to the global inner/outer value when a segment has no override.
    float SegmentOffset(TrackEnvironment.BarrierSide side, float distance)
    {
        float global = side == TrackEnvironment.BarrierSide.Inner ? environment.innerEdgeOffset : environment.outerEdgeOffset;
        if (environment.barrierOffsets == null || environment.barrierOffsets.Length == 0) return global;
        int seg = SegmentIndexAtDistance(distance);
        for (int i = 0; i < environment.barrierOffsets.Length; i++)
        {
            if (environment.barrierOffsets[i].segmentIndex != seg) continue;
            return side == TrackEnvironment.BarrierSide.Inner
                ? environment.barrierOffsets[i].innerOffset
                : environment.barrierOffsets[i].outerOffset;
        }
        return global;
    }

    int SegmentIndexAtDistance(float distance)
    {
        var segs = track != null && track.track != null ? track.track.segments : null;
        if (segs == null || segs.Length == 0) return 0;
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            cum += segs[i].length;
            if (distance < cum) return i;
        }
        return segs.Length - 1;
    }

    Mesh BuildPolylineRibbon(List<Vector2> centerline, float width, float uvScale)
    {
        var mesh = new Mesh { name = "BarrierRibbon" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float half = width * 0.5f;
        float cumulative = 0f;
        for (int i = 0; i < centerline.Count; i++)
        {
            Vector2 tangent;
            if (i == 0) tangent = (centerline[1] - centerline[0]);
            else if (i == centerline.Count - 1) tangent = (centerline[i] - centerline[i - 1]);
            else tangent = (centerline[i + 1] - centerline[i - 1]);
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector2.right;
            tangent.Normalize();
            Vector2 normal = new Vector2(tangent.y, -tangent.x);
            Vector2 left = centerline[i] - normal * half;
            Vector2 right = centerline[i] + normal * half;
            verts.Add(new Vector3(left.x, left.y, 0f));
            verts.Add(new Vector3(right.x, right.y, 0f));
            if (i > 0) cumulative += Vector2.Distance(centerline[i], centerline[i - 1]);
            uvs.Add(new Vector2(0f, cumulative * uvScale));
            uvs.Add(new Vector2(1f, cumulative * uvScale));
            if (i > 0)
            {
                int a = (i - 1) * 2;
                int bIdx = i * 2;
                tris.Add(a); tris.Add(bIdx); tris.Add(bIdx + 1);
                tris.Add(a); tris.Add(bIdx + 1); tris.Add(a + 1);
            }
        }
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float AnchorEdgeBias(TrackEnvironment.LateralAnchor anchor, float trackWidth)
    {
        switch (anchor)
        {
            case TrackEnvironment.LateralAnchor.LeftEdge: return -trackWidth * 0.5f;
            case TrackEnvironment.LateralAnchor.RightEdge: return trackWidth * 0.5f;
            default: return 0f;
        }
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    static bool IsFinishLine(string label) =>
        !string.IsNullOrEmpty(label) && label.ToLowerInvariant().Contains("finish");

    // Same trick as the finish line, on the pit spline: a strip labelled "PitExitLine" is anchored to
    // TrackInfoV2.PitExitLineDistance so the painted line and PitLimiter's release point can never drift apart.
    static bool IsPitExitLine(string label) =>
        !string.IsNullOrEmpty(label) && label.ToLowerInvariant().Replace(" ", "").Contains("pitexit");

    void BuildStrips(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (environment.strips == null || environment.strips.Length == 0) return;
        var stripsRoot = new GameObject("Strips");
        stripsRoot.transform.SetParent(transform, false);

        float spacing = Mathf.Max(0.25f, environment.stripSampleSpacing);
        for (int s = 0; s < environment.strips.Length; s++)
        {
            var strip = environment.strips[s];
            var lookup = strip.useSpline == TrackEnvironment.SplineRef.Pit ? pitSamples : mainSamples;
            if (lookup == null || lookup.Count < 2) continue;

            // Resolve the segment-anchored span (segment index + metres within it, like barrier gaps)
            // to absolute spline distances.
            var segs = strip.useSpline == TrackEnvironment.SplineRef.Pit
                ? (track.track != null ? track.track.pitSegments : null)
                : (track.track != null ? track.track.segments : null);
            if (segs == null || segs.Length == 0) continue;
            float startAbs = ResolveSegmentDistance(segs, strip.startSegmentIndex, strip.startDistance);
            float endAbs = ResolveSegmentDistance(segs, strip.endSegmentIndex, strip.endDistance);

            // The finish-line strip is anchored to the track's start/finish line, so it honours
            // TrackInfoV2.startFinishDistance instead of a hand-typed span (its band length is kept).
            if (track.track != null && IsFinishLine(strip.label))
            {
                float band = endAbs - startAbs;
                startAbs = track.track.startFinishDistance;
                endAbs = startAbs + (band > 0.01f ? band : 1f);
            }
            // The pit exit line lands on the pit spline at the limiter's release distance. Anchored at its END
            // so the whole painted band sits BEFORE the release point — cross the paint, you're free.
            else if (track.track != null && IsPitExitLine(strip.label) &&
                     strip.useSpline == TrackEnvironment.SplineRef.Pit)
            {
                float band = endAbs - startAbs;
                if (band < 0.01f) band = 1f;
                endAbs = track.track.PitExitLineDistance;
                startAbs = Mathf.Max(0f, endAbs - band);
            }

            if (endAbs <= startAbs) continue;

            var go = new GameObject(string.IsNullOrEmpty(strip.label) ? $"Strip_{s}" : strip.label);
            go.transform.SetParent(stripsRoot.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sortingOrder = strip.sortingOrder;
            if (strip.material != null) mr.sharedMaterial = strip.material;
            mf.sharedMesh = BuildStripMesh(lookup, strip, spacing, startAbs, endAbs);
        }
    }

    // Absolute spline distance of (segment index, metres past that segment's start). The within-segment
    // distance is clamped to the segment's length, mirroring barrier-gap behaviour.
    static float ResolveSegmentDistance(TrackInfoV2.TrackSegment[] segs, int segmentIndex, float distanceInSegment)
    {
        int idx = Mathf.Clamp(segmentIndex, 0, segs.Length - 1);
        float start = 0f;
        for (int i = 0; i < idx; i++) start += segs[i].length;
        return start + Mathf.Clamp(distanceInSegment, 0f, segs[idx].length);
    }

    Mesh BuildStripMesh(List<TrackBuilder.Sample> samples, TrackEnvironment.Strip strip, float spacing,
                        float startAbs, float endAbs)
    {
        var mesh = new Mesh { name = $"Strip_{startAbs}_{endAbs}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float length = endAbs - startAbs;
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        float uvScale = strip.uvLengthScale > 0f ? strip.uvLengthScale : 1f;

        bool usePit = strip.useSpline == TrackEnvironment.SplineRef.Pit;
        // U runs left→right vert (−normal → +normal). On the RIGHT edge that reads track→outside; on the
        // LEFT edge it reads outside→track, so an asymmetric texture (kerb profile) points INTO the track.
        // Mirror U for left-anchored strips so the texture always reads track-side → outside on both edges.
        bool mirrorU = strip.anchor == TrackEnvironment.LateralAnchor.LeftEdge;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            float d = startAbs + length * t;
            var sample = usePit ? track.SamplePitAt(d, samples) : track.SampleAt(d, samples);
            float edgeBias = AnchorEdgeBias(strip.anchor, sample.width);
            Vector2 center = sample.position + sample.normal * (edgeBias + strip.lateralOffset);
            Vector2 left = center - sample.normal * (strip.width * 0.5f);
            Vector2 right = center + sample.normal * (strip.width * 0.5f);
            verts.Add(new Vector3(left.x, left.y, 0));
            verts.Add(new Vector3(right.x, right.y, 0));
            uvs.Add(new Vector2(mirrorU ? 1f : 0f, length * t * uvScale));
            uvs.Add(new Vector2(mirrorU ? 0f : 1f, length * t * uvScale));

            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void BuildDecorations(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (environment.decorations == null || environment.decorations.Length == 0) return;
        var decorationsRoot = new GameObject("Decorations");
        decorationsRoot.transform.SetParent(transform, false);

        for (int i = 0; i < environment.decorations.Length; i++)
        {
            var deco = environment.decorations[i];
            if (deco.prefab == null) continue;

            bool usePit = deco.useSpline == TrackEnvironment.SplineRef.Pit;
            var lookup = usePit ? pitSamples : mainSamples;
            if (lookup == null || lookup.Count < 2) continue;
            var sample = usePit ? track.SamplePitAt(deco.distance, lookup) : track.SampleAt(deco.distance, lookup);
            float edgeBias = AnchorEdgeBias(deco.anchor, sample.width);
            Vector2 pos = sample.position + sample.normal * (edgeBias + deco.lateralOffset);
            float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg + deco.rotationOffset;

            GameObject instance;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(deco.prefab);
            else
                instance = Instantiate(deco.prefab);
#else
            instance = Instantiate(deco.prefab);
#endif
            instance.name = string.IsNullOrEmpty(deco.label) ? deco.prefab.name : deco.label;
            instance.transform.SetParent(decorationsRoot.transform, false);
            instance.transform.position = new Vector3(pos.x, pos.y, 0);
            instance.transform.rotation = Quaternion.Euler(0, 0, angleDeg);
            if (deco.scale.sqrMagnitude > 0f)
                instance.transform.localScale = new Vector3(deco.scale.x, deco.scale.y, 1f);
        }
    }

#if UNITY_EDITOR
    // Mark each barrier gap along the affected edge(s) so openings can be placed without entering play mode.
    void OnDrawGizmosSelected()
    {
        if (track == null || track.track == null || environment == null || environment.barrierGaps == null) return;
        var samples = track.SampleCenterline();
        if (samples == null || samples.Count < 2) return;
        var segs = track.track.segments;
        if (segs == null || segs.Length == 0) return;

        for (int i = 0; i < environment.barrierGaps.Length; i++)
        {
            var gap = environment.barrierGaps[i];
            if (gap.segmentIndex < 0 || gap.segmentIndex >= segs.Length) continue;
            float segStart = 0f;
            for (int s = 0; s < gap.segmentIndex; s++) segStart += segs[s].length;
            float segLen = segs[gap.segmentIndex].length;
            float local0 = Mathf.Clamp(Mathf.Min(gap.startDistance, gap.endDistance), 0f, segLen);
            float local1 = Mathf.Clamp(Mathf.Max(gap.startDistance, gap.endDistance), 0f, segLen);
            if (local1 - local0 < 1e-4f) continue;
            DrawGapMarker(gap.side, segStart + local0, segStart + local1, samples);
        }
    }

    void DrawGapMarker(TrackEnvironment.BarrierSide side, float gs, float ge, List<TrackBuilder.Sample> samples)
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 1f);
        int steps = Mathf.Max(2, Mathf.CeilToInt((ge - gs) / 2f) + 1);
        Vector3 prev = Vector3.zero;
        for (int i = 0; i < steps; i++)
        {
            float d = Mathf.Lerp(gs, ge, i / (float)(steps - 1));
            Vector2 p = EdgePoint(side, d, samples);
            Vector3 w = transform.TransformPoint(new Vector3(p.x, p.y, 0f));
            if (i == 0 || i == steps - 1) Gizmos.DrawWireSphere(w, 1.5f);
            if (i > 0) Gizmos.DrawLine(prev, w);
            prev = w;
        }
    }

    // One-shot migration: strips used to store ABSOLUTE spline distances; they are now segment-anchored
    // (segment index + metres within it, like barrier gaps). Converts every legacy strip on the open
    // scene's TrackEnvironment in place. Only touches strips whose segment indices are both still 0
    // (the legacy default), so re-running is harmless.
    [UnityEditor.MenuItem("Tools/Draftmaster/Migrate Strip Distances")]
    static void MigrateStripDistances()
    {
        var builder = FindObjectOfType<TrackEnvironmentBuilder>();
        if (builder == null || builder.environment == null || builder.track == null || builder.track.track == null)
        {
            Debug.LogError("Migrate Strips: need a TrackEnvironmentBuilder with environment + track assigned in the open scene.");
            return;
        }

        var env = builder.environment;
        if (env.strips == null || env.strips.Length == 0) { Debug.Log("Migrate Strips: no strips."); return; }

        int migrated = 0;
        for (int i = 0; i < env.strips.Length; i++)
        {
            var strip = env.strips[i];
            if (strip.startSegmentIndex != 0 || strip.endSegmentIndex != 0) continue; // already segment-anchored

            var segs = strip.useSpline == TrackEnvironment.SplineRef.Pit
                ? builder.track.track.pitSegments : builder.track.track.segments;
            if (segs == null || segs.Length == 0) continue;

            (strip.startSegmentIndex, strip.startDistance) = AbsoluteToSegment(segs, strip.startDistance);
            (strip.endSegmentIndex, strip.endDistance) = AbsoluteToSegment(segs, strip.endDistance);
            env.strips[i] = strip;
            migrated++;
        }

        UnityEditor.EditorUtility.SetDirty(env);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Migrate Strips: converted {migrated}/{env.strips.Length} strips on '{env.name}' to segment-anchored spans.", env);
    }

    // Walk the cumulative segment lengths to express an absolute spline distance as (segment, within-segment).
    static (int, float) AbsoluteToSegment(TrackInfoV2.TrackSegment[] segs, float absolute)
    {
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            if (absolute < cum + segs[i].length || i == segs.Length - 1)
                return (i, Mathf.Clamp(absolute - cum, 0f, segs[i].length));
            cum += segs[i].length;
        }
        return (0, absolute);
    }
#endif
}
