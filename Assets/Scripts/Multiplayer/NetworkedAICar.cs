using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Host-authoritative networked AI car. The HOST runs the full AI brain (SplineDriver + SplineInputDriver +
// PlayerVehicleController) and the resulting pose replicates to clients via SERVER-authoritative NetworkTransform.
// On clients every brain is disabled — the car is a pure puppet moved only by NetworkTransform. Visual identity
// (livery number + driver name + carset) is synced so all peers paint and label each car identically.
//
// GridSpawner (host only, in multiplayer) fills the *Seed fields before NetworkObject.Spawn(); the server then
// copies them into the synced NetworkVariables on spawn (writing NetworkVariables pre-spawn isn't allowed).
[RequireComponent(typeof(NetworkObject))]
public class NetworkedAICar : NetworkBehaviour
{
    // Set by the host spawner before Spawn(); copied into the NetworkVariables by the server on spawn.
    [HideInInspector] public int carNumberSeed;
    [HideInInspector] public string driverNameSeed = "";
    [HideInInspector] public string carsetSeed = "";

    // Replicated identity (server writes, everyone reads) so clients reproduce the exact paint/label.
    public NetworkVariable<int> CarNumber = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> DriverName = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> Carset = new(writePerm: NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        var track = FindFirstObjectByType<TrackBuilder>();
        var spline = GetComponent<SplineDriver>();
        var pvc = GetComponent<PlayerVehicleController>();
        if (track != null)
        {
            if (spline != null) spline.track = track;
            if (pvc != null) pvc.track = track;
        }

        if (IsServer)
        {
            CarNumber.Value = carNumberSeed;
            DriverName.Value = driverNameSeed ?? "";
            Carset.Value = carsetSeed ?? "";
        }
        else
        {
            // Clients: kill every brain so nothing fights NetworkTransform for the pose.
            DisableIfPresent<SplineDriver>();
            DisableIfPresent<SplineInputDriver>();
            DisableIfPresent<PlayerVehicleController>();
            DisableIfPresent<VehicleCollision>();
            DisableIfPresent<AIDriverBinding>();
            DisableIfPresent<FormationController>();
            DisableIfPresent<PitStopController>();
            DisableIfPresent<AIRacingBehaviour>();

            CarNumber.OnValueChanged += (_, __) => ApplyIdentity();
            DriverName.OnValueChanged += (_, __) => ApplyIdentity();
            Carset.OnValueChanged += (_, __) => ApplyIdentity();
        }

        ApplyIdentity();
    }

    // Paint + label the car from the synced identity. Rebuilds the deformable bodywork mesh from the livery
    // sprite so a client's car matches the host's exactly. Safe to call repeatedly (idempotent).
    void ApplyIdentity()
    {
        string carset = Carset.Value.ToString();
        if (string.IsNullOrEmpty(carset)) return;
        int number = CarNumber.Value;

        var sprite = Resources.Load<Sprite>($"{carset}livery{number}");
        if (sprite != null)
        {
            var dmg = GetComponentInChildren<VehicleDamage>();
            if (dmg != null) { dmg.sourceSprite = sprite; dmg.Build(); }
        }

        var label = GetComponent<DriverLabel>();
        if (label != null)
        {
            label.carset = carset;
            label.carNumber = number;
            label.driverName = DriverName.Value.ToString();
        }
    }

    void DisableIfPresent<T>() where T : MonoBehaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }
}
