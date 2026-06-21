using UnityEngine;

// Implemented by car controllers so the shared VehicleCollision component can push them apart
// and scrub speed on contact, regardless of whether motion is spline-driven or free-driven.
public interface ICollisionResponder
{
    // Mass (kg) of this car. Used to weight momentum transfer in car-vs-car contacts.
    float Mass { get; }

    // worldMtv: minimum translation vector (world space) to separate this car from the contact.
    // contactPoint: world-space point of contact. Its lever arm from the car centre produces yaw torque (spin).
    // severity: 0..1 closing severity used to scale speed loss.
    // Used for car-vs-BARRIER (static wall): the responder reflects its own velocity off the surface.
    void ApplyContact(Vector2 worldMtv, Vector2 contactPoint, float severity);

    // Car-vs-CAR impact. Unlike ApplyContact, the velocity change is supplied (worldDeltaV) — VehicleCollision
    // computes a proper 2-body impulse so momentum transfers (a fast car shunts a stationary one forward and
    // slows itself), rather than each car bouncing off the other like a wall.
    // worldMtv depenetrates this car; worldDeltaV is the world-space velocity change to add; contactPoint gives
    // the lever arm for spin; severity is the 0..1 closing severity.
    void ApplyCarImpact(Vector2 worldMtv, Vector2 contactPoint, Vector2 worldDeltaV, float severity);
}
