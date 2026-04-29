using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkBootstrap : MonoBehaviour
{
	const int maxPortProbes = 16;

	void OnGUI()
	{
		var nm = NetworkManager.Singleton;
		if (nm == null)
		{
			GUI.Box(new Rect(10, 10, 260, 40), "No NetworkManager in scene");
			return;
		}

		GUILayout.BeginArea(new Rect(10, 10, 240, 160));

		if (!nm.IsClient && !nm.IsServer)
		{
			if (GUILayout.Button("Host")) StartHostOnFreePort(nm);
			if (GUILayout.Button("Client")) nm.StartClient();
			if (GUILayout.Button("Server")) StartServerOnFreePort(nm);
		}
		else
		{
			string mode = nm.IsHost ? "Host"
				: nm.IsServer ? "Server"
				: "Client";
			GUILayout.Label($"Mode: {mode}");
			GUILayout.Label($"ClientId: {nm.LocalClientId}");
			if (nm.IsServer)
			{
				GUILayout.Label($"Connected: {nm.ConnectedClientsIds.Count}");
			}
			if (GUILayout.Button("Disconnect")) nm.Shutdown();
		}

		GUILayout.EndArea();
	}

	void StartHostOnFreePort(NetworkManager nm) => StartOnFreePort(nm, () => nm.StartHost(), "Host");
	void StartServerOnFreePort(NetworkManager nm) => StartOnFreePort(nm, () => nm.StartServer(), "Server");

	void StartOnFreePort(NetworkManager nm, System.Func<bool> start, string label)
	{
		var utp = nm.GetComponent<UnityTransport>();
		if (utp == null)
		{
			Debug.LogWarning("NetworkBootstrap: no UnityTransport on NetworkManager — falling back to default start.");
			start();
			return;
		}

		ushort basePort = utp.ConnectionData.Port;
		for (int i = 0; i < maxPortProbes; i++)
		{
			ushort port = (ushort)(basePort + i);
			utp.SetConnectionData(utp.ConnectionData.Address, port, utp.ConnectionData.ServerListenAddress);
			if (start())
			{
				if (i > 0) Debug.Log($"NetworkBootstrap: {label} bound to fallback port {port} (port {basePort} was in use).");
				return;
			}
			nm.Shutdown();
		}

		Debug.LogError($"NetworkBootstrap: failed to start {label} after probing ports {basePort}–{basePort + maxPortProbes - 1}.");
	}
}
