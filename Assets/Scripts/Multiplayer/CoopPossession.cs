using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// The guest picks up the persona of a driver already entered in the weekend.
//
// The rule is simple and automatic: whenever the host is out in a session, the guest is out in it too. They
// are not given a car of their own — the field is the weekend's field, and adding a car to it would mean an
// entry nobody qualified. Instead one of the AI already on track is handed over, at whatever pose and
// position it is currently running. The guest inherits that driver's identity, livery, championship entry
// and relationships for free, because they are that driver for the session.
//
// Mechanically this is TeamSwitchController's mid-race handover — HandToAI / TakeControl, which swap a car
// between the spline brain and live input with no teleport — plus NetworkObject.ChangeOwnership so the
// authoritative pose follows the human. CoopNetworkTransform is owner-authoritative for exactly this: while
// the host's AI drives, the host is the owner and nothing changes; the instant ownership moves, the guest's
// pose becomes the real one and the host's copy is a puppet.
//
// Host-authoritative throughout: the host chooses the car and hands it over. The guest never picks.
public class CoopPossession : MonoBehaviour
{
    [Tooltip("Seconds between host-side checks for whether the guest should be in a car.")]
    public float pollInterval = 0.5f;

    public static CoopPossession Instance { get; private set; }

    // The car the guest is currently driving, on whichever peer is asking. Null when they are on foot.
    public static GameObject GuestCar { get; private set; }

    const string PossessMessage = "coop.possess";

    ulong _possessedObjectId;
    bool _possessing;
    float _nextPoll;
    bool _registered;

    // The driver the guest stands in for. Chosen the moment they join — out of the entry list parked in
    // the paddock, before any session exists — and kept for as long as they are here, so the person they
    // replaced in the motorhome row is the same person whose car and pit box they take on race day. A car
    // re-rolled at the start of every session would mean a different driver each time, which reads as the
    // guest being teleported into somebody else's race.
    public static int GuestCarNumber { get; private set; }
    public static string GuestDriverName { get; private set; } = "";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        Coop.GuestJoined += ChooseGuestDriver;
        Coop.GuestLeft += ForgetGuestDriver;
    }

    void OnDestroy()
    {
        Coop.GuestJoined -= ChooseGuestDriver;
        Coop.GuestLeft -= ForgetGuestDriver;
        Unregister();
        if (Instance == this) Instance = null;
    }

    // Host: pick the entry the guest takes over, off the row of motorhomes — that row IS the entry list,
    // and it is standing in the paddock long before a field is spawned. The player's own number is not in
    // the pool: the weekend cannot contain two of it.
    static void ChooseGuestDriver()
    {
        if (!Coop.IsHost) return;
        if (GuestCarNumber > 0) return;   // they have a driver already; joining twice does not re-roll it

        var lot = DriverMotorhomeLot.Instance;
        if (lot == null || !lot.Built || lot.Slots.Count == 0) return;   // resolved off the field instead

        int playerNumber = CarIdentity.NumberOf(CarIdentity.FindPlayerCar());

        var pool = new List<DriverMotorhomeLot.Slot>();
        foreach (var slot in lot.Slots)
            if (slot != null && slot.carNumber > 0 && slot.carNumber != playerNumber && !slot.isPlayer)
                pool.Add(slot);
        if (pool.Count == 0) return;

        var picked = pool[Random.Range(0, pool.Count)];
        GuestCarNumber = picked.carNumber;
        GuestDriverName = !string.IsNullOrEmpty(picked.shortName) ? picked.shortName : picked.fullName;
        Debug.Log($"[Coop] The guest is driving for #{GuestCarNumber} {GuestDriverName}.");
    }

    static void ForgetGuestDriver()
    {
        GuestCarNumber = 0;
        GuestDriverName = "";
    }

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !Coop.Active)
        {
            _possessing = false;
            GuestCar = null;
            return;
        }

        Register(nm);

        if (!nm.IsServer) return;
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + pollInterval;
        HostPoll(nm);
    }

    // Only the guest listens: the pose that comes with a car is the host telling the new owner where the
    // car it has just been handed actually is.
    void Register(NetworkManager nm)
    {
        if (_registered || nm.IsServer) return;
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;
        msg.RegisterNamedMessageHandler(PossessMessage, OnPossessPose);
        _registered = true;
    }

    void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        var nm = NetworkManager.Singleton;
        var msg = nm != null ? nm.CustomMessagingManager : null;
        if (msg != null) msg.UnregisterNamedMessageHandler(PossessMessage);
    }

    // ------------------------------------------------------------------ host

    void HostPoll(NetworkManager nm)
    {
        // "The host is out in a session" is the trigger. Not the scene, not the lobby — the weekend's own
        // flag, which is set when the sheet routes the player into practice, qualifying or the race and
        // cleared when it settles.
        // FieldReady is the third condition and it matters as much as the other two: a car handed over
        // while the field is still being built is handed over before it has been parked in its box, and
        // ownership is what decides whose copy of the pose is real.
        // The row of motorhomes may not have been built when the guest arrived (they can land mid scene
        // load), so the pick is retried until it takes. It costs a list walk at the poll rate and stops
        // the moment they have a driver.
        if (Coop.GuestPresent && GuestCarNumber <= 0) ChooseGuestDriver();

        bool shouldDrive = Coop.GuestPresent && RaceWeekend.SessionLive && GridSpawner.FieldReady;

        if (shouldDrive && !_possessing) TryGrant(nm);
        else if (!shouldDrive && _possessing) Release(nm);
    }

    void TryGrant(NetworkManager nm)
    {
        var car = PickGuestCar();
        if (car == null) return;   // the field may not be up yet; poll again

        var netObj = car.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned) return;

        // Where the car is standing, read while the host's brain still owns it. This is what goes over with
        // the handover: the guest's copy of a car it has never driven can be a tick or two behind, or —
        // at a standing start, where a parked car sends nothing because nothing about it is changing —
        // still sat where it was instantiated. Owner authority makes whatever the guest believes true for
        // everybody, so the guest is told rather than left to guess.
        var pose = ReadPose(car.gameObject);

        // The host stops driving this one. Brains off BEFORE ownership moves, so there is never a frame
        // where the host's AI and the guest's input are both writing the same car.
        HandToHuman(car.gameObject);

        // The guest picks itself up from here: NGO raises OnGainedOwnership on its copy of this car, which
        // is the one moment it is certain that copy exists locally. A message announcing the handover could
        // arrive before the spawn did, and then land on nothing.
        netObj.ChangeOwnership(Coop.GuestClientId);
        _possessedObjectId = netObj.NetworkObjectId;
        _possessing = true;
        GuestCar = car.gameObject;

        // The number is locked in here as well as at join: a guest who arrived somewhere with no motorhome
        // row to read still keeps the same driver from this session on.
        if (GuestCarNumber <= 0)
        {
            GuestCarNumber = car.CarNumber.Value;
            GuestDriverName = car.DriverName.Value.ToString();
        }

        SendPose(nm, netObj.NetworkObjectId, pose);

        Debug.Log($"[Coop] Guest is now driving #{car.CarNumber.Value} ({car.DriverName.Value}).");
    }

    // ------------------------------------------------------------------ handing the pose over

    struct Pose
    {
        public Vector3 position;
        public float headingDeg;
        public float mph;
    }

    // The car's pose as the machine that has been driving it understands it: the spline brain's heading
    // while the AI is on it, the dynamic model's once a human has been.
    static Pose ReadPose(GameObject car)
    {
        var pvc = car.GetComponent<PlayerVehicleController>();
        var spline = car.GetComponent<SplineDriver>();

        var pose = new Pose { position = car.transform.position };
        if (spline != null && spline.enabled && !spline.externalMotionController)
        {
            pose.headingDeg = spline.CommandedHeadingDeg;
            pose.mph = spline.CurrentMph;
        }
        else if (pvc != null)
        {
            pose.headingDeg = pvc.HeadingDeg;
            pose.mph = pvc.SpeedMph;
        }
        return pose;
    }

    static void SendPose(NetworkManager nm, ulong objectId, Pose pose)
    {
        var msg = nm.CustomMessagingManager;
        if (msg == null || !Coop.GuestPresent) return;

        using var writer = new FastBufferWriter(40, Allocator.Temp);
        writer.WriteValueSafe(objectId);
        writer.WriteValueSafe(pose.position);
        writer.WriteValueSafe(pose.headingDeg);
        writer.WriteValueSafe(pose.mph);

        // Reliable: a dropped pose is a car left wherever the guest happened to think it was, and nothing
        // sends it again.
        msg.SendNamedMessage(PossessMessage, Coop.GuestClientId, writer, NetworkDelivery.Reliable);
    }

    // Guest: put the car exactly where the host says it is. Ordering does not matter — this can land before
    // or after OnGainedOwnership, and either way the car ends up on the host's spot with the local model
    // seeded from it rather than from whatever it had drifted to.
    void OnPossessPose(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong objectId);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out float headingDeg);
        reader.ReadValueSafe(out float mph);

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SpawnManager == null) return;
        if (!nm.SpawnManager.SpawnedObjects.TryGetValue(objectId, out var netObj) || netObj == null) return;

        var car = netObj.gameObject;
        var pvc = car.GetComponent<PlayerVehicleController>();

        float z = car.transform.position.z;
        bool facesUp = pvc != null && pvc.spriteFacesUp;
        float angleOffset = pvc != null ? pvc.angleOffsetDeg : 180f;

        car.transform.SetPositionAndRotation(
            new Vector3(position.x, position.y, z),
            Quaternion.Euler(0f, 0f, (facesUp ? headingDeg - 90f : headingDeg) + angleOffset));

        var body = car.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = car.transform.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        // Re-seed the dynamic model. TakeControl already seeded it, but from the pose the car was at when
        // ownership landed, which is the pose this message exists to correct.
        if (pvc != null && pvc.enabled) pvc.SeedPose(car.transform.position, headingDeg, mph / 2.237f);
    }

    void Release(NetworkManager nm)
    {
        _possessing = false;
        GuestCar = null;

        if (nm.SpawnManager != null
            && nm.SpawnManager.SpawnedObjects.TryGetValue(_possessedObjectId, out var netObj)
            && netObj != null)
        {
            netObj.ChangeOwnership(NetworkManager.ServerClientId);
            HandBackToAI(netObj.gameObject);
        }

        _possessedObjectId = 0;
    }

    // The car the guest drives: the one carrying the number of the driver they replaced when they joined.
    // Falling back to a free car at random only when that entry is not in this session's field at all —
    // a guest who joined somewhere with no entry list to read, or a driver who is not running today.
    // Skips the host's own car (it has no NetworkedAICar) and anything already handed over.
    NetworkedAICar PickGuestCar()
    {
        var pool = new List<NetworkedAICar>();
        foreach (var c in NetworkedAICar.All)
        {
            if (c == null) continue;
            var no = c.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned) continue;
            if (no.OwnerClientId != NetworkManager.ServerClientId) continue;   // already somebody's

            if (GuestCarNumber > 0 && c.CarNumber.Value == GuestCarNumber) return c;
            pool.Add(c);
        }
        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    // ------------------------------------------------------------------ guest
    //
    // Called from NetworkedAICar.OnGainedOwnership / OnLostOwnership — NGO raises those on the guest's own
    // copy of the car, so by definition the object exists locally by the time we are asked to drive it.

    public static void GuestTakeOver(GameObject car)
    {
        if (car == null) return;

        TakeControl(car);
        GuestCar = car;

        // The rest of this player's screen has to follow the car they are now driving, exactly as it does
        // for a team switch in single player.
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.target = car.transform;

        var dm = FindFirstObjectByType<DriveModeController>();
        if (dm != null) { dm.RetargetPlayerCar(car); dm.SetDriving(true); }

        if (RacePositionTracker.Instance != null) RacePositionTracker.Instance.SetLocalPlayer(car.transform);

        var hud = FindFirstObjectByType<PlayerTelemetryHUD>();
        if (hud != null) hud.target = car.GetComponent<PlayerVehicleController>();
    }

    public static void GuestHandBack(GameObject car)
    {
        // Back to a puppet: the host owns the pose again and every local brain must stay out of it.
        if (car != null) SilenceBrains(car);
        if (GuestCar == car) GuestCar = null;
    }

    // ------------------------------------------------------------------ brain swaps
    //
    // The same handover TeamSwitchController does in single player, split across two machines: the host
    // side takes the AI off the car, the guest side puts a human on it.

    // Host: this car is about to belong to somebody else. Everything that would write its transform or make
    // decisions for it comes off — the pose arrives over the network from here on.
    static void HandToHuman(GameObject car)
    {
        Disable<SplineDriver>(car);          // out of RaceField: it is a human's car now
        Disable<SplineInputDriver>(car);
        Disable<AIRacingBehaviour>(car);
        Disable<PitStopController>(car);     // the human decides when to pit
        Disable<FormationController>(car);
        Disable<PlayerVehicleController>(car);
        Disable<VehicleCollision>(car);      // the owner runs depenetration; two would fight
    }

    // Host: the guest has gone, or the session is over. Re-engage the spline brain at the car's current
    // pose so it rejoins the race where it is, rather than teleporting back to a start distance.
    static void HandBackToAI(GameObject car)
    {
        var pvc = car.GetComponent<PlayerVehicleController>();
        var spline = car.GetComponent<SplineDriver>();
        if (pvc == null || spline == null) return;

        float mph = pvc.SpeedMph;

        if (spline.track == null) spline.track = pvc.track;
        if (spline.vehicleInfo == null) spline.vehicleInfo = pvc.vehicleInfo;
        spline.spriteFacesUp = pvc.spriteFacesUp;
        spline.angleOffsetDeg = pvc.angleOffsetDeg;
        spline.enabled = true;
        spline.EngageFromCurrentPose(mph);

        var input = car.GetComponent<SplineInputDriver>();
        if (input != null) input.enabled = true;
        var racing = car.GetComponent<AIRacingBehaviour>();
        if (racing != null) racing.enabled = true;
        var pit = car.GetComponent<PitStopController>();
        if (pit != null) pit.enabled = true;
        var col = car.GetComponent<VehicleCollision>();
        if (col != null) col.enabled = true;

        pvc.externalInput = true;
        pvc.enableWheelspin = false;
        pvc.damageImpairsHandling = false;
        pvc.enabled = true;
    }

    // Guest: put live input on this car. Mirrors TeamSwitchController.TakeControl — brains off, input on,
    // and the PlayerVehicleController re-enabled so it registers as an obstacle the rest of the field
    // brakes for, seeded from the pose the car is already at so there is no jump.
    static void TakeControl(GameObject car)
    {
        Disable<SplineInputDriver>(car);
        Disable<AIRacingBehaviour>(car);
        Disable<PitStopController>(car);
        Disable<FormationController>(car);
        Disable<AIDriverBinding>(car);

        var pvc = car.GetComponent<PlayerVehicleController>();
        var spline = car.GetComponent<SplineDriver>();
        if (pvc == null) return;

        float mph = pvc.SpeedMph;
        float heading = pvc.HeadingDeg;
        Vector3 pos = car.transform.position;

        if (spline != null && spline.enabled)
        {
            if (!spline.externalMotionController) { mph = spline.CurrentMph; heading = spline.CommandedHeadingDeg; }
            spline.enabled = false;
        }

        // The track reference is a scene object the prefab cannot carry; NetworkedAICar wires it on spawn,
        // but re-assert it here in case this car arrived before the track did.
        if (pvc.track == null) pvc.track = FindFirstObjectByType<TrackBuilder>();

        var col = car.GetComponent<VehicleCollision>();
        if (col != null) col.enabled = true;

        pvc.externalInput = false;
        pvc.enableWheelspin = true;
        pvc.damageImpairsHandling = true;
        pvc.enabled = false;
        pvc.enabled = true;                  // OnEnable with the spline off → registers as an obstacle
        pvc.SeedPose(pos, heading, mph / 2.237f);
    }

    // Guest, on losing the car: nothing local may drive it again.
    static void SilenceBrains(GameObject car)
    {
        Disable<SplineDriver>(car);
        Disable<SplineInputDriver>(car);
        Disable<AIRacingBehaviour>(car);
        Disable<PitStopController>(car);
        Disable<FormationController>(car);
        Disable<PlayerVehicleController>(car);
        Disable<VehicleCollision>(car);
    }

    static void Disable<T>(GameObject go) where T : MonoBehaviour
    {
        var c = go.GetComponent<T>();
        if (c != null) c.enabled = false;
    }
}
