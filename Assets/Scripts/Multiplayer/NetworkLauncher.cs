using System;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// Drives Unity Gaming Services Sessions (Relay-backed) for the multiplayer demo.
//
//   Host:  create a session -> a short join code -> load the race scene. NGO scene management
//          then syncs any client that joins into that scene.
//   Client: join by code -> NGO starts the client and pulls it into the host's loaded scene.
//
// Persists across scene loads (DontDestroyOnLoad) and ensures a configured NetworkManager exists.
// Requires the project to be linked to a UGS project with Relay + Lobby enabled (set in the Unity
// dashboard / Project Settings > Services). Until then the sign-in step will throw, surfaced as a
// status message.
public class NetworkLauncher : MonoBehaviour
{
    public static NetworkLauncher Instance { get; private set; }

    [Tooltip("Scene name (must be in Build Settings) the host loads after creating the session.")]
    [SerializeField] string raceSceneName = "RaceScene";
    [Tooltip("Maximum players per session, including the host.")]
    [SerializeField] int maxPlayers = 8;
    [Tooltip("Maximum players in a CO-OP CAREER session. Two: the host's career, plus one guest riding it.")]
    [SerializeField] int coopMaxPlayers = 2;
    [Tooltip("Networked player car spawned per client once that prefab exists (Phase 2). Null = no auto spawn.")]
    [SerializeField] GameObject playerPrefab;
    [Tooltip("Extra prefabs spawned at runtime (e.g. networked AI cars). Registered as network prefabs on every peer so host-spawned objects resolve on clients.")]
    [SerializeField] GameObject[] networkPrefabs;
    [Tooltip("Authored join-code overlay spawned (DontDestroyOnLoad) so the host can read/copy the code from the menu through into the race. Auto-filled in the editor.")]
    [SerializeField] GameObject statusOverlayPrefab;
    [Tooltip("Networked AI car the CO-OP career field is spawned from (GridSpawner, host side). Registered on every peer so the guest can resolve the host's spawns. Auto-filled in the editor.")]
    [SerializeField] GameObject coopFieldPrefab;
    [Tooltip("How long a joining client may take to connect AND finish syncing the host's scene. Unity's own default is five seconds, which this game's race scene cannot load in.")]
    [SerializeField] double sessionStartTimeoutSeconds = 120d;
    [Tooltip("Network ticks per second. Every car on track replicates its transform on this clock, so a full field multiplies it: Netcode's default of 30 with 43 cars out is the single biggest thing on the wire. Interpolation covers the gap between ticks.")]
    [SerializeField] uint tickRate = 20;

    public bool Busy { get; private set; }
    public ISession Session { get; private set; }
    public string JoinCode => Session != null ? Session.Code : null;

    // UI subscribes to this for live connection status / errors.
    public event Action<string> StatusChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // Keep unfocused windows ticking — required so an MPPM virtual player (or a backgrounded client) keeps
        // simulating and reading its input device while another window has focus.
        Application.runInBackground = true;
        EnsureNetworkManager();

        // Authored join-code overlay; persists across the menu->race scene load alongside this launcher.
        if (statusOverlayPrefab != null) DontDestroyOnLoad(Instantiate(statusOverlayPrefab));
    }

    void EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            ConfigureTransport(NetworkManager.Singleton.GetComponent<UnityTransport>());
            HookTransportFailure(NetworkManager.Singleton);
            return;
        }

        var go = new GameObject("NetworkManager");
        var nm = go.AddComponent<NetworkManager>();
        var utp = go.AddComponent<UnityTransport>();
        ConfigureTransport(utp);
        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = utp,
            EnableSceneManagement = true,   // host loads the track; clients sync into it
            ConnectionApproval = false,
            PlayerPrefab = playerPrefab,    // assigned in Phase 2; null is fine for transport-only testing
            // A race replicates one transform per car per tick, and a career field is forty-three cars. At
            // Netcode's default of 30 that is thirteen hundred transform updates a second before anything
            // else is sent; at 20 it is a third less, and NetworkTransform's interpolation is what the
            // player actually sees either way.
            TickRate = tickRate,
        };

        // Register runtime-spawned prefabs (AI cars) on every peer; the auto PlayerPrefab registers itself.
        if (networkPrefabs != null)
            foreach (var p in networkPrefabs)
                if (p != null) nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = p });

        DontDestroyOnLoad(go);
        HookTransportFailure(nm);
    }

    // Unity Transport ships with a 128-packet ceiling on how much it will send in one frame, which this
    // game goes past the instant a guest connects: NGO answers a new client by spawning every existing
    // networked object at once, and the career field alone is more than that. The overflow is not silent —
    // it drops the sends and logs "send queue full (need to increase 'Max Packet Queue Size')" with a full
    // native stack trace each time, which costs a frame of its own. Ninety-six of them in one session reads
    // in game as the connection going lumpy just as the other player arrives.
    static void ConfigureTransport(UnityTransport utp)
    {
        if (utp == null) return;
        utp.MaxPacketQueueSize = 1024;
        // MaxSendQueueSize is deliberately left at 0: NGO sizes it dynamically, and pinning it low is the
        // documented way to cause disconnections under exactly the bursts this is widening the queue for.
    }

    bool _failureHooked;

    // Relay can drop the allocation under us (timeout, network loss). NGO surfaces that via OnTransportFailure
    // and then shuts the host/client down — so we recover here instead of leaving a dead NetworkManager.
    void HookTransportFailure(NetworkManager nm)
    {
        if (_failureHooked || nm == null) return;
        nm.OnTransportFailure += HandleTransportFailure;
        _failureHooked = true;
    }

    async void HandleTransportFailure()
    {
        bool wasHost = Session != null && Session.IsHost;
        bool wasCoop = GameSession.IsCoop;
        SetStatus(wasHost
            ? "Network transport failed — recreating session…"
            : "Network transport failed — connection lost.");

        // The old Relay allocation is gone; tear the dead session/host down before recreating.
        try { if (Session != null) await Session.LeaveAsync(); }
        catch (Exception e) { Debug.LogException(e); }
        Session = null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        Coop.Reset();
        GameSession.CurrentMode = GameSession.Mode.SinglePlayer;

        // Host: spin up a fresh allocation and restart NGO as host. This yields a NEW join code, so any
        // previously connected players must re-join with it. Clients just report the lost connection.
        //
        // A co-op host reopens as co-op, not as a competitive race: their career is still running underneath
        // and dropping it into the lobby mode would switch the whole weekend off. A co-op GUEST is not
        // reconnected at all — the career being played belongs to the host's save, so there is no host
        // migration to attempt and CareerMirror hands this player their own career back on the way out.
        if (!wasHost) return;
        if (wasCoop) HostCoop(); else HostGame();
    }

    // Add a prefab to the network prefab list if it is not already there. NGO resolves a spawn by prefab
    // hash, so an unregistered prefab is a spawn the client simply cannot build.
    static void RegisterPrefab(NetworkManager nm, GameObject prefab)
    {
        if (prefab == null || nm.NetworkConfig == null) return;
        foreach (var p in nm.NetworkConfig.Prefabs.Prefabs)
            if (p != null && p.Prefab == prefab) return;
        nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
    }

    void SetStatus(string s)
    {
        Debug.Log($"[NetworkLauncher] {s}");
        StatusChanged?.Invoke(s);
    }

    // Unity's session start gives a joining client FIVE SECONDS from StartClient to being fully
    // synchronised — and the NGO scene load counts inside that window. The race scene builds a track,
    // dresses a paddock and spawns a crowd, which is well past five seconds on any machine, so a join that
    // is working perfectly well times out and surfaces as "Unexpected exception processing network
    // metadata" while the guest is, in fact, already standing in the host's paddock.
    //
    // The knob is internal to com.unity.services.multiplayer, so it is set by reflection. If the package
    // moves or renames it, nothing breaks: the join behaves as it did before and the recovery in the join
    // catch blocks still keeps a connected guest connected.
    static bool _timeoutRelaxed;
    void RelaxSessionStartTimeout()
    {
        if (_timeoutRelaxed) return;
        _timeoutRelaxed = true;
        try
        {
            var type = typeof(ISession).Assembly.GetType("Unity.Services.Multiplayer.NetworkManagerSession");
            var prop = type?.GetProperty("CancellationTimeout",
                                         BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (prop == null || !prop.CanWrite)
            {
                Debug.LogWarning("[NetworkLauncher] Session start timeout knob not found — a slow scene load may report a false join failure.");
                return;
            }
            prop.SetValue(null, TimeSpan.FromSeconds(sessionStartTimeoutSeconds));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkLauncher] Could not extend the session start timeout: {e.Message}");
        }
    }

    // A join can throw while the connection underneath it is perfectly good — the timeout above is the
    // usual reason. NGO is not shut down when that happens, so the player IS in the host's world; treating
    // it as a failure would drop them back to single player with a live client still running.
    static bool ConnectedAnyway() =>
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsListening);

    async Task EnsureServicesAsync()
    {
        RelaxSessionStartTimeout();
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            SetStatus("Initialising services…");
            var options = new InitializationOptions();
#if UNITY_EDITOR
            // Multiplayer Play Mode runs the host and every virtual player in ONE process. Without a distinct UGS
            // auth profile per player they share a single anonymous identity, which breaks session join
            // ("Unexpected exception processing network metadata"). Give each player its own profile.
            options.SetProfile(MppmProfile());
#endif
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetStatus("Signing in…");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

#if UNITY_EDITOR
    // A stable, distinct UGS auth profile per Multiplayer Play Mode player (main editor + each virtual player),
    // so they sign in as separate UGS users instead of colliding on one shared anonymous identity.
    static string MppmProfile()
    {
        if (Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor) return "main";
        // Each virtual player has its own persistentDataPath; hash it for a distinct, stable profile name.
        int h = Application.persistentDataPath.GetHashCode() & 0x7fffffff;
        return "vp" + h;
    }
#endif

    public async void HostGame()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            await EnsureServicesAsync();
            SetStatus("Creating session…");
            GameSession.CurrentMode = GameSession.Mode.Multiplayer;

            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);

            SetStatus($"Hosting — share code: {Session.Code}");
            // The session has already started NGO as host; load the race scene so joiners sync in.
            NetworkManager.Singleton.SceneManager.LoadScene(raceSceneName, LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
            SetStatus($"Host failed: {e.Message}");
            Debug.LogException(e);
        }
        finally { Busy = false; }
    }

    public async void JoinGame(string code)
    {
        if (Busy) return;
        if (string.IsNullOrWhiteSpace(code)) { SetStatus("Enter a join code first."); return; }
        Busy = true;
        try
        {
            // Your own session has to go first. Hosting and joining are the same NetworkManager, and NGO
            // will not start a client on one that is already a server — so a player who opened their career
            // to a friend and then went looking for someone else's could never actually get in.
            if (InSession)
            {
                SetStatus("Closing your own session…");
                await LeaveSessionAsync();
            }

            await EnsureServicesAsync();
            SetStatus("Joining session…");
            GameSession.CurrentMode = GameSession.Mode.Multiplayer;

            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            SetStatus("Joined — loading race…");
            // The session started NGO as client; scene management brings us into the host's scene.
        }
        catch (Exception e)
        {
            if (ConnectedAnyway())
            {
                SetStatus("Joined — loading race…");
                Debug.LogWarning($"[NetworkLauncher] Session reported a join failure but the client is connected: {e.Message}");
            }
            else
            {
                GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
                SetStatus($"Join failed: {e.Message}");
                Debug.LogException(e);
            }
        }
        finally { Busy = false; }
    }

    // ------------------------------------------------------------------ co-op career
    //
    // Co-op is not a lobby. The host keeps playing its own career exactly where it is — no lobby scene, no
    // ready-up, no starting state to agree on — and a guest joining is pulled into whatever scene the host
    // is stood in, at whatever hour of the weekend the host has reached. That is the whole premise: a
    // second player drops in at any moment, so there is nothing to wait for.

    public async void HostCoop()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            await EnsureServicesAsync();
            SetStatus("Opening your career to a friend…");
            GameSession.CurrentMode = GameSession.Mode.CoopCareer;
            PrepareCoopSession();

            var options = new SessionOptions { MaxPlayers = coopMaxPlayers }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);

            Coop.Hook();
            SetStatus($"Co-op open — share code: {Session.Code}");
            // Deliberately NO scene load: the host stays where it is and the guest comes to them.
        }
        catch (Exception e)
        {
            GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
            SetStatus($"Could not open co-op: {e.Message}");
            Debug.LogException(e);
        }
        finally { Busy = false; }
    }

    public async void JoinCoop(string code)
    {
        if (Busy) return;
        if (string.IsNullOrWhiteSpace(code)) { SetStatus("Enter a join code first."); return; }
        Busy = true;
        try
        {
            // Same rule as JoinGame: NGO will not start a client on a NetworkManager that is already a
            // server, so a player who opened their own career has to close it before entering someone else's.
            if (InSession)
            {
                SetStatus("Closing your own session…");
                await LeaveSessionAsync();
            }

            await EnsureServicesAsync();
            SetStatus("Joining their weekend…");
            GameSession.CurrentMode = GameSession.Mode.CoopCareer;
            PrepareCoopSession();

            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());

            Coop.Hook();
            SetStatus("Joined — following the host…");
            // NGO scene management pulls us into the host's current scene; CareerMirror then hands us the
            // host's weekend the moment the connection lands.
        }
        catch (Exception e)
        {
            // Connected regardless: the session object failed to hand back but the guest is in the host's
            // world, so hook co-op up rather than stranding them in a career they are no longer playing.
            if (ConnectedAnyway())
            {
                Coop.Hook();
                SetStatus("Joined — following the host…");
                Debug.LogWarning($"[NetworkLauncher] Session reported a join failure but the client is connected: {e.Message}");
            }
            else
            {
                GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
                SetStatus($"Could not join: {e.Message}");
                Debug.LogException(e);
            }
        }
        finally { Busy = false; }
    }

    // Shape the NetworkManager for co-op and make sure the career mirror is running before the transport
    // comes up, so a fast connect cannot land before there is anything to receive it.
    void PrepareCoopSession()
    {
        EnsureNetworkManager();
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Co-op players are not handed a car at connect — the guest takes over a driver already entered in
        // the weekend. An auto-spawned player prefab would put a second, unentered car in the world.
        if (nm.NetworkConfig != null) nm.NetworkConfig.PlayerPrefab = null;

        if (nm.GetComponent<CareerMirror>() == null) nm.gameObject.AddComponent<CareerMirror>();
        if (nm.GetComponent<CoopBodies>() == null) nm.gameObject.AddComponent<CoopBodies>();
        if (nm.GetComponent<CoopPossession>() == null) nm.gameObject.AddComponent<CoopPossession>();
        if (nm.GetComponent<CoopPaddockMirror>() == null) nm.gameObject.AddComponent<CoopPaddockMirror>();

        // The career field is spawned from this prefab by GridSpawner on the host. Both peers must have it
        // registered or the guest cannot resolve the spawns and receives an empty track.
        //
        // A launcher created at runtime (no scene instance to carry serialized references) has none, so fall
        // back to the one the scene's own spawner is going to use — that is the prefab by definition.
        if (coopFieldPrefab == null)
        {
            var spawner = FindFirstObjectByType<GridSpawner>();
            if (spawner != null) coopFieldPrefab = spawner.networkedAiPrefab;
        }
        RegisterPrefab(nm, coopFieldPrefab);
    }

    public async void Leave() => await LeaveSessionAsync();

    // Stop being a host or a client, and stop calling ourselves co-op.
    //
    // The order matters. Everything a caller can observe — the session mode, NGO listening, Coop's state —
    // is torn down SYNCHRONOUSLY, before the first await; only the Relay-side goodbye is allowed to finish
    // in its own time. Leaving it the other way round meant a caller that walked out of a career and loaded
    // the next scene on the following line arrived with GameSession.CurrentMode still CoopCareer and the
    // NetworkManager still hosting, so the title screen was, in the game's own terms, a co-op career hosted
    // by the player — and nothing there could join anybody, because NGO will not start a client on a
    // NetworkManager that is already a server.
    async Task LeaveSessionAsync()
    {
        var leaving = Session;
        Session = null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        Coop.Reset();
        GameSession.CurrentMode = GameSession.Mode.SinglePlayer;

        try { if (leaving != null) await leaving.LeaveAsync(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // Is there a session of our own still up? Either half counts: the UGS session object, or NGO still
    // listening after one.
    public bool InSession => Session != null
                             || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

#if UNITY_EDITOR
    // Auto-wire the authored overlay prefab so the scene/build carries the reference with no manual dragging.
    void OnValidate()
    {
        if (statusOverlayPrefab == null)
            statusOverlayPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/NetworkStatusOverlay.prefab");
        if (coopFieldPrefab == null)
            coopFieldPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Multiplayer/NetworkedAICar.prefab");
    }
#endif
}
