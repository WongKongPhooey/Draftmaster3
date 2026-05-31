using UnityEngine;

// Implemented by car controllers so the shared VehicleCollision component can push them apart
// and scrub speed on contact, regardless of whether motion is spline-driven or free-driven.
public interface ICollisionResponder
{
    // worldMtv: minimum translation vector (world space) to separate this car from the contact.
    // severity: 0..1 closing severity used to scale speed loss.
    void ApplyContact(Vector2 worldMtv, float severity);
}
