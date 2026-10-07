using UnityEngine;

namespace Draftmaster.Sim
{
    // What a banked turn does for a car, in a top-down game that has no third dimension to put the bank in.
    //
    // Two things, both real:
    //   * Gravity has a component down the slope - toward the inside of the turn on an oval - of g·sinθ. It carries
    //     part of the cornering load for free.
    //   * Cornering pushes the car INTO the banking, so the tyres carry more than the car's weight: the normal load
    //     goes from g·cosθ (just leaning on it) to g·cosθ + a·sinθ (being thrown into it), and the tyres' grip
    //     scales with it.
    // Together a = μg·(cosθ + a·sinθ/g) + g·sinθ. With the game's grip (μ around 2) at Daytona's 31° that has no
    // finite answer - the more it corners, the more it grips - so the load factor is capped. The cap is what
    // makes a superspeedway turn flat out without making it limitless.
    //
    // PlayerVehicleController applies this to every car's physics; SplineDriver and SplineInputDriver plan the AI's
    // corner speeds off the same numbers, so the AI asks for exactly what the physics will hold.
    public static class BankedGrip
    {
        public const float G = 9.81f;

        // Most the tyres' load can be multiplied by being thrown into the banking.
        public const float MaxLoadFactor = 1.3f;

        // Tyre normal load as a multiple of the car's weight, cornering at lateralAccel (m/s²) on bankDeg.
        public static float LoadFactor(float bankDeg, float lateralAccel)
        {
            float rad = Mathf.Abs(bankDeg) * Mathf.Deg2Rad;
            return Mathf.Clamp(Mathf.Cos(rad) + Mathf.Sin(rad) * Mathf.Abs(lateralAccel) / G, 0.5f, MaxLoadFactor);
        }

        // Gravity's pull down the bank (m/s²), toward the inside of the turn.
        public static float DownslopeAccel(float bankDeg) => G * Mathf.Sin(Mathf.Abs(bankDeg) * Mathf.Deg2Rad);

        // The lateral acceleration (m/s²) a car with flatPeak (μg on level ground) can hold on bankDeg: the tyres
        // at their banked load plus gravity's share. Equal to flatPeak on the level.
        public static float Capacity(float flatPeak, float bankDeg)
        {
            if (flatPeak <= 0f) return 0f;
            if (Mathf.Abs(bankDeg) < 0.01f) return flatPeak;
            float a = flatPeak;
            for (int i = 0; i < 12; i++) a = flatPeak * LoadFactor(bankDeg, a) + DownslopeAccel(bankDeg);
            return a;
        }

        // How much a bank multiplies what the car can hold - for planning code that scales a flat-ground figure.
        public static float CapacityScale(float flatPeak, float bankDeg)
            => flatPeak > 0f ? Capacity(flatPeak, bankDeg) / flatPeak : 1f;
    }
}
