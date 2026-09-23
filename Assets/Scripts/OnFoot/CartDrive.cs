using UnityEngine;

// The golf cart's driving model: throttle, brake, reverse and steering lock, with no Unity in it beyond
// the vectors. Plain class rather than a component so the whole of it can be stepped in an EditMode test
// — "does holding left at a standstill spin the cart on the spot" is a question about arithmetic, not
// about a scene.
//
// The controls are the race car's, on one stick: push the stick forward to accelerate, pull it back to
// brake (and, once stopped, to back up), and push it left or right to steer. They are read the way a
// driving game reads them and NOT the way walking reads them — the stick is not a direction to go in, it
// is a set of pedals. Pushing forward while the cart points south accelerates it south.
//
// Steering only works while the cart is rolling, which is the one rule that makes it feel like a vehicle
// instead of a person: a stopped cart asked to turn does nothing until it is moving, and a reversing one
// turns the other way, because the wheels are at the front.
public class CartDrive
{
    // Metres/sec the cart will pull to on full throttle. The walking player does 3.5.
    public float topSpeed = 8f;
    // Metres/sec backwards, holding the brake once already stopped. A cart reverses at walking pace.
    public float reverseSpeed = 2.5f;
    // Metres/sec². Pulls to top speed in a bit under two seconds — a cart, not a dragster.
    public float accelRate = 5f;
    // Metres/sec² on the brake. Firmer than the throttle, so a stab of back-stick stops it short.
    public float brakeRate = 14f;
    // Metres/sec² with neither pedal: the cart rolls to a stop rather than dropping dead off the throttle.
    public float coastRate = 3.5f;
    // Degrees/sec of heading change at full lock, once there is enough speed for the steering to bite.
    public float steerRate = 200f;
    // Speed (m/s) at which the steering has full authority. Below it the lock is scaled down in
    // proportion, so a crawling cart turns lazily and a stopped one not at all.
    public float steerBiteSpeed = 2f;
    // Seconds the brake has to be held at a standstill before reverse engages. Without it, braking to a
    // halt and holding the stick a beat longer than needed — which is what stopping actually looks like —
    // rolls straight into a lurch backwards.
    public float reverseDelay = 0.4f;
    // Stick tilt below this is nothing at all — a resting stick must not creep the cart across the paddock.
    public float deadzone = 0.15f;

    // Metres/sec along the nose. Negative is reversing.
    public float Speed { get; private set; }

    // Unit vector the nose points along, world space.
    public Vector2 Heading { get; private set; } = Vector2.up;

    // Seconds the brake has been held with the cart already stopped.
    float _restingOnBrake;

    public void Reset(Vector2 heading)
    {
        Speed = 0f;
        _restingOnBrake = 0f;
        Heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector2.up;
    }

    // One step of driving. Hands back the velocity the body should move at.
    public Vector2 Step(Vector2 stick, float dt)
    {
        if (dt <= 0f) return Heading * Speed;

        float throttle = Mathf.Abs(stick.y) > deadzone ? Mathf.Clamp01(stick.y) : 0f;
        float brake = Mathf.Abs(stick.y) > deadzone ? Mathf.Clamp01(-stick.y) : 0f;
        float steer = Mathf.Abs(stick.x) > deadzone ? Mathf.Clamp(stick.x, -1f, 1f) : 0f;

        if (throttle > 0f)
        {
            // On the throttle while rolling backwards is the brake: you do not pull away forwards through
            // a cart that is still going the other way.
            float rate = Speed < 0f ? brakeRate : accelRate;
            Speed = Mathf.Min(topSpeed, Speed + rate * throttle * dt);
        }
        else if (brake > 0f)
        {
            // Stop first, then back up. Reverse is deliberately slow to reach so that a stab of brake at
            // walking pace never turns into an accidental lurch backwards.
            if (Speed > 0f) { Speed = Mathf.Max(0f, Speed - brakeRate * brake * dt); _restingOnBrake = 0f; }
            else
            {
                _restingOnBrake += dt;
                if (_restingOnBrake >= reverseDelay)
                    Speed = Mathf.Max(-reverseSpeed, Speed - accelRate * brake * dt);
            }
        }
        else
        {
            Speed = Mathf.MoveTowards(Speed, 0f, coastRate * dt);
            _restingOnBrake = 0f;
        }
        if (throttle > 0f) _restingOnBrake = 0f;

        // Steering authority: none at a standstill, full once rolling, and mirrored in reverse — turning
        // the wheel while backing up swings the nose the other way, exactly as it does in a car park.
        float bite = Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(0.01f, steerBiteSpeed));
        if (bite > 0f && steer != 0f)
        {
            // Stick right turns the cart right, which is clockwise, which is a NEGATIVE z rotation.
            float degrees = -steer * steerRate * bite * dt * Mathf.Sign(Speed);
            Heading = Rotate(Heading, degrees);
        }

        return Heading * Speed;
    }

    // What the body actually managed once the paddock boundary and the people in the way had their say.
    // A cart driven into a fence has to lose its speed against it, or the moment the nose comes off the
    // fence it leaps away at a speed it never earned.
    public void Blocked(Vector2 actualVelocity)
    {
        float along = Vector2.Dot(actualVelocity, Heading);
        if (Mathf.Abs(along) < Mathf.Abs(Speed)) Speed = along;
    }

    public static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
