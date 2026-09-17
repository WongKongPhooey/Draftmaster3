using UnityEngine;

// Keeps InputGlyphs' "which device is the player holding" answer current. One hidden object for the life of
// the game, polling ahead of everything else so a prompt drawn this frame already knows about a pad
// picked up this frame.
[DefaultExecutionOrder(-1000)]
public class InputDeviceWatcher : MonoBehaviour
{
    static InputDeviceWatcher _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("InputDeviceWatcher");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InputDeviceWatcher>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Update() => InputGlyphs.Poll();
}
