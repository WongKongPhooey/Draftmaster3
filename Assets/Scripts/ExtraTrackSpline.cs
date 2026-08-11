using System.Collections.Generic;
using UnityEngine;

// A free-placed secondary road ribbon. Drop it anywhere in the scene, drag/rotate the GameObject to
// position it, then author Straight/Turn segments in the inspector exactly like the main TrackInfoV2.
// Use for escape roads, runoff tarmac, and the disused splits of alternate track layouts.
//
// The mesh is built in LOCAL space starting at the object's origin, heading +X (plus startHeadingOffset).
// So "free placement" is just the Transform: move it to the branch point, rotate Z to aim it down the road.
//
// Layering: everything in the scene sits on the same z-plane and sorts by MeshRenderer.sortingOrder
// (ground -100, main track 0, its edge lines 1). Keep this object's transform at z=0 and use
// sortingOrder to slot the ribbon UNDER the main track so real tarmac always wins at junctions.
//
// Optional edge lines are built as extra submeshes on the SAME renderer: they draw after (on top of)
// the road submesh but inherit its sortingOrder, so line + road move as one unit through the stack.
// Single-renderer design also keeps Build() free of child-object teardown, which lets OnValidate call
// it directly (no delayCall — that stalls while the editor is unfocused and strands external edits).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ExtraTrackSpline : MonoBehaviour
{
    [Header("Surface")]
    [Tooltip("Tarmac material. Leave null to inherit whatever is already on the MeshRenderer.")]
    public Material surfaceMaterial;

    [Tooltip("Default road width in metres where a segment's width is 0.")]
    public float defaultWidth = 12f;

    [Tooltip("Extra heading (deg) added to the object's own Z rotation. 0 = the spline sets off straight down local +X.")]
    public float startHeadingOffset = 0f;

    [Tooltip("If true, the last segment stitches back to the start (a closed loop). Off for open roads / escape lanes.")]
    public bool closedLoop = false;

    [Tooltip("Renderer sorting order. Main track surface is 0 and its edge lines 1, ground is -100 — keep this negative so the main track always draws over the extra road at junctions.")]
    public int sortingOrder = -2;

    [Header("Sampling")]
    [Range(1, 64)]
    [Tooltip("Minimum centerline samples per segment (floor).")]
    public int samplesPerSegment = 4;
    [Tooltip("Max spacing between samples in metres. Longer segments emit more samples so curves stay smooth.")]
    public float maxArcStepMetres = 2f;

    [Header("Segments (in travel order)")]
    public TrackInfoV2.TrackSegment[] segments;

    [Header("Edge Lines (painted boundary, e.g. white line)")]
    [Tooltip("Paint edge lines along the road. Off for plain runoff tarmac.")]
    public bool drawEdgeLines = false;
    [Tooltip("Material for the painted line. The main track uses Assets/Materials/White.mat.")]
    public Material edgeLineMaterial;
    [Tooltip("Width of the painted line in metres.")]
    public float edgeLineWidth = 0.15f;
    [Tooltip("How far the line's centre sits inboard of the road edge, in metres. Set to edgeLineWidth/2 so the line's outer edge meets the road edge exactly.")]
    public float edgeLineInset = 0.075f;
    public bool drawLeftEdgeLine = true;
    public bool drawRightEdgeLine = true;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color centerlineColor = new Color(0.3f, 0.8f, 1f, 1f);
    public bool rebuildOnValidate = true;

    Mesh _mesh;
    List<TrackBuilder.Sample> _surfaceCache;

    // Live registry so TrackBuilder.IsOnSurface can treat every extra ribbon as drivable tarmac (not grass).
    public static readonly List<ExtraTrackSpline> Active = new List<ExtraTrackSpline>();

    void OnEnable()
    {
        Build();
        if (!Active.Contains(this)) Active.Add(this);
    }

    void OnDisable() { Active.Remove(this); }

    // True if worldPos sits over this ribbon. Outputs |lateral| offset from its centerline (m).
    // Uses the cached local-space centerline (invalidated on Build) and the object's transform.
    public bool IsOnSurface(Vector3 worldPos, out float lateralAbs)
    {
        lateralAbs = 0f;
        if (segments == null || segments.Length == 0) return false;
        if (_surfaceCache == null || _surfaceCache.Count < 2) _surfaceCache = Sample();
        if (_surfaceCache.Count < 2) return false;
        Vector2 local = transform.InverseTransformPoint(worldPos);
        return TrackBuilder.OnSampleSurface(_surfaceCache, local, out lateralAbs);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Build directly (no EditorApplication.delayCall): delayCall only fires on editor ticks, which
        // stall while the editor is unfocused — external tools (MCP) would set fields and never get a mesh.
        // Direct build is safe here: Build() only assigns meshes/materials, no object destruction.
        if (!rebuildOnValidate) return;
        if (isActiveAndEnabled) Build();
    }
#endif

    public void Build()
    {
        _surfaceCache = null; // invalidate the on-surface lookup; rebuilt lazily on next query

        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();

        if (segments == null || segments.Length == 0)
        {
            mf.sharedMesh = null;
            return;
        }

        var samples = Sample();
        if (samples.Count < 2)
        {
            mf.sharedMesh = null;
            return;
        }

        var meshSamples = samples;
        if (closedLoop)
        {
            meshSamples = new List<TrackBuilder.Sample>(samples.Count + 1);
            meshSamples.AddRange(samples);
            meshSamples.Add(samples[0]);
        }

        var mesh = new Mesh { name = $"ExtraSpline_{name}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var subTris = new List<List<int>>();
        var mats = new List<Material>();

        // Submesh 0: the road surface, centerline ± half width. Its UVs are world-anchored at the project
        // pixel density so this spur's asphalt shares one texel grid with the main track it joins.
        var surface = surfaceMaterial != null ? surfaceMaterial : mr.sharedMaterial;
        subTris.Add(AppendStrip(meshSamples, verts, uvs, s => -s.width * 0.5f, s => s.width * 0.5f,
            PixelArt.UvScale(surface), transform.localToWorldMatrix));
        mats.Add(surface);

        // Optional edge lines as extra submeshes. Later submeshes draw on top of earlier ones within
        // the same renderer, so the lines sit on the road without needing their own sortingOrder.
        if (drawEdgeLines && edgeLineMaterial != null)
        {
            float halfLine = edgeLineWidth * 0.5f;
            // Painted lines are directional, so they keep ribbon UVs (across the paint, along the road).
            Vector2 lineUv = PixelArt.UvScale(edgeLineMaterial);
            if (drawLeftEdgeLine)
            {
                subTris.Add(AppendStrip(meshSamples, verts, uvs,
                    s => -(s.width * 0.5f - edgeLineInset) - halfLine,
                    s => -(s.width * 0.5f - edgeLineInset) + halfLine, lineUv, null));
                mats.Add(edgeLineMaterial);
            }
            if (drawRightEdgeLine)
            {
                subTris.Add(AppendStrip(meshSamples, verts, uvs,
                    s => (s.width * 0.5f - edgeLineInset) - halfLine,
                    s => (s.width * 0.5f - edgeLineInset) + halfLine, lineUv, null));
                mats.Add(edgeLineMaterial);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = subTris.Count;
        for (int i = 0; i < subTris.Count; i++) mesh.SetTriangles(subTris[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _mesh = mesh;
        mf.sharedMesh = _mesh;
        mr.sharedMaterials = mats.ToArray();
        mr.sortingOrder = sortingOrder;
    }

    // Quad strip between two lateral offsets from the centerline (along +normal; negative = left side).
    // Appends into the shared vertex/uv lists and returns the strip's triangle indices.
    //
    // uvScale carries the project pixel density (PixelArt.UvScale). Pass toWorld to anchor the UVs to the
    // world texel grid (isotropic surfaces like asphalt); pass null for ribbon UVs that follow the strip
    // (directional textures like painted lines).
    static List<int> AppendStrip(List<TrackBuilder.Sample> samples, List<Vector3> verts, List<Vector2> uvs,
        System.Func<TrackBuilder.Sample, float> latFrom, System.Func<TrackBuilder.Sample, float> latTo,
        Vector2 uvScale, Matrix4x4? toWorld)
    {
        var tris = new List<int>(samples.Count * 6);
        int baseIndex = verts.Count;
        float distance = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            Vector3 c = new Vector3(s.position.x, s.position.y, 0);
            Vector3 vFrom = c + right * latFrom(s);
            Vector3 vTo = c + right * latTo(s);
            verts.Add(vFrom);
            verts.Add(vTo);
            if (toWorld.HasValue)
            {
                Vector3 wFrom = toWorld.Value.MultiplyPoint3x4(vFrom);
                Vector3 wTo = toWorld.Value.MultiplyPoint3x4(vTo);
                uvs.Add(new Vector2(wFrom.x * uvScale.x, wFrom.y * uvScale.y));
                uvs.Add(new Vector2(wTo.x * uvScale.x, wTo.y * uvScale.y));
            }
            else
            {
                uvs.Add(new Vector2(0f, distance * uvScale.y));
                uvs.Add(new Vector2(1f, distance * uvScale.y));
            }
            if (i > 0)
            {
                int a = baseIndex + (i - 1) * 2;
                int b = baseIndex + i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);
                distance += Vector2.Distance(samples[i - 1].position, s.position);
            }
        }
        return tris;
    }

    List<TrackBuilder.Sample> Sample()
    {
        return TrackBuilder.SampleSegments(
            Vector2.zero,                 // local origin — the Transform places it in the world
            startHeadingOffset,           // local heading; Transform Z rotation aims the whole ribbon
            segments,
            defaultWidth,
            Mathf.Max(1, samplesPerSegment),
            Mathf.Max(0.1f, maxArcStepMetres),
            closedLoop,
            seg => seg.width <= 0f ? defaultWidth : seg.width);
    }

    // Total authored length (m), handy for scripting.
    public float TotalLength()
    {
        if (segments == null) return 0f;
        float t = 0f;
        for (int i = 0; i < segments.Length; i++) t += segments[i].length;
        return t;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || segments == null || segments.Length == 0) return;
        var samples = Sample();
        if (samples.Count < 2) return;

        Gizmos.color = centerlineColor;
        for (int i = 1; i < samples.Count; i++)
        {
            Vector3 a = transform.TransformPoint(new Vector3(samples[i - 1].position.x, samples[i - 1].position.y, 0));
            Vector3 b = transform.TransformPoint(new Vector3(samples[i].position.x, samples[i].position.y, 0));
            Gizmos.DrawLine(a, b);
        }

        // Start marker so you can see where the ribbon anchors to the Transform.
        Gizmos.color = new Color(0.2f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
}
