using UnityEngine;

// Four-tyre wear + temperature model. Each tyre (FL, FR, RL, RR) tracks accumulated wear (0..1) and a working
// temperature. Grip = wear-grip × temp-grip:
//   • wear drops grip linearly toward the vehicle's tireMinGrip as the tyre shreds,
//   • temperature has a window — cold tyres are greasy, an optimal band is full grip, overheated tyres go off.
// Cornering load transfers to the OUTSIDE tyres, so they heat and wear faster (visible in the UI as one side
// going red/worn first). Fed each physics step by PlayerVehicleController; read by it for per-axle grip and by
// the AI (SplineDriver / AIRacingBehaviour) for pace.
public class TireModel : MonoBehaviour
{
    // Index: 0 = FL, 1 = FR, 2 = RL, 3 = RR.
    public const int FL = 0, FR = 1, RL = 2, RR = 3;

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
    public float heatRate = 120f;
    [Tooltip("Cooling toward ambient per second (scaled up by airflow at speed).")]
    public float coolRate = 0.45f;
    [Tooltip("Extra cooling per (m/s) of airflow.")]
    public float airCool = 0.015f;

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
    public void Tick(float dt, float frontWork, float rearWork, float speedMps, float latNorm)
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

        StepTyre(FL, wFL, dt, speedMps);
        StepTyre(FR, wFR, dt, speedMps);
        StepTyre(RL, wRL, dt, speedMps);
        StepTyre(RR, wRR, dt, speedMps);
    }

    void StepTyre(int i, float work, float dt, float speedMps)
    {
        work = Mathf.Max(0f, work);

        // Heat: friction power ≈ work × speed. Cool toward ambient, faster with airflow.
        float heat = heatRate * work * (0.25f + speedMps / 45f);
        float cool = coolRate * (tempC[i] - ambientC) * (1f + speedMps * airCool);
        tempC[i] += (heat - cool) * dt;
        if (tempC[i] < ambientC) tempC[i] = ambientC;

        // Wear: scales with work and accelerates when the tyre runs hot.
        float overheat = 1f + Mathf.Max(0f, tempC[i] - overheatWearStartC) * overheatWearPerDeg;
        wear[i] = Mathf.Clamp01(wear[i] + _wearRate * TrackConditions.TireWearMultiplier * work * overheat * dt * 60f);
    }

    public float TyreGrip(int i)
    {
        float wg = Mathf.Lerp(1f, minWearGrip, Mathf.Clamp01(wear[i]));
        return wg * TempGrip(tempC[i]);
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
