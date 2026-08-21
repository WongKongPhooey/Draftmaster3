using UnityEngine;
using UnityEngine.SceneManagement;

// Installs itself on play and keeps the root level of every loaded scene filed into the RuntimeHierarchy
// buckets. Most of the clutter arrives in the first couple of seconds (self-installing HUDs, directors,
// per-car particle emitters), but cars, NPCs and speech bubbles keep appearing all race, so it keeps
// sweeping on a slow tick rather than tidying once and stopping.
//
// The sweep is cheap: an object leaves the root list the moment it is filed, so a steady-state tick only
// walks the buckets themselves plus whatever spawned since the last one.
[DisallowMultipleComponent]
public class RuntimeHierarchyOrganizer : MonoBehaviour
{
    [Tooltip("Seconds between sweeps. Unscaled, so the hierarchy still tidies while the game is paused.")]
    public float sweepInterval = 0.25f;

    static RuntimeHierarchyOrganizer _instance;

    float _nextSweep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!RuntimeHierarchy.Enabled || _instance != null) return;

        var go = new GameObject("RuntimeHierarchyOrganizer");
        go.hideFlags = HideFlags.DontSave;
        _instance = go.AddComponent<RuntimeHierarchyOrganizer>();
        DontDestroyOnLoad(go);
    }

    void OnEnable()
    {
        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        RuntimeHierarchy.Organise();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (_instance == this) _instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A reloaded scene reuses handles; drop any bucket set left over from the last time round.
        RuntimeHierarchy.ForgetScene(scene);
        RuntimeHierarchy.Organise(scene);
    }

    void OnSceneUnloaded(Scene scene) => RuntimeHierarchy.ForgetScene(scene);

    void LateUpdate()
    {
        if (!RuntimeHierarchy.Enabled) return;
        if (Time.unscaledTime < _nextSweep) return;
        _nextSweep = Time.unscaledTime + Mathf.Max(0.05f, sweepInterval);
        RuntimeHierarchy.Organise();
    }
}
