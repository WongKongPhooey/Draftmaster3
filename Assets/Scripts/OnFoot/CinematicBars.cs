using UnityEngine;
using UnityEngine.UI;

// Cinematic letterbox. Two black bars slide in from the top + bottom of the screen whenever the on-foot player
// is mid-conversation, and slide back out when the talk ends — turning a dialogue into a little cut-scene.
//
// Self-contained: a single instance is spawned on first scene load and lives for the session, building its own
// overlay canvas. It watches every NPCInteractable's IsTalking (covers both the inline speech-bubble NPCs and
// the Ink-driven ones, since both derive from NPCInteractable), so no per-dialogue or per-scene wiring is needed.
public class CinematicBars : MonoBehaviour
{
    public static CinematicBars Instance { get; private set; }

    [Tooltip("Bar height as a fraction of screen height (each bar).")]
    [Range(0f, 0.3f)] public float barHeightFraction = 0.12f;
    [Tooltip("Slide speed (1/seconds): 6 ≈ a ~0.17s slide.")]
    public float slideSpeed = 6f;

    RectTransform _topBar, _bottomBar;
    float _barPx;
    float _t;        // 0 = hidden, 1 = fully in
    float _target;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("CinematicBars");
        DontDestroyOnLoad(go);
        go.AddComponent<CinematicBars>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        _target = AnyoneTalking() ? 1f : 0f;
        _t = Mathf.MoveTowards(_t, _target, Time.unscaledDeltaTime * Mathf.Max(0.01f, slideSpeed));
        if (_topBar != null) _topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(_barPx, 0f, _t));
        if (_bottomBar != null) _bottomBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-_barPx, 0f, _t));
    }

    static bool AnyoneTalking()
    {
        var all = NPCInteractable.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].IsTalking) return true;
        return false;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("CinematicBarsCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;   // under the dialogue panels (200) but over the game
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // Bars shouldn't eat clicks meant for buttons underneath them.
        canvasGO.AddComponent<GraphicRaycaster>();

        _barPx = 1080f * Mathf.Clamp(barHeightFraction, 0f, 0.49f);
        _topBar = MakeBar("LetterboxTop", canvasGO.transform, true);
        _bottomBar = MakeBar("LetterboxBottom", canvasGO.transform, false);
    }

    RectTransform MakeBar(string name, Transform parent, bool top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, _barPx);
        rt.anchoredPosition = new Vector2(0f, top ? _barPx : -_barPx); // parked offscreen
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }
}
