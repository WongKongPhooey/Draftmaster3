using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// UI toggle for Driving vs Broadcast.
// - Driving ON  : the player drives their car normally (PlayerVehicleController).
// - Driving OFF : the car is handed to the AI (kinematic SplineDriver) and the camera cycles through the AI
//                 field TV-style, switching featured car every few seconds. Flip back to resume control.
public class DriveModeController : MonoBehaviour
{
    [Header("Refs (auto-found if empty)")]
    [Tooltip("The player's car. Auto-finds the GameObject named playerCarName.")]
    public GameObject playerCar;
    public string playerCarName = "PlayerCar";
    [Tooltip("CameraFollow to retarget. Auto-found on Camera.main.")]
    public CameraFollow cameraFollow;

    [Header("Broadcast")]
    [Tooltip("Seconds each AI car is featured before cutting to the next.")]
    public float broadcastCycleSeconds = 5f;
    [Tooltip("Seconds a car picked from the leaderboard stays featured before the TV cycle resumes.")]
    public float clickHoldSeconds = 15f;
    [Tooltip("Start the scene in broadcast (spectator) mode rather than driving.")]
    public bool startInBroadcast = false;

    [Header("Input")]
    [Tooltip("Optional keyboard shortcut to toggle Driving.")]
    public Key toggleKey = Key.V;

    [Tooltip("When true, another system (e.g. CrewChiefController) owns the camera, so broadcast mode won't retarget it.")]
    public bool suppressBroadcastCamera = false;

    public bool IsDriving => _driving;
    public GameObject PlayerCar => playerCar;
    // The car the broadcast camera is on right now (null while driving). Leaderboard highlights this row.
    public Transform FeaturedTransform => _driving ? null : (_pinned != null ? _pinned : (_featured != null ? _featured.transform : null));

    // Leaderboard click: cut to this car and hold it for clickHoldSeconds, then resume the TV cycle.
    // Works for any car with a transform (incl. the player's own AI-driven car and client puppets).
    public void FeatureCar(Transform car)
    {
        if (_driving || car == null || suppressBroadcastCamera) return;
        _pinned = car;
        _pinnedTimer = clickHoldSeconds;
        if (cameraFollow != null && !suppressBroadcastCamera) cameraFollow.target = car;
    }

    PlayerVehicleController _pvc;
    SplineDriver _spline;
    PitLaneStart _zoom;      // owns the camera ortho lerp; null on scenes without the pit-start flow
    bool _driving = true;
    float _cycleTimer;
    int _featuredIndex = -1;
    SplineDriver _featured;
    Transform _pinned;       // leaderboard-clicked car; overrides the cycle while its hold timer runs
    float _pinnedTimer;
    readonly List<SplineDriver> _candidates = new();

    Text _label;
    bool _keyPrev;

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
        BuildUI();
        UpdateLabel();

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

        if (!_driving) UpdateBroadcastCamera();
    }

    public void Toggle() => SetDriving(!_driving);

    // Team switching moved the human into a different chassis: broadcast/resume must now operate on THAT
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
        else EnterBroadcast();
        UpdateLabel();
    }

    void EnterBroadcast()
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
        }
        _cycleTimer = 0f;
        _featured = null;
        _pinned = null;
        // Broadcast watches cars, so zoom out to driving level — the on-foot flow may have left the
        // camera at walking zoom if the player never climbed into the car this session.
        if (_zoom != null && !suppressBroadcastCamera) _zoom.SetZoomTarget(_zoom.DrivingZoom);
        PickNextCar();
    }

    void ResumeDriving()
    {
        if (_pvc != null && _spline != null)
        {
            float mph = _spline.CurrentMph;
            float heading = _spline.CommandedHeadingDeg;
            Vector3 pos = playerCar.transform.position;
            _spline.enabled = false;
            _pvc.enabled = true;
            _pvc.SeedPose(pos, heading, mph / 2.237f);
        }
        if (cameraFollow != null && playerCar != null) cameraFollow.target = playerCar.transform;
        _pinned = null;
    }

    void UpdateBroadcastCamera()
    {
        if (suppressBroadcastCamera) return;     // crew chief (or similar) owns the camera
        if (_pinned != null)
        {
            _pinnedTimer -= Time.deltaTime;
            if (_pinnedTimer > 0f)
            {
                if (cameraFollow != null) cameraFollow.target = _pinned;
                return;
            }
            _pinned = null;
            _cycleTimer = 0f;   // hold expired (or car despawned): cut to the next cycle car now
        }
        _cycleTimer -= Time.deltaTime;
        if (_cycleTimer <= 0f || _featured == null) PickNextCar();
        if (_featured != null && cameraFollow != null) cameraFollow.target = _featured.transform;
    }

    // Advance to the next AI car in the field (skipping the player's own spline driver).
    void PickNextCar()
    {
        var drivers = RaceField.Drivers;
        _candidates.Clear();
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || d == _spline || !d.isActiveAndEnabled) continue;
            _candidates.Add(d);
        }
        if (_candidates.Count == 0) { _featured = null; return; }
        _featuredIndex = (_featuredIndex + 1) % _candidates.Count;
        _featured = _candidates[_featuredIndex];
        _cycleTimer = broadcastCycleSeconds;
        if (cameraFollow != null && !suppressBroadcastCamera) cameraFollow.target = _featured.transform;
    }

    void UpdateLabel()
    {
        if (_label != null) _label.text = _driving ? "Driving: ON" : "Broadcast (AI)";
    }

    void BuildUI()
    {
        EnsureEventSystem();

        var canvasGO = new GameObject("DriveModeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var btnGO = new GameObject("DriveToggle", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasGO.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(220f, 48f);

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        var btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(Toggle);

        var txtGO = new GameObject("Label", typeof(RectTransform));
        txtGO.transform.SetParent(btnGO.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        _label = txtGO.AddComponent<Text>();
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.fontSize = 18;
        _label.fontStyle = FontStyle.Bold;
        _label.font = BrandFonts.Body;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}
