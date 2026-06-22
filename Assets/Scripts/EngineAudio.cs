using System;
using UnityEngine;

/// <summary>
/// Multi-sample engine note for the Papyrus/NR2003-style sound set. Two RPM-banked banks of looping clips —
/// on-throttle (under power) and off-throttle (coasting / engine braking) — are each crossfaded across the RPM
/// range, then blended against each other by engine load. Each clip is pitch-shifted to track the exact RPM.
/// Reads RPM and load from an <see cref="EngineGearbox"/> on the same GameObject; plays a shift sample on each
/// gear change and an optional one-shot when the engine first cranks.
///
/// No clips assigned = silent no-op. Leave <see cref="offLayers"/> empty to run a single-bank (RPM-only) engine.
/// </summary>
[RequireComponent(typeof(EngineGearbox))]
public class EngineAudio : MonoBehaviour
{
    [Serializable]
    public struct EngineLayer
    {
        public AudioClip clip;
        [Tooltip("Engine RPM this loop was recorded at. Layers crossfade by how close current RPM is to this.")]
        public float centerRpm;
    }

    [Tooltip("Optional shared clip set. If assigned, its layers/shift/start clips populate this component at startup (inline fields below act as overrides / fallback).")]
    public EngineSoundSet soundSet;

    [Tooltip("On-throttle (under power) loops across the RPM range. e.g. winston_idle/low/med/high or winnf_on_*.")]
    public EngineLayer[] layers;

    [Tooltip("Off-throttle (coasting / engine-braking) loops across the RPM range. e.g. win_low_off/med_off/high_off. Leave empty for an RPM-only engine.")]
    public EngineLayer[] offLayers;

    [Header("Mix")]
    [Tooltip("Overall engine volume.")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Tooltip("Extra volume scale when fully coasting (load 0). 1 = no dip.")]
    [Range(0f, 1f)] public float coastVolume = 0.85f;
    [Tooltip("Volume change rate (per second) — smooths crossfades so layer/bank swaps don't click.")]
    public float volumeLerpRate = 12f;

    [Header("Pitch")]
    [Tooltip("Global pitch multiplier on top of the per-layer RPM tracking.")]
    public float basePitch = 1f;
    [Tooltip("Clamp on how far a single sample is stretched from its recorded RPM. 1 = no stretch.")]
    public float minPitch = 0.6f;
    public float maxPitch = 1.7f;

    [Header("Spatialisation")]
    [Tooltip("0 = 2D (player's own car), 1 = full 3D positional (AI cars so they pan/attenuate as they pass).")]
    [Range(0f, 1f)] public float spatialBlend = 0f;
    [Tooltip("3D doppler amount. 0 disables the pitch-shift-on-pass effect.")]
    [Range(0f, 5f)] public float dopplerLevel = 0.4f;
    [Tooltip("Distance (m) beyond which a 3D engine is at its quietest.")]
    public float maxDistance = 120f;

    [Header("One-shots")]
    [Tooltip("Optional samples played on each gear change. One is picked at random.")]
    public AudioClip[] shiftClips;
    [Range(0f, 1f)] public float shiftVolume = 0.8f;
    [Tooltip("Optional engine-start/crank sample played once when the component wakes.")]
    public AudioClip startClip;
    [Range(0f, 1f)] public float startVolume = 0.9f;

    // A single RPM-banked set of looping sources.
    class Bank
    {
        EngineLayer[] _layers;
        AudioSource[] _src;
        int[] _order;
        float[] _vol;

        public bool Valid => _src != null;

        public Bank(EngineAudio host, EngineLayer[] layers)
        {
            if (layers == null || layers.Length == 0) return;
            _layers = layers;
            _order = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) _order[i] = i;
            Array.Sort(_order, (a, b) => layers[a].centerRpm.CompareTo(layers[b].centerRpm));

            _src = new AudioSource[layers.Length];
            _vol = new float[layers.Length];
            for (int i = 0; i < _order.Length; i++)
            {
                var clip = layers[_order[i]].clip;
                var s = host.gameObject.AddComponent<AudioSource>();
                s.clip = clip;
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0f;
                host.ConfigureSpatial(s);
                _src[i] = s;
                if (clip != null) s.Play();
            }
        }

        // bankMix: this bank's share of the overall engine volume (on vs off blend).
        public void Apply(EngineAudio host, float rpm, float bankMix, float dt)
        {
            if (_src == null) return;
            int n = _order.Length;
            float k = 1f - Mathf.Exp(-host.volumeLerpRate * dt);
            for (int i = 0; i < n; i++)
            {
                float target = Weight(i, rpm, n) * bankMix;
                _vol[i] = Mathf.Lerp(_vol[i], target, k);
                var s = _src[i];
                if (s == null) continue;
                s.volume = _vol[i];
                float center = _layers[_order[i]].centerRpm;
                float pitch = center > 1f ? rpm / center : 1f;
                s.pitch = host.basePitch * Mathf.Clamp(pitch, host.minPitch, host.maxPitch);
            }
        }

        // Triangular crossfade between the two layers bracketing the current RPM.
        float Weight(int i, float rpm, int n)
        {
            if (_layers[_order[i]].clip == null) return 0f;
            float center = _layers[_order[i]].centerRpm;
            if (rpm <= _layers[_order[0]].centerRpm) return i == 0 ? 1f : 0f;
            if (rpm >= _layers[_order[n - 1]].centerRpm) return i == n - 1 ? 1f : 0f;
            float lo = i > 0 ? _layers[_order[i - 1]].centerRpm : center;
            float hi = i < n - 1 ? _layers[_order[i + 1]].centerRpm : center;
            if (rpm >= lo && rpm <= center) return Mathf.InverseLerp(lo, center, rpm);
            if (rpm > center && rpm <= hi) return 1f - Mathf.InverseLerp(center, hi, rpm);
            return 0f;
        }
    }

    EngineGearbox _gearbox;
    Bank _onBank;
    Bank _offBank;
    AudioSource _oneShot; // shift + start bus

    void Awake()
    {
        _gearbox = GetComponent<EngineGearbox>();

        // A shared sound set fills in anything not set inline, so all cars can share one asset.
        if (soundSet != null)
        {
            if ((layers == null || layers.Length == 0) && soundSet.onLayers != null) layers = soundSet.onLayers;
            if ((offLayers == null || offLayers.Length == 0) && soundSet.offLayers != null) offLayers = soundSet.offLayers;
            if ((shiftClips == null || shiftClips.Length == 0) && soundSet.shiftClips != null) shiftClips = soundSet.shiftClips;
            if (startClip == null) startClip = soundSet.startClip;
        }

        _onBank = new Bank(this, layers);
        _offBank = new Bank(this, offLayers);

        _oneShot = gameObject.AddComponent<AudioSource>();
        _oneShot.playOnAwake = false;
        _oneShot.loop = false;
        ConfigureSpatial(_oneShot);

        if (startClip != null) _oneShot.PlayOneShot(startClip, startVolume);
    }

    void ConfigureSpatial(AudioSource s)
    {
        s.spatialBlend = spatialBlend;
        s.dopplerLevel = dopplerLevel;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.maxDistance = maxDistance;
    }

    void Update()
    {
        // Banks are built in Awake; guard against null in case Awake hasn't completed for a freshly
        // added component this frame (e.g. runtime-assembled cars), which otherwise NREs every frame.
        if (_gearbox == null || _onBank == null || _offBank == null) return;
        if (!_onBank.Valid && !_offBank.Valid) return;

        float rpm = _gearbox.Rpm;
        float load = _gearbox.Load01;                       // 1 = on power, 0 = coasting
        float master = masterVolume * Mathf.Lerp(coastVolume, 1f, load);
        float dt = Time.deltaTime;

        // If there's no off bank, the on bank carries the whole signal regardless of load.
        float onMix = _offBank.Valid ? load : 1f;
        float offMix = _offBank.Valid ? (1f - load) : 0f;

        _onBank.Apply(this, rpm, onMix * master, dt);
        _offBank.Apply(this, rpm, offMix * master, dt);

        if (_gearbox.ShiftEvent != 0) PlayShift();
    }

    void PlayShift()
    {
        if (shiftClips == null || shiftClips.Length == 0 || _oneShot == null) return;
        var clip = shiftClips[UnityEngine.Random.Range(0, shiftClips.Length)];
        if (clip != null) _oneShot.PlayOneShot(clip, shiftVolume);
    }
}
