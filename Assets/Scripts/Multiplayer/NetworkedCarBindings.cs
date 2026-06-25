using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Wires a spawned networked car's scene-dependent refs (the track) that a prefab can't serialize, and —
// for the owning client — drops the car onto the start grid and frames the camera for driving.
//
// Also owns the multiplayer lobby + identity for a PLAYER car:
//   • Ready     — owner-writable flag toggled from the lobby panel (MultiplayerLobbyUI).
//   • CarNumber — server-assigned, distinct per player, picks a Resources/cup26liveryN paint so players
//                 don't all look identical (mirrors NetworkedAICar).
//   • Start gate — once 2+ players are connected and all ready, the host runs PreGrid→Formation (pace lap
//                  behind the safety car), then Formation→Green when the safety car pits. Each transition is
//                  replicated to every client (SetPhaseClientRpc) since RaceStart is a per-process static.
//   • Pre-race hold — the owner's car is speed-governed: parked (PreGrid), cruise (Formation), free (Green).
//
// The host's player car spawns the instant StartHost runs, which is while the MENU scene is still active
// (before the race scene loads). At that point there's no track and no race camera, so setup is deferred:
// TrySetup() no-ops until a TrackBuilder exists, retrying on sceneLoaded. Clients spawn after they've been
// synced into the race scene, so their setup succeeds immediately. Brain gating lives in NetworkCarOwnerGate.
[RequireComponent(typeof(NetworkObject))]
public class NetworkedCarBindings : NetworkBehaviour
{
    [Tooltip("Spacing between grid slots along the track (m).")]
    public float gridSpacing = 8f;
    [Tooltip("Lateral stagger between alternating slots (m). 0 = single file.")]
    public float gridStagger = 3.5f;
    [Tooltip("Lateral offset off the centerline for the whole grid (m).")]
    public float gridLateral = 0f;
    [Tooltip("Orthographic camera size while driving (matches the single-player driving zoom).")]
    public float drivingOrthoSize = 20f;
    [Tooltip("Carset prefix for the player's livery, e.g. cup26 → Resources/cup26liveryN.")]
    public string carset = "cup26";

    // Live roster of connected player cars — the lobby panel and the start gate read this.
    static readonly List<NetworkedCarBindings> _players = new();
    public static IReadOnlyList<NetworkedCarBindings> Players => _players;
    public static int ReadyCount
    {
        get { int c = 0; for (int i = 0; i < _players.Count; i++) if (_players[i] != null && _players[i].Ready.Value) c++; return c; }
    }

    // Owner toggles their own ready; server assigns the distinct livery number + display name.
    public NetworkVariable<bool> Ready = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> CarNumber = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> DisplayName = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsReadyFlag => Ready.Value;
    public string DisplayLabel => DisplayName.Value.ToString();
    public bool IsLocalPlayer => IsOwner;
    public int Number => CarNumber.Value;

    // Distinct livery numbers, drawn from the carset's Resources pool (server-side only).
    static List<int> _liveryPool;
    static int _liveryCursor;

    bool _setupDone;
    PlayerVehicleController _pvc;
    static bool _greenBroadcast; // host: ensures the Formation→Green RPC fires exactly once per race

    public override void OnNetworkSpawn()
    {
        _players.Add(this);
        CarNumber.OnValueChanged += (_, __) => ApplyLivery();

        if (IsServer)
        {
            CarNumber.Value = NextLiveryNumber(carset);
            DisplayName.Value = OwnerClientId == NetworkManager.ServerClientId ? "Host" : $"Player {OwnerClientId}";
        }

        ApplyLivery();

        if (!TrySetup())
            SceneManager.sceneLoaded += OnSceneLoaded; // host car: spawned before the race scene existed
    }

    public override void OnNetworkDespawn()
    {
        _players.Remove(this);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Owner-only: flip our own ready flag (called by the lobby UI).
    public void ToggleReadyLocal()
    {
        if (IsOwner) Ready.Value = !Ready.Value;
    }

    void Update()
    {
        // Owner: parked on the grid (PreGrid) or free once Green. The Formation lap is governed by PaceLapAssist
        // instead (free to drive, but speed-capped so the player can't overtake the car directly ahead).
        if (IsOwner && _pvc != null && RaceStart.Current != RaceStart.Phase.Formation)
            _pvc.speedGovernorMps = RaceStart.IsGreen ? Mathf.Infinity : 0f;

        if (!IsServer) return;

        // RaceStart is a per-process static, so the host flips it locally AND replicates each transition to
        // every client (SetPhaseClientRpc). The first binding to pass a gate this frame flips it; the rest
        // see the new phase and skip, so each RPC fires once.
        if (RaceStart.Current == RaceStart.Phase.PreGrid && AllReadyToStart())
        {
            // Lobby satisfied (2+ players, all ready) → roll out for the formation lap behind the safety car.
            RaceStart.Current = RaceStart.Phase.Formation;
            _greenBroadcast = false;        // re-arm for THIS race: the static persists across races in one process
            BeginFormationClientRpc();
        }
        else if (RaceStart.Current == RaceStart.Phase.Green && !_greenBroadcast)
        {
            // The host's safety car has pitted and FormationDirector flipped the race green → tell the field.
            _greenBroadcast = true;
            GoGreenClientRpc();
        }
    }

    // Parameterless RPCs — a plain enum param can fail to round-trip; the original working start gate used these.
    // Each client mirrors the host's phase so its OWN car's governor lifts from the 60 mph pace cap to free
    // running the instant the race goes green.
    [ClientRpc]
    void BeginFormationClientRpc()
    {
        RaceStart.Current = RaceStart.Phase.Formation;
    }

    [ClientRpc]
    void GoGreenClientRpc()
    {
        RaceStart.Current = RaceStart.Phase.Green;
    }

    static bool AllReadyToStart()
    {
        if (_players.Count < 2) return false;
        for (int i = 0; i < _players.Count; i++)
            if (_players[i] == null || !_players[i].Ready.Value) return false;
        return true;
    }

    // Paint the car from the synced livery number so every peer renders it identically. Rebuilds the deformable
    // bodywork mesh from the sprite (mirrors NetworkedAICar). Safe to call repeatedly.
    void ApplyLivery()
    {
        if (string.IsNullOrEmpty(carset)) return;
        var sprite = Resources.Load<Sprite>($"{carset}livery{CarNumber.Value}");
        if (sprite == null) return;

        var dmg = GetComponentInChildren<VehicleDamage>();
        // Force a fresh per-car material from THIS livery — Build() keeps an already-assigned material, so a
        // shared prefab material would make every player's car look identical.
        if (dmg != null) { dmg.sourceSprite = sprite; dmg.material = null; dmg.Build(); }
        else
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = sprite;
        }
    }

    static int NextLiveryNumber(string carset)
    {
        if (_liveryPool == null)
        {
            _liveryPool = new List<int>();
            for (int n = 0; n <= 99; n++)
                if (Resources.Load<Sprite>($"{carset}livery{n}") != null) _liveryPool.Add(n);
            for (int i = _liveryPool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_liveryPool[i], _liveryPool[j]) = (_liveryPool[j], _liveryPool[i]);
            }
        }
        if (_liveryPool.Count == 0) return 0;
        int num = _liveryPool[_liveryCursor % _liveryPool.Count];
        _liveryCursor++;
        return num;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (TrySetup()) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // True once the track is present and the car has been wired (and, for the owner, placed + camera framed).
    bool TrySetup()
    {
        if (_setupDone) return true;
        var track = FindFirstObjectByType<TrackBuilder>();
        if (track == null) return false;

        _pvc = GetComponent<PlayerVehicleController>();
        var spline = GetComponent<SplineDriver>();
        if (_pvc != null) _pvc.track = track;
        if (spline != null) spline.track = track;

        // Clients never run GridSpawner (host-only), so they don't set the pre-race hold themselves. Drop into
        // the lobby hold the moment we're synced into the race scene; the host's StartRaceClientRpc lifts it to
        // Green. (Late-joining after green isn't supported — clients are expected to join during the lobby.)
        if (!IsServer) RaceStart.Current = RaceStart.Phase.PreGrid;

        if (IsOwner)
        {
            PlaceOnGrid(track, (int)OwnerClientId, _pvc);
            // Start may have already run (host car, menu scene); re-seed heading/track from the placed pose.
            if (_pvc != null) _pvc.ReinitializeAtCurrentPose();
            WireCamera();

            // Local pace-lap helper: free driving, but speed-capped to not overtake the car ahead, plus the
            // "line up behind car #N" prompt + out-of-position warning. Owner-only (others are remote puppets).
            var assist = GetComponent<PaceLapAssist>();
            if (assist == null) assist = gameObject.AddComponent<PaceLapAssist>();
            assist.pvc = _pvc;
        }

        _setupDone = true;
        return true;
    }

    // Mirrors PitLaneStart's parked-car placement: sample the track behind the start line for this slot,
    // offset laterally, and set the transform so the car faces down-track.
    void PlaceOnGrid(TrackBuilder track, int slot, PlayerVehicleController pvc)
    {
        float sf = track.track != null ? track.track.startFinishDistance : 0f;
        float dist = sf - slot * gridSpacing;
        var s = track.SampleAt(dist);

        float lateral = gridLateral + ((slot % 2 == 0) ? gridStagger * 0.5f : -gridStagger * 0.5f);
        Vector2 off = s.position + s.normal * lateral;
        Vector3 worldPos = track.transform.TransformPoint(new Vector3(off.x, off.y, 0f));

        bool facesUp = pvc != null && pvc.spriteFacesUp;
        float angleOffset = pvc != null ? pvc.angleOffsetDeg : 180f;

        float headingDeg = Mathf.Atan2(s.tangent.y, s.tangent.x) * Mathf.Rad2Deg;
        float zRot = headingDeg - ((facesUp ? 90f : 0f) - angleOffset);

        transform.SetPositionAndRotation(
            new Vector3(worldPos.x, worldPos.y, transform.position.z),
            Quaternion.Euler(0f, 0f, zRot));
    }

    void WireCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = transform;
        if (cam.orthographic) cam.orthographicSize = drivingOrthoSize;
    }
}
