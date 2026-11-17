using UnityEngine;

namespace Draftmaster.Sim
{
    // The steering ratio the driver picks on the setup panel: turns of the wheel per turn of the front tyres,
    // written "1:N". A bigger N is a slower rack — the same stick throw turns the front wheels less.
    //
    // 1:8 is the baseline: it is how the car steered before the ratio existed, so at 1:8 the wheels see
    // exactly what they always did. Every other ratio scales the human's steering by Baseline / N, so 1:16 is
    // half as sensitive, 1:32 a quarter, and 1:4 twice as quick.
    //
    // Ovals default slower (1:12): long, constant-radius corners want small, steady corrections, and the
    // baseline rack makes a thumb on a stick twitchy there. Road courses keep the baseline, which already
    // feels right at Watkins Glen.
    //
    // The range was 1:6 to 1:16 until 2026-11-17. A keyboard steers all or nothing (a key is full lock), so the
    // ratio is the only thing setting how far a tap turns the wheels, and even 1:16 was too twitchy to hold a
    // line round Daytona on keys. The slow end now goes to 1:32; the quick end to 1:4 for symmetry.
    public static class SteeringRatio
    {
        public const float Min = 4f;
        public const float Max = 32f;
        public const float Baseline = 8f;
        public const float OvalDefault = 12f;
        public const float RoadCourseDefault = Baseline;

        public static float DefaultFor(bool roadCourse) => roadCourse ? RoadCourseDefault : OvalDefault;

        public static float Clamp(float ratio) => Mathf.Clamp(Mathf.Round(ratio), Min, Max);

        // What the human's steering input is multiplied by before it reaches the front wheels.
        public static float Gain(float ratio) => Baseline / Clamp(ratio);

        public static string Label(float ratio) => $"1:{Mathf.RoundToInt(Clamp(ratio))}";
    }
}
