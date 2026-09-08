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

    // The co-op row's memory of the attempt it is watching: whether the launcher was busy last frame, and
    // until when the row should be reporting that the last attempt failed.
    bool _coopWasBusy;
    float _coopFailedUntil;

    // How long "COULDN'T OPEN" stays on the row before it goes back to offering. Long enough to be read
    // on the way back from a several-second wait, short enough not to outlive the player's next try.
    const float FailureSeconds = 8f;

    // What the board was told to do this frame, settled once every layout scope has closed.
    //
    // Accepting or turning in from the board changes what the board draws — a ReadyToTurnIn row carries a
    // button, a Completed row does not — and IMGUI caches the control list during the Layout event that
    // precedes each real one. Doing it inline meant the pass after the click walked a different list than
    // Layout had recorded: "Mismatched LayoutGroup", thrown from inside the scroll view, so EndScrollView
    // and EndArea never ran and the clip stack was left unbalanced. Recording the press and settling it
    // below keeps each event self-consistent; the next one lays the new state out from scratch.
    QuestInfo _pendingAccept, _pendingTurnIn;

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
        TrackCoopAttempt();

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
        // Walking out of a race is the closest thing this game has to closing a save file, so it is dated:
        // the title screen the player lands on says where they were and when, under CONTINUE.
        CareerSave.Stamp();

        // Quitting a career ends the co-op session it was being played in. The launcher and its
        // NetworkManager are DontDestroyOnLoad, so without this the host walks out of the weekend and
        // arrives at the title screen STILL hosting: GameSession.CurrentMode is CoopCareer, the old join
        // code is still live, and pressing MULTIPLAYER cannot join anyone because NGO refuses to start a
        // client on a NetworkManager that is already a server. Leave() tears all of that down before the
        // scene load, and is harmless when there was never a session.
        if (NetworkLauncher.Instance != null) NetworkLauncher.Instance.Leave();
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
                  + rowH * 3f + gapH * 2f + (rowH + PixelGUI.Px(6f)) * 2f + gapH + PixelGUI.LineH + PixelGUI.Px(8f);
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

        // Co-op lives here rather than on the title screen because "open my career to a friend" only means
        // anything once there IS a career to open — the host keeps playing exactly where they are and the
        // guest is pulled to them. The join half is on the title screen, where an arriving guest starts.
        DrawCoopRow(new Rect(content.x, cy, content.width, row));
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

    // What the co-op row says, given the state around it. Split out of the drawing so the order of the
    // states is a rule that can be asserted rather than a shape buried in an IMGUI pass.
    //
    // `busy` comes first deliberately: it is the only one of these that is true the instant the player
    // clicks, and the click is the thing the row has to answer.
    public enum CoopRow { Offer, Retry, Opening, Guest, GuestConnected, Code }

    public static CoopRow CoopRowState(bool busy, bool active, bool isGuest, bool guestPresent,
                                       bool hasCode, bool failed)
    {
        if (busy) return CoopRow.Opening;
        if (!active) return failed ? CoopRow.Retry : CoopRow.Offer;
        if (isGuest) return CoopRow.Guest;
        if (guestPresent) return CoopRow.GuestConnected;
        return hasCode ? CoopRow.Code : CoopRow.Opening;
    }

    // Working dots that count 1-2-3. A static "Opening…" and a hung game look identical, and opening a
    // session is long enough (UGS sign-in, then a Relay allocation) for the difference to matter.
    static string WorkingDots()
    {
        int n = 1 + Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 3;
        return new string('.', n);
    }

    // One row, and every state it can be in. The join code is the point of the waiting state — it is what
    // the host reads out to their friend.
    //
    // Opening a session is several seconds of UGS sign-in and Relay allocation before one thing about the
    // game changes, and none of that used to reach this row: the tab still read PLAY WITH A FRIEND, so a
    // press that had in fact registered looked like a press that had missed, and the row only admitted to
    // working once the mode flipped at the far end of sign-in. The launcher's Busy flag is set
    // synchronously inside the click, so the very next repaint can say the work is under way — and a
    // failure puts the tab back with the reason on it rather than silently reverting to the offer.
    void DrawCoopRow(Rect r)
    {
        var launcher = NetworkLauncher.Instance;
        string code = launcher != null ? launcher.JoinCode : null;

        switch (CoopRowState(launcher != null && launcher.Busy, Coop.Active, Coop.IsGuest, Coop.GuestPresent,
                             !string.IsNullOrEmpty(code), Time.unscaledTime < _coopFailedUntil))
        {
            case CoopRow.Offer:
                if (PixelGUI.Tab(r, "PLAY WITH A FRIEND", false)) HostCoop();
                break;

            case CoopRow.Retry:
                // Still a tab, still clickable: the player's next move after a failed open is to try it
                // again, and a label they cannot press is a dead end.
                if (PixelGUI.Tab(r, "COULDN'T OPEN — RETRY", false)) HostCoop();
                break;

            case CoopRow.Opening:
                GUI.Label(r, "  Opening" + WorkingDots(), PixelGUI.Label);
                break;

            case CoopRow.Guest:
                GUI.Label(r, "  In your friend's weekend", PixelGUI.LabelDim);
                break;

            case CoopRow.GuestConnected:
                GUI.Label(r, "  Friend connected", PixelGUI.Label);
                break;

            case CoopRow.Code:
                // Gold, because this is the one thing on the panel the player has to read out loud.
                var was = GUI.color;
                GUI.color = PixelGUI.Gold;
                GUI.Label(r, $"  CODE  {code}", PixelGUI.Label);
                GUI.color = was;
                break;
        }
    }

    // Race scenes carry no NetworkLauncher — hosting co-op happens from inside a career, so make one.
    void HostCoop()
    {
        _coopFailedUntil = 0f;      // a fresh attempt, not the last one's result
        var launcher = NetworkLauncher.Instance != null
            ? NetworkLauncher.Instance
            : new GameObject("NetworkLauncher").AddComponent<NetworkLauncher>();
        launcher.HostCoop();
    }

    // Did the attempt we were watching end without a session? The launcher drops Busy either way and puts
    // the mode back to single player when it throws, so "was busy, isn't now, and co-op is not on" is the
    // failure — and the row says so for a few seconds instead of quietly offering the same tab again.
    //
    // Watched here rather than in OnGUI because IMGUI runs several event passes per frame and none of them
    // is a good place to keep a clock.
    void TrackCoopAttempt()
    {
        bool busy = NetworkLauncher.Instance != null && NetworkLauncher.Instance.Busy;
        if (_coopWasBusy && !busy && !Coop.Active) _coopFailedUntil = Time.unscaledTime + FailureSeconds;
        _coopWasBusy = busy;
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
                        _pendingAccept = q;
                    break;
                case QuestManager.State.Active:
                    GUILayout.Label(QuestManager.DescribeProgress(q), PixelGUI.Row);
                    break;
                case QuestManager.State.ReadyToTurnIn:
                    if (q.objective == QuestInfo.ObjectiveType.DeliverItem)
                        GUILayout.Label("Deliver it in person.", PixelGUI.Row);
                    else if (PixelGUI.Button(GUILayoutUtility.GetRect(content.width, PixelGUI.LineH + PixelGUI.Px(6f)), "TURN IN"))
                        _pendingTurnIn = q;
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

        // Outside every layout group: safe to change what the board says about itself.
        if (_pendingAccept != null) { QuestManager.Accept(_pendingAccept); _pendingAccept = null; }
        if (_pendingTurnIn != null) { QuestManager.Complete(_pendingTurnIn); _pendingTurnIn = null; }
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
