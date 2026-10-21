using UnityEngine;

// The crew straightening a wrecked car in its box, over seconds rather than in a frame.
//
// A pit stop hands the car back whole the instant the jack drops, because a stop is a stop: four tyres, a
// fuel can, and the driver never gets out. This is the other thing that happens to a damaged car — it gets
// dragged in, the driver climbs out, and the crew stand round it beating the panels back out — five
// seconds for a scuffed wing, half a minute for a car that has been destroyed. Being able to watch that
// happen is the point: the dents come out a little every frame (VehicleDamage.Repair), not all at once. A tow that returned a straight car would make putting
// it in the wall free, and the player would take the wall over the lap time every time.
//
// Self-installing: PitLaneStart.TowToPits calls Begin(car) and this attaches itself. Nothing to wire.
public class PitCrewRepair : MonoBehaviour
{
    [Tooltip("Seconds the crew take on the lightest damage worth a repair. Even a scuff gets the crew out " +
             "round the car for long enough to be seen doing it.")]
    public float minRepairSeconds = 5f;

    [Tooltip("Seconds the crew take to bring a completely destroyed car (damage 1) back to straight. In " +
             "between, the wait scales with how hard the car was hit (RepairSeconds).")]
    public float maxRepairSeconds = 30f;

    [Tooltip("Damage level at or below which the car counts as repaired and the crew stand down.")]
    public float doneBelow = 0.02f;

    [Tooltip("Stop work while the player is sat in the car. Nobody welds around a driver, and a car being " +
             "repaired while it is being driven would repair itself on the way down the back straight.")]
    public bool pauseWhileDriven = true;

    PlayerVehicleController _car;
    VehicleDamage _bodywork;

    // How long this job takes, fixed when it starts (RepairSeconds of the damage then).
    float _jobSeconds;

    // Set when the driver never got out (no on-foot body): the car is held parked with its controls off
    // while the crew work, and handed back to the driver the moment they are done.
    bool _handBackWhenDone;

    // How bad it was when they started, so the readout can say how far through they are rather than just
    // how bad it still is.
    float _startedAt;

    // The box crew stood round the car while the work runs, so the repair is something you can see
    // happening rather than a number going down. Null while they are on the wall.
    PitCrewBox _crew;

    public static PitCrewRepair Active { get; private set; }

    // Fraction of the job done, 0 to 1. -1 when no crew are working on anything.
    public static float Progress01
    {
        get
        {
            var r = Active;
            if (r == null || r._bodywork == null || r._startedAt <= 0f) return -1f;
            return Mathf.Clamp01(1f - r._bodywork.DamageLevel / r._startedAt);
        }
    }

    // Put a crew on this car. Calling it again on a car already being worked on restarts the estimate
    // against whatever the damage is now, which is what a second trip into the wall should do.
    //
    // handBackWhenDone: the driver is still sat in the car (a single race has no on-foot body). Pass the car
    // with its controls OFF; it is pinned in the box while the crew work and its controls come back on when
    // they finish. Without it the controls were switched straight back on, which is "being driven", so the
    // crew never came out and the car never straightened.
    public static PitCrewRepair Begin(PlayerVehicleController car, bool handBackWhenDone = false)
    {
        if (car == null) return null;

        var repair = car.GetComponent<PitCrewRepair>();
        if (repair == null) repair = car.gameObject.AddComponent<PitCrewRepair>();

        repair._car = car;
        repair._bodywork = car.GetComponentInChildren<VehicleDamage>();
        repair._startedAt = repair._bodywork != null ? repair._bodywork.DamageLevel : 0f;
        repair._jobSeconds = RepairSeconds(repair._startedAt, repair.minRepairSeconds, repair.maxRepairSeconds);
        repair._handBackWhenDone = handBackWhenDone;
        if (handBackWhenDone) ParkedCarPin.Hold(car);
        repair.enabled = true;
        // Already out round it from an earlier tow: re-measure, because the car may have landed elsewhere.
        if (repair._crew != null && !repair._crew.BeginRepair(car.transform)) repair._crew = null;
        Active = repair;
        return repair;
    }

    // How long the crew take over a car this badly damaged (DamageLevel, 0-1): minSeconds for the lightest
    // knock, maxSeconds for a destroyed car, straight-line in between.
    public static float RepairSeconds(float damage01, float minSeconds, float maxSeconds)
    {
        float lo = Mathf.Max(0f, minSeconds);
        return Mathf.Lerp(lo, Mathf.Max(lo, maxSeconds), Mathf.Clamp01(damage01));
    }

    void OnDestroy() { if (Active == this) Active = null; StandCrewDown(); }

    void OnDisable() => StandCrewDown();

    void Update()
    {
        if (_bodywork == null) { Finish(); return; }

        // The driver is back in it. Work stops until they are out again — which also means a driver who
        // climbs in early and goes back out is taking a bent car with them, exactly as they should.
        if (pauseWhileDriven && _car != null && _car.enabled && !_handBackWhenDone) { StandCrewDown(); return; }

        if (_bodywork.DamageLevel <= doneBelow) { _bodywork.RepairFull(); Finish(); return; }

        CallCrewOut();

        // A fixed fraction of what is LEFT each second would approach straight without ever arriving, so
        // the rate is set against the damage the crew started with. That makes the wait proportional to
        // how hard the car was hit, which is the honest version of this.
        float perSecond = Mathf.Max(0.0001f, _startedAt) / Mathf.Max(1f, _jobSeconds);
        float remaining = Mathf.Max(0.0001f, _bodywork.DamageLevel);
        _bodywork.Repair(Mathf.Clamp01(perSecond * Time.deltaTime / remaining));
    }

    void Finish()
    {
        if (Active == this) Active = null;
        StandCrewDown();
        enabled = false;
        // The driver who never got out gets the controls back (which also lifts the box pin).
        if (_handBackWhenDone && _car != null) _car.enabled = true;
        _handBackWhenDone = false;
    }

    // The car's own box crew — the player's reserved box, or whichever is nearest when the session has none.
    void CallCrewOut()
    {
        if (_crew != null && _crew.IsRepairing) return;
        if (_car == null) return;
        var crew = PitLane.Configured && PitLane.PlayerBox >= 0 ? PitCrewRegistry.ForBox(PitLane.PlayerBox) : null;
        if (crew == null) crew = PitCrewRegistry.Nearest(_car.transform.position);
        // A box mid-stop says no; ask again next frame rather than pull its men off somebody else's car.
        if (crew != null && crew.BeginRepair(_car.transform)) _crew = crew;
    }

    void StandCrewDown()
    {
        if (_crew != null) _crew.EndRepair();
        _crew = null;
    }
}
