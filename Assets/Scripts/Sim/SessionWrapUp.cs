namespace Draftmaster.Sim
{
    // What happens to the player's car once the clock on a practice or qualifying session has run out.
    //
    // The session does not end the moment the clock does. A driver still out on track finishes the lap they
    // are on — in qualifying that lap still counts — and then the crew bring the car in for them: the AI takes
    // over at the line and drives the in-lap down pit road into the player's own box. Or the driver comes in
    // themselves, whenever they like. Either way the car stops in its box, the driver climbs out, and only
    // then is the session over.
    //
    // Kept here, free of MonoBehaviour state, so the decision can be unit-tested in EditMode — the session
    // itself can only be judged in Play Mode, which is not always available.
    public static class SessionWrapUp
    {
        public enum Step
        {
            Wait,          // still running the lap out, or rolling down pit road to the box
            TakeOver,      // hand the car to the AI to drive it into the box
            CloseSession,  // nothing left to wait for: end the session now
        }

        // How long (s) a car may sit stopped on pit road away from its box before the crew come and push it
        // the rest of the way — a driver who pulled up short, or stopped in the wrong box.
        public const float StoppedOnPitRoadSeconds = 3f;

        // How long (s) after the line crossing the hand-over waits, so the lap that was just completed is
        // timed and scored before the car changes hands.
        public const float HandOverDelaySeconds = 0.5f;

        // The next step for the player's car after the flag.
        //
        // inCar           — sat in the car with the controls (false once on foot, after climbing out or a tow)
        // lapFinished     — has crossed the line at least once since the clock ran out
        // onPitRoad       — on the pit-lane surface: they are bringing it in themselves
        // stoppedSeconds  — how long the car has been stood still on pit road (0 while moving)
        // canTakeOver     — there is an AI brain on the car and a pit lane with boxes to drive it to
        public static Step Next(bool inCar, bool lapFinished, bool onPitRoad, float stoppedSeconds, bool canTakeOver)
        {
            // Already out of the car — climbed out in the box, towed in, or never got in at all.
            if (!inCar) return Step.CloseSession;

            // Coming in on their own. The climb-out in the box is automatic once the session is over, so this
            // only acts when the car has stopped short of the box (or past it) and stayed there.
            if (onPitRoad)
            {
                if (stoppedSeconds < StoppedOnPitRoadSeconds) return Step.Wait;
                return canTakeOver ? Step.TakeOver : Step.CloseSession;
            }

            if (!lapFinished) return Step.Wait;
            return canTakeOver ? Step.TakeOver : Step.CloseSession;
        }

        // Has the car crossed the line since the flag? Lap counts are the race tracker's, sampled at the flag.
        public static bool LapFinished(int lapAtFlag, int lapNow) => lapNow > lapAtFlag;
    }
}
