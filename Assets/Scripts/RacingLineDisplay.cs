using System.Collections.Generic;
using UnityEngine;

// Play-mode racing line overlay: a wide, edge-faded ribbon drawn along the ideal racing line,
// vertex-coloured by what the pedals should be doing there — green = accelerating (or flat out),
// yellow = lifting / holding corner speed, red = braking.
//
// The colours come from a speed profile built the same way SplineDriver's is: per-segment corner
// speed targets from the player's VehicleInfo (corneringSpeedCurve or maxLateralG + banking),
// relaxed by forward accel and backward brake passes over the sampled centerline. Where the
// profile falls at brake rate the line is red; where it holds below top speed it's yellow; a short
// lift band is painted ahead of every brake zone.
//
// Self-bootstraps like HandlingTuner: no scene wiring needed. It builds only in scenes that have a
// TrackBuilder and a car to read a VehicleInfo from. Toggled from the pause menu (RacePauseMenu);
// the on/off state persists in PlayerPrefs.
public class RacingLineDisplay : MonoBehaviour
{
    public static RacingLineDisplay Instance { get; private set; }

    const string PrefKey = "ShowRacingLine";
    const float MphToMps = 1f / 2.237f;
    const float MpsToMph = 2.237f;

    [Header("Ribbon")]
    [Tooltip("Full width (m) of the ribbon, fade included.")]
    public float lineWidth = 3.2f;
    [Tooltip("Fraction of the width that is fully opaque; the rest fades to nothing at the edges.")]
    [Range(0.1f, 0.95f)] public float coreFraction = 0.45f;
    [Tooltip("Alpha of the opaque core — keep below 1 so the tarmac shows through.")]
    [Range(0.1f, 1f)] public float coreAlpha = 0.55f;
    [Tooltip("Local z of the ribbon relative to the track mesh. Negative = toward the camera, above the road.")]
    public float zOffset = -0.02f;
    [Tooltip("Sorting order: above the road and edge lines (1), below the brake marker boards (3).")]
    public int sortingOrder = 2;

    [Header("Speed Profile")]
    [Tooltip("Safety factor on the computed corner speeds shown to the player. 1 = the physics limit.")]
    [Range(0.6f, 1.2f)] public float cornerSpeedScale = 0.95f;
    [Tooltip("Fallback corner speed (mph) when no VehicleInfo can be found.")]
    public float fallbackCornerMph = 90f;

    [Header("Colour Thresholds")]
    [Tooltip("Required decel (m/s²) above which the line reads BRAKE (red).")]
    public float brakeDecelThreshold = 2.0f;
    [Tooltip("Required accel (m/s²) above which the line reads POWER (green).")]
    public float accelThreshold = 0.25f;
    [Tooltip("Distance (m) of yellow painted before each brake zone — the lift before the brake point.")]
    public float liftLeadMetres = 25f;
    [Tooltip("Span (m) over which the profile slope is measured. Longer = smoother colour transitions.")]
    public float slopeSpanMetres = 8f;

    public Color accelColor = new Color(0.15f, 0.9f, 0.25f);
    public Color liftColor = new Color(1f, 0.85f, 0.1f);
    public Color brakeColor = new Color(1f, 0.15f, 0.1f);

    TrackBuilder _builder;
    GameObject _lineGo;
    MeshRenderer _lineRenderer;
    float _pollTimer;

    // On/off switch surfaced in the pause menu. Persisted; applies live.
    public static bool Visible
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            if (Instance != null) Instance.ApplyVisibility();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("RacingLineDisplay");
        DontDestroyOnLoad(go);
        go.AddComponent<RacingLineDisplay>();
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

        // Scene changes destroy the ribbon with its parent TrackBuilder — just drop the stale refs.
        if (_lineGo == null) { _lineRenderer = null; _builder = null; }

        if (!Visible)
        {
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            return;
        }

        if (_lineGo != null)
        {
            _lineRenderer.enabled = true;
            return;
        }

        var builder = FindAnyObjectByType<TrackBuilder>();
        if (builder == null || builder.track == null) return;
        var vi = FindVehicleInfo();
        if (vi == null) return;

        _builder = builder;
        BuildRibbon(vi);
    }

    public void ApplyVisibility()
    {
        if (_lineRenderer != null) _lineRenderer.enabled = Visible;
        _pollTimer = 0f; // rebuild promptly if turning on before the first build
    }

    // Force a rebuild (e.g. after grip sliders change). Cheap enough to call from a menu button.
    public void RebuildNow()
    {
        if (_lineGo != null) Destroy(_lineGo);
        _lineGo = null;
        _lineRenderer = null;
        _pollTimer = 0f;
    }

    // Prefer the human car's VehicleInfo; fall back to any AI's so the line still shows in broadcast mode.
    static VehicleInfo FindVehicleInfo()
    {
        var pvcs = FindObjectsByType<PlayerVehicleController>();
        for (int i = 0; i < pvcs.Length; i++)
            if (pvcs[i].enabled && pvcs[i].vehicleInfo != null && pvcs[i].GetComponent<SplineInputDriver>() == null)
                return pvcs[i].vehicleInfo;
        for (int i = 0; i < pvcs.Length; i++)
            if (pvcs[i].vehicleInfo != null) return pvcs[i].vehicleInfo;
        var ai = FindAnyObjectByType<SplineDriver>();
        return ai != null ? ai.vehicleInfo : null;
    }

    enum PedalState { Accel, Lift, Brake }

    void BuildRibbon(VehicleInfo vi)
    {
        var track = _builder.track;
        var samples = _builder.SampleCenterline();
        if (samples == null || samples.Count < 2) return;

        float loopLen = samples[samples.Count - 1].distance;
        var anchors = track.BuildRacingLineAnchors();
        bool loop = track.closedLoop;

        float[] profile = BuildSpeedProfile(vi, track, samples, loop);
        PedalState[] states = ClassifySamples(vi, samples, profile, loop, loopLen);

        // Closed loops get the first sample appended so the ribbon meets itself across the seam.
        int n = samples.Count;
        int count = loop ? n + 1 : n;

        var verts = new List<Vector3>(count * 4);
        var cols = new List<Color>(count * 4);
        var uvs = new List<Vector2>(count * 4);
        var tris = new List<int>((count - 1) * 18);

        float halfW = lineWidth * 0.5f;
        float coreHalf = halfW * Mathf.Clamp01(coreFraction);

        for (int i = 0; i < count; i++)
        {
            var s = samples[i % n];
            var state = states[i % n];
            float dist = i < n ? s.distance : 0f;

            float lateral = track.GetLateralAt(dist, 0f, anchors, loopLen);
            Vector2 right = s.normal;
            Vector2 center = s.position + right * lateral;

            Color c = state == PedalState.Brake ? brakeColor : state == PedalState.Lift ? liftColor : accelColor;
            Color edge = c; edge.a = 0f;
            Color core = c; core.a = coreAlpha;

            verts.Add(new Vector3(center.x - right.x * halfW, center.y - right.y * halfW, 0f));
            verts.Add(new Vector3(center.x - right.x * coreHalf, center.y - right.y * coreHalf, 0f));
            verts.Add(new Vector3(center.x + right.x * coreHalf, center.y + right.y * coreHalf, 0f));
            verts.Add(new Vector3(center.x + right.x * halfW, center.y + right.y * halfW, 0f));
            cols.Add(edge); cols.Add(core); cols.Add(core); cols.Add(edge);
            uvs.Add(new Vector2(0f, s.distance)); uvs.Add(new Vector2(0.33f, s.distance));
            uvs.Add(new Vector2(0.67f, s.distance)); uvs.Add(new Vector2(1f, s.distance));

            if (i > 0)
            {
                int a = (i - 1) * 4;
                int b = i * 4;
                for (int band = 0; band < 3; band++)
                {
                    tris.Add(a + band); tris.Add(b + band); tris.Add(b + band + 1);
                    tris.Add(a + band); tris.Add(b + band + 1); tris.Add(a + band + 1);
                }
            }
        }

        var mesh = new Mesh { name = $"RacingLine_{track.name}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        _lineGo = new GameObject("RacingLineOverlay");
        _lineGo.transform.SetParent(_builder.transform, false);
        _lineGo.transform.localPosition = new Vector3(0f, 0f, zOffset);
        var mf = _lineGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        _lineRenderer = _lineGo.AddComponent<MeshRenderer>();
        _lineRenderer.sharedMaterial = LineMaterial();
        _lineRenderer.sortingOrder = sortingOrder;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.enabled = Visible;
    }

    static Material _lineMat;
    static Material LineMaterial()
    {
        if (_lineMat != null) return _lineMat;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _lineMat = new Material(sh) { name = "RacingLineOverlay" };
        return _lineMat;
    }

    // --- Speed profile: per-segment targets, then accel/brake relaxation. Mirrors SplineDriver, but
    // uses the PLAYER's effective grip (no AI-only bonus) so the colours match what the human car can do.
    float[] BuildSpeedProfile(VehicleInfo vi, TrackInfoV2 track, List<TrackBuilder.Sample> samples, bool loop)
    {
        var segs = track.segments;
        int n = samples.Count;
        float loopLen = samples[n - 1].distance;
        float topMph = vi != null ? vi.topSpeed : 200f;

        // Cumulative segment starts over the authored lap; seam samples past the total read as the last segment.
        var segStart = new float[segs.Length];
        float authoredTotal = 0f;
        for (int i = 0; i < segs.Length; i++) { segStart[i] = authoredTotal; authoredTotal += segs[i].length; }

        var profile = new float[n];
        int segIdx = 0;
        for (int i = 0; i < n; i++)
        {
            float d = samples[i].distance;
            while (segIdx < segs.Length - 1 && d >= segStart[segIdx + 1]) segIdx++;
            profile[i] = d >= authoredTotal ? topMph : TargetMphForSegment(vi, segs[segIdx], topMph);
        }

        int passes = loop ? 2 : 1;
        for (int p = 0; p < passes; p++)
        {
            for (int i = 1; i < n; i++) ApplyAccelLimit(vi, profile, samples, loopLen, i, i - 1);
            if (loop) ApplyAccelLimit(vi, profile, samples, loopLen, 0, n - 1);
        }
        for (int p = 0; p < passes; p++)
        {
            for (int i = n - 2; i >= 0; i--) ApplyBrakeLimit(vi, profile, samples, loopLen, i, i + 1);
            if (loop) ApplyBrakeLimit(vi, profile, samples, loopLen, n - 1, 0);
        }
        return profile;
    }

    float TargetMphForSegment(VehicleInfo vi, TrackInfoV2.TrackSegment seg, float topMph)
    {
        if (seg.maxSpeed > 0) return Mathf.Min(topMph, seg.maxSpeed);
        if (seg.type == TrackInfoV2.SegmentType.Straight || Mathf.Approximately(seg.angle, 0f)) return topMph;

        float radius = seg.length / Mathf.Max(Mathf.Abs(seg.angle) * Mathf.Deg2Rad, 1e-4f);
        float grip = Mathf.Max(TrackConditions.Effective, 0.05f);
        float baseMph;
        if (vi != null && vi.corneringSpeedCurve != null && vi.corneringSpeedCurve.length > 0)
            baseMph = vi.corneringSpeedCurve.Evaluate(radius) * Mathf.Sqrt(grip);
        else if (vi != null && vi.maxLateralG > 0.01f)
            baseMph = Mathf.Sqrt(radius * vi.maxLateralG * grip * 9.81f) * MpsToMph;
        else
            baseMph = fallbackCornerMph;

        float bankingMph = vi != null ? seg.banking * vi.bankingMphPerDegree : 0f;
        return Mathf.Clamp((baseMph + bankingMph) * cornerSpeedScale, 5f, topMph);
    }

    static void ApplyAccelLimit(VehicleInfo vi, float[] profile, List<TrackBuilder.Sample> samples, float loopLen, int i, int prev)
    {
        float d = samples[i].distance - samples[prev].distance;
        if (d < 0f) d += loopLen;
        if (d <= 0f) return;
        float vPrev = profile[prev] * MphToMps;
        float a = SampleAccelMph(vi, profile[prev]);
        float vMaxMph = Mathf.Sqrt(vPrev * vPrev + 2f * a * d) * MpsToMph;
        if (vMaxMph < profile[i]) profile[i] = vMaxMph;
    }

    static void ApplyBrakeLimit(VehicleInfo vi, float[] profile, List<TrackBuilder.Sample> samples, float loopLen, int i, int next)
    {
        float d = samples[next].distance - samples[i].distance;
        if (d < 0f) d += loopLen;
        if (d <= 0f) return;
        float vNext = profile[next] * MphToMps;
        float decel = SampleDecelMph(vi, profile[i]);
        float vMaxMph = Mathf.Sqrt(vNext * vNext + 2f * decel * d) * MpsToMph;
        if (vMaxMph < profile[i]) profile[i] = vMaxMph;
    }

    static float SampleAccelMph(VehicleInfo vi, float mph)
    {
        if (vi != null && vi.accelerationCurve != null && vi.accelerationCurve.length > 0)
            return Mathf.Max(0f, vi.accelerationCurve.Evaluate(mph));
        return 5f;
    }

    static float SampleDecelMph(VehicleInfo vi, float mph)
    {
        if (vi != null && vi.decelerationCurve != null && vi.decelerationCurve.length > 0)
            return Mathf.Max(0.1f, vi.decelerationCurve.Evaluate(mph));
        return 10f;
    }

    // --- Colour classification from the profile slope: falling at brake rate = red, rising = green,
    // held below top speed = yellow. A short yellow lift band is then painted before every red zone.
    PedalState[] ClassifySamples(VehicleInfo vi, List<TrackBuilder.Sample> samples, float[] profile, bool loop, float loopLen)
    {
        int n = samples.Count;
        var states = new PedalState[n];
        float topMps = (vi != null ? vi.topSpeed : 200f) * MphToMps;

        for (int i = 0; i < n; i++)
        {
            float vi0 = profile[i] * MphToMps;

            // Slope over slopeSpanMetres ahead (wrap-aware) — sample-to-sample slopes are too noisy.
            int j = i;
            float span = 0f;
            while (span < slopeSpanMetres)
            {
                int next = j + 1;
                if (next >= n) { if (!loop) break; next = 0; }
                float step = samples[next].distance - samples[j].distance;
                if (step < 0f) step += loopLen;
                span += step;
                j = next;
                if (j == i) break;
            }
            float vi1 = profile[j] * MphToMps;
            float a = span > 0.01f ? (vi1 * vi1 - vi0 * vi0) / (2f * span) : 0f;

            if (a <= -brakeDecelThreshold) states[i] = PedalState.Brake;
            else if (a >= accelThreshold) states[i] = PedalState.Accel;
            else states[i] = vi0 >= topMps * 0.985f ? PedalState.Accel : PedalState.Lift;
        }

        // Lift-before-brake: at each green→red boundary, repaint the preceding green back to yellow.
        if (liftLeadMetres > 0.01f)
        {
            for (int i = 0; i < n; i++)
            {
                int prev = i == 0 ? (loop ? n - 1 : -1) : i - 1;
                if (prev < 0 || states[i] != PedalState.Brake || states[prev] == PedalState.Brake) continue;

                float walked = 0f;
                int k = prev;
                while (walked < liftLeadMetres && states[k] == PedalState.Accel)
                {
                    states[k] = PedalState.Lift;
                    int back = k == 0 ? (loop ? n - 1 : -1) : k - 1;
                    if (back < 0 || back == i) break;
                    float step = samples[k].distance - samples[back].distance;
                    if (step < 0f) step += loopLen;
                    walked += step;
                    k = back;
                }
            }
        }
        return states;
    }
}
