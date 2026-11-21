using UnityEngine;

// Fuel load for a car. In a race it burns for the distance driven, scaled by throttle, at a rate planned at the green
// so a full tank lasts the race plus a couple of laps flat out (Draftmaster.Sim.FuelPlan, TrackConditions.FuelRequiredStops
// / FuelSpareLaps). Dry, the engine cuts (PlayerVehicleController.FuelDry). Refilled in the pits. The AI driver lifts to
// save fuel when it would not make the finish (SplineInputDriver).
public class FuelTank : MonoBehaviour
{
    [Tooltip("Tank capacity in litres.")]
    public float capacityLitres = 100f;
    [Tooltip("Current fuel in litres.")]
    public float fuelLitres = 100f;
    [Tooltip("Burn (litres/sec) at full throttle and racing speed.")]
    public float litresPerSecAtFull = 0.06f;
    [Tooltip("Idle/part-throttle burn (litres/sec).")]
    public float idleBurn = 0.004f;
    [Tooltip("Mass per litre (kg). Race fuel ≈ 0.75.")]
    public float kgPerLitre = 0.75f;

    // The driver's setup put this load in (CarSetup.ApplyTo): the race starts on it rather than brimmed. Cars nobody
    // set up - the AI field - still start the race full.
    [System.NonSerialized] public bool loadChosen;

    public float FuelKg => Mathf.Max(0f, fuelLitres) * kgPerLitre;
    public float Fraction => capacityLitres > 0f ? Mathf.Clamp01(fuelLitres / capacityLitres) : 0f;
    public bool IsEmpty => fuelLitres <= 0.001f;

    public void Consume(float throttle01, float speedMps, float dt)
    {
        if (dt <= 0f) return;
        float load = idleBurn + litresPerSecAtFull * Mathf.Clamp01(throttle01) * (0.4f + 0.6f * Mathf.Clamp01(speedMps / 60f));
        fuelLitres = Mathf.Max(0f, fuelLitres - load * TrackConditions.FuelUseMultiplier * dt);
    }

    // ---- Race fuel (Draftmaster.Sim.FuelPlan) ----
    // Planned once at the green of a race: the full-throttle burn per metre that makes a full tank last one stint of
    // the race plus TrackConditions.FuelSpareLaps. Nothing burns until then (practice, the formation lap).
    float _litresPerMetreFull;
    public bool Planned => _litresPerMetreFull > 0f;
    public float LitresPerLapFull { get; private set; }

    public void PlanForRace(int raceLaps, float lapLengthM)
    {
        _litresPerMetreFull = Draftmaster.Sim.FuelPlan.LitresPerMetreFull(capacityLitres, raceLaps, lapLengthM,
            TrackConditions.FuelRequiredStops, TrackConditions.FuelSpareLaps);
        LitresPerLapFull = _litresPerMetreFull * lapLengthM;
    }

    public void ClearPlan() { _litresPerMetreFull = 0f; LitresPerLapFull = 0f; }

    // A race is on (green or caution, a real race session with a lap count): plan the tank at the first step of it,
    // then burn for the distance actually driven. Measured off the transform so it counts the same whether the car
    // is on its dynamic model or driven kinematically behind the safety car (whose throttle is taken as a cruise).
    [Tooltip("Throttle assumed for a car being driven kinematically (behind the safety car, the pace lap).")]
    [Range(0f, 1f)] public float kinematicThrottle = 0.3f;
    PlayerVehicleController _pvc;
    SplineDriver _spline;
    Vector2 _lastPos;
    bool _hasLastPos;

    void FixedUpdate()
    {
        var rd = RaceDirector.Instance;
        bool raceSession = rd != null && rd.isActiveAndEnabled && rd.raceLaps > 0 && !RaceWeekend.IsPracticeLike;
        bool race = raceSession && RaceStart.IsRunning;
        Vector2 pos = transform.position;
        float moved = _hasLastPos ? (pos - _lastPos).magnitude : 0f;
        _lastPos = pos;
        _hasLastPos = true;
        // On the grid and the formation lap of a race the car already carries what it will start on: the driver's
        // chosen load, or brimmed for a car nobody set up.
        if (raceSession && !race && !Planned) { if (!loadChosen) FillFull(); return; }
        if (_pvc == null) _pvc = GetComponent<PlayerVehicleController>();
        if (_spline == null) _spline = GetComponent<SplineDriver>();
        if (!race)
        {
            BurnInPractice(moved);
            return;
        }
        if (!Planned)
        {
            float lap = _spline != null && _spline.TrackLength > 0f ? _spline.TrackLength
                      : _pvc != null ? DraftAero.TrackLengthFor(_pvc.track) : 0f;
            if (lap <= 0f) return;
            PlanForRace(rd.raceLaps, lap);
            // A race starts on the load the driver chose in the setup (10-100 L); a car nobody set up starts brimmed.
            if (!loadChosen) FillFull();
        }
        // A jump (a re-seed, a team switch, a tow back to the pits) is not driving.
        if (moved > 20f) return;
        float throttle = _pvc != null && _pvc.enabled ? _pvc.ThrottleInput : kinematicThrottle;
        BurnDistance(throttle, moved);
    }

    // Practice and qualifying: there is no race to plan against, so the player's car burns by the clock at the
    // tank's own rates (litresPerSecAtFull, idleBurn - Consume) whenever it is rolling - the setup's fuel load
    // (1-20 L) is a few laps' worth, and the HUD gauge drains with it. The player's car only: an AI stint has no
    // fuel stop in it, and an AI car run dry would sit out on the circuit.
    void BurnInPractice(float moved)
    {
        if (!RaceWeekend.IsPracticeLike || _pvc == null || _pvc != PlayerVehicleController.Local) return;
        float dt = Time.fixedDeltaTime;
        // Parked (in the box, on the grid) burns nothing; a jump (re-seed, tow back to the pits) is not driving.
        if (dt <= 0f || moved < 0.01f || moved > 20f) return;
        float throttle = _pvc.enabled ? _pvc.ThrottleInput : kinematicThrottle;
        Consume(throttle, moved / dt, dt);
    }

    // Burn for metres driven at this throttle (planned races only).
    public void BurnDistance(float throttle01, float metres)
    {
        if (!Planned || metres <= 0f) return;
        float litres = _litresPerMetreFull * Draftmaster.Sim.FuelPlan.BurnShare(throttle01) * metres * TrackConditions.FuelUseMultiplier;
        fuelLitres = Mathf.Max(0f, fuelLitres - litres);
    }

    public void Refuel(float litres) => fuelLitres = Mathf.Clamp(fuelLitres + Mathf.Max(0f, litres), 0f, capacityLitres);
    public void FillFull() => fuelLitres = capacityLitres;
}
