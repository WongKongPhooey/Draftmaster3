using UnityEngine;

namespace Draftmaster.Sim
{
    // Pure maths behind the driving camera's "feel", kept free of MonoBehaviour state so it can be unit-tested
    // in EditMode (the camera itself can only be judged in Play Mode, which isn't always available).
    //
    // Two ideas live here:
    //   * Lean — a camera pinned rigidly to the car reads as a spreadsheet, but a camera that answers every
    //     load is sickening. So the lean runs through a dead band: the ordinary business of racing — part
    //     throttle, coasting, a corner taken at a sensible lick — moves the view not at all, and what is left
    //     over is the heavy stop, where the camera drops back behind the nose and lets you feel the anchors.
    //   * Trauma — impacts add to a 0..1 shake budget that decays on its own. Shake scales with the SQUARE of
    //     that budget, so a light scrape barely registers while a proper shunt really kicks.
    public static class CameraFeel
    {
        public const float Gravity = 9.81f;

        // Frame-rate independent exponential approach. responseHz is roughly "how many e-folds per second",
        // so 6 settles in about a third of a second regardless of frame rate.
        public static float Approach(float current, float target, float responseHz, float dt)
        {
            if (dt <= 0f) return current;
            if (responseHz <= 0f) return target;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-responseHz * dt));
        }

        // Acceleration (m/s²) → -1..1, measured against a reference expressed in g, ignoring anything under
        // thresholdG. Loads inside the dead band return a flat zero — that is what keeps the camera still while
        // the car is merely driving — and the ramp from the band up to the reference is eased at both ends, so
        // crossing out of the band never snaps and saturating at the top never clips.
        public static float NormaliseAccel(float accelMps2, float referenceG, float thresholdG = 0f)
        {
            float reference = Mathf.Max(referenceG * Gravity, 0.01f);
            float threshold = Mathf.Clamp(thresholdG * Gravity, 0f, reference - 0.01f);
            float magnitude = Mathf.Abs(accelMps2);
            if (magnitude <= threshold) return 0f;
            float t = Mathf.Clamp01((magnitude - threshold) / (reference - threshold));
            return Mathf.Sign(accelMps2) * Mathf.SmoothStep(0f, 1f, t);
        }

        // Where the camera wants to sit relative to the car, in metres, given what the car is doing:
        //   x = along the nose  (+ = pushes ahead under power, − = drops back under braking)
        //   y = along body-left (+ = slides toward the inside of a left-hander)
        // The two longitudinal directions get their own travel on purpose: throttle is a long, gentle shove that
        // the camera should barely acknowledge, while braking is a short, violent one worth showing.
        // latAccel follows the vehicle's body convention: + is to the left.
        public static Vector2 LeanOffset(float longAccelMps2, float latAccelMps2, float referenceG,
                                         float throttleLean, float brakingLean, float lateralLean,
                                         float longitudinalThresholdG, float lateralThresholdG)
        {
            float longNorm = NormaliseAccel(longAccelMps2, referenceG, longitudinalThresholdG);
            return new Vector2(longNorm * (longNorm >= 0f ? throttleLean : brakingLean),
                               NormaliseAccel(latAccelMps2, referenceG, lateralThresholdG) * lateralLean);
        }

        // Camera roll, degrees. Cornering left (+ lateral accel) rolls the view clockwise, the way a body leans
        // to the outside of a turn. Feed a negative maxRollDeg to lean the other way. Rolling the whole view is
        // the quickest way to turn a stomach, so this is the knob to keep small.
        public static float RollDegrees(float latAccelMps2, float referenceG, float maxRollDeg, float thresholdG = 0f)
            => -NormaliseAccel(latAccelMps2, referenceG, thresholdG) * maxRollDeg;

        // Lean fades out at a crawl so a parked or pitting car doesn't sit at an angle.
        public static float LeanFade(float speedMph, float fadeInMph)
            => fadeInMph <= 0f ? 1f : Mathf.Clamp01(speedMph / fadeInMph);

        // ---- Trauma ----

        public static float AddTrauma(float trauma, float amount)
            => Mathf.Clamp01(trauma + Mathf.Max(0f, amount));

        public static float DecayTrauma(float trauma, float decayPerSecond, float dt)
            => Mathf.Clamp01(trauma - Mathf.Max(0f, decayPerSecond) * Mathf.Max(0f, dt));

        // Quadratic response: half a tank of trauma shakes at a quarter strength, so only real hits are violent.
        public static float ShakeAmount(float trauma)
        {
            float t = Mathf.Clamp01(trauma);
            return t * t;
        }

        // Smooth (Perlin) shake displacement in metres. Sampled against time rather than rolled per frame, so
        // the shake is continuous — random jitter reads as a broken sprite, a noise wobble reads as a jolt.
        public static Vector2 ShakeOffset(float trauma, float amplitude, float time, float frequency, int seed)
        {
            float k = ShakeAmount(trauma) * amplitude;
            if (k <= 0f) return Vector2.zero;
            float t = time * frequency;
            return new Vector2(SignedNoise(Row(seed, 0), t), SignedNoise(Row(seed, 1), t)) * k;
        }

        public static float ShakeRoll(float trauma, float amplitudeDeg, float time, float frequency, int seed)
            => SignedNoise(Row(seed, 2), time * frequency) * ShakeAmount(trauma) * amplitudeDeg;

        // Perlin noise is symmetric about integer lattice lines and returns a flat 0.5 along them, so every
        // channel is sampled on its own off-lattice row.
        static float Row(int seed, int channel) => (seed * 3 + channel) * 7.13f + 0.37f;

        static float SignedNoise(float row, float t) => Mathf.PerlinNoise(row, t) * 2f - 1f;
    }
}
