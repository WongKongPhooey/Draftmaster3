using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Sim;

// Grey duct-tape strips stuck over the bodywork wherever it has been repaired — by a pit stop or by the
// crew beating a towed-in car straight. VehicleDamage installs this on itself and reports every repair;
// DuctTape decides where the tape goes, and this draws it as one small mesh just in front of the car.
//
// The tape stays on for the life of the car. A later hit moves each strip with the panel it is stuck to.
[DisallowMultipleComponent]
public class DuctTapePatches : MonoBehaviour
{
    [Tooltip("How far (m) a spot of bodywork has to be pulled back straight before it earns tape.")]
    public float threshold = 0.1f;
    [Tooltip("Minimum distance (m) between two patches of tape.")]
    public float spacing = 0.8f;
    [Tooltip("Most patches one car will carry.")]
    public int maxPatches = 7;
    [Tooltip("Strip length range in sprite pixels.")]
    public Vector2Int lengthPx = new(5, 10);
    [Tooltip("Strip width in sprite pixels.")]
    public int widthPx = 2;
    [Tooltip("Up to this many strips laid side by side per patch.")]
    public int maxStripsPerPatch = 3;
    public Color tapeLight = new(0.70f, 0.70f, 0.68f, 1f);
    public Color tapeDark = new(0.55f, 0.55f, 0.54f, 1f);

    VehicleDamage _body;
    float[] _repaired;
    readonly List<DuctTape.Strip> _strips = new();
    int _patches;
    System.Random _rng;
    Texture2D _alpha;
    Rect _texRect;

    MeshFilter _filter;
    MeshRenderer _renderer;
    Mesh _mesh;
    static Material s_material;
    readonly Vector2[] _corners = new Vector2[4];

    public int StripCount => _strips.Count;

    // Fresh body (built or rebuilt from a new livery): no tape, nothing owed.
    public void Bind(VehicleDamage body, Sprite sprite, int vertexCount)
    {
        if (_body != null) _body.BodyChanged -= Redraw;
        _body = body;
        if (_body != null && isActiveAndEnabled) _body.BodyChanged += Redraw;
        _repaired = new float[vertexCount];
        _strips.Clear();
        _patches = 0;
        _rng ??= new System.Random(GetInstanceID());
        _alpha = sprite != null && sprite.texture != null && sprite.texture.isReadable ? sprite.texture : null;
        _texRect = sprite != null ? sprite.textureRect : default;
        Redraw();
    }

    // Called by VehicleDamage with the mesh as it was before a repair and as it is after.
    public void OnRepaired(Vector3[] baseVerts, Vector3[] before, Vector3[] after, Vector2 halfSize, float pixel)
    {
        if (_repaired == null || _repaired.Length != baseVerts.Length) _repaired = new float[baseVerts.Length];
        DuctTape.Accumulate(_repaired, baseVerts, before, after);

        var s = new DuctTape.Settings
        {
            threshold = threshold,
            spacing = spacing,
            maxPatches = maxPatches,
            pixel = pixel,
            area = TapeArea(halfSize),
            minLengthPx = Mathf.Max(1, lengthPx.x),
            maxLengthPx = Mathf.Max(lengthPx.x, lengthPx.y),
            widthPx = Mathf.Max(1, widthPx),
            maxStripsPerPatch = Mathf.Max(1, maxStripsPerPatch),
        };
        int added = DuctTape.Place(_strips, _patches, _repaired, baseVerts, s, _rng, p => OnBody(p, halfSize), out int patches);
        _patches += patches;
        if (added > 0) Redraw();
    }

    // The rectangle tape has to stay wholly inside, in the bodywork's local space: the car's collision box,
    // undamaged, cut down to the sprite. The sprite has clear margins round the car; the box is the car.
    Rect TapeArea(Vector2 halfSize)
    {
        var sprite = new Rect(-halfSize, halfSize * 2f);
        var box = GetComponentInParent<BoxCollider2D>();
        if (box == null) return sprite;

        Vector2 size = box.size, offset = box.offset;
        var vc = box.GetComponent<VehicleCollision>();
        if (vc != null)
        {
            // The box shortens as the ends crush; the tape goes on metal that has just been straightened.
            size = new Vector2(vc.halfExtents.y * 2f, vc.halfExtents.x * 2f);
            offset = vc.BaseOffset;
        }

        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        for (int k = 0; k < 4; k++)
        {
            var corner = offset + new Vector2((k & 1) == 0 ? -0.5f : 0.5f, (k & 2) == 0 ? -0.5f : 0.5f) * size;
            Vector2 local = transform.InverseTransformPoint(box.transform.TransformPoint(corner));
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        min = Vector2.Max(min, sprite.min);
        max = Vector2.Min(max, sprite.max);
        return max.x > min.x && max.y > min.y ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : sprite;
    }

    // Only stick tape on the car, not on the clear pixels round its outline.
    bool OnBody(Vector2 local, Vector2 halfSize)
    {
        if (_alpha == null || halfSize.x <= 0f || halfSize.y <= 0f) return true;
        float fx = Mathf.Clamp01((local.x + halfSize.x) / (2f * halfSize.x));
        float fy = Mathf.Clamp01((local.y + halfSize.y) / (2f * halfSize.y));
        int px = Mathf.Clamp(Mathf.FloorToInt(_texRect.x + fx * _texRect.width), 0, _alpha.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(_texRect.y + fy * _texRect.height), 0, _alpha.height - 1);
        return _alpha.GetPixel(px, py).a > 0.5f;
    }

    void OnEnable() { if (_body != null) _body.BodyChanged += Redraw; }
    void OnDisable() { if (_body != null) _body.BodyChanged -= Redraw; }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_filter != null) Destroy(_filter.gameObject);
    }

    void EnsureRenderer()
    {
        if (_filter != null) return;
        var go = new GameObject("DuctTape");
        go.transform.SetParent(transform, false);
        // A hair toward the camera so the tape never ties with the bodywork it is stuck to.
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        _filter = go.AddComponent<MeshFilter>();
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.sharedMaterial = Material;
        _mesh = new Mesh { name = "DuctTape" };
        _mesh.MarkDynamic();
        _filter.sharedMesh = _mesh;
    }

    static Material Material
    {
        get
        {
            if (s_material != null) return s_material;
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            s_material = new Material(sh) { name = "DuctTape (auto)" };
            if (s_material.HasProperty("_MainTex")) s_material.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (s_material.HasProperty("_BaseMap")) s_material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            return s_material;
        }
    }

    void Redraw()
    {
        if (_strips.Count == 0)
        {
            if (_mesh != null) _mesh.Clear();
            if (_renderer != null) _renderer.enabled = false;
            return;
        }
        EnsureRenderer();

        var src = _body != null ? _body.GetComponent<MeshRenderer>() : null;
        if (src != null)
        {
            _renderer.sortingLayerID = src.sortingLayerID;
            _renderer.sortingOrder = src.sortingOrder + 1;
        }
        _renderer.enabled = true;

        Vector3[] baseVerts = _body != null ? _body.BaseVertices : null;
        Vector3[] current = _body != null ? _body.CurrentVertices : null;

        var verts = new Vector3[_strips.Count * 4];
        var colors = new Color[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[_strips.Count * 12];
        for (int i = 0; i < _strips.Count; i++)
        {
            var strip = _strips[i];
            // Ride the panel: if it has been hit since the tape went on, the tape moved with it.
            Vector2 shift = Vector2.zero;
            if (baseVerts != null && current != null && strip.anchor >= 0 && strip.anchor < baseVerts.Length
                && strip.anchor < current.Length)
                shift = current[strip.anchor] - baseVerts[strip.anchor];
            DuctTape.Corners(strip, strip.center + shift, _corners);

            Color c = Color.Lerp(tapeDark, tapeLight, strip.shade);
            int v = i * 4;
            for (int k = 0; k < 4; k++)
            {
                verts[v + k] = _corners[k];
                colors[v + k] = c;
            }
            uvs[v] = new Vector2(0, 0); uvs[v + 1] = new Vector2(1, 0);
            uvs[v + 2] = new Vector2(1, 1); uvs[v + 3] = new Vector2(0, 1);

            // Both windings, so it shows whichever way the camera and the car's flip leave it facing.
            int t = i * 12;
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            tris[t + 6] = v; tris[t + 7] = v + 2; tris[t + 8] = v + 1;
            tris[t + 9] = v; tris[t + 10] = v + 3; tris[t + 11] = v + 2;
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.colors = colors;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }
}
