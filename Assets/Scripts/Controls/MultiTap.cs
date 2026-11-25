namespace Draftmaster.Controls
{
    // A run of quick taps on one input: N presses, each a short press-and-release, each starting soon after the last
    // let go. It fires THE MOMENT the Nth press starts, not when it lets go - so the last press, held, is already
    // doing whatever the input does once the run has fired (a steering key steering the car the player has just
    // taken off Auto).
    //
    // A press held too long, or a pause too long between presses, starts the count again from that press. A press
    // that fires never counts as the first of another run, so a fourth tap doesn't half-arm a second one.
    //
    // Fed the input's value (0..1: a key, a stick's tilt one way, a touch button) and a clock in seconds -
    // Time.unscaledTime from the runtime, a plain number from the tests. Pure C#, like StickDoubleTap.
    public sealed class MultiTap
    {
        public const float PushAt = 0.5f;          // value that counts as pressed
        public const float ReleaseAt = 0.25f;      // and back below this, let go (a gap, so a wobble isn't a tap)
        public const float TapSeconds = 0.25f;     // a press shorter than this is a tap
        public const float GapSeconds = 0.3f;      // from a tap's release to the next press

        public int Taps { get; }

        bool _pushed;
        float _pushedAt;
        bool _thisPushFired;
        int _count;              // taps completed in the current run
        float _tapEndedAt;

        public MultiTap(int taps) { Taps = taps < 1 ? 1 : taps; }

        // True on the update the Nth press of a quick run starts.
        public bool Update(float value, float now)
        {
            if (!_pushed)
            {
                if (value < PushAt) return false;
                _pushed = true;
                _pushedAt = now;
                // Carries on the run only straight after a tap; otherwise this press is the first of a new one.
                if (_count > 0 && now - _tapEndedAt > GapSeconds) _count = 0;
                _thisPushFired = _count + 1 >= Taps;
                if (_thisPushFired) _count = 0;
                return _thisPushFired;
            }

            if (value < ReleaseAt)
            {
                _pushed = false;
                if (_thisPushFired) _count = 0;
                else if (now - _pushedAt <= TapSeconds) _count++;
                else _count = 0;   // held: not a tap, the run is broken
                _thisPushFired = false;
                _tapEndedAt = now;
            }
            return false;
        }

        // The input went to something else for a while (a menu, Auto off): forget any half-finished run. A press
        // already under way has to be let go before it counts, so one held through a menu can't start a run.
        public void Interrupt()
        {
            _pushed = true;
            _pushedAt = float.NegativeInfinity;
            _thisPushFired = false;
            _count = 0;
        }
    }
}
