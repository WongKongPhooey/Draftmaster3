using System.Collections.Generic;
using UnityEngine;

// The SAFER barrier the previous game drew its walls with (Assets/Prefabs/SaferBarrier.prefab), rebuilt as flat
// strips for the spline tracks and the title screen. From the track outward: the inner wall, the impact
// absorbers, the outer wall, and the catch fence drawn over the top of all three.
//
// The prefab is the source: each strip's material, width and lateral position are read off its four children
// (InnerEdge, Absorbers, OuterEdge, CatchFence — scaled cubes, x = across the wall). Change the prefab and every
// track follows the next time its environment builds.
//
// Scale: one prefab unit is LegacyUnitMetres. That value is the one where the absorber strip (8 px across) and
// the catch fence (32 px across) both land on the project's 12.8 px/m, so nothing is stretched.
//
// One instance lives at Resources/Track/SaferBarrierStyle.asset (Default).
[CreateAssetMenu(fileName = "SaferBarrierStyle", menuName = "Racetrack/SAFER Barrier Style", order = 4)]
public class SaferBarrierStyle : ScriptableObject
{
    public const string ResourcePath = "Track/SaferBarrierStyle";
    public const float LegacyUnitMetres = 2.5f;

    [Tooltip("The previous game's barrier prefab. Its InnerEdge / Absorbers / OuterEdge / CatchFence children are the strips.")]
    public GameObject prefab;
    [Tooltip("Sorting order added to the barrier's for the catch fence, so it draws over the walls (and over a car against them).")]
    public int fenceSortingOffset = 1;

    static SaferBarrierStyle _default;
    static bool _looked;

    public static SaferBarrierStyle Default
    {
        get
        {
            if (!_looked || _default == null)
            {
                _looked = true;
                _default = Resources.Load<SaferBarrierStyle>(ResourcePath);
            }
            return _default;
        }
    }

    // One strip, in metres across the wall measured OUTWARD from the barrier's track-side face.
    public struct Layer
    {
        public string name;
        public Material material;
        public float from, to;
        public bool fence;
    }

    static readonly string[] Parts = { "InnerEdge", "Absorbers", "OuterEdge", "CatchFence" };

    // The strips, walls first and the fence last (draw order). Empty when the prefab is missing or malformed.
    public List<Layer> Layers()
    {
        var layers = new List<Layer>();
        if (prefab == null) return layers;

        // The wall's track face is the inner edge's track-side edge; everything is measured from there.
        var inner = prefab.transform.Find("InnerEdge");
        if (inner == null) return layers;
        float face = inner.localPosition.x - inner.localScale.x * 0.5f;

        foreach (var part in Parts)
        {
            var t = prefab.transform.Find(part);
            var r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null || r.sharedMaterial == null) continue;
            float lo = t.localPosition.x - t.localScale.x * 0.5f, hi = t.localPosition.x + t.localScale.x * 0.5f;
            layers.Add(new Layer
            {
                name = part,
                material = r.sharedMaterial,
                from = (lo - face) * LegacyUnitMetres,
                to = (hi - face) * LegacyUnitMetres,
                fence = part == "CatchFence",
            });
        }
        return layers;
    }

    // Track face to the outer wall's back, in metres: how much ground the solid part of the barrier covers.
    public float WallWidthMetres()
    {
        float w = 0f;
        foreach (var l in Layers()) if (!l.fence) w = Mathf.Max(w, l.to);
        return w;
    }

    // Builds the strips under `parent` along `line`.
    //
    //   line        the polyline the barrier follows (at least two points)
    //   trackSign   +1 if the track is on the line's RIGHT (+normal, normal = right of travel), -1 if on its left
    //   faceOffset  how far toward the track the barrier's face sits from `line` — 0 when `line` IS the face,
    //               half the collider's thickness when `line` is the collider's centre
    //
    // Textures are laid in metres at 12.8 px/m: U along the wall (both legacy textures run their long side
    // along it), V across it from the track side outward.
    public void Build(Transform parent, IList<Vector2> line, int trackSign, float faceOffset, int sortingOrder,
                      float z = 0f)
    {
        if (line == null || line.Count < 2) return;
        foreach (var layer in Layers())
        {
            var go = new GameObject(layer.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.hideFlags = parent.gameObject.hideFlags;
            go.transform.SetParent(parent, false);
            var mesh = Ribbon(line, trackSign, faceOffset, layer, z);
            mesh.hideFlags = go.hideFlags;   // DontSave under a title-screen backdrop, saved under a track package
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = layer.material;
            mr.sortingOrder = sortingOrder + (layer.fence ? fenceSortingOffset : 0);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    static Mesh Ribbon(IList<Vector2> line, int trackSign, float faceOffset, Layer layer, float z)
    {
        var mesh = new Mesh { name = "SAFER " + layer.name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector2 d = PixelArt.UvScale(layer.material);
        int n = line.Count;
        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new List<int>((n - 1) * 6);
        float along = 0f;
        float vFar = (layer.to - layer.from) * d.y;

        for (int i = 0; i < n; i++)
        {
            Vector2 tangent = i == 0 ? line[1] - line[0]
                            : i == n - 1 ? line[i] - line[i - 1]
                            : line[i + 1] - line[i - 1];
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector2.right;
            tangent.Normalize();
            Vector2 normal = new Vector2(tangent.y, -tangent.x);    // right of travel
            Vector2 toTrack = normal * trackSign;
            Vector2 face = line[i] + toTrack * faceOffset;
            Vector2 near = face - toTrack * layer.from;             // track side of this strip
            Vector2 far = face - toTrack * layer.to;                // outside

            if (i > 0) along += Vector2.Distance(line[i], line[i - 1]);
            float u = along * d.x;

            // Left (-normal) vertex first, as every ribbon in TrackEnvironmentBuilder is, so the winding faces
            // the camera whichever side the track is on.
            bool nearIsLeft = trackSign < 0;
            verts[i * 2] = nearIsLeft ? new Vector3(near.x, near.y, z) : new Vector3(far.x, far.y, z);
            verts[i * 2 + 1] = nearIsLeft ? new Vector3(far.x, far.y, z) : new Vector3(near.x, near.y, z);
            uvs[i * 2] = new Vector2(u, nearIsLeft ? 0f : vFar);
            uvs[i * 2 + 1] = new Vector2(u, nearIsLeft ? vFar : 0f);

            if (i > 0)
            {
                int a = (i - 1) * 2, b = i * 2;
                tris.Add(a); tris.Add(b); tris.Add(b + 1);
                tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
