using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Draftmaster.Controls;

// Driving vs Auto, the hand-off itself.
// - Driving ON  : the player drives their car normally (PlayerVehicleController).
// - Driving OFF : Auto. The car is handed to the AI (kinematic SplineDriver) and the camera stays on it. Watching
//                 the rest of the field is the crew chief's job (CrewChiefController).
// - Flipping Auto back OFF does not hand the car straight back: the AI keeps driving until the player taps the
//   accelerator or the brake (AutoHandBack), so control changes hands on a pedal the player chose to press, not
//   mid-corner with their thumbs off everything. Toggling again while it waits keeps Auto on. Only the player's
//   own toggle waits; a system putting them back in the car (SetDriving(true)) hands it over at once.
//
// Handing the car over is also what happens when the player steps out of it, so it is driven by whoever they
// step into — the crew chief's headset in the HUD corner (CrewChiefController), the TEAM panel, co-op
// possession — plus the keyboard shortcut below and an AUTO button: the touch controls' top row on a phone,
// and on PC one of its own, stacked above the headset (AUTO with an ON / OFF chip, green ON while the AI has the car).
//
// Draftmaster 2's lane controls (OPTIONS > AI Drivers > Controls: Lanes, LegacyLaneControls): the car is put on Auto
// as soon as the player is racing it - rolling off pit road, or on the grid before the start - and left / right no
// longer steer it. Each press moves it a lane across the road (Draftmaster.Controls.LaneSelect): the inside line, the
// middle line or the outside line, which the AI then drives (AIRacingBehaviour.playerLane), speed and following its
// own. V and the AUTO button still take the car back (the usual pedal-tap hand-back), and the lanes then leave it
// with the player - no re-engaging at speed - until they put it on Auto again.
public class DriveModeController : MonoBehaviour
{
    [Header("Refs (auto-found if empty)")]
    [Tooltip("The player's car. Auto-finds the GameObject named playerCarName.")]
    public GameObject playerCar;
    public string playerCarName = "PlayerCar";
    [Tooltip("CameraFollow to retarget. Auto-found on Camera.main.")]
    public CameraFollow cameraFollow;

    [Header("Auto")]
    [Tooltip("Start the scene with the AI driving the player's car rather than the player.")]
    public bool startInBroadcast = false;

    [Header("Input")]
    [Tooltip("Keyboard shortcut to hand the car to the AI and back. The only control on this component " +
             "since the DRIVING tab was folded into the crew chief's headset button.")]
    public Key toggleKey = Key.V;

    [Tooltip("When true, another system (e.g. CrewChiefController) owns the camera, so handing the car over won't retarget it.")]
    public bool suppressBroadcastCamera = false;

    [Header("HUD (PC)")]
    [Tooltip("Gap from the bottom-right corner to the AUTO button, in 640x360 UI pixels. The default stands it " +
             "on top of the crew chief's headset (CrewChiefController.buttonCorner + buttonSize + 6), where the " +
             "chief's TIMING stack goes — that only shows while the player is on foot, and this only in the car.")]
    public Vector2 buttonCorner = new Vector2(92f, 50f);

    [Header("Lane controls (Draftmaster 2)")]
    [Tooltip("With the lane controls on, the player's car goes onto Auto once it is rolling at this speed (mph) off " +
             "pit road. Below it - pulling out of the pit box - the player still drives. On the grid before the start " +
             "it goes onto Auto whatever the speed.")]
    public float laneEngageMph = 20f;

    [Header("Pit stops on Auto")]
    [Tooltip("While Auto drives the player's car in a race, the AI brings it in for repairs: under a caution at the " +
             "first pits-open pass with any damage, under green once its bodywork damage (VehicleDamage.DamageLevel, " +
             "0..1 — a full-severity hit adds ~0.18) reaches this. 0 = only under a caution.")]
    [Range(0f, 1f)] public float autoGreenRepairDamage = 0.15f;

    public bool IsDriving => _driving;
    // Auto has been switched off and the AI is still driving, waiting for the player to tap a pedal.
    public bool HandBackPending => _handBack.Waiting;
    public GameObject PlayerCar => playerCar;
    // The player's own car while Auto drives it for them from the seat. The AI input driver Auto bolts on means
    // PlayerVehicleController.Human no longer answers with it, and the race HUD - position, lap, speed, tyres - still
    // belongs to it (PlayerVehicleController.Local). Null while the player drives (Human has the car), on foot, from
    // the pit wall, or while the AI brings the car home at the end of a session. The controller can be switched off:
    // behind the safety car FormationController hands the car to its SplineDriver.
    public PlayerVehicleController AutoCar => !_driving && InSeat ? _pvc : null;

    PlayerVehicleController _pvc;
    SplineDriver _spline;
    PitLaneStart _zoom;      // owns the camera ortho lerp; null on scenes without the pit-start flow
    bool _driving = true;
    GameObject _inputAddedTo;   // the car Auto put a SplineInputDriver on, to take it off again on the way back

    bool _keyPrev;
    readonly AutoHandBack _handBack = new AutoHandBack();
    readonly LaneSelect _laneSelect = new LaneSelect();
    bool _laneActive;            // the lane controls have the player's lane on the car's brain
    // The player took the car back off the lane controls (V / AUTO): they drive it, and the lanes do not put it back on
    // Auto until they are on Auto again in the seat with no hand-back pending.
    bool _laneOptOut;
    AIRacingBehaviour _laneBrain;
    // The car was on Auto when it was towed in (TowedToPits): Auto goes back on once the driver is sat in it again.
    bool _autoAfterTow;

    // The Draftmaster 2 lane controls are driving the player's car: Auto on, left / right picking the lane.
    public bool LaneSteering => _laneActive;
    // The lane they have it in, by name (Inside / Middle / Outside, or Left / Right on a road course). "" when off.
    public string LaneName => _laneActive && _laneBrain != null ? _laneBrain.PlayerLaneName(_laneSelect.Lane) : "";

    // The scene's one auto-drive switch, for the touch controls' AUTO button (a phone has no V).
    public static DriveModeController Current { get; private set; }
    void OnEnable() => Current = this;
    void OnDisable() { if (Current == this) Current = null; }

    void Start()
    {
        if (playerCar == null) playerCar = GameObject.Find(playerCarName);
        if (playerCar != null)
        {
            _pvc = playerCar.GetComponent<PlayerVehicleController>();
            _spline = playerCar.GetComponent<SplineDriver>();
        }
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
        _zoom = FindFirstObjectByType<PitLaneStart>();

        if (startInBroadcast) SetDriving(false);
        // Broadcast mode (the TV button beside the crew chief's headset) rides along with the car hand-off it uses.
        if (GetComponent<BroadcastController>() == null) gameObject.AddComponent<BroadcastController>();
        // A phone has the touch controls' own AUTO button in its top row.
        if (!TouchDriveControls.TouchPlatform) BuildButton();
    }

    void Update()
    {
        if (Keyboard.current != null && toggleKey != Key.None)
        {
            bool held = Keyboard.current[toggleKey].isPressed;
            if (held && !_keyPrev) Toggle();
            _keyPrev = held;
        }

        // On a pad it is a face button that means "back" everywhere else, so only from the seat and only
        // with nothing open over the race.
        if (PadInput.PressedDriving(PadBindings.DriveBroadcast)) Toggle();

        UpdateAutoAfterTow();
        UpdateHandBack();
        UpdateLanes();
        UpdateButton();
    }

    // ---- The tow ----

    // PitLaneStart.TowToPits is about to park the player's car in its box. On Auto, the AI is taken off it the way
    // V takes it off - Auto off, the brain and its pit stop dropped, the car the human's - rather than the tow
    // switching the brain off underneath this component. That left Auto "on" here with nobody driving: the car came
    // back from the repair under the player's controls while the HUD, the touch controls (pedals hidden) and the
    // lane controls all still read it as Auto. A car that was on Auto goes back on Auto once the driver is back in
    // it (UpdateAutoAfterTow), lane controls and all. A car the player had asked to take back (hand-back waiting)
    // stays theirs.
    public void TowedToPits(GameObject car)
    {
        if (car == null || car != playerCar) return;
        _autoAfterTow = !_driving && !_handBack.Waiting && InSeat;
        SetDriving(true);
    }

    // Back in the towed car with its controls live - climbed back in after the crew finished, or handed back by the
    // repair in a race with no on-foot body - and nothing over the race: Auto goes back on.
    void UpdateAutoAfterTow()
    {
        if (!_autoAfterTow) return;
        if (!_driving) { _autoAfterTow = false; return; }   // on Auto again some other way
        if (!InSeat || PadInput.ModalOpen || _pvc == null || !_pvc.enabled) return;
        if (_zoom != null && !_zoom.IsDriving) return;
        _autoAfterTow = false;
        SetDriving(false);
    }

    // ---- Draftmaster 2 lane controls ----

    // In the seat and the car theirs: not on foot, not running the race from the pit wall, not being brought home.
    bool InSeat => playerCar != null && !PadInput.OnFoot && !CrewChiefController.IsCrewChief &&
                   !PracticeDirector.BringingPlayerIn && !RaceEndPitIn.HandedOverAtLine;

    void UpdateLanes()
    {
        if (!LegacyLaneControls.Enabled) { _laneOptOut = false; StopLanes(); return; }
        // Back on Auto from the seat with nothing waiting to hand it over (V again, the AUTO button, the hand-back
        // cancelled): the opt-out is over and the lanes pick the car up again. Not while out of the seat - the crew
        // chief's headset puts the car on Auto and hands it back, and the player should get it back as they left it.
        if (_laneOptOut && !_driving && !_handBack.Waiting && InSeat) _laneOptOut = false;
        // The player asked for the car back: while the AI waits for the pedal tap it drives its own line, and once the
        // player has it the lanes leave it alone.
        if (_laneOptOut) { StopLanes(); return; }
        if (_driving)
        {
            StopLanes();
            // Racing it: put it on Auto. Not from the pit box - the player drives it out - and not over a menu.
            if (!InSeat || PadInput.ModalOpen || _pvc == null || PlayerVehicleController.Human != _pvc) return;
            bool onGrid = RaceStart.IsPreGrid || RaceStart.IsFormation;
            var limiter = playerCar.GetComponent<PitLimiter>();
            bool onPitRoad = limiter != null && limiter.InPitZone;
            if (onPitRoad || (!onGrid && _pvc.SpeedMph < laneEngageMph)) return;
            SetDriving(false);
            if (_driving) return;
        }
        if (!InSeat) { StopLanes(); return; }

        var brain = playerCar.GetComponent<AIRacingBehaviour>();
        if (brain == null) return;
        float steer = ReadSteer();
        if (!_laneActive || brain != _laneBrain)
        {
            StopLanes();
            _laneBrain = brain;
            _laneActive = true;
            // Start in the lane the car is nearest, so taking over does not swerve it; a held turn has to be let go.
            _laneSelect.Reset(brain.NearestPlayerLane(), steer);
        }
        else if (!PadInput.ModalOpen) _laneSelect.Update(steer);
        brain.playerLane = _laneSelect.Lane;
    }

    // The lane goes back to the brain to choose.
    void StopLanes()
    {
        if (!_laneActive) return;
        _laneActive = false;
        if (_laneBrain != null) _laneBrain.playerLane = AIRacingBehaviour.NoPlayerLane;
        _laneBrain = null;
    }

    // Left / right, read the way PlayerVehicleController reads its steering when the human drives: the left stick,
    // the phone's steer buttons, A / D or the arrow keys - whatever the car's control scheme listens to. Not the
    // d-pad: from the seat its left and right open the tyre and rivalry panels.
    float ReadSteer()
    {
        float steer = 0f;
        var scheme = _pvc != null ? _pvc.controlScheme : PlayerVehicleController.ControlScheme.Auto;
        if (scheme != PlayerVehicleController.ControlScheme.Keyboard)
        {
            var gp = Gamepad.current;
            if (gp != null) steer = gp.leftStick.ReadValue().x;
        }
        if (scheme == PlayerVehicleController.ControlScheme.Auto && TouchDriveControls.Active && TouchDriveControls.Steer != 0f)
            steer = TouchDriveControls.Steer;
        if (scheme != PlayerVehicleController.ControlScheme.Gamepad)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer = -1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer = 1f;
            }
        }
        return steer;
    }

    // ---- Hand-back on a pedal tap ----

    void UpdateHandBack()
    {
        if (!_handBack.Waiting) return;
        // Out of the seat (crew chief, on foot) or the car is being brought home: the wait is off and Auto stays on.
        if (_driving || PadInput.OnFoot || CrewChiefController.IsCrewChief ||
            PracticeDirector.BringingPlayerIn || RaceEndPitIn.HandedOverAtLine)
        {
            _handBack.Cancel();
            return;
        }
        ReadPedals(out float throttle, out float brake);
        // A menu over the race owns the pedals' keys; a pedal held through it is not a tap when it closes.
        if (PadInput.ModalOpen) { _handBack.Arm(throttle, brake); return; }
        if (_handBack.Update(throttle, brake)) SetDriving(true);
    }

    // The player's pedals, read the way PlayerVehicleController reads them when the human drives: pad triggers,
    // the phone's touch pedals, W/S or the arrow keys — whatever the car's control scheme listens to.
    void ReadPedals(out float throttle, out float brake)
    {
        throttle = 0f; brake = 0f;
        var scheme = _pvc != null ? _pvc.controlScheme : PlayerVehicleController.ControlScheme.Auto;
        if (scheme != PlayerVehicleController.ControlScheme.Keyboard)
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                throttle = gp.rightTrigger.ReadValue();
                brake = gp.leftTrigger.ReadValue();
            }
        }
        if (scheme == PlayerVehicleController.ControlScheme.Auto && TouchDriveControls.Active)
        {
            throttle = Mathf.Max(throttle, TouchDriveControls.Throttle);
            brake = Mathf.Max(brake, TouchDriveControls.Brake);
        }
        if (scheme != PlayerVehicleController.ControlScheme.Gamepad)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1f;
            }
        }
    }

    // ---- PC button ----
    // Drawn as the Draftmaster3_UI_Unity design's RaceHUD_PC toggle (2026-11-11): a 72x26 dark plate with a 3px
    // green border outside it, "AUTO" in cream Silkscreen tracked 2px, and a state chip beside it - green with
    // dark "ON" while the AI has the car, slate with grey "OFF" while the player drives.

    // The design's measurements and colours, in kit pixels (the canvas is the kit's 640x360 grid).
    static readonly Vector2 ButtonSize = new Vector2(72f, 26f);
    const float ButtonBorder = 3f;
    const float ChipRight = 8f, ChipTop = 7f, ChipH = 12f, ChipMinW = 19f;
    static readonly Color ButtonPlate = new Color32(0x12, 0x14, 0x1c, 0xdd);
    static readonly Color ChipOffFill = DriveScoutStyle.Edge;
    static readonly Color ChipOnText = new Color32(0x0b, 0x0c, 0x11, 0xff);
    const float SilkscreenTrack2Px = 25f;   // TMP characterSpacing is 1/100 em: 2px at the 8px cell

    GameObject _buttonRoot;
    RectTransform _chip;
    Image _chipFill;
    TMP_Text _chipLabel;
    GameObject _hint;
    TMP_Text _hintText;
    bool _buttonShowsDriving;

    void BuildButton()
    {
        var canvas = PixelUI.CreateCanvas("AutoDriveCanvas", 111);
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // The root is the border's outer edge, so buttonCorner keeps meaning the button's bottom-right corner.
        float b = ButtonBorder;
        var rootGo = new GameObject("AutoButton", typeof(RectTransform));
        rootGo.transform.SetParent(canvas.transform, false);
        var root = (RectTransform)rootGo.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = ButtonSize + new Vector2(b * 2f, b * 2f);
        root.anchoredPosition = new Vector2(-buttonCorner.x, buttonCorner.y);

        // The border as four strips: the plate is see-through, so a solid green fill under it would tint it.
        Strip(root, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -b), Vector2.zero);
        Strip(root, "BorderBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, b));
        Strip(root, "BorderLeft", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, b), new Vector2(b, -b));
        Strip(root, "BorderRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-b, b), new Vector2(0f, -b));

        var face = new GameObject("Face", typeof(RectTransform), typeof(Image), typeof(Button));
        face.transform.SetParent(root, false);
        var frt = (RectTransform)face.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(b, b);
        frt.offsetMax = new Vector2(-b, -b);
        var faceImg = face.GetComponent<Image>();
        faceImg.color = ButtonPlate;
        var button = face.GetComponent<Button>();
        button.targetGraphic = faceImg;
        button.onClick.AddListener(Toggle);

        var auto = IronOvalUI.Label(frt, "Label", "AUTO", IronOvalUI.Role.HeaderSmall, DriveScoutStyle.Text);
        auto.characterSpacing = SilkscreenTrack2Px;
        auto.alignment = TextAlignmentOptions.MidlineLeft;
        auto.textWrappingMode = TextWrappingModes.NoWrap;
        var art = auto.rectTransform;
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0f, 1f);
        art.anchoredPosition = new Vector2(8f, -8f);   // the 8px caps sit 9px down; the line box is 10 tall
        art.sizeDelta = new Vector2(36f, 10f);

        var chipGo = new GameObject("Chip", typeof(RectTransform), typeof(Image));
        chipGo.transform.SetParent(frt, false);
        _chip = (RectTransform)chipGo.transform;
        _chip.anchorMin = _chip.anchorMax = _chip.pivot = new Vector2(1f, 1f);
        _chip.anchoredPosition = new Vector2(-ChipRight, -ChipTop);
        _chip.sizeDelta = new Vector2(ChipMinW, ChipH);
        _chipFill = chipGo.GetComponent<Image>();
        _chipFill.raycastTarget = false;

        _chipLabel = IronOvalUI.Label(_chip, "State", "ON", IronOvalUI.Role.HeaderSmall, ChipOnText);
        _chipLabel.characterSpacing = 0f;
        _chipLabel.alignment = TextAlignmentOptions.Center;
        _chipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var crt = _chipLabel.rectTransform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(0f, 0f);
        crt.offsetMax = new Vector2(0f, 0f);

        // Over the button while Auto is off but the AI still has the car: what takes it back.
        var hint = IronOvalUI.Label(root, "HandBackHint", "TAP GAS OR BRAKE", IronOvalUI.Role.HeaderSmall, DriveScoutStyle.Text);
        hint.alignment = TextAlignmentOptions.BottomRight;
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        hint.raycastTarget = false;
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(1f, 0f);
        hrt.anchoredPosition = new Vector2(0f, 3f);
        hrt.sizeDelta = new Vector2(160f, 10f);
        _hint = hint.gameObject;
        _hintText = hint;
        _hint.SetActive(false);

        _buttonRoot = rootGo;
        BuildPitButton(canvas.transform, root);
        _buttonShowsDriving = !_driving;   // forces the first paint
        UpdateButton();
    }

    // ---- PIT button (Auto only) ----
    // Auto saves fuel only gently (SplineInputDriver.fuelSaveMinThrottle), so a car short of it has to come in: under
    // pitButtonFuelLitres, while Auto drives a race, a PIT button sits left of AUTO in the same style - amber border -
    // and tapping it books the stop (PitStopController.RequestStop), taken at the next pit entry pit road allows.
    [Header("Pit button (Auto)")]
    [Tooltip("The PIT button shows while Auto drives a race with less than this much fuel (litres) in the tank.")]
    public float pitButtonFuelLitres = 15f;
    static readonly Vector2 PitButtonSize = new Vector2(44f, 26f);
    static readonly Color PitAmber = new Color32(0xe0, 0xa8, 0x3a, 0xff);
    GameObject _pitRoot;
    TMP_Text _pitLabel;

    void BuildPitButton(Transform canvas, RectTransform autoRoot)
    {
        float b = ButtonBorder;
        var rootGo = new GameObject("PitButton", typeof(RectTransform));
        rootGo.transform.SetParent(canvas, false);
        var root = (RectTransform)rootGo.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = PitButtonSize + new Vector2(b * 2f, b * 2f);
        root.anchoredPosition = new Vector2(-buttonCorner.x - autoRoot.sizeDelta.x - 6f, buttonCorner.y);

        foreach (var strip in new[] {
                     (n: "BorderTop", a: new Vector2(0f, 1f), c: new Vector2(1f, 1f), lo: new Vector2(0f, -b), hi: Vector2.zero),
                     (n: "BorderBottom", a: Vector2.zero, c: new Vector2(1f, 0f), lo: Vector2.zero, hi: new Vector2(0f, b)),
                     (n: "BorderLeft", a: Vector2.zero, c: new Vector2(0f, 1f), lo: new Vector2(0f, b), hi: new Vector2(b, -b)),
                     (n: "BorderRight", a: new Vector2(1f, 0f), c: Vector2.one, lo: new Vector2(-b, b), hi: new Vector2(0f, -b)) })
        {
            Strip(root, strip.n, strip.a, strip.c, strip.lo, strip.hi);
            root.Find(strip.n).GetComponent<Image>().color = PitAmber;
        }

        var face = new GameObject("Face", typeof(RectTransform), typeof(Image), typeof(Button));
        face.transform.SetParent(root, false);
        var frt = (RectTransform)face.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(b, b);
        frt.offsetMax = new Vector2(-b, -b);
        var faceImg = face.GetComponent<Image>();
        faceImg.color = ButtonPlate;
        var button = face.GetComponent<Button>();
        button.targetGraphic = faceImg;
        button.onClick.AddListener(RequestAutoPit);

        _pitLabel = IronOvalUI.Label(frt, "Label", "PIT", IronOvalUI.Role.HeaderSmall, DriveScoutStyle.Text);
        _pitLabel.characterSpacing = SilkscreenTrack2Px;
        _pitLabel.alignment = TextAlignmentOptions.Center;
        _pitLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var lrt = _pitLabel.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        _pitRoot = rootGo;
        _pitRoot.SetActive(false);
    }

    void RequestAutoPit()
    {
        var pit = playerCar != null ? playerCar.GetComponent<PitStopController>() : null;
        if (pit != null) pit.RequestStop();
    }

    // Shown with the AUTO button, while the AI drives a race with the tank under pitButtonFuelLitres and a pit brain to
    // take the stop. Once booked it reads "IN" and stays until the car is on pit road.
    void UpdatePitButton(bool autoShown)
    {
        if (_pitRoot == null) return;
        bool show = false;
        string label = "PIT";
        if (autoShown && !_driving && playerCar != null && RaceStart.IsRunning && !RaceWeekend.IsPracticeLike)
        {
            // Lanes put the car on Auto on the grid, before GridSpawner has published the player's pit box, and the one
            // try at fitting the pit brain then gave up for the race: no pit brain, so no PIT button however low the
            // tank ran (Daytona, 2026-11-21). Tried again until it sticks; once fitted this returns at once.
            EnsureAutoPitBrain();
            var tank = playerCar.GetComponent<FuelTank>();
            var pit = playerCar.GetComponent<PitStopController>();
            if (tank != null && pit != null && !pit.IsPitting && tank.fuelLitres < pitButtonFuelLitres)
            {
                show = true;
                if (pit.StopRequested) label = "IN";
            }
        }
        if (_pitRoot.activeSelf != show) _pitRoot.SetActive(show);
        if (show && _pitLabel != null && _pitLabel.text != label) _pitLabel.text = label;
    }

    static void Strip(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img = go.GetComponent<Image>();
        img.color = DriveScoutStyle.Good;
        img.raycastTarget = false;
    }

    // Up while the player is sat in their car (or has handed it to the AI) with nothing modal over the race, and
    // the car is theirs to take back — the same moments V answers in.
    void UpdateButton()
    {
        if (_buttonRoot == null) return;
        bool show = !PadInput.OnFoot && !PadInput.ModalOpen &&
                    (PlayerVehicleController.Human != null || !_driving) &&
                    !PracticeDirector.BringingPlayerIn && !RaceEndPitIn.HandedOverAtLine;
        if (_buttonRoot.activeSelf != show) _buttonRoot.SetActive(show);
        UpdatePitButton(show);
        bool hintShown = _handBack.Waiting || _laneActive;
        if (_hint != null)
        {
            if (_hint.activeSelf != hintShown) _hint.SetActive(hintShown);
            // Over the button: what takes the car back, or with the lane controls on, the lane it is in.
            string hintText = _laneActive ? "< " + LaneName.ToUpperInvariant() + " >" : "TAP GAS OR BRAKE";
            if (_hintText != null && _hintText.text != hintText) _hintText.text = hintText;
        }
        if (_buttonShowsDriving == _driving) return;
        _buttonShowsDriving = _driving;

        // AUTO ON while the AI has the car: the green chip. Driving, the chip goes dark and reads OFF.
        bool on = !_driving;
        if (_chipFill != null) _chipFill.color = on ? DriveScoutStyle.Good : ChipOffFill;
        if (_chipLabel != null)
        {
            _chipLabel.text = on ? "ON" : "OFF";
            _chipLabel.color = on ? ChipOnText : DriveScoutStyle.Meta;
            // The chip grows left for the longer word, keeping the design's 8px to the button's right edge.
            if (_chip != null)
                _chip.sizeDelta = new Vector2(Mathf.Max(ChipMinW, Mathf.Ceil(_chipLabel.GetPreferredValues(_chipLabel.text).x) + 4f), ChipH);
        }
    }

    public void Toggle()
    {
        // The session is over and the AI is bringing the player's car home: it is not theirs to take back.
        if (PracticeDirector.BringingPlayerIn || RaceEndPitIn.HandedOverAtLine) return;
        if (_driving) { SetDriving(false); return; }
        // Auto off: the AI drives on until a pedal is tapped. Asked again while it waits: Auto stays on.
        if (_handBack.Waiting) { _handBack.Cancel(); return; }
        ReadPedals(out float throttle, out float brake);
        _handBack.Arm(throttle, brake);
        // With the lane controls on, this is the player leaving them: they would otherwise put the car straight back
        // on Auto the moment it is handed over (it is racing, above laneEngageMph).
        if (LegacyLaneControls.Enabled) _laneOptOut = true;
    }

    // Team switching moved the human into a different chassis: auto/resume must now operate on THAT
    // car, or toggling V would disable the AI-driven old car and leave the new one uncontrolled.
    public void RetargetPlayerCar(GameObject car)
    {
        if (car == null) return;
        StopLanes();
        _autoAfterTow = false;
        playerCar = car;
        _pvc = car.GetComponent<PlayerVehicleController>();
        _spline = car.GetComponent<SplineDriver>();
    }

    // Team switching put the player in a teammate's car the AI is already racing: it is theirs now, on Auto. Nothing
    // is rewired - the team car keeps the brain, input driver and pit brain it was built with - so the AI drives on
    // undisturbed, and V / AUTO hands it over the usual way (Auto off, then a pedal tap).
    public void AdoptOnAuto(GameObject car)
    {
        if (car == null) return;
        RetargetPlayerCar(car);
        _handBack.Cancel();
        _driving = false;
        _inputAddedTo = null;   // not Auto's: the team car's own input driver stays on when the player takes it
        _laneOptOut = false;
    }

    public void SetDriving(bool driving)
    {
        _handBack.Cancel();   // set outright: nothing left to wait for
        if (driving == _driving) return;
        _driving = driving;
        if (_driving) ResumeDriving();
        else EnterAuto();
    }

    void EnterAuto()
    {
        if (_pvc != null && _spline != null)
        {
            // Driven the way the field is: the car's own dynamic model, steered by SplineInputDriver, with
            // AIRacingBehaviour's racecraft on top. A bare kinematic SplineDriver drove its speed profile blind —
            // no follow distance, no idea anything was in front of it — and from the green it ran into the back
            // of whatever was there. The brain is set up as GridSpawner sets up a field car's.
            var spawner = FindFirstObjectByType<GridSpawner>();
            if (spawner != null) _spline.cornerSpeedScale = spawner.aiCornerSpeedScale;
            if (playerCar.GetComponent<SplineInputDriver>() == null) _inputAddedTo = playerCar;
            AIHandover.ToAI(playerCar, spawner != null ? spawner.aiSteerGain : 1.5f, spawner != null ? spawner.aiSpeedGain : 2f);
            EnsureAutoPitBrain();

            // In the train behind the safety car (formation lap, cautions) like every other car, and handed to the
            // racing brain at the green.
            FormationController.TakeOver(_spline, _pvc.GridPosition, dynamicModel: true);
        }
        // The camera stays on the player's car, at driving zoom — the on-foot flow may have left the camera at
        // walking zoom if the player never climbed into the car this session.
        if (suppressBroadcastCamera) return;
        if (_zoom != null) _zoom.SetZoomTarget(_zoom.DrivingZoom);
        if (cameraFollow != null && playerCar != null) cameraFollow.target = playerCar.transform;
    }

    // A damaged car on Auto has to know to come in. The field gets its pit brain from GridSpawner; the player's own
    // car has none (the human calls their stops, PlayerPitService runs them), so Auto fits one the first time it takes
    // the car in a race: damage stops only - tyre strategy and the demo stop stay the player's - in the player's box.
    // A team car switched into keeps the pit brain it was built with. Not in practice or qualifying (the stint
    // controller owns pit road there), not after the chequer (RaceEndPitIn brings the car home), and not without a
    // reserved box to stop in.
    void EnsureAutoPitBrain()
    {
        if (playerCar == null || playerCar.GetComponent<PitStopController>() != null) return;
        if (RaceWeekend.IsPracticeLike || RaceDirector.TookChequer(playerCar.transform)) return;
        if (!PitLane.Configured || PitLane.PlayerBox < 0) return;
        var pit = playerCar.AddComponent<PitStopController>();
        pit.boxOverride = PitLane.PlayerBox;
        pit.forcedPit = false;
        pit.tyreStops = false;
        pit.greenRepairDamage = autoGreenRepairDamage;
    }

    void ResumeDriving()
    {
        StopLanes();
        if (_pvc != null && _spline != null)
        {
            FormationController.HandBack(_spline);
            // Taken back part-way through a stop the AI was making: drop it, rather than leave the car pinned in
            // the box or the crew working on a car that has gone.
            var pit = playerCar.GetComponent<PitStopController>();
            if (pit != null) pit.ReleaseForHuman();
            AIHandover.ToHuman(playerCar);
            // A car with an AI input driver on it is not the human's, enabled or not (PlayerVehicleController.IsHuman),
            // so the one Auto added comes off again. A team car keeps the one it was built with.
            if (_inputAddedTo == playerCar)
            {
                var input = playerCar.GetComponent<SplineInputDriver>();
                if (input != null) Destroy(input);
            }
            _inputAddedTo = null;
        }
        if (cameraFollow != null && playerCar != null) cameraFollow.target = playerCar.transform;
    }
}
