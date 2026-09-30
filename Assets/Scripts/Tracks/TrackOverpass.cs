using UnityEngine;

// A bridge over the racetrack: scenery the cars drive underneath.
//
// Seen from straight above, "over the track" means only two things — it draws on top of the cars, and it
// is not in their way. So the deck is a transparent mesh sorted above every car and stood well towards the
// camera, and nothing here carries a Collider2D: the cars only ever meet colliders (the walls are), and the
// abutments stand outside the walls, which already stop anybody before they reach them.
//
// A shadow lies on the road beneath it, drawn under the cars, so a car passing under goes dark for a
// moment — that is what sells the deck as being up in the air rather than painted on the tarmac.
//
// A deck over the player's own car hides it for a quarter of a second at racing speed, on the exit of a
// corner where they are lining up the next one. So while the camera is over the deck it fades to
// `fadedAlpha` and comes back once the camera has moved on. The camera, not the player's car: it follows
// whoever is being watched — the player, the broadcast cut, the crew chief's pick — and it is the view that
// the deck is in the way of.
//
// Placement: with `snapToTrack` on, the bridge puts itself across the track `metresAfterLastTurn` past the
// end of the last real corner of the lap (or at `trackDistance`, if that is set), squared up to the
// track's heading and as wide as the track is there — so it is right on any venue and survives a re-trace
// of the centreline. Untick it to place the object by hand.
//
// Placeholder art: flat colour, blocked out to be driven under now and repainted later.
[ExecuteAlways]
[DisallowMultipleComponent]
public class TrackOverpass : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Put the bridge across the track automatically. Off = the transform is where it stands: local +Y " +
             "runs along the track, +X across it.")]
    public bool snapToTrack = true;

    [Tooltip("Metres past the end of the lap's last corner the bridge crosses. Ignored when Track Distance is set.")]
    public float metresAfterLastTurn = 30f;

    [Tooltip("Metres from the start of the lap to put the bridge at, exactly. Negative = use Metres After Last Turn.")]
    public float trackDistance = -1f;

    [Tooltip("A turn gentler than this (degrees) is a kink, not a corner, and is passed over when looking for the " +
             "last corner — a traced centreline is made of many small ones.")]
    public float cornerMinAngle = 20f;

    [Header("Size (metres)")]
    [Tooltip("How far the deck reaches past each edge of the track, over the runoff and the walls.")]
    public float overhang = 10f;
    [Tooltip("The deck's width along the track: how long a car is underneath it.")]
    public float deckWidth = 8f;
    [Tooltip("The footing at each end of the span, measured across the track.")]
    public float abutmentLength = 5f;
    [Tooltip("Width across the track used when there is no track to measure (Snap To Track off, or no track found).")]
    public float fallbackTrackWidth = 14f;

    [Header("Look")]
    public Color deckColour = new Color(0.62f, 0.62f, 0.60f);
    public Color railColour = new Color(0.38f, 0.39f, 0.42f);
    public Color abutmentColour = new Color(0.47f, 0.46f, 0.44f);
    [Tooltip("Where the shadow falls, metres in the track's frame (x across, y along) — as if lit from one side.")]
    public Vector2 shadowOffset = new Vector2(2f, -2.5f);
    [Range(0f, 1f)] public float shadowAlpha = 0.35f;

    [Header("Draw order")]
    [Tooltip("Sorting order of the deck. Cars draw at 5 on Default, the safety car's light at 100.")]
    public int deckSortingOrder = 60;
    [Tooltip("Sorting order of the shadow: above the road, below the cars.")]
    public int shadowSortingOrder = 1;
    [Tooltip("How far towards the camera the deck stands (metres). The camera looks down +Z from above.")]
    public float deckHeight = 3f;

    [Header("See-through while under it")]
    [Range(0f, 1f)] public float fadedAlpha = 0.35f;
    [Tooltip("Metres beyond the deck's edge that already count as under it, so the fade is under way before " +
             "the car goes in.")]
    public float fadeMargin = 6f;
    [Tooltip("Seconds for the deck to fade out or back.")]
    public float fadeSeconds = 0.15f;

    const string BuiltName = "Overpass (generated)";
    const float ShadowZ = -0.06f;   // just off the road; see the runtime-quad-mesh note on the Ground plane

    Transform _built;
    Material _deckMat, _railMat, _abutmentMat, _shadowMat;
    float _span;        // across the track, deck only
    float _alpha = 1f;

    void OnEnable() => Rebuild();

    void OnDisable() => Clear();

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        // The prefab asset on disk (OnValidate fires for it too) has no scene to build into.
        if (!gameObject.scene.IsValid()) return;

        Clear();

        float trackWidth = fallbackTrackWidth;
        if (snapToTrack && TryPlaceOnTrack(out Vector3 at, out Quaternion facing, out float width))
        {
            transform.SetPositionAndRotation(at, facing);
            trackWidth = width;
        }

        _span = trackWidth + 2f * Mathf.Max(0f, overhang);
        Build();
    }

    // Where the bridge crosses, from the track this object belongs to: the package's own TrackBuilder, or
    // the one in the scene if it is not under a package.
    bool TryPlaceOnTrack(out Vector3 at, out Quaternion facing, out float width)
    {
        at = transform.position;
        facing = transform.rotation;
        width = fallbackTrackWidth;

        var builder = FindTrack();
        if (builder == null || builder.track == null || builder.track.segments == null) return false;

        var segs = builder.track.segments;
        float d = trackDistance >= 0f ? trackDistance : DistanceAfterLastCorner(segs, cornerMinAngle, metresAfterLastTurn);
        if (d < 0f) return false;

        var s = builder.SampleAt(d);
        Vector3 w = builder.transform.TransformPoint(new Vector3(s.position.x, s.position.y, 0f));
        Vector3 along = builder.transform.TransformDirection(new Vector3(s.tangent.x, s.tangent.y, 0f));
        at = new Vector3(w.x, w.y, transform.position.z);
        facing = Quaternion.LookRotation(Vector3.forward, new Vector3(along.x, along.y, 0f).normalized);
        width = s.width > 0f ? s.width : fallbackTrackWidth;
        return true;
    }

    // Metres from the start of the lap to `after` metres past the end of the last segment that is a real
    // corner. -1 if the lap has none. Pure, so it can be tested.
    public static float DistanceAfterLastCorner(TrackInfoV2.TrackSegment[] segs, float minAngle, float after)
    {
        if (segs == null) return -1f;
        float total = 0f, end = -1f;
        for (int i = 0; i < segs.Length; i++)
        {
            total += Mathf.Max(0f, segs[i].length);
            if (segs[i].type == TrackInfoV2.SegmentType.Turn && Mathf.Abs(segs[i].angle) >= minAngle) end = total;
        }
        if (end < 0f) return -1f;
        float d = end + Mathf.Max(0f, after);
        return total > 0f ? d % total : d;
    }

    TrackBuilder FindTrack()
    {
        var package = GetComponentInParent<TrackPackage>();
        if (package != null)
        {
            var own = package.GetComponentInChildren<TrackBuilder>(true);
            if (own != null) return own;
        }
        return FindFirstObjectByType<TrackBuilder>();
    }

    // ------------------------------------------------------------------ building

    void Build()
    {
        var root = new GameObject(BuiltName);
        // Generated every time the bridge is enabled, so never saved into the package or the scene: the
        // component and its numbers are the authored thing, the meshes are output.
        root.hideFlags = HideFlags.DontSave;
        root.transform.SetParent(transform, false);
        _built = root.transform;

        _deckMat = Mat("Overpass Deck", deckColour);
        _railMat = Mat("Overpass Rail", railColour);
        _abutmentMat = Mat("Overpass Abutment", abutmentColour);
        _shadowMat = Mat("Overpass Shadow", new Color(0f, 0f, 0f, shadowAlpha));

        float hs = _span * 0.5f, hd = deckWidth * 0.5f;
        float deckZ = -Mathf.Abs(deckHeight);

        // Shadow first, on the road: the deck plus its footings, thrown off to one side.
        Quad("Shadow", shadowOffset, new Vector2(_span + 2f * abutmentLength, deckWidth), ShadowZ, _shadowMat,
             shadowSortingOrder);

        // The footings, one past each end of the span.
        Quad("Abutment_L", new Vector2(-hs - abutmentLength * 0.5f, 0f), new Vector2(abutmentLength, deckWidth + 2f),
             deckZ, _abutmentMat, deckSortingOrder);
        Quad("Abutment_R", new Vector2(hs + abutmentLength * 0.5f, 0f), new Vector2(abutmentLength, deckWidth + 2f),
             deckZ, _abutmentMat, deckSortingOrder);

        // The deck, and a parapet down each long side so it reads as a bridge rather than a slab.
        Quad("Deck", Vector2.zero, new Vector2(_span, deckWidth), deckZ, _deckMat, deckSortingOrder + 1);
        const float rail = 0.6f;
        Quad("Parapet_Near", new Vector2(0f, -hd + rail * 0.5f), new Vector2(_span, rail), deckZ - 0.01f, _railMat,
             deckSortingOrder + 2);
        Quad("Parapet_Far", new Vector2(0f, hd - rail * 0.5f), new Vector2(_span, rail), deckZ - 0.01f, _railMat,
             deckSortingOrder + 2);

        _alpha = 1f;
        ApplyAlpha();
    }

    void Clear()
    {
        if (_built != null) Kill(_built.gameObject);
        else
        {
            var stale = transform.Find(BuiltName);
            if (stale != null) Kill(stale.gameObject);
        }
        _built = null;
        Kill(_deckMat); Kill(_railMat); Kill(_abutmentMat); Kill(_shadowMat);
        _deckMat = _railMat = _abutmentMat = _shadowMat = null;
    }

    void Quad(string name, Vector2 centre, Vector2 size, float z, Material mat, int order)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(_built, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, z);

        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        var mesh = new Mesh { name = "Overpass_" + name, hideFlags = HideFlags.DontSave };
        mesh.vertices = new[]
        {
            new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f), new Vector3(hx, hy, 0f), new Vector3(-hx, hy, 0f),
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        // Both windings: seen from the top-down camera one of them is always the back face.
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingLayerName = "Default";
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static Material Mat(string name, Color c)
    {
        // Sprites/Default: unlit, alpha-blended, no culling and no depth write — sorted by sorting order like
        // the cars are, which is exactly how the deck gets on top of them. Always in a build.
        var shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave, color = c };
        return m;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // ------------------------------------------------------------------ the fade

    void LateUpdate()
    {
        if (!Application.isPlaying || _built == null) return;

        float target = CameraOverDeck() ? fadedAlpha : 1f;
        float rate = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        float next = Mathf.MoveTowards(_alpha, target, rate);
        if (Mathf.Approximately(next, _alpha)) return;
        _alpha = next;
        ApplyAlpha();
    }

    bool CameraOverDeck()
    {
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 local = transform.InverseTransformPoint(cam.transform.position);
        float hx = _span * 0.5f + abutmentLength + fadeMargin;
        float hy = deckWidth * 0.5f + fadeMargin;
        return Mathf.Abs(local.x) <= hx && Mathf.Abs(local.y) <= hy;
    }

    void ApplyAlpha()
    {
        SetAlpha(_deckMat, deckColour);
        SetAlpha(_railMat, railColour);
        SetAlpha(_abutmentMat, abutmentColour);
    }

    void SetAlpha(Material m, Color c)
    {
        if (m == null) return;
        c.a *= _alpha;
        m.color = c;
    }
}
