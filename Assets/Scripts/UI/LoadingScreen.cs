using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The card the game shows while it changes scene, so a big load reads as loading rather than as a hang.
//
// A plain SceneManager.LoadScene blocks the main thread for the whole load: the last frame of the menu sits
// on screen, unresponsive, for the couple of seconds the race scene takes to build — which looks exactly
// like a crash. Routing the load through here puts a card up first, loads asynchronously underneath it (so
// asset reads happen off the main thread and the bar can move), and keeps the card up through the new
// scene's first frames, where TrackSceneLoader, the grid and the paddock do their one-off work. Then it fades.
//
// Self-installing and scene-independent like ScreenFade: a DontDestroyOnLoad object drawn in IMGUI, driven
// on unscaled time so it still runs when the pause menu has the clock stopped. It draws in front of the wipe
// (CardDepth < ScreenFade.WipeDepth), so a caller that has already faded to black just sees the card arrive
// on the black, and a scene that opens on HoldBlack is still black when the card fades off it.
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    // True from the moment a load is asked for until the card has faded off the new scene.
    public static bool IsLoading => Instance != null && Instance._running;

    // True only while a scene is actually being read in. A second request in that window is dropped; once the
    // new scene is live (settle frames, fade-out) a request is taken — a scene that sends the player straight
    // on from its first frame must not be swallowed by the card still fading off the last one.
    static bool InFlight => Instance != null && Instance._inFlight;

    // Lower depth draws in front. In front of ScreenFade's wipe and every other panel.
    public const int CardDepth = ScreenFade.WipeDepth - 100;

    // Timings, all unscaled seconds.
    public const float FadeInSeconds = 0.15f;   // up over the outgoing scene before the load starts
    public const float MinShowSeconds = 0.45f;  // a quick load still reads as a card, not a flicker
    public const float FadeOutSeconds = 0.25f;
    public const int SettleFrames = 3;          // new-scene frames held under the card (Start + first Updates)

    // How much of the bar the asynchronous read fills. The rest is the activation and the settle frames,
    // which report no progress of their own.
    public const float ReadShare = 0.85f;

    float _alpha;
    float _shownProgress;
    float _targetProgress;
    bool _running;
    bool _inFlight;
    GUIStyle _titleStyle;
    GUIStyle _titleSource;

    // Load a scene by name behind the card. Does what SceneManager.LoadScene did when there is no play mode
    // to show a card in (edit-mode callers), and ignores a second request while one is already under way.
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (!Application.isPlaying) { SceneManager.LoadScene(sceneName); return; }
        if (InFlight) { Debug.LogWarning($"LoadingScreen: '{sceneName}' asked for while a load is under way — ignored."); return; }
        Ensure().Begin(() => SceneManager.LoadSceneAsync(sceneName), sceneName);
    }

    // Same, for the callers that hold a build index.
    public static void Load(int buildIndex)
    {
        if (!Application.isPlaying) { SceneManager.LoadScene(buildIndex); return; }
        if (InFlight) { Debug.LogWarning($"LoadingScreen: build index {buildIndex} asked for while a load is under way — ignored."); return; }
        Ensure().Begin(() => SceneManager.LoadSceneAsync(buildIndex), "#" + buildIndex);
    }

    // The bar's reading for a given async progress. AsyncOperation.progress stops at 0.9 until the scene
    // activates, so 0.9 is the end of the read, not nine-tenths of it.
    public static float BarForReadProgress(float asyncProgress) =>
        Mathf.Clamp01(asyncProgress / 0.9f) * ReadShare;

    static LoadingScreen Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        return go.AddComponent<LoadingScreen>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Begin(System.Func<AsyncOperation> start, string label)
    {
        StopAllCoroutines();
        StartCoroutine(Run(start, label));
    }

    IEnumerator Run(System.Func<AsyncOperation> start, string label)
    {
        _running = true;
        _inFlight = true;
        _shownProgress = _targetProgress = 0f;
        float shownFor = 0f;

        // Up over the outgoing scene. Starting from wherever the card already is, so back-to-back loads
        // (a scene that loads another on its first frame) don't blink.
        for (float t = _alpha * FadeInSeconds; t < FadeInSeconds; t += Time.unscaledDeltaTime)
        {
            _alpha = Mathf.Clamp01(t / FadeInSeconds);
            shownFor += Time.unscaledDeltaTime;
            yield return null;
        }
        _alpha = 1f;
        yield return null;   // one frame fully covered and presented before the load can hitch anything

        AsyncOperation op = start();
        if (op == null)
        {
            // Not in the build, or refused. Say so and get out of the way rather than sit on a dead card.
            Debug.LogError($"LoadingScreen: could not start loading '{label}'.");
            _inFlight = false;
            yield return FadeOut();
            yield break;
        }

        while (!op.isDone)
        {
            _targetProgress = BarForReadProgress(op.progress);
            shownFor += Time.unscaledDeltaTime;
            yield return null;
        }

        _inFlight = false;

        // The new scene is live. Hold the card over its first frames, where the heavy one-off setup lands.
        _targetProgress = 1f;
        for (int i = 0; i < SettleFrames; i++)
        {
            shownFor += Time.unscaledDeltaTime;
            yield return null;
        }
        while (shownFor < MinShowSeconds)
        {
            shownFor += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeOut();
    }

    IEnumerator FadeOut()
    {
        float from = _alpha;
        for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            _alpha = from * (1f - Mathf.Clamp01(t / FadeOutSeconds));
            yield return null;
        }
        _alpha = 0f;
        _running = false;
    }

    void Update()
    {
        // The bar eases toward what the load reports, so a load that jumps 0 -> 0.9 in one read still moves.
        if (_running) _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress, Time.unscaledDeltaTime * 2.5f);
    }

    void OnGUI()
    {
        if (_alpha <= 0f) return;

        GUI.depth = CardDepth;
        float a = _alpha;
        float w = Screen.width, h = Screen.height;

        PixelGUI.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, a));

        // Title, centred a little above the middle.
        var source = PixelGUI.Heading;
        if (_titleStyle == null || _titleSource != source)
        {
            _titleSource = source;
            _titleStyle = new GUIStyle(source) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }
        float titleH = _titleStyle.CalcSize(new GUIContent("Loading")).y;
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(0f, Mathf.Round(h * 0.5f - titleH - PixelGUI.Px(4f)), w, titleH), "Loading", _titleStyle);
        GUI.color = prev;

        // The bar: ink outline, plate trough, gold fill — PixelGUI.Bar's construction, drawn by hand so the
        // whole thing fades with the card (Bar sets its own opaque colours).
        float barW = Mathf.Round(Mathf.Min(w * 0.5f, PixelGUI.Px(160f)));
        float barH = Mathf.Round(PixelGUI.Px(6f));
        var r = new Rect(Mathf.Round((w - barW) * 0.5f), Mathf.Round(h * 0.5f + PixelGUI.Px(4f)), barW, barH);
        float b = PixelGUI.Px(1f);
        PixelGUI.Fill(r, Faded(PixelGUI.Ink, a));
        var inner = new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f);
        PixelGUI.Fill(inner, Faded(PixelGUI.PlateLight, a));
        float fillW = Mathf.Round(inner.width * Mathf.Clamp01(_shownProgress));
        if (fillW > 0f) PixelGUI.Fill(new Rect(inner.x, inner.y, fillW, inner.height), Faded(PixelGUI.Gold, a));
    }

    static Color Faded(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);
}
