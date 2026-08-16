using System.Collections.Generic;
using UnityEngine;

// Corner-of-screen track mini-map for the spline scenes. The main (and pit) centerline is baked
// once into a small texture — white main loop, grey pit lane, notch at the start/finish — and the
// live cars are plotted over it every frame: white dots for AI, a bigger green dot for the player's
// car. World-aligned (no rotation), so it matches what the top-down camera shows.
//
// Self-bootstraps like HandlingTuner/RacingLineDisplay: builds only in scenes with a TrackBuilder,
// no wiring needed. Toggled from the pause menu; state persists in PlayerPrefs.
public class TrackMiniMap : MonoBehaviour
{
    public static TrackMiniMap Instance { get; private set; }

    const string PrefKey = "ShowMiniMap";

    [Header("Layout")]
    [Tooltip("Mini-map square size (px).")]
    public float mapSize = 220f;
    [Tooltip("Distance (px) from the screen's bottom-right corner.")]
    public Vector2 cornerMargin = new Vector2(16f, 16f);
    [Tooltip("Height (in the HUD's 1920x1080 reference units) to keep clear at the bottom right for the " +
             "speedometer dial, which is anchored there: 30 margin + 220 dial + 16 gap. Scaled to the real " +
             "screen the same way the HUD canvas scales, so the map clears the dial at any resolution.")]
    public float speedoClearance = 266f;

    [Header("Dots")]
    public float aiDotSize = 6f;
    public float playerDotSize = 10f;
    public Color aiDotColor = Color.white;
    public Color playerDotColor = new Color(0.3f, 1f, 0.4f);
    public Color pitLaneColor = new Color(1f, 1f, 1f, 0.35f);
    public Color trackLineColor = new Color(1f, 1f, 1f, 0.9f);

    TrackBuilder _builder;
    Texture2D _mapTex, _dot;
    Rect _worldRect;      // world-space bounds baked into the map texture
    float _pollTimer;
    SplineDriver[] _aiCars = System.Array.Empty<SplineDriver>();
    PlayerVehicleController _playerCar;

    public static bool Visible
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set => PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("TrackMiniMap");
        DontDestroyOnLoad(go);
        go.AddComponent<TrackMiniMap>();
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
        _pollTimer = 1f;

        // Scene change: the TrackBuilder died, so the baked map is stale.
        if (_builder == null)
        {
            _mapTex = null;
            _builder = FindAnyObjectByType<TrackBuilder>();
            if (_builder == null) return;
        }
        if (_mapTex == null) BakeMap();

        // Cache the car lists (cheap to re-find once a second; positions are read live each frame).
        _aiCars = FindObjectsByType<SplineDriver>();
        if (_playerCar == null || !_playerCar.isActiveAndEnabled)
        {
            _playerCar = null;
            var pvcs = FindObjectsByType<PlayerVehicleController>();
            for (int i = 0; i < pvcs.Length; i++)
                if (pvcs[i].enabled && pvcs[i].GetComponent<SplineInputDriver>() == null) { _playerCar = pvcs[i]; break; }
        }
    }

    void BakeMap()
    {
        var main = _builder.SampleCenterline();
        if (main == null || main.Count < 2) return;
        var pit = _builder.SamplePitCenterline();

        // World bounds over both splines, padded so the line's thickness never clips.
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        void Grow(Vector2 p) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        for (int i = 0; i < main.Count; i++) Grow(_builder.transform.TransformPoint(main[i].position));
        for (int i = 0; i < pit.Count; i++) Grow(_builder.transform.TransformPoint(pit[i].position));
        Vector2 pad = (max - min) * 0.06f;
        min -= pad; max += pad;
        _worldRect = new Rect(min, max - min);

        const int N = 256;
        _mapTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var clear = new Color32(0, 0, 0, 0);
        var pxs = new Color32[N * N];
        for (int i = 0; i < pxs.Length; i++) pxs[i] = clear;

        void Stamp(Vector2 world, Color32 c, int radius)
        {
            Vector2 uv = WorldToMap01(world);
            int cx = Mathf.RoundToInt(uv.x * (N - 1));
            int cy = Mathf.RoundToInt(uv.y * (N - 1));
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= N || y >= N) continue;
                    pxs[y * N + x] = c;
                }
            }
        }

        for (int i = 0; i < pit.Count; i++) Stamp(_builder.transform.TransformPoint(pit[i].position), pitLaneColor, 1);
        for (int i = 0; i < main.Count; i++) Stamp(_builder.transform.TransformPoint(main[i].position), trackLineColor, 2);
        // Start/finish notch, slightly fatter than the line.
        Stamp(_builder.transform.TransformPoint(main[0].position), new Color(1f, 0.35f, 0.3f, 1f), 4);

        _mapTex.SetPixels32(pxs);
        _mapTex.Apply();
        Debug.Log($"[MiniMap] baked {_builder.name}: bounds {_worldRect.size.x:0}x{_worldRect.size.y:0}m");
    }

    Vector2 WorldToMap01(Vector2 world)
    {
        // Uniform scale (longest world side fills the map), centred on the shorter axis.
        float span = Mathf.Max(_worldRect.width, _worldRect.height);
        if (span <= 0f) return Vector2.zero;
        Vector2 centered = world - _worldRect.center;
        return new Vector2(0.5f + centered.x / span, 0.5f + centered.y / span);
    }

    void OnGUI()
    {
        if (!Visible || _mapTex == null || RacePauseMenu.IsPaused) return;
        EnsureAssets();

        // Sat above the speedometer rather than on top of it. This is IMGUI in raw screen pixels while the
        // dial is on a scaled canvas, so the clearance is converted with the same factor CanvasScaler uses
        // (Scale With Screen Size, 1920x1080, match 0.5 — the geometric mean of the two ratios).
        var rect = new Rect(
            Screen.width - mapSize - cornerMargin.x,
            Screen.height - mapSize - cornerMargin.y - speedoClearance * HudScale(),
            mapSize, mapSize);

        // No backdrop: the baked map is transparent apart from the lines, so it reads as an overlay on the
        // track instead of a panel bolted to the corner.
        GUI.color = Color.white;
        GUI.DrawTexture(rect, _mapTex);

        for (int i = 0; i < _aiCars.Length; i++)
        {
            var car = _aiCars[i];
            if (car == null || !car.isActiveAndEnabled) continue;
            // The player's car also carries a SplineDriver (AI hand-off) — skip it here, it gets the green dot.
            if (_playerCar != null && car.transform == _playerCar.transform) continue;
            DrawDot(rect, car.transform.position, aiDotColor, aiDotSize);
        }
        if (_playerCar != null)
            DrawDot(rect, _playerCar.transform.position, playerDotColor, playerDotSize);
        GUI.color = Color.white;
    }

    // What CanvasScaler does to the HUD at this resolution, so a measurement taken off the speedometer's
    // RectTransform means the same thing here.
    static float HudScale() =>
        Mathf.Sqrt(Mathf.Max(0.0001f, (Screen.width / 1920f) * (Screen.height / 1080f)));

    void DrawDot(Rect mapRect, Vector2 world, Color color, float size)
    {
        Vector2 uv = WorldToMap01(world);
        if (uv.x < -0.05f || uv.x > 1.05f || uv.y < -0.05f || uv.y > 1.05f) return;
        float x = mapRect.x + uv.x * mapRect.width;
        float y = mapRect.y + (1f - uv.y) * mapRect.height; // GUI space runs y-down
        GUI.color = color;
        GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), _dot);
    }

    void EnsureAssets()
    {
        if (_dot == null)
        {
            const int n = 16;
            _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(0f, 0f, 0f, 0f);
            Vector2 c = new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (n * 0.5f);
                    _dot.SetPixel(x, y, d <= 0.9f ? Color.white : clear);
                }
            _dot.Apply();
        }
    }
}
