using UnityEngine;

// How much of the tyre's peak friction the car can actually hold in a steady corner.
//
// The AI plans corner speeds from v = sqrt(R * aLat). It used to take aLat as the PEAK friction —
// maxLateralG x the track's grip multipliers — which is what the friction circle in PlayerVehicleController
// caps each axle at. But the car never gets there in a real corner: the steering is wound back at speed,
// the axles are biased toward understeer, and the rear lets go before the front has used everything. On a
// skid pad the Cup car holds about 81% of peak up to 55 m/s and less above it as the steering authority runs
// out. Planning off the peak put every fast corner 10-20% over what the car could take, and practice at
// Watkins Glen strewed cars across the run-off at the same four corners every lap.
//
// These are the skid-pad numbers (AIGripCalibrationTests), a little under what was measured so there is
// margin for turning in rather than sitting in a steady circle. That test fails if the physics changes enough
// that they stop being true, so a handling change can't quietly put the AI back over the limit.
public static class AIGrip
{
    // Measured at these speeds (m/s); the fraction of peak lateral grip usable at each.
    static readonly float[] Speeds = { 25f, 35f, 45f, 55f, 65f };
    static readonly float[] Fractions = { 0.79f, 0.80f, 0.80f, 0.80f, 0.72f };
    const float Floor = 0.55f;

    public static float UsableFraction(float speedMps)
    {
        if (speedMps <= Speeds[0]) return Fractions[0];
        for (int i = 1; i < Speeds.Length; i++)
        {
            if (speedMps <= Speeds[i])
                return Mathf.Lerp(Fractions[i - 1], Fractions[i], (speedMps - Speeds[i - 1]) / (Speeds[i] - Speeds[i - 1]));
        }
        // Past the last point, carry on down at the same rate — the steering keeps losing authority.
        int n = Speeds.Length - 1;
        float slope = (Fractions[n] - Fractions[n - 1]) / (Speeds[n] - Speeds[n - 1]);
        return Mathf.Max(Floor, Fractions[n] + slope * (speedMps - Speeds[n]));
    }

    // Lateral acceleration (m/s²) the car can hold at this speed, given its peak.
    public static float Usable(float peakMps2, float speedMps) => peakMps2 * UsableFraction(speedMps);

    // The steady speed (m/s) the car can take a corner of this radius at, given its peak lateral acceleration.
    // The usable fraction depends on the speed that comes out, so it is found by a few fixed-point steps —
    // the fraction moves slowly with speed, so it settles almost at once.
    public static float CornerSpeed(float radius, float peakMps2)
    {
        if (radius <= 0f || peakMps2 <= 0f) return 0f;
        float v = Mathf.Sqrt(radius * peakMps2 * Fractions[0]);
        for (int i = 0; i < 4; i++) v = Mathf.Sqrt(radius * Usable(peakMps2, v));
        return v;
    }
}
