using System.Collections.Generic;
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

    ulong _possessedObjectId;
    bool _possessing;
    float _nextPoll;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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

        if (!nm.IsServer) return;
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + pollInterval;
        HostPoll(nm);
    }

    // ------------------------------------------------------------------ host

    void HostPoll(NetworkManager nm)
    {
        // "The host is out in a session" is the trigger. Not the scene, not the lobby — the weekend's own
        // flag, which is set when the sheet routes the player into practice, qualifying or the race and
        // cleared when it settles.
        bool shouldDrive = Coop.GuestPresent && RaceWeekend.SessionLive;

        if (shouldDrive && !_possessing) TryGrant(nm);
        else if (!shouldDrive && _possessing) Release(nm);
    }

    void TryGrant(NetworkManager nm)
    {
        var car = PickFreeCar();
        if (car == null) return;   // the field may not be up yet; poll again

        var netObj = car.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned) return;

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

        Debug.Log($"[Coop] Guest is now driving #{car.CarNumber.Value} ({car.DriverName.Value}).");
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

    // A driver the guest can be. Random rather than "the slowest" or "the one at the back": the guest is
    // dropped into the weekend as somebody already in it, and which somebody is not the host's to curate.
    // Skips the host's own car (it has no NetworkedAICar) and anything already handed over.
    NetworkedAICar PickFreeCar()
    {
        var pool = new List<NetworkedAICar>();
        foreach (var c in NetworkedAICar.All)
        {
            if (c == null) continue;
            var no = c.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned) continue;
            if (no.OwnerClientId != NetworkManager.ServerClientId) continue;   // already somebody's
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
