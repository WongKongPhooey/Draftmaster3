namespace Draftmaster.Sim
{
    // The full-course caution's rules, lifted out of FullCourseCaution / SafetyCar so they can be pinned in EditMode.
    //
    // On an oval a car stopped on the track brings out the safety car: the field closes up single file behind it,
    // pit road stays open so damaged cars can be repaired, and once the safety car has run its minimum laps and the
    // track is clear it pits and the green waves at the line.
    public static class CautionRules
    {
        // Whether a car stopped on the track brings out a full-course caution now.
        // `oval`: only ovals run cautions (a road course keeps its local yellows). `racing`: the race is green — not
        // the pace lap, not already under caution. `secondsSinceGreen`: since the last green (start or restart).
        // `stoppedSeconds`: how long the longest-standing stopped car (that has not already had its caution) has been
        // there; negative = none. `raceOver`: the chequer is out.
        public static bool Declare(bool oval, bool racing, bool raceOver, float secondsSinceGreen, float minSecondsAfterGreen,
                                   float stoppedSeconds, float declareAfterSeconds)
        {
            if (!oval || !racing || raceOver) return false;
            if (secondsSinceGreen < minSecondsAfterGreen) return false;
            return stoppedSeconds >= 0f && stoppedSeconds >= declareAfterSeconds;
        }

        // Whether the safety car is held out for now: the track has not been clear for `clearNeededSeconds` yet
        // (`clearForSeconds` < 0 = something is still stopped on it). Never past `maxLaps` laps behind it — a car that
        // cannot be moved must not hold the race under yellow for ever.
        public static bool HoldOut(float clearForSeconds, float clearNeededSeconds, float lapsRun, float maxLaps)
        {
            if (maxLaps > 0f && lapsRun >= maxLaps) return false;
            return clearForSeconds < clearNeededSeconds;
        }

        // Metres the safety car must cover, from where it came out, before it may take the pit entry. The green waves
        // as the leader reaches the line after it pits, so the run from the entry to the line counts towards `minLaps`
        // laps; the window it commits inside (`window`) is taken off so it is not a whole lap late. Never under half a
        // lap, so it can't come straight back in. `pitEntry` < 0 = no pit entry known: the full laps.
        public static float PitFloor(float lap, float pitEntry, float startFinish, float minLaps, float window)
        {
            if (lap <= 0f) return 0f;
            float laps = minLaps > 0f ? minLaps : 0f;
            if (pitEntry < 0f) return laps * lap;
            float entryToLine = Repeat(startFinish - pitEntry, lap);
            float floor = laps * lap - entryToLine - (window > 0f ? window : 0f);
            return floor > lap * 0.5f ? floor : lap * 0.5f;
        }

        static float Repeat(float t, float length)
        {
            float r = t - (float)System.Math.Floor(t / length) * length;
            return r >= length ? 0f : r;
        }
    }
}
