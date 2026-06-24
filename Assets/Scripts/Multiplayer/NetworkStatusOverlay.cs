using UnityEngine;
using UnityEngine.UI;

// Thin binder for the authored network-status overlay (Assets/Prefabs/UI/NetworkStatusOverlay.prefab).
//
// Replaces NetworkLauncher's old IMGUI overlay. NetworkLauncher spawns one of these (DontDestroyOnLoad) so the
// host can read / copy the join code from the menu through into the race. The box and copy button are authored;
// this script only reflects the live session and wires the copy click.
public class NetworkStatusOverlay : MonoBehaviour
{
    [Header("Authored children (auto-wired in editor)")]
    [SerializeField] GameObject panel;   // the box; hidden until a session exists
    [SerializeField] Text label;
    [SerializeField] Button copyButton;  // host only

    void Awake()
    {
        ResolveRefs();
        if (copyButton != null) copyButton.onClick.AddListener(CopyCode);
    }

    void Update()
    {
        var session = NetworkLauncher.Instance != null ? NetworkLauncher.Instance.Session : null;
        bool show = session != null;

        if (panel != null && panel.activeSelf != show) panel.SetActive(show);
        if (!show)
        {
            if (copyButton != null && copyButton.gameObject.activeSelf) copyButton.gameObject.SetActive(false);
            return;
        }

        bool host = session.IsHost;
        if (label != null)
            label.text = host ? $"HOST  —  Join code: {session.Code}" : $"Connected  ({session.Code})";
        if (copyButton != null && copyButton.gameObject.activeSelf != host) copyButton.gameObject.SetActive(host);
    }

    void CopyCode()
    {
        var session = NetworkLauncher.Instance != null ? NetworkLauncher.Instance.Session : null;
        if (session != null) GUIUtility.systemCopyBuffer = session.Code;
    }

    void ResolveRefs()
    {
        if (panel == null)
        {
            var t = transform.Find("Panel");
            if (t != null) panel = t.gameObject;
        }
        if (label == null)
        {
            var t = transform.Find("Panel/Label");
            if (t != null) label = t.GetComponent<Text>();
        }
        if (copyButton == null)
        {
            var t = transform.Find("CopyButton");
            if (t != null) copyButton = t.GetComponent<Button>();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) ResolveRefs();
    }
#endif
}
