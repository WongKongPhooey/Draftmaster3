using System.Collections.Generic;

namespace Draftmaster.Controls
{
    // Where the on-screen walking controls sit: a floating left stick, and nothing else. The paddock has no
    // pedals and no wheel — it has a thumb that pushes a direction, and a finger that points at whoever the
    // player wants to talk to.
    //
    // The stick FLOATS: there is no fixed ring to find, the ring appears wherever the thumb lands in the left
    // half of the screen. A fixed one has to be hunted for on a screen the player is not looking at the edges
    // of, and a thumb that lands 10 px off it walks nowhere while the player is sure they are pushing.
    //
    // Measured in the UI's design pixels (the 640x360 grid) times `unit`, the same scale the driving controls
    // use, and kept inside the safe area.
    public readonly struct TouchWalkLayout
    {
        // Design-pixel sizes.
        public const float Margin = 12f;
        public const float RingSize = 64f;        // the ring drawn around the thumb's landing point
        public const float KnobSize = 26f;
        public const float Travel = 26f;          // thumb travel from centre to a full push
        public const float Deadzone = 0.22f;      // of travel; below this the thumb is resting, not pushing
        public const float ZoneTop = 0.18f;       // the stick takes the left side below this much of the height

        // A finger is a tap rather than a poke if it lifts soon enough and has not wandered.
        public const float TapSeconds = 0.45f;
        public const float TapSlop = 18f;

        public readonly TouchRect safe;
        public readonly float unit;

        public readonly TouchRect stickZone;      // a thumb landing here takes the stick
        public readonly TouchRect stickRest;      // where the ring waits while nobody holds it
        public readonly float travel;
        public readonly float tapSlop;

        public TouchWalkLayout(TouchRect safe, float unit)
        {
            this.safe = safe;
            this.unit = unit < 1f ? 1f : unit;
            float u = this.unit;

            float m = Margin * u, ring = RingSize * u;
            travel = Travel * u;
            tapSlop = TapSlop * u;

            // The left half, below the top strip: the top is where the objective banner and the clock live,
            // and a thumb reaching for those is not asking to walk.
            float zoneTop = safe.y + safe.height * ZoneTop;
            stickZone = new TouchRect(safe.x, zoneTop, safe.width * 0.5f, safe.yMax - zoneTop);

            stickRest = new TouchRect(safe.x + m, safe.yMax - m - ring, ring, ring);
        }
    }

    // What the thumb is asking for, worked out from the fingers on the glass each frame.
    //
    // Each finger's job is decided when it lands and kept until it lifts:
    //   - in the stick zone: the stick. One at a time, and the centre is wherever it came down, so there is
    //     nothing to aim at. Push past full travel and the centre is dragged along, so pulling back the other
    //     way turns around at once rather than crossing a dead patch.
    //   - anywhere else: a tap candidate. If it lifts inside TapSeconds without wandering further than
    //     TapSlop, it is a tap at the point it went down, and the game gets one.
    //
    // `now` is a clock in seconds — Time.unscaledTime from the runtime, a plain number from the tests.
    public sealed class TouchWalkState
    {
        enum Role { Stick, Tap, Ignored }

        struct Finger
        {
            public Role role;
            public float downX, downY, downAt;
            public bool wandered;
        }

        readonly Dictionary<int, Finger> _fingers = new Dictionary<int, Finger>();
        readonly HashSet<int> _present = new HashSet<int>();
        readonly List<int> _lifted = new List<int>();
        bool _walking;

        public float MoveX { get; private set; }    // -1 left .. +1 right
        public float MoveY { get; private set; }    // -1 back .. +1 forward (screen up)

        // A finger lifted as a tap this update, and where it went down. Top-left space, like everything here.
        public bool Tapped { get; private set; }
        public float TapX { get; private set; }
        public float TapY { get; private set; }

        // For drawing: whether a thumb is on the stick, where its centre is, and where the knob sits.
        public bool Walking => _walking;
        public float CentreX { get; private set; }
        public float CentreY { get; private set; }
        public float KnobX { get; private set; }
        public float KnobY { get; private set; }

        public void Update(IReadOnlyList<TouchPoint> touches, in TouchWalkLayout layout, float now)
        {
            MoveX = 0f; MoveY = 0f; Tapped = false;

            // Lifts first, then landings: a thumb taken off and put straight back down inside one frame is a
            // new stick thumb, not a second one to ignore. A lift is also the only place a tap can be born.
            _present.Clear();
            for (int i = 0; i < touches.Count; i++) _present.Add(touches[i].id);
            _lifted.Clear();
            foreach (var id in _fingers.Keys)
                if (!_present.Contains(id)) _lifted.Add(id);
            for (int i = 0; i < _lifted.Count; i++)
            {
                var f = _fingers[_lifted[i]];
                if (f.role == Role.Stick) _walking = false;
                else if (f.role == Role.Tap && !f.wandered && now - f.downAt <= TouchWalkLayout.TapSeconds)
                {
                    Tapped = true;
                    TapX = f.downX;
                    TapY = f.downY;
                }
                _fingers.Remove(_lifted[i]);
            }

            _present.Clear();
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (!_present.Add(t.id)) continue;   // the same finger twice in one list

                if (!_fingers.TryGetValue(t.id, out var f))
                {
                    f = new Finger
                    {
                        role = layout.stickZone.Contains(t.x, t.y)
                                   ? (_walking ? Role.Ignored : Role.Stick)
                                   : Role.Tap,
                        downX = t.x,
                        downY = t.y,
                        downAt = now,
                    };
                    if (f.role == Role.Stick)
                    {
                        _walking = true;
                        CentreX = t.x;
                        CentreY = t.y;
                    }
                    _fingers[t.id] = f;
                }

                if (f.role == Role.Stick)
                {
                    float travel = layout.travel > 1f ? layout.travel : 1f;
                    float dx = t.x - CentreX, dy = t.y - CentreY;

                    // Drag the centre along rather than clamping the reading: the thumb stays under the knob.
                    float len = Sqrt(dx * dx + dy * dy);
                    if (len > travel)
                    {
                        float back = (len - travel) / len;
                        CentreX += dx * back;
                        CentreY += dy * back;
                        dx -= dx * back;
                        dy -= dy * back;
                        len = travel;
                    }

                    KnobX = CentreX + dx;
                    KnobY = CentreY + dy;

                    float mag = len / travel;
                    if (mag >= TouchWalkLayout.Deadzone)
                    {
                        // Rescale from the edge of the deadzone, so the first millimetre of push is a slow
                        // walk rather than a lurch, and y flips: the glass counts downwards, the world up.
                        float scaled = (mag - TouchWalkLayout.Deadzone) / (1f - TouchWalkLayout.Deadzone);
                        if (scaled > 1f) scaled = 1f;
                        MoveX = dx / len * scaled;
                        MoveY = -dy / len * scaled;
                    }
                }
                else if (f.role == Role.Tap && !f.wandered)
                {
                    float dx = t.x - f.downX, dy = t.y - f.downY;
                    if (Sqrt(dx * dx + dy * dy) > layout.tapSlop)
                    {
                        f.wandered = true;
                        _fingers[t.id] = f;
                    }
                }
            }
        }

        // Forget every finger: the controls were put away. One still down when they come back is placed
        // afresh, so a thumb resting on the stick takes it from where it is with no jump — and cannot lift
        // into a tap the player never meant, because the finger it belonged to is gone.
        public void Reset()
        {
            _fingers.Clear();
            _walking = false;
            MoveX = 0f; MoveY = 0f;
            Tapped = false;
        }

        static float Sqrt(float v) => (float)System.Math.Sqrt(v);
    }
}
