using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Where the player HEARS from. A 2D game's camera sits 100 m off the ground looking down, so an AudioListener
/// riding on it is 100 m from every car on the circuit — which flattens 3D audio into a constant mush: no
/// pass-by swell, barely any stereo pan, and a doppler shift that never arrives because the distance the sound
/// travels hardly changes as a car goes by.
///
/// So the ears are taken off the camera and put on the ground, at the camera's subject, in the plane the cars
/// drive in. Stood at the pit wall, a car coming down pit road now rises as it approaches, passes across the
/// stereo field and falls away behind — with the pitch bend that comes with it.
///
/// Installs itself on first play and survives scene loads; whatever listener a scene carries (Unity puts one on
/// every new camera) is switched off, because Unity mixes through exactly one.
/// </summary>
[DefaultExecutionOrder(2000)]   // after CameraFollow/OnFootCameraFollow have placed the camera for the frame
public class AudioEars : MonoBehaviour
{
    public static AudioEars Current { get; private set; }

    [Tooltip("World z the ears sit at — the plane the cars drive in, not the camera's.")]
    public float listeningPlaneZ = 0f;
    [Tooltip("A move further than this in one frame is a cut, not a drive: the mix is ducked across it so the teleport doesn't arrive as a doppler screech.")]
    public float cutMetres = 25f;
    [Tooltip("Seconds to bring the mix back up after a cut.")]
    public float cutRecoverSeconds = 0.15f;

    AudioListener _ears;
    Camera _cam;
    CameraFollow _follow;
    OnFootCameraFollow _footFollow;
    Vector3 _last;
    bool _hasLast;
    float _duck;          // seconds of recovery left after a cut
    float _duckBaseline;  // the master volume we ducked from, so a settings volume isn't overwritten

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Current != null) return;
        var go = new GameObject("AudioEars");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioEars>();
    }

    void Awake()
    {
        if (Current != null && Current != this) { Destroy(gameObject); return; }
        Current = this;
        _ears = gameObject.AddComponent<AudioListener>();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ClaimListener();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Current == this) Current = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cam = null; _follow = null; _footFollow = null;
        _hasLast = false;       // a scene load is the biggest cut there is; don't measure across it
        ClaimListener();
    }

    // Unity mixes through one listener and warns about the rest. The scene's own (on its camera) goes off.
    void ClaimListener()
    {
        var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != _ears && all[i].enabled) all[i].enabled = false;
        if (_ears != null) _ears.enabled = true;
    }

    void LateUpdate()
    {
        Vector3 want = ListeningPoint();
        want.z = listeningPlaneZ;

        // Doppler is computed from how far the ears moved since last frame, so a cut — the broadcast camera
        // jumping to another car, a tow, a teleport into the motorhome — reads as the listener travelling
        // hundreds of metres in one frame and bends every engine in the field. Ride across it muted.
        if (_hasLast && Vector3.Distance(want, _last) > cutMetres && cutRecoverSeconds > 0f)
        {
            if (_duck <= 0f) _duckBaseline = AudioListener.volume;
            _duck = cutRecoverSeconds;
        }

        transform.position = want;
        _last = want;
        _hasLast = true;

        if (_duck > 0f)
        {
            _duck -= Time.unscaledDeltaTime;
            float back = cutRecoverSeconds > 0f ? Mathf.Clamp01(1f - _duck / cutRecoverSeconds) : 1f;
            AudioListener.volume = _duckBaseline * back;
            if (_duck <= 0f) AudioListener.volume = _duckBaseline;
        }
    }

    // The camera's SUBJECT, not the camera itself: following the camera would feed it the lean and the impact
    // shake as listener movement, and doppler would warble every time the car hit a bump.
    Vector3 ListeningPoint()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam != null)
            {
                _follow = _cam.GetComponent<CameraFollow>();
                _footFollow = _cam.GetComponent<OnFootCameraFollow>();
            }
        }

        if (_follow != null && _follow.isActiveAndEnabled && _follow.target != null) return _follow.target.position;
        if (_footFollow != null && _footFollow.isActiveAndEnabled && _footFollow.target != null) return _footFollow.target.position;
        if (_cam != null) return _cam.transform.position;
        return transform.position;
    }
}
