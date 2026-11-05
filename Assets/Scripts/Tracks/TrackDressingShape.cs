using System.Collections.Generic;
using UnityEngine;

// A flat piece of track dressing drawn from the air: a paved lot, an access road, a building's roof, a video
// board. Points are local metres; the mesh is rebuilt from them in OnEnable (like Grandstand and
// TrackEnvironmentBuilder), so a package prefab stores only the recipe and never a stale baked mesh.
//
// Purely visual. Nothing here is registered with the car physics (SurfaceField) or given a collider - it is for
// ground behind the walls and buildings off the circuit, which no car reaches. Paving the racing surface's own
// run-off belongs in TrackEnvironment.runoffAreas instead.
//
//   Polygon  - a filled outline (simple, either winding). Optional border drawn just inside the outline, and an
//              optional drop shadow: the outline swept along shadowOffset, which reads as height from above.
//   Path     - a polyline of constant width (pathWidth), round-jointed, optionally closed: roads and loops.
//
// Fill is `color`, over `texture` when one is set (tiled in world metres at the project pixel standard, as the
// run-off areas are, so it lands on the road's texel grid). Shadow and border are always flat colours.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackDressingShape : MonoBehaviour
{
    public enum Kind { Polygon, Path }

    public Kind kind = Kind.Polygon;
    [Tooltip("Outline (Polygon) or centreline (Path), in local metres.")]
    public Vector2[] points;
    [Tooltip("Path only: join the last point back to the first.")]
    public bool closedPath;
    [Tooltip("Path only: width in metres.")]
    public float pathWidth = 8f;

    [Tooltip("Fill texture, tiled in world metres at 12.8 px/m and tinted by the colour below. Empty = flat colour.")]
    public Texture2D texture;
    public Color color = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Tooltip("Polygon only: an edge band this wide just inside the outline (0 = none).")]
    public float borderWidth;
    public Color borderColor = new Color(0.15f, 0.15f, 0.17f, 1f);

    [Tooltip("Polygon only: the outline swept this far (metres) as a shadow under the shape. Zero = no shadow.")]
    public Vector2 shadowOffset;
    [Range(0f, 1f)] public float shadowAlpha = 0.35f;

    public int sortingOrder = -12;

    static Material _flat;
    static readonly Dictionary<Texture, Material> _textured = new Dictionary<Texture, Material>();
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

    static Material Flat
    {
        get
        {
            if (_flat != null) return _flat;
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _flat = new Material(sh) { name = "TrackDressingFlat", hideFlags = HideFlags.HideAndDontSave };
            return _flat;
        }
    }

    // One unsaved material per texture, shared by every shape that tiles it; the vertex colour does the tinting.
    static Material Textured(Texture tex)
    {
        if (_textured.TryGetValue(tex, out var m) && m != null) return m;
        m = new Material(Flat) { name = "TrackDressing_" + tex.name, hideFlags = HideFlags.HideAndDontSave, mainTexture = tex };
        _textured[tex] = m;
        return m;
    }

    public void Build()
    {
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (points == null || points.Length < 2) { mf.sharedMesh = null; return; }

        var shadow = new MeshPart();
        var fill = new MeshPart();
        var border = new MeshPart();

        if (kind == Kind.Polygon && points.Length >= 3)
        {
            var poly = new List<Vector2>(points);
            if (SignedArea(poly) < 0f) poly.Reverse();   // anticlockwise from here on: inside is to the left

            if (shadowOffset.sqrMagnitude > 1e-4f && shadowAlpha > 0f)
            {
                // The outline swept along the offset: for each edge, the quad between it and its shifted copy,
                // plus the shifted outline itself. Overlaps are the same colour, so they don't show.
                var shade = new Color(0f, 0f, 0f, shadowAlpha);
                var moved = new List<Vector2>(poly.Count);
                foreach (var p in poly) moved.Add(p + shadowOffset);
                shadow.AddPolygon(moved, shade);
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                    shadow.AddQuad(a, b, b + shadowOffset, a + shadowOffset, shade);
                }
            }

            fill.AddPolygon(poly, color);

            if (borderWidth > 0f)
            {
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                    Vector2 d = b - a;
                    if (d.sqrMagnitude < 1e-6f) continue;
                    Vector2 inward = new Vector2(-d.y, d.x).normalized * borderWidth;   // left of an anticlockwise edge
                    border.AddQuad(a, b, b + inward, a + inward, borderColor);
                }
            }
        }
        else
        {
            float half = Mathf.Max(0.05f, pathWidth * 0.5f);
            int n = points.Length;
            int edges = closedPath ? n : n - 1;
            for (int i = 0; i < edges; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % n];
                Vector2 d = b - a;
                if (d.sqrMagnitude < 1e-6f) continue;
                Vector2 side = new Vector2(-d.y, d.x).normalized * half;
                fill.AddQuad(a - side, b - side, b + side, a + side, color);
            }
            // Round joints, so a bend has no notch on its outside.
            for (int i = 0; i < n; i++) fill.AddDisc(points[i], half, color);
        }

        if (_mesh == null) _mesh = new Mesh { name = "TrackDressingShape", hideFlags = HideFlags.DontSave };
        _mesh.Clear();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var uvs = new List<Vector2>();
        Vector2 uvScale = texture != null ? PixelArt.UvScale(texture) : Vector2.one;
        int[] Append(MeshPart part)
        {
            int start = verts.Count;
            foreach (var v in part.verts)
            {
                verts.Add(new Vector3(v.x, v.y, 0f));
                Vector2 world = transform.TransformPoint(v);
                uvs.Add(new Vector2(world.x * uvScale.x, world.y * uvScale.y));
            }
            // Vertex colours reach the shader unconverted, so in a linear-space project an sRGB colour picked in
            // the inspector would come out paler than picked: convert it here, as Unity does for material colours.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            foreach (var c in part.colours) cols.Add(linear ? c.linear : c);
            var tris = new int[part.tris.Count * 2];
            for (int i = 0; i < part.tris.Count; i += 3)
            {
                // Both windings: the shape shows whichever way the camera and the material's culling face.
                tris[i * 2] = start + part.tris[i];
                tris[i * 2 + 1] = start + part.tris[i + 1];
                tris[i * 2 + 2] = start + part.tris[i + 2];
                tris[i * 2 + 3] = start + part.tris[i];
                tris[i * 2 + 4] = start + part.tris[i + 2];
                tris[i * 2 + 5] = start + part.tris[i + 1];
            }
            return tris;
        }
        // Drawn in this order within the one renderer: shadow under the fill, border over it.
        var parts = new List<(int[] tris, Material mat)>();
        if (shadow.tris.Count > 0) parts.Add((Append(shadow), Flat));
        if (fill.tris.Count > 0) parts.Add((Append(fill), texture != null ? Textured(texture) : Flat));
        if (border.tris.Count > 0) parts.Add((Append(border), Flat));
        _mesh.SetVertices(verts);
        _mesh.SetColors(cols);
        _mesh.SetUVs(0, uvs);
        _mesh.subMeshCount = parts.Count;
        var mats = new Material[parts.Count];
        for (int i = 0; i < parts.Count; i++)
        {
            _mesh.SetTriangles(parts[i].tris, i);
            mats[i] = parts[i].mat;
        }
        _mesh.RecalculateBounds();
        mf.sharedMesh = _mesh;
        mr.sharedMaterials = mats;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // Does the shape (fill, not shadow) cover this world point? For clearing scattered scenery off it.
    public bool Covers(Vector2 world, float pad = 0f)
    {
        if (points == null || points.Length < 2) return false;
        Vector2 p = transform.InverseTransformPoint(world);
        if (kind == Kind.Path)
        {
            float reach = pathWidth * 0.5f + pad;
            int edges = closedPath ? points.Length : points.Length - 1;
            for (int i = 0; i < edges; i++)
                if (DistanceToSegment(p, points[i], points[(i + 1) % points.Length]) <= reach) return true;
            return false;
        }
        if (points.Length < 3) return false;
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            if ((points[i].y > p.y) != (points[j].y > p.y) &&
                p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                inside = !inside;
        if (inside || pad <= 0f) return inside;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            if (DistanceToSegment(p, points[j], points[i]) <= pad) return true;
        return false;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static float SignedArea(List<Vector2> p)
    {
        float a = 0f;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += p[j].x * p[i].y - p[i].x * p[j].y;
        return a * 0.5f;
    }

    class MeshPart
    {
        public readonly List<Vector2> verts = new List<Vector2>();
        public readonly List<Color> colours = new List<Color>();
        public readonly List<int> tris = new List<int>();

        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int i = 0; i < 4; i++) colours.Add(col);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }

        public void AddDisc(Vector2 centre, float radius, Color col)
        {
            const int Steps = 12;
            int s = verts.Count;
            verts.Add(centre);
            colours.Add(col);
            for (int i = 0; i < Steps; i++)
            {
                float a = i * Mathf.PI * 2f / Steps;
                verts.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                colours.Add(col);
            }
            for (int i = 0; i < Steps; i++)
            {
                tris.Add(s); tris.Add(s + 1 + i); tris.Add(s + 1 + (i + 1) % Steps);
            }
        }

        // Ear clipping of a simple anticlockwise polygon.
        public void AddPolygon(List<Vector2> poly, Color col)
        {
            int s = verts.Count;
            foreach (var p in poly) { verts.Add(p); colours.Add(col); }
            var remaining = new List<int>();
            for (int i = 0; i < poly.Count; i++) remaining.Add(i);
            int guard = poly.Count * poly.Count + 16;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int r = 0; r < remaining.Count; r++)
                {
                    int i0 = remaining[(r - 1 + remaining.Count) % remaining.Count];
                    int i1 = remaining[r];
                    int i2 = remaining[(r + 1) % remaining.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    if (Cross(b - a, c - a) <= 0f) continue;   // reflex corner, not an ear
                    bool blocked = false;
                    foreach (int k in remaining)
                    {
                        if (k == i0 || k == i1 || k == i2) continue;
                        if (InTriangle(poly[k], a, b, c)) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    tris.Add(s + i0); tris.Add(s + i1); tris.Add(s + i2);
                    remaining.RemoveAt(r);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // self-intersecting: keep what was clipped
            }
            if (remaining.Count == 3) { tris.Add(s + remaining[0]); tris.Add(s + remaining[1]); tris.Add(s + remaining[2]); }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
    }
}
