using UnityEngine;

namespace Draftmaster.Sim
{
    // Re-opening the setup sheet mid-session (back in the car after a tow, in practice or qualifying) must not
    // be a free pit stop: only what the driver changed goes on the car. Balance and steering are settings and
    // always apply; tyres and fuel are things the car has been using up, so they are only touched when the
    // driver asked. CarSetup.ApplyChangesTo reads these.
    public static class SetupAdjustment
    {
        // A fuel slider nudged by less than this has not been moved — the panel snaps to whole litres.
        public const float FuelStepLitres = 0.5f;

        // A different compound is a new set of it; the same compound keeps the worn set on the car.
        public static bool FitsNewTyres(int compoundBefore, int compoundAfter) => compoundBefore != compoundAfter;

        // A moved slider is the crew filling (or draining) to the new load; an untouched one keeps what is left.
        public static bool ChangesFuel(float litresBefore, float litresAfter) =>
            Mathf.Abs(litresAfter - litresBefore) >= FuelStepLitres;
    }
}
