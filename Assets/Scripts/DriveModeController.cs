using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Draftmaster.Controls;

// Driving vs Auto, the hand-off itself.
// - Driving ON  : the player drives their car normally (PlayerVehicleController).
// - Driving OFF : Auto. The car is handed to the AI (kinematic SplineDriver) and the camera stays on it. Watching
//                 the rest of the field is the crew chief's job (CrewChiefController). Flip back to resume control.
//
// Handing the car over is also what happens when the player steps out of it, so it is driven by whoever they
// step into — the crew chief's headset in the HUD corner (CrewChiefController), the TEAM panel, co-op
// possession — plus the keyboard shortcut below and an AUTO button: the touch controls' top row on a phone,
// and on PC one of its own, stacked above the headset (AUTO with an ON / OFF chip, green ON while the AI has the car).
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

    public bool IsDriving => _driving;
    public GameObject PlayerCar => playerCar;

    PlayerVehicleController _pvc;
    SplineDriver _spline;
    PitLaneStart _zoom;      // owns the camera ortho lerp; null on scenes without the pit-start flow
    bool _driving = true;
    GameObject _inputAddedTo;   // the car Auto put a SplineInputDriver on, to take it off again on the way back

    bool _keyPrev;

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

        UpdateButton();
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

        _buttonRoot = rootGo;
        _buttonShowsDriving = !_driving;   // forces the first paint
        UpdateButton();
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
        SetDriving(!_driving);
    }

    // Team switching moved the human into a different chassis: auto/resume must now operate on THAT
    // car, or toggling V would disable the AI-driven old car and leave the new one uncontrolled.
    public void RetargetPlayerCar(GameObject car)
    {
        if (car == null) return;
        playerCar = car;
        _pvc = car.GetComponent<PlayerVehicleController>();
        _spline = car.GetComponent<SplineDriver>();
    }

    public void SetDriving(bool driving)
    {
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

    void ResumeDriving()
    {
        if (_pvc != null && _spline != null)
        {
            FormationController.HandBack(_spline);
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
