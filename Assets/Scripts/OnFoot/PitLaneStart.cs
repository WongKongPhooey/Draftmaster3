using UnityEngine;
using UnityEngine.InputSystem;

// Scene-start flow: spawn the on-foot player mid pit lane next to a parked car.
// Walk up to the car, press E / gamepad south to climb in and drive.
// Drop on an empty GameObject; wire track, onFootPrefab, and car in the inspector.
public class PitLaneStart : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("TrackBuilder that owns the pit lane spline.")]
    public TrackBuilder track;
    [Tooltip("On-foot player prefab (TaylorEmerson). Legacy MovementOnFoot/PlayerInput get disabled on spawn.")]
    public GameObject onFootPrefab;
    [Tooltip("The drivable car parked in the pit lane. Its PlayerVehicleController stays disabled until entered.")]
    public PlayerVehicleController car;
    [Tooltip("Input actions asset (PlayerControl). Passed to the spawned OnFootController so it reads the OnFoot/Movement action.")]
    public InputActionAsset controls;

    [Header("Placement")]
    [Tooltip("Where along the pit lane to spawn, as a fraction of its length (0.5 = middle).")]
    [Range(0f, 1f)] public float pitFraction = 0.5f;
    [Tooltip("How far ahead of the player (along the pit lane) the car is parked, metres.")]
    public float carAheadMetres = 5f;
    [Tooltip("Lateral offset (m) off the pit centerline for both player and parked car. Negative = away from the pit wall — keeps them clear of AI cars driving the pit spline at race start.")]
    public float lateralOffsetMetres = -3f;
    [Tooltip("If set, spawn the on-foot player at the PlayerSpawnPoint GameObject with this exact name (deterministic) instead of a weighted-random pick among all markers. Empty = random. Defaults to the motorhome/RV start so the scene opens with the player stood inside the RV.")]
    public string forcedSpawnName = "SpawnPoint_RV";
    [Tooltip("When the player spawns at the RV marker (forcedSpawnName), give the RV a masked interior: the rest of the scene goes black and an interior room shows until the player walks back out the doorway. See RVInterior.")]
    public bool rvInterior = true;

    [Header("Entering")]
    [Tooltip("Max distance from car centre to allow climbing in.")]
    public float enterRange = 2.5f;
    [Tooltip("World height (m) of the keycap prompt floating over the car. Bigger than the NPC one — it sits above a 5m car, not a person.")]
    public float enterPromptIconHeight = 0.9f;

    // Fired the moment the player climbs into the car. FormationDirector subscribes to this to start
    // the safety-car formation lap.
    public event System.Action PlayerEnteredCar;

    [Header("Cast")]
    [Tooltip("The pit greeter, race engineer and crew chief are PlacedNPC markers now — place, edit and gate " +
             "them in the NPC Director (Draftmaster > NPCs > Director). With this on, a scene that has no " +
             "markers for those three roles gets the stock set built at runtime, so a track that was never " +
             "dressed still opens with its cast. Run 'Install Default Pit Cast' to turn them into real, " +
             "editable scene objects; the runtime install then leaves them alone.")]
    public bool installDefaultCast = true;
    [Tooltip("Open the tyre / fuel / balance panel after the chief's briefing. Off = climb in and drive.")]
    public bool showSetupPanel = true;

    [Header("Control Hints")]
    [Tooltip("Teach the controls as the player uses them (run, get in, pit limiter). Each hint shows once per save.")]
    public bool showControlHints = true;
    [Tooltip("Distance from the car (m) at which the 'get in' hint appears. Well outside enterRange so it reads as a heads-up, not a prompt.")]
    public float enterHintRange = 14f;
    [Tooltip("How far the player must walk (m) before the run hint appears.")]
    public float runHintAfterMetres = 3f;

    [Header("Atmosphere")]
    [Tooltip("Looping crowd/paddock bed started when the scene opens. Ducks while the player is inside the RV. Empty = silence.")]
    public AudioClip ambienceClip;
    [Range(0f, 1f)] public float ambienceVolume = 0.3f;

    [Header("Pit Limiter")]
    [Tooltip("Fit a pit speed limiter to the player's car when they get in. It auto-engages in the pit lane and releases at the pit exit line.")]
    public bool fitPitLimiter = true;

    [Header("Camera")]
    [Tooltip("Orthographic size while walking.")]
    public float onFootOrthoSize = 3.5f;
    [Tooltip("Orthographic size while driving.")]
    public float drivingOrthoSize = 20f;
    public float orthoLerpSpeed = 3f;

    GameObject _player;
    SpawnIntroUI _intro;
    CameraFollow _camFollow;
    Camera _cam;
    float _orthoTarget;
    bool _entered;
    bool _interactHeldPrev;
    GameObject _prompt;
    Sprite _carIcon;

    // Walk to the car → chief's briefing → setup panel → green light on the controls. Each step hands to the
    // next; the car's controller stays disabled until the very end so the driver can't roll away mid-briefing.
    enum EntryPhase { Walking, Briefing, Setup, Driving }
    EntryPhase _phase = EntryPhase.Walking;
    NPCInteractable _chief;
    PlacedNPC _chiefNpc;
    Vector3 _hintOrigin;
    bool _hintOriginSet, _hintedRun, _hintedEnter;
    // Set while an opening cutscene is armed/playing: control hints stay off until it's done, so the
    // run prompt lands as the player is handed control rather than under the engineer's dialogue.
    bool _hintsHeldForCutscene;
    System.Collections.Generic.List<TrackBuilder.Sample> _pitSamples;
    bool _usedPit;

    // Where the player's parked car sits along the pit lane (metres) and whether the pit lane was used.
    // GridSpawner reads these to reserve the player's pit box so it doesn't spawn an AI on top of the car.
    public float PlayerPitDistance { get; private set; }
    public bool PlayerOnPit { get; private set; }

    // Camera-zoom arbiter. This component owns the ortho lerp for the whole scene, but other systems
    // retarget the camera (broadcast TV cuts, crew chief's pit-wall avatar) and need the zoom to follow:
    // without this the camera stays at whatever level the last on-foot/enter-car flow left it.
    public float DrivingZoom => drivingOrthoSize;
    public float OnFootZoom => onFootOrthoSize;
    public void SetZoomTarget(float orthoSize) => _orthoTarget = orthoSize;

    void Start()
    {
        // Multiplayer skips the on-foot pit-entry flow: networked cars spawn straight onto the grid
        // (see NetworkedCarBindings). Hide the single-player scene car so it doesn't double up with them.
        if (GameSession.IsMultiplayer)
        {
            if (car != null) car.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        if (track == null || onFootPrefab == null || car == null)
        {
            Debug.LogError("PitLaneStart: missing refs (track / onFootPrefab / car).");
            enabled = false;
            return;
        }

        // Pit lane mid-point in world space. Falls back to the main spline if no pit lane authored.
        var samples = track.SamplePitCenterline();
        bool usedPit = samples.Count >= 2;
        if (!usedPit) samples = track.SampleCenterline();
        if (samples.Count < 2)
        {
            Debug.LogError("PitLaneStart: track has no usable centerline.");
            enabled = false;
            return;
        }
        if (!usedPit) Debug.LogWarning("PitLaneStart: no pit lane on track, using main spline.");

        float total = samples[samples.Count - 1].distance;
        float carDistance = Mathf.Min(total, total * pitFraction + carAheadMetres);
        var mid = track.SamplePitAt(total * pitFraction, samples);
        var carSample = track.SamplePitAt(carDistance, samples);

        // Expose the parked car's pit position so GridSpawner can keep its box clear.
        PlayerPitDistance = carDistance;
        PlayerOnPit = usedPit;

        Vector2 midOff = mid.position + mid.normal * lateralOffsetMetres;
        Vector2 carOff = carSample.position + carSample.normal * lateralOffsetMetres;
        Vector3 playerPos = track.transform.TransformPoint(new Vector3(midOff.x, midOff.y, 0f));
        Vector3 carPos = track.transform.TransformPoint(new Vector3(carOff.x, carOff.y, 0f));

        // Editor-placed spawn markers override the procedural pit-lane spawn for the PLAYER only —
        // the car stays parked at its pit box, so the walk to it becomes part of the scene open.
        // forcedSpawnName pins the start to a named marker (the RV) when present; else weighted-random.
        var marker = PlayerSpawnPoint.Pick(forcedSpawnName);
        if (marker != null)
            playerPos = new Vector3(marker.transform.position.x, marker.transform.position.y, playerPos.z);

        // If a walkable boundary is authored, never spawn the player outside it.
        if (PaddockBoundary.AnyActive)
        {
            Vector2 c = PaddockBoundary.Constrain(playerPos);
            playerPos = new Vector3(c.x, c.y, playerPos.z);
        }
        float carHeadingDeg = Mathf.Atan2(carSample.tangent.y, carSample.tangent.x) * Mathf.Rad2Deg;

        // Park the car. PlayerVehicleController reads heading from transform on its Start (first enable),
        // with convention heading = euler.z + (spriteFacesUp ? 90 : 0) - angleOffsetDeg.
        var carT = car.transform;
        carT.position = new Vector3(carPos.x, carPos.y, carT.position.z);
        float zRot = carHeadingDeg - ((car.spriteFacesUp ? 90f : 0f) - car.angleOffsetDeg);
        carT.rotation = Quaternion.Euler(0f, 0f, zRot);

        // Make sure nothing drives the car until the player climbs in.
        car.enabled = false;
        var spline = car.GetComponent<SplineDriver>();
        if (spline != null) spline.enabled = false;

        SpawnPlayer(playerPos);
        AmbienceLoop.Play(ambienceClip, ambienceVolume);

        _pitSamples = samples;
        _usedPit = usedPit;

        // Camera: follow the walker, zoomed in.
        _cam = Camera.main;
        if (_cam != null)
        {
            _camFollow = _cam.GetComponent<CameraFollow>();
            if (_camFollow == null) _camFollow = _cam.gameObject.AddComponent<CameraFollow>();
            _camFollow.target = _player.transform;
            _cam.orthographicSize = onFootOrthoSize;
        }
        _orthoTarget = onFootOrthoSize;

        // "Scene within a scene": if the on-foot start actually landed on the RV marker, wrap the spawn in a
        // masked interior. Standing inside blacks out the rest of the world and shows an interior room; walking
        // out through the doorway (the edge facing the parked car) reveals the scene again. See RVInterior.
        RVExterior rvExterior = null;
        RVInterior rvRoom = null;
        if (rvInterior && marker != null && marker.gameObject.name == forcedSpawnName)
        {
            // Prefer the hand-editable prefab (built via Draftmaster > RV Interior > Build Prefab, then
            // edited in Prefab Mode); fall back to the fully procedural room when it doesn't exist.
            var prefab = Resources.Load<GameObject>("OnFoot/RVInterior");
            RVInterior rv;
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = "RVInterior";
                rv = go.GetComponent<RVInterior>();
                if (rv == null) rv = go.AddComponent<RVInterior>();
            }
            else
            {
                rv = new GameObject("RVInterior").AddComponent<RVInterior>();
            }
            // A marker parented under a placed RV exterior orients the interior's doorway to the RV's
            // authored door direction and lets RVInterior swap the shell's colliders off while inside;
            // a bare marker falls back to pointing the door at the parked car.
            var exterior = marker.GetComponentInParent<RVExterior>();
            rv.Initialize(_player.transform.position, _player.transform, car.transform, exterior);

            rvExterior = exterior;
            rvRoom = rv;
        }

        // Everyone on foot in this scene — the greeter, the engineer's opening beat, the crew chief, and any
        // NPC hand-placed in this track's package — is a PlacedNPC marker. They're built here, in one pass,
        // once the geometry their anchors read from (pit lane, parked car, RV door) is all in place.
        BuildCast(playerPos, total * pitFraction, rvExterior, rvRoom);

        // Spawn-in presentation: "<Track> - <spawn label>" title card, plus an objective marker
        // pointing at the parked car (edge-clamped arrow + distance + paint-scheme icon when far).
        // Geometry first, then the catalogue. The scene name is last and no longer useful on its own — every
        // track runs in RaceScene, so falling back to it would title every round "Race Scene".
        string trackTitle = (track.track != null && !string.IsNullOrEmpty(track.track.trackName))
            ? track.track.trackName
            : TrackCatalog.DisplayName(AppearanceConditions.CurrentTrackId);
        string spawnLabel = (marker != null && !string.IsNullOrEmpty(marker.label)) ? marker.label : "Pit Lane";
        // Under the name: where the player is in the weekend. A race weekend is a schedule, and the first
        // thing a driver wants to know on waking up in the motorhome is what day it is and how long they
        // have got.
        string when = Draftmaster.Weekend.WeekendSlots.Day(Draftmaster.Weekend.WeekendLedger.CurrentSlot) + " - " +
                      Draftmaster.Weekend.WeekendSlots.ClockAmPm(Draftmaster.Weekend.WeekendLedger.ClockMinute);
        _intro = SpawnIntroUI.Create($"{trackTitle} - {spawnLabel}", _player.transform, when);
        var carSprite = car.GetComponentInChildren<SpriteRenderer>();
        _carIcon = carSprite != null ? carSprite.sprite : null;
        SyncCarMarker();
    }

    void SpawnPlayer(Vector3 pos)
    {
        _player = Instantiate(onFootPrefab, pos, Quaternion.identity);
        _player.name = "OnFootPlayer";

        // Legacy components depend on RaceManager/InputManager which aren't active in this scene.
        var legacy = _player.GetComponent<MovementOnFoot>();
        if (legacy != null) legacy.enabled = false;
        var pi = _player.GetComponent<PlayerInput>();
        if (pi != null) pi.enabled = false;

        var ofc = _player.GetComponent<OnFootController>();
        if (ofc == null) ofc = _player.AddComponent<OnFootController>();
        ofc.controlsAsset = controls; // OnFootController builds the action lazily, after this assignment

        // Scene uses the 3D URP renderer — Sprite-Lit-Default gets no Light2D and renders black. Swap to unlit.
        var sr = _player.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) sr.sharedMaterial = new Material(sh);
        }

        var rb = _player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    // Stand up everyone on foot. Every one of them is a PlacedNPC marker — placed in this track's package
    // for somebody who belongs to this track, or in the shared scene for the every-track cast — so who
    // appears, where they stand and what they say is all authored rather than coded.
    //
    // The context handed over is what the geometry anchors resolve against: the pit lane and where along it
    // the player spawned, the parked car, and the RV they may have woken up in.
    void BuildCast(Vector3 playerPos, float playerPitDistance, RVExterior exterior, RVInterior interior)
    {
        // A scene nobody has dressed yet still opens with its greeter and chief. They go under the scene's
        // "NPCs" root, not under this object — this one only marks where the pit lane starts.
        if (installDefaultCast) PlacedNPCDefaults.EnsureCast();

        var ctx = new PlacedNPC.BuildContext
        {
            prefab = onFootPrefab,
            player = _player.transform,
            car = car.transform,
            track = track,
            pitSamples = _pitSamples,
            usedPit = _usedPit,
            playerPitDistance = playerPitDistance,
            playerSpawnPos = playerPos,
            rv = exterior,
            rvInterior = interior,
            groundZ = exterior != null ? exterior.transform.position.z : playerPos.z,
        };

        // Nothing gets taught while a cutscene owns the screen; the beat's own Finished callback releases it.
        PlacedNPC.CutsceneFinished += OnPlacedCutsceneFinished;
        PlacedNPC.BuildAll(ctx);
        _hintsHeldForCutscene = PlacedNPC.AnyCutsceneArmed;

        // The chief's briefing is driven from EnterCar rather than by walking up to him.
        var chief = PlacedNPC.Find(PlacedNPC.Role.CrewChief);
        _chiefNpc = chief;
        _chief = chief != null ? chief.Interactable : null;
    }

    void OnDestroy()
    {
        PlacedNPC.CutsceneFinished -= OnPlacedCutsceneFinished;
    }

    // An opening beat has finished: put its objective on screen, fly the car marker out from the centre so
    // the eye follows it to the edge, and only now teach the run control (the player has just got movement
    // back, and the hint would otherwise have landed under the dialogue).
    void OnPlacedCutsceneFinished(PlacedNPC npc)
    {
        _hintsHeldForCutscene = false;
        _hintOriginSet = false; // re-base the "walked far enough" test from where the talk left them

        if (_intro != null)
        {
            if (npc != null && !string.IsNullOrEmpty(npc.objectiveOnFinish)) _intro.ShowTitle(npc.objectiveOnFinish);
            _intro.PulseMarker(car.transform);
        }
        if (showControlHints && !_hintedRun)
        {
            ControlHints.Show("run", "LEFT SHIFT", "LB", "Hold to run");
            _hintedRun = true;
        }
    }


    void Update()
    {
        if (_cam != null && _cam.orthographic)
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _orthoTarget, 1f - Mathf.Exp(-orthoLerpSpeed * Time.deltaTime));

        if (_player == null) return;

        if (_phase == EntryPhase.Briefing) { StepBriefing(); return; }
        if (_entered) return;

        SyncCarMarker();

        if (showControlHints) StepWalkHints();

        bool inRange = Vector2.Distance(_player.transform.position, car.transform.position) <= enterRange;

        // Outside a session the car is parked scenery: the paddock is walkable for all three days, but the
        // hour in the car is something the sheet gives you.
        //
        // The exception is the sheet's own sessions. An obligation is a place you go to, and a practice
        // session is no different — stood at the car with one booked, E takes it. The director reloads the
        // scene with the session live (the field only comes out at load) and the spawn is a few steps away.
        if (!RaceWeekend.SessionLive)
        {
            var due = BookedSession;
            ShowPrompt(inRange && due != null);
            if (due == null) ControlHints.Hide("entercar");
            if (inRange && due != null && InteractPressed()) WeekendDirector.Begin(due);
            return;
        }

        ShowPrompt(inRange);

        if (inRange && InteractPressed()) EnterCar();
    }

    // Teach the two things the walk needs, as the player gets to them: sprint once they're actually walking,
    // and "get in" while the car is still a way off. Both are once-per-save (ControlHints owns that memory).
    void StepWalkHints()
    {
        if (_hintsHeldForCutscene) return; // released by the cutscene's Finished callback
        if (!_hintOriginSet) { _hintOrigin = _player.transform.position; _hintOriginSet = true; }

        if (!_hintedRun && Vector2.Distance(_player.transform.position, _hintOrigin) > runHintAfterMetres)
        {
            ControlHints.Show("run", "LEFT SHIFT", "LB", "Hold to run");
            _hintedRun = true;
        }

        if (!_hintedEnter && (RaceWeekend.SessionLive || BookedSession != null)
            && Vector2.Distance(_player.transform.position, car.transform.position) < enterHintRange)
        {
            ControlHints.Show("entercar", "E", "E", "Get in the car");
            _hintedEnter = true;
        }
    }

    // The player's own on-track session, if that is what they are currently due at. Null the rest of the
    // time, which is most of the weekend.
    static Draftmaster.Weekend.WeekendActivity BookedSession
    {
        get
        {
            var due = WeekendAppointment.Pending;
            return due != null && due.IsOnTrack ? due : null;
        }
    }

    // The car is an objective only while a session is live; the rest of the weekend, pointing the player at
    // it would be pointing them at something they cannot do.
    void SyncCarMarker()
    {
        if (_intro == null || car == null) return;

        // When a session is what the player is due at, the weekend's own marker is already on the car — at a
        // higher priority and with the booking's name on it. Leave that one alone rather than the two of us
        // rewriting the same entry every frame.
        if (BookedSession != null) return;

        if (RaceWeekend.SessionLive) _intro.AddMarker(car.transform, _carIcon, enterRange * 2f, "Your car");
        else _intro.RemoveMarker(car.transform);
    }

    void EnterCar()
    {
        _entered = true;
        ShowPrompt(false);
        ControlHints.Hide("entercar");
        _player.SetActive(false);
        if (_intro != null) _intro.RemoveMarker(car.transform); // objective complete

        // Camera moves to the car straight away, but the CONTROLLER stays off: the chief still has to be
        // heard out and the setup made before the car is live.
        if (_camFollow != null) _camFollow.target = car.transform;

        if (_chief != null && _chief.lines != null && _chief.lines.Length > 0)
        {
            // Hold the walking zoom through the briefing — the chief is stood beside the car and both
            // bubbles are on-foot scale. The pull-back to driving distance waits for his last line.
            _phase = EntryPhase.Briefing;
            if (_chiefNpc != null) _chiefNpc.MarkPlayed(); // the briefing has actually started, not just been staged
            _chief.SetInteractor(car.transform); // "#player" lines bubble over the car, where the driver now is
            _chief.Interact();                   // opens the first line
            _interactHeldPrev = true;            // swallow the same press that got us in the car
            if (showControlHints) ControlHints.Show("advance", "E", "E", "Continue");
            return;
        }

        OpenSetupOrDrive();
    }

    // Advance the chief's lines on interact; when he runs out, the setup panel takes over.
    void StepBriefing()
    {
        if (_chief == null) { OpenSetupOrDrive(); return; }
        if (InteractPressed() && !_chief.Interact()) OpenSetupOrDrive();
    }

    void OpenSetupOrDrive()
    {
        ControlHints.Hide("advance");
        _orthoTarget = drivingOrthoSize; // the talking is over — now pull back to driving distance
        if (!showSetupPanel) { StartDriving(null); return; }
        _phase = EntryPhase.Setup;
        CarSetupPanelUI.Open(CarSetup.Load(), StartDriving);
    }

    // Everything is settled — apply the setup, hand the car over, and tell the rest of the scene we're driving.
    void StartDriving(CarSetup setup)
    {
        setup?.ApplyTo(car.gameObject);

        _phase = EntryPhase.Driving;
        car.enabled = true; // PlayerVehicleController.Start captures parked heading on first enable
        if (fitPitLimiter) EnsurePitLimiter();

        if (showControlHints)
        {
            ControlHints.Show("drive", "W / S", "RT / LT", "Throttle and brake", 6f);
            if (fitPitLimiter) ControlHints.Show("limiter", "L", "Y", "Pit limiter — holds you to the pit speed limit", 7f);
        }

        PlayerEnteredCar?.Invoke();
    }

    void EnsurePitLimiter()
    {
        var limiter = car.GetComponent<PitLimiter>();
        if (limiter == null) limiter = car.gameObject.AddComponent<PitLimiter>();
        limiter.car = car;
        limiter.track = track;
    }

    bool InteractPressed()
    {
        bool held = false;
        var gp = Gamepad.current;
        if (gp != null) held |= gp.buttonSouth.isPressed;
        var kb = Keyboard.current;
        if (kb != null) held |= kb.eKey.isPressed;

        bool pressed = held && !_interactHeldPrev;
        _interactHeldPrev = held;
        return pressed;
    }

    void ShowPrompt(bool show)
    {
        if (show)
        {
            if (_prompt == null)
            {
                _prompt = new GameObject("EnterPrompt");
                _prompt.transform.SetParent(car.transform, false);
                _prompt.transform.localPosition = new Vector3(0f, 2.2f, 0f);

                // Same Kenney keycap the NPC prompts use, so "press E" looks identical whether you're
                // walking up to a person or to your own car. Sized larger than the NPC one because it
                // floats over a 5m car rather than a 0.6m figure.
                if (InputPromptIcon.Create(_prompt.transform, "Icon", enterPromptIconHeight, "Vehicles", 50) == null)
                {
                    var tm = _prompt.AddComponent<TextMesh>();
                    tm.text = "E";
                    tm.characterSize = 0.5f;
                    tm.fontSize = 32;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.color = new Color(1f, 1f, 0.4f, 0.95f);
                    var mr = _prompt.GetComponent<MeshRenderer>();
                    mr.sortingLayerName = "Vehicles"; // above the car bodywork
                    mr.sortingOrder = 50;
                }
            }
            _prompt.transform.rotation = Quaternion.identity; // stay upright regardless of car rotation
            _prompt.SetActive(true);
        }
        else if (_prompt != null) _prompt.SetActive(false);
    }
}
