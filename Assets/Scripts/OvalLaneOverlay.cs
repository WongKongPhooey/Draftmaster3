using System.Collections.Generic;
using UnityEngine;

// Play-mode overlay of the two fixed lanes an oval is raced in (Draftmaster.Tracks.OvalLanes, Docs/Oval-Lanes.md):
// a thin cyan line for the inside lane, magenta for the outside lane - the same colours as TrackBuilder's scene
// gizmo. Only on an oval; a road course has no lanes and draws nothing.
//
// Self-bootstraps like RacingLineDisplay: no scene wiring. Toggled from the pause menu ("Oval lanes", shown only
// on an oval); the on/off state persists in PlayerPrefs.
public class OvalLaneOverlay : MonoBehaviour
{
    public static OvalLaneOverlay Instance { get; private set; }

    const string PrefKey = "ShowOvalLanes";

    [Tooltip("Width (m) of each lane line.")]
    public float lineWidth = 0.5f;
    [Range(0.1f, 1f)] public float alpha = 0.8f;
    [Tooltip("Local z relative to the track mesh. Negative = toward the camera, above the road and the racing-line ribbon.")]
    public float zOffset = -0.03f;
    [Tooltip("Sorting order: above the racing-line ribbon (2), below the brake marker boards (3).")]
    public int sortingOrder = 2;
    public Color insideColor = new Color(0.1f, 0.85f, 1f);
    public Color outsideColor = new Color(1f, 0.35f, 0.9f);

    TrackBuilder _builder;        // the track the overlay was last built (or found to have no lanes) for
    GameObject _go;
    MeshRenderer _renderer;
    float _pollTimer;

    // On/off, surfaced in the pause menu. Persisted; applies live.
    public static bool Visible
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            if (Instance != null) Instance.ApplyVisibility();
        }
    }

    // Whether the track on screen has lanes to draw (the pause menu only offers the toggle then).
    public static bool Available => Instance != null && Instance._go != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("OvalLaneOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<OvalLaneOverlay>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer > 0f) return;
        _pollTimer = 0.5f;

        if (_renderer != null) _renderer.enabled = Visible;

        // Built for this track already (or it has no lanes): nothing to do until the track changes.
        var builder = _builder != null && _builder.track != null ? _builder : FindAnyObjectByType<TrackBuilder>();
        if (builder == null || builder.track == null) return;
        if (builder == _builder) return;

        if (_go != null) Destroy(_go);
        _go = null;
        _renderer = null;
        _builder = builder;
        Build();
    }

    public void ApplyVisibility()
    {
        if (_renderer != null) _renderer.enabled = Visible;
    }

    void Build()
    {
        var samples = _builder.SampleCenterline();
        if (!_builder.TryGetOvalLanes(samples, out var inside, out var outside)) return;

        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();
        AddLine(samples, inside, insideColor, _builder.track.closedLoop, verts, cols, tris);
        AddLine(samples, outside, outsideColor, _builder.track.closedLoop, verts, cols, tris);

        var mesh = new Mesh { name = $"OvalLanes_{_builder.track.name}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        _go = new GameObject("OvalLaneOverlay");
        _go.transform.SetParent(_builder.transform, false);
        _go.transform.localPosition = new Vector3(0f, 0f, zOffset);
        _go.AddComponent<MeshFilter>().sharedMesh = mesh;
        _renderer = _go.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = Material();
        _renderer.sortingOrder = sortingOrder;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.enabled = Visible;
    }

    // One flat strip along `lateral`, wound both ways so it shows whichever way the camera looks at it.
    void AddLine(List<TrackBuilder.Sample> samples, float[] lateral, Color color, bool loop,
                 List<Vector3> verts, List<Color> cols, List<int> tris)
    {
        int n = samples.Count;
        int count = loop ? n + 1 : n;
        int baseIndex = verts.Count;
        float half = lineWidth * 0.5f;
        Color c = color; c.a = alpha;
        for (int i = 0; i < count; i++)
        {
            var s = samples[i % n];
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            Vector2 p = s.position + right * lateral[i % n];
            verts.Add(new Vector3(p.x - right.x * half, p.y - right.y * half, 0f));
            verts.Add(new Vector3(p.x + right.x * half, p.y + right.y * half, 0f));
            cols.Add(c); cols.Add(c);
            if (i == 0) continue;
            int a = baseIndex + (i - 1) * 2, b = baseIndex + i * 2;
            tris.Add(a); tris.Add(b); tris.Add(b + 1);
            tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
            tris.Add(a); tris.Add(b + 1); tris.Add(b);
            tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
        }
    }

    static Material _mat;
    static Material Material()
    {
        if (_mat != null) return _mat;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _mat = new Material(sh) { name = "OvalLaneOverlay" };
        return _mat;
    }
}
