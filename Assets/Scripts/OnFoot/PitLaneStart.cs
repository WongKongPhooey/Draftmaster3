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

    // Fired the moment the player climbs into the car. FormationDirector subscribes to this to start
    // the safety-car formation lap.
    public event System.Action PlayerEnteredCar;

    [Header("Pit Greeter NPC")]
    [Tooltip("Spawn a walk-up-and-talk crew member near the player's pit spawn.")]
    public bool spawnGreeter = true;
    [Tooltip("Name shown in the dialogue panel.")]
    public string greeterName = "Pit Crew";
    [TextArea]
    [Tooltip("Conversation lines, one per interact. A line ending with \"#player\" is spoken by the driver (their own bubble); the marker is stripped.")]
    public string[] greeterLines =
    {
        "Morning! Car's prepped and fuelled, ready when you are.",
        "Thanks. Anything I should know? #player",
        "Track's still cold, so take the first lap easy.",
        "Will do. #player",
        "Right then — hop in whenever you're set. Good luck out there!"
    };
    [Tooltip("Lateral offset (m) off the pit centerline for the NPC. More negative = further from the wall.")]
    public float greeterLateral = -5.5f;
    [Tooltip("Metres behind the player's spawn (along the pit) to place the NPC.")]
    public float greeterBehind = 1.5f;

    [Header("RV Door Cutscene")]
    [Tooltip("When spawning at the RV, stand a crew member outside and play a walk-up cutscene the first time the player steps out the door: player freezes, bars slide in, the NPC walks over, both face each other, dialogue opens.")]
    public bool rvDoorCutscene = true;
    [Tooltip("Name shown for the RV-door NPC's dialogue.")]
    public string rvNpcName = "Team Manager";
    [TextArea]
    [Tooltip("Cutscene conversation lines. A line ending with \"#player\" is spoken by the driver.")]
    public string[] rvNpcLines =
    {
        "There you are! Was starting to think you'd sleep through the whole weekend.",
        "The bunk in that thing is better than my bed at home. #player",
        "Well, shake it off — the car's fuelled and sitting in the box.",
        "Any changes from yesterday? #player",
        "Touch more front wing, nothing you'll fight. Go get settled in.",
        "On my way. #player"
    };
    [Tooltip("How far straight out from the RV door the NPC stands (m).")]
    public float rvNpcDoorDistance = 5f;
    [Tooltip("Sideways offset (m) along the RV for the NPC's stand point (+ = toward the cab).")]
    public float rvNpcLateral = 2.5f;
    [Tooltip("How far outside the door the walk-over trigger sits (m). Keep past the interior's exit threshold (roomFront + hysteresis) so the mask has already lifted when it fires.")]
    public float rvTriggerDistance = 2.6f;
    [Tooltip("Radius of the walk-over trigger (m).")]
    public float rvTriggerRadius = 1.5f;

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

        if (spawnGreeter)
        {
            float npcDist = Mathf.Clamp(total * pitFraction - greeterBehind, 0f, total);
            var npcSample = usedPit ? track.SamplePitAt(npcDist, samples) : track.SampleAt(npcDist, samples);
            Vector2 npcOff = npcSample.position + npcSample.normal * greeterLateral;
            Vector3 npcPos = track.transform.TransformPoint(new Vector3(npcOff.x, npcOff.y, 0f));
            SpawnGreeter(npcPos);
        }

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

            // The exit beat: a crew member waiting outside who walks over the first time the player
            // steps out the door. Needs the exterior for the door's position/facing.
            if (rvDoorCutscene && exterior != null)
                BuildRvDoorCutscene(exterior, rv);
        }

        // Spawn-in presentation: "<Track> - <spawn label>" title card, plus an objective marker
        // pointing at the parked car (edge-clamped arrow + distance + paint-scheme icon when far).
        string trackTitle = (track.track != null && !string.IsNullOrEmpty(track.track.trackName))
            ? track.track.trackName
            : SpawnIntroUI.Nicify(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        string spawnLabel = (marker != null && !string.IsNullOrEmpty(marker.label)) ? marker.label : "Pit Lane";
        _intro = SpawnIntroUI.Create($"{trackTitle} - {spawnLabel}", _player.transform);
        var carSprite = car.GetComponentInChildren<SpriteRenderer>();
        _intro.AddMarker(car.transform, carSprite != null ? carSprite.sprite : null, enterRange * 2f);
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

    // Spawn a stationary crew member from the on-foot prefab and make it talkable. Dialogue shows as
    // world-space speech bubbles with a typewriter reveal above the crew member and the driver (SpeechBubble),
    // driven by the player's OnFootController interaction. Lines come from greeterLines (no scene wiring needed).
    void SpawnGreeter(Vector3 pos)
    {
        SpawnTalkableNpc(pos, "PitCrewNPC", greeterName, greeterLines);
    }

    // Stand a crew member outside the RV door and arm a walk-over trigger just past the doorway.
    // Stepping out of the RV onto the trigger plays NPCWalkUpCutscene: the player freezes, the
    // cinematic bars come in, the NPC walks over, and the conversation opens face-to-face.
    void BuildRvDoorCutscene(RVExterior exterior, RVInterior interior)
    {
        Vector2 doorDir = exterior.DoorWorldDirection;
        Vector2 side = new Vector2(-doorDir.y, doorDir.x); // door's left = toward the cab on the default prefab
        Vector3 doorPos = exterior.DoorWorldPosition;
        // Ground-plane z, NOT the player's current z — the player has already been pulled to the
        // interior's -2.5 by this point, which would put the NPC in front of the black mask.
        float z = exterior.transform.position.z;

        Vector3 npcPos = doorPos + (Vector3)(doorDir * rvNpcDoorDistance + side * rvNpcLateral);
        npcPos.z = z;
        var npc = SpawnTalkableNpc(npcPos, "RVDoorNPC", rvNpcName, rvNpcLines);

        // One GameObject hosts both the trigger and the sequence, parked on the trigger spot; the
        // cutscene destroys it when the conversation ends, so the beat can only play once.
        var seq = new GameObject("RVDoorCutscene");
        Vector3 triggerPos = doorPos + (Vector3)(doorDir * rvTriggerDistance);
        triggerPos.z = z;
        seq.transform.position = triggerPos;

        var walkUp = seq.AddComponent<NPCWalkUpCutscene>();
        walkUp.player = _player.GetComponent<OnFootController>();
        walkUp.npc = npc;

        var trigger = seq.AddComponent<CutsceneTrigger>();
        trigger.radius = rvTriggerRadius;
        trigger.target = _player.transform;
        trigger.Gate = () => !interior.IsInside; // never fire while the world is masked
        trigger.Triggered = walkUp.Play;
    }

    // Shared NPC factory: clone the on-foot prefab, strip anything that would control it, freeze the
    // walk cycle at an idle pose, and attach an NPCInteractable carrying the given dialogue.
    NPCInteractable SpawnTalkableNpc(Vector3 pos, string goName, string speaker, string[] npcLines)
    {
        var npc = Instantiate(onFootPrefab, pos, Quaternion.identity);
        npc.name = goName;

        // Strip anything that would drive/control it — it just stands and talks.
        var mv = npc.GetComponent<MovementOnFoot>(); if (mv != null) mv.enabled = false;
        var pi = npc.GetComponent<PlayerInput>(); if (pi != null) pi.enabled = false;
        var ofc = npc.GetComponent<OnFootController>(); if (ofc != null) Destroy(ofc);
        var rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.gravityScale = 0f; rb.bodyType = RigidbodyType2D.Kinematic; }

        // Nothing drives this Animator, so freeze it — otherwise the walk cycle plays in place forever.
        var anim = npc.GetComponent<Animator>();
        if (anim != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float &&
                    (p.name == "Horizontal" || p.name == "Vertical" || p.name == "Speed"))
                    anim.SetFloat(p.name, 0f);
            }
            anim.Update(0f);   // sample the idle pose at zeroed params...
            anim.speed = 0f;   // ...then stop so it can't treadmill
        }

        // Same unlit-shader swap the player gets, so it renders under the 3D URP renderer.
        var sr = npc.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) sr.sharedMaterial = new Material(sh);
        }

        var inter = npc.AddComponent<NPCInteractable>();
        inter.speakerName = speaker;
        if (npcLines != null && npcLines.Length > 0) inter.lines = npcLines;
        return inter;
    }

    void Update()
    {
        if (_cam != null && _cam.orthographic)
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _orthoTarget, 1f - Mathf.Exp(-orthoLerpSpeed * Time.deltaTime));

        if (_entered || _player == null) return;

        bool inRange = Vector2.Distance(_player.transform.position, car.transform.position) <= enterRange;
        ShowPrompt(inRange);

        if (inRange && InteractPressed()) EnterCar();
    }

    void EnterCar()
    {
        _entered = true;
        ShowPrompt(false);
        _player.SetActive(false);
        if (_intro != null) _intro.RemoveMarker(car.transform); // objective complete

        car.enabled = true; // PlayerVehicleController.Start captures parked heading on first enable
        if (_camFollow != null) _camFollow.target = car.transform;
        _orthoTarget = drivingOrthoSize;

        PlayerEnteredCar?.Invoke();
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
            _prompt.transform.rotation = Quaternion.identity; // stay upright regardless of car rotation
            _prompt.SetActive(true);
        }
        else if (_prompt != null) _prompt.SetActive(false);
    }
}
