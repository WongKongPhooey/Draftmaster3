using UnityEngine;

namespace Draftmaster.Sim
{
    // Racecraft maths shared by the AI brains, kept free of MonoBehaviour state so it can be unit-tested
    // in EditMode (the racing itself can only be judged in Play Mode, which isn't always available).
    //
    // Three ideas live here:
    //   * Race phase — a field that races identically on lap 1 and on the last lap reads as robotic. Drivers
    //     settle in early (wider gaps, fewer lunges) and throw everything at it over the closing laps.
    //   * Pressure and wear — a mistake is far likelier with a rival filling the mirrors on worn tyres than
    //     it is in clean air, so the error roll keys off both instead of being a flat per-second dice throw.
    //   * Blue flags — a car a lap down that races the leader is the single most immersion-breaking thing an
    //     AI field does. Lapped traffic yields instead.
    public static class RaceCraft
    {
        // ---- Race phase ----

        // Opening share of the race distance run conservatively.
        public const float SettleFraction = 0.15f;
        // Closing share of the race distance run flat out.
        public const float ChargeFraction = 0.25f;
        // Aggression multiplier at the green flag and at the checkered.
        public const float OpeningAggressionScale = 0.55f;
        public const float ClosingAggressionScale = 1.45f;
        // Following-gap multiplier at the green flag and at the checkered (>1 = hang further back).
        public const float OpeningFollowMargin = 1.22f;
        public const float ClosingFollowMargin = 0.85f;

        // Race length is only known in a single-player race with a RaceDirector. Practice, qualifying and
        // multiplayer report unknown; those sessions get the neutral mid-race envelope rather than an
        // eternal opening lap.
        public const float UnknownProgress = -1f;
        public const float NeutralProgress = 0.5f;

        public static float NormaliseProgress(float rawProgress01)
            => rawProgress01 < 0f ? NeutralProgress : Mathf.Clamp01(rawProgress01);

        // 0..1 ramp across the race: OpeningAggressionScale at the green, 1 through the middle stint,
        // ClosingAggressionScale at the flag. Result stays a valid 0..1 aggression.
        public static float PhaseAggression(float baseAggression01, float raceProgress01)
            => Mathf.Clamp01(Mathf.Clamp01(baseAggression01) * PhaseScale(raceProgress01,
                   OpeningAggressionScale, 1f, ClosingAggressionScale));

        // Multiplier on the gap the AI insists on holding to the car ahead. Mirrors the aggression
        // envelope inverted — cautious early means a longer safety margin, not just fewer passes.
        public static float PhaseFollowMargin(float raceProgress01)
            => PhaseScale(raceProgress01, OpeningFollowMargin, 1f, ClosingFollowMargin);

        // Piecewise ramp: opening → mid over SettleFraction, mid → closing over the last ChargeFraction.
        static float PhaseScale(float raceProgress01, float opening, float mid, float closing)
        {
            float p = Mathf.Clamp01(raceProgress01);
            if (p < SettleFraction) return Mathf.Lerp(opening, mid, p / SettleFraction);
            float chargeStart = 1f - ChargeFraction;
            if (p > chargeStart) return Mathf.Lerp(mid, closing, (p - chargeStart) / ChargeFraction);
            return mid;
        }

        // ---- Mistakes ----

        // Even a metronome cracks eventually, so consistency buys at most this much of the base rate away.
        // Without the ceiling a consistency-1.0 driver could never make an error, however hard they're leaned on.
        public const float MaxConsistencyRelief = 0.9f;
        // How much a rival on the bumper multiplies the error rate at full pressure.
        public const float PressureWeight = 1.6f;
        // How much fully worn tyres multiply the error rate.
        public const float WearWeight = 1.2f;

        // 0 = nobody near, 1 = filling the mirrors. Negative gaps (car alongside/ahead) count as full pressure.
        public static float Pressure01(float gapBehindMetres, float pressureRange)
        {
            if (pressureRange <= 0f) return 0f;
            if (gapBehindMetres <= 0f) return 1f;
            return Mathf.Clamp01(1f - gapBehindMetres / pressureRange);
        }

        // Per-second probability of a driver error. gripLoss01 is 1 - tyre grip multiplier.
        public static float MistakeChancePerSecond(float baseRate, float consistency01, float pressure01, float gripLoss01)
        {
            if (baseRate <= 0f) return 0f;
            float skill = 1f - Mathf.Clamp01(consistency01) * MaxConsistencyRelief;
            float heat = 1f + PressureWeight * Mathf.Clamp01(pressure01);
            float wear = 1f + WearWeight * Mathf.Clamp01(gripLoss01);
            return Mathf.Max(0f, baseRate * skill * heat * wear);
        }

        // ---- Blue flags ----

        // Should we get out of the way? Only for a car strictly further round the race than us that is
        // closing from behind and within waving distance.
        public static bool ShouldYield(int myLap, int lapperLap, float gapBehindMetres, float yieldRange)
            => lapperLap > myLap && gapBehindMetres >= 0f && gapBehindMetres <= yieldRange && yieldRange > 0f;

        // How firmly to move over: nothing at the edge of the range, full commitment on the bumper.
        public static float YieldStrength01(float gapBehindMetres, float yieldRange)
        {
            if (yieldRange <= 0f) return 0f;
            return Mathf.Clamp01(1f - Mathf.Max(0f, gapBehindMetres) / yieldRange);
        }

        // Which way to step aside: away from the line the lapper is already using. Dead heat (same
        // lateral) breaks toward the outside of where we sit, so two lapped cars don't both pick centre.
        public static float YieldDirection(float myLateral, float lapperLateral)
        {
            float delta = myLateral - lapperLateral;
            if (Mathf.Abs(delta) < 0.05f) return myLateral >= 0f ? 1f : -1f;
            return delta > 0f ? 1f : -1f;
        }

        // Pace multiplier while yielding: a small lift so the pass actually completes, never a parked car.
        // liftFactor is the multiplier applied at full strength (e.g. 0.94 = 6% off the pace).
        public static float YieldSpeedFactor(float strength01, float liftFactor)
            => Mathf.Lerp(1f, Mathf.Clamp(liftFactor, 0.5f, 1f), Mathf.Clamp01(strength01));
    }
}
