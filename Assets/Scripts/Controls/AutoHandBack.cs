namespace Draftmaster.Controls
{
    // Turning Auto off does not snatch the car straight back off the AI: the AI keeps driving until the player
    // taps the accelerator or the brake, and that tap is the moment control changes hands. Coming off Auto mid-
    // corner with no thumb on anything would otherwise hand the player a car at 180 mph with the pedals up.
    //
    // A tap is a fresh press — the pedal going down after the hand-back was asked for. A pedal already held when
    // Auto is switched off (a thumb resting on the trigger) has to come up and go down again before it counts,
    // so the car is never handed over by a press the player made before they asked for it.
    //
    // Fed each pedal's value (0..1: a trigger, a key or a touch pedal) once a frame. Pure C#, like StickDoubleTap.
    public sealed class AutoHandBack
    {
        public const float PressAt = 0.3f;     // pedal travel that counts as pressed
        public const float ReleaseAt = 0.1f;   // and back below this, let go (a gap, so a trigger's rest jitter isn't a tap)

        bool _throttleDown, _brakeDown;

        // The player has turned Auto off and the AI is waiting for a pedal tap to hand the car over.
        public bool Waiting { get; private set; }

        // Turn Auto off: wait for a tap. Whatever the pedals read right now is already "down" and does not count.
        public void Arm(float throttle, float brake)
        {
            Waiting = true;
            _throttleDown = throttle >= PressAt;
            _brakeDown = brake >= PressAt;
        }

        // The hand-back is off (Auto back on, or something else moved the car): stop waiting.
        public void Cancel() => Waiting = false;

        // True on the frame the player taps either pedal while waiting; the wait ends there.
        public bool Update(float throttle, float brake)
        {
            bool tapped = Edge(ref _throttleDown, throttle) | Edge(ref _brakeDown, brake);
            if (!Waiting || !tapped) return false;
            Waiting = false;
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
    }
}
