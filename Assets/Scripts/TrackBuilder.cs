using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackBuilder : MonoBehaviour
{
    public TrackInfoV2 track;
    public Material surfaceMaterial;
    public Material pitSurfaceMaterial;
    public bool drawGizmos = true;
    public bool rebuildOnValidate = true;

    [Header("Racing Line Gizmo")]
    public bool drawRacingLineGizmo = true;
    public Color idealLineColor = new Color(0.2f, 1f, 0.3f, 1f);
    public Color leftLineColor = new Color(0.3f, 0.5f, 1f, 0.7f);
    public Color rightLineColor = new Color(1f, 0.3f, 0.3f, 0.7f);
    public bool drawLeftRightBounds = true;
    public float anchorMarkerRadius = 1.2f;

    Mesh _mainMesh;
    Mesh _pitMesh;
    GameObject _pitChild;
    List<Sample> _surfaceCache;

    public struct Sample
    {
        public Vector2 position;
        public Vector2 tangent;
        public Vector2 normal;
        public float width;
        public float distance;
    }

    void OnEnable() { Build(); }

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
        if (track == null || track.segments == null || track.segments.Length == 0) return;
        _surfaceCache = null; // invalidate on-surface lookup; rebuilt lazily on next query

        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (surfaceMaterial != null) mr.sharedMaterial = surfaceMaterial;

        var mainSamples = SampleCenterline();
        _mainMesh = BuildRibbonMesh(mainSamples, track.closedLoop, $"Track_{track.name}");
        mf.sharedMesh = _mainMesh;

        BuildEdgeLines(mainSamples);
        BuildPitLane();
    }

    void BuildEdgeLines(List<Sample> samples)
    {
        TearDownChild("LeftEdgeLine");
        TearDownChild("RightEdgeLine");

        if (!track.drawEdgeLines || track.edgeLineMaterial == null) return;
        if (samples == null || samples.Count < 2) return;
        if (track.drawLeftEdgeLine) BuildSingleEdgeLine("LeftEdgeLine", samples, -1f);
        if (track.drawRightEdgeLine) BuildSingleEdgeLine("RightEdgeLine", samples, 1f);
    }

    void BuildSingleEdgeLine(string childName, List<Sample> samples, float edgeSign)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = track.edgeLineMaterial;
        mr.sortingOrder = track.edgeLineSortingOrder;

        var meshSamples = samples;
        if (track.closedLoop)
        {
            meshSamples = new List<Sample>(samples.Count + 1);
            meshSamples.AddRange(samples);
            meshSamples.Add(samples[0]);
        }

        var mesh = new Mesh { name = $"EdgeLine_{childName}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var verts = new List<Vector3>(meshSamples.Count * 2);
        var uvs = new List<Vector2>(meshSamples.Count * 2);
        var tris = new List<int>(meshSamples.Count * 6);
        float distance = 0f;
        float halfLine = track.edgeLineWidth * 0.5f;

        for (int i = 0; i < meshSamples.Count; i++)
        {
            var s = meshSamples[i];
            float centerLateral = edgeSign * (s.width * 0.5f - track.edgeLineInset);
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            Vector3 baseP = new Vector3(s.position.x, s.position.y, 0);
            Vector3 lineCenter = baseP + right * centerLateral;
            verts.Add(lineCenter - right * halfLine);
            verts.Add(lineCenter + right * halfLine);
            uvs.Add(new Vector2(0f, distance));
            uvs.Add(new Vector2(1f, distance));
            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);
                distance += Vector2.Distance(meshSamples[i - 1].position, s.position);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

    void TearDownChild(string childName)
    {
        var existing = transform.Find(childName);
        if (existing == null) return;
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    void BuildPitLane()
    {
        // Tear down any previous pit child first.
        var existing = transform.Find("PitLane");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        _pitChild = null;
        _pitMesh = null;

        if (!track.hasPitLane || track.pitSegments == null || track.pitSegments.Length == 0) return;

        var pitSamples = SamplePitCenterline();
        if (pitSamples.Count < 2) return;

        _pitChild = new GameObject("PitLane");
        _pitChild.transform.SetParent(transform, false);
        var mf = _pitChild.AddComponent<MeshFilter>();
        var mr = _pitChild.AddComponent<MeshRenderer>();
        mr.sharedMaterial = pitSurfaceMaterial != null ? pitSurfaceMaterial : surfaceMaterial;
        _pitMesh = BuildRibbonMesh(pitSamples, false, $"Pit_{track.name}");
        mf.sharedMesh = _pitMesh;
    }

    Mesh BuildRibbonMesh(List<Sample> samples, bool closedLoop, string name)
    {
        if (samples == null || samples.Count < 2) return null;

        var meshSamples = samples;
        if (closedLoop)
        {
            meshSamples = new List<Sample>(samples.Count + 1);
            meshSamples.AddRange(samples);
            meshSamples.Add(samples[0]);
        }

        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>(meshSamples.Count * 2);
        var uvs = new List<Vector2>(meshSamples.Count * 2);
        var tris = new List<int>(meshSamples.Count * 6);

        float distance = 0f;
        for (int i = 0; i < meshSamples.Count; i++)
        {
            var s = meshSamples[i];
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            verts.Add(new Vector3(s.position.x, s.position.y, 0) - right * (s.width * 0.5f));
            verts.Add(new Vector3(s.position.x, s.position.y, 0) + right * (s.width * 0.5f));
            uvs.Add(new Vector2(0f, distance));
            uvs.Add(new Vector2(1f, distance));

            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);
                distance += Vector2.Distance(meshSamples[i - 1].position, s.position);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public List<Sample> SampleCenterline()
    {
        return SampleSegments(
            track.startPosition,
            track.startHeading,
            track.segments,
            track.defaultWidth,
            Mathf.Max(1, track.samplesPerSegment),
            Mathf.Max(0.1f, track.maxArcStepMetres),
            track.closedLoop,
            seg => seg.width <= 0f ? track.defaultWidth : seg.width);
    }

    public List<Sample> SamplePitCenterline()
    {
        if (track == null || !track.hasPitLane || track.pitSegments == null) return new List<Sample>();
        float pitWidth = track.pitDefaultWidth > 0f ? track.pitDefaultWidth : track.defaultWidth;

        // Pit start locked to the pit entry node on the main spline. Use authored-segment walker (skips seam closure samples).
        track.SampleAuthoredSpline(track.pitEntryDistance, out Vector2 pitStartPos, out float pitStartHeading);
        pitStartHeading += track.pitStartHeadingOffset;
        track.pitStartPosition = pitStartPos;
        track.pitStartHeading = pitStartHeading;

        return SampleSegments(
            pitStartPos,
            pitStartHeading,
            track.pitSegments,
            pitWidth,
            Mathf.Max(1, track.samplesPerSegment),
            Mathf.Max(0.1f, track.maxArcStepMetres),
            false,
            seg => seg.width <= 0f ? pitWidth : seg.width);
    }

    public Sample SampleAt(float distance, List<Sample> samples = null)
    {
        if (samples == null) samples = SampleCenterline();
        return SampleListAt(samples, distance, track != null && track.closedLoop);
    }

    public Sample SamplePitAt(float distance, List<Sample> samples = null)
    {
        if (samples == null) samples = SamplePitCenterline();
        return SampleListAt(samples, distance, false);
    }

    // True if worldPos sits over the drivable track surface. Outputs |lateral| offset from the centreline (m).
    // Uses a cached centerline (invalidated on Build); cheap enough to call per-FixedUpdate for one car.
    public bool IsOnSurface(Vector3 worldPos, out float lateralAbs)
    {
        lateralAbs = 0f;
        if (track == null) return true;
        if (_surfaceCache == null || _surfaceCache.Count < 2) _surfaceCache = SampleCenterline();
        if (_surfaceCache.Count < 2) return true;

        Vector2 local = transform.InverseTransformPoint(worldPos);
        float best = float.MaxValue;
        int bi = 0;
        for (int i = 0; i < _surfaceCache.Count; i++)
        {
            float d = ((Vector2)_surfaceCache[i].position - local).sqrMagnitude;
            if (d < best) { best = d; bi = i; }
        }
        var s = _surfaceCache[bi];
        lateralAbs = Mathf.Abs(Vector2.Dot(local - s.position, s.normal));
        return lateralAbs <= s.width * 0.5f;
    }

    static Sample SampleListAt(List<Sample> samples, float distance, bool loop)
    {
        if (samples.Count == 0) return default;
        if (samples.Count == 1) return samples[0];

        float total = samples[samples.Count - 1].distance;
        if (loop && total > 0f) distance = ((distance % total) + total) % total;

        int lo = 0, hi = samples.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (samples[mid].distance <= distance) lo = mid;
            else hi = mid;
        }
        var a = samples[lo];
        var b = samples[hi];
        float denom = b.distance - a.distance;
        float t = denom > 0f ? Mathf.Clamp01((distance - a.distance) / denom) : 0f;
        Vector2 tan = Vector2.Lerp(a.tangent, b.tangent, t);
        if (tan.sqrMagnitude > 0) tan.Normalize();
        else tan = a.tangent;
        Vector2 nrm = new Vector2(tan.y, -tan.x);
        return new Sample
        {
            position = Vector2.Lerp(a.position, b.position, t),
            tangent = tan,
            normal = nrm,
            width = Mathf.Lerp(a.width, b.width, t),
            distance = distance
        };
    }

    static List<Sample> SampleSegments(
        Vector2 startPos,
        float startHeading,
        TrackInfoV2.TrackSegment[] segments,
        float defaultWidth,
        int minSpp,
        float step,
        bool closedLoop,
        System.Func<TrackInfoV2.TrackSegment, float> widthFn)
    {
        var samples = new List<Sample>();
        if (segments == null || segments.Length == 0) return samples;

        Vector2 pos = startPos;
        float headingDeg = startHeading;
        float cumulativeDistance = 0f;

        EmitSample(samples, pos, headingDeg, widthFn(segments[0]), cumulativeDistance);

        for (int segIndex = 0; segIndex < segments.Length; segIndex++)
        {
            var seg = segments[segIndex];
            if (seg.length <= 0f) continue;

            TrackInfoV2.TrackSegment nextSeg;
            if (segIndex < segments.Length - 1) nextSeg = segments[segIndex + 1];
            else if (closedLoop) nextSeg = segments[0];
            else nextSeg = seg;
            float wA = widthFn(seg);
            float wB = widthFn(nextSeg);
            int spp = Mathf.Max(minSpp, Mathf.CeilToInt(seg.length / step));

            if (seg.type == TrackInfoV2.SegmentType.Straight)
            {
                Vector2 dir = HeadingToDir(headingDeg);
                Vector2 sPos = pos;
                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    Vector2 p = sPos + dir * seg.length * t;
                    float w = Mathf.Lerp(wA, wB, t);
                    float d = cumulativeDistance + seg.length * t;
                    EmitSample(samples, p, headingDeg, w, d);
                }
                pos = sPos + dir * seg.length;
                cumulativeDistance += seg.length;
            }
            else // Turn
            {
                float angleRad = seg.angle * Mathf.Deg2Rad;
                if (Mathf.Abs(angleRad) < 0.0001f)
                {
                    Vector2 dir = HeadingToDir(headingDeg);
                    pos += dir * seg.length;
                    cumulativeDistance += seg.length;
                    EmitSample(samples, pos, headingDeg, wB, cumulativeDistance);
                    continue;
                }

                float radius = seg.length / Mathf.Abs(angleRad);
                Vector2 forward = HeadingToDir(headingDeg);
                Vector2 toCentre = (seg.angle >= 0f)
                    ? new Vector2(-forward.y, forward.x)
                    : new Vector2(forward.y, -forward.x);
                Vector2 centre = pos + toCentre * radius;

                Vector2 startRadial = pos - centre;
                float startAngle = Mathf.Atan2(startRadial.y, startRadial.x);

                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    float a = startAngle + angleRad * t;
                    Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    float headingHere = headingDeg + seg.angle * t;
                    float w = Mathf.Lerp(wA, wB, t);
                    float d = cumulativeDistance + seg.length * t;
                    EmitSample(samples, p, headingHere, w, d);
                }

                pos = centre + new Vector2(Mathf.Cos(startAngle + angleRad), Mathf.Sin(startAngle + angleRad)) * radius;
                headingDeg += seg.angle;
                cumulativeDistance += seg.length;
            }
        }

        // Stitch the loop seam: if closed, add samples interpolating final pos back to startPos so the car travels
        // continuously across the join instead of teleporting. Authoring imprecision in segment sums lands here.
        if (closedLoop)
        {
            float closeDist = Vector2.Distance(pos, startPos);
            if (closeDist > 0.01f)
            {
                int spp = Mathf.Max(minSpp, Mathf.CeilToInt(closeDist / step));
                Vector2 dir = (startPos - pos) / closeDist;
                float closeHeading = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float wA = widthFn(segments[segments.Length - 1]);
                float wB = widthFn(segments[0]);
                Vector2 sPos = pos;
                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    Vector2 p = sPos + dir * (closeDist * t);
                    float headingHere = Mathf.LerpAngle(headingDeg, closeHeading, t);
                    float w = Mathf.Lerp(wA, wB, t);
                    float d = cumulativeDistance + closeDist * t;
                    EmitSample(samples, p, headingHere, w, d);
                }
                cumulativeDistance += closeDist;
            }
        }

        return samples;
    }

    static Vector2 HeadingToDir(float headingDeg)
    {
        float r = headingDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    static void EmitSample(List<Sample> samples, Vector2 pos, float headingDeg, float width, float distance)
    {
        Vector2 t = HeadingToDir(headingDeg);
        Vector2 n = new Vector2(t.y, -t.x);
        samples.Add(new Sample
        {
            position = pos,
            tangent = t,
            normal = n,
            width = width,
            distance = distance
        });
    }

    void DrawRacingLineCurve(List<Sample> samples, List<TrackInfoV2.RacingLineAnchor> anchors, float lineFactor, Color color)
    {
        Gizmos.color = color;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            float lateral = track.GetLateralAt(s.distance, lineFactor, anchors);
            Vector2 p = s.position + right * lateral;
            Vector3 w = transform.TransformPoint(new Vector3(p.x, p.y, 0));
            if (i > 0) Gizmos.DrawLine(prev, w);
            prev = w;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || track == null) return;
        var samples = SampleCenterline();
        if (samples.Count < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 1; i < samples.Count; i++)
        {
            Vector3 a = transform.TransformPoint(new Vector3(samples[i - 1].position.x, samples[i - 1].position.y, 0));
            Vector3 b = transform.TransformPoint(new Vector3(samples[i].position.x, samples[i].position.y, 0));
            Gizmos.DrawLine(a, b);
        }

        Gizmos.color = Color.green;
        Vector3 sp = transform.TransformPoint(new Vector3(track.startPosition.x, track.startPosition.y, 0));
        Gizmos.DrawCube(sp, Vector3.one * 4f);

        if (track.hasPitLane)
        {
            var pitSamples = SamplePitCenterline();
            Gizmos.color = new Color(1f, 0.6f, 0f);
            for (int i = 1; i < pitSamples.Count; i++)
            {
                Vector3 a = transform.TransformPoint(new Vector3(pitSamples[i - 1].position.x, pitSamples[i - 1].position.y, 0));
                Vector3 b = transform.TransformPoint(new Vector3(pitSamples[i].position.x, pitSamples[i].position.y, 0));
                Gizmos.DrawLine(a, b);
            }

            // Entry/exit markers on the main spline + connector lines to pit ends
            var entrySample = SampleAt(track.pitEntryDistance, samples);
            var exitSample = SampleAt(track.pitExitDistance, samples);
            Vector3 entry = transform.TransformPoint(new Vector3(entrySample.position.x, entrySample.position.y, 0));
            Vector3 exit = transform.TransformPoint(new Vector3(exitSample.position.x, exitSample.position.y, 0));
            Gizmos.color = new Color(0.2f, 1f, 0.4f);
            Gizmos.DrawWireSphere(entry, 4f);
            Gizmos.color = new Color(1f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(exit, 4f);
            if (pitSamples.Count > 0)
            {
                Vector3 pitStart = transform.TransformPoint(new Vector3(pitSamples[0].position.x, pitSamples[0].position.y, 0));
                Vector3 pitEnd = transform.TransformPoint(new Vector3(pitSamples[pitSamples.Count - 1].position.x, pitSamples[pitSamples.Count - 1].position.y, 0));
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
                Gizmos.DrawLine(entry, pitStart);
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.5f);
                Gizmos.DrawLine(exit, pitEnd);
            }
        }

        if (drawRacingLineGizmo && samples.Count >= 2)
        {
            var anchors = track.BuildRacingLineAnchors();
            if (anchors.Count > 0)
            {
                if (drawLeftRightBounds)
                {
                    DrawRacingLineCurve(samples, anchors, -1f, leftLineColor);
                    DrawRacingLineCurve(samples, anchors, +1f, rightLineColor);
                }
                DrawRacingLineCurve(samples, anchors, 0f, idealLineColor);

                Gizmos.color = idealLineColor;
                for (int i = 0; i < anchors.Count; i++)
                {
                    var anchor = anchors[i];
                    var s = SampleAt(anchor.distance, samples);
                    Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
                    Vector2 p = s.position + right * anchor.ideal;
                    Vector3 w = transform.TransformPoint(new Vector3(p.x, p.y, 0));
                    Gizmos.DrawWireSphere(w, anchorMarkerRadius);
                }
            }
        }
    }
}
