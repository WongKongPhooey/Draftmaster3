using UnityEngine;

namespace Draftmaster.Sim
{
    // The steering ratio the driver picks on the setup panel: turns of the wheel per turn of the front tyres,
    // written "1:N". A bigger N is a slower rack — the same stick throw turns the front wheels less.
    //
    // 1:8 is the baseline: it is how the car steered before the ratio existed, so at 1:8 the wheels see
    // exactly what they always did. Every other ratio scales the human's steering by Baseline / N, so 1:16 is
    // half as sensitive and 1:6 a third quicker.
    //
    // Ovals default slower (1:12): long, constant-radius corners want small, steady corrections, and the
    // baseline rack makes a thumb on a stick twitchy there. Road courses keep the baseline, which already
    // feels right at Watkins Glen.
    public static class SteeringRatio
    {
        public const float Min = 6f;
        public const float Max = 16f;
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
