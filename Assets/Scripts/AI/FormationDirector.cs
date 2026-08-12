using Unity.Netcode;
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
    [Tooltip("Distance (m) along the main spline, past the pit-exit node, to start the safety car. Wants to be " +
             "at least FormationController.paceCarGap: at the old 4m the leader emerged from the pit lane already " +
             "inside its following cushion and had to brake immediately, which is the disturbance the train " +
             "behind then amplified. Pit-in is triggered by proximity to the authored entry node, so a larger " +
             "offset here does NOT push the safety car past its pit entry.")]
    public float safetyCarStartOffset = 28f;

    [Header("Pace")]
    [Tooltip("Formation cruise pace (mph). Shared with the AI FormationControllers via Instance.")]
    public float cruiseMph = 60f;

    [Header("Debug")]
    [Tooltip("If > 0, auto-start the formation lap this many seconds after load, without the on-foot enter step (for testing). 0 = off.")]
    public float debugAutoStartAfterSeconds = 0f;
    float _debugTimer;

    [Header("Player")]
    [Tooltip("ON: hold the player in station during the formation lap — free to drive any speed, but speed-capped so they can't overtake the car directly ahead, with an on-screen prompt to line up (same as multiplayer). OFF: no restriction at all.")]
    public bool enforceHoldStation = true;

    SafetyCar _safetyCar;
    float _greenMsgTimer;

    // True while the leader has slowed in the close-up zone before the line. FormationControllers read this to
    // pack the field into tight two-wide rows for the final run to the green.
    public bool FieldClosingUp => _safetyCar != null && _safetyCar.ClosingUp;

    void Awake()
    {
        // Practice and qualifying have no formation lap or safety car — PracticeDirector owns those
        // sessions. Disabling here (before OnEnable/Start) skips the safety-car spawn and subscriptions.
        if (RaceWeekend.IsPracticeLike)
        {
            enabled = false;
            return;
        }
        Instance = this;
        RaceStart.Current = RaceStart.Phase.PreGrid;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pitLaneStart != null) pitLaneStart.PlayerEnteredCar -= BeginFormation;
    }

    void OnEnable()  => RaceStart.PhaseChanged += OnPhaseChanged;
    void OnDisable() => RaceStart.PhaseChanged -= OnPhaseChanged;

    // SP, or the MP host, owns the phase write. MP clients take each phase from the host's replicated
    // gate (NetworkedCarBindings) — their local cosmetic safety car must never flip the race itself.
    static bool PhaseAuthority =>
        !GameSession.IsMultiplayer ||
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

    // Fires on every peer the instant the race goes green (locally in SP / on the host, or via the host's
    // RPC on a client): release the SP human and flash the banner so all players see GREEN together.
    void OnPhaseChanged(RaceStart.Phase phase)
    {
        if (phase != RaceStart.Phase.Green) return;
        if (playerCar != null) playerCar.speedGovernorMps = Mathf.Infinity;
        _greenMsgTimer = 3.5f;
    }

    void Start()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();

        if (pitLaneStart != null) pitLaneStart.PlayerEnteredCar += BeginFormation;
        else Debug.LogWarning("FormationDirector: no PitLaneStart wired — the formation lap will never start.");

        // Single-player pace lap: the player drives any speed but is held in station (can't pass the car ahead)
        // with the same prompt as multiplayer. PaceLapAssist governs it; we no longer hard-cap to pace below.
        // (Multiplayer attaches its own PaceLapAssist via NetworkedCarBindings for the owning client.)
        if (!GameSession.IsMultiplayer && playerCar != null && enforceHoldStation)
        {
            var assist = playerCar.GetComponent<PaceLapAssist>();
            if (assist == null) assist = playerCar.gameObject.AddComponent<PaceLapAssist>();
            assist.pvc = playerCar;
        }

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
        spline.qualifyingPosition = FormationOrder.SafetyCarGrid; // leads the formation order (below every car)

        _safetyCar = go.GetComponent<SafetyCar>();
        if (_safetyCar == null) _safetyCar = go.AddComponent<SafetyCar>();
        _safetyCar.cruiseMph = cruiseMph;
        _safetyCar.rooflightColor = rooflightColor;
        _safetyCar.OnPitEntry += GoGreen;
    }

    void BeginFormation()
    {
        // Multiplayer enters the formation lap through the host's lobby gate (NetworkedCarBindings), which
        // flips the phase AND replicates it. Flipping it here as well would leave clients behind, so skip.
        if (GameSession.IsMultiplayer) return;
        if (RaceStart.Current != RaceStart.Phase.PreGrid) return;
        RaceStart.Current = RaceStart.Phase.Formation;
    }

    void GoGreen()
    {
        // The MP host flips green here when its safety car pits; NetworkedCarBindings sees the change and
        // replicates it. Clients don't write the phase off their local safety car — they wait for that RPC.
        if (!PhaseAuthority) return;
        RaceStart.Current = RaceStart.Phase.Green;
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
