using UnityEngine;
using Draftmaster.Sim;

// Four-tyre wear + temperature model. Each tyre (FL, FR, RL, RR) tracks accumulated wear (0..1) and a working
// temperature. Grip = wear-grip × temp-grip:
//   • wear drops grip linearly toward the vehicle's tireMinGrip as the tyre shreds,
//   • temperature has a window — cold tyres are greasy, an optimal band is full grip, overheated tyres go off.
// Cornering load transfers to the OUTSIDE tyres, so they heat and wear faster (visible in the UI as one side
// going red/worn first). Fed each physics step by PlayerVehicleController; read by it for per-axle grip and by
// the AI (SplineDriver / AIRacingBehaviour) for pace.
//
// Temperatures move SLOWLY. Rubber has thermal mass: a tyre takes most of a lap to come in and most of another
// to go cold, so the readout is something a driver manages over a stint rather than a needle that tracks the
// steering. thermalInertia below is that knob, and it is deliberately separate from heatRate/coolRate so that
// slowing the swing does not move the temperatures a hot lap settles at. The maths itself is in
// Draftmaster.Sim.TyreThermal, where it can be unit-tested.
public class TireModel : MonoBehaviour
{
    // Index: 0 = FL, 1 = FR, 2 = RL, 3 = RR.
    public const int FL = 0, FR = 1, RL = 2, RR = 3;

    // Rubber choice. Soft is the REFERENCE compound — its multipliers are 1.0, so every existing tuning value
    // in this file and in VehicleInfo still means what it used to. Hard trades a slice of peak grip and slower
    // warm-up for tyres that last.
    public enum Compound { Soft, Hard }

    [Header("Compound")]
    [Tooltip("Fitted compound. Soft = maximum grip, wears fast, warms quickly. Hard = a little less grip, warms slowly, lasts.")]
    public Compound compound = Compound.Soft;
    [Tooltip("Peak grip multiplier for the hard compound (soft = 1.0).")]
    [Range(0.85f, 1f)] public float hardGripScale = 0.955f;
    [Tooltip("Wear-rate multiplier for the hard compound (soft = 1.0). Below 1 = lasts longer.")]
    [Range(0.2f, 1f)] public float hardWearScale = 0.7f;
    [Tooltip("Heating multiplier for the hard compound (soft = 1.0). Below 1 = takes longer to switch on.")]
    [Range(0.4f, 1f)] public float hardHeatScale = 0.8f;

    [Header("State (read-only telemetry)")]
    [Range(0f, 1f)] public float[] wear = new float[4];
    public float[] tempC = new float[4];

    [Header("Temperature model (°C)")]
    public float ambientC = 25f;
    [Tooltip("Temperature of peak grip.")]
    public float optimalC = 90f;
    [Tooltip("At/below this temperature grip has fallen to coldGrip.")]
    public float coldC = 25f;
    [Tooltip("At/above this temperature grip has fallen to hotGrip.")]
    public float overheatC = 135f;
    [Range(0.5f, 1f)] public float coldGrip = 0.82f;
    [Range(0.5f, 1f)] public float hotGrip = 0.72f;
    [Tooltip("Heating per unit of tyre work per second.")]
    public float heatRate = 60f;
    [Tooltip("Cooling toward ambient per second (scaled up by airflow at speed). Sets, with heatRate, WHERE a tyre settles for a given amount of work — the ratio of the two is the equilibrium temperature.")]
    public float coolRate = 0.225f;
    [Tooltip("Extra cooling per (m/s) of airflow.")]
    public float airCool = 0.015f;
    [Tooltip("Thermal mass: divides heating AND cooling together, so the tyre takes longer to reach the SAME temperature. 1 = instant-feeling tyres that snap to temperature inside a corner; 8 = rubber that comes in over about a lap and goes cold about as slowly. Raise for lazier tyres; the settling temperatures don't move.")]
    [Range(1f, 20f)] public float thermalInertia = 8f;

    [Header("Scrub heat (steering)")]
    [Tooltip("Extra front-tyre heating at full scrub lock, in the same units as tyre work. This is what makes weaving down a straight warm the fronts up — lateral load alone can't tell a car sawing at the wheel from one tracking straight.")]
    public float scrubHeatFront = 0.35f;
    [Tooltip("Front-wheel angle (°) counting as full scrub. Small on purpose: an oval is steered in fractions of a degree, so measuring against the car's full lock would read every amount of steering as none.")]
    public float scrubFullLockDeg = 5f;
    [Tooltip("Share of the scrub heat the rear tyres get. The fronts do the scrubbing, so keep this well under 1 — it is the gap between the axles that shows up as a weave warming the fronts first.")]
    [Range(0f, 1f)] public float scrubHeatRearShare = 0.3f;

    [Header("Wear model")]
    [Tooltip("Grip floor at fully-worn tyre. Falls back to VehicleInfo.tireMinGrip.")]
    [Range(0.5f, 1f)] public float minWearGrip = 0.85f;
    [Tooltip("Multiplies the vehicle's tireWearRate.")]
    public float wearRateScale = 1f;
    [Tooltip("Base wear rate per unit work per second if no vehicle wear rate is set.")]
    public float baseWearRate = 0.0006f;
    [Tooltip("Temperature (°C) above which wear accelerates.")]
    public float overheatWearStartC = 110f;
    [Tooltip("Extra wear multiplier per °C above the overheat-wear threshold.")]
    public float overheatWearPerDeg = 0.03f;

    [Header("Load transfer")]
    [Tooltip("Fraction of axle load that shifts to the outside tyre at full lateral load.")]
    [Range(0f, 0.9f)] public float lateralTransfer = 0.5f;

    float _wearRate = 0.0006f;

    void Awake()
    {
        for (int i = 0; i < 4; i++) tempC[i] = ambientC;
        _wearRate = baseWearRate;
    }

    // Called by PlayerVehicleController with the vehicle's tyre params so grip floor + wear rate match the car.
    public void Configure(VehicleInfo vi)
    {
        if (vi == null) return;
        minWearGrip = vi.tireMinGrip;
        _wearRate = vi.tireWearRate > 0f ? vi.tireWearRate * wearRateScale : baseWearRate;
    }

    // frontWork / rearWork: 0..1 how hard each axle's tyres are working this step (slip force / grip ceiling).
    // latNorm: signed lateral load (+ = cornering loads the right-hand tyres). speedMps for friction heat + airflow.
    // steerDeg: front-wheel angle, for the scrub heat the fronts pick up from being turned.
    public void Tick(float dt, float frontWork, float rearWork, float speedMps, float latNorm, float steerDeg = 0f)
    {
        if (dt <= 0f) return;
        latNorm = Mathf.Clamp(latNorm, -1f, 1f) * lateralTransfer;
        float rightFrac = 0.5f + 0.5f * latNorm; // >0.5 when loaded right
        float leftFrac = 0.5f - 0.5f * latNorm;

        // Per-tyre work: axle work split L/R by load, ×2 so the two sides average back to the axle work.
        float wFL = frontWork * 2f * leftFrac;
        float wFR = frontWork * 2f * rightFrac;
        float wRL = rearWork * 2f * leftFrac;
        float wRR = rearWork * 2f * rightFrac;

        // Scrub heat from steering, split L/R on the same load fractions so the loaded outside tyre takes the
        // bigger share of it. Heat only — the wear numbers are tuned against lateral work and stay that way.
        float scrub = TyreThermal.Scrub01(steerDeg, scrubFullLockDeg);
        float scrubF = scrubHeatFront * scrub;
        float scrubR = scrubF * scrubHeatRearShare;

        StepTyre(FL, wFL, scrubF * 2f * leftFrac, dt, speedMps);
        StepTyre(FR, wFR, scrubF * 2f * rightFrac, dt, speedMps);
        StepTyre(RL, wRL, scrubR * 2f * leftFrac, dt, speedMps);
        StepTyre(RR, wRR, scrubR * 2f * rightFrac, dt, speedMps);
    }

    void StepTyre(int i, float work, float scrubWork, float dt, float speedMps)
    {
        work = Mathf.Max(0f, work);

        // Heat: friction power ≈ (work + scrub) × speed, cooling toward ambient and faster with airflow, the
        // whole exchange slowed by the tyre's thermal mass.
        float heatWork = (work + Mathf.Max(0f, scrubWork)) * CompoundHeat;
        tempC[i] = TyreThermal.Step(tempC[i], ambientC, heatWork, speedMps,
                                    heatRate, coolRate, airCool, thermalInertia, dt);

        // Wear: scales with work and accelerates when the tyre runs hot.
        float overheat = 1f + Mathf.Max(0f, tempC[i] - overheatWearStartC) * overheatWearPerDeg;
        wear[i] = Mathf.Clamp01(wear[i] + _wearRate * CompoundWear * TrackConditions.TireWearMultiplier * work * overheat * dt * 60f);
    }

    // Compound multipliers. Soft is the reference (1.0) so the tuned defaults keep their meaning.
    public float CompoundGrip => compound == Compound.Hard ? hardGripScale : 1f;
    public float CompoundWear => compound == Compound.Hard ? hardWearScale : 1f;
    public float CompoundHeat => compound == Compound.Hard ? hardHeatScale : 1f;

    // Fit a compound. Called by the pre-race setup panel and by a pit stop that changes rubber; resets the
    // tyres because you can't swap compound without swapping the tyre.
    public void SetCompound(Compound c)
    {
        compound = c;
        PitReset();
    }

    public float TyreGrip(int i)
    {
        float wg = Mathf.Lerp(1f, minWearGrip, Mathf.Clamp01(wear[i]));
        return wg * TempGrip(tempC[i]) * CompoundGrip;
    }

    float TempGrip(float t)
    {
        if (t <= optimalC)
            return Mathf.Lerp(coldGrip, 1f, Mathf.Clamp01(Mathf.InverseLerp(coldC, optimalC, t)));
        return Mathf.Lerp(1f, hotGrip, Mathf.Clamp01(Mathf.InverseLerp(optimalC, overheatC, t)));
    }

    public float AxleGripFront => 0.5f * (TyreGrip(FL) + TyreGrip(FR));
    public float AxleGripRear => 0.5f * (TyreGrip(RL) + TyreGrip(RR));
    public float OverallGrip => 0.25f * (TyreGrip(FL) + TyreGrip(FR) + TyreGrip(RL) + TyreGrip(RR));
    public float FrontWear => 0.5f * (wear[FL] + wear[FR]);
    public float RearWear => 0.5f * (wear[RL] + wear[RR]);

    public void PitReset()
    {
        for (int i = 0; i < 4; i++) { wear[i] = 0f; tempC[i] = ambientC; }
    }
}
