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

    // How the left thumb steers: a wheel it turns, or a left and a right button.
    public enum TouchSteerMode { Wheel, Buttons }

    // Where the on-screen driving controls sit: a steering wheel under the left thumb, brake and throttle under
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
        public const float WheelSize = 136f;      // the wheel's diameter: as wide as the old steering strip
        public const float WheelLock = 45f;       // degrees either way from straight to full lock
        public const float WheelHub = 0.2f;       // a thumb this close to the hub (fraction of the radius) can't turn it
        public const float PauseSize = 24f;
        public const float SteerZoneTop = 0.25f;  // steering takes the left side below this fraction of the height

        public readonly TouchRect safe;
        public readonly float unit;

        public readonly TouchRect steerZone;      // a thumb landing here takes the wheel
        public readonly TouchRect wheel;          // the whole wheel, hub at its centre, all of it on screen; it never moves

        // Button steering: two pedal-sized buttons in the bottom-left corner, mirroring the pedals.
        public readonly TouchRect steerLeft, steerRight;          // drawn
        public readonly TouchRect steerLeftHit, steerRightHit;    // pressed: drawn plus slop, split at the gap

        public readonly TouchRect brake, throttle;          // drawn
        public readonly TouchRect brakeHit, throttleHit;    // pressed: the drawn pedal plus slop, split at the gap
        public readonly TouchRect pause;
        // Either side of pause, same size: the pit limiter (only while it can be toggled) and the broadcast
        // view. A keyboard has L and V for them; a phone has these.
        public readonly TouchRect limiter, broadcast;
        // Right of the broadcast view, pause-sized: where the crew chief's headset goes while the phone is held
        // upright (swing camera). Drawn by CrewChiefController, not here, so it takes no touch role.
        public readonly TouchRect crewChief;

        // The size of one design pixel on a screen this big: whole numbers, so the controls land on a clean
        // pixel grid. Taken from the short side so a portrait screen isn't given pedals wider than itself.
        // Rounded up, where the HUD's scale rounds down: a pedal is a thumb target first, and on a screen
        // between steps (480 or 800 lines) rounding down would shrink it to a fifth of the height or less.
        // Up, the pedals stay between about a quarter and two fifths of the height on any screen.
        public static float UnitFor(float screenWidth, float screenHeight)
        {
            float shortSide = screenWidth < screenHeight ? screenWidth : screenHeight;
            float u = (float)System.Math.Ceiling(shortSide / 360f - 0.001f);
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
            float side = p * 2f, sideGap = 8f * u;
            limiter = new TouchRect(pause.x - sideGap - side, pause.y, side, p);
            broadcast = new TouchRect(pause.xMax + sideGap, pause.y, side, p);
            crewChief = new TouchRect(broadcast.xMax + sideGap, pause.y, p, p);

            // Steering has the left half, stopping short of the brake on a narrow screen, and leaves the top of
            // the screen alone so the pause button and the HUD up there are never mistaken for a steer.
            float zoneTop = safe.y + safe.height * SteerZoneTop;
            float zoneRight = System.Math.Min(safe.x + safe.width * 0.5f, brakeHit.x - gap);
            steerZone = new TouchRect(safe.x, zoneTop, zoneRight - safe.x, safe.yMax - zoneTop);

            // The whole wheel, standing on the bottom margin like the pedals. On a short screen it shrinks to
            // fit under the top of the steering zone rather than reach up into the HUD.
            float d = System.Math.Min(WheelSize * u, safe.yMax - m - zoneTop);
            wheel = new TouchRect(safe.x + m, safe.yMax - m - d, d, d);

            // The pedals mirrored: left button in the corner, right beside it. The hit areas meet in the middle
            // of the gap and run out to the safe area's edge, but stay inside the steering zone.
            steerLeft = new TouchRect(safe.x + m, safe.yMax - m - h, w, h);
            steerRight = new TouchRect(steerLeft.xMax + gap, steerLeft.y, w, h);
            float steerSplit = steerLeft.xMax + gap * 0.5f;
            float steerTop = System.Math.Max(steerLeft.y - slop, steerZone.y);
            float steerBottom = steerZone.yMax;
            steerLeftHit = new TouchRect(steerZone.x, steerTop, steerSplit - steerZone.x, steerBottom - steerTop);
            float rightEdge = System.Math.Min(steerRight.xMax + slop, steerZone.xMax);
            steerRightHit = new TouchRect(steerSplit, steerTop, rightEdge - steerSplit, steerBottom - steerTop);
        }
    }

    // What the thumbs are asking for, worked out from the fingers on the glass each frame.
    //
    // Each finger's job is decided the moment it lands and kept until it lifts:
    //   - on the pause button: a pause, once;
    //   - in the steering zone, wheel mode: the wheel. It is fixed in its corner and the thumb turns it
    //     about its hub, from wherever the thumb landed — the rim under the thumb stays under the thumb.
    //     Anticlockwise is left; 45 degrees either way is full lock, and further holds full lock until the
    //     thumb comes back inside it. A thumb on the hub can't turn it and holds the lock it has.
    //     One thumb steers at a time.
    //   - in the steering zone, button mode: a steering thumb that presses whichever button it is over right
    //     now, like a pedal thumb. Left and right held together cancel.
    //   - anywhere else: a pedal thumb, which presses whichever pedal it is over right now. Sliding from the
    //     brake to the throttle works, and a steering thumb that strays over the pedals presses nothing.
    public sealed class TouchDriveState
    {
        enum Role { Steer, SteerButton, Pedal, Pause, Limiter, Broadcast, Ignored }

        readonly Dictionary<int, Role> _roles = new Dictionary<int, Role>();
        readonly HashSet<int> _present = new HashSet<int>();
        readonly List<int> _lifted = new List<int>();
        bool _steering;
        bool _gripped;        // the steering thumb has an angle about the hub to turn from
        float _thumbAngle;    // where it was last update, degrees clockwise from straight up
        float _turned;        // how far it has turned the wheel since it landed, unwrapped
        float _wheelSteer;    // the lock that gives, kept while the thumb is on the hub
        bool _returning;   // the first update since Reset: fingers already down did not land just now

        public float Steer { get; private set; }        // -1 full left .. +1 full right
        public float Throttle { get; private set; }     // 0 or 1
        public float Brake { get; private set; }        // 0 or 1
        public bool PauseTapped { get; private set; }   // a finger came down on the pause button this update
        public bool LimiterTapped { get; private set; }
        public bool BroadcastTapped { get; private set; }

        // Whether those two buttons are on screen. A button that is not drawn takes no finger: one landing
        // where it would be is a pedal thumb, as it always was.
        public bool LimiterShown { get; set; }
        public bool BroadcastShown { get; set; }

        // Broadcast view: the AI has the car, so the wheel and pedals are put away and only the buttons at the
        // top stand. Fingers anywhere else do nothing.
        public bool ButtonsOnly { get; set; }

        public TouchSteerMode SteerMode { get; set; }

        // Button mode, for drawing: which steering buttons are held.
        public bool SteerLeftHeld { get; private set; }
        public bool SteerRightHeld { get; private set; }

        // For drawing: whether a thumb is on the wheel, and how far the wheel is turned (degrees, + is right).
        public bool Steering => _steering;
        public float WheelAngle => Steer * TouchLayout.WheelLock;

        public void Update(IReadOnlyList<TouchPoint> touches, in TouchLayout layout)
        {
            Steer = 0f; Throttle = 0f; Brake = 0f; PauseTapped = false; LimiterTapped = false; BroadcastTapped = false;
            SteerLeftHeld = false; SteerRightHeld = false;

            // Let go of the fingers that have lifted before placing the ones that have landed: a thumb taken
            // off the wheel and put straight back down inside one frame is a new steering thumb, not a second
            // one to ignore.
            _present.Clear();
            for (int i = 0; i < touches.Count; i++) _present.Add(touches[i].id);
            _lifted.Clear();
            foreach (var id in _roles.Keys)
                if (!_present.Contains(id)) _lifted.Add(id);
            for (int i = 0; i < _lifted.Count; i++)
            {
                if (_roles[_lifted[i]] == Role.Steer) _steering = false;
                _roles.Remove(_lifted[i]);
            }

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
                    if (role == Role.Steer) { _steering = true; _gripped = false; _turned = 0f; _wheelSteer = 0f; }
                    else if (role == Role.Pause) PauseTapped = true;
                    else if (role == Role.Limiter) LimiterTapped = true;
                    else if (role == Role.Broadcast) BroadcastTapped = true;
                }

                switch (role)
                {
                    case Role.Steer:
                        Steer = TurnWheel(t, layout);
                        break;
                    case Role.SteerButton:
                        if (layout.steerLeftHit.Contains(t.x, t.y)) SteerLeftHeld = true;
                        else if (layout.steerRightHit.Contains(t.x, t.y)) SteerRightHeld = true;
                        break;
                    case Role.Pedal:
                        if (layout.throttleHit.Contains(t.x, t.y)) Throttle = 1f;
                        else if (layout.brakeHit.Contains(t.x, t.y)) Brake = 1f;
                        break;
                }
            }
            if (SteerMode == TouchSteerMode.Buttons) Steer = (SteerRightHeld ? 1f : 0f) - (SteerLeftHeld ? 1f : 0f);
            _returning = false;
        }

        // Forget every finger: the controls were put away. A thumb still down when they come back is placed
        // afresh, as if it had just landed — a thumb resting on the wheel steers from where it is — except
        // on the pause button: the finger that tapped it to open the pause menu is not a second tap once the
        // menu has gone.
        public void Reset()
        {
            _roles.Clear();
            _steering = false;
            _returning = true;
            Steer = 0f; Throttle = 0f; Brake = 0f; PauseTapped = false; LimiterTapped = false; BroadcastTapped = false;
            SteerLeftHeld = false; SteerRightHeld = false;
        }

        // The wheel turns by however far the thumb has gone round the hub since last update. Each step is taken
        // the short way round, so a thumb circling past the bottom keeps counting rather than flipping sides.
        float TurnWheel(TouchPoint t, in TouchLayout layout)
        {
            float dx = t.x - layout.wheel.centerX, dy = t.y - layout.wheel.centerY;
            float hub = layout.wheel.width * 0.5f * TouchLayout.WheelHub;
            if (dx * dx + dy * dy < hub * hub) return _wheelSteer;

            float angle = (float)(System.Math.Atan2(dx, -dy) * 180.0 / System.Math.PI);
            if (_gripped)
            {
                float step = angle - _thumbAngle;
                if (step > 180f) step -= 360f;
                else if (step < -180f) step += 360f;
                _turned += step;
            }
            _gripped = true;
            _thumbAngle = angle;

            float lockFrac = _turned / TouchLayout.WheelLock;
            _wheelSteer = lockFrac > 1f ? 1f : lockFrac < -1f ? -1f : lockFrac;
            return _wheelSteer;
        }

        Role Classify(TouchPoint t, in TouchLayout layout, float slop)
        {
            if (layout.pause.Inflate(slop).Contains(t.x, t.y)) return _returning ? Role.Ignored : Role.Pause;
            if (LimiterShown && layout.limiter.Inflate(slop).Contains(t.x, t.y)) return _returning ? Role.Ignored : Role.Limiter;
            if (BroadcastShown && layout.broadcast.Inflate(slop).Contains(t.x, t.y)) return _returning ? Role.Ignored : Role.Broadcast;
            if (ButtonsOnly) return Role.Ignored;
            if (layout.steerZone.Contains(t.x, t.y))
            {
                if (SteerMode == TouchSteerMode.Buttons) return Role.SteerButton;
                return _steering ? Role.Ignored : Role.Steer;
            }
            return Role.Pedal;
        }
    }
}
