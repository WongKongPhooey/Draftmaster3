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

        // How far (m) up the road from the race leader the safety car comes out: at least `minGap`, and far enough for a
        // leader at `leaderMps` to come down to the safety car's `cruiseMps` braking at `decelMps2`, plus `minGap` to
        // spare. A fixed 80 m was fine from 140 mph; from a plate pack's 190 it asked for over 3 g and the leader drove
        // straight past the safety car, splitting the field a lap apart.
        public static float PickupGap(float leaderMps, float cruiseMps, float decelMps2, float minGap)
        {
            float closing = UnityEngine.Mathf.Max(0f, leaderMps * leaderMps - cruiseMps * cruiseMps);
            return UnityEngine.Mathf.Max(minGap, closing / (2f * UnityEngine.Mathf.Max(0.5f, decelMps2)) + minGap);
        }

        // When the caution comes out, a car does not stamp on the brakes: it drives on round at this share of its
        // racing pace until it reaches the train behind the safety car (or the car in front that already has).
        public const float ApproachPace = 0.85f;
        // The gentle lift (m/s^2) from racing pace down to ApproachPace, and from there down onto the train. A pace or
        // speed cap under a caution never falls faster than this, unless a car right in front calls for more.
        public const float ApproachDecelMps2 = 2f;

        // How far (m) up the road from the race leader the safety car comes out under a caution: far enough for the
        // leader, still at `leaderMps` as the flag comes out, to come down to the safety car's `cruiseMps` at a lift
        // gentler than the field's own approach (ApproachDecelMps2), so it never arrives above its arrival envelope
        // and has to brake harder. Placed off the leader's approach pace instead, the leader started out already over
        // that envelope and ran up to the safety car too fast to stop behind it.
        public static float CautionPickupGap(float leaderMps, float cruiseMps, float minGap)
            => PickupGap(leaderMps, cruiseMps, ApproachDecelMps2 * 0.75f, minGap);

        // The fastest (m/s) a car may run with `gapM` of road to the car in front, which is doing `aheadMps`, and
        // wanting to settle `stationM` behind it: it can still come down to that car's speed at `decelMps2` by then.
        // Off the car in front's ACTUAL speed - a car still running ApproachPace up the road holds nobody back.
        public static float ApproachCapMps(float gapM, float stationM, float aheadMps, float decelMps2)
        {
            float room = UnityEngine.Mathf.Max(0f, gapM - stationM);
            float v = UnityEngine.Mathf.Max(0f, aheadMps);
            return UnityEngine.Mathf.Sqrt(v * v + 2f * UnityEngine.Mathf.Max(0.01f, decelMps2) * room);
        }

        // A cap `dt` seconds on from `previous`, heading for `target`: it may rise at once, but falls no faster than
        // `easeMps2`. Infinity (no cap last step) takes the target straight away.
        public static float EaseDown(float previous, float target, float easeMps2, float dt)
        {
            if (float.IsInfinity(previous) || float.IsNaN(previous) || target >= previous) return target;
            return UnityEngine.Mathf.Max(target, previous - UnityEngine.Mathf.Max(0f, easeMps2) * UnityEngine.Mathf.Max(0f, dt));
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

    // Overtime (RaceDirector): a caution that is out with fewer than two laps left would see the race finish behind
    // the safety car, so the distance is extended — once the field goes green again it gets two laps of racing to
    // the chequer. It happens once a race: a caution after the overtime green finishes the race under caution.
    public static class OvertimeRules
    {
        // Laps of green racing the overtime restart is given.
        public const int Laps = 2;
        // A green waved this little way (of a lap) past the line is taken as waved at it — the lap is not counted again.
        public const float AtLineTolerance = 0.05f;

        // Whether the caution out now sends the race to overtime. `scheduledLaps`: the race distance before any
        // extension. `leaderLaps`: racing laps the leader has run, plus the fraction of the current one.
        // `overtimeRun`: the race has already gone green in overtime.
        public static bool Triggers(int scheduledLaps, float leaderLaps, bool overtimeRun)
            => !overtimeRun && scheduledLaps > 0 && scheduledLaps - leaderLaps < Laps;

        // The race distance in overtime if the green waved with the leader here: two laps from the line crossing
        // the green waves at — the next one, unless the leader is only just past the line. Under the caution it
        // is always at least two laps beyond the laps the leader has completed, so nobody takes the chequer
        // behind the safety car. Never shorter than the scheduled distance.
        public static int Distance(int scheduledLaps, float leaderLaps)
        {
            int fromLine = (int)System.Math.Ceiling(leaderLaps - AtLineTolerance);
            int laps = fromLine + Laps;
            return laps > scheduledLaps ? laps : scheduledLaps;
        }
    }
}
