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
    // Resources name of the VehicleInfo this car runs (e.g. "Cup24"). A ScriptableObject cannot travel over
    // the wire, but its name can, and every one of them lives in Resources/Vehicles — so the guest loads the
    // same asset rather than being handed a car with no accel/decel curves the moment it possesses one.
    [HideInInspector] public string vehicleInfoSeed = "";

    // Peer-wide registry of every networked AI (present on host AND clients, where they're puppets). Lets the
    // local player's PaceLapAssist find cars by transform without needing the host-only spline/RaceField.
    static readonly List<NetworkedAICar> _all = new();
    public static IReadOnlyList<NetworkedAICar> All => _all;

    // Replicated identity (server writes, everyone reads) so clients reproduce the exact paint/label.
    public NetworkVariable<int> CarNumber = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> DriverName = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> Carset = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> VehicleInfoName = new(writePerm: NetworkVariableWritePermission.Server);

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
            VehicleInfoName.Value = vehicleInfoSeed ?? "";

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
            VehicleInfoName.OnValueChanged += (_, __) => ApplyVehicleInfo();

            ParkPhysics();
        }

        ApplyIdentity();
        ApplyVehicleInfo();
    }

    // Load the car's VehicleInfo from its synced name so a client copy has the same accel/decel/cornering
    // curves the host is driving it with. Only matters once a guest possesses this car — until then the
    // client's copy is a puppet with every brain off — but wiring it on spawn means possession is an
    // ownership change and nothing else.
    void ApplyVehicleInfo()
    {
        string n = VehicleInfoName.Value.ToString();
        if (string.IsNullOrEmpty(n)) return;

        var info = Resources.Load<VehicleInfo>($"Vehicles/{n}");
        if (info == null) { Debug.LogWarning($"NetworkedAICar: no VehicleInfo at Resources/Vehicles/{n}."); return; }

        var spline = GetComponent<SplineDriver>();
        if (spline != null && spline.vehicleInfo == null) spline.vehicleInfo = info;
        var pvc = GetComponent<PlayerVehicleController>();
        if (pvc != null && pvc.vehicleInfo == null) pvc.vehicleInfo = info;
        var binding = GetComponent<AIDriverBinding>();
        if (binding != null && binding.vehicleInfo == null) binding.vehicleInfo = info;
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

    // ------------------------------------------------------------------ co-op possession
    //
    // The host hands this car to the guest with ChangeOwnership; NGO raises these on each peer's own copy.
    // Doing the swap here rather than off a broadcast message removes the race entirely — a message can
    // arrive before this client has spawned the object, an ownership callback cannot.

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        // The server owns every car it spawns, so it "gains" them all at spawn; only a client gaining one
        // means a human has been put in it.
        if (IsServer || !Coop.Active) return;
        DrivePhysics();
        CoopPossession.GuestTakeOver(gameObject);
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        if (IsServer || !Coop.Active) return;
        ParkPhysics();
        CoopPossession.GuestHandBack(gameObject);
    }

    // A car this machine does not own is moved entirely by NetworkTransform, so its body has no business
    // being simulated: a dynamic Rigidbody2D that is teleported every tick still runs the solver, still
    // resolves contacts against the forty-two other cars around it, and still fights the pose it is being
    // handed. Forty-three of those on the guest is a physics scene's worth of work for a field the guest is
    // only watching. Kinematic keeps the collider — the guest's own car can still hit these — while taking
    // the body out of the solver.
    RigidbodyType2D _drivenBodyType = RigidbodyType2D.Dynamic;
    bool _bodyTypeCaptured;

    void ParkPhysics()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (!_bodyTypeCaptured) { _drivenBodyType = rb.bodyType; _bodyTypeCaptured = true; }
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        var cf = GetComponent<ConstantForce2D>();
        if (cf != null) cf.enabled = false;
    }

    // Handed back to this machine to drive: the body has to simulate again, in whatever mode the prefab
    // was authored with.
    void DrivePhysics()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = _bodyTypeCaptured ? _drivenBodyType : RigidbodyType2D.Dynamic;

        var cf = GetComponent<ConstantForce2D>();
        if (cf != null) cf.enabled = true;
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
