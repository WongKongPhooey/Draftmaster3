using UnityEngine;

namespace Draftmaster.Sim
{
    // When a car takes a hit, small bits of it come off: flakes of paint, splinters of fibreglass, a clip or
    // two. They drop off the bodywork where it folded and are left lying on the track as the car drives on.
    // A panel that has been knocked loose keeps shedding for a few seconds afterwards, less and less, so a
    // car limping away from a shunt trails a few bits behind it. The numbers live here, out of the FX
    // component, so they can be pinned in EditMode:
    //
    //   * Pieces — how many bits a dent knocks off. Scaled by how much the denting grew (VehicleDamage's
    //     DentAmount, 0..1), with the fraction carried over so a crush that folds the metal a little every
    //     step sheds the same total as one hit that folds it all at once.
    //   * Loosen / Settle — how loose the damaged panel is: every dent loosens it, and it settles back with
    //     a half-life, so the trickle after a hit dies away instead of running forever.
    //   * TrickleRate — bits per second while the panel is loose. A parked car doesn't shake bits off; a
    //     car at speed does.
    public static class BodyworkDebris
    {
        // Bits knocked off by a dent that grew DentAmount by `dentGained`. `carry` holds the fractional
        // piece between calls; at most `maxBurst` come off at once (the remainder is dropped, not banked,
        // so one huge shunt doesn't spill bits for the next ten frames).
        public static int Pieces(ref float carry, float dentGained, float piecesPerDent, int maxBurst)
        {
            if (dentGained <= 0f || piecesPerDent <= 0f || maxBurst <= 0) return 0;
            carry += dentGained * piecesPerDent;
            int n = Mathf.FloorToInt(carry);
            if (n <= 0) return 0;
            carry -= n;
            if (n > maxBurst) { n = maxBurst; carry = 0f; }
            return n;
        }

        // How loose the bodywork is after a dent that grew DentAmount by `dentGained`, 0..1.
        public static float Loosen(float loose, float dentGained, float loosenPerDent)
        {
            if (dentGained <= 0f || loosenPerDent <= 0f) return Mathf.Clamp01(loose);
            return Mathf.Clamp01(loose + dentGained * loosenPerDent);
        }

        // Looseness after `dt` seconds of settling with the given half-life.
        public static float Settle(float loose, float dt, float halfLife)
        {
            if (loose <= 0f) return 0f;
            if (halfLife <= 0f) return 0f;
            return loose * Mathf.Pow(0.5f, Mathf.Max(0f, dt) / halfLife);
        }

        // Bits per second shaken off a panel this loose at this speed. Nothing below `minSpeed`; full
        // `maxRate` once the panel is fully loose and the car is at `fullSpeed`.
        public static float TrickleRate(float loose, float speedMps, float minSpeed, float fullSpeed, float maxRate)
        {
            if (loose <= 0f || maxRate <= 0f || speedMps <= minSpeed) return 0f;
            float s = fullSpeed > minSpeed ? Mathf.Clamp01((speedMps - minSpeed) / (fullSpeed - minSpeed)) : 1f;
            return maxRate * Mathf.Clamp01(loose) * s;
        }
    }
}
