using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Minimal host/client bootstrap for the race scene. Creates a NetworkManager at runtime if the scene has none,
// wires UnityTransport + the player vehicle prefab, and shows Host / Client / Server buttons. On connect, NGO
// auto-spawns the player prefab per client so each player gets a car to drive (owner-gated by NetworkCarOwnerGate).
public class MultiplayerBootstrap : MonoBehaviour
{
    [Tooltip("Networked player vehicle prefab (must have a NetworkObject). Spawned once per connected client.")]
    public GameObject playerPrefab;
    [Tooltip("IP the client connects to / the server binds.")]
    public string address = "127.0.0.1";
    public ushort port = 7777;
    [Tooltip("Turn off NGO scene management — both ends already have the race scene loaded, so we don't want the client reloading it.")]
    public bool disableSceneManagement = true;

    void Awake()
    {
        if (NetworkManager.Singleton == null) CreateNetworkManager();
    }

    void CreateNetworkManager()
    {
        var go = new GameObject("NetworkManager");
        var nm = go.AddComponent<NetworkManager>();
        var utp = go.AddComponent<UnityTransport>();
        utp.SetConnectionData(address, port);

        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = utp,
            ConnectionApproval = false,
            PlayerPrefab = playerPrefab,
            EnableSceneManagement = !disableSceneManagement,
        };
    }

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        const float x = 20f;
        float y = 160f;
        var box = new GUIStyle(GUI.skin.box) { fontSize = 16 };

        if (!nm.IsClient && !nm.IsServer)
        {
            GUI.Box(new Rect(x - 6, y - 6, 160, 190), "Multiplayer", box);
            if (playerPrefab == null)
                GUI.Label(new Rect(x, y + 150, 150, 30), "No player prefab set");
            if (GUI.Button(new Rect(x, y + 20, 140, 36), "Host")) nm.StartHost();
            if (GUI.Button(new Rect(x, y + 62, 140, 36), "Client")) nm.StartClient();
            if (GUI.Button(new Rect(x, y + 104, 140, 36), "Server")) nm.StartServer();
        }
        else
        {
            string role = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
            GUI.Box(new Rect(x - 6, y - 6, 200, 90), $"{role}", box);
            GUI.Label(new Rect(x, y + 22, 190, 30), $"Players: {nm.ConnectedClientsList.Count}");
            if (GUI.Button(new Rect(x, y + 48, 140, 32), "Disconnect")) nm.Shutdown();
        }
    }
}
