using UnityEngine;

// Global modifiers applied to every car's lateral grip and pace. Singleton-style static fields.
// Wire weather/temp/rubber-buildup into these from a race manager later.
public static class TrackConditions
{
    // Baked baseline so the GripMultiplier slider at neutral (1.0) feels like the old 1.5x tuning.
    // Grip consumers use Effective, not GripMultiplier directly.
    public const float BaseGrip = 1.5f;

    [Tooltip("Driver-facing grip slider. 1.0 neutral (= baked 1.5x feel), <1 damp/wet, >1 extra bite. Multiplied by BaseGrip.")]
    public static float GripMultiplier = 1f;

    [Tooltip("Multiplier on engine power / accel. 1.0 nominal; <1 hot air or thin atmosphere.")]
    public static float PowerMultiplier = 1f;

    // Effective grip the dynamics actually consume: baked baseline × driver slider.
    public static float Effective => BaseGrip * GripMultiplier;

    public static void Reset()
    {
        GripMultiplier = 1f;
        PowerMultiplier = 1f;
    }
}
