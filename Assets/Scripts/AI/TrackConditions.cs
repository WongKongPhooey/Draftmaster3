using UnityEngine;

// Global modifiers applied to every car's lateral grip and pace. Singleton-style static fields.
// Wire weather/temp/rubber-buildup into these from a race manager later.
public static class TrackConditions
{
    // Baked baseline so the GripMultiplier slider at neutral (1.0) feels like 1.2x of the raw tuning.
    // Grip consumers use Effective, not GripMultiplier directly.
    public const float BaseGrip = 1.2f;

    // Baked baseline so the PowerMultiplier slider at neutral (1.0) feels like 1.5x of the raw accel.
    // Power consumers use EffectivePower, not PowerMultiplier directly.
    public const float BasePower = 1.5f;

    [Tooltip("Driver-facing grip slider. Default 1.2 (= 1.2 baked × 1.2 = 1.44x effective), <1 damp/wet, >1 extra bite. Multiplied by BaseGrip.")]
    public static float GripMultiplier = 1.2f;

    [Tooltip("Driver-facing power slider. Default 0.8 (= 1.5 baked × 0.8 = 1.2x effective), <1 hot air / thin atmosphere. Multiplied by BasePower.")]
    public static float PowerMultiplier = 0.8f;

    // Effective grip the dynamics actually consume: baked baseline × driver slider.
    public static float Effective => BaseGrip * GripMultiplier;

    // Effective power the dynamics actually consume: baked baseline × driver slider.
    public static float EffectivePower => BasePower * PowerMultiplier;

    [Tooltip("Scales tyre wear accrual on every car. Default 0.05 (gentle). 0 = no wear, 1 = raw model rate, >1 abrasive track.")]
    public static float TireWearMultiplier = 0.05f;

    [Tooltip("Scales fuel burn on every car. 1 nominal, 0 = no burn, 2 = double consumption.")]
    public static float FuelUseMultiplier = 1f;

    [Tooltip("Scales crash damage accrual on every car. 1 nominal, 0 = invulnerable bodywork, 2 = fragile.")]
    public static float DamageMultiplier = 1f;

    [Tooltip("Scales every racing AI's pace (their target speeds AND engine power under the shared dynamic model). 1 nominal, <1 slower field, >1 faster field. Does not touch formation/safety-car pacing.")]
    public static float AiPaceMultiplier = 1.2f;

    [Tooltip("AI-only grip multiplier layered on top of the global grip. >1 = AI corner faster than the player at equal tuning; player unaffected.")]
    public static float AiGripMultiplier = 1.2f;

    // Effective grip for AI-driven cars: global effective grip × AI-only bonus.
    public static float AiEffective => Effective * AiGripMultiplier;

    public static void Reset()
    {
        GripMultiplier = 1.2f;
        PowerMultiplier = 0.8f;
        TireWearMultiplier = 0.05f;
        FuelUseMultiplier = 1f;
        DamageMultiplier = 1f;
        AiPaceMultiplier = 1.2f;
        AiGripMultiplier = 1.2f;
    }
}
