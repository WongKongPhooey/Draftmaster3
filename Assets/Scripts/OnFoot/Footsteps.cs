using UnityEngine;

// Footsteps for a walking body. Installed by OnFootController on every body it drives — the player, the crew
// chief's pit-wall avatar, a co-op partner's puppet — and silent while that body is stood on an e-scooter.
//
// Paced by distance covered rather than by a timer or the walk animation: a step lands every stride, so a
// scripted walk, a shove out of somebody and a co-op puppet whose transform is written from the network all
// step at the right rate without asking how they were moved. Running lengthens the stride as well as
// quickening it, so the cadence rises less than the speed does — as legs do.
//
// No footstep recordings ship yet, so with `clips` empty a handful of heel-and-toe scuffs are generated on
// first use (filtered noise and a low thump, like the phone tone and the alarm are generated). Drop real
// samples into `clips` and they are used instead.
public class Footsteps : MonoBehaviour
{
    [Tooltip("Footstep samples, picked at random without repeating the last. Empty = generated scuffs.")]
    public AudioClip[] clips;
    [Tooltip("Volume of a walking step.")]
    [Range(0f, 1f)] public float volume = 0.25f;
    [Tooltip("Volume multiplier while running.")]
    public float runVolumeScale = 1.35f;
    [Tooltip("Metres between steps at walking pace.")]
    public float walkStride = 1.3f;
    [Tooltip("Metres between steps at a run. Between walk and run pace it is blended.")]
    public float runStride = 1.9f;
    [Tooltip("Random pitch spread either side of 1, so no two steps are quite the same.")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.08f;
    [Tooltip("Left/right pan alternating per foot. Only for the body the player is listening from.")]
    [Range(0f, 0.5f)] public float footPan = 0.06f;
    [Tooltip("A body that moves further than this in one frame was placed there, not walked there: no step.")]
    public float teleportMetres = 2f;
    [Tooltip("Somebody else's body (a co-op partner) is heard in the world, fading out by this distance.")]
    public float remoteHearingMetres = 18f;

    // Below this (m/s) the body is standing, or being nudged out of somebody, not walking.
    const float StillSpeed = 1.2f;

    OnFootController _walker;
    AudioSource _src;
    Vector3 _last;
    bool _hasLast;
    float _toNextStep;
    float _stillFor;
    bool _leftFoot;
    int _lastClip = -1;

    void Awake()
    {
        _walker = GetComponent<OnFootController>();
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
    }

    void OnEnable() => _hasLast = false;

    void Update()
    {
        Vector3 pos = transform.position;
        if (!_hasLast) { _last = pos; _hasLast = true; _toNextStep = FirstStep(); return; }

        Vector2 delta = pos - _last;
        _last = pos;
        float moved = delta.magnitude;

        // In a cart, or put somewhere: the legs didn't do that.
        if ((_walker != null && _walker.Ridden != null) || moved > teleportMetres)
        {
            _toNextStep = FirstStep();
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        if (moved / dt < StillSpeed)
        {
            // Stood still long enough that setting off again should land a foot straight away, not a stride later.
            _stillFor += dt;
            if (_stillFor > 0.2f) _toNextStep = FirstStep();
            return;
        }
        _stillFor = 0f;

        float walkSpeed = _walker != null ? _walker.moveSpeed : 3.5f;
        float runSpeed = walkSpeed * (_walker != null ? _walker.runMultiplier : 2f);
        float pace = Mathf.InverseLerp(walkSpeed, runSpeed, moved / dt);   // 0 walking, 1 running

        _toNextStep -= moved;
        if (_toNextStep > 0f) return;
        _toNextStep += Mathf.Lerp(walkStride, runStride, pace);
        Step(pace);
    }

    // Setting off lands the first foot after a short shuffle rather than a full stride.
    float FirstStep() => walkStride * 0.35f;

    void Step(float pace)
    {
        var clip = PickClip();
        if (clip == null) return;

        bool remote = _walker != null && _walker.RemotePuppet;
        _src.spatialBlend = remote ? 1f : 0f;
        if (remote)
        {
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 2f;
            _src.maxDistance = remoteHearingMetres;
            _src.dopplerLevel = 0f;
        }

        _leftFoot = !_leftFoot;
        _src.panStereo = remote ? 0f : (_leftFoot ? -footPan : footPan);
        _src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter) + pace * 0.04f;
        _src.PlayOneShot(clip, volume * Mathf.Lerp(1f, runVolumeScale, pace) * Random.Range(0.85f, 1f));
    }

    AudioClip PickClip()
    {
        var set = clips != null && clips.Length > 0 ? clips : Generated();
        if (set.Length == 1) return set[0];
        int i = Random.Range(0, set.Length - 1);
        if (i >= _lastClip && _lastClip >= 0) i++;   // never the same sample twice running
        _lastClip = i;
        return set[i];
    }

    // ---- generated scuffs ---------------------------------------------------------------------------------

    static AudioClip[] _generated;
    const int Variants = 6;

    static AudioClip[] Generated()
    {
        if (_generated != null) return _generated;
        _generated = new AudioClip[Variants];
        for (int v = 0; v < Variants; v++) _generated[v] = Synth(v);
        return _generated;
    }

    // A shoe on concrete: the heel lands (a dull thump under a burst of grit), then the toe rolls down a few
    // tens of milliseconds later, quieter and brighter. Every variant differs in its timing and tone.
    internal static AudioClip Synth(int seed)
    {
        const int rate = 44100;
        const float length = 0.18f;
        int n = Mathf.CeilToInt(length * rate);
        var data = new float[n];
        var rng = new System.Random(9173 + seed * 7919);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        float heelCut = R(1400f, 2200f), toeCut = R(2600f, 3800f);
        float thumpHz = R(85f, 130f);
        float toeAt = R(0.035f, 0.06f), toeGain = R(0.3f, 0.45f);
        float aHeel = 1f - Mathf.Exp(-2f * Mathf.PI * heelCut / rate);
        float aToe = 1f - Mathf.Exp(-2f * Mathf.PI * toeCut / rate);
        float aGrit = 1f - Mathf.Exp(-2f * Mathf.PI * 5000f / rate);
        float lpHeel = 0f, lpToe = 0f, lpGrit = 0f;

        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = R(-1f, 1f);
            lpHeel += aHeel * (noise - lpHeel);
            lpToe += aToe * (noise - lpToe);
            lpGrit += aGrit * (noise - lpGrit);
            float grit = noise - lpGrit;   // high-passed: the sand under the sole

            float heelEnv = Mathf.Clamp01(t / 0.002f) * Mathf.Exp(-t / 0.022f);
            float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t / 0.03f) * Mathf.Clamp01(t / 0.003f);
            float s = lpHeel * heelEnv * 1.6f + thump * 0.5f + grit * Mathf.Exp(-t / 0.01f) * 0.12f;

            float tt = t - toeAt;
            if (tt > 0f)
            {
                float toeEnv = Mathf.Clamp01(tt / 0.003f) * Mathf.Exp(-tt / 0.016f);
                s += lpToe * toeEnv * toeGain * 1.6f + grit * Mathf.Exp(-tt / 0.008f) * 0.08f;
            }

            // Faded to nothing over the last few milliseconds so the tail doesn't click.
            s *= Mathf.Clamp01((length - t) / 0.01f);
            data[i] = s;
            peak = Mathf.Max(peak, Mathf.Abs(s));
        }

        if (peak > 0f)
            for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;

        var clip = AudioClip.Create("Footstep" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
