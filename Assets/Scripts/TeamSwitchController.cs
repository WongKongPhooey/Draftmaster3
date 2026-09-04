using System.Collections.Generic;
using TMPro;
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
    [Tooltip("Car number stamped on the original player car's DriverLabel (livery/number stays with the chassis). 0 = read it off the car's livery sprite, which is what you want unless the paint and the number deliberately disagree.")]
    public int playerCarNumber = 0;

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
    [Tooltip("Key toggling the TEAM box.")]
    public KeyCode toggleKey = KeyCode.F3;

    // What RacePositionTracker.playerName reads as when nobody has set a career name.
    public const string kPlaceholderName = "You";

    GameObject _current;                       // the car the human is driving right now
    readonly List<GameObject> _teamCars = new();
    readonly List<Button> _buttons = new();
    readonly List<TMP_Text> _buttonLabels = new();
    Canvas _canvas;
    RectTransform _window;
    RectTransform _panel;
    float _rosterTimer, _labelTimer;
    bool _hidden;                              // toggleKey; the panel also hides itself in practice/qualifying

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
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) _hidden = !_hidden;

        // No roster in practice/qualifying — team cars are parked stint props there. And none at all
        // outside the player's own session: another championship's cars going past while the player is on
        // foot in the paddock are not theirs to climb into.
        bool available = RaceWeekend.SessionLive && !RaceWeekend.IsPracticeLike && !_hidden;
        // Hide the whole window, frame included — hiding only the content would leave an empty plate.
        if (_window != null && _window.gameObject.activeSelf != available)
            _window.gameObject.SetActive(available);
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

        // The number comes off the paintwork, so the player is whoever really races that car.
        if (label.carNumber == 0)
        {
            int fromLivery = playerCarNumber > 0 ? playerCarNumber : CarIdentity.NumberOf(playerCar);
            if (fromLivery >= 0) label.carNumber = fromLivery;
        }

        var spawner = FindObjectOfType<GridSpawner>();
        if (string.IsNullOrEmpty(label.carset) && spawner != null) label.carset = spawner.carsetPrefix;

        var rosterDriver = RosterLookup.ByCarNumber(label.carNumber);

        if (string.IsNullOrEmpty(label.driverName))
        {
            // A real career name wins; otherwise the player takes the identity of the driver who races
            // this car. "You" is the unconfigured placeholder, not a name, so it doesn't count as set.
            var rt = RacePositionTracker.Instance;
            string careerName = rt != null ? rt.playerName : null;
            bool haveCareerName = !string.IsNullOrEmpty(careerName) && careerName != kPlaceholderName;

            if (haveCareerName) label.driverName = careerName;
            else if (rosterDriver != null)
                label.driverName = !string.IsNullOrEmpty(rosterDriver.ShortName) ? rosterDriver.ShortName : rosterDriver.LastName;
            else label.driverName = kPlaceholderName;

            // Keep the position HUD and timing tower calling the player the same thing.
            if (rt != null && !haveCareerName) rt.playerName = label.driverName;
        }

        label.teamId = 0;
        if (string.IsNullOrEmpty(label.teamName))
        {
            if (rosterDriver != null && !string.IsNullOrEmpty(rosterDriver.TeamName)) label.teamName = rosterDriver.TeamName;
            else label.teamName = spawner != null ? spawner.playerTeamName : "Your Team";
        }
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
        // The window is a fixed frame around a growing list, so it is resized to whatever the content
        // ended up being: title row, one row per team car, and the kit's 6px margins.
        if (_window != null)
            _window.sizeDelta = new Vector2(120f, 22f + _teamCars.Count * 20f);

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

        // Iron Oval window on the kit's 640x360 canvas. IronOvalUI.Window puts the frame and the dithered
        // interior in as children, so the layout group goes on a content child of its own rather than on
        // the window root — a VerticalLayoutGroup there would try to lay out the frame too.
        _canvas = PixelUI.CreateCanvas("TeamSwitchCanvas", 110);

        var window = IronOvalUI.Window(_canvas.transform, "TeamPanel", new Vector2(120f, 80f));
        window.anchorMin = new Vector2(0f, 0f);
        window.anchorMax = new Vector2(0f, 0f);
        window.pivot = new Vector2(0f, 0f);
        window.anchoredPosition = new Vector2(8f, 8f);

        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                                       typeof(ContentSizeFitter));
        contentGO.transform.SetParent(window, false);
        _panel = contentGO.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(1f, 1f);
        _panel.pivot = new Vector2(0f, 1f);
        _panel.offsetMin = new Vector2(6f, 0f);
        _panel.offsetMax = new Vector2(-6f, -6f);

        var layout = contentGO.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        var fitter = contentGO.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = IronOvalUI.Label(_panel, "Title", "TEAM", IronOvalUI.Role.HeaderSmall);
        title.alignment = TextAlignmentOptions.Left;
        var tle = title.gameObject.AddComponent<LayoutElement>();
        tle.preferredWidth = 108f;
        tle.preferredHeight = 10f;
        _window = window;
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
            var btnGO = new GameObject("TeamCarButton", typeof(RectTransform), typeof(Image), typeof(Button),
                                       typeof(LayoutElement));
            btnGO.transform.SetParent(_panel, false);
            var plate = btnGO.GetComponent<Image>();
            plate.color = PixelGUI.PlateDeep;
            var le = btnGO.GetComponent<LayoutElement>();
            le.preferredWidth = 108f;
            le.preferredHeight = 16f;

            var btn = btnGO.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var colours = btn.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = Color.white;
            colours.pressedColor = Color.white;
            colours.selectedColor = Color.white;
            colours.disabledColor = Color.white;   // "mine" is shown by the label colour, not a grey plate
            colours.fadeDuration = 0f;             // no fades on pixel art
            btn.colors = colours;
            btn.onClick.AddListener(() => SwitchTo(carRef));
            _buttons.Add(btn);

            var label = IronOvalUI.Label(btnGO.transform, "Label", "", IronOvalUI.Role.Data);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4f, 0f);
            lrt.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Left;
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
            // The car you are in takes the accent; the rest are ordinary rows you can click.
            text.color = mine ? PixelGUI.Gold : PixelGUI.Text;
            if (btn != null) btn.interactable = !mine;
        }
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}
