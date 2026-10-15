using UnityEngine;

namespace Draftmaster.Sim
{
    // What a wall does to a car's velocity on one contact step, lifted out of PlayerVehicleController so it can
    // be pinned in EditMode.
    //
    // VehicleCollision calls the responder on EVERY physics step the car overlaps a barrier, not once per hit,
    // so anything applied per call compounds while a car scrapes along. The old response kept a fixed share of
    // the tangential speed per call (0.94) — 50 calls a second left a car riding the wall with ~5% of its speed
    // after one second — and bounced every little re-press of the wheel back off the wall.
    //
    //   * Scrub is Coulomb friction: the speed lost along the wall is `friction` times the impulse taken across
    //     it. A car leaning gently on the wall loses almost nothing; a hard, steep hit loses a lot. It can stop
    //     the car's slide along the wall but never reverse it.
    //   * Bounce only comes from a real hit. Below `bounceThreshold` (m/s into the wall) the car simply stays
    //     against it; above, restitution ramps in over `bounceRamp` m/s to its full value.
    public static class WallContact
    {
        // Restitution actually used for a hit arriving at `closingSpeed` m/s into the wall.
        public static float EffectiveRestitution(float closingSpeed, float restitution, float bounceThreshold, float bounceRamp)
        {
            if (restitution <= 0f || closingSpeed <= bounceThreshold) return 0f;
            float t = bounceRamp > 0f ? Mathf.Clamp01((closingSpeed - bounceThreshold) / bounceRamp) : 1f;
            return restitution * t;
        }

        // New world velocity after touching a wall whose outward normal is `n` (unit). A car moving away from or
        // along the wall is left alone. `normalImpulse` is the velocity change across the wall (m/s, >= 0), for
        // the caller's yaw kick.
        public static Vector2 Resolve(Vector2 vel, Vector2 n, float restitution, float friction,
                                      float bounceThreshold, float bounceRamp, out float normalImpulse)
        {
            normalImpulse = 0f;
            float vn = Vector2.Dot(vel, n);
            if (vn >= 0f) return vel;

            float e = EffectiveRestitution(-vn, restitution, bounceThreshold, bounceRamp);
            normalImpulse = (1f + e) * -vn;

            Vector2 tangent = vel - vn * n;
            float vt = tangent.magnitude;
            float scrub = Mathf.Min(vt, Mathf.Max(0f, friction) * normalImpulse);
            Vector2 newTangent = vt > 1e-6f ? tangent * ((vt - scrub) / vt) : Vector2.zero;

            return newTangent - e * vn * n;
        }
    }
}
