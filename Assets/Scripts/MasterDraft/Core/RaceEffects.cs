using System;

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
}
