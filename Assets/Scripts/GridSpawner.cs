using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Draftmaster.Data;
using Unity.Netcode;
using UnityEngine;
// Aliased rather than a plain using: Draftmaster.Data has its own Series/Driver types and this file is
// built on those, so only the weekend names actually needed here are pulled in.
using RacingSeries = Draftmaster.Weekend.RacingSeries;
using SeriesCatalog = Draftmaster.Weekend.SeriesCatalog;
using WeekendLedger = Draftmaster.Weekend.WeekendLedger;

public class GridSpawner : MonoBehaviour
{
    public TrackBuilder track;
    public GameObject carPrefab;
    [Tooltip("Networked AI car prefab (NetworkObject + NetworkedAICar). In multiplayer the host spawns the field from this instead of carPrefab; clients receive the replicas.")]
    public GameObject networkedAiPrefab;
    [Tooltip("Material applied to each spawned car's SpriteRenderer. Use an unlit sprite material so cars render correctly without Light2D coverage.")]
    public Material carMaterial;
    [Tooltip("Sorting order applied to each spawned car's SpriteRenderer. Higher draws on top.")]
    public int carSortingOrder = 5;

    [Header("Livery / Names")]
    [Tooltip("Carset prefix for liveries + numbers, e.g. cup26 → loads Resources/cup26liveryN sprites.")]
    public string carsetPrefix = "cup26";
    [Tooltip("Give each car a distinct livery sprite from the carset (Resources/<prefix>liveryN).")]
    public bool useCarsetLiveries = true;
    [Tooltip("Fallback only: name drivers from the DriverNames surname pool when no database driver claims the car's number.")]
    public bool useDriverNamePool = true;
    [Tooltip("Highest car number probed when collecting liveries from Resources.")]
    public int maxLiveryNumber = 99;

    [Header("Field")]
    public int count = 6;
    [Tooltip("Distance between cars on the grid, in metres.")]
    public float spacing = 6f;
    [Tooltip("Lateral offset stagger between odd/even rows (m). 0 = single file.")]
    public float rowStagger = 3.5f;
    [Tooltip("Distance behind the start line where the front of the grid sits.")]
    public float gridStartDistance = -6f;
    [Tooltip("If true, AI start in their pit boxes. If false, they line up on the grid behind the start line. Currently false for testing.")]
    public bool spawnInPit = false;
    [Tooltip("Signed lateral offset (m) of the parked box lane from the pit centerline (positive = wall side, matching PitCrewSpawner.wallSide=1). Parked cars sit here in front of their box props; cars driving the lane keep the centerline so they clear the parked file.")]
    public float pitBoxParkLateral = 2.6f;
    [Tooltip("Formation-lap race: AI start frozen in pit boxes and form a train behind the safety car until green. Forces pit spawn, auto-fits pit-box spacing, and adds a FormationController to each car. Needs a FormationDirector in the scene.")]
    public bool formationRace = true;
    [Tooltip("Fallback car speed (m/s) when no VehicleInfo is assigned. Otherwise speed is driven by the vehicle's accel/decel curves.")]
    public float speed = 35f;
    [Tooltip("VehicleInfo asset applied to every spawned AI car. Defines accel/decel/cornering curves.")]
    public VehicleInfo vehicleInfo;
    [Tooltip("Scale to apply to spawned cars.")]
    public Vector2 carScale = new Vector2(6, 6);

    [Header("Teams")]
    [Tooltip("Group the AI field into teams of this size (assigned in spawn order). 0 = no teams.")]
    public int teamSize = 2;
    [Tooltip("How many spawned AI join the PLAYER'S team (team 0). TeamSwitchController offers mid-race control of these cars.")]
    public int playerTeammates = 2;
    [Tooltip("Display name of the player's team.")]
    public string playerTeamName = "Your Team";

    [Header("Engine Audio")]
    [Tooltip("Shared engine sound set applied to every spawned AI car (3D/positional). Leave null for silent AI.")]
    public EngineSoundSet aiEngineSound;
    [Tooltip("Max audible distance (m) for an AI car's engine. Beyond this it attenuates to silence.")]
    public float aiEngineMaxDistance = 120f;
    [Tooltip("Overall AI engine volume. Keep below the player's so the field doesn't drown the player's own car.")]
    [Range(0f, 1f)] public float aiEngineVolume = 0.7f;

    [Header("Collision")]
    [Tooltip("Add VehicleCollision to each spawned car for barrier + car-car contact.")]
    public bool addCollision = true;
    [Tooltip("Box collider half-extents (m). x = half-width, y = half-length.")]
    public Vector2 collisionHalfExtents = new Vector2(1.0f, 2.4f);
    [Tooltip("Layers cars collide against (barriers + other vehicles).")]
    public LayerMask collisionMask = ~0;

    [Header("Dynamic AI Physics")]
    [Tooltip("Drive AI with the player's full dynamic model (tyre model, damage denting, feel) via PlayerVehicleController + SplineInputDriver. Off = legacy kinematic SplineDriver motion (safe fallback).")]
    public bool dynamicAI = true;
    [Tooltip("Multiplayer only: drive the networked AI field with the cheap kinematic SplineDriver instead of the full dynamic model. Big host-CPU saving so a large field (e.g. 43) stays smooth over the network. Single-player AI follow 'dynamicAI' above.")]
    public bool multiplayerKinematicAI = true;
    [Tooltip("AI pure-pursuit steering gain (SplineInputDriver.steerGain).")]
    public float aiSteerGain = 1.5f;
    [Tooltip("AI speed-tracking gain — throttle/brake per m/s of speed error (SplineInputDriver.speedGain). Low values make the AI cruise under their target speed (throttle sags near zero error).")]
    public float aiSpeedGain = 2f;
    [Tooltip("Scales the AI's corner target speeds (SplineDriver.cornerSpeedScale). Targets are computed from the driven line's real curvature and the physics' own grip, so 1.0 = theoretical limit; keep slightly below 1 for margin. >1 commands past the grip ceiling and strews understeering cars across the track.")]
    public float aiCornerSpeedScale = 0.95f;

    [Header("Another championship's session")]
    [Tooltip("Field size for a session the player is not entered in — the trucks practising while the player is doing sponsor duties. Well below the race field on purpose: these are traffic seen from the paddock, not opponents, and they run the cheap kinematic brain. 0 = use the full field size above.")]
    public int ambientCount = 16;
    [Tooltip("Carset prefix used when the Truck Series has the circuit. Falls back to carsetPrefix if it loads no liveries.")]
    public string trucksCarsetPrefix = "cts25";
    [Tooltip("Carset prefix used when the National Stock Series has the circuit.")]
    public string nationalCarsetPrefix = "xfi25";
    [Tooltip("Carset prefix used when the Premier Cup Series has the circuit.")]
    public string cupCarsetPrefix = "cup26";

    // The other championship's cars, while they are out. Null the rest of the weekend.
    Transform _ambientField;
    string _ambientFor = "";
    // The player's own session spawned its field: this spawner is done, and the weekend clock must never
    // reach in and clear a grid the player is sat on.
    bool _playerField;
    bool _followingTheClock;

    // Subscribed from Start rather than OnEnable, and only on the walk-around side: reading the weekend's
    // sheet can itself settle the ledger on the first read of a fresh weekend, and a Changed callback
    // arriving before Start has decided what to put out would race it into spawning the field twice.
    void OnDestroy()
    {
        if (!_followingTheClock) return;
        WeekendLedger.Changed -= OnWeekendClockMoved;
        WeekendTrackState.HoldChanged -= OnWeekendClockMoved;
    }

    // The weekend clock only moves when the player finishes (or gives up) an hour, and moving it hands the
    // circuit to a different championship — or empties it. The field follows the sheet rather than the
    // scene load, so an obligation that runs to 10:00 ends with the trucks already going past.
    //
    // Also raised when a grandstand takes the circuit off the clock and holds it for as long as somebody is
    // sat watching (WeekendTrackState.Hold), which is the one case where who is out changes with the clock
    // standing still.
    void OnWeekendClockMoved()
    {
        if (_playerField || GameSession.IsMultiplayer) return;
        if (track == null || carPrefab == null || !isActiveAndEnabled) return;
        StartCoroutine(SyncAmbientField());
    }

    IEnumerator Start()
    {
        // Multiplayer: only the host spawns AI, as networked objects replicated to clients (SpawnNetworkedField).
        // Clients receive the field from the host and spawn nothing locally.
        if (GameSession.IsMultiplayer)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && networkedAiPrefab != null && track != null)
                yield return SpawnNetworkedField();
            yield break;
        }

        if (track == null || carPrefab == null) yield break;

        // No session, no field. The three championships share the circuit for three days, but their cars are
        // only on it during their own sessions; the rest of the weekend the track is empty and everything
        // that happens, happens in the paddock.
        var live = WeekendTrackState.Now();

        // Anything other than the player's own hour in the car is watched from outside it. Their cars run
        // whether the player is in the grandstand or under the hospitality awning, but there is no way into
        // one of them — so no grid, no formation lap, no session director, just a field circulating past the
        // paddock. From here on the sheet's clock, not the scene load, decides who is out.
        if (!live.any || !live.playerDriving)
        {
            _followingTheClock = true;
            WeekendLedger.Changed += OnWeekendClockMoved;
            WeekendTrackState.HoldChanged += OnWeekendClockMoved;
            if (live.any) yield return SpawnAmbientField(live);
            yield break;
        }

        _playerField = true;

        if (DatabaseManager.Instance == null)
        {
            Debug.LogWarning("GridSpawner: no DatabaseManager in scene — spawning without driver bindings.");
        }
        else
        {
            while (!DatabaseManager.Instance.IsReady) yield return null;
        }

        var drivers = DatabaseManager.Instance != null
            ? DatabaseManager.Instance.Connection.Table<Driver>().ToList()
            : new List<Driver>();

        var pool = new List<Driver>(drivers);
        Shuffle(pool);

        // Car number -> the driver who actually races it. The roster keys every driver to their real
        // number, which is also the livery sprite's number, so the timing tower matches the paintwork.
        var driversByNumber = new Dictionary<int, Driver>();
        foreach (var d in drivers)
            if (d.CarNumber > 0) driversByNumber[d.CarNumber] = d;

        var parent = new GameObject("AIField").transform;
        parent.SetParent(transform, false);

        // Livery pool: a distinct paint per car, loaded from the carset's Resources sprites.
        var liveries = new List<(int number, Sprite sprite)>();
        if (useCarsetLiveries && !string.IsNullOrEmpty(carsetPrefix))
        {
            for (int n = 0; n <= maxLiveryNumber; n++)
            {
                var spr = Resources.Load<Sprite>($"{carsetPrefix}livery{n}");
                if (spr != null) liveries.Add((n, spr));
            }
            Shuffle(liveries);
            if (liveries.Count == 0)
                Debug.LogWarning($"GridSpawner: no liveries found for '{carsetPrefix}' in Resources.");
        }

        // The player's own paint is out of the AI pool — the field can't contain a second #8 while the
        // player is racing it, and the player already IS that number's driver.
        int playerCarNumber = CarIdentity.NumberOf(CarIdentity.FindPlayerCar());
        if (playerCarNumber >= 0) liveries.RemoveAll(l => l.number == playerCarNumber);

        // Driver names from DriverNames (surname pool), instead of the database.
        List<string> namePool = null;
        if (useDriverNamePool)
        {
            DriverNames.loadData();
            namePool = new List<string>(DriverNames.driverPool);
            Shuffle(namePool);
        }

        // Wait a frame so PitLaneStart.Start has positioned the player car (its DB-less Start may run after ours).
        yield return null;

        // Practice/qualifying: same pit-box parking as a formation race, but no formation lap — cars
        // are held by PracticeAIStint and cycled out for lap stints by the PracticeDirector.
        bool practice = RaceWeekend.IsPracticeLike;
        if (practice) formationRace = false;
        bool pitStart = formationRace || practice;
        var director = practice ? PracticeDirector.Ensure() : null;
        if (!practice) RaceDirector.Ensure();   // race sessions get a finish + results screen

        // Race after qualifying: the captured grid fixes each car's identity and slot. GridOrder[g] is
        // grid slot g (pole = 0); the player's rank becomes their reserved pit box, and the AI keep the
        // name/livery they qualified with instead of re-shuffling on the reload.
        List<RaceWeekend.GridEntry> aiGrid = null;
        int playerRank = -1;
        if (!practice && RaceWeekend.GridOrder != null && RaceWeekend.GridOrder.Count > 0)
        {
            aiGrid = new List<RaceWeekend.GridEntry>();
            for (int g = 0; g < RaceWeekend.GridOrder.Count; g++)
            {
                var entry = RaceWeekend.GridOrder[g];
                if (entry.isPlayer) playerRank = g;
                else aiGrid.Add(entry);
            }
        }

        // Reserve the player's pit box so an AI isn't spawned on top of the player car (they'd collide forever).
        // The player is +1 on top of `count` AI, so the boxes are fit for that total and one box index is left empty.
        var pls = pitStart ? FindObjectOfType<PitLaneStart>() : null;
        bool reservePlayer = pls != null && pls.PlayerOnPit;
        int totalBoxes = count + (reservePlayer ? 1 : 0);

        // Pit start (formation race or practice): park the field in the pit lane and fit the boxes to
        // the available pit length.
        float pitBoxExitGap = 12f;
        float pitBoxSpacing = 12f;
        float pitLen = 0f;
        if (pitStart)
        {
            spawnInPit = true;
            // Park the field in the middle of the track's box lane when it has one, so the parked file,
            // the box props, and the player's marker all sit on the grey strip TrackBuilder generated.
            if (track.HasPitBoxLane) pitBoxParkLateral = track.PitBoxLaneCenterLateral;
            var pit = track.SamplePitCenterline();
            pitLen = pit.Count > 0 ? pit[pit.Count - 1].distance : 0f;
            // Fit every box inside the grey box-lane strip. PitLane.FitBoxes owns that maths so the
            // editor preview (Draftmaster/Debug/Show Pit Boxes) draws the ladder the cars really park on.
            var boxFit = PitLane.FitBoxes(track, pitLen, totalBoxes);
            pitBoxExitGap = boxFit.exitGap;
            pitBoxSpacing = boxFit.spacing;
            // Publish the box layout so every car (safety car, pit stops) agrees where the boxes are.
            if (pitLen > 0f) PitLane.Configure(pitBoxExitGap, pitBoxSpacing, totalBoxes, pitBoxParkLateral);
        }

        int reservedBox = -1;
        if (reservePlayer && pitLen > 0f)
        {
            reservedBox = Mathf.RoundToInt((pitLen - pitBoxExitGap - pls.PlayerPitDistance) / Mathf.Max(pitBoxSpacing, 0.01f));
            reservedBox = Mathf.Clamp(reservedBox, 0, totalBoxes - 1);
            // Qualified: the player's grid rank IS their box (box order = grid order); the snap below
            // physically parks the car there.
            if (playerRank >= 0) reservedBox = Mathf.Clamp(playerRank, 0, totalBoxes - 1);
            // Race with no qualifying rank for the player (weekend skipped straight to race, or the
            // player never made the timing rows): no time = no earned slot — start from the very back.
            // Without this the physical fallback above maps the default pit parking spot to the front.
            else if (!practice) reservedBox = totalBoxes - 1;
            // Tell the player car its reserved grid slot so the formation order holds the place open for it.
            if (pls.car != null) pls.car.SetFormationGrid(reservedBox);
            // Publish it so the player-facing pit systems (box marker, pit service) know which box is theirs.
            PitLane.SetPlayerBox(reservedBox);

            // Snap the parked player car into its box: single file on the pit centerline, in front of
            // its tyre/fuel props, exactly like the AI. PitLaneStart parked it off-lane at a fraction of
            // the pit length; the box grid only exists now that the boxes are fitted. The car is still
            // parked (PlayerVehicleController disabled — it captures heading from the transform when the
            // player climbs in), so a transform write is all it takes.
            if (pls.car != null)
            {
                var pitSamples = track.SamplePitCenterline();
                float boxD = PitLane.BoxDistance(reservedBox, pitLen);
                var bs = track.SamplePitAt(boxD, pitSamples);
                var carT = pls.car.transform;
                Vector2 boxPos = bs.position + bs.normal * pitBoxParkLateral;
                Vector3 wp = track.transform.TransformPoint(new Vector3(boxPos.x, boxPos.y, 0f));
                carT.position = new Vector3(wp.x, wp.y, carT.position.z);
                Vector3 wt = track.transform.TransformDirection(new Vector3(bs.tangent.x, bs.tangent.y, 0f));
                float headingDeg = Mathf.Atan2(wt.y, wt.x) * Mathf.Rad2Deg;
                carT.rotation = Quaternion.Euler(0f, 0f, headingDeg - ((pls.car.spriteFacesUp ? 90f : 0f) - pls.car.angleOffsetDeg));
            }
        }

        // Real-team grouping: cars share a teamId when they share a NASCAR team. The player's team is
        // playerTeamName when that names a real team, otherwise the team of the first car spawned —
        // either way TeamSwitchController still has a team 0 to offer control of.
        var realTeamIds = new Dictionary<string, int>();
        int nextRealTeamId = 1;
        string playerRealTeam = null;
        if (playerTeammates > 0)
        {
            // An explicit playerTeamName wins when it names a real team; otherwise the player's own livery
            // decides — driving the #8 puts them in that car's team, so their teammates are the real ones.
            if (!string.IsNullOrEmpty(playerTeamName))
                foreach (var d in drivers)
                    if (d.TeamName == playerTeamName) { playerRealTeam = d.TeamName; break; }

            if (playerRealTeam == null)
            {
                var playerDriver = RosterLookup.ByCarNumber(playerCarNumber);
                if (playerDriver != null) playerRealTeam = playerDriver.TeamName;
            }
        }

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(carPrefab, parent);
            go.name = $"AI_{i + 1:D2}";
            go.transform.localScale = new Vector3(carScale.x, carScale.y, 1f);

            // Identity: the qualifying grid pins name + livery to the slot; otherwise the shuffled pools.
            var gridEntry = (aiGrid != null && i < aiGrid.Count) ? aiGrid[i] : null;

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            int carNumber = i;
            Sprite liverySprite = null;
            if (gridEntry != null)
            {
                carNumber = gridEntry.carNumber;
                liverySprite = Resources.Load<Sprite>($"{carsetPrefix}livery{carNumber}");
            }
            if (liverySprite == null && liveries.Count > 0)
            {
                var liv = liveries[i % liveries.Count];
                carNumber = liv.number;
                liverySprite = liv.sprite;
            }
            if (sr != null)
            {
                if (carMaterial != null) sr.sharedMaterial = carMaterial;
                sr.sortingOrder = carSortingOrder;
                // Distinct livery before the VehicleDamage mesh is built from this sprite below.
                if (liverySprite != null) sr.sprite = liverySprite;
            }

            var splineDriver = go.GetComponent<SplineDriver>();
            if (splineDriver == null) splineDriver = go.AddComponent<SplineDriver>();
            splineDriver.track = track;
            splineDriver.vehicleInfo = vehicleInfo;
            splineDriver.cornerSpeedScale = aiCornerSpeedScale;
            float sfAnchor = (track != null && track.track != null) ? track.track.startFinishDistance : 0f;
            splineDriver.startDistance = sfAnchor + gridStartDistance - i * spacing;
            splineDriver.spawnInPit = spawnInPit;
            // Skip the player's reserved box when numbering AI grid slots.
            splineDriver.qualifyingPosition = (reservedBox >= 0 && i >= reservedBox) ? i + 1 : i;
            // Pit starts park single file on the wall-side box lane, in front of the box props. The
            // offset is cleared at the pit exit (SplineDriver) so it never leaks onto the track; the
            // grid stagger is only for fields lined up on the track. Formation columns come from
            // tacticalLateralOffset.
            splineDriver.lateralOffset = pitStart ? pitBoxParkLateral : ((i % 2 == 0) ? rowStagger * 0.5f : -rowStagger * 0.5f);
            splineDriver.speed = speed;
            splineDriver.spriteFacesUp = false;
            splineDriver.angleOffsetDeg = 180f;

            if (pitStart)
            {
                splineDriver.pitBoxExitGap = pitBoxExitGap;
                splineDriver.pitBoxSpacing = pitBoxSpacing;
            }
            if (formationRace)
            {
                splineDriver.freezeUntilFormation = true;
                var fc = go.GetComponent<FormationController>();
                if (fc == null) fc = go.AddComponent<FormationController>();
                fc.kinematic = !dynamicAI; // a kinematic SP field stays kinematic through green
            }
            if (practice)
            {
                // Pin in the box (the phase is Green, so freezeUntilFormation wouldn't hold);
                // the director releases cars for stints via PracticeAIStint.
                splineDriver.parkedHold = true;
                var stint = go.GetComponent<PracticeAIStint>();
                if (stint == null) stint = go.AddComponent<PracticeAIStint>();
                director.Register(stint);
            }

            // The car's number decides the driver: whoever races that number in the roster. Only cars
            // whose number nobody claims fall back to the shuffled pool.
            Driver rosterDriver = null;
            driversByNumber.TryGetValue(carNumber, out rosterDriver);

            var binding = go.GetComponent<AIDriverBinding>();
            if (binding == null) binding = go.AddComponent<AIDriverBinding>();
            binding.vehicleInfo = vehicleInfo;
            if (rosterDriver != null) binding.driver = rosterDriver;
            else if (pool.Count > 0) binding.driver = pool[i % pool.Count];
            binding.Apply();

            // Wear-based pit strategy (self-gates on green flag). Not in practice — the stint
            // controller owns all pit-lane decisions there.
            if (!practice && go.GetComponent<PitStopController>() == null) go.AddComponent<PitStopController>();
            // Fuel so the crew has something to refill (TireModel is auto-added by the dynamic model).
            if (go.GetComponent<FuelTank>() == null) go.AddComponent<FuelTank>();

            // Identity for the position counter / HUD: the roster driver who runs this number, falling
            // back to the qualifying grid's captured name and then the surname pool.
            var label = go.GetComponent<DriverLabel>();
            if (label == null) label = go.AddComponent<DriverLabel>();
            label.carset = carsetPrefix;
            label.carNumber = carNumber;
            if (rosterDriver != null)
                label.driverName = RosterLookup.LabelName(rosterDriver);
            else if (gridEntry != null && !string.IsNullOrEmpty(gridEntry.driverName))
                label.driverName = gridEntry.driverName;
            else
                label.driverName = (namePool != null && namePool.Count > 0) ? namePool[i % namePool.Count] : "";
            if (!string.IsNullOrEmpty(label.driverName)) go.name = $"AI_{label.driverName}_{carNumber}";

            // Teams: real NASCAR teams when the roster knows one, so teammates on track are actual
            // teammates. Without roster data, fall back to spawn-order pairing.
            string realTeam = rosterDriver != null ? rosterDriver.TeamName : null;
            if (!string.IsNullOrEmpty(realTeam))
            {
                if (playerRealTeam == null && playerTeammates > 0) playerRealTeam = realTeam;
                if (!realTeamIds.TryGetValue(realTeam, out int tid))
                {
                    tid = realTeam == playerRealTeam ? 0 : nextRealTeamId++;
                    realTeamIds[realTeam] = tid;
                }
                label.teamId = tid;
                label.teamName = realTeam;
            }
            else if (playerTeammates > 0 && i < playerTeammates)
            {
                label.teamId = 0;
                label.teamName = playerTeamName;
            }
            else if (teamSize > 0)
            {
                label.teamId = 1 + (i - Mathf.Max(playerTeammates, 0)) / teamSize;
                label.teamName = $"Team {label.teamId}";
            }

            if (dynamicAI)
            {
                // Hand motion to the player's dynamic model; SplineDriver stays the path/speed brain.
                splineDriver.externalMotionController = true;

                var pvc = go.GetComponent<PlayerVehicleController>();
                if (pvc == null) pvc = go.AddComponent<PlayerVehicleController>();
                pvc.vehicleInfo = vehicleInfo;
                pvc.track = track;
                pvc.spriteFacesUp = false;   // match the AI sprite convention used by SplineDriver
                pvc.angleOffsetDeg = 180f;
                pvc.grassTrails = false;     // perf: no trail renderers across a full AI field
                pvc.enableWheelspin = false; // AI launch cleanly; no donuts off the line
                pvc.externalInput = true;
                pvc.damageImpairsHandling = false; // damage only slows AI; never pulls them off the racing line

                var input = go.GetComponent<SplineInputDriver>();
                if (input == null) input = go.AddComponent<SplineInputDriver>();
                input.steerGain = aiSteerGain;
                input.speedGain = aiSpeedGain;

                // Deformable bodywork so AI dent like the player. A SpriteRenderer and the MeshRenderer that
                // VehicleDamage requires can't share a GameObject, so replace the sprite with the deformable mesh —
                // mirroring the player car (VehicleDamage + MeshRenderer, no SpriteRenderer). Added before
                // VehicleCollision, which caches the IDamageable in Awake.
                if (sr != null && sr.sprite != null && sr.GetComponent<VehicleDamage>() == null)
                {
                    var srGo = sr.gameObject;
                    var spr = sr.sprite;
                    DestroyImmediate(sr);
                    var dmg = srGo.AddComponent<VehicleDamage>();
                    dmg.sourceSprite = spr;
                    dmg.material = null;          // auto-build an unlit per-car material from the sprite
                    dmg.sortingLayer = "Default";
                    dmg.sortingOrder = carSortingOrder;
                    dmg.Build();
                }
            }

            if (aiEngineSound != null)
            {
                // Gearbox derives RPM/gear from the car's speed; EngineAudio voices it positionally (3D).
                if (go.GetComponent<EngineGearbox>() == null) go.AddComponent<EngineGearbox>();
                var ea = go.GetComponent<EngineAudio>();
                if (ea == null) ea = go.AddComponent<EngineAudio>();
                ea.soundSet = aiEngineSound;
                ea.spatialBlend = 1f;
                ea.masterVolume = aiEngineVolume;
                ea.maxDistance = aiEngineMaxDistance;
            }

            if (addCollision)
            {
                var col = go.GetComponent<VehicleCollision>();
                if (col == null) col = go.AddComponent<VehicleCollision>();
                col.halfExtents = collisionHalfExtents;
                col.collisionMask = collisionMask;
                col.ApplyExtents();
            }

            // Practice: physically place the car in its box now. The PreGrid freeze that pins a
            // formation field never runs here (the phase is already Green), so without this a
            // dynamic-AI car's motion model can latch a stale origin pose on its first physics step.
            if (practice) splineDriver.PlaceAtStartDistance();
        }
    }

    // Host-only multiplayer field spawn. Mirrors the single-player grid setup but instantiates the networked
    // AI prefab, seeds each car's identity, and NetworkObject.Spawn()s it so clients get a replica. The host
    // runs the brains; NetworkedAICar disables them on clients. The field runs a formation lap behind the
    // safety car (same as SP): the FormationDirector stays enabled and each host AI gets a FormationController.
    IEnumerator SpawnNetworkedField()
    {
        // Hold the field on the grid until the multiplayer lobby gate passes (2+ players, all ready).
        // NetworkedCarBindings (host) then runs PreGrid → Formation (pace lap) → Green. Leave the
        // FormationDirector enabled so it spawns the safety car and leads the lap, exactly as in single player.
        RaceStart.Current = RaceStart.Phase.PreGrid;

        if (DatabaseManager.Instance != null)
            while (!DatabaseManager.Instance.IsReady) yield return null;

        var drivers = DatabaseManager.Instance != null
            ? DatabaseManager.Instance.Connection.Table<Driver>().ToList()
            : new List<Driver>();
        var pool = new List<Driver>(drivers);
        Shuffle(pool);

        var driversByNumber = new Dictionary<int, Driver>();
        foreach (var d in drivers)
            if (d.CarNumber > 0) driversByNumber[d.CarNumber] = d;

        var liveries = new List<(int number, Sprite sprite)>();
        if (useCarsetLiveries && !string.IsNullOrEmpty(carsetPrefix))
        {
            for (int n = 0; n <= maxLiveryNumber; n++)
            {
                var spr = Resources.Load<Sprite>($"{carsetPrefix}livery{n}");
                if (spr != null) liveries.Add((n, spr));
            }
            Shuffle(liveries);
        }

        List<string> namePool = null;
        if (useDriverNamePool)
        {
            DriverNames.loadData();
            namePool = new List<string>(DriverNames.driverPool);
            Shuffle(namePool);
        }

        float sfAnchor = (track.track != null) ? track.track.startFinishDistance : 0f;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(networkedAiPrefab);
            go.transform.localScale = new Vector3(carScale.x, carScale.y, 1f);

            int carNumber = (liveries.Count > 0) ? liveries[i % liveries.Count].number : i;
            // Same pairing as single player: the number on the car picks the driver who runs it.
            driversByNumber.TryGetValue(carNumber, out Driver rosterDriver);
            string driverName = rosterDriver != null
                ? (!string.IsNullOrEmpty(rosterDriver.ShortName) ? rosterDriver.ShortName : rosterDriver.LastName)
                : ((namePool != null && namePool.Count > 0) ? namePool[i % namePool.Count] : "");

            var splineDriver = go.GetComponent<SplineDriver>();
            if (splineDriver != null)
            {
                splineDriver.track = track;
                splineDriver.vehicleInfo = vehicleInfo;
                splineDriver.cornerSpeedScale = aiCornerSpeedScale;
                splineDriver.startDistance = sfAnchor + gridStartDistance - i * spacing;
                splineDriver.spawnInPit = false;
                splineDriver.qualifyingPosition = i;
                splineDriver.lateralOffset = (i % 2 == 0) ? rowStagger * 0.5f : -rowStagger * 0.5f;
                splineDriver.speed = speed;
                splineDriver.spriteFacesUp = false;
                splineDriver.angleOffsetDeg = 180f;
                splineDriver.freezeUntilFormation = true; // hold on the grid through the lobby; released when the formation lap starts
                splineDriver.externalMotionController = !multiplayerKinematicAI; // kinematic: SplineDriver writes the transform itself
            }

            // Pace lap: file in behind the safety car during Formation (dormant in every other phase). Host-only
            // brain — on clients the car is a NetworkTransform puppet and NetworkedAICar disables this there.
            var fc = go.GetComponent<FormationController>();
            if (fc == null) fc = go.AddComponent<FormationController>();
            fc.kinematic = multiplayerKinematicAI; // a kinematic field stays kinematic through green

            var input = go.GetComponent<SplineInputDriver>();
            if (input != null) { input.steerGain = aiSteerGain; input.speedGain = aiSpeedGain; }

            var pvc = go.GetComponent<PlayerVehicleController>();
            if (pvc != null)
            {
                pvc.vehicleInfo = vehicleInfo;
                pvc.track = track;
                pvc.spriteFacesUp = false;
                pvc.angleOffsetDeg = 180f;
                pvc.grassTrails = false;
                pvc.enableWheelspin = false;
                pvc.externalInput = true;
                pvc.damageImpairsHandling = false; // damage only slows AI; never pulls them off the racing line
            }

            var binding = go.GetComponent<AIDriverBinding>();
            if (binding != null)
            {
                binding.vehicleInfo = vehicleInfo;
                if (rosterDriver != null) binding.driver = rosterDriver;
                else if (pool.Count > 0) binding.driver = pool[i % pool.Count];
            }

            // Configure the brain and place the car on its start spot BEFORE Spawn, so NetworkTransform
            // captures a valid on-track pose. (The AI input driver's first physics step runs before Start
            // and would otherwise latch the car at the origin, piling the whole field up at (0,0).)
            if (binding != null) binding.Apply();              // adds AIRacingBehaviour/TireState, rebuilds spline
            if (splineDriver != null) splineDriver.PlaceAtStartDistance();

            // Seed identity before Spawn; the server copies it into the synced NetworkVariables on spawn.
            var net = go.GetComponent<NetworkedAICar>();
            if (net != null)
            {
                net.carsetSeed = carsetPrefix;
                net.carNumberSeed = carNumber;
                net.driverNameSeed = driverName;
                net.kinematicSeed = multiplayerKinematicAI;
            }

            // NetworkObjects stay at the scene root — NGO forbids parenting them under a plain GameObject.
            go.GetComponent<NetworkObject>().Spawn();
        }
    }

    // ------------------------------------------------------------------ somebody else's session

    // The clock has moved. Put the right championship on track, or take the circuit back.
    IEnumerator SyncAmbientField()
    {
        var live = WeekendTrackState.Now();

        // The player's own session is a scene load away and brings its own field; nothing to do from here.
        if (live.any && live.playerDriving) yield break;

        string want = live.any ? live.activityId : "";
        if (want == _ambientFor) yield break;

        // Claimed here rather than inside the spawn, and before the first yield: StartCoroutine runs this
        // far synchronously, so a second Changed in the same frame sees the booking already taken.
        ClearAmbientField();
        _ambientFor = want;
        if (live.any) yield return SpawnAmbientField(live);
    }

    void ClearAmbientField()
    {
        if (_ambientField != null) Destroy(_ambientField.gameObject);
        _ambientField = null;
        _ambientFor = "";
        PitLane.Clear();   // their boxes go with them
    }

    // Another championship's practice, qualifying or race, watched from outside it.
    //
    // Deliberately far less machinery than the player's own session: no reserved slot, no formation lap, no
    // directors and no results, because none of that is the player's to take part in. A field spread around
    // the lap, circulating, on the cheap kinematic brain — the player is a couple of hundred metres away on
    // foot and these are traffic.
    //
    // The pit boxes ARE laid out, though, and to this field. A session running is a pit road with that
    // championship's crews stood in it, one box per car; a Truck practice with the Cup boxes still painted
    // for a field that is not out, or with no boxes at all, is the tell that nothing here is really
    // happening. Box index is the car's slot in this field, which is what PitBoxCars matches on, so every
    // crew ends up in the colours of the car that pits in front of them.
    IEnumerator SpawnAmbientField(WeekendTrackState.Live live)
    {
        _ambientFor = live.activityId;

        // No geometry, no field. The race scene holds no road — TrackSceneLoader binds the selected
        // package's TrackBuilder — so this is null until the track is in. Unclaim the booking on the way
        // out so the next tick of the clock tries again rather than believing this field is already up.
        if (track == null || track.track == null || carPrefab == null) { _ambientFor = ""; yield break; }

        if (DatabaseManager.Instance != null)
            while (!DatabaseManager.Instance.IsReady) yield return null;

        // The clock can have moved on while the database opened.
        if (_ambientFor != live.activityId) yield break;

        // The session is under way — there is no starter and no grid, so the field is racing from the
        // moment it appears. (FormationDirector stands itself down outside the player's own session, so
        // nothing else is going to write this.)
        RaceStart.Current = RaceStart.Phase.Green;

        string carset = CarsetFor(live.series);
        var liveries = LoadLiveries(carset);
        if (liveries.Count == 0 && carset != carsetPrefix)
        {
            carset = carsetPrefix;
            liveries = LoadLiveries(carset);
        }

        // The player's own paint stays out of it. When the championship running is theirs — a practice they
        // never booked, going ahead without them — the point is that they are the one car missing from it,
        // not that a second #8 goes past the fence while theirs is parked in the box.
        if (live.series == SeriesCatalog.PlayerSeries)
        {
            int playerCarNumber = CarIdentity.NumberOf(CarIdentity.FindPlayerCar());
            if (playerCarNumber >= 0) liveries.RemoveAll(l => l.number == playerCarNumber);
        }

        var drivers = DatabaseManager.Instance != null
            ? DatabaseManager.Instance.Connection.Table<Driver>().ToList()
            : new List<Driver>();
        var pool = new List<Driver>(drivers);
        Shuffle(pool);

        var driversByNumber = new Dictionary<int, Driver>();
        foreach (var d in drivers)
            if (d.CarNumber > 0) driversByNumber[d.CarNumber] = d;

        List<string> namePool = null;
        if (useDriverNamePool)
        {
            DriverNames.loadData();
            namePool = new List<string>(DriverNames.driverPool);
            Shuffle(namePool);
        }

        string code = SeriesCatalog.ShortCode(live.series);
        var parent = new GameObject($"AmbientField_{code}").transform;
        parent.SetParent(transform, false);
        _ambientField = parent;

        int n = Mathf.Max(1, ambientCount > 0 ? ambientCount : count);
        LayOutBoxesFor(n);
        // Off the sampled centerline, not the sum of the segment lengths: this is the length SplineDriver
        // itself measures, so a start distance wrapped against it lands where the car expects to be.
        var centerline = track.SampleCenterline();
        float lapLength = centerline.Count > 0 ? centerline[centerline.Count - 1].distance : 0f;
        float sfAnchor = track.track != null ? track.track.startFinishDistance : 0f;
        // Spread around the whole lap. A session already running has nobody on a grid, and an even spread
        // is also what stops a field with no starter arriving at turn one as a single heap.
        float gap = lapLength > 0f ? lapLength / n : Mathf.Max(spacing, 1f);

        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(carPrefab, parent);

            int carNumber = i;
            Sprite livery = null;
            if (liveries.Count > 0)
            {
                var liv = liveries[i % liveries.Count];
                carNumber = liv.number;
                livery = liv.sprite;
            }

            go.transform.localScale = new Vector3(carScale.x, carScale.y, 1f);

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (carMaterial != null) sr.sharedMaterial = carMaterial;
                sr.sortingOrder = carSortingOrder;
                if (livery != null) sr.sprite = livery;
            }

            // Kinematic: SplineDriver writes the transform itself. The full dynamic model is what the
            // player's own field costs, and a session they are only listening to must not cost that.
            DisableIfPresent<PlayerVehicleController>(go);
            DisableIfPresent<SplineInputDriver>(go);

            var splineDriver = go.GetComponent<SplineDriver>();
            if (splineDriver == null) splineDriver = go.AddComponent<SplineDriver>();
            splineDriver.track = track;
            splineDriver.vehicleInfo = vehicleInfo;
            splineDriver.cornerSpeedScale = aiCornerSpeedScale;
            splineDriver.externalMotionController = false;
            splineDriver.loop = true;
            splineDriver.spawnInPit = false;
            splineDriver.startDistance = Wrap(sfAnchor + i * gap, lapLength);
            splineDriver.qualifyingPosition = i;
            splineDriver.lateralOffset = 0f;
            splineDriver.speed = speed;
            splineDriver.spriteFacesUp = false;
            splineDriver.angleOffsetDeg = 180f;

            driversByNumber.TryGetValue(carNumber, out Driver rosterDriver);

            // Identity for the timing tower. teamId is left unaffiliated (-1) on purpose: these are not
            // the player's teammates and TeamSwitchController must never offer one of them.
            var label = go.GetComponent<DriverLabel>();
            if (label == null) label = go.AddComponent<DriverLabel>();
            label.carset = carset;
            label.carNumber = carNumber;
            label.driverName = rosterDriver != null
                ? RosterLookup.LabelName(rosterDriver)
                : (namePool != null && namePool.Count > 0 ? namePool[i % namePool.Count] : "");
            go.name = string.IsNullOrEmpty(label.driverName)
                ? $"{code}_{i + 1:D2}"
                : $"{code}_{label.driverName}_{carNumber}";

            var binding = go.GetComponent<AIDriverBinding>();
            if (binding == null) binding = go.AddComponent<AIDriverBinding>();
            binding.vehicleInfo = vehicleInfo;
            if (rosterDriver != null) binding.driver = rosterDriver;
            else if (pool.Count > 0) binding.driver = pool[i % pool.Count];
            binding.Apply();   // adds AIRacingBehaviour/TireState, rebuilds the spline

            if (aiEngineSound != null)
            {
                if (go.GetComponent<EngineGearbox>() == null) go.AddComponent<EngineGearbox>();
                var ea = go.GetComponent<EngineAudio>();
                if (ea == null) ea = go.AddComponent<EngineAudio>();
                ea.soundSet = aiEngineSound;
                ea.spatialBlend = 1f;
                ea.masterVolume = aiEngineVolume;
                ea.maxDistance = aiEngineMaxDistance;
            }

            if (addCollision)
            {
                var col = go.GetComponent<VehicleCollision>();
                if (col == null) col = go.AddComponent<VehicleCollision>();
                col.halfExtents = collisionHalfExtents;
                col.collisionMask = collisionMask;
                col.ApplyExtents();
            }

            // Place the car on its spot now: a dynamic model is not driving this one, but the same
            // first-physics-step latch that piles a spawned field up at the origin applies to the
            // kinematic brain too.
            splineDriver.PlaceAtStartDistance();
        }
    }

    // Pit road, set up for whichever championship is out. Same fit the player's own field gets — the boxes
    // land on the grey strip TrackBuilder drew — but no box is reserved, because the player is not in this
    // session and a box painted as theirs would be a lie they can walk up to.
    void LayOutBoxesFor(int cars)
    {
        if (track == null || track.track == null || !track.track.hasPitLane) return;

        var pit = track.SamplePitCenterline();
        float pitLength = pit.Count > 0 ? pit[pit.Count - 1].distance : 0f;
        if (pitLength <= 0f) return;

        float parkLateral = track.HasPitBoxLane ? track.PitBoxLaneCenterLateral : 0f;
        var fit = PitLane.FitBoxes(track, pitLength, cars);
        PitLane.Configure(fit.exitGap, fit.spacing, cars, parkLateral);
    }

    // The paint a championship turns up in. An empty prefix (or a carset with no liveries in Resources)
    // falls back to the spawner's own, so a missing carset costs the right liveries, not the session.
    string CarsetFor(RacingSeries s)
    {
        string prefix = s switch
        {
            RacingSeries.Trucks => trucksCarsetPrefix,
            RacingSeries.National => nationalCarsetPrefix,
            _ => cupCarsetPrefix,
        };
        return string.IsNullOrEmpty(prefix) ? carsetPrefix : prefix;
    }

    List<(int number, Sprite sprite)> LoadLiveries(string prefix)
    {
        var liveries = new List<(int number, Sprite sprite)>();
        if (!useCarsetLiveries || string.IsNullOrEmpty(prefix)) return liveries;

        for (int n = 0; n <= maxLiveryNumber; n++)
        {
            var spr = Resources.Load<Sprite>($"{prefix}livery{n}");
            if (spr != null) liveries.Add((n, spr));
        }
        Shuffle(liveries);
        return liveries;
    }

    // Keep a start distance inside one lap. SplineDriver takes startDistance as written, so a field spread
    // from the start-finish anchor has to come back round rather than run off the end of the spline.
    static float Wrap(float distance, float lapLength) =>
        lapLength > 0f ? ((distance % lapLength) + lapLength) % lapLength : Mathf.Max(0f, distance);

    static void DisableIfPresent<T>(GameObject go) where T : MonoBehaviour
    {
        var c = go.GetComponent<T>();
        if (c != null) c.enabled = false;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
