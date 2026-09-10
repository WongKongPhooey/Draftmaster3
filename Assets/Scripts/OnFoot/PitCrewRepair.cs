using UnityEngine;

// The crew straightening a wrecked car in its box, over minutes rather than in a frame.
//
// A pit stop hands the car back whole the instant the jack drops, because a stop is a stop: four tyres, a
// fuel can, and the driver never gets out. This is the other thing that happens to a damaged car — it gets
// dragged in, the driver climbs out, and the crew spend the rest of the session beating the panels back
// out. Being able to watch that happen is the point. A tow that returned a straight car would make putting
// it in the wall free, and the player would take the wall over the lap time every time.
//
// Self-installing: PitLaneStart.TowToPits calls Begin(car) and this attaches itself. Nothing to wire.
public class PitCrewRepair : MonoBehaviour
{
    [Tooltip("Seconds the crew take to bring a completely destroyed car back to straight. A lightly " +
             "creased one is proportionally quicker, so the wait is what the damage was worth.")]
    public float secondsForFullRepair = 150f;

    [Tooltip("Damage level at or below which the car counts as repaired and the crew stand down.")]
    public float doneBelow = 0.02f;

    [Tooltip("Stop work while the player is sat in the car. Nobody welds around a driver, and a car being " +
             "repaired while it is being driven would repair itself on the way down the back straight.")]
    public bool pauseWhileDriven = true;

    PlayerVehicleController _car;
    VehicleDamage _bodywork;

    // How bad it was when they started, so the readout can say how far through they are rather than just
    // how bad it still is.
    float _startedAt;

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
    public static PitCrewRepair Begin(PlayerVehicleController car)
    {
        if (car == null) return null;

        var repair = car.GetComponent<PitCrewRepair>();
        if (repair == null) repair = car.gameObject.AddComponent<PitCrewRepair>();

        repair._car = car;
        repair._bodywork = car.GetComponentInChildren<VehicleDamage>();
        repair._startedAt = repair._bodywork != null ? repair._bodywork.DamageLevel : 0f;
        repair.enabled = true;
        Active = repair;
        return repair;
    }

    void OnDestroy() { if (Active == this) Active = null; }

    void Update()
    {
        if (_bodywork == null) { Finish(); return; }

        // The driver is back in it. Work stops until they are out again — which also means a driver who
        // climbs in early and goes back out is taking a bent car with them, exactly as they should.
        if (pauseWhileDriven && _car != null && _car.enabled) return;

        if (_bodywork.DamageLevel <= doneBelow) { _bodywork.RepairFull(); Finish(); return; }

        // A fixed fraction of what is LEFT each second would approach straight without ever arriving, so
        // the rate is set against the damage the crew started with. That makes the wait proportional to
        // how hard the car was hit, which is the honest version of this.
        float perSecond = Mathf.Max(0.0001f, _startedAt) / Mathf.Max(1f, secondsForFullRepair);
        float remaining = Mathf.Max(0.0001f, _bodywork.DamageLevel);
        _bodywork.Repair(Mathf.Clamp01(perSecond * Time.deltaTime / remaining));
    }

    void Finish()
    {
        if (Active == this) Active = null;
        enabled = false;
    }
}
