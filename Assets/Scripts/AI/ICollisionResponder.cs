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
    void ApplyContact(Vector2 worldMtv, Vector2 contactPoint, float severity);
}
