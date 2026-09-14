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
    [Tooltip("Distance (m) inside which a 3D engine is at full volume. Logarithmic rolloff halves the level on every doubling of this.")]
    public float minDistance = 8f;
    [Tooltip("Distance (m) beyond which a 3D engine is at its quietest.")]
    public float maxDistance = 400f;
    [Tooltip("Custom is the house curve: a 1/d swell as a car comes past, faded out to actual silence by maxDistance. Logarithmic never reaches silence — a full field then sits under everything as a floor of distant hum. Linear fades evenly and sounds flat.")]
    public AudioRolloffMode rolloff = AudioRolloffMode.Custom;
    [Tooltip("Stereo spread (degrees) of the 3D source. A little stops a car hard-panning into one ear as it goes by.")]
    [Range(0f, 360f)] public float spread = 35f;

    [Header("One-shots")]
    [Tooltip("Optional samples played on each gear change. One is picked at random.")]
    public AudioClip[] shiftClips;
    [Range(0f, 1f)] public float shiftVolume = 0.8f;
    [Tooltip("Optional engine-start/crank sample played when the engine is fired up (the driver climbs in), not when the component wakes.")]
    public AudioClip startClip;
    [Range(0f, 1f)] public float startVolume = 0.9f;

    [Header("Ignition")]
    [Tooltip("Silence the engine unless something is actually driving this car (EngineGearbox.Running). A parked car has its engine off.")]
    public bool silentWhenParked = true;
    [Tooltip("Seconds to fade the engine in when it fires and out when it's switched off.")]
    public float ignitionFadeSeconds = 0.35f;

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

        // A parked car's loops are stopped outright rather than turned down to zero: a field of 40 cars is
        // 40 x N looping voices, and Unity mixes silence at the same cost as noise.
        public void SetPlaying(bool play)
        {
            if (_src == null) return;
            for (int i = 0; i < _src.Length; i++)
            {
                var s = _src[i];
                if (s == null || s.clip == null) continue;
                if (play && !s.isPlaying) s.Play();
                else if (!play && s.isPlaying) { s.Stop(); _vol[i] = 0f; s.volume = 0f; }
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
    bool _running;        // is the engine turning — mirrors EngineGearbox.Running
    float _gate;          // 0 = engine off and silent, 1 = fully voiced

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

        // The banks start stopped: a car is silent until something drives it. StepIgnition fires them up.
        _gate = 0f;
        _running = false;
        _onBank.SetPlaying(false);
        _offBank.SetPlaying(false);
    }

    void ConfigureSpatial(AudioSource s)
    {
        s.spatialBlend = spatialBlend;
        s.dopplerLevel = dopplerLevel;
        s.minDistance = Mathf.Max(0.1f, minDistance);
        s.maxDistance = Mathf.Max(s.minDistance + 1f, maxDistance);
        s.spread = spread;
        s.rolloffMode = rolloff;
        if (rolloff == AudioRolloffMode.Custom) s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, RolloffCurve());
    }

    // 1/d out to maxDistance, then taken the rest of the way to silence.
    //
    // Unity's own Logarithmic mode clamps at maxDistance rather than reaching zero, so every car in a
    // 40-car field keeps a small permanent voice no matter how far away it is and the whole grid piles up
    // as a hum under the mix. This is the same near-field shape — a car swelling as it comes past the pit
    // wall and falling away behind — with the tail actually taken to nothing, so what you hear is the cars
    // near you rather than all of them at once.
    AnimationCurve _rolloffCurve;

    AnimationCurve RolloffCurve()
    {
        if (_rolloffCurve != null) return _rolloffCurve;

        float min = Mathf.Max(0.1f, minDistance);
        float max = Mathf.Max(min + 1f, maxDistance);

        const int n = 18;
        var keys = new Keyframe[n];
        for (int i = 0; i < n; i++)
        {
            // Squared spacing: the near field, where a pass-by actually happens, gets most of the keys.
            float x = Mathf.Pow(i / (float)(n - 1), 2f);
            float d = Mathf.Max(min, x * max);
            float v = min / d;                                    // inverse-distance law
            float tail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, x));
            keys[i] = new Keyframe(x, Mathf.Clamp01(v * tail));
        }
        keys[n - 1].value = 0f;

        _rolloffCurve = new AnimationCurve(keys);
        for (int i = 0; i < n; i++) _rolloffCurve.SmoothTangents(i, 0f);
        return _rolloffCurve;
    }

    void Update()
    {
        // Banks are built in Awake; guard against null in case Awake hasn't completed for a freshly
        // added component this frame (e.g. runtime-assembled cars), which otherwise NREs every frame.
        if (_gearbox == null || _onBank == null || _offBank == null) return;
        if (!_onBank.Valid && !_offBank.Valid) return;

        float dt = Time.deltaTime;

        StepIgnition(dt);
        if (_gate <= 0.0001f) return;   // engine off: the loops are stopped, there is nothing to mix

        float rpm = _gearbox.Rpm;
        float load = _gearbox.Load01;                       // 1 = on power, 0 = coasting
        float master = masterVolume * Mathf.Lerp(coastVolume, 1f, load) * _gate;

        // If there's no off bank, the on bank carries the whole signal regardless of load.
        float onMix = _offBank.Valid ? load : 1f;
        float offMix = _offBank.Valid ? (1f - load) : 0f;

        _onBank.Apply(this, rpm, onMix * master, dt);
        _offBank.Apply(this, rpm, offMix * master, dt);

        if (_gearbox.ShiftEvent != 0) PlayShift();
    }

    // Engine off / engine on. An engine is only making noise while something is driving the car, so a pit
    // lane of parked cars — and a career that loads with no session on track at all — is quiet.
    void StepIgnition(float dt)
    {
        bool running = !silentWhenParked || _gearbox.Running;

        if (running != _running)
        {
            _running = running;
            if (running)
            {
                _onBank.SetPlaying(true);
                _offBank.SetPlaying(true);
                // The crank belongs to the moment the key is turned, not to the moment the component woke up.
                if (startClip != null && _oneShot != null) _oneShot.PlayOneShot(startClip, startVolume);
            }
        }

        float step = ignitionFadeSeconds > 0f ? dt / ignitionFadeSeconds : 1f;
        _gate = Mathf.MoveTowards(_gate, running ? 1f : 0f, step);

        // Only stop the loops once the fade has actually reached silence, or switching off would cut.
        if (!running && _gate <= 0.0001f)
        {
            _onBank.SetPlaying(false);
            _offBank.SetPlaying(false);
        }
    }

    void PlayShift()
    {
        if (shiftClips == null || shiftClips.Length == 0 || _oneShot == null) return;
        var clip = shiftClips[UnityEngine.Random.Range(0, shiftClips.Length)];
        if (clip != null) _oneShot.PlayOneShot(clip, shiftVolume);
    }
}
