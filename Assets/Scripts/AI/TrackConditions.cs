using UnityEngine;

// Global modifiers applied to every car's lateral grip and pace. Singleton-style static fields.
// Wire weather/temp/rubber-buildup into these from a race manager later.
public static class TrackConditions
{
    [Tooltip("Multiplier on tire μ. 1.0 dry, ~0.7 damp, ~0.5 wet. Affects corner speeds globally.")]
    public static float GripMultiplier = 1f;

    [Tooltip("Multiplier on engine power / accel. 1.0 nominal; <1 hot air or thin atmosphere.")]
    public static float PowerMultiplier = 1f;

    public static void Reset()
    {
        GripMultiplier = 1f;
        PowerMultiplier = 1f;
    }
}
