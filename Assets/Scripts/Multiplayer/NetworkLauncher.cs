using System;
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
    [SerializeField] string raceSceneName = "WatkinsGlen";
    [Tooltip("Maximum players per session, including the host.")]
    [SerializeField] int maxPlayers = 8;
    [Tooltip("Networked player car spawned per client once that prefab exists (Phase 2). Null = no auto spawn.")]
    [SerializeField] GameObject playerPrefab;
    [Tooltip("Extra prefabs spawned at runtime (e.g. networked AI cars). Registered as network prefabs on every peer so host-spawned objects resolve on clients.")]
    [SerializeField] GameObject[] networkPrefabs;
    [Tooltip("Authored join-code overlay spawned (DontDestroyOnLoad) so the host can read/copy the code from the menu through into the race. Auto-filled in the editor.")]
    [SerializeField] GameObject statusOverlayPrefab;

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
        if (NetworkManager.Singleton != null) { HookTransportFailure(NetworkManager.Singleton); return; }

        var go = new GameObject("NetworkManager");
        var nm = go.AddComponent<NetworkManager>();
        var utp = go.AddComponent<UnityTransport>();
        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = utp,
            EnableSceneManagement = true,   // host loads the track; clients sync into it
            ConnectionApproval = false,
            PlayerPrefab = playerPrefab,    // assigned in Phase 2; null is fine for transport-only testing
        };

        // Register runtime-spawned prefabs (AI cars) on every peer; the auto PlayerPrefab registers itself.
        if (networkPrefabs != null)
            foreach (var p in networkPrefabs)
                if (p != null) nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = p });

        DontDestroyOnLoad(go);
        HookTransportFailure(nm);
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
        SetStatus(wasHost
            ? "Network transport failed — recreating session…"
            : "Network transport failed — connection lost.");

        // The old Relay allocation is gone; tear the dead session/host down before recreating.
        try { if (Session != null) await Session.LeaveAsync(); }
        catch (Exception e) { Debug.LogException(e); }
        Session = null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        GameSession.CurrentMode = GameSession.Mode.SinglePlayer;

        // Host: spin up a fresh allocation and restart NGO as host. This yields a NEW join code, so any
        // previously connected players must re-join with it. Clients just report the lost connection.
        if (wasHost) HostGame();
    }

    void SetStatus(string s)
    {
        Debug.Log($"[NetworkLauncher] {s}");
        StatusChanged?.Invoke(s);
    }

    async Task EnsureServicesAsync()
    {
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
            await EnsureServicesAsync();
            SetStatus("Joining session…");
            GameSession.CurrentMode = GameSession.Mode.Multiplayer;

            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            SetStatus("Joined — loading race…");
            // The session started NGO as client; scene management brings us into the host's scene.
        }
        catch (Exception e)
        {
            GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
            SetStatus($"Join failed: {e.Message}");
            Debug.LogException(e);
        }
        finally { Busy = false; }
    }

    public async void Leave()
    {
        try { if (Session != null) await Session.LeaveAsync(); }
        catch (Exception e) { Debug.LogException(e); }
        Session = null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
    }

#if UNITY_EDITOR
    // Auto-wire the authored overlay prefab so the scene/build carries the reference with no manual dragging.
    void OnValidate()
    {
        if (statusOverlayPrefab == null)
            statusOverlayPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/NetworkStatusOverlay.prefab");
    }
#endif
}
