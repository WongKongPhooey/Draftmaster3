using UnityEngine;

// Something the on-foot player is sat in that STEERS rather than walks — the paddock golf cart today.
//
// Walking is "the stick is the direction you go". Driving is not: the stick is throttle, brake and lock,
// and where the body ends up is whatever the vehicle's own speed and heading say. OnFootController still
// owns the body — the paddock boundary clamp, the shove out of people, the co-op puppet and the animator
// are all one set of rules and none of them should be written twice — so the vehicle is asked for a
// velocity instead of being given the transform.
//
// The contract per fixed step, in order:
//   1. Steer(stick, dt)  — advance the vehicle's own state and hand back the velocity to move at.
//   2. the walker clamps that velocity to the boundary and out of anybody it is stood in.
//   3. Moved(actual)     — what the body really did, so driving into a fence bleeds the speed off rather
//                          than leaving the cart grinding at full pelt against it.
// Facing is read after all three and points the rider the way the nose points.
public interface IRiddenVehicle
{
    // Throttle/brake/steer for this step from the raw on-foot stick, and the world velocity that comes out.
    Vector2 Steer(Vector2 stick, float dt);

    // The velocity the body actually ended up with once the walker had its say.
    void Moved(Vector2 actualVelocity);

    // Which way the nose points, world space. The rider is turned to match.
    Vector2 Facing { get; }
}
