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

        // ---- Across the width: progressive banking ----
        //
        // A real oval's banking is not one flat plane. The surface curves up from the apron to the wall, so the top
        // groove sits on steeper banking than the bottom (Bristol 24-30, Homestead 18-20, Kansas 17-20). A track's
        // published figure is taken as the middle of the road; the bottom edge is ProgressiveSpread/2 of it flatter
        // and the top edge as much steeper. Proportional, so the steeper the track the more the high line gains:
        // a couple of degrees at Daytona, a fraction of one at a flat track.
        //
        // TrackBuilder.BankAt hands the physics the banking under the car's actual lateral position, and SplineDriver
        // plans each AI line's corner speeds off the banking under that line, so the outside lane plans what the
        // outside lane holds and the inside lane no more than the inside holds.
        public static float ProgressiveSpread = 0.2f;

        // Banking (degrees) at acrossFrac of the way across the road from the bottom (inside) edge (0) to the top
        // (outside) edge (1). acrossFrac 0.5 - the middle of the road - is bankDeg itself.
        public static float BankAcross(float bankDeg, float acrossFrac)
            => bankDeg * (1f + ProgressiveSpread * (Mathf.Clamp01(acrossFrac) - 0.5f));

        // ---- Running off the banking ----
        //
        // Height (m) of the road surface acrossFrac of the way up from its bottom edge, on a road widthM wide
        // banked bankDeg at its middle: the integral of sin(bank) up the progressive cross-section. The bottom edge
        // is at 0 - level with the apron and the flat infield.
        //
        // Where a turn's banking eases out onto a flatter straight, the top of the road comes down and the bottom
        // stays where it is, so a car up the banking runs downhill along the track as it exits - gravity hands it
        // the height back as speed - and one high on the way in climbs and pays for it. The gradient of this along
        // the lap is what TrackBuilder.BankAt turns into a fore-aft pull. It is the same surface whose sideways
        // slope is DownslopeAccel(BankAcross(...)), so the two forces together are gravity on one real surface:
        // nothing is gained round a closed lap that was not paid for in height.
        public static float SurfaceHeight(float bankDeg, float acrossFrac, float widthM)
        {
            if (widthM <= 0f) return 0f;
            float u = Mathf.Clamp01(acrossFrac) * widthM;
            float b0 = Mathf.Abs(BankAcross(bankDeg, 0f)) * Mathf.Deg2Rad;
            float b1 = Mathf.Abs(BankAcross(bankDeg, 1f)) * Mathf.Deg2Rad;
            float beta = (b1 - b0) / widthM;   // bank (rad) gained per metre up the road
            if (Mathf.Abs(beta) < 1e-6f) return u * Mathf.Sin(b0);
            return (Mathf.Cos(b0) - Mathf.Cos(b0 + beta * u)) / beta;
        }

        // Gravity's pull ALONG the lap (m/s², + = forward) on a car acrossFrac up a road widthM wide, given the
        // nominal banking a step behind (bankBehindDeg) and a step ahead (bankAheadDeg), stepM apart in total.
        // Positive where the banking flattens ahead - the exit, for a car up the bank.
        public static float AlongTrackAccel(float bankBehindDeg, float bankAheadDeg, float stepM, float acrossFrac, float widthM)
        {
            if (stepM <= 0f) return 0f;
            float dh = SurfaceHeight(bankAheadDeg, acrossFrac, widthM) - SurfaceHeight(bankBehindDeg, acrossFrac, widthM);
            float slope = dh / stepM;
            // Along a slope of rise/run `slope`, gravity's component down it is g·sin(atan(slope)).
            return -G * slope / Mathf.Sqrt(1f + slope * slope);
        }
    }
}
