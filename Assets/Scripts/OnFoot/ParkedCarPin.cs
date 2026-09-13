using UnityEngine;

// Holds a parked car exactly where it was put down, until somebody drives it away.
//
// A car that has been placed rather than driven — towed in off the circuit, parked in its box — is held
// there by nothing at all once the frame it was placed on has passed. PlayerVehicleController is switched
// off while nobody is driving, so the pose it was given is not re-asserted by anything, and everything else
// in the scene is still free to move it: a depenetration push off the box props or the car in the next box,
// a Rigidbody2D that kept a fraction of the speed it hit the wall with, an AI brain left enabled on the
// player's own car from a broadcast cut. Any of those drags the car out of its box over the following
// seconds, which is how a driver ends up stood in their box next to an empty patch of tarmac, and how the
// clock keeps running on a lap that finished in the wall (the pit-lane rules are read off where the car IS).
//
// So the pose is pinned rather than written once. This is the same lesson SplineInputDriver learned about a
// held AI car — it re-seeds every frame it is held still rather than seeding once — applied to the one car
// that has no brain re-seeding it.
//
// Self-releasing: the pin is over the moment PlayerVehicleController is enabled, because that is exactly
// the moment somebody is driving the car again. Nothing has to remember to take it off.
[DisallowMultipleComponent]
public class ParkedCarPin : MonoBehaviour
{
    PlayerVehicleController _car;
    Rigidbody2D _body;
    Vector3 _pos;
    Quaternion _rot;

    // Pin a car on the pose it is stood on right now.
    public static ParkedCarPin Hold(PlayerVehicleController car)
    {
        if (car == null) return null;

        var pin = car.GetComponent<ParkedCarPin>();
        if (pin == null) pin = car.gameObject.AddComponent<ParkedCarPin>();

        pin._car = car;
        pin._body = car.GetComponent<Rigidbody2D>();
        pin._pos = car.transform.position;
        pin._rot = car.transform.rotation;
        pin.enabled = true;
        return pin;
    }

    // Let a car go before it is driven — for anything that moves a parked car on purpose.
    public static void Release(PlayerVehicleController car)
    {
        if (car == null) return;
        var pin = car.GetComponent<ParkedCarPin>();
        if (pin != null) pin.Retire();
    }

    // Both halves of the frame. FixedUpdate undoes whatever the physics step did to it; LateUpdate undoes
    // whatever a script did during the frame, so the pose the camera draws is the one the car was parked on.
    void FixedUpdate() => Reassert();
    void LateUpdate() => Reassert();

    void Reassert()
    {
        if (_car == null) { Retire(); return; }
        if (_car.enabled) { Retire(); return; }   // somebody is driving it: it is not parked any more

        if (_car.transform.position != _pos || _car.transform.rotation != _rot)
            _car.transform.SetPositionAndRotation(_pos, _rot);

        if (_body != null)
        {
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.position = _pos;
            _body.rotation = _rot.eulerAngles.z;
        }
    }

    // Destroy, either side of play mode. The tow has an edit-mode test and Destroy() is an error there.
    void Retire()
    {
        if (Application.isPlaying) Destroy(this);
        else DestroyImmediate(this);
    }
}
