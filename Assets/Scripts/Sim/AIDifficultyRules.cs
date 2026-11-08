using UnityEngine;

namespace Draftmaster.Sim
{
    // NR2003's two AI sliders, as rules: AI STRENGTH (how fast the field is) and AI AGGRESSION (how readily they go for
    // a gap). Both are percentages; 100 is the game as calibrated. OPTIONS > AI DRIVERS shows them, AIDifficulty saves
    // them, and these say what they mean - pure, so EditMode tests can pin them.
    public static class AIDifficultyRules
    {
        public const int StrengthMin = 80, StrengthMax = 120, StrengthStep = 2, StrengthDefault = 100;
        public const int AggressionMin = 50, AggressionMax = 150, AggressionStep = 10, AggressionDefault = 100;

        public static int ClampStrength(int percent) => Mathf.Clamp(percent, StrengthMin, StrengthMax);
        public static int ClampAggression(int percent) => Mathf.Clamp(percent, AggressionMin, AggressionMax);

        // Left / right: a step, stopping at the ends.
        public static int StepStrength(int percent, int dir) => ClampStrength(percent + System.Math.Sign(dir) * StrengthStep);
        public static int StepAggression(int percent, int dir) => ClampAggression(percent + System.Math.Sign(dir) * AggressionStep);

        // ENTER / a tap: up a step, and round from the top to the bottom, so a phone reaches every value.
        public static int CycleStrength(int percent) => percent >= StrengthMax ? StrengthMin : StepStrength(percent, +1);
        public static int CycleAggression(int percent) => percent >= AggressionMax ? AggressionMin : StepAggression(percent, +1);

        // Strength multiplies the AI's grip and power together, on top of the track's calibration. A lap moves about
        // 0.4% for each 1% (Watkins Glen, AIPaceCalibrator: x1.0 -> x1.2 took the lap from 69.3 s to 63.5 s), so the
        // range is roughly 8% of a lap either way. The plate tracks race every car on the same plate and ignore it.
        public static float StrengthScale(int percent) => ClampStrength(percent) / 100f;

        // Aggression scales each driver's own rolled temperament, so the field keeps its spread of hot heads and
        // careful ones.
        public static float Aggression(float rolled01, int percent) => Mathf.Clamp01(rolled01 * ClampAggression(percent) / 100f);

        public static string Format(int percent) => percent + "%";

        public static string DescribeStrength(int percent) =>
            percent == StrengthDefault ? "AI pace as calibrated for each track."
            : percent < StrengthDefault ? "The AI are slower than calibrated."
            : "The AI are faster than calibrated.";

        public static string DescribeAggression(int percent) =>
            percent == AggressionDefault ? "Each driver races to their own temperament."
            : percent < AggressionDefault ? "The AI wait longer for a gap and give more room."
            : "The AI go for gaps sooner and hold their line harder.";
    }
}
