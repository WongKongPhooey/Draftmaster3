using Draftmaster.Controls;
using UnityEngine;

// Keeps InputGlyphs' "which device is the player holding" answer current. One hidden object for the life of
// the game, polling ahead of everything else so a prompt drawn this frame already knows about a pad
// picked up this frame.
//
// On Android it also switches on PadKeyEchoFilter, which drops the keyboard presses the OS makes out of pad
// buttons, so a pad on a phone behaves as it does on a PC: B backs out once, and doesn't pause the game too.
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

    void OnEnable()
    {
        if (Application.platform == RuntimePlatform.Android) PadKeyEchoFilter.Install();
    }

    void OnDisable()
    {
        if (_instance == this) PadKeyEchoFilter.Uninstall();
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Update() => InputGlyphs.Poll();
}
