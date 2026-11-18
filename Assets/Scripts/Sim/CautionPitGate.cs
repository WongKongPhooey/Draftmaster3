namespace Draftmaster.Sim
{
    // Who may come into the pits around a full-course caution, for one car (PitStopController). Engine-free, so the
    // rules are pinned headless (CautionPitGateTests).
    //
    //   * When the caution comes out, a damaged car comes in at the next opportunity: its first pass of the pit entry
    //     with pit road open - and only a pass it will make before pit road shuts again (`openMetresLeft`), so it is
    //     never still on its way in once the field has been told the green is coming.
    //   * A car that has made its stop under the caution does not come in again until after the restart.
    //   * Once the field has been told it is going green at the restart line (pit road shut for the rest of the
    //     caution - "Green next time by" / "Green at the line"), nobody pits on that lap: a car makes no stop until it
    //     has next crossed the start/finish line - the restart line. That covers a car at the back of the train that
    //     only reaches the pit entry after the green has waved at the front, and a car already on its way in but not
    //     yet committed to pit road turns it down (AbortRunIn).
    public sealed class CautionPitGate
    {
        bool _stopMade;       // made (or is making) its stop under the current caution
        bool _restartHold;    // told the green is coming; not over the restart line since
        bool _wasSignalled;
        float _prevDistance = -1f;

        // This car has had its stop under the current caution.
        public bool StopMadeThisCaution => _stopMade;
        // The green has been signalled and this car has not crossed the restart line since: no stop.
        public bool RestartLapHold => _restartHold;
        // A car on its way in but still out on the track turns the stop down.
        public bool AbortRunIn => _restartHold;

        // Once per physics step. `caution`: a full-course caution is out. `greenSignalled`: under it, pit road has shut
        // for good and the green comes at the restart line (FullCourseCaution.GreenSignalled). `onTrack`: on the main
        // track, not pit road. `distance`: metres along the lap from the start/finish line; `lap`: the lap's length.
        public void Step(bool caution, bool greenSignalled, bool onTrack, float distance, float lap)
        {
            if (greenSignalled && !_wasSignalled) _restartHold = true;
            _wasSignalled = greenSignalled;

            if (!onTrack || lap <= 0f) _prevDistance = -1f;
            else
            {
                if (_prevDistance >= 0f && CrossedLine(_prevDistance, distance, lap)) _restartHold = false;
                _prevDistance = distance;
            }

            // Green, and over the restart line: the restart has been run, and the next caution is a new one.
            if (!caution && !_restartHold) _stopMade = false;
        }

        // Whether the car may decide to come in now. `caution` / `pitRoadOpen`: FullCourseCaution (pit road is always
        // open under green). Under a caution, `toEntryM`: the road to the pit entry; `openMetresLeft`: how much further
        // the safety car runs before pit road shuts (FullCourseCaution.PitRoadOpenMetresLeft) - a car that would get
        // there after that leaves it for the restart.
        public bool MayPit(bool caution, bool pitRoadOpen, float toEntryM, float openMetresLeft)
        {
            if (_restartHold) return false;
            if (!caution) return true;
            if (!pitRoadOpen || _stopMade) return false;
            return toEntryM <= openMetresLeft;
        }

        // The car has committed to a stop. Under a caution it is its stop for this caution.
        public void CommittedToStop(bool caution)
        {
            if (caution) _stopMade = true;
        }

        // Whether a car that was `prev` metres along the lap a step ago and is `now` metres along it has gone over the
        // start/finish line forwards (the lap distance wrapped).
        public static bool CrossedLine(float prev, float now, float lap) => lap > 0f && now - prev < -0.5f * lap;
    }
}
