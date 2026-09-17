using System.Collections.Generic;

namespace Draftmaster.Controls
{
    // A rectangle in screen pixels with a top-left origin and y growing downwards: the space IMGUI draws in,
    // so the layout is hit-tested in the same numbers it is drawn with. Its own type rather than
    // UnityEngine.Rect so the rules stay in this engine-free assembly, where the tests can reach them.
    public readonly struct TouchRect
    {
        public readonly float x, y, width, height;

        public TouchRect(float x, float y, float width, float height)
        {
            this.x = x; this.y = y; this.width = width; this.height = height;
        }

        public float xMax => x + width;
        public float yMax => y + height;
        public float centerX => x + width * 0.5f;
        public float centerY => y + height * 0.5f;

        public bool Contains(float px, float py) => px >= x && px < xMax && py >= y && py < yMax;

        public bool Overlaps(TouchRect o) => x < o.xMax && o.x < xMax && y < o.yMax && o.y < yMax;

        public TouchRect Inflate(float by) => new TouchRect(x - by, y - by, width + by * 2f, height + by * 2f);

        public override string ToString() => $"({x:0.#}, {y:0.#}, {width:0.#} x {height:0.#})";
    }

    // One finger on the glass this frame, in the same top-left space as TouchRect.
    public readonly struct TouchPoint
    {
        public readonly int id;
        public readonly float x, y;

        public TouchPoint(int id, float x, float y) { this.id = id; this.x = x; this.y = y; }
    }

    // Where the on-screen driving controls sit: a steering strip under the left thumb, brake and throttle under
    // the right, and a small pause button at the top. Measured in the UI's design pixels (the 640x360 grid)
    // times `unit`, and kept inside the screen's safe area so a notch or a gesture bar never covers a pedal.
    public readonly struct TouchLayout
    {
        // Design-pixel sizes.
        public const float Margin = 12f;          // from the safe area's edge
        public const float PedalWidth = 56f;
        public const float PedalHeight = 96f;
        public const float PedalGap = 8f;         // between brake and throttle
        public const float Slop = 8f;             // how far outside a pedal a thumb still presses it
        public const float SteerTravel = 56f;     // thumb travel from centre to full lock
        public const float KnobSize = 24f;
        public const float PauseSize = 24f;
        public const float SteerZoneTop = 0.25f;  // steering takes the left side below this fraction of the height

        public readonly TouchRect safe;
        public readonly float unit;

        public readonly TouchRect steerZone;      // a thumb landing here takes the wheel
        public readonly TouchRect steerRest;      // where the steering strip is drawn while nobody holds it
        public readonly float steerTravel;        // pixels from centre to full lock

        public readonly TouchRect brake, throttle;          // drawn
        public readonly TouchRect brakeHit, throttleHit;    // pressed: the drawn pedal plus slop, split at the gap
        public readonly TouchRect pause;

        // The size of one design pixel on a screen this big: whole numbers, so the controls land on the same
        // pixel grid as the rest of the UI. Taken from the short side so a portrait screen isn't given pedals
        // wider than itself.
        public static float UnitFor(float screenWidth, float screenHeight)
        {
            float shortSide = screenWidth < screenHeight ? screenWidth : screenHeight;
            float u = (float)System.Math.Floor(shortSide / 360f);
            return u < 1f ? 1f : u;
        }

        public TouchLayout(TouchRect safe, float unit)
        {
            this.safe = safe;
            this.unit = unit < 1f ? 1f : unit;
            float u = this.unit;

            float m = Margin * u, w = PedalWidth * u, h = PedalHeight * u, gap = PedalGap * u, slop = Slop * u;

            // Throttle in the corner, where a right thumb rests; brake beside it, so rolling the thumb left
            // goes from one to the other.
            throttle = new TouchRect(safe.xMax - m - w, safe.yMax - m - h, w, h);
            brake = new TouchRect(throttle.x - gap - w, throttle.y, w, h);

            // The hit areas meet in the middle of the gap, so a thumb between the pedals presses exactly one of
            // them, and run out past the margin to the edge of the safe area and a little beyond.
            float split = throttle.x - gap * 0.5f;
            float top = throttle.y - slop;
            float bottom = safe.yMax + m;
            throttleHit = new TouchRect(split, top, safe.xMax + m - split, bottom - top);
            brakeHit = new TouchRect(brake.x - slop, top, split - (brake.x - slop), bottom - top);

            float p = PauseSize * u;
            pause = new TouchRect(safe.centerX - p * 0.5f, safe.y + 6f * u, p, p);

            // Steering has the left half, stopping short of the brake on a narrow screen, and leaves the top of
            // the screen alone so the pause button and the HUD up there are never mistaken for a steer.
            steerTravel = SteerTravel * u;
            float zoneTop = safe.y + safe.height * SteerZoneTop;
            float zoneRight = System.Math.Min(safe.x + safe.width * 0.5f, brakeHit.x - gap);
            steerZone = new TouchRect(safe.x, zoneTop, zoneRight - safe.x, safe.yMax - zoneTop);

            float k = KnobSize * u;
            steerRest = new TouchRect(safe.x + m, safe.yMax - m - k, steerTravel * 2f + k, k);
        }
    }

    // What the thumbs are asking for, worked out from the fingers on the glass each frame.
    //
    // Each finger's job is decided the moment it lands and kept until it lifts:
    //   - on the pause button: a pause, once;
    //   - in the steering zone: the wheel. Steering is relative to where the thumb came down, so the player
    //     never has to look for a centre; drag past full lock and the centre is dragged along, so reversing
    //     the thumb starts turning the other way at once. One thumb steers at a time.
    //   - anywhere else: a pedal thumb, which presses whichever pedal it is over right now. Sliding from the
    //     brake to the throttle works, and a steering thumb that strays over the pedals presses nothing.
    public sealed class TouchDriveState
    {
        enum Role { Steer, Pedal, Pause, Ignored }

        readonly Dictionary<int, Role> _roles = new Dictionary<int, Role>();
        readonly HashSet<int> _present = new HashSet<int>();
        readonly List<int> _lifted = new List<int>();
        bool _steering;

        public float Steer { get; private set; }        // -1 full left .. +1 full right
        public float Throttle { get; private set; }     // 0 or 1
        public float Brake { get; private set; }        // 0 or 1
        public bool PauseTapped { get; private set; }   // a finger came down on the pause button this update

        // For drawing: whether a thumb is on the wheel, where its centre is, and where it came down.
        public bool Steering => _steering;
        public float SteerCentreX { get; private set; }
        public float SteerCentreY { get; private set; }

        public void Update(IReadOnlyList<TouchPoint> touches, in TouchLayout layout)
        {
            Steer = 0f; Throttle = 0f; Brake = 0f; PauseTapped = false;
            _present.Clear();

            float slop = TouchLayout.Slop * layout.unit;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (!_present.Add(t.id)) continue;   // the same finger twice in one list

                if (!_roles.TryGetValue(t.id, out var role))
                {
                    role = Classify(t, layout, slop);
                    _roles[t.id] = role;
                    if (role == Role.Steer)
                    {
                        _steering = true;
                        SteerCentreX = t.x;
                        SteerCentreY = t.y;
                    }
                    else if (role == Role.Pause) PauseTapped = true;
                }

                switch (role)
                {
                    case Role.Steer:
                        float travel = layout.steerTravel > 1f ? layout.steerTravel : 1f;
                        float off = t.x - SteerCentreX;
                        if (off > travel) { SteerCentreX = t.x - travel; off = travel; }
                        else if (off < -travel) { SteerCentreX = t.x + travel; off = -travel; }
                        Steer = off / travel;
                        break;
                    case Role.Pedal:
                        if (layout.throttleHit.Contains(t.x, t.y)) Throttle = 1f;
                        else if (layout.brakeHit.Contains(t.x, t.y)) Brake = 1f;
                        break;
                }
            }

            _lifted.Clear();
            foreach (var id in _roles.Keys)
                if (!_present.Contains(id)) _lifted.Add(id);
            for (int i = 0; i < _lifted.Count; i++)
            {
                if (_roles[_lifted[i]] == Role.Steer) _steering = false;
                _roles.Remove(_lifted[i]);
            }
        }

        // Forget every finger: the controls were put away, and whatever was held when they went is not a
        // press when they come back.
        public void Reset()
        {
            _roles.Clear();
            _steering = false;
            Steer = 0f; Throttle = 0f; Brake = 0f; PauseTapped = false;
        }

        Role Classify(TouchPoint t, in TouchLayout layout, float slop)
        {
            if (layout.pause.Inflate(slop).Contains(t.x, t.y)) return Role.Pause;
            if (layout.steerZone.Contains(t.x, t.y)) return _steering ? Role.Ignored : Role.Steer;
            return Role.Pedal;
        }
    }
}
