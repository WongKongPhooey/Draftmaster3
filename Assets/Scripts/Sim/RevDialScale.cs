using System;

namespace Draftmaster.Sim
{
    // The numbering of the race HUD's rev counter (IronOvalRaceHUD), worked out from the car's own rev range so a
    // 9,000 rpm Cup engine, a 7,000 rpm truck and a 3,000 rpm road car each get a dial that fits them: numerals
    // in thousands, a round top end just past the engine's maximum, and a red band from the redline up.
    //
    // Engine-free so the layout is tested without a scene.
    public readonly struct RevDialScale
    {
        public const float SweepDeg = 270f;

        // Major steps tried in order; the first giving no more than MaxMajors numerals wins.
        static readonly float[] Steps = { 500f, 1000f, 2000f, 2500f, 5000f, 10000f };
        const int MaxMajors = 10;

        public readonly float TopRpm;        // dial end, a whole number of major steps
        public readonly float MajorStep;     // rpm between numerals
        public readonly float MinorStep;     // rpm between small ticks (half a major step)
        public readonly float RedFromRpm;    // red band start; runs to TopRpm

        RevDialScale(float top, float major, float minor, float red)
        {
            TopRpm = top; MajorStep = major; MinorStep = minor; RedFromRpm = red;
        }

        public int MajorCount => (int)Math.Round(TopRpm / MajorStep) + 1;   // numerals, 0 included

        // `maxRpm` is where the engine stops (VehicleInfo.maxRpm), `redlineRpm` where it should be shifted
        // (VehicleInfo.shiftUpRpm). The band starts at the redline or 90% of max, whichever is lower, so a car
        // that shifts right at its limiter still shows a band you can see.
        public static RevDialScale For(float maxRpm, float redlineRpm)
        {
            if (!(maxRpm > 0f)) maxRpm = 8000f;
            float major = Steps[Steps.Length - 1];
            for (int i = 0; i < Steps.Length; i++)
            {
                float top = CeilTo(maxRpm, Steps[i]);
                if (top / Steps[i] <= MaxMajors) { major = Steps[i]; break; }
            }
            float topRpm = CeilTo(maxRpm, major);
            // An engine whose max lands exactly on a numeral reads as pinned at the end stop: one more step.
            if (topRpm - maxRpm < major * 0.1f && topRpm / major < MaxMajors) topRpm += major;

            float minor = major / 2f;

            float red = redlineRpm > 0f ? Math.Min(redlineRpm, maxRpm * 0.9f) : maxRpm * 0.9f;
            return new RevDialScale(topRpm, major, minor, red);
        }

        // Needle angle, degrees clockwise from straight up: -135 at 0 rpm, +135 at TopRpm.
        public float Degrees(float rpm)
        {
            float t = rpm / TopRpm;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return SweepDeg * t - SweepDeg * 0.5f;
        }

        // The numeral printed at a major tick: thousands, with one decimal only when the step needs it.
        public string Label(float rpm)
        {
            float k = rpm / 1000f;
            return Math.Abs(k - Math.Round(k)) < 0.01f
                ? ((int)Math.Round(k)).ToString()
                : k.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        static float CeilTo(float v, float step) => (float)Math.Ceiling(v / step - 1e-4) * step;
    }
}
