using UnityEngine;

namespace Draftmaster.Sim
{
    // Pure maths behind the swing (chase) camera, kept out of MonoBehaviour land so it can be unit-tested in
    // EditMode — the camera itself can only really be judged by driving it, which isn't always available.
    //
    // The race camera hangs straight above the car looking down +Z with +Y up the screen, so "sitting behind
    // the car" in a top-down game means rolling the whole view until the car's nose points up the screen.
    // Doing that rigidly — snapping the roll to the heading every frame — makes the world whip around the car
    // and reads as a turntable. So the roll is a spring on a hinge instead: it is pulled toward the heading,
    // carries its own angular momentum, and is deliberately left slightly under-damped so it arrives a beat
    // late and overshoots a touch on the way out of a corner. That lag IS the effect.
    public static class CameraSwing
    {
        // Longest slice the integrator will take in one go, and how many it will take for one frame. A spring
        // stepped with too coarse a slice gains energy instead of losing it, and a frame here can be very long
        // (a scene load, an editor that was left unfocused). Substepping keeps the maths honest; the cap keeps
        // a one-second hitch from costing a thousand iterations.
        public const float MaxSubstep = 1f / 90f;
        public const int MaxSubsteps = 8;

        // Fold an angle into -180..180, which is the range every comparison here expects.
        public static float Normalise(float angleDeg) => Mathf.Repeat(angleDeg + 180f, 360f) - 180f;

        // The camera roll, in degrees, that puts a car heading `headingDeg` (world, 0 = +X) straight up the
        // screen. Camera up is (-sin θ, cos θ); matching that to (cos h, sin h) gives θ = h - 90.
        public static float TargetAngle(float headingDeg) => Normalise(headingDeg - 90f);

        // One step of the hinge. `angleDeg` and `velocityDegPerSec` are the spring's state and are advanced in
        // place; `responseHz` is its natural frequency (how eagerly it chases) and `damping` the damping ratio
        // (1 = arrives without overshoot, below 1 = swings past and settles back, which is the point).
        // `maxDegPerSecond` caps how fast the view is ever allowed to turn, so a spin whips the car and not the
        // player's stomach. Pass 0 or less to leave it uncapped.
        public static void Step(ref float angleDeg, ref float velocityDegPerSec, float targetDeg,
                                float responseHz, float damping, float maxDegPerSecond, float dt)
        {
            if (dt <= 0f) return;

            float omega = 2f * Mathf.PI * Mathf.Max(0.01f, responseHz);
            float zeta = Mathf.Max(0f, damping);

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSubstep), 1, MaxSubsteps);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                // Always the short way round: at heading 179° a camera sat at -179° should nudge two degrees
                // backwards, not unwind through the whole circle.
                float error = Mathf.DeltaAngle(angleDeg, targetDeg);
                velocityDegPerSec += (omega * omega * error - 2f * zeta * omega * velocityDegPerSec) * h;
                if (maxDegPerSecond > 0f)
                    velocityDegPerSec = Mathf.Clamp(velocityDegPerSec, -maxDegPerSecond, maxDegPerSecond);
                angleDeg = Normalise(angleDeg + velocityDegPerSec * h);
            }
        }

        // Has the spring effectively arrived? Used to stop writing a rotation once a switched-off swing has
        // unwound back to square, so a fixed camera is left exactly as its author placed it.
        public static bool Settled(float angleDeg, float velocityDegPerSec, float targetDeg,
                                   float angleEpsilonDeg = 0.02f, float velocityEpsilonDegPerSec = 0.5f)
            => Mathf.Abs(Mathf.DeltaAngle(angleDeg, targetDeg)) <= angleEpsilonDeg
               && Mathf.Abs(velocityDegPerSec) <= velocityEpsilonDegPerSec;
    }
}
