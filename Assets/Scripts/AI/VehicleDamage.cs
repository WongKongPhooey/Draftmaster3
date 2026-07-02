using UnityEngine;

// Deformable car bodywork. Builds a subdivided quad mesh from a sprite and dents vertices
// inward at impact points, accumulating permanent damage. Replaces the SpriteRenderer visual.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VehicleDamage : MonoBehaviour, IDamageable
{
    [Header("Source")]
    [Tooltip("Car sprite to build the deformable mesh from.")]
    public Sprite sourceSprite;
    [Tooltip("Material to render with. Should be an unlit transparent/sprite material using the sprite texture.")]
    public Material material;

    [Header("Mesh Resolution")]
    [Range(2, 32)] public int gridX = 10;
    [Range(2, 32)] public int gridY = 16;

    [Header("Render")]
    public string sortingLayer = "Default";
    public int sortingOrder = 5;

    [Header("Damage Tuning")]
    [Tooltip("Radius (local units) of vertices affected by one impact.")]
    public float dentRadius = 0.5f;
    [Tooltip("Max displacement per unit severity (local units).")]
    public float dentStrength = 0.35f;
    [Tooltip("Max total dent depth any vertex can accumulate (local units).")]
    public float maxDent = 0.8f;
    [Tooltip("Minimum severity to register a dent. Filters tiny scrapes.")]
    public float minSeverity = 0.08f;

    [Header("Rigid Core")]
    [Tooltip("Optional greyscale mask painted over the sprite: white = deforms fully, black = rigid (core shell). Must be Read/Write enabled. Overrides the core rect below.")]
    public Texture2D deformMask;
    [Tooltip("Width of the rigid core as a fraction of the sprite (0 = disabled). Vertices inside never deform.")]
    [Range(0f, 1f)] public float coreWidthFrac = 0f;
    [Tooltip("Height of the rigid core as a fraction of the sprite (0 = disabled).")]
    [Range(0f, 1f)] public float coreHeightFrac = 0f;
    [Tooltip("Falloff band (fraction of sprite) outside the core rect where deformation fades in from 0 to full.")]
    [Range(0.01f, 0.5f)] public float coreFalloffFrac = 0.15f;

    [Header("Damage Severity → Handling")]
    [Tooltip("Accumulated damage per unit impact severity. Higher = a few hits cripple the car.")]
    public float damageAccrual = 0.18f;

    // 0..1 accumulated bodywork damage, read by PlayerVehicleController to spoil grip / top speed.
    public float DamageLevel { get; private set; }
    // Signed left/right damage bias (−1 = left side battered, +1 = right). Drives a steering pull.
    public float DamageBiasX { get; private set; }

    Mesh _mesh;
    Vector3[] _base;
    Vector3[] _current;
    float[] _deformWeight;
    float _worldToLocal = 1f;
    float _biasAccum;

    void Awake() { Build(); }

    public void Build()
    {
        if (sourceSprite == null) return;

        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (material == null) material = BuildSpriteMaterial(sourceSprite);
        mr.sharedMaterial = material;
        mr.sortingLayerName = sortingLayer;
        mr.sortingOrder = sortingOrder;

        // Disable any SpriteRenderer on this object so only the mesh shows.
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        Vector2 size = sourceSprite.bounds.size;
        Vector2 min = -size * 0.5f;

        int vx = gridX + 1, vy = gridY + 1;
        _base = new Vector3[vx * vy];
        _deformWeight = new float[vx * vy];
        var uvs = new Vector2[vx * vy];
        Rect uvRect = new Rect(
            sourceSprite.textureRect.x / sourceSprite.texture.width,
            sourceSprite.textureRect.y / sourceSprite.texture.height,
            sourceSprite.textureRect.width / sourceSprite.texture.width,
            sourceSprite.textureRect.height / sourceSprite.texture.height);

        for (int y = 0; y < vy; y++)
        {
            for (int x = 0; x < vx; x++)
            {
                float fx = x / (float)gridX, fy = y / (float)gridY;
                int idx = y * vx + x;
                _base[idx] = new Vector3(min.x + size.x * fx, min.y + size.y * fy, 0f);
                uvs[idx] = new Vector2(uvRect.x + uvRect.width * fx, uvRect.y + uvRect.height * fy);
                _deformWeight[idx] = ComputeDeformWeight(fx, fy);
            }
        }

        // Double-sided (12 indices/quad) so the car shows regardless of camera-facing / winding.
        var tris = new int[gridX * gridY * 12];
        int t = 0;
        for (int y = 0; y < gridY; y++)
        {
            for (int x = 0; x < gridX; x++)
            {
                int i0 = y * vx + x, i1 = i0 + 1, i2 = i0 + vx, i3 = i2 + 1;
                tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                // reversed
                tris[t++] = i0; tris[t++] = i1; tris[t++] = i2;
                tris[t++] = i1; tris[t++] = i3; tris[t++] = i2;
            }
        }

        _current = (Vector3[])_base.Clone();
        _mesh = new Mesh { name = "CarBodywork" };
        _mesh.vertices = _current;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
        mf.sharedMesh = _mesh;
    }

    // Per-vertex deform weight, 0 = rigid, 1 = fully deformable. fx/fy are sprite-normalized [0,1].
    float ComputeDeformWeight(float fx, float fy)
    {
        if (deformMask != null)
        {
            if (deformMask.isReadable)
                return Mathf.Clamp01(deformMask.GetPixelBilinear(fx, fy).grayscale);
            Debug.LogWarning($"VehicleDamage ({name}): deformMask '{deformMask.name}' is not Read/Write enabled — falling back to core rect.", this);
        }

        if (coreWidthFrac <= 0f || coreHeightFrac <= 0f) return 1f;

        // Distance outside the centered core rect, per axis, in sprite fractions.
        float dx = Mathf.Abs(fx - 0.5f) - coreWidthFrac * 0.5f;
        float dy = Mathf.Abs(fy - 0.5f) - coreHeightFrac * 0.5f;
        float outside = Mathf.Max(dx, dy); // <= 0 inside the core
        return Mathf.Clamp01(outside / coreFalloffFrac);
    }

    static Material BuildSpriteMaterial(Sprite sprite)
    {
        // Prefer URP unlit sprite shader; fall back to built-in sprite shader.
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh) { name = "CarBodywork (auto)" };
        if (sprite != null && sprite.texture != null)
        {
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", sprite.texture);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", sprite.texture);
        }
        return mat;
    }

    public void OnImpact(Vector2 worldPoint, Vector2 worldInward, float severity)
    {
        if (_mesh == null || _base == null || severity < minSeverity) return;

        // Convert to local space (mesh is in this transform's local frame).
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 localDir = transform.InverseTransformVector(new Vector3(worldInward.x, worldInward.y, 0f));
        if (localDir.sqrMagnitude > 1e-6f) localDir.Normalize();

        float displace = dentStrength * Mathf.Clamp01(severity);
        bool changed = false;

        for (int i = 0; i < _current.Length; i++)
        {
            float weight = _deformWeight != null ? _deformWeight[i] : 1f;
            if (weight <= 0f) continue; // rigid core shell — never bends

            float dist = Vector2.Distance(_current[i], localPoint);
            if (dist > dentRadius) continue;
            float falloff = 1f - (dist / dentRadius);
            Vector3 push = localDir * (displace * falloff * weight);
            Vector3 target = _current[i] + push;

            // Clamp accumulated displacement from base; rigid-ish vertices also cap shallower.
            Vector3 fromBase = target - _base[i];
            float vertexMaxDent = maxDent * weight;
            if (fromBase.magnitude > vertexMaxDent) fromBase = fromBase.normalized * vertexMaxDent;
            _current[i] = _base[i] + fromBase;
            changed = true;
        }

        if (changed)
        {
            _mesh.vertices = _current;
            _mesh.RecalculateBounds();

            // Accumulate a 0..1 damage level + a left/right bias (from where the hit landed) for handling effects.
            DamageLevel = Mathf.Clamp01(DamageLevel + Mathf.Clamp01(severity) * damageAccrual * TrackConditions.DamageMultiplier);
            _biasAccum += localPoint.x * Mathf.Clamp01(severity);
            DamageBiasX = Mathf.Clamp(_biasAccum, -1f, 1f);
        }
    }

    public void RepairFull()
    {
        if (_base == null || _mesh == null) return;
        _current = (Vector3[])_base.Clone();
        _mesh.vertices = _current;
        _mesh.RecalculateBounds();
        DamageLevel = 0f;
        DamageBiasX = 0f;
        _biasAccum = 0f;
    }
}
