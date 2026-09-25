using Draftmaster.Sim;
using UnityEngine;

// The ground under the title screen: we are looking straight down on the outside of a racetrack. Grass on the
// left under the menu and logo, the outer wall running top to bottom at the middle of the screen, asphalt on
// the right where TitleCrashScene throws its cars, and the finish line painted across the track.
//
// Same materials as the spline tracks (Watkins Glen's: Grass, TrackSurface, White barrier, FinishLine) at the
// same 12.8 texture px per metre, and the same metre as the crash — TitleCrash.CarLengthPx canvas pixels is a
// 5 m car — so the cars sit on the tarmac at the size they would on a real circuit.
//
// Built as world-space quads BEHIND the crash (sorting orders well under its baseSortingOrder), not as UI:
// the title canvas draws in front of the cars, so a canvas image would cover them.
//
// Runs in edit mode too, so the scene doesn't open onto a black screen. The meshes and their GameObjects are
// DontSave: nothing generated here is written into TitleScreen.unity, only this component and its settings.
[ExecuteAlways]
public class TitleTrackBackdrop : MonoBehaviour
{
    [Header("Materials (Watkins Glen's)")]
    public Material grass;
    public Material asphalt;
    public Material wall;
    public Material finishLine;

    [Header("Layout")]
    [Tooltip("Where the wall's centre sits, as a fraction of the screen's width from the left.")]
    [Range(0f, 1f)] public float wallAt = 0.5f;
    [Tooltip("Wall thickness in metres when there is no SAFER barrier style to draw (a plain strip of `wall`). " +
             "With the style, the SAFER barrier's own width is used.")]
    public float wallWidthMetres = 1f;
    [Tooltip("Where the finish line's centre sits, as a fraction of the screen's height from the TOP.")]
    [Range(0f, 1f)] public float finishLineAt = 0.72f;
    [Tooltip("Finish line depth along the track, in metres. finish.png is two chequers (16 px = 1.25 m) deep and " +
             "clamped, so anything deeper stretches its last row.")]
    public float finishLineMetres = 1.25f;

    [Header("Draw order")]
    [Tooltip("Distance behind the crash's plane. Positive = further from the camera.")]
    public float depthZ = 1f;
    public int groundSortingOrder = -200;
    public int finishLineSortingOrder = -190;
    public int wallSortingOrder = -180;

    Camera _camera;
    Vector2 _builtFor;              // (aspect, orthographic size) the quads were last laid out for
    Transform _root;
    readonly Mesh[] _meshes = new Mesh[4];

    // Built straight away rather than on the first Update: in edit mode Update only runs when something in
    // the scene changes, so a scene opened (or a capture taken) before then would show no ground at all.
    void OnEnable()
    {
        _builtFor = Vector2.zero;
        EnsureBuilt();
    }

    void OnDisable() => Clear();

    // Can't build here (no object creation inside OnValidate), so ask the editor for a tick instead.
    void OnValidate()
    {
        _builtFor = Vector2.zero;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    // Also rebuilds when the screen changes shape (a window resize, the simulator rotating).
    void Update() => EnsureBuilt();

    // Lay it out again now, whether or not the screen changed shape — after the materials or the SAFER barrier
    // style have changed underneath it.
    public void Rebuild()
    {
        _builtFor = Vector2.zero;
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        if (_camera == null) _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (_camera == null || !_camera.orthographic) return;

        var key = new Vector2(_camera.aspect, _camera.orthographicSize);
        if (key == _builtFor && _root != null) return;
        _builtFor = key;
        Build();
    }

    void Build()
    {
        Clear();
        _root = new GameObject("TrackBackdrop (generated)").transform;
        _root.gameObject.hideFlags = HideFlags.DontSave;
        _root.SetParent(transform, false);

        // One metre in world units: the screen is TitleCrash.CanvasHeight canvas pixels tall, and a car of
        // CarLengthPx pixels is 5 m.
        float pxPerMetre = TitleCrash.CarLengthPx / 5f;
        float halfH = _camera.orthographicSize;
        float halfW = halfH * _camera.aspect;
        float m = halfH * 2f / TitleCrash.CanvasHeight * pxPerMetre;   // world units per metre

        // The screen in metres, centred on the camera.
        float right = halfW / m, top = halfH / m;
        float left = -right, bottom = -top;

        float wallMid = Mathf.Lerp(left, right, wallAt);
        float wallW = BarrierWidthMetres;
        float wallL = wallMid - wallW * 0.5f, wallR = wallMid + wallW * 0.5f;   // wallR = the track-side face
        float finishMid = Mathf.Lerp(top, bottom, finishLineAt);
        float finishB = finishMid - finishLineMetres * 0.5f, finishT = finishMid + finishLineMetres * 0.5f;

        // Grass and asphalt: world-anchored, UV = metres x density, like the circuits' road and runoff. The tarmac
        // runs on under the barrier to its back face, as it did in the old game: the absorbers are cut-out blocks,
        // and what shows between them should be the track's surface, not a strip of lawn.
        Quad(0, "Grass", grass, groundSortingOrder, left, bottom, wallL, top, m, ribbon: false);
        Quad(1, "Asphalt", asphalt, groundSortingOrder, wallL, bottom, right, top, m, ribbon: false);
        // Ribbons: U across, V along, both in metres from the strip's own corner — the chequer and the
        // barrier's stripes come out the same physical size they do on the track. The track runs up the
        // screen, so the wall runs along y and the finish line runs across it, wall to right edge.
        // finish.png runs its short axis (U, two chequers) along the track and its long one (V) across it, so
        // on a track that runs up the screen its U follows y and its V follows x.
        Quad(2, "FinishLine", finishLine, finishLineSortingOrder, wallR, finishB, right, finishT, m, ribbon: true,
             swapUv: true);
        var safer = SaferBarrierStyle.Default;
        if (safer != null && safer.WallWidthMetres() > 0f)
        {
            // The same SAFER barrier the circuits are walled with. Built in metres under a transform that scales
            // metres to world units; the line is its track-side face, running up the screen, so the track is on
            // its right (+x) and the catch fence leans out over the tarmac.
            var wallGo = new GameObject("OuterWall");
            wallGo.hideFlags = HideFlags.DontSave;
            wallGo.transform.SetParent(_root, false);
            var centre = _camera.transform.position;
            wallGo.transform.position = new Vector3(centre.x, centre.y, depthZ);
            wallGo.transform.localScale = new Vector3(m, m, 1f);
            safer.Build(wallGo.transform, new[] { new Vector2(wallR, bottom), new Vector2(wallR, top) },
                        trackSign: 1, faceOffset: 0f, sortingOrder: wallSortingOrder);
        }
        else Quad(3, "OuterWall", wall, wallSortingOrder, wallL, bottom, wallR, top, m, ribbon: true);
    }

    // How much ground the wall covers, grass edge to track face: the SAFER barrier's own width when there is one.
    public float BarrierWidthMetres
    {
        get
        {
            var safer = SaferBarrierStyle.Default;
            float w = safer != null ? safer.WallWidthMetres() : 0f;
            return w > 0f ? w : wallWidthMetres;
        }
    }

    // A rectangle in screen metres (x0,y0)-(x1,y1), drawn `depthZ` behind the crash.
    void Quad(int slot, string name, Material material, int order, float x0, float y0, float x1, float y1,
              float worldPerMetre, bool ribbon, bool swapUv = false)
    {
        if (material == null) return;

        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(_root, false);
        var centre = _camera.transform.position;
        go.transform.position = new Vector3(centre.x, centre.y, depthZ);

        Vector2 d = PixelArt.UvScale(material);
        Vector2 uv0, uv1;
        if (ribbon) { uv0 = Vector2.zero; uv1 = new Vector2((x1 - x0) * d.x, (y1 - y0) * d.y); }
        else { uv0 = new Vector2(x0 * d.x, y0 * d.y); uv1 = new Vector2(x1 * d.x, y1 * d.y); }

        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        float s = worldPerMetre;
        mesh.vertices = new[]
        {
            new Vector3(x0 * s, y0 * s, 0f), new Vector3(x1 * s, y0 * s, 0f),
            new Vector3(x0 * s, y1 * s, 0f), new Vector3(x1 * s, y1 * s, 0f),
        };
        // Swapped: U runs up the quad (y) and V across it (x).
        if (swapUv) uv1 = new Vector2((y1 - y0) * d.x, (x1 - x0) * d.y);
        mesh.uv = swapUv
            ? new[] { uv0, new Vector2(uv0.x, uv1.y), new Vector2(uv1.x, uv0.y), uv1 }
            : new[] { uv0, new Vector2(uv1.x, uv0.y), new Vector2(uv0.x, uv1.y), uv1 };
        // Wound to face the camera looking down +z; RecalculateNormals then points them at it.
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        _meshes[slot] = mesh;

        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void Clear()
    {
        // Every mesh under the root is generated and DontSave (the SAFER strips included, which this class
        // doesn't track by slot), so free them all rather than leaking a set per rebuild.
        if (_root != null)
            foreach (var mf in _root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || System.Array.IndexOf(_meshes, mesh) >= 0) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
        if (_root != null)
        {
            if (Application.isPlaying) Destroy(_root.gameObject);
            else DestroyImmediate(_root.gameObject);
        }
        _root = null;
        for (int i = 0; i < _meshes.Length; i++)
        {
            if (_meshes[i] == null) continue;
            if (Application.isPlaying) Destroy(_meshes[i]);
            else DestroyImmediate(_meshes[i]);
            _meshes[i] = null;
        }
    }
}
