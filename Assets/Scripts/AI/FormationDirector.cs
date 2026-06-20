using UnityEngine;

// Orchestrates the pre-race formation lap:
//   PreGrid   — AI parked in pit boxes, safety car parked at the pit exit (set up here in Awake/Start).
//   Formation — fired when the player climbs into their car (PitLaneStart.PlayerEnteredCar). The safety
//               car laps at cruise pace; the AI field forms a weaving train behind it (FormationController).
//   Green     — fired when the safety car commits to pit-in. AI race; the player is released.
//
// Also enforces the player's hold-station pace cap during the formation lap (toggle with
// enforceHoldStation — off = free-follow, where the player drives unrestricted).
public class FormationDirector : MonoBehaviour
{
    public static FormationDirector Instance { get; private set; }

    [Header("Scene refs")]
    public TrackBuilder track;
    public PitLaneStart pitLaneStart;
    public PlayerVehicleController playerCar;

    [Header("Safety car")]
    public GameObject safetyCarPrefab;
    public VehicleInfo safetyCarVehicle;
    [Tooltip("Optional material for the safety car sprite. Left empty keeps the prefab material.")]
    public Material safetyCarLivery;
    public Color safetyCarTint = Color.white;
    public Color rooflightColor = new Color(1f, 0.55f, 0f, 1f);
    public Vector2 safetyCarScale = new Vector2(1f, 1f);
    public int safetyCarSortingOrder = 6;
    [Tooltip("Distance (m) along the main spline, past the pit-exit node, to start the safety car.")]
    public float safetyCarStartOffset = 4f;

    [Header("Pace")]
    [Tooltip("Formation cruise pace (mph). Shared with the AI FormationControllers via Instance.")]
    public float cruiseMph = 60f;

    [Header("Debug")]
    [Tooltip("If > 0, auto-start the formation lap this many seconds after load, without the on-foot enter step (for testing). 0 = off.")]
    public float debugAutoStartAfterSeconds = 0f;
    float _debugTimer;

    [Header("Player")]
    [Tooltip("ON: cap the player to pace and stop them overtaking under caution (default). OFF: free-follow, player drives unrestricted.")]
    public bool enforceHoldStation = true;
    [Tooltip("How far ahead (m) to look for the car the player should hold station behind.")]
    public float holdRange = 60f;
    [Tooltip("Gap (m) the player is nudged toward holding behind the car ahead.")]
    public float targetFollowGap = 10f;
    [Tooltip("Inside this gap (m) the player is forced to back off so they can't overtake.")]
    public float hardGap = 14f;
    [Tooltip("Speed factor applied to the car-ahead's speed when inside hardGap (forces a gap to open).")]
    [Range(0.3f, 1f)] public float holdSpeedFactor = 0.85f;
    [Tooltip("Extra speed (m/s) the player may use over the car ahead to close a gap before settling.")]
    public float closeUpAllowanceMps = 3f;

    SafetyCar _safetyCar;
    float _mainLength;
    float _greenMsgTimer;

    void Awake()
    {
        Instance = this;
        RaceStart.Current = RaceStart.Phase.PreGrid;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pitLaneStart != null) pitLaneStart.PlayerEnteredCar -= BeginFormation;
    }

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        if (track != null)
        {
            var main = track.SampleCenterline();
            _mainLength = main.Count > 0 ? main[main.Count - 1].distance : 0f;
        }

        if (pitLaneStart != null) pitLaneStart.PlayerEnteredCar += BeginFormation;
        else Debug.LogWarning("FormationDirector: no PitLaneStart wired — the formation lap will never start.");

        SpawnSafetyCar();
    }

    void SpawnSafetyCar()
    {
        if (safetyCarPrefab == null || track == null || track.track == null)
        {
            Debug.LogWarning("FormationDirector: missing safetyCarPrefab / track — no safety car spawned.");
            return;
        }

        var go = Instantiate(safetyCarPrefab);
        go.name = "SafetyCar";
        go.transform.localScale = new Vector3(safetyCarScale.x, safetyCarScale.y, 1f);

        // Strip anything that would try to move/control it — it rides a kinematic SplineDriver only.
        DisableIfPresent<MonoBehaviour>(go, "PlayerVehicleController");
        DisableIfPresent<MonoBehaviour>(go, "SplineInputDriver");
        DisableIfPresent<MonoBehaviour>(go, "AIRacingBehaviour");
        DisableIfPresent<MonoBehaviour>(go, "AIDriverBinding");
        DisableIfPresent<MonoBehaviour>(go, "FormationController");
        DisableIfPresent<MonoBehaviour>(go, "VehicleLogic");
        DisableIfPresent<MonoBehaviour>(go, "MovementOnFoot");

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            if (safetyCarLivery != null) sr.sharedMaterial = safetyCarLivery;
            sr.color = safetyCarTint;
            sr.sortingOrder = safetyCarSortingOrder;
        }

        var spline = go.GetComponent<SplineDriver>();
        if (spline == null) spline = go.AddComponent<SplineDriver>();
        spline.track = track;
        spline.vehicleInfo = safetyCarVehicle;
        spline.loop = true;
        spline.lineFactor = 0f;
        spline.spriteFacesUp = false;
        spline.angleOffsetDeg = 180f;
        spline.externalMotionController = false; // kinematic: SplineDriver writes the transform itself
        spline.aiMaxSpeedMph = cruiseMph;
        spline.startDistance = track.track.pitExitDistance + safetyCarStartOffset;

        _safetyCar = go.GetComponent<SafetyCar>();
        if (_safetyCar == null) _safetyCar = go.AddComponent<SafetyCar>();
        _safetyCar.cruiseMph = cruiseMph;
        _safetyCar.rooflightColor = rooflightColor;
        _safetyCar.OnPitEntry += GoGreen;
    }

    void BeginFormation()
    {
        if (RaceStart.Current != RaceStart.Phase.PreGrid) return;
        RaceStart.Current = RaceStart.Phase.Formation;
    }

    void GoGreen()
    {
        RaceStart.Current = RaceStart.Phase.Green;
        if (playerCar != null) playerCar.speedGovernorMps = Mathf.Infinity;
        _greenMsgTimer = 3.5f;
    }

    void FixedUpdate()
    {
        if (RaceStart.Current != RaceStart.Phase.Formation || playerCar == null) return;

        if (!enforceHoldStation)
        {
            playerCar.speedGovernorMps = Mathf.Infinity; // free-follow: never capped
            return;
        }

        playerCar.speedGovernorMps = ComputePlayerCap();
    }

    float ComputePlayerCap()
    {
        float cruiseMps = cruiseMph / 2.237f;
        if (track == null) return cruiseMps;

        float pd = track.NearestCenterlineDistance(playerCar.transform.position);

        SplineDriver lead = null;
        float bestGap = float.MaxValue;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null) continue;
            float tl = d.TrackLength;
            if (tl <= 0f) continue;
            float g = d.DistanceOnTrack - pd;
            g = ((g % tl) + tl) % tl;            // forward distance from the player to this car
            if (g > 0.5f && g < holdRange && g < bestGap) { bestGap = g; lead = d; }
        }

        float cap = cruiseMps;
        if (lead != null)
        {
            float leadMps = lead.CurrentMph / 2.237f;
            if (bestGap > targetFollowGap) cap = leadMps + closeUpAllowanceMps; // close the gap, may nudge over cruise
            else cap = Mathf.Min(cruiseMps, leadMps);                            // hold station at pace
            if (bestGap < hardGap) cap = leadMps * holdSpeedFactor;              // too close — back off, no overtaking
        }

        // Never exceed cruise by more than the close-up allowance; never fully stop.
        cap = Mathf.Clamp(cap, 1.5f, cruiseMps + closeUpAllowanceMps);
        return cap;
    }

    void Update()
    {
        if (debugAutoStartAfterSeconds > 0f && RaceStart.Current == RaceStart.Phase.PreGrid)
        {
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= debugAutoStartAfterSeconds) BeginFormation();
        }
        if (_greenMsgTimer > 0f) _greenMsgTimer -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (_greenMsgTimer <= 0f) return;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 64,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = new Color(0.2f, 1f, 0.2f, Mathf.Clamp01(_greenMsgTimer));
        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 90f), "GREEN — GO!", style);
    }

    static void DisableIfPresent<T>(GameObject go, string typeName) where T : Behaviour
    {
        foreach (var c in go.GetComponentsInChildren<T>(true))
            if (c.GetType().Name == typeName) c.enabled = false;
    }
}
