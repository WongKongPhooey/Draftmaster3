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

        BuildStrips(samples, pitSamples);
        BuildBarriers(samples, pitSamples);
        BuildDecorations(samples, pitSamples);
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

        for (int i = 0; i < segs.Length; i++)
        {
            float dStart = segStart[i];
            float dEnd = dStart + segs[i].length;
            BuildBarrierSide(root.transform, i, TrackEnvironment.BarrierSide.Inner, dStart, dEnd, mainSamples, spacing);
            BuildBarrierSide(root.transform, i, TrackEnvironment.BarrierSide.Outer, dStart, dEnd, mainSamples, spacing);
        }
    }

    // Build one segment's barrier for a side, split into pieces with any overlapping gap ranges cut out.
    // Auto sections sample the track edge per piece; manual (hand-drawn) sections slice their polyline by arc-length
    // so gaps work on pit walls / service-road sections too.
    void BuildBarrierSide(Transform root, int segIndex, TrackEnvironment.BarrierSide side,
        float dStart, float dEnd, List<TrackBuilder.Sample> mainSamples, float spacing)
    {
        int manualIdx = FindManualSection(segIndex, side);
        List<Vector2> manualFull = manualIdx >= 0
            ? BuildManualCenterline(manualIdx, side, dStart, dEnd, mainSamples)
            : null;
        float len = Mathf.Max(dEnd - dStart, 1e-4f);

        var spans = SubtractGaps(segIndex, dStart, dEnd, side);
        for (int k = 0; k < spans.Count; k++)
        {
            float a = spans[k].x, b = spans[k].y;
            if (b - a < 0.05f) continue; // skip slivers left by a gap touching a segment boundary

            List<Vector2> centerline = manualFull != null
                ? SlicePolylineByFraction(manualFull, (a - dStart) / len, (b - dStart) / len)
                : BuildAutoEdgeCenterline(side, a, b, mainSamples, spacing);

            BuildBarrierPieceMesh(root, segIndex, side, k, centerline);
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
            if (ge - gs < 1e-4f) continue;

            var next = new List<Vector2>();
            for (int k = 0; k < spans.Count; k++)
            {
                float a = spans[k].x, b = spans[k].y;
                if (ge <= a || gs >= b) { next.Add(spans[k]); continue; } // no overlap
                if (gs > a) next.Add(new Vector2(a, gs));                  // left remainder
                if (ge < b) next.Add(new Vector2(ge, b));                  // right remainder
            }
            spans = next;
        }
        return spans;
    }

    void BuildBarrierPieceMesh(Transform root, int segIndex, TrackEnvironment.BarrierSide side,
        int pieceIndex, List<Vector2> centerline)
    {
        if (centerline == null || centerline.Count < 2) return;

        var go = new GameObject($"Barrier_{side}_{segIndex}_{pieceIndex}");
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

    // Extract the sub-polyline between two arc-length fractions [f0, f1] of the input, interpolating new endpoints.
    // Maps a main-spline distance gap onto a hand-drawn barrier proportionally by its own length.
    static List<Vector2> SlicePolylineByFraction(List<Vector2> pts, float f0, float f1)
    {
        var result = new List<Vector2>();
        if (pts == null || pts.Count < 2) return result;
        f0 = Mathf.Clamp01(f0); f1 = Mathf.Clamp01(f1);
        if (f1 - f0 < 1e-4f) return result;

        float total = 0f;
        for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
        if (total < 1e-4f) return result;

        float dStart = f0 * total;
        float dEnd = f1 * total;

        float cum = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            float segLen = Vector2.Distance(pts[i - 1], pts[i]);
            if (segLen < 1e-6f) continue;
            float segStart = cum;
            float segEnd = cum + segLen;

            if (segEnd >= dStart && segStart <= dEnd) // segment overlaps the kept range
            {
                if (result.Count == 0)
                    result.Add(Vector2.Lerp(pts[i - 1], pts[i], Mathf.Clamp01((dStart - segStart) / segLen)));

                if (segEnd <= dEnd)
                {
                    result.Add(pts[i]);
                }
                else
                {
                    result.Add(Vector2.Lerp(pts[i - 1], pts[i], Mathf.Clamp01((dEnd - segStart) / segLen)));
                    break;
                }
            }
            cum = segEnd;
        }
        return result;
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

    int FindManualSection(int segIndex, TrackEnvironment.BarrierSide side)
    {
        if (environment.manualSections == null) return -1;
        for (int i = 0; i < environment.manualSections.Length; i++)
            if (environment.manualSections[i].segmentIndex == segIndex && environment.manualSections[i].side == side)
                return i;
        return -1;
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

    // Manual: seed endpoints at the segment boundaries (so it connects to neighbour barriers), then user points between.
    List<Vector2> BuildManualCenterline(int manualIdx, TrackEnvironment.BarrierSide side, float dStart, float dEnd,
        List<TrackBuilder.Sample> mainSamples)
    {
        var section = environment.manualSections[manualIdx];
        var list = new List<Vector2>();
        Vector2 startAnchor = EdgePoint(side, dStart, mainSamples);
        Vector2 endAnchor = EdgePoint(side, dEnd, mainSamples);
        list.Add(startAnchor);
        if (section.manualPoints != null) list.AddRange(section.manualPoints);
        list.Add(endAnchor);
        return list;
    }

    Vector2 EdgePoint(TrackEnvironment.BarrierSide side, float distance, List<TrackBuilder.Sample> mainSamples)
    {
        var sample = track.SampleAt(distance, mainSamples);
        // normal = right of travel. Inner = right edge + innerOffset (outboard right). Outer = left edge - outerOffset (outboard left).
        if (side == TrackEnvironment.BarrierSide.Inner)
            return sample.position + sample.normal * (sample.width * 0.5f + environment.innerEdgeOffset);
        return sample.position - sample.normal * (sample.width * 0.5f + environment.outerEdgeOffset);
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

    void BuildStrips(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (environment.strips == null || environment.strips.Length == 0) return;
        var stripsRoot = new GameObject("Strips");
        stripsRoot.transform.SetParent(transform, false);

        float spacing = Mathf.Max(0.25f, environment.stripSampleSpacing);
        for (int s = 0; s < environment.strips.Length; s++)
        {
            var strip = environment.strips[s];
            if (strip.endDistance <= strip.startDistance) continue;
            var lookup = strip.useSpline == TrackEnvironment.SplineRef.Pit ? pitSamples : mainSamples;
            if (lookup == null || lookup.Count < 2) continue;

            var go = new GameObject(string.IsNullOrEmpty(strip.label) ? $"Strip_{s}" : strip.label);
            go.transform.SetParent(stripsRoot.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sortingOrder = strip.sortingOrder;
            if (strip.material != null) mr.sharedMaterial = strip.material;
            mf.sharedMesh = BuildStripMesh(lookup, strip, spacing);
        }
    }

    Mesh BuildStripMesh(List<TrackBuilder.Sample> samples, TrackEnvironment.Strip strip, float spacing)
    {
        var mesh = new Mesh { name = $"Strip_{strip.startDistance}_{strip.endDistance}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float length = strip.endDistance - strip.startDistance;
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        float uvScale = strip.uvLengthScale > 0f ? strip.uvLengthScale : 1f;

        bool usePit = strip.useSpline == TrackEnvironment.SplineRef.Pit;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            float d = strip.startDistance + length * t;
            var sample = usePit ? track.SamplePitAt(d, samples) : track.SampleAt(d, samples);
            float edgeBias = AnchorEdgeBias(strip.anchor, sample.width);
            Vector2 center = sample.position + sample.normal * (edgeBias + strip.lateralOffset);
            Vector2 left = center - sample.normal * (strip.width * 0.5f);
            Vector2 right = center + sample.normal * (strip.width * 0.5f);
            verts.Add(new Vector3(left.x, left.y, 0));
            verts.Add(new Vector3(right.x, right.y, 0));
            uvs.Add(new Vector2(0f, length * t * uvScale));
            uvs.Add(new Vector2(1f, length * t * uvScale));

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
#endif
}
