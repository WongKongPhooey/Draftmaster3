using System.Collections.Generic;
using UnityEngine;

// Corner-of-screen track mini-map for the spline scenes. The main (and pit) centerline is baked
// once into a small texture — cream main loop, dim pit lane, notch at the start/finish — and the
// live cars are plotted over it every frame: dim square pips for AI, a bigger gold one for the
// player's car. World-aligned (no rotation), so it matches what the top-down camera shows.
//
// Iron Oval furniture: the map sits on the kit's framed plate, and the pips are hard-edged squares
// rather than soft circles — an anti-aliased dot next to pixel art reads as a rendering error.
//
// Self-bootstraps like HandlingTuner/RacingLineDisplay: builds only in scenes with a TrackBuilder,
// no wiring needed. Toggled from the pause menu; state persists in PlayerPrefs.
public class TrackMiniMap : MonoBehaviour
{
    public static TrackMiniMap Instance { get; private set; }

    const string PrefKey = "ShowMiniMap";

    [Header("Layout (UI pixels, before PixelGUI.Scale)")]
    [Tooltip("Mini-map square size.")]
    public float mapSize = 104f;
    [Tooltip("Distance from the screen's bottom-right corner.")]
    public Vector2 cornerMargin = new Vector2(8f, 8f);
    [Tooltip("Height (in the HUD's 1920x1080 reference units) to keep clear at the bottom right for the " +
             "speedometer dial, which is anchored there: 30 margin + 220 dial + 16 gap. Scaled to the real " +
             "screen the same way the HUD canvas scales, so the map clears the dial at any resolution.")]
    public float speedoClearance = 266f;

    [Header("Dots (UI pixels, before PixelGUI.Scale)")]
    public float aiDotSize = 3f;
    public float playerDotSize = 5f;
    [Tooltip("Left at clear, these take the theme's colours: dim for the field, accent gold for the player.")]
    public Color aiDotColor = new Color(0f, 0f, 0f, 0f);
    public Color playerDotColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Also theme-defaulted when left at clear: the pit lane draws dim, the racing surface in the " +
             "primary text colour.")]
    public Color pitLaneColor = new Color(0f, 0f, 0f, 0f);
    public Color trackLineColor = new Color(0f, 0f, 0f, 0f);

    TrackBuilder _builder;
    Texture2D _mapTex;
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
        // Point filtering: the map is drawn at a whole-number scale like everything else in the kit, and
        // a bilinear track line smears into a grey haze at that size.
        _mapTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
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

        for (int i = 0; i < pit.Count; i++)
            Stamp(_builder.transform.TransformPoint(pit[i].position), Resolve(pitLaneColor, PixelGUI.TextDisabled), 1);
        for (int i = 0; i < main.Count; i++)
            Stamp(_builder.transform.TransformPoint(main[i].position), Resolve(trackLineColor, PixelGUI.Text), 2);
        // Start/finish notch, slightly fatter than the line. Alarm red is the one place it is used here,
        // and it is a landmark rather than decoration.
        Stamp(_builder.transform.TransformPoint(main[0].position), PixelGUI.Danger, 4);

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

        // Sat above the speedometer rather than on top of it. This is IMGUI in raw screen pixels while the
        // dial is on a scaled canvas, so the clearance is converted with the same factor CanvasScaler uses
        // (Scale With Screen Size, 1920x1080, match 0.5 — the geometric mean of the two ratios).
        float size = PixelGUI.Px(mapSize);
        var rect = new Rect(
            Screen.width - size - PixelGUI.Px(cornerMargin.x),
            Screen.height - size - PixelGUI.Px(cornerMargin.y) - speedoClearance * HudScale(),
            size, size);

        // The baked map is transparent apart from its lines, and over a light piece of track it vanished.
        // The kit's plate gives it a constant backing; the frame is what makes it read as an instrument
        // rather than a decal.
        float pad = PixelGUI.Px(4f);
        PixelGUI.Panel(new Rect(rect.x - pad, rect.y - pad, rect.width + pad * 2f, rect.height + pad * 2f));
        GUI.color = Color.white;
        GUI.DrawTexture(rect, _mapTex);

        for (int i = 0; i < _aiCars.Length; i++)
        {
            var car = _aiCars[i];
            if (car == null || !car.isActiveAndEnabled) continue;
            // The player's car also carries a SplineDriver (AI hand-off) — skip it here, it gets the green dot.
            if (_playerCar != null && car.transform == _playerCar.transform) continue;
            DrawDot(rect, car.transform.position, Resolve(aiDotColor, PixelGUI.TextDim), aiDotSize);
        }
        if (_playerCar != null)
            DrawDot(rect, _playerCar.transform.position, Resolve(playerDotColor, PixelGUI.Gold), playerDotSize);
        GUI.color = Color.white;
    }

    // What CanvasScaler does to the HUD at this resolution, so a measurement taken off the speedometer's
    // RectTransform means the same thing here.
    static float HudScale() =>
        Mathf.Sqrt(Mathf.Max(0.0001f, (Screen.width / 1920f) * (Screen.height / 1080f)));

    // An unset (fully transparent) inspector colour means "use the theme", so the palette stays in one
    // place while a scene can still override a pip by hand.
    static Color Resolve(Color authored, Color themed) => authored.a > 0f ? authored : themed;

    void DrawDot(Rect mapRect, Vector2 world, Color color, float size)
    {
        Vector2 uv = WorldToMap01(world);
        if (uv.x < -0.05f || uv.x > 1.05f || uv.y < -0.05f || uv.y > 1.05f) return;
        float s = PixelGUI.Px(size);
        // Whole pixels, or a pip lands on a half-pixel and blurs against everything else on the screen.
        float x = Mathf.Round(mapRect.x + uv.x * mapRect.width - s * 0.5f);
        float y = Mathf.Round(mapRect.y + (1f - uv.y) * mapRect.height - s * 0.5f); // GUI space runs y-down
        // 1px ink surround, so a pip stays visible where it crosses a track line of its own colour.
        float b = PixelGUI.Px(1f);
        PixelGUI.Fill(new Rect(x - b, y - b, s + b * 2f, s + b * 2f), PixelGUI.Ink);
        PixelGUI.Fill(new Rect(x, y, s, s), color);
    }

}
