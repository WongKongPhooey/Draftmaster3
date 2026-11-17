namespace Draftmaster.Controls
{
    // Draftmaster 2's lane controls, the rules half (OPTIONS > AI Drivers > Controls: Lanes). The car drives itself on
    // Auto and the player's left / right do not steer it: each press moves it one lane across the road - the inside
    // line, the middle line or the outside line - and it stops at the edges.
    //
    // A press is the steer input going past PressAt from rest, so a held key or a stick pushed over moves one lane, not
    // one a frame; it has to come back under ReleaseAt before it counts again. Steer already held when the lanes take
    // over (the player was turning when Auto came on) is "down" and has to be let go first.
    //
    // Fed the steer input (-1 full left .. +1 full right: a key, a stick, a touch button) once a frame. Pure C#, like
    // AutoHandBack.
    public sealed class LaneSelect
    {
        public const int Left = -1, Middle = 0, Right = 1;
        public const float PressAt = 0.5f;     // stick travel that counts as a press
        public const float ReleaseAt = 0.25f;  // and back under this, let go (a gap, so a stick's wobble isn't a second press)

        bool _leftDown, _rightDown;

        // -1 the left-hand lane, 0 the middle, +1 the right-hand lane.
        public int Lane { get; private set; }

        // Take over in `lane`, with whatever the steer input reads right now already counted as held.
        public void Reset(int lane, float steer)
        {
            Lane = Clamp(lane);
            _leftDown = steer <= -PressAt;
            _rightDown = steer >= PressAt;
        }

        // True on the frame a press moves the lane.
        public bool Update(float steer)
        {
            bool left = Edge(ref _leftDown, -steer);
            bool right = Edge(ref _rightDown, steer);
            if (left == right) return false;   // neither, or both at once (a stick can't, two keys can): no move
            int next = Clamp(Lane + (left ? -1 : 1));
            if (next == Lane) return false;
            Lane = next;
            return true;
        }

        static bool Edge(ref bool down, float value)
        {
            if (down)
            {
                if (value < ReleaseAt) down = false;
                return false;
            }
            if (value < PressAt) return false;
            down = true;
            return true;
        }

        static int Clamp(int lane) => lane < Left ? Left : lane > Right ? Right : lane;

        // What the lane is called. On an oval the side lanes are the inside and the outside (insideIsLeft: the infield
        // is on the left of travel, as on every oval on the calendar); on a road course, where the inside swaps from
        // corner to corner, they are left and right.
        public static string Name(int lane, bool oval, bool insideIsLeft = true)
        {
            if (lane == Middle) return "Middle";
            bool left = lane < Middle;
            if (!oval) return left ? "Left" : "Right";
            return left == insideIsLeft ? "Inside" : "Outside";
        }

        // The OPTIONS row.
        public static string Label(bool lanes) => lanes ? "Lanes" : "Steer";

        public static string Describe(bool lanes) => lanes
            ? "Your car drives itself. Left / right pick the lane."
            : "You steer the car yourself.";
    }
}
