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
        EnsureNetworkManager();
    }

    void EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null) return;

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
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetStatus("Signing in…");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

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

    // Persistent on-screen join code. This object survives the menu->race scene load (DontDestroyOnLoad),
    // so the host can still read / copy the code after the race has loaded and share it with friends.
    void OnGUI()
    {
        if (Session == null) return;

        var box = new GUIStyle(GUI.skin.box)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        string label = Session.IsHost ? $"HOST  —  Join code: {Session.Code}" : $"Connected  ({Session.Code})";
        GUI.Box(new Rect(10, 10, 380, 46), label, box);

        if (Session.IsHost && GUI.Button(new Rect(10, 60, 140, 32), "Copy code"))
            GUIUtility.systemCopyBuffer = Session.Code;
    }
}
