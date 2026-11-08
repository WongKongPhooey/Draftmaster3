#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// An AI-only race to watch from the editor: Draftmaster > AI > Watch AI-Only Race.
//
// The editor menu (AIOnlyRaceMenu) points the race scene at a track, marks the session live and leaves a one-shot
// flag in PlayerPrefs, then enters play mode straight into RaceScene. This picks the flag up before the scene loads
// and runs the race as a SINGLE RACE — no paddock, no weekend, the player car sat in its box — with three changes:
//
//   * the setup sheet is skipped, so nothing waits on a CONFIRM click;
//   * the moment the player car is live it goes on Auto (DriveModeController), so it is driven by the same dynamic
//     model + SplineInputDriver + AIRacingBehaviour as every car GridSpawner builds. An Auto car starts the
//     formation lap at once (FormationDirector.ThrottleStartsFormation), so the race runs on its own;
//   * a spectator camera: , and . step through the running order, [ and ] change game speed.
//
// V (or the Auto button) still hands the player car back to a human — useful to drop in and poke the AI.
// Editor-only: the whole file compiles out of a build.
public class AIOnlyRaceWatch : MonoBehaviour
{
    public const string FlagKey = "debug.aionlyrace";
    public const string CarsKey = "debug.aionlyrace.cars";
    public const string LapsKey = "debug.aionlyrace.laps";

    // This play session is an AI-only race. Survives the scene reloads inside the session (RACE AGAIN).
    public static bool Active { get; private set; }
    static int s_cars;
    static int s_laps;   // race distance; 0 = the scene's RaceDirector.raceLaps

    static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 3f, 4f };

    int _speedIndex = 2;
    Transform _watching;
    bool _prevHeld, _nextHeld, _slowerHeld, _fasterHeld;
    bool _handedOver;
    string _status = "Waiting for the field…";
    GUIStyle _style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        s_cars = 0;
        s_laps = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (PlayerPrefs.GetInt(FlagKey, 0) != 1) return;

        // One shot: the flag belongs to this play session only. A normal Play press afterwards is a normal game.
        s_cars = PlayerPrefs.GetInt(CarsKey, 0);
        s_laps = PlayerPrefs.GetInt(LapsKey, 0);
        PlayerPrefs.DeleteKey(FlagKey);
        PlayerPrefs.DeleteKey(CarsKey);
        PlayerPrefs.DeleteKey(LapsKey);
        PlayerPrefs.Save();

        Active = true;
        // The one-off race shape SingleRaceUI.Go sets up: one race, sat in the car, no career layer around it.
        GameSession.CurrentMode = GameSession.Mode.SingleRace;
        RaceWeekend.Current = RaceWeekend.Session.Race;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[AIOnlyRace] AI-only race at {TrackSelection.CurrentId}" +
                  (s_cars > 0 ? $", {s_cars} AI + the player car on Auto" : ", scene field size + the player car on Auto") +
                  (s_laps > 0 ? $", {s_laps} laps." : "."));
    }

    // After every Awake, before any Start — so the spawner and the pit start still read what is set here.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Active || mode != LoadSceneMode.Single) return;
        var pls = FindFirstObjectByType<PitLaneStart>();
        if (pls == null) return;   // not a race scene (results → title, say): nothing to watch

        pls.showSetupPanel = false;
        if (s_cars > 0)
            foreach (var gs in FindObjectsByType<GridSpawner>(FindObjectsSortMode.None))
                gs.count = s_cars;

        new GameObject("AIOnlyRaceWatch").AddComponent<AIOnlyRaceWatch>();
    }

    IEnumerator Start()
    {
        // The player car is handed to the AI once it is actually live: PitLaneStart enables its controller only
        // after the field is parked (DriveOnceTheFieldIsUp). Taking it any earlier would engage the spline from
        // the pose the car had before GridSpawner snapped it into its box.
        var pls = FindFirstObjectByType<PitLaneStart>();
        float deadline = Time.realtimeSinceStartup + 60f;
        while (pls != null && (pls.car == null || !pls.car.enabled) && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return new WaitForFixedUpdate();

        var drive = DriveModeController.Current;
        if (drive == null)
        {
            drive = new GameObject("DriveModeController (AI-only race)").AddComponent<DriveModeController>();
            if (pls != null && pls.car != null) drive.playerCar = pls.car.gameObject;
            yield return null;   // its Start finds the car and the camera
        }
        drive.SetDriving(false);
        _handedOver = !drive.IsDriving;
        _status = _handedOver ? "" : "Couldn't put the player car on Auto — press V.";
        if (!_handedOver) Debug.LogWarning("[AIOnlyRace] " + _status);
        else Debug.Log("[AIOnlyRace] Player car on Auto. , . = previous/next car, [ ] = game speed, V = take the wheel.");
    }

    void OnDisable()
    {
        if (Time.timeScale > 0f) Time.timeScale = 1f;
    }

    void Update()
    {
        // The race distance, until the green: RaceDirector reads raceLaps once, when the race goes green.
        var director = RaceDirector.Instance;
        if (s_laps > 0 && director != null && director.ScheduledLaps < 0 && director.raceLaps != s_laps)
            director.raceLaps = s_laps;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (Edge(kb[Key.Comma].isPressed, ref _prevHeld)) Step(-1);
        if (Edge(kb[Key.Period].isPressed, ref _nextHeld)) Step(+1);
        if (Edge(kb[Key.LeftBracket].isPressed, ref _slowerHeld)) _speedIndex = Mathf.Max(0, _speedIndex - 1);
        if (Edge(kb[Key.RightBracket].isPressed, ref _fasterHeld)) _speedIndex = Mathf.Min(Speeds.Length - 1, _speedIndex + 1);

        // Re-applied every frame so a pause menu that resumes to 1x does not lose the chosen speed; a paused game
        // (timeScale 0) is left alone.
        if (Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, Speeds[_speedIndex]))
            Time.timeScale = Speeds[_speedIndex];
    }

    void LateUpdate()
    {
        // A watched car that despawned (retired, parked) hands the camera back to the player car.
        if (_watching == null) return;
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cam != null && cam.target != _watching) cam.target = _watching;
    }

    static bool Edge(bool held, ref bool was)
    {
        bool pressed = held && !was;
        was = held;
        return pressed;
    }

    // Next / previous car in the running order, wrapping round.
    void Step(int dir)
    {
        var order = RacePositionTracker.Instance != null ? RacePositionTracker.Instance.Order : null;
        if (order == null || order.Count == 0) return;

        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        Transform current = _watching != null ? _watching : (cam != null ? cam.target : null);
        int at = -1;
        for (int i = 0; i < order.Count; i++)
            if (order[i] != null && order[i].tf == current) { at = i; break; }

        for (int tries = 0; tries < order.Count; tries++)
        {
            at = at < 0 ? (dir > 0 ? 0 : order.Count - 1) : (at + dir + order.Count) % order.Count;
            if (order[at] != null && order[at].tf != null)
            {
                _watching = order[at].tf;
                if (cam != null) cam.target = _watching;
                return;
            }
        }
    }

    void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.Max(12, Screen.height / 50),
                padding = new RectOffset(8, 8, 4, 4),
            };
            _style.normal.textColor = Color.white;
        }

        string watching = "player car";
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        var order = RacePositionTracker.Instance != null ? RacePositionTracker.Instance.Order : null;
        if (cam != null && cam.target != null && order != null)
            for (int i = 0; i < order.Count; i++)
                if (order[i] != null && order[i].tf == cam.target)
                {
                    watching = $"P{order[i].position} #{order[i].carNumber} {order[i].name}";
                    break;
                }

        string line = $"AI-ONLY RACE   watching {watching}   {Speeds[_speedIndex]:0.##}x" +
                      "   [ , . ] car   [ [ ] ] speed   [V] take the wheel";
        if (!string.IsNullOrEmpty(_status)) line += "   — " + _status;
        var size = _style.CalcSize(new GUIContent(line));
        GUI.Box(new Rect(8f, 8f, size.x, size.y), line, _style);
    }
}
#endif
