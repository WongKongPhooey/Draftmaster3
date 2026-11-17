using UnityEngine;

namespace Draftmaster.Sim
{
    // Spun by another car: past a certain angle between where the car points and where it is going, the tyres are
    // no longer rolling along the road but scrubbing across it, and they let go. A real car turned sideways at speed
    // does not catch itself - it slides on its momentum, scrubbing speed and rotating, until it has slowed enough or
    // come back near straight for the tyres to bite again.
    //
    // How far it can be turned before that happens falls with speed: at a crawl a car can be swung well round and
    // still drive away, at 180 mph a dozen degrees is gone. The angle is interpolated linearly between a low-speed
    // and a high-speed point (held flat outside them).
    //
    // PlayerVehicleController owns the state (player and AI cars alike - both are driven through it); this is the
    // engine-free rule so it can be tested headless.
    public static class SpinSlide
    {
        // Angle (deg, 0..180) between the car's nose and its direction of travel, from body-frame velocity
        // (vx forward, vy lateral). 0 = travelling straight ahead, 90 = sideways, 180 = going backwards.
        public static float TravelAngleDeg(float vx, float vy)
        {
            if (vx * vx + vy * vy < 1e-6f) return 0f;
            return Mathf.Atan2(Mathf.Abs(vy), vx) * Mathf.Rad2Deg;
        }

        // Angle (deg) past which the car breaks away at speedMps: lowAngleDeg at or below lowSpeedMps, highAngleDeg
        // at or above highSpeedMps, linear between. highAngleDeg is expected to be the smaller of the two.
        public static float BreakAngleDeg(float speedMps, float lowSpeedMps, float lowAngleDeg,
                                          float highSpeedMps, float highAngleDeg)
        {
            float t = Mathf.InverseLerp(lowSpeedMps, Mathf.Max(highSpeedMps, lowSpeedMps + 0.01f), speedMps);
            return Mathf.Lerp(lowAngleDeg, highAngleDeg, t);
        }

        // Should a gripping car (armed by a recent hit) break away now? Not below minSpeedMps - a crawling car
        // pivots on its tyres, it doesn't slide.
        public static bool ShouldBreak(float travelAngleDeg, float speedMps, float breakAngleDeg, float minSpeedMps)
            => speedMps >= minSpeedMps && travelAngleDeg >= breakAngleDeg;

        // Should a sliding car regrip? Once it has scrubbed down to regripSpeedMps, or has come back within
        // regripFraction of the break angle while no longer rotating faster than regripYawRateDeg (a car going
        // through straight in the middle of a 360 is still spinning, and stays loose).
        public static bool ShouldRegrip(float travelAngleDeg, float speedMps, float breakAngleDeg,
                                        float regripFraction, float yawRateDeg, float regripYawRateDeg,
                                        float regripSpeedMps)
        {
            if (speedMps <= regripSpeedMps) return true;
            return travelAngleDeg <= breakAngleDeg * Mathf.Clamp01(regripFraction)
                && Mathf.Abs(yawRateDeg) <= regripYawRateDeg;
        }
    }
}
