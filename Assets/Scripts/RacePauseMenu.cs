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
    bool _showMissions;
    Vector2 _missionScroll;
    Texture2D _px;
    GUIStyle _title, _label, _button, _toggle, _questTitle, _questBody;

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

        GUILayout.Space(10f);
        if (GUILayout.Button(_showMissions ? "MISSIONS ◂" : "MISSIONS ▸", _button, GUILayout.Height(34f)))
            _showMissions = !_showMissions;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("RESUME", _button, GUILayout.Height(40f))) Resume();
        GUILayout.Space(6f);
        GUILayout.Label("Esc to resume", _label);
        GUILayout.EndArea();

        if (_showMissions) DrawMissions(x + w + 12f, y);
    }

    // Mission board: every QuestInfo asset with its state, progress text, and the state-appropriate
    // action. Accept/turn-in here mirrors what a QuestGiverNPC would do, so quests are fully playable
    // in race scenes that have no walking NPCs. DeliverItem still hands over at its target NPC.
    void DrawMissions(float x, float y)
    {
        float w = 360f, h = 380f;
        if (x + w > Screen.width) x = Screen.width - w - 8f;

        GUI.color = new Color(0.09f, 0.09f, 0.12f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), _px);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(x + 16f, y + 14f, w - 32f, h - 28f));
        GUILayout.Label("MISSIONS", _title);
        GUILayout.Space(6f);
        _missionScroll = GUILayout.BeginScrollView(_missionScroll);

        var quests = QuestManager.All;
        int shown = 0;
        foreach (var q in quests)
        {
            if (q == null || string.IsNullOrEmpty(q.id)) continue;
            var state = QuestManager.GetState(q);
            bool locked = state == QuestManager.State.NotStarted && !QuestManager.PrerequisiteMet(q);
            if (locked) continue;   // hidden until its prerequisite quest is done
            shown++;

            GUILayout.Label(q.title, _questTitle);
            switch (state)
            {
                case QuestManager.State.NotStarted:
                    GUILayout.Label(q.description, _questBody);
                    if (GUILayout.Button("ACCEPT", _button, GUILayout.Height(28f)))
                        QuestManager.Accept(q);
                    break;
                case QuestManager.State.Active:
                    GUILayout.Label(QuestManager.DescribeProgress(q), _questBody);
                    break;
                case QuestManager.State.ReadyToTurnIn:
                    if (q.objective == QuestInfo.ObjectiveType.DeliverItem)
                        GUILayout.Label("Deliver it in person.", _questBody);
                    else if (GUILayout.Button("TURN IN", _button, GUILayout.Height(28f)))
                        QuestManager.Complete(q);
                    break;
                case QuestManager.State.Completed:
                    GUILayout.Label(string.IsNullOrEmpty(q.rewardText) ? "Done." : $"Done — {q.rewardText}", _questBody);
                    break;
            }
            GUILayout.Space(10f);
        }
        if (shown == 0) GUILayout.Label("No missions available.", _questBody);

        GUILayout.EndScrollView();
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
        _questTitle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _questTitle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        _questBody = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        _questBody.normal.textColor = new Color(0.85f, 0.85f, 0.9f);
    }
}
