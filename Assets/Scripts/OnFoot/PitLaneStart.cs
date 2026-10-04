using System.Collections;
using Draftmaster.Controls;
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
    [Tooltip("Where in the motorhome the driver starts, in the interior's own frame (metres): X toward " +
             "the bed end (the room's local +X, the RV's tail), Y toward the doorway. The room itself is " +
             "still anchored on the marker, so this moves the player relative to the FURNITURE — dragging " +
             "SpawnPoint_RV cannot, because the room is built around wherever that marker lands. Zero = " +
             "stood on the room's origin.")]
    public Vector2 rvSpawnOffset = new Vector2(2.53f, 0f);

    [Header("Entering")]
    [Tooltip("Max distance from car centre to allow climbing in.")]
    public float enterRange = 2.5f;
    [Tooltip("World height (m) of the keycap prompt floating over the car. Bigger than the NPC one — it sits above a 5m car, not a person.")]
    public float enterPromptIconHeight = 0.9f;

    // Fired the moment the player climbs into the car. FormationDirector subscribes to this to start
    // the safety-car formation lap.
    public event System.Action PlayerEnteredCar;

    [Tooltip("Single race only: how long to wait for GridSpawner to finish parking the field before handing " +
             "the car over anyway. The player starts the race by getting in, so getting in before the field " +
             "is in its boxes starts the formation lap with nothing to form up behind.")]
    public float fieldWaitTimeout = 20f;

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

    [Header("Wake Up")]
    [Tooltip("Open on a black screen with an alarm clock going off, then fade in with the driver getting up. " +
             "Only when the scene opens inside the motorhome, and only on the first morning of a weekend — " +
             "a session reload does not wake you up again. See WakeUpSequence.")]
    public bool wakeUpInRV = true;
    [Tooltip("Alarm clock sound. Empty = a synthesised placeholder (four square-wave beeps on a loop).")]
    public AudioClip alarmClip;
    [Range(0f, 1f)] public float alarmVolume = 0.55f;
    [Tooltip("Seconds the alarm rings in the dark before the picture comes up. Any key hits the clock early. " +
             "The where-and-when card is on screen over the black for all of it.")]
    public float wakeDarkSeconds = 3.4f;
    [Tooltip("Seconds the fade from black takes.")]
    public float wakeFadeInSeconds = 1.8f;
    [Tooltip("Seconds the getting-up beat takes.")]
    public float wakeGetUpSeconds = 0.8f;
    [Tooltip("Lying-down sprite. Empty = the standing sprite laid on its side, which is the placeholder.")]
    public Sprite lyingDownSprite;
    [Tooltip("Animator trigger for the getting-up animation, if the player rig has one. Empty (or missing " +
             "from the rig) = the body rotates upright instead, which is the placeholder.")]
    public string getUpTrigger = "GetUp";
    [Tooltip("World direction the driver is facing once they are on their feet: (0,1) is north, up the " +
             "screen, and out of the motorhome. A screen direction, not the rig's own doorway — an RV " +
             "parked with its door on another side wants this turned to match. Zero = keep the spawn pose.")]
    public Vector2 wakeFacing = Vector2.up;

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
    [Tooltip("Orthographic size while stood inside a room — the motorhome, a team's popup garage. These " +
             "interiors are only a few metres across, so at walking distance the walls sit out past the " +
             "edges of the frame and the room reads as a patch of floor rather than somewhere you are.")]
    public float indoorOrthoSize = 2.5f;
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
    // A cutscene has just handed movement back: teach running now rather than a few steps later — unless the
    // phone's lesson is still to come on this walk (ChiefCheckInBeat.HoldsRunHint), which goes first.
    bool _runHintDue;
    // Set while an opening cutscene is armed/playing: control hints stay off until it's done, so the
    // run prompt lands as the player is handed control rather than under the engineer's dialogue.
    bool _hintsHeldForCutscene;
    System.Collections.Generic.List<TrackBuilder.Sample> _pitSamples;
    bool _usedPit;

    // Where the player's parked car sits along the pit lane (metres) and whether the pit lane was used.
    // GridSpawner reads these to reserve the player's pit box so it doesn't spawn an AI on top of the car.
    public float PlayerPitDistance { get; private set; }
    public bool PlayerOnPit { get; private set; }

    // The pose the car was parked in when the scene opened. This is only where it sits until the field
    // arrives — GridSpawner then snaps it into its reserved pit box — so it is the tow's FALLBACK, for a
    // session that never fitted any boxes. See CurrentBoxPose.
    Vector3 _boxPosition;
    Quaternion _boxRotation;
    float _boxHeadingDeg;
    bool _boxKnown;

    // The player is in the car and has been handed the controls. The stranded-car tow asks this before it
    // offers to bring them in: there is nothing to tow while they are still walking to it.
    public bool IsDriving => _phase == EntryPhase.Driving;

    // Sat in the car but not yet handed it: the chief's briefing, then the setup panel. Both are laid out
    // for a landscape screen, so the phone's swing-camera turn to portrait waits for this to clear
    // (DriveOrientationController) — the screen turns when the drive buttons come up, not when the door shuts.
    //
    // Also asked positively of the scene's walk-up: until it has actually handed the car over (Driving), the
    // answer is "not yet" whatever else looks like a driven car — plus the chief mid-line, which is the
    // briefing whatever the phase says. The flag alone still let the briefing turn the phone upright on
    // device (0.4.1), so the screen is held landscape on any of the three.
    public static bool GettingIn =>
        _gettingIn != null ||
        // A co-op guest never gets into this car — they are put in one of the field's — so not for them.
        (_current != null && _current._phase != EntryPhase.Driving && !Coop.IsGuest) ||
        (_current != null && _current._chief != null && _current._chief.IsTalking);
    static PitLaneStart _gettingIn;
    static PitLaneStart _current;   // the scene's on-foot walk-up, when it has one

    // What the orientation log prints when the screen turns.
    public static string DescribeEntry() =>
        _current == null ? "no walk-up" :
        $"phase={_current._phase} entered={_current._entered} chiefTalking={(_current._chief != null && _current._chief.IsTalking)} flag={_gettingIn != null}";

    void SetPhase(EntryPhase phase)
    {
        _phase = phase;
        if (phase == EntryPhase.Briefing || phase == EntryPhase.Setup) _gettingIn = this;
        else if (_gettingIn == this) _gettingIn = null;
    }

    // The chief has already had his say and the setup is already made. Getting back into the same car
    // after a tow is not a fresh session, so it skips both and hands the controls straight over.
    bool _briefed;
    bool _headedOut;   // SessionHud has had its reset — once a session, not again after a tow

    // Camera-zoom arbiter. This component owns the ortho lerp for the whole scene, but other systems
    // retarget the camera (broadcast TV cuts, crew chief's pit-wall avatar) and need the zoom to follow:
    // without this the camera stays at whatever level the last on-foot/enter-car flow left it.
    public float DrivingZoom => drivingOrthoSize;
    public float OnFootZoom => onFootOrthoSize;
    public float IndoorZoom => indoorOrthoSize;
    public void SetZoomTarget(float orthoSize) => _orthoTarget = orthoSize;

    // Straight to a zoom, no lerp — for cuts made at black. The lerp runs on scaled time, so a result card
    // that pauses the game (WeekendModal) would otherwise hold the old zoom on screen until it is dismissed.
    public void SnapZoom(float orthoSize)
    {
        _orthoTarget = orthoSize;
        if (_cam != null && _cam.orthographic) _cam.orthographicSize = orthoSize;
    }

    // Whether the player was in a room last frame, so walking through a doorway can be spotted as the event
    // it is rather than re-asserted every frame. See StepIndoorZoom.
    bool _indoors;

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

        // The car goes on the box lane — the grey strip the whole field parks on — not on the walker's
        // offset. GridSpawner moves it along that lane into the box it reserves once the field arrives, but
        // if that never happens (a device that fails the spawn, a session with no field) a car parked on the
        // walker's side of pit road sat on the track side of it, outside the paddock, where the player
        // could not reach it and the session could not start.
        float carLateral = usedPit && track.HasPitBoxLane ? track.PitBoxLaneCenterLateral : lateralOffsetMetres;
        Vector2 midOff = mid.position + mid.normal * lateralOffsetMetres;
        Vector2 carOff = carSample.position + carSample.normal * carLateral;
        Vector3 playerPos = track.transform.TransformPoint(new Vector3(midOff.x, midOff.y, 0f));
        Vector3 carPos = track.transform.TransformPoint(new Vector3(carOff.x, carOff.y, 0f));

        // Editor-placed spawn markers override the procedural pit-lane spawn for the PLAYER only —
        // the car stays parked at its pit box, so the walk to it becomes part of the scene open.
        // forcedSpawnName pins the start to a named marker (the RV) when present; else weighted-random.
        var marker = PlayerSpawnPoint.Pick(forcedSpawnName);
        if (marker != null)
            playerPos = new Vector3(marker.transform.position.x, marker.transform.position.y, playerPos.z);

        // Lights out before anything is drawn. The demo opens on a black screen with an alarm clock going
        // off, so the first frame of the paddock must not be visible underneath it — the decision is made
        // here, at the top of the scene open, and the beat itself plays once there is a player to wake up.
        bool waking = GameSession.OnFootAllowed && ShouldWakeUp(marker);
        if (waking) ScreenFade.HoldBlack();

        // Waking up in the motorhome: the rig is walkable wherever it was parked, so the clamp below leaves
        // the spawn on the marker instead of dragging it (and the room built round it) to the old paddock.
        if (rvInterior && marker != null && marker.gameObject.name == forcedSpawnName)
        {
            var shell = marker.GetComponentInParent<RVExterior>();
            if (shell != null) shell.InstallWalkablePocket();
        }

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

        // The opening pose, remembered, as the tow's fallback for a session with no pit boxes in it.
        _boxPosition = carT.position;
        _boxRotation = carT.rotation;
        _boxHeadingDeg = carHeadingDeg;
        _boxKnown = true;

        // Make sure nothing drives the car until the player climbs in.
        car.enabled = false;
        var spline = car.GetComponent<SplineDriver>();
        if (spline != null) spline.enabled = false;

        // A single race never puts a body on the ground. No walk up pit road, no motorhome, no cast and no
        // title card: the driver is already strapped in when the scene opens, at driving zoom, and what is
        // left of this object is the pit geometry it just published and the tow. Everything below this
        // point builds the on-foot opening, so a single race stops here.
        if (!GameSession.OnFootAllowed)
        {
            _pitSamples = samples;
            _usedPit = usedPit;

            _cam = Camera.main;
            if (_cam != null)
            {
                _camFollow = _cam.GetComponent<CameraFollow>();
                if (_camFollow == null) _camFollow = _cam.gameObject.AddComponent<CameraFollow>();
                _camFollow.target = car.transform;
                _cam.orthographicSize = drivingOrthoSize;
            }
            _orthoTarget = drivingOrthoSize;

            _entered = true;
            _briefed = true;   // nobody to brief and no setup panel: this is a race, not a race weekend
            AmbienceLoop.Play(ambienceClip, ambienceVolume);
            StartCoroutine(DriveOnceTheFieldIsUp());
            return;
        }

        _current = this;
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

            // Where in the room they actually wake up. The anchor above is the marker, so the furniture
            // stays put and only the driver moves — down the rig toward the bed end by default.
            rv.PlaceOccupantLocal(rvSpawnOffset);

            rvExterior = exterior;
            rvRoom = rv;
            _rvRoom = rv;
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
        var carSprite = car.GetComponentInChildren<SpriteRenderer>();
        _carIcon = carSprite != null ? carSprite.sprite : null;

        // Woken up rather than dropped in: the alarm and the fade come first, and the card that says where
        // and when you are is read off the black screen while the clock is still going. Both paths end in
        // the same title card.
        _introTitle = $"{trackTitle} - {spawnLabel}";
        if (waking) StartCoroutine(WakeUpThenIntroduce(_introTitle, when));
        else
        {
            _intro = SpawnIntroUI.Create(_introTitle, _player.transform, when);
            SyncCarMarker();
        }
    }

    // Hand the car over once the rest of the scene is standing, not on the frame it is built.
    //
    // Climbing into the car is what starts the race (PlayerEnteredCar -> FormationDirector.BeginFormation),
    // and that only works if the people listening for it exist and the field it forms up is parked. In a
    // career the walk up pit road buys all of that: several seconds in which FormationDirector.Start
    // subscribes, the safety car is spawned and GridSpawner finishes putting every AI in its box. A single
    // race has no walk, so firing the hand-over from inside this object's own Start ran the formation lap
    // before there was a field or a safety car to run it behind, and the AI drove off at racing pace,
    // leaving the player parked.
    //
    // So: one frame for everyone else's Start, then the field's own signal, then over it goes. The timeout
    // covers a scene with no GridSpawner in it at all rather than leaving the player sat in a dead car.
    IEnumerator DriveOnceTheFieldIsUp()
    {
        yield return null;   // every other Start() has now run, subscriptions included

        if (FindFirstObjectByType<GridSpawner>() != null)
        {
            float deadline = Time.time + fieldWaitTimeout;
            while (!GridSpawner.FieldReady && Time.time < deadline) yield return null;
        }

        // One more frame so the cars that were just parked have had a physics step to settle on their
        // boxes before the phase moves off PreGrid and releases them.
        yield return new WaitForFixedUpdate();

        StartDriving(CarSetup.Load());
    }

    // ------------------------------------------------------------------ waking up

    // The weekend whose first morning has already been slept through. A weekend is several scene loads —
    // practice, qualifying, the race, a trip to the garage and back — and only the first of them is a
    // morning; the rest are the same day continuing.
    const string WokeUpKey = "weekend.wokeup";

    // Why the scene did or didn't open with the alarm. Read by Draftmaster > Debug when the opening does
    // not play and it is not obvious which of the five gates said no.
    public static string LastWakeDecision = "not evaluated";

    bool ShouldWakeUp(PlayerSpawnPoint marker)
    {
        if (!wakeUpInRV) { LastWakeDecision = "wakeUpInRV is off"; return false; }
        if (!rvInterior) { LastWakeDecision = "rvInterior is off"; return false; }
        if (marker == null || marker.gameObject.name != forcedSpawnName)
        {
            LastWakeDecision = $"spawn is '{(marker == null ? "none" : marker.gameObject.name)}', not {forcedSpawnName}";
            return false;
        }

        // Here to drive, or already part-way through the three days: no alarm, you have been up for hours.
        if (!string.IsNullOrEmpty(WeekendDirector.PendingRouteId))
        {
            LastWakeDecision = "here to drive a booked session";
            return false;
        }
        if (Draftmaster.Weekend.WeekendLedger.DoneCount > 0 ||
            Draftmaster.Weekend.WeekendLedger.MissedCount > 0)
        {
            LastWakeDecision = "the weekend is already underway";
            return false;
        }

        if (PlayerPrefs.GetInt(WokeUpKey, -1) == RaceWeekend.WeekendId)
        {
            LastWakeDecision = "already woken up this weekend";
            return false;
        }

        LastWakeDecision = "waking up";
        return true;
    }

    IEnumerator WakeUpThenIntroduce(string title, string when)
    {
        PlayerPrefs.SetInt(WokeUpKey, RaceWeekend.WeekendId);
        PlayerPrefs.Save();

        var walker = _player.GetComponent<OnFootController>();
        var settings = WakeUpSequence.Settings.Default;
        settings.alarmClip = alarmClip;
        settings.alarmVolume = alarmVolume;
        settings.darkSeconds = wakeDarkSeconds;
        settings.fadeInSeconds = wakeFadeInSeconds;
        settings.getUpSeconds = wakeGetUpSeconds;
        settings.lyingDownSprite = lyingDownSprite;
        settings.getUpTrigger = getUpTrigger;
        settings.facing = wakeFacing;

        // Up before the alarm is, and drawn over the top of the black rather than under it: a black screen
        // with a buzzer on it says nothing about where the weekend has taken you, and this is the one beat
        // in the game with no scene to read that off. Held long enough to span the dark AND the fade, so it
        // is still there as the motorhome comes up and then bows out on its own.
        //
        // A morning after a night's sleep reuses the card already on screen: it is carrying the scene's
        // markers, and a second one stood up beside it would leave the first drawing them forever.
        float restoreHold = -1f;
        if (_intro == null) _intro = SpawnIntroUI.Create(title, _player.transform, when);
        else
        {
            restoreHold = _intro.titleHold;
            _intro.ShowTitle(title, when);
        }
        _intro.overFade = true;
        _intro.titleHold = Mathf.Max(_intro.titleHold, wakeDarkSeconds + wakeFadeInSeconds);

        // No walker to wake up (a prefab with no controller): bring the lights up rather than leaving the
        // player staring at the black screen this method just committed to.
        if (WakeUpSequence.Play(walker, settings) == null)
        {
            ScreenFade.FromBlack(0f, 0.25f);
            yield return new WaitForSecondsRealtime(0.25f);   // the card still has to outlast the wipe
        }
        while (WakeUpSequence.Running) yield return null;

        // Lights on: the card goes back into the ordinary stack and the objective markers come with it.
        _intro.overFade = false;
        SyncCarMarker();

        // The stretched hold was for this card; later banners keep their own rhythm. Put back once the card
        // has had its time rather than now, or it would be cut short mid-fade — and off to one side, so the
        // morning carries on while it does.
        if (restoreHold >= 0f) StartCoroutine(RestoreTitleHold(restoreHold));
    }

    IEnumerator RestoreTitleHold(float hold)
    {
        while (_intro != null && _intro.TitleBusy) yield return null;
        if (_intro != null) _intro.titleHold = hold;
    }

    // ------------------------------------------------------------------ going to bed

    // Friday and Saturday end in bed. Once the evening's last obligation is done the weekend stops booking
    // (WeekendDirector.BookNextUp waits on Draftmaster.Weekend.WeekendBedtime), the objective marker points
    // at the motorhome and then the bed in it, and tapping the bed (BedInteractable) puts the driver to
    // sleep: black, the clock moves to the next morning, and they wake up to the alarm exactly as the first
    // morning of the weekend opened.

    [Header("Going To Bed")]
    [Tooltip("Seconds the screen takes to go dark when the driver lies down.")]
    public float sleepFadeSeconds = 1.2f;
    [Tooltip("Seconds of silent black between lying down and the alarm going off - the night.")]
    public float nightSeconds = 1.5f;

    string _introTitle = "";
    RVInterior _rvRoom;
    bool _sleeping;
    bool _bedtimeDue;
    float _bedtimePoll;
    Transform _bedHome;      // the player's motorhome shell, looked up at the poll rather than every frame
    Transform _bedMarked;    // what the bedtime marker is currently hung on

    // True from the moment the driver lies down until they are back on their feet the next morning.
    public bool Sleeping => _sleeping;

    // Put the driver to bed and wake them the next morning. False (and nothing happens) unless it is
    // actually bedtime and there is a driver on foot to do it with.
    public bool GoToSleep()
    {
        if (_sleeping || _player == null || !_player.activeInHierarchy) return false;
        if (!GameSession.CareerActive || Coop.IsGuest) return false;   // the host's weekend; the host sleeps
        if (ScreenFade.Busy || !Draftmaster.Weekend.WeekendBedtime.Due()) return false;

        StartCoroutine(SleepThroughTheNight());
        return true;
    }

    IEnumerator SleepThroughTheNight()
    {
        _sleeping = true;
        ClearBedtimeMarker();
        if (_rvRoom != null && _rvRoom.Bed != null) _rvRoom.Bed.enabled = false;

        var walker = _player.GetComponent<OnFootController>();
        if (walker != null) walker.MovementLocked = true;

        // Lights out.
        bool dark = false;
        ScreenFade.ToBlack(Mathf.Max(0.05f, sleepFadeSeconds), () => dark = true);
        while (!dark) yield return null;

        // The night. The rest of the evening is given up (there is nothing left in it - that is what made it
        // bedtime) and the clock opens on the next morning. Whatever was booked belonged to yesterday.
        Draftmaster.Weekend.WeekendBedtime.Sleep();
        WeekendAppointment.Clear();

        // Back where the first morning woke them, beside the bed, whichever corner of the motorhome they
        // lay down from.
        if (_rvRoom != null) _rvRoom.PlaceOccupantLocal(rvSpawnOffset);

        for (float t = 0f; t < nightSeconds; t += Time.unscaledDeltaTime) yield return null;

        // Morning: the alarm, the where-and-when card over the black, the fade, getting up. The same beat
        // the career opens on.
        string when = Draftmaster.Weekend.WeekendSlots.Day(Draftmaster.Weekend.WeekendLedger.CurrentSlot) + " - " +
                      Draftmaster.Weekend.WeekendSlots.ClockAmPm(Draftmaster.Weekend.WeekendLedger.ClockMinute);
        if (walker != null) walker.MovementLocked = false;   // WakeUpSequence takes the lock itself
        yield return WakeUpThenIntroduce(_introTitle, when);

        // ...and the game carries on: the new day's first obligation goes on the map.
        _sleeping = false;
        _bedtimeDue = false;
        _bedtimePoll = 0f;
        WeekendDirector.BookNextUp(replaceExisting: true);
    }

    // Keep the bed and the marker in step with whether it is bedtime. The rule itself is a sheet walk, so it
    // is asked twice a second rather than every frame; the marker is re-stated every frame because it can
    // share its target (the motorhome) with the weekend's own marker, and that one takes its entry with it
    // when its booking clears.
    void StepBedtime()
    {
        if (Time.unscaledTime >= _bedtimePoll)
        {
            _bedtimePoll = Time.unscaledTime + 0.5f;
            _bedtimeDue = !_sleeping && GameSession.CareerActive && !Coop.IsGuest
                          && Draftmaster.Weekend.WeekendBedtime.Due();
            var shell = _bedtimeDue ? RVExterior.Player : null;
            _bedHome = shell != null ? shell.transform : null;
        }

        var bed = _rvRoom != null ? _rvRoom.Bed : null;
        bool live = _bedtimeDue && !_sleeping && _player.activeInHierarchy;
        if (bed != null && bed.enabled != live) bed.enabled = live;

        Transform target = null;
        string label = "";
        if (live && bed != null && _rvRoom.IsInside) { target = bed.transform; label = "Bed"; }
        else if (live && _bedHome != null) { target = _bedHome; label = "Your motorhome - bed"; }
        else if (live && bed != null) { target = bed.transform; label = "Bed"; }

        if (target != _bedMarked) ClearBedtimeMarker();
        if (target == null || _intro == null) return;

        bool fresh = _bedMarked == null;
        _bedMarked = target;
        _intro.AddMarker(target, BedIcon(), hideWithinMetres: 1.5f, label: label, priority: 10);
        if (fresh) _intro.PulseMarker(target);
    }

    void ClearBedtimeMarker()
    {
        if (_bedMarked != null && _intro != null) _intro.RemoveMarker(_bedMarked);
        _bedMarked = null;
    }

    // The bed's own art, if the room has it, so the marker is a picture of where to go.
    Sprite BedIcon()
    {
        var art = _rvRoom != null && _rvRoom.InteriorRoot != null ? _rvRoom.InteriorRoot.Find("Bed") : null;
        var sr = art != null ? art.GetComponentInChildren<SpriteRenderer>(true) : null;
        return sr != null ? sr.sprite : null;
    }

    void SpawnPlayer(Vector3 pos)
    {
        // In front of the ground and of everything laid on it. The paddock is drawn through the 3D URP
        // renderer, so depth is what covers what, and a player spawned at tarmac depth is BEHIND every prop
        // in the place — which is what made the player disappear on the winner's circle floor and, before
        // it, under the hospitality canopy. Everyone else was already forward of them.
        pos.z = Mathf.Min(pos.z, PaddockPerson.PlayerZ);

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

        // Is anybody standing outside the motorhome with today's plan? If so the weekend books nothing
        // until they have said it, and the driver wakes up with an empty objective strip. If not — this
        // track's cast has no liaison, or hers didn't pass her conditions — the weekend books for itself
        // as it always did, straight away rather than after a pause the player would read as a bug.
        WeekendDirector.OpeningCastBuilt(PlacedNPC.ObjectiveGiver() != null);
    }

    void OnDestroy()
    {
        PlacedNPC.CutsceneFinished -= OnPlacedCutsceneFinished;
        RunLocked = false;
        if (_gettingIn == this) _gettingIn = null;
        if (_current == this) _current = null;
    }

    // No running until the run hint has been shown. The hint itself waits on the phone lesson
    // (ChiefCheckInBeat.HoldsRunHint), so on the opening walk that is: phone goes off, phone out, phone away,
    // "run" taught — and only then do the legs work. Taught once per save, so a returning player never sees
    // the lock; and only while this walk is still the one that will teach it, so a scene or phase that never
    // shows the hint can't leave the player unable to run. OnFootController reads it.
    public static bool RunLocked { get; private set; }

    // An opening beat has finished: put the objective back on screen and only now teach the run control
    // (the player has just got movement back, and the hint would otherwise have landed under the dialogue).
    // The run hint itself is shown from StepWalkHints next frame, not here: the liaison's beat books the
    // briefing from its own Finished handler, and until that has run nobody can tell whether the phone's
    // lesson has to go first.
    //
    // What it no longer does is throw the car marker's fly-in again. That marker flew in when it was added,
    // and a second fly-in out of the middle of the screen does not read as emphasis — it reads as a second
    // marker turning up.
    void OnPlacedCutsceneFinished(PlacedNPC npc)
    {
        _hintsHeldForCutscene = false;
        _hintOriginSet = false; // re-base the "walked far enough" test from where the talk left them

        if (_intro != null)
        {
            // The centre card says where you are, not what you are due — that belongs on the strip at the
            // top, which is the one place the objective is written. So the beat's closing line slides that
            // strip back in; the card is only used when there is no strip to use (a booking-less scene),
            // where it is the only surface there is.
            bool toldByTheStrip = WeekendAppointment.Pending != null && WeekendObjectiveHUD.Instance != null;
            if (toldByTheStrip) WeekendObjectiveHUD.Reveal();
            else if (npc != null && !string.IsNullOrEmpty(npc.objectiveOnFinish))
                _intro.ShowTitle(npc.objectiveOnFinish);
        }
        if (showControlHints && !ControlHints.Taught("run")) _runHintDue = true;
    }


    void Update()
    {
        if (_cam != null && _cam.orthographic)
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _orthoTarget, 1f - Mathf.Exp(-orthoLerpSpeed * Time.deltaTime));

        StepIndoorZoom();

        RunLocked = showControlHints && _player != null && !_entered && _phase == EntryPhase.Walking
                    && !ControlHints.Taught("run");

        if (_player == null) return;

        StepBedtime();
        if (_sleeping) return;

        if (_phase == EntryPhase.Briefing) { StepBriefing(); return; }
        if (_phase == EntryPhase.Driving) { StepParkedExit(); return; }
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
            StepSessionEntryFromRV(due);

            // With a motorhome in the paddock the session is started by walking into it, and the car is not
            // a way in: it is parked at the back of the team's garage, and letting E there start the hour
            // put the player in a scene that reloaded around them and stood them somewhere else. A track
            // with no motorhome keeps the old route, so nothing is left with no way to start.
            bool rvRoute = RVExterior.Player != null;
            bool carRoute = due != null && !rvRoute;

            ShowPrompt(inRange && carRoute);
            if (!carRoute) ControlHints.Hide("entercar");
            if (inRange && carRoute && InteractPressed()) WeekendDirector.Begin(due);
            return;
        }

        ShowPrompt(inRange);

        // A co-op guest never gets into the scene's player car: that car is the HOST's entry in the weekend,
        // and the guest's copy of it is scene content, not a second entry. When a session starts, the guest
        // is put into one of the cars already in the field instead (CoopPossession).
        if (inRange && InteractPressed() && !Coop.IsGuest) EnterCar();
    }

    // Armed once the player has been seen OUTSIDE the motorhome. The scene opens with them stood inside it,
    // and a load that lands them back in there — coming out of the garage sheet, say — must not read as
    // walking in and start the session under them.
    bool _rvEntryArmed;

    // Between sessions the player's car is not on pit road: PopupGarageLot takes it home to the team's
    // garage, because a car sat in a box through somebody else's practice is a car in everybody's way. So an
    // hour in the car cannot begin at the car. It begins where a driver's hour begins — at their own
    // motorhome. Walk in with a session booked and the paddock turns over for it: the field comes out of
    // the garages and into the boxes, the player's own car with it, and they step back out into a pit lane
    // that is ready for them.
    //
    // The turnover itself is the scene reload WeekendDirector.Begin already does. The spawn it lands on is
    // the motorhome, which is exactly where this player is standing, so the world changes around them
    // rather than teleporting them across the paddock.
    void StepSessionEntryFromRV(Draftmaster.Weekend.WeekendActivity due)
    {
        var room = RVInterior.Current;
        bool inside = room != null && room.IsInside;

        if (!inside) { _rvEntryArmed = true; return; }
        if (!_rvEntryArmed || due == null) return;

        _rvEntryArmed = false;   // one turnover per walk-in
        WeekendDirector.Begin(due);
    }

    // Walking through a doorway pulls the camera in; walking back out pushes it to walking distance again.
    //
    // Written on the TRANSITION, not asserted every frame. This component is the scene's zoom arbiter and
    // several things borrow it for a moment — the broadcast cut pulling back to the field, the crew chief's
    // pit wall, a fight in the paddock, a seat in the grandstand. Re-stating a zoom every frame would
    // overrule each of them the frame after they asked. Crossing a threshold is an event, so it is handled
    // like one, and whoever asks next still wins until the player walks through another door.
    //
    // Both kinds of room count, because to the player they are the same move: the motorhome and the team's
    // popup garage are each a masked room you step into off the paddock.
    void StepIndoorZoom()
    {
        bool indoors = PlayerIsIndoors();
        if (indoors == _indoors) return;

        _indoors = indoors;
        _orthoTarget = indoors ? indoorOrthoSize : onFootOrthoSize;
    }

    // Is the player stood in any masked room right now. A list read, not a scene walk: both interiors keep
    // the same register everybody else on foot keeps.
    static bool PlayerIsIndoors()
    {
        var rooms = RVInterior.All;
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i] != null && rooms[i].IsInside) return true;

        return PopupGarageInterior.Occupied != null;
    }

    // Teach the two things the walk needs, as the player gets to them: sprint once they're actually walking,
    // and "get in" while the car is still a way off. Both are once-per-save (ControlHints owns that memory).
    // On the walk to the first briefing, sprint waits until the phone has gone off and been put away again.
    void StepWalkHints()
    {
        if (_hintsHeldForCutscene) return; // released by the cutscene's Finished callback
        if (!_hintOriginSet) { _hintOrigin = _player.transform.position; _hintOriginSet = true; }

        // Asked of the hint's own memory, not a local flag: the phone lesson re-arms "run" when it fires, and
        // a flag set by an earlier Show that did nothing (already taught) would swallow the re-armed one.
        if (!ControlHints.Taught("run") && !ChiefCheckInBeat.HoldsRunHint
            && (_runHintDue || Vector2.Distance(_player.transform.position, _hintOrigin) > runHintAfterMetres))
        {
            ControlHints.Show("run", "LEFT SHIFT", InputGlyphs.Pad(PadBindings.Run), "Hold to run",
                              touchText: "Double-tap the left stick to run / walk");
            _hintedRun = true;
            _runHintDue = false;
        }

        if (!_hintedEnter && RaceWeekend.SessionLive
            && Vector2.Distance(_player.transform.position, car.transform.position) < enterHintRange)
        {
            ControlHints.Show("entercar", "E", InputGlyphs.Pad(PadBindings.Interact), "Get in the car",
                              onPress: PressInteract, icon: PixelGUI.ActionIcon.Car,
                              touchText: "Tap the car to get in");
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

        if (!_briefed && _chief != null && _chief.lines != null && _chief.lines.Length > 0)
        {
            // Hold the walking zoom through the briefing — the chief is stood beside the car and both
            // bubbles are on-foot scale. The pull-back to driving distance waits for his last line.
            SetPhase(EntryPhase.Briefing);
            if (_chiefNpc != null) _chiefNpc.MarkPlayed(); // the briefing has actually started, not just been staged
            _chief.SetInteractor(car.transform); // "#player" lines bubble over the car, where the driver now is
            _chief.Interact();                   // opens the first line
            _interactHeldPrev = true;            // swallow the same press that got us in the car
            _briefingFrom = Time.unscaledTime;   // ...and the same tap
            if (showControlHints) ControlHints.Show("advance", "E", InputGlyphs.Pad(PadBindings.Interact), "Continue",
                                                    onPress: PressInteract, icon: PixelGUI.ActionIcon.Next);
            return;
        }

        OpenSetupOrDrive();
    }

    // Advance the chief's lines on interact; when he runs out, the setup panel takes over. On a phone a tap
    // anywhere does it, as it does in every conversation on foot — the player is sat in the car now, so the
    // walk controls that do that job there have stood down, and this reads the tap itself.
    void StepBriefing()
    {
        if (_chief == null) { OpenSetupOrDrive(); return; }
        bool pressed = InteractPressed();
        if (TouchTaps.TakeTap(downAfter: _briefingFrom)) pressed = true;
        if (pressed && !_chief.Interact()) OpenSetupOrDrive();
    }

    float _briefingFrom;

    void OpenSetupOrDrive()
    {
        ControlHints.Hide("advance");
        _orthoTarget = drivingOrthoSize; // the talking is over — now pull back to driving distance
        if (!showSetupPanel || _briefed) { StartDriving(null); return; }
        SetPhase(EntryPhase.Setup);
        CarSetupPanelUI.Open(CarSetup.Load(), StartDriving);
    }

    // Everything is settled — apply the setup, hand the car over, and tell the rest of the scene we're driving.
    void StartDriving(CarSetup setup)
    {
        setup?.ApplyTo(car.gameObject);

        SetPhase(EntryPhase.Driving);
        _briefed = true;
        ParkedCarPin.Release(car);   // it is being driven now, not parked (a tow pins it; see TowToPits)
        car.enabled = true; // PlayerVehicleController.Start captures parked heading on first enable
        if (fitPitLimiter) EnsurePitLimiter();

        if (showControlHints)
        {
            ControlHints.Show("drive", "W / S", InputGlyphs.Pad(PadBindings.Throttle) + " / " + InputGlyphs.Pad(PadBindings.Brake),
                              "Throttle and brake", 6f);
            if (fitPitLimiter) ControlHints.Show("limiter", "L", InputGlyphs.Pad(PadBindings.PitLimiter),
                                                 "Pit limiter — holds you to the pit speed limit", 7f,
                                                 onPress: TogglePitLimiter, icon: PixelGUI.ActionIcon.Limiter);
        }

        // The first time out this session, the screen starts from lap timing alone. Every scene load is a new
        // session (practice, qualifying and the race each reload), so a flag on this object is enough.
        if (!_headedOut) { _headedOut = true; SessionHud.HeadOut(); }

        PlayerEnteredCar?.Invoke();
    }

    // Drag the car in off the circuit and put the driver out beside it, in their own box.
    //
    // This is the reverse of EnterCar and deliberately shares its moving parts: the controller goes off,
    // the on-foot body comes back on, and the camera follows whoever the player currently is. What it does
    // NOT do is repair anything — the car arrives as wrecked as it left, and the crew work on it from
    // there (PitCrewRepair). A tow that handed back a straight car would make crashing free.
    //
    // Both bodies are moved the way this project has learned to move things — through the model that owns
    // the pose (SeedPose for the car) and with the Rigidbody2D written as well as the Transform — and the
    // box is the one the player actually has this session (CurrentBoxPose), not the spot the car happened
    // to be parked on when the scene opened.
    //
    // Returns false when there is nothing to tow: not driving, or the scene never worked out where the
    // car's box is.
    public bool TowToPits()
    {
        if (!IsDriving || car == null || !_boxKnown) return false;
        // A single race has no body to stand beside the car, but the tow itself still has to work: the car
        // is dragged to its box and repaired with the driver left sat in it.
        if (_player == null && GameSession.OnFootAllowed) return false;

        // Controls off first. The body is about to be teleported and a live controller would spend the
        // frame fighting the move.
        car.enabled = false;

        // And the AI brain with them. The player's own car keeps a SplineDriver it does not normally use,
        // and the broadcast cut (V) and the crew chief's headset both switch it on to drive the car while
        // the player is watching somebody else. A brain left running through a tow does not park the car in
        // its box — it drives it back out onto the circuit, which is the driver stood in an empty box
        // watching their lap clock carry on. The same two lines the scene open parks it with.
        var brain = car.GetComponent<SplineDriver>();
        if (brain != null) brain.enabled = false;
        var aiInput = car.GetComponent<SplineInputDriver>();
        if (aiInput != null) aiInput.enabled = false;
        car.externalInput = false;

        CurrentBoxPose(out Vector3 boxPos, out float boxHeadingDeg);

        // Parked through the dynamic model rather than by writing the transform: SeedPose puts the car on
        // the pose AND clears the heading and speed it crashed with, so what the driver climbs back into is
        // stopped and square instead of still carrying the moment it hit the wall.
        car.SeedPose(boxPos, boxHeadingDeg);

        // Then tell the BODY, or the move is only a suggestion — the pose physics holds is the one that
        // wins, which is why every other place that teleports a car here writes it too (PopupGarageLot
        // taking the car home to its garage, CoopPossession handing one over).
        var body = car.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = car.transform.position;
            body.rotation = car.transform.eulerAngles.z;
        }

        // The running order is kept by watching each car's distance along the track tick past, so a car that
        // is picked up and put down somewhere else has to say so — otherwise the jump reads as a lap. Half a
        // lap's worth of it, at the wrong moment, and the tow hands the driver a free lap on the scoreboard
        // and re-arms the clock it is about to stop.
        RacePositionTracker.NoteTeleport(car.transform);

        // The lap they were on ended in the wall. Nothing else was going to end it: the pit-lane rule that
        // voids a running lap is read off a car's spline, and the human car's is switched off, so the clock
        // ran on through the crash, the tow and the whole repair.
        if (LapTimingManager.Instance != null) LapTimingManager.Instance.AbandonLap(car.transform);

        // No on-foot body in this mode: the car is back in its box, the crew go to work on it, and the
        // driver never gets out. Controls come back on the far side of the repair, the same as the car
        // they would have climbed back into.
        if (_player == null)
        {
            car.enabled = true;
            PitCrewRepair.Begin(car);
            return true;
        }

        PutDriverOutBesideCar();
        PitCrewRepair.Begin(car);
        return true;
    }

    // The driver steps out of a car that is already stopped where it should stay, and the player is on foot
    // beside it: the second half of a tow, and the whole of climbing out in the box. The car's controls must
    // already be off.
    void PutDriverOutBesideCar()
    {
        ControlHints.Hide(ExitHintId);
        _offeringExit = false;

        // Nothing owns a parked car's pose: the controller that was writing it is off, and the crew may be
        // working on it for minutes. Pin it to the box so it is still there when the driver walks back to
        // it. See ParkedCarPin — it takes itself off the moment the car is driven again.
        ParkedCarPin.Hold(car);

        // Stood beside the car rather than inside it, so walking away from it works the same as it did at
        // the start of the session — and on the garage side of it, not out on pit road. See BesideCarPosition.
        Vector3 beside = BesideCarPosition();
        beside.z = _player.transform.position.z;
        _player.transform.position = beside;
        _player.SetActive(true);

        // Same again on foot. That body is dynamic with interpolation on, which rewrites the Transform from
        // the body pose — the trap that used to leave a repositioned NPC stood at the world origin.
        var walker = _player.GetComponent<Rigidbody2D>();
        if (walker != null)
        {
            walker.linearVelocity = Vector2.zero;
            walker.position = beside;
        }

        if (_camFollow != null) _camFollow.target = _player.transform;
        _orthoTarget = onFootOrthoSize;

        // Back to the walk-up state, so E gets them into the car again once the crew are done with it.
        SetPhase(EntryPhase.Walking);
        _entered = false;
        _hintedEnter = false;
        SyncCarMarker();
    }

    // How far (m) from the car's centre the driver stands once out of it: clear of a car's half-width.
    const float StepOutMetres = 2.2f;

    // Where a driver stands beside the parked car: a step off its side on the GARAGE side of the box, away
    // from the pit lane cars drive down. A tow, climbing out and the fast travel to a session all land here.
    //
    // Read off the pit lane, not off the car. It used to be a step along the car's own local -Y, which on
    // the player's car (sprite facing down its local -X, angleOffsetDeg 180) is the driver's left — the
    // pit-lane side of a car parked nose down the lane — so the driver was stood in the path of the field.
    public Vector3 BesideCarPosition()
    {
        if (car == null) return transform.position;

        // Not run through PaddockBoundary: the walkable area can be drawn as the paddock alone, and clamping
        // to it threw the driver a hundred metres from the car they had just climbed out of.
        CurrentBoxPose(out _, out float laneHeadingDeg);
        return BesideCar(car.transform.position, laneHeadingDeg, GarageSideSign());
    }

    // A step of StepOutMetres across the lane from the car: along the pit lane's normal (right of travel,
    // TrackBuilder's convention), signed toward the garages.
    static Vector3 BesideCar(Vector3 carPos, float laneHeadingDeg, float garageSign)
    {
        float r = laneHeadingDeg * Mathf.Deg2Rad;
        var normal = new Vector3(Mathf.Sin(r), -Mathf.Cos(r), 0f);
        return carPos + normal * (garageSign * StepOutMetres);
    }

    // Which side of the pit centreline the garages are on, along its normal: the side the box lane is on,
    // since the parked file sits between pit road and the garages. +1 when nothing says otherwise — that is
    // where TrackBuilder lays every box lane.
    float GarageSideSign()
    {
        float lateral = PitLane.Configured ? PitLane.ParkLateral : 0f;
        if (Mathf.Approximately(lateral, 0f) && track != null && track.HasPitBoxLane)
            lateral = track.PitBoxLaneCenterLateral;
        return lateral < 0f ? -1f : 1f;
    }

    // ------------------------------------------------------------------ getting out in the box

    // Stopped in their own box, the driver can get out: E, the pad's interact button, or — on a phone, where
    // there is no E — the prompt at the bottom of the screen, the same button the stranded-car tow offers.
    const string ExitHintId = "exitcar";
    [Header("Getting out")]
    [Tooltip("How close (m) the car must be to the player's own box for the driver to get out.")]
    public float exitBoxRadius = 4f;
    [Tooltip("Stopped means below this (mph)...")]
    public float exitBelowMph = 0.5f;
    [Tooltip("...for at least this long (s), so rolling to a halt doesn't flash the prompt.")]
    public float exitAfterSeconds = 0.4f;

    bool _offeringExit;
    float _stoppedInBoxSince = -1f;

    void StepParkedExit()
    {
        bool pressed = InteractPressed();   // read every frame so a held E doesn't fire the moment the car stops
        bool can = CanClimbOut();

        if (can != _offeringExit)
        {
            _offeringExit = can;
            if (can)
                ControlHints.ShowSticky(ExitHintId, "E", InputGlyphs.Pad(PadBindings.Interact), "Get out of the car",
                                        onPress: PressInteract, icon: PixelGUI.ActionIcon.Walk);
            else
                ControlHints.Hide(ExitHintId);
        }

        // Once a practice or qualifying session is over, stopping in the box is the end of it: the driver gets
        // out without being asked, and the session closes behind them (SessionEndPitIn).
        if (can && (pressed || PracticeDirector.SessionOver)) ClimbOut();
    }

    // Whether this scene worked out where the player's box is. Without it there is no climbing out in the box.
    public bool BoxKnown => _boxKnown;

    // The session is over and the AI has brought the car into its box for the driver: out they get. The car
    // is parked on the pose the AI stopped it on — written through the dynamic model and the body, the way a
    // tow parks it, so the controller does not resume from wherever it was when the AI took over if the
    // driver gets back in later. False when there is nobody to put out (not driving, or no on-foot body).
    public bool ClimbOutAtSessionEnd()
    {
        if (!IsDriving || car == null || _player == null) return false;

        var brain = car.GetComponent<SplineDriver>();
        float heading = brain != null && brain.enabled ? brain.CommandedHeadingDeg : car.HeadingDeg;
        car.SeedPose(car.transform.position, heading);
        var body = car.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = car.transform.position;
            body.rotation = car.transform.eulerAngles.z;
        }

        ClimbOut();
        return true;
    }

    bool CanClimbOut()
    {
        if (!IsDriving || car == null || !_boxKnown || !GameSession.OnFootAllowed || Coop.IsGuest) return Idle();
        if (RacePauseMenu.IsPaused || ScreenFade.Busy) return Idle();

        // Somebody else is driving it: the broadcast cut or the crew chief's headset hands the car to the AI.
        if (car.externalInput) return Idle();
        // ...or the kinematic hand-off, where the AI's spline drives the car with the controller switched off
        // (and its speed reading frozen at whatever it was when the AI took over).
        var brain = car.GetComponent<SplineDriver>();
        if (brain != null && brain.enabled) return Idle();

        // The crew are on it — a pit stop in progress, or a repair the driver is sat through.
        var stop = car.GetComponent<PitStopController>();
        if (stop != null && stop.IsPitting) return Idle();
        if (PitCrewRepair.Active != null) return Idle();

        CurrentBoxPose(out Vector3 boxPos, out _);
        if (Vector2.Distance(car.transform.position, boxPos) > exitBoxRadius) return Idle();
        if (car.SpeedMph > exitBelowMph) return Idle();

        if (_stoppedInBoxSince < 0f) _stoppedInBoxSince = Time.time;
        return Time.time - _stoppedInBoxSince >= exitAfterSeconds;

        bool Idle() { _stoppedInBoxSince = -1f; return false; }
    }

    // The reverse of EnterCar, with the car left exactly where it stopped: controls off, the brain off with
    // them (see TowToPits for why), the lap they were on closed, and the driver out beside the door. Nothing
    // is repaired — that is what a tow or a pit stop is for.
    void ClimbOut()
    {
        car.enabled = false;
        var brain = car.GetComponent<SplineDriver>();
        if (brain != null) brain.enabled = false;
        var aiInput = car.GetComponent<SplineInputDriver>();
        if (aiInput != null) aiInput.enabled = false;
        car.externalInput = false;

        var body = car.GetComponent<Rigidbody2D>();
        if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }

        // A lap that ends parked in the box isn't a lap. The pit-lane rule that voids one reads the car's
        // spline, which the human car doesn't run, so it has to be closed here as the tow closes it.
        if (LapTimingManager.Instance != null) LapTimingManager.Instance.AbandonLap(car.transform);

        _stoppedInBoxSince = -1f;
        PutDriverOutBesideCar();
    }

    // Where the player's box is NOW, which is not where the car was parked when the scene opened.
    //
    // This object parks the car a fraction of the way down pit road because at that point there is no
    // ladder of boxes to park it in: the boxes are fitted to the entry list, and the entry list arrives
    // with the field. GridSpawner then snaps the car into the box the player has earned — which, for a car
    // starting at the back, is most of a pit lane from where it began the scene. A tow that read the
    // opening snapshot therefore dropped the car out on pit road, with the driver stood beside it and their
    // own box, their crew and the marker over it somewhere else entirely.
    //
    // So the box is re-derived from the same published geometry every other pit system reads (PitLane), and
    // the opening pose is kept only for a session that fitted no boxes at all.
    void CurrentBoxPose(out Vector3 pos, out float headingDeg)
    {
        pos = _boxPosition;
        headingDeg = _boxHeadingDeg;

        if (track == null || !_usedPit || _pitSamples == null || _pitSamples.Count < 2) return;
        if (!PitLane.Configured || PitLane.PlayerBox < 0) return;

        float pitLength = _pitSamples[_pitSamples.Count - 1].distance;
        if (pitLength <= 0f) return;

        // Single file on the pit centerline at the box lane's offset, nose down the lane — the same three
        // lines GridSpawner parks the car with, and the same ones the AI park their own boxes on.
        var s = track.SamplePitAt(PitLane.BoxDistance(PitLane.PlayerBox, pitLength), _pitSamples);
        Vector2 parked = s.position + s.normal * PitLane.ParkLateral;
        Vector3 world = track.transform.TransformPoint(new Vector3(parked.x, parked.y, 0f));
        Vector3 tangent = track.transform.TransformDirection(new Vector3(s.tangent.x, s.tangent.y, 0f));

        pos = new Vector3(world.x, world.y, _boxPosition.z);
        headingDeg = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
    }

    void EnsurePitLimiter()
    {
        var limiter = car.GetComponent<PitLimiter>();
        if (limiter == null) limiter = car.gameObject.AddComponent<PitLimiter>();
        limiter.car = car;
        limiter.track = track;
    }

    // The prompt's button, pressed: counts as one press of E on the next InteractPressed, so tapping "Get in
    // the car" or "Continue" runs exactly the path the key does.
    bool _virtualInteract;
    void PressInteract() => _virtualInteract = true;

    void TogglePitLimiter()
    {
        var limiter = car != null ? car.GetComponent<PitLimiter>() : null;
        if (limiter != null) limiter.SetArmed(!limiter.Armed);
    }

    bool InteractPressed()
    {
        if (_virtualInteract)
        {
            _virtualInteract = false;
            return true;
        }

        // On a phone the car is tapped, the way an NPC is: a finger on the bodywork is E. Only asked while the
        // player is in range, so a tap on the car from across the paddock does nothing, as E would.
        if (OnFootController.TakeUnclaimedTap(out Vector2 tap) && TapLandsOnCar(tap)) return true;

        bool held = false;
        var gp = Gamepad.current;
        if (gp != null) held |= PadInput.Control(gp, PadBindings.Interact).isPressed;
        var kb = Keyboard.current;
        if (kb != null) held |= kb.eKey.isPressed;

        bool pressed = held && !_interactHeldPrev;
        _interactHeldPrev = held;
        return pressed;
    }

    // A tap (screen space, y up) on the car's bodywork, with a little slack round it for a thumb.
    bool TapLandsOnCar(Vector2 screenPoint)
    {
        var cam = Camera.main;
        if (cam == null || car == null) return false;

        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -cam.transform.position.z));
        Vector2 p = new Vector2(w.x, w.y);
        foreach (var c in car.GetComponentsInChildren<Collider2D>())
            if (c != null && c.enabled && c.OverlapPoint(p)) return true;
        return Vector2.Distance(p, car.transform.position) <= 3f;
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
                    tm.text = InputGlyphs.Label("E", PadBindings.Interact);
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
