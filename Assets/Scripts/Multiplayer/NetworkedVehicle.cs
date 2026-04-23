using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(VehicleLogic))]
public class NetworkedVehicle : NetworkBehaviour
{
	private VehicleLogic vehicleLogic;

	private readonly NetworkVariable<float> syncedLocation = new(
		writePerm: NetworkVariableWritePermission.Owner);
	private readonly NetworkVariable<float> syncedYRatio = new(
		writePerm: NetworkVariableWritePermission.Owner);
	private readonly NetworkVariable<float> syncedSpeed = new(
		writePerm: NetworkVariableWritePermission.Owner);
	private readonly NetworkVariable<float> syncedWorldDirection = new(
		writePerm: NetworkVariableWritePermission.Owner);

	void Awake()
	{
		vehicleLogic = GetComponent<VehicleLogic>();
	}

	public override void OnNetworkSpawn()
	{
		if (IsOwner)
		{
			// Owner drives their car locally via existing VehicleLogic + InputManager
			vehicleLogic.setAsPlayer();
		}
		else
		{
			// Remote clients render this car as a ghost driven by the network state
			vehicleLogic.isNetworkRemote = true;
		}
	}

	void FixedUpdate()
	{
		if (!IsSpawned) return;

		if (IsOwner)
		{
			syncedLocation.Value = vehicleLogic.locationOnTrack;
			syncedYRatio.Value = vehicleLogic.yRatio;
			syncedSpeed.Value = vehicleLogic.speed;
			syncedWorldDirection.Value = vehicleLogic.worldDirection;
		}
		else
		{
			// Mirror authoritative state so draft/speed reads by other systems see correct values
			vehicleLogic.locationOnTrack = syncedLocation.Value;
			vehicleLogic.yRatio = syncedYRatio.Value;
			vehicleLogic.speed = syncedSpeed.Value;
			vehicleLogic.speedMetres = syncedSpeed.Value / 2.237f;
			vehicleLogic.worldDirection = syncedWorldDirection.Value;

			// Each client renders this remote car at a screen position relative to its own local player
			float localPlayerLoc = RaceManager.playerLocation;
			float trackWidth = vehicleLogic.trackWidth;
			float x = syncedLocation.Value - localPlayerLoc;
			float y = (syncedYRatio.Value - 0.5f) * trackWidth;
			transform.position = new Vector3(x, y, 0);
			transform.rotation = Quaternion.Euler(0, 0,
				EnvironmentManager.circuitRotation - syncedWorldDirection.Value);
		}
	}
}
