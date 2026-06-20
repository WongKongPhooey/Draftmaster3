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

    static bool IsFinishLine(string label) =>
        !string.IsNullOrEmpty(label) && label.ToLowerInvariant().Contains("finish");

    void BuildStrips(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (environment.strips == null || environment.strips.Length == 0) return;
        var stripsRoot = new GameObject("Strips");
        stripsRoot.transform.SetParent(transform, false);

        float spacing = Mathf.Max(0.25f, environment.stripSampleSpacing);
        for (int s = 0; s < environment.strips.Length; s++)
        {
            var strip = environment.strips[s];

            // The finish-line strip is anchored to the track's start/finish line, so it honours
            // TrackInfoV2.startFinishDistance instead of a hand-typed distance (its band length is kept).
            if (track.track != null && IsFinishLine(strip.label))
            {
                float band = strip.endDistance - strip.startDistance;
                strip.startDistance = track.track.startFinishDistance;
                strip.endDistance = strip.startDistance + (band > 0.01f ? band : 1f);
            }

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
