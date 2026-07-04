using UnityEngine;
using UnityEngine.InputSystem;

// Pause menu for the spline-based race scenes. Esc freezes time and opens a small centred panel
// with the driving-aid toggles (racing line) and a resume button. Self-bootstraps like
// HandlingTuner so no scene wiring is needed; it only arms itself in scenes that have a
// TrackBuilder (i.e. actual race/practice scenes, not the menus).
public class RacePauseMenu : MonoBehaviour
{
    public static RacePauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    public Key toggleKey = Key.Escape;

    bool _inRaceScene;
    float _pollTimer;
    float _prevTimeScale = 1f;
    Texture2D _px;
    GUIStyle _title, _label, _button, _toggle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("RacePauseMenu");
        DontDestroyOnLoad(go);
        go.AddComponent<RacePauseMenu>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _px = new Texture2D(1, 1);
        _px.SetPixel(0, 0, Color.white);
        _px.Apply();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (IsPaused) Resume(); // never leave the game frozen if this object dies
        Instance = null;
    }

    void Update()
    {
        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer = 0.5f;
            bool inRace = FindAnyObjectByType<TrackBuilder>() != null;
            if (!inRace && IsPaused) Resume(); // scene changed out from under a pause
            _inRaceScene = inRace;
        }

        if (!_inRaceScene) return;
        if (Keyboard.current != null && toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = _prevTimeScale;
        AudioListener.pause = false;
    }

    void OnGUI()
    {
        if (!IsPaused) return;
        EnsureStyles();

        // Dim the whole frame so the frozen race reads as background.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _px);
        GUI.color = Color.white;

        float w = 340f, h = 240f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        GUI.color = new Color(0.09f, 0.09f, 0.12f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), _px);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(x + 24f, y + 20f, w - 48f, h - 40f));
        GUILayout.Label("PAUSED", _title);
        GUILayout.Space(14f);

        bool line = RacingLineDisplay.Visible;
        bool newLine = GUILayout.Toggle(line, "  Racing line", _toggle);
        if (newLine != line) RacingLineDisplay.Visible = newLine;

        bool map = TrackMiniMap.Visible;
        bool newMap = GUILayout.Toggle(map, "  Mini-map", _toggle);
        if (newMap != map) TrackMiniMap.Visible = newMap;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("RESUME", _button, GUILayout.Height(40f))) Resume();
        GUILayout.Space(6f);
        GUILayout.Label("Esc to resume", _label);
        GUILayout.EndArea();
    }

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _title.normal.textColor = Color.white;
        _label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        _label.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
        _button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
        _toggle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
        _toggle.onNormal.textColor = Color.white;
        _toggle.hover.textColor = Color.white;
        _toggle.onHover.textColor = Color.white;
    }
}
