using UnityEngine;
using Draftmaster.Sim;

// Deformable car bodywork. Builds a subdivided quad mesh from a sprite and folds it around whatever hit it,
// accumulating permanent damage. Replaces the SpriteRenderer visual.
//
// The dent is a PRESS, not a blast: the striker's own body is driven into the panel and the vertices left
// inside it are pushed back out along the contact normal, so the dent comes out the shape of the thing that
// made it. BodyDeform holds that geometry and explains why the old point-and-radius crater was wrong.
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
    [Tooltip("Footprint (local units) of a hit with no body behind it — an authored dent, a kerb, a stone. " +
             "A contact with a real striker takes its width from that body instead, so this no longer bounds " +
             "car-vs-car or car-vs-wall damage.")]
    public float dentRadius = 0.5f;
    [Tooltip("How far a full-severity hit drives the striker into the bodywork (local units). The deepest " +
             "any single impact can fold a panel.")]
    public float dentStrength = 0.35f;
    [Tooltip("Max total dent depth any vertex can accumulate (local units).")]
    public float maxDent = 0.8f;
    [Tooltip("Minimum severity to register a dent. Filters tiny scrapes.")]
    public float minSeverity = 0.08f;

    [Header("Crumple")]
    [Tooltip("How much the metal beside a fold comes with it, 0..1. 0 = the press leaves a clean stamp of " +
             "the striker with a sheared edge; higher buckles the surrounding panel.")]
    [Range(0f, 1f)] public float crumpleSpread = 0.35f;
    [Tooltip("Smoothing passes per impact. More = the fold carries further out across the bodywork.")]
    [Range(0, 6)] public int crumplePasses = 2;

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
    float _biasAccum;

    // Scratch for one press: the displacement field, which vertices the press reached, and the buffers
    // BodyDeform smooths through. Allocated once in Build so an impact costs no garbage.
    Vector3[] _disp;
    bool[] _region;
    Vector3[] _dispScratch;
    bool[] _regionScratch;

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
        _disp = new Vector3[_base.Length];
        _region = new bool[_base.Length];
        _dispScratch = new Vector3[_base.Length];
        _regionScratch = new bool[_base.Length];
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

    // A hit with no body behind it — an authored dent, a kerb, a stone, anything that only knows where it
    // landed. Struck as a small hammer press `dentRadius` across rather than a crater centred on the point,
    // so even this leaves a flat-bottomed crease that folds one way.
    public void OnImpact(Vector2 worldPoint, Vector2 worldInward, float severity)
    {
        // dentRadius is authored in local units; the striker lives in world space.
        float scale = transform.TransformVector(Vector3.right).magnitude;
        OnImpact(BodyDeform.Striker.Point(worldPoint, worldInward, dentRadius * Mathf.Max(1e-4f, scale)), severity);
    }

    // The real one. The striker's body is driven into the bodywork by however far severity buys, and every
    // vertex left inside it is pushed back out along the contact normal — so the dent that is left behind is
    // the shape of what hit us, as wide as the face that actually touched, deep where it was deepest in.
    // Then the panel around it is dragged along, because bodywork is one sheet and metal does not shear
    // along the outline of the thing that hit it.
    public void OnImpact(in BodyDeform.Striker striker, float severity)
    {
        OnImpact(striker, severity, BodyDeform.RigidPartner);
    }

    // `share` is how much of this one contact THIS body absorbs. Both cars in a contact run this against
    // each other, so handing each of them the whole thing folds one impact's metal twice and opens a void
    // between two cars that are meant to be welded together. See BodyDeform.Share.
    public void OnImpact(in BodyDeform.Striker striker, float severity, float share)
    {
        if (_mesh == null || _base == null || _disp == null || severity < minSeverity) return;

        float damageMult = Mathf.Max(0f, TrackConditions.DamageMultiplier);
        share = Mathf.Clamp01(share) * damageMult;
        if (share <= 0f) return; // invulnerable bodywork — no dents, no damage, no bias

        // The press is measured in world metres (that is where the striker's body lives); the mesh lives in
        // this transform's local frame. Carrying one world unit through the transform gives both the local
        // fold direction and the scale between the two frames in one go.
        Vector3 localStep = transform.InverseTransformVector(new Vector3(striker.inward.x, striker.inward.y, 0f));
        if (localStep.sqrMagnitude < 1e-12f) return;
        float localToWorld = 1f / Mathf.Max(1e-4f, localStep.magnitude);

        // VIRTUAL intrusion, on top of however far the two bodies genuinely overlap. It exists for contacts
        // the solver has already pulled apart — a race, where the cars are ejected every step and there is
        // no real overlap left to read, so closing speed has to buy the fold instead. Where two bodies stay
        // inside each other on purpose this should be zero and the real burial does the work; anything more
        // folds metal nothing is occupying and pushes the two panels apart by exactly that much.
        //
        // dentStrength stays in the LOCAL units it has always been authored in, so every tuned value in the
        // project keeps meaning what it did.
        float press = dentStrength * Mathf.Clamp01(severity) * damageMult * localToWorld;

        System.Array.Clear(_region, 0, _region.Length);
        for (int i = 0; i < _current.Length; i++) _disp[i] = _current[i] - _base[i];

        bool changed = false;
        float biasNum = 0f, biasDen = 0f;

        // The fold is a TARGET, not an increment, and it is measured against where the panel started.
        //
        // Probing the already-folded position and adding to it converges on the wrong answer: each press
        // reads the intrusion that is LEFT and folds a share of that, so press the same contact enough times
        // and the shares wash out — the panel ends up fully conformed to the other car's silhouette however
        // the split was set. Which is what a crush window does: it presses every frame for half a second.
        // Measuring from the base position instead makes a press idempotent, so holding two cars together
        // deepens the fold only as far as they are actually inside each other.
        Vector3 foldDir = localStep.normalized;
        float worldToLocal = localStep.magnitude;   // local units per world metre

        for (int i = 0; i < _current.Length; i++)
        {
            float weight = _deformWeight != null ? _deformWeight[i] : 1f;
            if (weight <= 0f) continue; // rigid core shell — never bends

            Vector2 world = transform.TransformPoint(_base[i]);
            float t = BodyDeform.Intrusion(striker, world, press);
            if (t <= 0f) continue;

            // Our share of the intrusion, in local units. Anything an earlier hit already folded deeper
            // along this line stays — damage accumulates, it just doesn't accumulate against itself.
            float target = t * share * weight * worldToLocal;
            float already = Vector3.Dot(_disp[i], foldDir);
            if (target <= already) continue;

            _disp[i] += foldDir * (target - already);
            _region[i] = true;
            changed = true;

            // Which side of the car took it, weighted by how hard — drives the steering pull.
            biasNum += _base[i].x * t;
            biasDen += t;
        }

        if (!changed) return;

        // Buckle the surrounding panel, then commit, clamping each vertex's total set from base.
        BodyDeform.Dilate(_region, gridX + 1, gridY + 1, ref _regionScratch);
        BodyDeform.Crumple(_disp, gridX + 1, gridY + 1, _deformWeight, _region,
                           crumpleSpread, crumplePasses, ref _dispScratch);

        for (int i = 0; i < _current.Length; i++)
        {
            if (!_region[i]) continue;
            float weight = _deformWeight != null ? _deformWeight[i] : 1f;
            Vector3 fromBase = _disp[i];
            float vertexMaxDent = maxDent * weight;
            if (fromBase.magnitude > vertexMaxDent) fromBase = fromBase.normalized * vertexMaxDent;
            _current[i] = _base[i] + fromBase;
        }

        _mesh.vertices = _current;
        _mesh.RecalculateBounds();

        // Accumulate a 0..1 damage level + a left/right bias (from where the fold landed) for handling effects.
        DamageLevel = Mathf.Clamp01(DamageLevel + Mathf.Clamp01(severity) * damageAccrual * TrackConditions.DamageMultiplier);
        if (biasDen > 1e-6f) _biasAccum += (biasNum / biasDen) * Mathf.Clamp01(severity);
        DamageBiasX = Mathf.Clamp(_biasAccum, -1f, 1f);
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
