namespace Draftmaster.Controls
{
    // Android turns a pad button that nobody claimed into a key: B falls back to BACK — which the input system
    // reports as Esc — and A, X and Start fall back to DPAD_CENTER. The game reads the button off the pad
    // itself and then reads the fallback a moment later as a separate keyboard press. So on a phone, B in a
    // menu would back out twice (the pause menu would close and open straight back up) and B in the car,
    // drive / broadcast, would open the pause menu as well.
    //
    // This decides which keyboard presses are those echoes: any that land within Window seconds of a pad
    // button going down. A player pressing a real key that soon after a pad button is not a thing that
    // happens. Times are the input system's event timestamps, in seconds.
    public sealed class PadKeyEcho
    {
        public const double Window = 0.2;

        double _lastPadPress = double.NegativeInfinity;

        public void PadPressed(double time)
        {
            if (time > _lastPadPress) _lastPadPress = time;
        }

        // A keyboard press at `time` is the echo of a pad press, and should be dropped.
        public bool IsEcho(double time) => time >= _lastPadPress && time - _lastPadPress <= Window;

        public void Reset() => _lastPadPress = double.NegativeInfinity;
    }
}
