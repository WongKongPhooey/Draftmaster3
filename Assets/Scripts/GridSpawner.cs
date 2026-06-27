using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Draftmaster.Data;
using Unity.Netcode;
using UnityEngine;

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
    [Tooltip("Name drivers from the DriverNames pool instead of the SQLite database.")]
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
    [Tooltip("Formation-lap race: AI start frozen in pit boxes and form a train behind the safety car until green. Forces pit spawn, auto-fits pit-box spacing, and adds a FormationController to each car. Needs a FormationDirector in the scene.")]
    public bool formationRace = true;
    [Tooltip("Fallback car speed (m/s) when no VehicleInfo is assigned. Otherwise speed is driven by the vehicle's accel/decel curves.")]
    public float speed = 35f;
    [Tooltip("VehicleInfo asset applied to every spawned AI car. Defines accel/decel/cornering curves.")]
    public VehicleInfo vehicleInfo;
    [Tooltip("Scale to apply to spawned cars.")]
    public Vector2 carScale = new Vector2(6, 6);

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
    [Tooltip("AI speed-tracking gain — throttle/brake per m/s of speed error (SplineInputDriver.speedGain).")]
    public float aiSpeedGain = 0.5f;
    [Tooltip("Scales the AI's corner target speeds (SplineDriver.cornerSpeedScale). <1 gives a little grip/braking margin. Tight-turn line-following is handled by the steering lookahead, so this can stay near 1.")]
    public float aiCornerSpeedScale = 0.9f;

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

        // Reserve the player's pit box so an AI isn't spawned on top of the player car (they'd collide forever).
        // The player is +1 on top of `count` AI, so the boxes are fit for that total and one box index is left empty.
        var pls = formationRace ? FindObjectOfType<PitLaneStart>() : null;
        bool reservePlayer = pls != null && pls.PlayerOnPit;
        int totalBoxes = count + (reservePlayer ? 1 : 0);

        // Formation race: park the field in the pit lane and fit the boxes to the available pit length.
        float pitBoxExitGap = 12f;
        float pitBoxSpacing = 12f;
        float pitLen = 0f;
        if (formationRace)
        {
            spawnInPit = true;
            var pit = track.SamplePitCenterline();
            pitLen = pit.Count > 0 ? pit[pit.Count - 1].distance : 0f;
            if (pitLen > 0f && totalBoxes > 1)
            {
                float usable = Mathf.Max(0f, pitLen - pitBoxExitGap - 6f);
                pitBoxSpacing = Mathf.Clamp(usable / (totalBoxes - 1), 5f, 12f);
            }
            // Publish the box layout so every car (safety car, pit stops) agrees where the boxes are.
            if (pitLen > 0f) PitLane.Configure(pitBoxExitGap, pitBoxSpacing, totalBoxes);
        }

        int reservedBox = -1;
        if (reservePlayer && pitLen > 0f)
        {
            reservedBox = Mathf.RoundToInt((pitLen - pitBoxExitGap - pls.PlayerPitDistance) / Mathf.Max(pitBoxSpacing, 0.01f));
            reservedBox = Mathf.Clamp(reservedBox, 0, totalBoxes - 1);
            // Tell the player car its reserved grid slot so the formation order holds the place open for it.
            if (pls.car != null) pls.car.SetFormationGrid(reservedBox);
        }

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(carPrefab, parent);
            go.name = $"AI_{i + 1:D2}";
            go.transform.localScale = new Vector3(carScale.x, carScale.y, 1f);

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            int carNumber = i;
            if (sr != null)
            {
                if (carMaterial != null) sr.sharedMaterial = carMaterial;
                sr.sortingOrder = carSortingOrder;
                // Distinct livery before the VehicleDamage mesh is built from this sprite below.
                if (liveries.Count > 0)
                {
                    var liv = liveries[i % liveries.Count];
                    carNumber = liv.number;
                    sr.sprite = liv.sprite;
                }
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
            splineDriver.lateralOffset = (i % 2 == 0) ? rowStagger * 0.5f : -rowStagger * 0.5f;
            splineDriver.speed = speed;
            splineDriver.spriteFacesUp = false;
            splineDriver.angleOffsetDeg = 180f;

            if (formationRace)
            {
                splineDriver.pitBoxExitGap = pitBoxExitGap;
                splineDriver.pitBoxSpacing = pitBoxSpacing;
                splineDriver.freezeUntilFormation = true;
                var fc = go.GetComponent<FormationController>();
                if (fc == null) fc = go.AddComponent<FormationController>();
                fc.kinematic = !dynamicAI; // a kinematic SP field stays kinematic through green
            }

            var binding = go.GetComponent<AIDriverBinding>();
            if (binding == null) binding = go.AddComponent<AIDriverBinding>();
            binding.vehicleInfo = vehicleInfo;
            if (pool.Count > 0) binding.driver = pool[i % pool.Count];
            binding.Apply();

            // Wear-based pit strategy (self-gates on green flag).
            if (go.GetComponent<PitStopController>() == null) go.AddComponent<PitStopController>();
            // Fuel so the crew has something to refill (TireModel is auto-added by the dynamic model).
            if (go.GetComponent<FuelTank>() == null) go.AddComponent<FuelTank>();

            // Identity for the position counter / HUD: name from DriverNames, number from the livery.
            var label = go.GetComponent<DriverLabel>();
            if (label == null) label = go.AddComponent<DriverLabel>();
            label.carset = carsetPrefix;
            label.carNumber = carNumber;
            label.driverName = (namePool != null && namePool.Count > 0) ? namePool[i % namePool.Count] : "";
            if (!string.IsNullOrEmpty(label.driverName)) go.name = $"AI_{label.driverName}_{carNumber}";

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
            string driverName = (namePool != null && namePool.Count > 0) ? namePool[i % namePool.Count] : "";

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
                if (pool.Count > 0) binding.driver = pool[i % pool.Count];
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

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
