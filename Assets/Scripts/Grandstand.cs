using UnityEngine;

// Trackside crowd grandstand: a flat quad tiled with a repeating crowd texture, sized in metres.
// [ExecuteAlways] like TrackBuilder — drop in the scene, assign the texture, set length/depth, and
// rotate the GameObject to run parallel with the track. UVs repeat at the texture's native pixel
// density (metresPerRepeat horizontally, vertical derived from the aspect) so the crowd blocks stay
// square no matter the stand's size. Uses the sprite-unlit shader (cull off) so it renders under
// this project's 3D URP renderer from either winding.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Grandstand : MonoBehaviour
{
    [Tooltip("Tileable crowd texture (e.g. Textures/Props/crowd-phoenix). Import wrap mode must be Repeat.")]
    public Texture2D crowdTexture;
    [Tooltip("Length of the stand (m), along local +X.")]
    public float length = 120f;
    [Tooltip("Depth of the stand (m), along local +Y.")]
    public float depth = 12f;
    [Tooltip("Leave on to size the crowd at the project pixel standard (PixelArt.PixelsPerMetre), so a crowd " +
             "pixel is the same size as a car pixel. Turn off only for a deliberate forced-perspective stand.")]
    public bool usePixelStandard = true;
    [Tooltip("World metres covered by ONE horizontal repeat of the texture. Ignored when usePixelStandard is on, " +
             "where it is derived as textureWidth / PixelArt.PixelsPerMetre.")]
    public float metresPerRepeat = 12f;
    [Tooltip("Flip the texture vertically — for stands on the far side of the track, so the rows face it.")]
    public bool flipFacing;
    [Tooltip("Bend the stand into a circular arc that turns this many degrees end to end, so it follows a curved " +
             "piece of road (Daytona's tri-oval). Positive bends the ends toward local +Y. 0 = a straight stand. " +
             "Length is measured along the bend; the transform sits at the middle of the arc.")]
    public float bendDegrees;
    [Tooltip("Metres the crowd texture is already into its repeat at the stand's local -X end, so stands laid end " +
             "to end carry one unbroken crowd instead of restarting the pattern at every joint.")]
    public float textureOffset;
    [Tooltip("Above the grass (0) but below track furniture like marker boards (3).")]
    public int sortingOrder = 2;
    [Tooltip("Keep wandering NPCs off the seating. The crowd in a stand is drawn INTO the texture, so a " +
             "paddock walker strolling across it reads as somebody stood on top of the painted crowd. Turn " +
             "off only for a stand people are meant to walk on — a ground-level viewing bank.")]
    public bool keepCrowdOff = true;
    [Tooltip("Make the stand a solid block the on-foot player walks round rather than across. It is opened " +
             "for as long as the player is sat in a stand watching a session (GrandstandVisit), since the gate " +
             "puts them down inside it. Turn off for a ground-level viewing bank people are meant to walk on.")]
    public bool solid = true;

    // What the keep-out volume is called, so a rebuild finds the one it made last time.
    public const string KeepOutName = "CrowdKeepOut";

    // What the solid block is called, likewise.
    public const string StructureName = "Structure";

    // Every stand's solid block, so a visit can open them all at once and a stand built mid-visit comes up
    // open too. Stands are few (a handful per circuit), so a plain list is all this needs.
    static readonly System.Collections.Generic.List<BoxCollider2D> _structures = new();
    static bool _openToPlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _structures.Clear();
        _openToPlayer = false;
    }

    // Let the player into (or shut them out of) every stand. GrandstandVisit opens them while the player is
    // sat in one: the gate drops them on a seat inside the footprint, and a solid block round them would
    // shove them back out of it on the next physics step. The crowd's keep-out is separate and stays down.
    public static void SetOpenToPlayer(bool open)
    {
        _openToPlayer = open;
        for (int i = _structures.Count - 1; i >= 0; i--)
        {
            if (_structures[i] == null) { _structures.RemoveAt(i); continue; }
            _structures[i].enabled = !open;
        }
    }

    public static bool OpenToPlayer => _openToPlayer;

    Mesh _mesh;
    Material _mat;

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

    public void Build()
    {
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();

        float hx = length * 0.5f, hy = depth * 0.5f;

        // One repeat spans however many metres the texture's width covers at the project standard, so the
        // crowd's pixels match the car's. At 12.8 px/m a 128px crowd strip repeats every 10m.
        float repeat = metresPerRepeat;
        if (usePixelStandard && crowdTexture != null)
            repeat = crowdTexture.width / PixelArt.PixelsPerMetre;

        float uRep = length / Mathf.Max(0.1f, repeat);
        float vRep = 1f;
        if (crowdTexture != null)
        {
            float pixelsPerMetre = crowdTexture.width / Mathf.Max(0.1f, repeat);
            vRep = depth * pixelsPerMetre / Mathf.Max(1, crowdTexture.height);
        }
        float v0 = flipFacing ? vRep : 0f;
        float v1 = flipFacing ? 0f : vRep;
        float u0 = textureOffset / Mathf.Max(0.1f, repeat);

        _mesh = new Mesh { name = "Grandstand" };
        if (Mathf.Approximately(bendDegrees, 0f))
        {
            _mesh.vertices = new[]
            {
                new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f),
                new Vector3(hx, hy, 0f), new Vector3(-hx, hy, 0f)
            };
            _mesh.uv = new[]
            {
                new Vector2(u0, v0), new Vector2(u0 + uRep, v0),
                new Vector2(u0 + uRep, v1), new Vector2(u0, v1)
            };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }
        else
        {
            // A strip of quads round the arc, one every few metres - plenty for a bend of a few degrees. The
            // crowd is mapped by distance along the middle of the stand, so the rows run round the curve.
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / 5f), 2, 64);
            var verts = new Vector3[(steps + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[steps * 6];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                ArcPoint(-hx + length * t, out Vector2 p, out Vector2 n);
                verts[i * 2] = p - n * hy;
                verts[i * 2 + 1] = p + n * hy;
                uvs[i * 2] = new Vector2(u0 + uRep * t, v0);
                uvs[i * 2 + 1] = new Vector2(u0 + uRep * t, v1);
                if (i == steps) continue;
                int b = i * 2, k = i * 6;
                tris[k] = b; tris[k + 1] = b + 2; tris[k + 2] = b + 3;
                tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 1;
            }
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
        }
        _mesh.RecalculateBounds();
        mf.sharedMesh = _mesh;

        if (_mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _mat = new Material(sh) { name = "Grandstand" };
        }
        _mat.mainTexture = crowdTexture;
        mr.sharedMaterial = _mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Runtime only. The crowd's router is the only thing that reads the keep-out and it only runs in
        // play, whereas laying one at edit time would write a collider into all 38 track packages the next
        // time each was opened and saved. Tests call BuildKeepOut directly.
        if (Application.isPlaying)
        {
            BuildKeepOut();
            BuildStructure();
        }
    }

    // The point `s` metres along the middle of the stand from its centre, and the stand's depth direction there
    // (local +Y when straight). A bent stand is a circular arc through the origin, tangent to local +X there.
    public void ArcPoint(float s, out Vector2 point, out Vector2 depthAxis)
    {
        float k = bendDegrees * Mathf.Deg2Rad / Mathf.Max(0.01f, length);
        if (Mathf.Abs(k) < 1e-6f)
        {
            point = new Vector2(s, 0f);
            depthAxis = Vector2.up;
            return;
        }
        float a = k * s;
        point = new Vector2(Mathf.Sin(a) / k, (1f - Mathf.Cos(a)) / k);
        depthAxis = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
    }

    // The rectangle in the stand's own frame that covers it: length x depth about the origin when straight, the
    // bounds of the arc when bent (the ends swing toward +Y for a positive bend, so the box grows and shifts).
    public Rect LocalFootprint()
    {
        float hx = Mathf.Max(0.01f, length) * 0.5f, hy = Mathf.Max(0.01f, depth) * 0.5f;
        if (Mathf.Approximately(bendDegrees, 0f)) return new Rect(-hx, -hy, hx * 2f, hy * 2f);

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i <= 16; i++)
        {
            ArcPoint(-hx + hx * 2f * i / 16f, out Vector2 p, out Vector2 n);
            foreach (var q in new[] { p - n * hy, p + n * hy })
            {
                minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
                minY = Mathf.Min(minY, q.y); maxY = Mathf.Max(maxY, q.y);
            }
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    // Whether a point in the stand's own frame lies on the stand (grown by `pad` metres all round), following the
    // arc when it is bent rather than the box round it.
    public bool Covers(Vector2 local, float pad = 0f)
    {
        float hx = length * 0.5f + pad, hy = depth * 0.5f + pad;
        float k = bendDegrees * Mathf.Deg2Rad / Mathf.Max(0.01f, length);
        if (Mathf.Abs(k) < 1e-6f) return Mathf.Abs(local.x) <= hx && Mathf.Abs(local.y) <= hy;

        // About the centre of the bend, at (0, 1/k): how far off the middle line, and how far round it.
        float radius = 1f / k;
        Vector2 fromCentre = local - new Vector2(0f, radius);
        float off = Mathf.Abs(fromCentre.magnitude - Mathf.Abs(radius));
        float round = Mathf.Abs(Mathf.Atan2(fromCentre.x, -fromCentre.y * Mathf.Sign(radius))) * Mathf.Abs(radius);
        return off <= hy && round <= hx;
    }

    // Lay (or take away) the solid block over the stand's footprint, so the player walks round the seating
    // instead of straight across the painted crowd. Runtime only, for the same reason as the keep-out.
    //
    // A plain static BoxCollider2D, like a parked motorhome's: it stops the player's dynamic Rigidbody2D, and
    // PaddockObstacles reads it as scenery for the crowd as well. Cars never reach it — a stand is set back
    // behind the walls — and the keep-out beside it is unchanged, so the crowd still routes round a stand
    // whose block is open while the player is watching from it.
    public BoxCollider2D BuildStructure()
    {
        Transform existing = transform.Find(StructureName);

        if (!solid)
        {
            if (existing != null) DestroyVolume(existing.gameObject);
            return null;
        }

        // A bent stand gets the box round its arc - a few metres generous at the corners for the slight bends
        // this is used for, which is fine for a block the player walks round.
        var footprint = LocalFootprint();
        var size = footprint.size;
        BoxCollider2D box = existing != null ? existing.GetComponent<BoxCollider2D>() : null;
        if (existing == null)
        {
            var go = new GameObject(StructureName);
            go.transform.SetParent(transform, false);
            existing = go.transform;
        }
        if (box == null) box = existing.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = false;
        if (box.size != size) box.size = size;
        if (box.offset != footprint.center) box.offset = footprint.center;
        box.enabled = !_openToPlayer;

        if (!_structures.Contains(box)) _structures.Add(box);
        return box;
    }

    // Lay (or take away) the volume that keeps wandering NPCs off the seating.
    //
    // Nothing about a stand is solid: it is one flat quad with a crowd painted on it, so the ground it
    // covers reads as clear tarmac to the physics world. The paddock crowd routes around what physics can
    // see (PaddockObstacles), and so wherever a paddock reaches a stand — a short oval's infield is narrow
    // enough that the rectangle behind pit road runs across to the back straight — people wandered up the
    // seating and stood about in the middle of the painted crowd.
    //
    // Filling it in with a solid collider would shut the PLAYER out too, and watching a session means
    // walking into a stand and moving about in it (GrandstandSeat, GrandstandVisit). So the keep-out is
    // STATED instead: a PaddockNoGo trigger reads as a wall to the crowd's routing and to nothing else,
    // and a trigger never stops the player's dynamic Rigidbody2D.
    public PaddockNoGo BuildKeepOut()
    {
        Transform existing = transform.Find(KeepOutName);

        if (!keepCrowdOff)
        {
            if (existing != null) DestroyVolume(existing.gameObject);
            return null;
        }

        // The stand's whole footprint in its own frame: length along local +X, depth along local +Y (round the
        // arc for a bent stand).
        var footprint = LocalFootprint();
        var size = footprint.size;
        if (existing == null) return PaddockNoGo.Box(transform, KeepOutName, footprint.center, size);

        existing.localPosition = new Vector3(footprint.center.x, footprint.center.y, existing.localPosition.z);
        var box = existing.GetComponent<BoxCollider2D>();
        if (box != null && box.size != size) box.size = size;

        var noGo = existing.GetComponent<PaddockNoGo>();
        if (noGo == null) noGo = existing.gameObject.AddComponent<PaddockNoGo>();
        return noGo;
    }

    static void DestroyVolume(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
