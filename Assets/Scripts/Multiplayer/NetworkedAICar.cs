using System.Collections.Generic;
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
    // Kinematic field: drive via SplineDriver only (no dynamic bicycle model). Set by GridSpawner before Spawn().
    [HideInInspector] public bool kinematicSeed;

    // Peer-wide registry of every networked AI (present on host AND clients, where they're puppets). Lets the
    // local player's PaceLapAssist find cars by transform without needing the host-only spline/RaceField.
    static readonly List<NetworkedAICar> _all = new();
    public static IReadOnlyList<NetworkedAICar> All => _all;

    // Replicated identity (server writes, everyone reads) so clients reproduce the exact paint/label.
    public NetworkVariable<int> CarNumber = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> DriverName = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> Carset = new(writePerm: NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!_all.Contains(this)) _all.Add(this);

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

            // The host runs the whole AI field. The prefab ships its brains DISABLED so they don't tick on a
            // client before the else-branch below switches them off — but that means the server has to switch
            // them back ON, or every car sits dead on the grid (placed by GridSpawner.PlaceAtStartDistance but
            // never advanced). Single-player AI use a different prefab whose brains are already enabled, which
            // is why this only bit multiplayer. Idempotent if a component is already enabled.
            EnableIfPresent<SplineDriver>();
            EnableIfPresent<VehicleCollision>();
            if (kinematicSeed)
            {
                // Cheap kinematic AI: SplineDriver writes the transform directly. Skip the dynamic bicycle model
                // (PlayerVehicleController + SplineInputDriver) — the big host-CPU saving for a full networked field.
                DisableIfPresent<SplineInputDriver>();
                DisableIfPresent<PlayerVehicleController>();
                if (spline != null) spline.externalMotionController = false;
            }
            else
            {
                EnableIfPresent<SplineInputDriver>();
                EnableIfPresent<PlayerVehicleController>();
            }
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
            // Force a fresh per-car material from THIS livery — Build() reuses an existing material if one is
            // already assigned (the prefab ships a shared one), which would paint every car the same.
            if (dmg != null) { dmg.sourceSprite = sprite; dmg.material = null; dmg.Build(); }
        }

        var label = GetComponent<DriverLabel>();
        if (label != null)
        {
            label.carset = carset;
            label.carNumber = number;
            label.driverName = DriverName.Value.ToString();
        }
    }

    public override void OnNetworkDespawn() => _all.Remove(this);

    void DisableIfPresent<T>() where T : MonoBehaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }

    void EnableIfPresent<T>() where T : MonoBehaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = true;
    }
}
