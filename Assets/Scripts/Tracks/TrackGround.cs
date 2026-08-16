using UnityEngine;

// The grass/dirt plane every track sits on, sized from the track itself.
//
// Watkins Glen has a hand-made quad for this, which is fine for one track and pointless for thirty-five:
// the size is entirely determined by the spline's bounding box, so it may as well be computed. Same idiom
// as TrackBuilder and Grandstand — [ExecuteAlways], mesh generated in Build(), nothing serialised but the
// settings.
//
// Depth convention matches the rest of the project: -z is towards the camera, so the road (z = 0) and the
// brake boards (z = -0.05) sit ON this plane, which is pushed slightly positive.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackGround : MonoBehaviour
{
    [Tooltip("The road this plane is sized from. Left null, the package's TrackBuilder is used.")]
    public TrackBuilder track;

    [Tooltip("Ground surface (grass, dirt, concrete). Tiled at the project pixel standard.")]
    public Material material;

    [Tooltip("Metres of ground beyond the track's bounding box on every side.")]
    public float margin = 150f;

    [Tooltip("Depth. Positive = behind the road, which is what you want — -z is towards the camera here.")]
    public float depth = 0.1f;

    [Tooltip("Below the road (0), the runoff (-10) and everything else trackside.")]
    public int sortingOrder = -30;

    Mesh _mesh;

    void OnEnable() { Build(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Build();
        };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Build()
    {
        var builder = Resolve();
        if (builder == null || builder.track == null) return;

        var samples = builder.SampleCenterline();
        if (samples.Count < 2) return;

        // Bounding box of the road surface, not just the centreline — a wide superspeedway corner would
        // otherwise poke over the edge of its own ground.
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var s in samples)
        {
            float half = s.width * 0.5f;
            minX = Mathf.Min(minX, s.position.x - half); maxX = Mathf.Max(maxX, s.position.x + half);
            minY = Mathf.Min(minY, s.position.y - half); maxY = Mathf.Max(maxY, s.position.y + half);
        }

        var pit = builder.SamplePitCenterline();
        foreach (var s in pit)
        {
            float half = s.width * 0.5f;
            minX = Mathf.Min(minX, s.position.x - half); maxX = Mathf.Max(maxX, s.position.x + half);
            minY = Mathf.Min(minY, s.position.y - half); maxY = Mathf.Max(maxY, s.position.y + half);
        }

        // Samples are in the builder's space; the plane may hang off a different parent.
        Vector3 min = transform.InverseTransformPoint(builder.transform.TransformPoint(new Vector3(minX - margin, minY - margin, 0f)));
        Vector3 max = transform.InverseTransformPoint(builder.transform.TransformPoint(new Vector3(maxX + margin, maxY + margin, 0f)));
        min.z = max.z = depth;

        Vector2 span = new Vector2(max.x - min.x, max.y - min.y);
        Vector2 tiling = material != null && material.mainTexture != null
            ? PixelArt.TilingForSpan(span, material.mainTexture)
            : Vector2.one;

        _mesh = new Mesh { name = "TrackGround" };
        _mesh.vertices = new[]
        {
            new Vector3(min.x, min.y, depth), new Vector3(max.x, min.y, depth),
            new Vector3(max.x, max.y, depth), new Vector3(min.x, max.y, depth)
        };
        _mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(tiling.x, 0f),
            new Vector2(tiling.x, tiling.y), new Vector2(0f, tiling.y)
        };
        // Both windings: the project's URP renderer views this from either side depending on the camera rig.
        _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        mf.sharedMesh = _mesh;
        if (material != null) mr.sharedMaterial = material;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    TrackBuilder Resolve()
    {
        if (track != null) return track;
        var package = GetComponentInParent<TrackPackage>();
        if (package != null) track = package.Builder;
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        return track;
    }
}
