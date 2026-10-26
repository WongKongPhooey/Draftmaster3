using UnityEngine;

namespace Draftmaster.Sim
{
    // The impulse two cars exchange when they hit, along the contact normal.
    //
    // A hit away from a car's centre spends part of itself turning the car instead of shoving it, so the car
    // gives way at the contact point more easily than its mass alone says. The textbook rigid-body impulse
    // carries that in its denominator — each body's (r × n)² / I next to its 1 / m. Leaving those terms out
    // (mass only) treats every hit as if it landed dead on the centre of mass: right for a nose-to-tail bump in
    // the draft, but for a door or a rear corner it overstates the impulse by up to three or four times, and the
    // same overstated impulse then sets the yaw kick. Cars rebounded and spun off each other far harder than the
    // closing speed warranted.
    public static class CarContact
    {
        // closingSpeed: relative speed along n, > 0 when closing. n: unit contact normal.
        // rA / rB: contact point minus each car's centre. inertiaA / inertiaB: yaw inertia (kg·m²) the impulse
        // turns against; +inf (or 0 lever) leaves only the mass term — a car that can't be spun.
        // Returns the impulse magnitude (N·s); car A takes -j·n, car B +j·n. 0 when separating.
        public static float NormalImpulse(float closingSpeed, float restitution, Vector2 n,
                                          float massA, float inertiaA, Vector2 rA,
                                          float massB, float inertiaB, Vector2 rB)
        {
            if (closingSpeed <= 0f) return 0f;
            float denom = 1f / Mathf.Max(massA, 1f) + 1f / Mathf.Max(massB, 1f)
                        + Turning(rA, n, inertiaA) + Turning(rB, n, inertiaB);
            return (1f + Mathf.Max(0f, restitution)) * closingSpeed / Mathf.Max(denom, 1e-6f);
        }

        static float Turning(Vector2 r, Vector2 n, float inertia)
        {
            if (!(inertia > 0f) || float.IsInfinity(inertia)) return 0f;
            float cross = r.x * n.y - r.y * n.x;
            return cross * cross / inertia;
        }
    }
}
