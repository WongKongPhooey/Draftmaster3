using UnityEngine;

// Background atmosphere bed for the on-foot pit scene — a distant crowd that's already there when the demo
// opens, so the paddock doesn't feel empty before a car has turned a wheel.
//
// Ducks itself while the player is inside the RV (RVInterior.IsInside): the world outside is masked out, so
// the crowd should sound like it's through a wall. Spawned by PitLaneStart; no scene wiring beyond the clip.
public class AmbienceLoop : MonoBehaviour
{
    [Tooltip("Looping ambience clip.")]
    public AudioClip clip;
    [Tooltip("Volume out in the paddock.")]
    [Range(0f, 1f)] public float volume = 0.35f;
    [Tooltip("Volume multiplier while the player is inside the RV — muffled, not silent.")]
    [Range(0f, 1f)] public float indoorScale = 0.25f;
    [Tooltip("Seconds to cross-fade between indoor and outdoor levels.")]
    public float fadeSeconds = 1.2f;

    AudioSource _src;
    RVInterior _interior;
    float _level;

    public static AmbienceLoop Play(AudioClip clip, float volume)
    {
        if (clip == null) return null;
        var go = new GameObject("PitAmbience");
        var amb = go.AddComponent<AmbienceLoop>();
        amb.clip = clip;
        amb.volume = volume;
        return amb;
    }

    void Start()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;      // 2D bed, not a point source
        _src.volume = 0f;
        _level = 0f;
        if (clip != null) _src.Play();
    }

    void Update()
    {
        if (_src == null) return;
        if (_interior == null) _interior = FindFirstObjectByType<RVInterior>();

        bool indoors = _interior != null && _interior.IsInside;
        float target = volume * (indoors ? indoorScale : 1f);
        _level = Mathf.MoveTowards(_level, target, (volume / Mathf.Max(0.05f, fadeSeconds)) * Time.deltaTime);
        _src.volume = _level;
    }
}
