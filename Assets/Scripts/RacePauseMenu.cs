using UnityEngine;
using UnityEngine.InputSystem;

// Pause menu for the spline-based race scenes. Esc freezes time and opens a small centred panel
// with the driving-aid toggles (racing line) and a resume button. Self-bootstraps like
// HandlingTuner so no scene wiring is needed; it only arms itself in scenes that have a
// TrackBuilder (i.e. actual race/practice scenes, not the menus).
//
// Drawn with the Iron Oval kit (PixelGUI): the frozen race goes behind the deep dither scrim rather than
// a black wash, so it still reads as the thing being paused, and the panel is the same framed plate the
// rest of the UI uses.
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
    GUIStyle _title, _toggle;

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
        // The phone owns Escape while it's up: the first press puts it away, not the game on hold.
        if (PhoneUI.IsOpen) return;
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

        // The frozen race sits under the kit's scrim, not a flat black wash: still legible, clearly halted.
        PixelGUI.Scrim();

        float w = PixelGUI.Px(200f), h = PixelGUI.Px(150f);
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Round((Screen.height - h) * 0.5f);

        PixelGUI.Panel(new Rect(x, y, w, h), focused: true);

        var content = PixelGUI.PanelContent(new Rect(x, y, w, h), 10f);
        float row = PixelGUI.Px(16f), gap = PixelGUI.Px(4f);
        float cy = content.y;

        GUI.Label(new Rect(content.x, cy, content.width, row), "PAUSED", _title);
        cy += row + gap;
        PixelGUI.Rule(content.x, cy, content.width);
        cy += gap * 2f;

        bool line = RacingLineDisplay.Visible;
        bool newLine = GUI.Toggle(new Rect(content.x, cy, content.width, row), line, "  Racing line", _toggle);
        if (newLine != line) RacingLineDisplay.Visible = newLine;
        cy += row;

        bool map = TrackMiniMap.Visible;
        bool newMap = GUI.Toggle(new Rect(content.x, cy, content.width, row), map, "  Mini-map", _toggle);
        if (newMap != map) TrackMiniMap.Visible = newMap;
        cy += row + gap;

        if (PixelGUI.Tab(new Rect(content.x, cy, content.width, PixelGUI.Px(14f)),
                         _showMissions ? "MISSIONS ◂" : "MISSIONS ▸", _showMissions))
            _showMissions = !_showMissions;
        cy += PixelGUI.Px(14f) + gap;

        float footer = PixelGUI.Px(10f);
        float buttonH = PixelGUI.Px(20f);
        float buttonY = content.yMax - buttonH - footer;
        if (PixelGUI.Button(new Rect(content.x, buttonY, content.width, buttonH), "RESUME")) Resume();
        GUI.Label(new Rect(content.x, content.yMax - footer, content.width, footer), "ESC TO RESUME", PixelGUI.Footer);

        if (_showMissions) DrawMissions(x + w + PixelGUI.Px(6f), y);
    }

    // Mission board: every QuestInfo asset with its state, progress text, and the state-appropriate
    // action. Accept/turn-in here mirrors what a QuestGiverNPC would do, so quests are fully playable
    // in race scenes that have no walking NPCs. DeliverItem still hands over at its target NPC.
    void DrawMissions(float x, float y)
    {
        float w = PixelGUI.Px(220f), h = PixelGUI.Px(220f);
        if (x + w > Screen.width) x = Screen.width - w - PixelGUI.Px(6f);

        PixelGUI.Panel(new Rect(x, y, w, h));
        var content = PixelGUI.PanelContent(new Rect(x, y, w, h), 8f);

        float row = PixelGUI.Px(16f);
        GUI.Label(new Rect(content.x, content.y, content.width, row), "MISSIONS", _title);

        var listRect = new Rect(content.x, content.y + row, content.width, content.height - row);
        GUILayout.BeginArea(listRect);
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

            GUILayout.Label(q.title.ToUpperInvariant(), PixelGUI.HeadingSmall);
            switch (state)
            {
                case QuestManager.State.NotStarted:
                    GUILayout.Label(q.description, PixelGUI.Body);
                    if (PixelGUI.Button(GUILayoutUtility.GetRect(content.width, PixelGUI.Px(16f)), "ACCEPT"))
                        QuestManager.Accept(q);
                    break;
                case QuestManager.State.Active:
                    GUILayout.Label(QuestManager.DescribeProgress(q), PixelGUI.Row);
                    break;
                case QuestManager.State.ReadyToTurnIn:
                    if (q.objective == QuestInfo.ObjectiveType.DeliverItem)
                        GUILayout.Label("Deliver it in person.", PixelGUI.Row);
                    else if (PixelGUI.Button(GUILayoutUtility.GetRect(content.width, PixelGUI.Px(16f)), "TURN IN"))
                        QuestManager.Complete(q);
                    break;
                case QuestManager.State.Completed:
                    GUILayout.Label(string.IsNullOrEmpty(q.rewardText) ? "Done." : $"Done — {q.rewardText}",
                                    PixelGUI.Footer);
                    break;
            }
            GUILayout.Space(PixelGUI.Px(6f));
        }
        if (shown == 0) GUILayout.Label("No missions available.", PixelGUI.Row);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // Only the toggle needs building by hand: it is the one control here that draws Unity's own check box,
    // so it takes the kit's font and colours rather than a kit sprite.
    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(PixelGUI.Heading) { alignment = TextAnchor.MiddleCenter };
        _toggle = new GUIStyle(GUI.skin.toggle)
        {
            font = PixelGUI.Theme != null ? PixelGUI.Theme.imguiFont : null,
            fontSize = 16 * PixelGUI.Scale,
        };
        _toggle.normal.textColor = PixelGUI.TextDim;
        _toggle.onNormal.textColor = PixelGUI.Text;
        _toggle.hover.textColor = PixelGUI.Text;
        _toggle.onHover.textColor = PixelGUI.Text;
    }
}
