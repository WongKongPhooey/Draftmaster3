using UnityEngine;
using UnityEngine.InputSystem;
using Draftmaster.Controls;

// Driving vs Auto, the hand-off itself.
// - Driving ON  : the player drives their car normally (PlayerVehicleController).
// - Driving OFF : Auto. The car is handed to the AI (kinematic SplineDriver) and the camera stays on it. Watching
//                 the rest of the field is the crew chief's job (CrewChiefController). Flip back to resume control.
//
// This has no button of its own. Handing the car over is what happens when the player steps out of it, so it
// is driven by whoever they step into — the crew chief's headset in the HUD corner (CrewChiefController), the
// TEAM panel, co-op possession, the touch controls' AUTO button — plus the keyboard shortcut below.
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

    public bool IsDriving => _driving;
    public GameObject PlayerCar => playerCar;

    PlayerVehicleController _pvc;
    SplineDriver _spline;
    PitLaneStart _zoom;      // owns the camera ortho lerp; null on scenes without the pit-start flow
    bool _driving = true;

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
            // Match the player car's sprite/heading + track refs so the AI drives it correctly.
            if (_spline.track == null) _spline.track = _pvc.track;
            if (_spline.vehicleInfo == null) _spline.vehicleInfo = _pvc.vehicleInfo;
            _spline.spriteFacesUp = _pvc.spriteFacesUp;
            _spline.angleOffsetDeg = _pvc.angleOffsetDeg;

            float mph = _pvc.SpeedMph;
            _spline.enabled = true;
            _spline.EngageFromCurrentPose(mph);
            _pvc.enabled = false;
            // Before the green the car has to form up with the field, not drive its race pace into the back of
            // it. No-op once racing.
            FormationController.TakeOver(_spline, _pvc.GridPosition);
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
            float mph = _spline.CurrentMph;
            float heading = _spline.CommandedHeadingDeg;
            Vector3 pos = playerCar.transform.position;
            FormationController.HandBack(_spline);
            _spline.enabled = false;
            _pvc.enabled = true;
            _pvc.SeedPose(pos, heading, mph / 2.237f);
        }
        if (cameraFollow != null && playerCar != null) cameraFollow.target = playerCar.transform;
    }
}
