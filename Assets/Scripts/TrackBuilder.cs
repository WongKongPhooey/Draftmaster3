using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackBuilder : MonoBehaviour
{
    public TrackInfoV2 track;
    public Material surfaceMaterial;
    public bool drawGizmos = true;
    public bool rebuildOnValidate = true;

    Mesh _mesh;

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

        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (surfaceMaterial != null) mr.sharedMaterial = surfaceMaterial;

        var samples = SampleCenterline();
        if (samples.Count < 2)
        {
            mf.sharedMesh = null;
            return;
        }

        _mesh = new Mesh { name = $"Track_{track.name}" };
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>(samples.Count * 2);
        var uvs = new List<Vector2>(samples.Count * 2);
        var tris = new List<int>(samples.Count * 6);

        float distance = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            verts.Add(new Vector3(s.position.x, s.position.y, 0) - right * (s.width * 0.5f));
            verts.Add(new Vector3(s.position.x, s.position.y, 0) + right * (s.width * 0.5f));

            uvs.Add(new Vector2(0f, distance * 0.1f));
            uvs.Add(new Vector2(1f, distance * 0.1f));

            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);

                distance += Vector2.Distance(samples[i - 1].position, s.position);
            }
        }

        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0);
        _mesh.SetUVs(0, uvs);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        mf.sharedMesh = _mesh;
    }

    public struct Sample
    {
        public Vector2 position;
        public Vector2 tangent;
        public Vector2 normal;
        public float width;
        public float distance;
    }

    public List<Sample> SampleCenterline()
    {
        var samples = new List<Sample>();
        if (track == null || track.segments == null || track.segments.Length == 0) return samples;

        Vector2 pos = track.startPosition;
        float headingDeg = track.startHeading;
        int spp = Mathf.Max(1, track.samplesPerSegment);
        float cumulativeDistance = 0f;

        EmitSample(samples, pos, headingDeg, GetSegmentWidth(track.segments[0]), cumulativeDistance);

        for (int segIndex = 0; segIndex < track.segments.Length; segIndex++)
        {
            var seg = track.segments[segIndex];
            if (seg.length <= 0f) continue;

            float width = GetSegmentWidth(seg);

            if (seg.type == TrackInfoV2.SegmentType.Straight)
            {
                Vector2 dir = HeadingToDir(headingDeg);
                Vector2 startPos = pos;
                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    Vector2 p = startPos + dir * seg.length * t;
                    float d = cumulativeDistance + seg.length * t;
                    EmitSample(samples, p, headingDeg, width, d);
                }
                pos = startPos + dir * seg.length;
                cumulativeDistance += seg.length;
            }
            else // Turn
            {
                float angleRad = seg.angle * Mathf.Deg2Rad;
                if (Mathf.Abs(angleRad) < 0.0001f)
                {
                    // Degenerate turn — treat as straight.
                    Vector2 dir = HeadingToDir(headingDeg);
                    pos += dir * seg.length;
                    cumulativeDistance += seg.length;
                    EmitSample(samples, pos, headingDeg, width, cumulativeDistance);
                    continue;
                }

                float radius = seg.length / Mathf.Abs(angleRad);
                Vector2 forward = HeadingToDir(headingDeg);
                // Centre of arc: 90° to the left if turning left (positive angle), to the right if turning right.
                Vector2 toCentre = (seg.angle >= 0f)
                    ? new Vector2(-forward.y, forward.x)
                    : new Vector2(forward.y, -forward.x);
                Vector2 centre = pos + toCentre * radius;

                // Vector from centre to current position
                Vector2 startRadial = pos - centre;
                float startAngle = Mathf.Atan2(startRadial.y, startRadial.x);

                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    float a = startAngle + angleRad * t;
                    Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    float headingHere = headingDeg + seg.angle * t;
                    float d = cumulativeDistance + seg.length * t;
                    EmitSample(samples, p, headingHere, width, d);
                }

                pos = centre + new Vector2(Mathf.Cos(startAngle + angleRad), Mathf.Sin(startAngle + angleRad)) * radius;
                headingDeg += seg.angle;
                cumulativeDistance += seg.length;
            }
        }

        if (track.closedLoop && samples.Count > 1)
        {
            // Add a closing sample at the start position so the mesh visually closes the loop.
            var first = samples[0];
            samples.Add(first);
        }

        return samples;
    }

    float GetSegmentWidth(TrackInfoV2.TrackSegment seg)
    {
        return seg.width <= 0f ? track.defaultWidth : seg.width;
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

    void OnDrawGizmos()
    {
        if (!drawGizmos || track == null) return;
        var samples = SampleCenterline();
        if (samples.Count < 2) return;

        // Centerline
        Gizmos.color = Color.cyan;
        for (int i = 1; i < samples.Count; i++)
        {
            Vector3 a = transform.TransformPoint(new Vector3(samples[i - 1].position.x, samples[i - 1].position.y, 0));
            Vector3 b = transform.TransformPoint(new Vector3(samples[i].position.x, samples[i].position.y, 0));
            Gizmos.DrawLine(a, b);
        }

        // Segment-boundary markers
        Gizmos.color = Color.yellow;
        int spp = Mathf.Max(1, track.samplesPerSegment);
        for (int segIndex = 0; segIndex < track.segments.Length; segIndex++)
        {
            int idx = (segIndex + 1) * spp;
            if (idx >= samples.Count) idx = samples.Count - 1;
            Vector3 p = transform.TransformPoint(new Vector3(samples[idx].position.x, samples[idx].position.y, 0));
            Gizmos.DrawSphere(p, 2f);
        }

        // Start line
        Gizmos.color = Color.green;
        Vector3 sp = transform.TransformPoint(new Vector3(track.startPosition.x, track.startPosition.y, 0));
        Gizmos.DrawCube(sp, Vector3.one * 4f);
    }
}
