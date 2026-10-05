using Unity.Netcode;
using UnityEngine;

// Orchestrates the pre-race formation lap:
//   PreGrid   — AI parked in pit boxes, safety car parked at the pit exit (set up here in Awake/Start).
//   Formation — fired when the player, sat in their car (PitLaneStart.PlayerEnteredCar), first touches the
//               accelerator. The safety car laps at cruise pace; the AI field forms a weaving train behind it
//               (FormationController).
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
    [Tooltip("ON: climbing into the car only readies the formation lap — the safety car and the field stay parked " +
             "until the player first presses the accelerator, so nobody drives off while they are still settling in. " +
             "OFF: the formation lap starts the moment the player gets in.")]
    public bool waitForThrottle = true;
    [Tooltip("Throttle (0..1) the player has to press to start the formation lap when waitForThrottle is on.")]
    [Range(0.01f, 1f)] public float throttleToStart = 0.1f;

    SafetyCar _safetyCar;
    float _greenMsgTimer;
    bool _awaitingThrottle; // player is in the car; the formation lap starts on their first press of the accelerator

    // True while the leader has slowed in the close-up zone before the line. FormationControllers read this to
    // pack the field into tight two-wide rows for the final run to the green.
    public bool FieldClosingUp => _safetyCar != null && _safetyCar.ClosingUp;

    void Awake()
    {
        // Practice and qualifying have no formation lap or safety car — PracticeDirector owns those
        // sessions. Disabling here (before OnEnable/Start) skips the safety-car spawn and subscriptions.
        //
        // Nor is there a lap to form up for when the player is not in the car at all: the paddock between
        // sessions, or another championship's session running past while the player is on foot. Without
        // this the safety car is spawned onto an empty circuit and sits at pit exit for three days.
        if (RaceWeekend.IsPracticeLike || !RaceWeekend.SessionLive)
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
        spline.startDistance = SafetyCarStartDistance(track, safetyCarStartOffset);
        spline.qualifyingPosition = FormationOrder.SafetyCarGrid; // leads the formation order (below every car)

        _safetyCar = go.GetComponent<SafetyCar>();
        if (_safetyCar == null) _safetyCar = go.AddComponent<SafetyCar>();
        _safetyCar.cruiseMph = cruiseMph;
        _safetyCar.rooflightColor = rooflightColor;
        _safetyCar.OnPitEntry += GoGreen;
    }

    // Where the safety car starts: `offset` metres past wherever the field actually comes onto the track. That
    // is normally the authored pit-exit node, but the pit lane is authored separately and can run on well past
    // it — at Bristol the lane rejoins 140 m after the node, so the first seven cars filed out AHEAD of the pace
    // car and it drove into the back of them. Start past whichever of the two comes later.
    public static float SafetyCarStartDistance(TrackBuilder track, float offset, float pitExitThreshold = 0.98f)
    {
        float node = track.track.pitExitDistance;
        var main = track.SampleCenterline();
        var pit = track.SamplePitCenterline();
        if (main.Count < 2 || pit.Count < 2) return node + offset;
        float lap = main[main.Count - 1].distance;
        float pitLen = pit[pit.Count - 1].distance;
        if (lap <= 0f || pitLen <= 0f) return node + offset;

        // The same point SplineDriver hands a pit-lane car back to the main spline at (pitExitThreshold).
        var exit = track.SamplePitAt(pitLen * pitExitThreshold, pit);
        float rejoin = track.NearestCenterlineDistance(track.transform.TransformPoint(new Vector3(exit.position.x, exit.position.y, 0f)));
        float past = Mathf.Repeat(rejoin - node + lap * 0.5f, lap) - lap * 0.5f; // + = rejoin is after the node
        float from = past > 0f ? rejoin : node;
        return Mathf.Repeat(from + offset, lap);
    }

    void BeginFormation()
    {
        // Multiplayer enters the formation lap through the host's lobby gate (NetworkedCarBindings), which
        // flips the phase AND replicates it. Flipping it here as well would leave clients behind, so skip.
        if (GameSession.IsMultiplayer) return;
        if (RaceStart.Current != RaceStart.Phase.PreGrid) return;
        if (waitForThrottle && PlayerCar != null) { _awaitingThrottle = true; return; }
        StartFormation();
    }

    void StartFormation()
    {
        _awaitingThrottle = false;
        if (RaceStart.Current != RaceStart.Phase.PreGrid) return;
        RaceStart.Current = RaceStart.Phase.Formation;
    }

    PlayerVehicleController PlayerCar =>
        playerCar != null ? playerCar : (pitLaneStart != null ? pitLaneStart.car : null);

    // Whether the parked field should now roll off on the formation lap. The car's own resolved throttle is the
    // trigger. A car the AI is driving (V / crew-chief headset) goes at once: its controller sits pinned until the
    // formation lap begins, so it would never press the pedal and the race would never start. So does a missing
    // car, for the same reason.
    public static bool ThrottleStartsFormation(PlayerVehicleController car, float threshold)
    {
        if (car == null || !car.enabled || !car.gameObject.activeInHierarchy || car.externalInput) return true;
        return car.ThrottleInput >= threshold;
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
            if (_debugTimer >= debugAutoStartAfterSeconds) StartFormation();
        }
        if (_awaitingThrottle)
        {
            if (RaceStart.Current != RaceStart.Phase.PreGrid) _awaitingThrottle = false;
            else if (ThrottleStartsFormation(PlayerCar, throttleToStart)) StartFormation();
        }
        if (_greenMsgTimer > 0f) _greenMsgTimer -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (_awaitingThrottle) DrawThrottlePrompt();
        if (_greenMsgTimer <= 0f) return;

        // The green flag, in the kit's banner shape: kerb-edged plate across the screen with the call in
        // Silkscreen. Gain green, which is the palette's "you may go" colour.
        float alpha = Mathf.Clamp01(_greenMsgTimer);
        float h = PixelGUI.Px(34f);
        float y = Mathf.Round(Screen.height * 0.18f);
        float kerb = PixelGUI.Px(4f);

        var prevGui = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        PixelGUI.Fill(new Rect(0f, y, Screen.width, h), PixelGUI.PlateDeep);
        PixelGUI.Kerb(new Rect(0f, y - kerb, Screen.width, kerb));
        PixelGUI.Kerb(new Rect(0f, y + h, Screen.width, kerb));

        var style = PixelGUI.Heading;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(PixelGUI.Confirm.r, PixelGUI.Confirm.g, PixelGUI.Confirm.b, alpha);
        GUI.Label(new Rect(0f, y, Screen.width, h), "GREEN · GO!", style);
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;
        GUI.color = prevGui;
    }

    // Nothing moves until the player does, so say so: a slim plate low on screen while the field waits.
    void DrawThrottlePrompt()
    {
        float h = PixelGUI.Px(22f);
        float y = Mathf.Round(Screen.height * 0.72f);
        PixelGUI.Fill(new Rect(0f, y, Screen.width, h), PixelGUI.PlateDeep);

        var style = PixelGUI.Heading;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = PixelGUI.Confirm;
        GUI.Label(new Rect(0f, y, Screen.width, h), "ACCELERATE TO START THE PACE LAP", style);
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;
    }

    static void DisableIfPresent<T>(GameObject go, string typeName) where T : Behaviour
    {
        foreach (var c in go.GetComponentsInChildren<T>(true))
            if (c.GetType().Name == typeName) c.enabled = false;
    }
}
