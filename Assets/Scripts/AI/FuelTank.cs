using UnityEngine;

// Fuel load for a car. Burns with throttle + speed, adds weight (a full tank is heavy and slow), and cuts the
// engine when dry. Refilled in the pits. Read by PlayerVehicleController for mass + power, by the pit strategy
// for when to stop, and by the HUD for the gauge.
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

    public float FuelKg => Mathf.Max(0f, fuelLitres) * kgPerLitre;
    public float Fraction => capacityLitres > 0f ? Mathf.Clamp01(fuelLitres / capacityLitres) : 0f;
    public bool IsEmpty => fuelLitres <= 0.001f;

    public void Consume(float throttle01, float speedMps, float dt)
    {
        if (dt <= 0f) return;
        float load = idleBurn + litresPerSecAtFull * Mathf.Clamp01(throttle01) * (0.4f + 0.6f * Mathf.Clamp01(speedMps / 60f));
        fuelLitres = Mathf.Max(0f, fuelLitres - load * dt);
    }

    public void Refuel(float litres) => fuelLitres = Mathf.Clamp(fuelLitres + Mathf.Max(0f, litres), 0f, capacityLitres);
    public void FillFull() => fuelLitres = capacityLitres;
}
