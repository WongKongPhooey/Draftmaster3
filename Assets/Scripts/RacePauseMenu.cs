using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Pause menu for the spline-based race scenes. Esc freezes time and opens a small centred panel
// with the driving-aid toggles (racing line), a resume button and the way back to the title.
// Self-bootstraps like HandlingTuner so no scene wiring is needed; it only arms itself in scenes that
// have a TrackBuilder (i.e. actual race/practice scenes, not the menus).
//
// Drawn with the Iron Oval kit (PixelGUI): the frozen race goes behind the deep dither scrim rather than
// a black wash, so it still reads as the thing being paused, and the panel is the same framed plate the
// rest of the UI uses.
public class RacePauseMenu : MonoBehaviour
{
    public static RacePauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    public Key toggleKey = Key.Escape;
    [Tooltip("Scene the QUIT TO TITLE button loads. Falls back to build index 0 when it isn't in the build settings.")]
    public string titleSceneName = "TitleScreen";

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
            // The loaded package answers this without looking through the scene; only an authored track
            // (a legacy scene with its builder in it) needs the search.
            bool inRace = TrackPackage.ActiveTrack != null || FindAnyObjectByType<TrackBuilder>() != null;
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

    // Out of the weekend and back to the front of the game. Unpause first: loading a scene with time
    // frozen leaves the next one frozen too, since nothing there knows a pause was ever on.
    void QuitToTitle()
    {
        Resume();
        if (Application.CanStreamedLevelBeLoaded(titleSceneName)) SceneManager.LoadScene(titleSceneName);
        else SceneManager.LoadScene(0);   // the title is the first scene in the build list
    }

    void OnGUI()
    {
        if (!IsPaused) return;
        EnsureStyles();

        // The frozen race sits under the kit's scrim, not a flat black wash: still legible, clearly halted.
        PixelGUI.Scrim();

        // Rows are a line of the label face tall, so the panel is sized from them rather than a
        // literal 150 that was true while the face sat on an 8px cell.
        float rowH = PixelGUI.LineH, gapH = PixelGUI.Px(4f);
        float w = PixelGUI.Px(200f);
        float h = PixelGUI.Px(24f) + PixelGUI.Heading.fontSize + gapH * 5f + rowH * 2f
                  + rowH * 2f + gapH + (rowH + PixelGUI.Px(6f)) * 2f + gapH + PixelGUI.LineH + PixelGUI.Px(8f);
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Round((Screen.height - h) * 0.5f);

        PixelGUI.Panel(new Rect(x, y, w, h), focused: true);

        var content = PixelGUI.PanelContent(new Rect(x, y, w, h), 10f);
        float row = rowH, gap = gapH;
        float cy = content.y;

        GUI.Label(new Rect(content.x, cy, content.width, PixelGUI.Heading.fontSize), "PAUSED", _title);
        cy += PixelGUI.Heading.fontSize + gap;
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

        if (PixelGUI.Tab(new Rect(content.x, cy, content.width, row),
                         _showMissions ? "MISSIONS ◂" : "MISSIONS ▸", _showMissions))
            _showMissions = !_showMissions;
        cy += row + gap;

        // The weekend timetable: what else is on today besides the session you are sat in.
        if (PixelGUI.Tab(new Rect(content.x, cy, content.width, row), "WEEKEND SCHEDULE", false))
        {
            Resume();
            WeekendScheduleUI.Open();
        }
        cy += row + gap;

        float footer = PixelGUI.LineH;
        float buttonH = PixelGUI.LineH + PixelGUI.Px(6f);
        float resumeY = content.yMax - buttonH - footer;
        // The way back to the front of the game. Without it a race is a one-way trip and the only exit
        // from the demo is stopping play mode.
        if (PixelGUI.Button(new Rect(content.x, resumeY - buttonH - gap, content.width, buttonH), "QUIT TO TITLE"))
            QuitToTitle();
        if (PixelGUI.Button(new Rect(content.x, resumeY, content.width, buttonH), "RESUME")) Resume();
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

        float row = Mathf.Max(PixelGUI.Px(16f), PixelGUI.LineH);
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
                    if (PixelGUI.Button(GUILayoutUtility.GetRect(content.width, PixelGUI.LineH + PixelGUI.Px(6f)), "ACCEPT"))
                        QuestManager.Accept(q);
                    break;
                case QuestManager.State.Active:
                    GUILayout.Label(QuestManager.DescribeProgress(q), PixelGUI.Row);
                    break;
                case QuestManager.State.ReadyToTurnIn:
                    if (q.objective == QuestInfo.ObjectiveType.DeliverItem)
                        GUILayout.Label("Deliver it in person.", PixelGUI.Row);
                    else if (PixelGUI.Button(GUILayoutUtility.GetRect(content.width, PixelGUI.LineH + PixelGUI.Px(6f)), "TURN IN"))
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
