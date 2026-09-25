namespace Draftmaster.Controls
{
    // Double-tap the walking stick to flip between walk and run.
    //
    // A tap is a quick flick out and back. A second push that starts soon after a tap flips the mode THE MOMENT
    // it starts, not when it lets go — so tap, then push and hold, and the held push is already walking in the
    // new gait. That push is the double-tap's second half and never counts as the first half of another, or a
    // triple tap would flip twice and land back where it began.
    //
    // Fed the stick's raw tilt (0..1) and a clock in seconds — Time.unscaledTime from the runtime, a plain number
    // from the tests. Pure C#, like TouchWalkState.
    public sealed class StickDoubleTap
    {
        public const float PushAt = 0.5f;          // tilt that counts as pushed
        public const float ReleaseAt = 0.25f;      // and back below this, let go (a gap, so a wobble isn't a tap)
        public const float TapSeconds = 0.25f;     // a push shorter than this is a tap
        public const float GapSeconds = 0.3f;      // from the tap's release to the second push

        bool _pushed;
        float _pushedAt;
        bool _thisPushFlipped;
        bool _armed;             // a tap has just finished; the next push inside the gap flips
        float _tapEndedAt;

        public bool Running { get; private set; }

        // True on the update the mode flips.
        public bool Update(float tilt, float now)
        {
            if (!_pushed)
            {
                if (tilt < PushAt) return false;
                _pushed = true;
                _pushedAt = now;
                _thisPushFlipped = _armed && now - _tapEndedAt <= GapSeconds;
                _armed = false;
                if (_thisPushFlipped) Running = !Running;
                return _thisPushFlipped;
            }

            if (tilt < ReleaseAt)
            {
                _pushed = false;
                _armed = !_thisPushFlipped && now - _pushedAt <= TapSeconds;
                _tapEndedAt = now;
            }
            return false;
        }

        // The stick went to something else for a while (a menu, a conversation): forget any half-finished
        // double tap, keep the gait. A push already under way when the stick comes back has to be let go
        // before it counts, so a stick flicked through a menu can't land as the first half of a tap.
        public void Interrupt()
        {
            _pushed = true;
            _pushedAt = float.NegativeInfinity;
            _thisPushFlipped = false;
            _armed = false;
        }

        // Back to walking, with no half-finished double tap pending.
        public void Reset()
        {
            Running = false;
            _pushed = false;
            _armed = false;
            _thisPushFlipped = false;
        }
    }
}
