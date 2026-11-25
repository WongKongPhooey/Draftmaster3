using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Draftmaster.Controls;

// Mid-race team car switching (NASCAR Thunder-style): a button per team car lets the player jump into
// any car on their team (DriverLabel.teamId == 0). The car they leave is handed to the AI seamlessly
// (SplineDriver re-engaged from its current pose + SplineInputDriver feeding the shared dynamic model);
// the car they take stays with its AI, on Auto (DriveModeController.AdoptOnAuto) — V / AUTO and a pedal tap
// hand them the wheel, as they would in their own car. Driver NAMES swap
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

    [Header("HUD swap buttons")]
    [Tooltip("A swap button showing the car number (Resources/cup20num<N>) on the HUD for every other car on the player's team, in the bottom-right row " +
             "left of the broadcast TV and the crew chief's headset — a teammate one tap away during the race, " +
             "without opening the TEAM box (F3), which a phone has no key for.")]
    public bool hudButtons = true;
    [Tooltip("Width of a swap button for a car with no number art (a text 'Drive <name>' plate), in 640x360 UI pixels. " +
             "A car with number art gets a square icon button the size of the TV and headset (BroadcastController.buttonSize).")]
    public float hudButtonWidth = 64f;
    [Tooltip("Resources name prefix for the car-number art on the swap buttons: this + the number, e.g. 'cup20num' + 24.")]
    public string hudNumberPrefix = "cup20num";
    [Tooltip("Gap between the swap buttons, and between them and the TV/headset, in 640x360 UI pixels.")]
    public float hudButtonGap = 6f;
    [Tooltip("Margin from the bottom-right corner when neither the TV nor the headset is on the HUD (clears the " +
             "speedometer dial, as the headset's own buttonCorner does).")]
    public Vector2 hudFallbackCorner = new Vector2(92f, 12f);

    // What RacePositionTracker.playerName reads as when nobody has set a career name.
    public const string kPlaceholderName = "You";

    GameObject _current;                       // the car the human is driving right now
    readonly List<GameObject> _teamCars = new();
    readonly List<Button> _buttons = new();
    readonly List<TMP_Text> _buttonLabels = new();
    Canvas _canvas;
    RectTransform _window;
    Button _close;                             // touch only: a phone has no F3 to put the box away
    RectTransform _panel;
    float _rosterTimer, _labelTimer;
    bool _hidden = true;                       // toggleKey (starts down); the panel also hides itself in practice/qualifying

    public GameObject CurrentCar => _current;
    public bool Hidden { get => _hidden; set => _hidden = value; }   // toggleKey's state; SessionHud sets it too

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
        if ((toggleKey != KeyCode.None && LegacyKeys.Down(toggleKey)) || PadInput.PressedDriving(PadBindings.TeamBox))
            _hidden = !_hidden;

        // No roster in practice/qualifying — team cars are parked stint props there. And none at all
        // outside the player's own session: another championship's cars going past while the player is on
        // foot in the paddock are not theirs to climb into.
        bool raceOn = RaceWeekend.SessionLive && !RaceWeekend.IsPracticeLike;
        bool available = raceOn && !_hidden;
        bool hud = raceOn && HudButtonsWanted();
        // Hide the whole window, frame included — hiding only the content would leave an empty plate.
        if (_window != null && _window.gameObject.activeSelf != available)
            _window.gameObject.SetActive(available);
        if (_hudCanvas != null && _hudCanvas.gameObject.activeSelf != hud)
            _hudCanvas.gameObject.SetActive(hud);
        if (!available && !hud) return;

        bool touch = InputGlyphs.UsingTouch;
        if (_close != null && _close.gameObject.activeSelf != touch) _close.gameObject.SetActive(touch);

        _rosterTimer -= Time.deltaTime;
        if (_rosterTimer <= 0f) { _rosterTimer = rosterRefreshSeconds; RefreshRoster(); }

        _labelTimer -= Time.deltaTime;
        if (_labelTimer <= 0f) { _labelTimer = labelRefreshSeconds; RefreshButtonLabels(); }

        if (hud) UpdateHudButtons();
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

        // The car being left: already on Auto, the AI simply keeps it. Handing it back to the human first, to "leave
        // Auto", took off the input driver Auto had added - with Destroy, at the end of the frame, after HandToAI
        // below had picked that same driver up again - and the car was left with nothing steering it. It went into
        // the wall or the car ahead on every cycle through the team.
        var dm = FindObjectOfType<DriveModeController>();
        bool leavingOnAI = AIDriving(_current);
        if (dm != null && !dm.IsDriving && !leavingOnAI) dm.SetDriving(true);

        if (!leavingOnAI && !HandToAI(_current)) return;
        EnsureCautionBrain(_current);
        // The player arrives in the teammate's car on Auto: its AI keeps driving, and V / AUTO takes it over.
        // Without a drive-mode switch to take it back with, they get the wheel straight away.
        if (dm == null) TakeControl(target);
        else if (!AIDriving(target)) HandToAI(target);
        SwapDriverNames(_current, target);

        _current = target;
        if (dm != null) dm.AdoptOnAuto(target);
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
        _hudLayoutDirty = true;   // the car just left gets a button, the one just taken loses its
    }

    // A car left to the AI mid-race has to fall in behind the safety car at the next caution like the rest of the
    // field. A field car has its own FormationController from GridSpawner; the player's own car only gets one when
    // Auto takes it (and Auto's is switched off again when the player takes the wheel back), so without this the
    // player's original car raced on at full speed into the caution train.
    static void EnsureCautionBrain(GameObject car)
    {
        if (car == null) return;
        var spline = car.GetComponent<SplineDriver>();
        if (spline == null) return;
        var fc = car.GetComponent<FormationController>();
        if (fc != null && fc.enabled) return;
        var pvc = car.GetComponent<PlayerVehicleController>();
        FormationController.TakeOver(spline, pvc != null ? pvc.GridPosition : 0, dynamicModel: true);
    }

    // Hand the car the human is leaving to the AI, without a teleport: the spline brain re-engages at the
    // car's current pose, then SplineInputDriver drives the same dynamic model the human was just using.
    bool HandToAI(GameObject car) => AIHandover.ToAI(car, aiSteerGain, aiSpeedGain);

    // Give the human live control of a (dynamic-AI) team car: brains off, inputs live, obstacle
    // registration on so the rest of the field brakes for it like they do for any human.
    static void TakeControl(GameObject car) => AIHandover.ToHuman(car);

    // A team car mid-race is the AI's: spline brain engaged and its input driver feeding the dynamic model.
    static bool AIDriving(GameObject car)
    {
        var spline = car.GetComponent<SplineDriver>();
        var input = car.GetComponent<SplineInputDriver>();
        return spline != null && spline.enabled && input != null && input.enabled;
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

        var title = IronOvalUI.Label(_panel, "Title", "Team", IronOvalUI.Role.HeaderSmall);
        title.alignment = TextAlignmentOptions.Left;
        var tle = title.gameObject.AddComponent<LayoutElement>();
        tle.preferredWidth = 108f;
        tle.preferredHeight = 10f;
        _window = window;

        // On the window, not in the layout column, so it sits in the corner opposite the title.
        _close = IronOvalUI.Button(window, "Close", "X", new Vector2(14f, 12f));
        var xrt = (RectTransform)_close.transform;
        // A window's corner close glyph, not a button in the menu sense: it keeps the title row's height
        // rather than the standard button's, or it would hang down over the first car.
        xrt.sizeDelta = new Vector2(14f, 12f);
        xrt.anchorMin = xrt.anchorMax = xrt.pivot = new Vector2(1f, 1f);
        xrt.anchoredPosition = new Vector2(-4f, -4f);
        _close.onClick.AddListener(() => _hidden = true);
        _close.gameObject.SetActive(InputGlyphs.UsingTouch);

        BuildHudCanvas();
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
            le.preferredHeight = Draftmaster.Controls.UIButton.KitPx;   // one button height, as every screen's

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
        RebuildHudButtons();
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
            text.text = $"#{number} {name}{(pos > 0 ? $"  P{pos}" : "")}";
            // The car you are in takes the accent; the rest are ordinary rows you can click.
            text.color = mine ? PixelGUI.Gold : PixelGUI.Text;
            if (btn != null) btn.interactable = !mine;
        }
    }

    // ---- HUD swap buttons ----
    //
    // The TEAM box starts put away (SessionHud) and only F3 / LB brings it up, so a phone could never swap
    // cars at all. These sit on the HUD for the whole race instead: one "Drive #N" per team car other than
    // the one the player is in, along the bottom-right row left of the TV and the headset, stood on top of
    // the touch pedals while they show. Off while the player is on foot, on the pit wall or watching the
    // broadcast — the camera is away from the car then — and greyed under a caution or while the teammate
    // is on pit road, when SwitchTo would refuse anyway.

    Canvas _hudCanvas;
    CanvasScaler _hudScaler;
    readonly List<GameObject> _hudRoots = new();   // parallel to _teamCars; the current car's is hidden
    readonly List<Button> _hudButtons = new();
    readonly List<Graphic> _hudLabels = new();     // the caption or the number icon, greyed when the swap is refused
    bool _hudLayoutDirty = true;
    float _hudScale = -1f, _hudLift = -1f, _hudStartX = -1f;
    CrewChiefController _chief;
    bool _chiefLooked;

    void BuildHudCanvas()
    {
        if (!hudButtons) return;
        // The kit's 640x360 grid scaled off the screen's short side, as the TV and headset canvases are, so
        // the row lines up with them upright or flat.
        _hudCanvas = PixelUI.CreateCanvas("TeamSwapCanvas", 111);
        _hudScaler = _hudCanvas.GetComponent<CanvasScaler>();
        if (_hudScaler != null) _hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        FitHudCanvas();
        _hudCanvas.gameObject.SetActive(false);
    }

    // The player's own race is running and they are sat in a car the camera is following.
    bool HudButtonsWanted() =>
        hudButtons && _hudCanvas != null
        && RaceStart.IsRunning && !RaceDirector.InResults
        && OnFootController.Current == null
        && !BroadcastController.IsBroadcasting && !CrewChiefController.IsCrewChief;

    void RebuildHudButtons()
    {
        foreach (var r in _hudRoots) if (r != null) Destroy(r);
        _hudRoots.Clear();
        _hudButtons.Clear();
        _hudLabels.Clear();
        if (_hudCanvas == null) return;

        foreach (var car in _teamCars)
        {
            var carRef = car; // captured
            // The car's own number art, square and the size of the TV and headset beside it. A car with no art
            // (or no number) falls back to a text plate.
            var number = HudNumberSprite(car);
            Button btn;
            Graphic label;
            if (number != null)
            {
                btn = IronOvalUI.IconButton(_hudCanvas.transform, "TeamSwapButton", number, HudIconSize());
                var icon = btn.transform.Find("Icon");
                var img = icon != null ? icon.GetComponent<Image>() : null;
                if (img != null) { img.preserveAspect = true; img.color = Color.white; }   // the art's own colours
                label = img;
            }
            else
            {
                btn = IronOvalUI.TabButton(_hudCanvas.transform, "TeamSwapButton", HudCaption(car),
                                           new Vector2(hudButtonWidth, Draftmaster.Controls.UIButton.KitPx));
                label = btn.GetComponentInChildren<TMP_Text>(true);
            }
            btn.onClick.AddListener(() => SwitchTo(carRef));
            var root = btn.transform.parent.gameObject;   // TabButton/IconButton return the face; the root is the shadow
            _hudRoots.Add(root);
            _hudButtons.Add(btn);
            _hudLabels.Add(label);
        }
        _hudLayoutDirty = true;
    }

    Sprite HudNumberSprite(GameObject car)
    {
        var label = car != null ? car.GetComponent<DriverLabel>() : null;
        if (label == null || label.carNumber <= 0 || string.IsNullOrEmpty(hudNumberPrefix)) return null;
        return Resources.Load<Sprite>(hudNumberPrefix + label.carNumber);
    }

    // Square, matched to the TV and headset buttons it sits beside.
    static float HudIconSize()
    {
        var tv = BroadcastController.Current;
        if (tv != null && tv.buttonSize > 0f) return tv.buttonSize;
        var chief = FindFirstObjectByType<CrewChiefController>();
        return chief != null && chief.buttonSize > 0f ? chief.buttonSize : 32f;
    }

    // The number is on the car, so it is what the player picks a teammate by; a car without one goes by name.
    static string HudCaption(GameObject car)
    {
        var label = car != null ? car.GetComponent<DriverLabel>() : null;
        if (label != null && label.carNumber > 0) return $"Drive #{label.carNumber}";
        string name = label != null && !string.IsNullOrEmpty(label.driverName) ? label.driverName
                    : car != null ? car.name : "";
        return $"Drive {name}";
    }

    void UpdateHudButtons()
    {
        float scale = FitHudCanvas();
        float lift = Mathf.Ceil(TouchDriveControls.PedalsTopFromBottom / scale);
        float startX = HudStartX();
        if (_hudLayoutDirty || scale != _hudScale || lift != _hudLift || startX != _hudStartX)
        {
            _hudLayoutDirty = false;
            _hudScale = scale;
            _hudLift = lift;
            _hudStartX = startX;

            // Right to left from the TV: the original car first, then the teammates in grid order.
            float x = startX;
            for (int i = 0; i < _hudRoots.Count && i < _teamCars.Count; i++)
            {
                var root = _hudRoots[i];
                if (root == null) continue;
                bool show = _teamCars[i] != null && _teamCars[i] != _current;
                if (root.activeSelf != show) root.SetActive(show);
                if (!show) continue;
                var rt = (RectTransform)root.transform;
                HudCorner(rt, new Vector2(x, hudFallbackCorner.y + lift));
                x += rt.sizeDelta.x + hudButtonGap;   // sizeDelta: a phone grows the plate to its caption
            }
        }

        bool green = RaceStart.IsGreen;
        for (int i = 0; i < _hudButtons.Count && i < _teamCars.Count; i++)
        {
            var car = _teamCars[i];
            var btn = _hudButtons[i];
            if (car == null || btn == null || car == _current) continue;
            var spline = car.GetComponent<SplineDriver>();
            bool can = green && !(spline != null && spline.enabled && spline.IsOnPit);
            if (btn.interactable != can) btn.interactable = can;
            var text = i < _hudLabels.Count ? _hudLabels[i] : null;
            var colour = text is Image ? (can ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f))
                       : (can ? PixelGUI.Text : PixelGUI.TextDim);
            if (text != null && text.color != colour) text.color = colour;
        }
    }

    // Right edge of the row, in UI pixels in from the screen's right: one gap past whichever of the TV and
    // the headset reaches furthest left, or the headset's own spot when neither is in the corner.
    float HudStartX()
    {
        float x = 0f;
        var tv = BroadcastController.Current;
        if (tv != null && tv.CornerButtonShown) x = Mathf.Max(x, tv.buttonCorner.x + tv.buttonSize);
        if (!_chiefLooked) { _chief = FindFirstObjectByType<CrewChiefController>(); _chiefLooked = true; }
        if (_chief != null && _chief.CornerButtonShown) x = Mathf.Max(x, _chief.buttonCorner.x + _chief.buttonSize);
        return x > 0f ? x + hudButtonGap : hudFallbackCorner.x;
    }

    // As CrewChiefController.FitCanvasToShortSide — measured off Screen, the surface the canvas draws to.
    float FitHudCanvas()
    {
        float scale = DriveOrientation.CanvasScale(Screen.width, Screen.height, PixelUITheme.ReferenceHeight);
        if (_hudScaler != null && !Mathf.Approximately(_hudScaler.scaleFactor, scale)) _hudScaler.scaleFactor = scale;
        return scale;
    }

    // Pin a control to the bottom-right corner, `margin` UI pixels in from it.
    static void HudCorner(RectTransform rt, Vector2 margin)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-(margin.x + IronOvalRaceHUD.DialCornerClearance), margin.y);
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}
