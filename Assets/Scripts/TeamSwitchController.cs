using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Mid-race team car switching (NASCAR Thunder-style): a button per team car lets the player jump into
// any car on their team (DriverLabel.teamId == 0). The car they leave is handed to the AI seamlessly
// (SplineDriver re-engaged from its current pose + SplineInputDriver feeding the shared dynamic model);
// the car they take gets AI brains disabled and live PlayerVehicleController input. Driver NAMES swap
// with the human (you are always "You" in the standings), while car number/livery stay with the chassis —
// a real driver swap.
//
// Requires the dynamic-AI field (GridSpawner.dynamicAI): only team cars that already carry a
// PlayerVehicleController are offered. Single-player only.
public class TeamSwitchController : MonoBehaviour
{
    [Header("Refs (auto-found if empty)")]
    [Tooltip("The original player car. Auto-finds the GameObject named playerCarName.")]
    public GameObject playerCar;
    public string playerCarName = "PlayerCar";
    [Tooltip("CameraFollow to retarget. Auto-found on Camera.main.")]
    public CameraFollow cameraFollow;

    [Header("Identity")]
    [Tooltip("Car number stamped on the original player car's DriverLabel (livery/number stays with the chassis).")]
    public int playerCarNumber = 3;

    [Header("AI Handover")]
    [Tooltip("Steering gain for the AI that inherits a car (SplineInputDriver.steerGain).")]
    public float aiSteerGain = 1.5f;
    [Tooltip("Speed-tracking gain for the AI that inherits a car (SplineInputDriver.speedGain).")]
    public float aiSpeedGain = 2f;

    [Header("UI")]
    [Tooltip("Seconds between scans for team cars joining/leaving the roster.")]
    public float rosterRefreshSeconds = 1f;
    [Tooltip("Seconds between button label updates (position readouts).")]
    public float labelRefreshSeconds = 0.5f;

    GameObject _current;                       // the car the human is driving right now
    readonly List<GameObject> _teamCars = new();
    readonly List<Button> _buttons = new();
    readonly List<Text> _buttonLabels = new();
    Canvas _canvas;
    RectTransform _panel;
    float _rosterTimer, _labelTimer;

    public GameObject CurrentCar => _current;

    void Start()
    {
        if (GameSession.IsMultiplayer) { enabled = false; return; }

        if (playerCar == null) playerCar = GameObject.Find(playerCarName);
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
        _current = playerCar;
        EnsurePlayerLabel();
        BuildUI();
        RefreshRoster();
    }

    void Update()
    {
        // No roster in practice/qualifying — team cars are parked stint props there.
        bool available = !RaceWeekend.IsPracticeLike;
        if (_panel != null && _panel.gameObject.activeSelf != available)
            _panel.gameObject.SetActive(available);
        if (!available) return;

        _rosterTimer -= Time.deltaTime;
        if (_rosterTimer <= 0f) { _rosterTimer = rosterRefreshSeconds; RefreshRoster(); }

        _labelTimer -= Time.deltaTime;
        if (_labelTimer <= 0f) { _labelTimer = labelRefreshSeconds; RefreshButtonLabels(); }
    }

    // The human's driver name persists on whichever car they occupy; the original chassis needs a label
    // so its identity (and the name swap) has somewhere to live.
    void EnsurePlayerLabel()
    {
        if (playerCar == null) return;
        var label = playerCar.GetComponent<DriverLabel>();
        if (label == null) label = playerCar.AddComponent<DriverLabel>();
        if (string.IsNullOrEmpty(label.driverName))
        {
            var rt = RacePositionTracker.Instance;
            label.driverName = rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You";
        }
        if (label.carNumber == 0) label.carNumber = playerCarNumber;
        label.teamId = 0;
        var spawner = FindObjectOfType<GridSpawner>();
        if (string.IsNullOrEmpty(label.teamName))
            label.teamName = spawner != null ? spawner.playerTeamName : "Your Team";
    }

    // Team roster = the original player car + every team-0 car that can actually be driven by the human
    // (has the shared dynamic model). Rebuilds buttons when membership changes.
    void RefreshRoster()
    {
        var found = new List<GameObject>();
        if (playerCar != null) found.Add(playerCar);
        foreach (var label in FindObjectsOfType<DriverLabel>())
        {
            if (label.teamId != 0 || label.gameObject == playerCar) continue;
            if (label.GetComponent<PlayerVehicleController>() == null) continue; // kinematic AI can't hand over input
            if (label.GetComponent<SplineDriver>() == null) continue;
            found.Add(label.gameObject);
        }

        bool changed = found.Count != _teamCars.Count;
        if (!changed)
            for (int i = 0; i < found.Count; i++)
                if (found[i] != _teamCars[i]) { changed = true; break; }
        if (!changed) return;

        _teamCars.Clear();
        _teamCars.AddRange(found);
        RebuildButtons();
    }

    // ---- Switching ----

    public void SwitchTo(GameObject target)
    {
        if (target == null || target == _current || !enabled) return;
        if (!RaceStart.IsGreen) return;   // pre-green the formation logic owns every car

        var targetSpline = target.GetComponent<SplineDriver>();
        if (targetSpline != null && targetSpline.enabled && targetSpline.IsOnPit) return; // not mid-pit-lane

        // Leave broadcast/crew-chief camera mode before swapping so control state is unambiguous.
        var dm = FindObjectOfType<DriveModeController>();
        if (dm != null && !dm.IsDriving) dm.SetDriving(true);

        if (!HandToAI(_current)) return;
        TakeControl(target);
        SwapDriverNames(_current, target);

        _current = target;
        if (dm != null) dm.RetargetPlayerCar(target);
        if (cameraFollow != null) cameraFollow.target = target.transform;
        if (RacePositionTracker.Instance != null) RacePositionTracker.Instance.SetLocalPlayer(target.transform);
        var hud = FindObjectOfType<PlayerTelemetryHUD>();
        if (hud != null) hud.target = target.GetComponent<PlayerVehicleController>();

        PlayerStatsLedger.Increment("teamswitches");
        RefreshButtonLabels();
    }

    // Hand the car the human is leaving to the AI, without a teleport: the spline brain re-engages at the
    // car's current pose, then SplineInputDriver drives the same dynamic model the human was just using.
    bool HandToAI(GameObject car)
    {
        if (car == null) return false;
        var pvc = car.GetComponent<PlayerVehicleController>();
        var spline = car.GetComponent<SplineDriver>();
        if (pvc == null || spline == null) return false;

        float mph = pvc.SpeedMph;

        pvc.enabled = false;   // drops the RaceObstacles registration while we rewire
        if (spline.track == null) spline.track = pvc.track;
        if (spline.vehicleInfo == null) spline.vehicleInfo = pvc.vehicleInfo;
        spline.spriteFacesUp = pvc.spriteFacesUp;
        spline.angleOffsetDeg = pvc.angleOffsetDeg;
        spline.enabled = true;                 // back into RaceField
        spline.EngageFromCurrentPose(mph);

        if (car.GetComponent<TireState>() == null) car.AddComponent<TireState>();
        var racing = car.GetComponent<AIRacingBehaviour>();
        if (racing == null) racing = car.AddComponent<AIRacingBehaviour>();
        racing.enabled = true;

        var input = car.GetComponent<SplineInputDriver>();
        if (input == null) input = car.AddComponent<SplineInputDriver>();
        input.steerGain = aiSteerGain;
        input.speedGain = aiSpeedGain;
        input.enabled = true;                  // OnEnable: externalMotionController=true, externalInput=true, reseeds

        var pit = car.GetComponent<PitStopController>();
        if (pit != null) pit.enabled = true;   // wear-based strategy resumes for the AI

        pvc.externalInput = true;
        pvc.enableWheelspin = false;           // AI parity with GridSpawner-built cars
        pvc.damageImpairsHandling = false;
        pvc.enabled = true;                    // OnEnable sees an enabled SplineDriver → not an obstacle
        return true;
    }

    // Give the human live control of a (dynamic-AI) team car: brains off, inputs live, obstacle
    // registration on so the rest of the field brakes for it like they do for any human.
    void TakeControl(GameObject car)
    {
        var pvc = car.GetComponent<PlayerVehicleController>();
        var spline = car.GetComponent<SplineDriver>();

        var input = car.GetComponent<SplineInputDriver>();
        if (input != null) input.enabled = false;
        var racing = car.GetComponent<AIRacingBehaviour>();
        if (racing != null) racing.enabled = false;
        var pit = car.GetComponent<PitStopController>();
        if (pit != null) pit.enabled = false;  // the human decides when to pit

        float mph = pvc.SpeedMph;
        float heading = pvc.HeadingDeg;
        Vector3 pos = car.transform.position;
        if (spline != null && spline.enabled)
        {
            if (!spline.externalMotionController) // kinematic fallback: the spline owned the transform
            {
                mph = spline.CurrentMph;
                heading = spline.CommandedHeadingDeg;
            }
            spline.enabled = false;            // out of RaceField — this car is the human now
        }

        pvc.externalInput = false;
        pvc.enableWheelspin = true;
        pvc.damageImpairsHandling = true;
        pvc.enabled = false;
        pvc.enabled = true;                    // OnEnable sees the spline disabled → registers as an obstacle
        pvc.SeedPose(pos, heading, mph / 2.237f);
    }

    // The human's name rides with the human; number + livery stay with the chassis.
    static void SwapDriverNames(GameObject a, GameObject b)
    {
        var la = a != null ? a.GetComponent<DriverLabel>() : null;
        var lb = b != null ? b.GetComponent<DriverLabel>() : null;
        if (la == null || lb == null) return;
        (la.driverName, lb.driverName) = (lb.driverName, la.driverName);
    }

    // ---- UI ----

    void BuildUI()
    {
        EnsureEventSystem();

        var canvasGO = new GameObject("TeamSwitchCanvas");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 110;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("TeamPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGO.transform.SetParent(canvasGO.transform, false);
        _panel = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0f, 0f);
        _panel.anchorMax = new Vector2(0f, 0f);
        _panel.pivot = new Vector2(0f, 0f);
        _panel.anchoredPosition = new Vector2(16f, 16f);

        panelGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.7f);
        var layout = panelGO.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 4f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        var fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = MakeLabel(panelGO.transform, "TEAM", 14, FontStyle.Bold, new Color(1f, 0.85f, 0.3f));
        title.alignment = TextAnchor.MiddleLeft;
        var tle = title.gameObject.AddComponent<LayoutElement>();
        tle.preferredWidth = 210f;
        tle.preferredHeight = 20f;
    }

    void RebuildButtons()
    {
        foreach (var b in _buttons) if (b != null) Destroy(b.gameObject);
        _buttons.Clear();
        _buttonLabels.Clear();
        if (_panel == null) return;

        foreach (var car in _teamCars)
        {
            var carRef = car; // captured
            var btnGO = new GameObject("TeamCarButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(_panel, false);
            btnGO.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
            var le = btnGO.GetComponent<LayoutElement>();
            le.preferredWidth = 210f;
            le.preferredHeight = 34f;

            var btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => SwitchTo(carRef));
            _buttons.Add(btn);

            var label = MakeLabel(btnGO.transform, "", 14, FontStyle.Bold, Color.white);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 0f);
            lrt.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleLeft;
            _buttonLabels.Add(label);
        }
        RefreshButtonLabels();
    }

    void RefreshButtonLabels()
    {
        var rt = RacePositionTracker.Instance;
        for (int i = 0; i < _teamCars.Count && i < _buttonLabels.Count; i++)
        {
            var car = _teamCars[i];
            var text = _buttonLabels[i];
            var btn = _buttons[i];
            if (car == null || text == null) continue;

            var label = car.GetComponent<DriverLabel>();
            string name = label != null && !string.IsNullOrEmpty(label.driverName) ? label.driverName : car.name;
            int number = label != null ? label.carNumber : 0;

            int pos = 0;
            if (rt != null)
                for (int e = 0; e < rt.Order.Count; e++)
                    if (rt.Order[e].tf == car.transform) { pos = rt.Order[e].position; break; }

            bool mine = car == _current;
            text.text = $"#{number} {name.ToUpperInvariant()}{(pos > 0 ? $"  P{pos}" : "")}";
            text.color = mine ? new Color(0.45f, 1f, 0.55f) : Color.white;
            if (btn != null) btn.interactable = !mine;
        }
    }

    static Text MakeLabel(Transform parent, string content, int size, FontStyle style, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}
