using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // What a team's cards do to the race, as plain numbers. The race host applies them to the player's car,
    // pit crew and fuel tank; nothing here touches the scene.
    //
    // Deliberately modest: a car card moves power, grip and draft by at most CarSwing (3%), a crew chief moves
    // pit-stop time by at most 10% and fuel burn / tyre wear by at most 5%. A good team helps; it doesn't
    // drive the car.
    public static class TeamEffects
    {
        public const float CarSwing = 0.03f;
        public const int CarNeutral = 75;          // a car stat of 75 is stock
        public const int CarRange = 25;            // ±25 from neutral reaches the full swing

        public const float PitSwing = 0.10f;
        public const float FuelSwing = 0.05f;
        public const float WearSwing = 0.05f;
        public const int ChiefNeutral = 50;
        public const int ChiefRange = 50;

        static float Unit(int stat, int neutral, int range) =>
            Math.Max(-1f, Math.Min(1f, (stat - neutral) / (float)range));

        static float CarScale(int stat) => 1f + Unit(stat, CarNeutral, CarRange) * CarSwing;

        // Car card. Null (no card) = stock.
        public static float PowerScale(CardDefinition car) => car == null ? 1f : CarScale(car.power);
        public static float GripScale(CardDefinition car) => car == null ? 1f : CarScale(car.handling);
        public static float DraftScale(CardDefinition car) => car == null ? 1f : CarScale(car.draft);

        // Crew chief card: a faster stop, less fuel burned, less tyre wear. All multipliers on the stock value.
        public static float PitTimeScale(CardDefinition chief) =>
            chief == null ? 1f : 1f - Unit(chief.pitSpeed, ChiefNeutral, ChiefRange) * PitSwing;
        public static float FuelBurnScale(CardDefinition chief) =>
            chief == null ? 1f : 1f - Unit(chief.strategy, ChiefNeutral, ChiefRange) * FuelSwing;
        public static float TyreWearScale(CardDefinition chief) =>
            chief == null ? 1f : 1f - Unit(chief.setup, ChiefNeutral, ChiefRange) * WearSwing;
    }

    // The action cards in play: timed boosts with a clock, plus the one-shot "next pit stop" flag. Times are
    // whatever clock the caller uses (race seconds). Playing a timed card that is already running restarts its
    // clock rather than stacking it.
    public sealed class ActiveEffects
    {
        readonly Dictionary<ActionEffect, (float until, float magnitude)> _timed = new();

        // Perfect Pit Stop: waiting for the next stop to use it.
        public bool PerfectPitPending { get; private set; }
        public float PerfectPitScale { get; private set; } = 1f;

        // Starts what a played card does over time. Returns false for instant effects (the caller applies
        // those itself — tyres, bodywork) so the caller knows nothing is running.
        public bool Start(ActionSpec spec, float now)
        {
            if (spec == null) return false;
            if (spec.Effect == ActionEffect.PerfectPitStop)
            {
                PerfectPitPending = true;
                PerfectPitScale = Math.Max(0.05f, spec.Magnitude);
                return true;
            }
            if (spec.Seconds <= 0f) return false;
            _timed[spec.Effect] = (now + spec.Seconds, spec.Magnitude);
            return true;
        }

        public void PitStopUsed()
        {
            PerfectPitPending = false;
            PerfectPitScale = 1f;
        }

        public bool IsActive(ActionEffect e, float now) => _timed.TryGetValue(e, out var t) && now < t.until;

        public float Remaining(ActionEffect e, float now) =>
            _timed.TryGetValue(e, out var t) ? Math.Max(0f, t.until - now) : 0f;

        float Magnitude(ActionEffect e, float now) => IsActive(e, now) ? _timed[e].magnitude : 0f;

        // Push To Pass: +magnitude engine power.
        public float PowerScale(float now) => 1f + Magnitude(ActionEffect.PushToPass, now);

        // Slipstream: draft strength multiplied by magnitude.
        public float DraftScale(float now) => IsActive(ActionEffect.Slipstream, now) ? _timed[ActionEffect.Slipstream].magnitude : 1f;

        // Fuel Saver: burn cut by magnitude.
        public float FuelBurnScale(float now) => 1f - Magnitude(ActionEffect.FuelSaver, now);

        // Pit service time while a Perfect Pit Stop is waiting.
        public float PitTimeScale => PerfectPitPending ? PerfectPitScale : 1f;

        public IEnumerable<ActionEffect> Running(float now)
        {
            foreach (var kv in _timed) if (now < kv.Value.until) yield return kv.Key;
        }
    }
}
