using UnityEngine;
using Unity.Netcode;

public class NetworkBootstrap : MonoBehaviour
{
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
			if (GUILayout.Button("Host")) nm.StartHost();
			if (GUILayout.Button("Client")) nm.StartClient();
			if (GUILayout.Button("Server")) nm.StartServer();
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
}
