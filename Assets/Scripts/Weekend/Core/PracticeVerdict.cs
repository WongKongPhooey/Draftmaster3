using UnityEngine;

namespace Draftmaster.Weekend
{
    // What a practice session was worth to the crew and the engineers.
    //
    // Two things are being judged, and they pay into different meters:
    //
    //   Work rate (laps)  - laps are data. Setup knowledge comes from laps and nothing else, and the crew
    //                       want to see their driver out there using the car. No laps at all is a wasted
    //                       session and they let you know; three is a proper run; past that it keeps
    //                       paying, more slowly, up to a full run sheet.
    //   Pace (position)   - where your best lap left you on the timing screen. A crew will forgive a short
    //                       run that was quick and will not be cheered up by a long run at the back. The
    //                       top eight and the bottom five are where it bites hardest; the middle of the
    //                       pack is close to neutral.
    //
    // Morale is the two added together, and pace is weighted so it can outvote the laps: a single lap in the
    // top eight still lifts the garage, and even a full run sheet down in the bottom five still sinks it. The
    // trade-off survives because setup knowledge only ever comes from laps - one quick lap leaves the
    // engineers with very little to work with however good it looked.
    //
    // Pure arithmetic so the EditMode tests can sweep it; PracticeDirector feeds it the timing rows.
    public static class PracticeVerdict
    {
        public const int FullRunLaps = 12;       // a full run sheet; past this the engineers have enough
        public const int ProperRunLaps = 3;      // the point where a run stops being a token appearance
        public const int TopGroup = 8;           // "in the top eight"
        public const int BottomGroup = 5;        // "in the bottom five"

        // Morale for the laps alone. Zero is a real hit, one or two is a shrug, three and up is a positive
        // that keeps growing to a full run sheet.
        public static float WorkRateMorale(int laps)
        {
            if (laps <= 0) return -6f;
            if (laps == 1) return -2f;
            if (laps == 2) return -0.5f;
            float t = Mathf.Clamp01((laps - ProperRunLaps) / (float)(FullRunLaps - ProperRunLaps));
            return Mathf.Lerp(2f, 5f, t);
        }

        // Morale for where the player's best lap left them. Position is 1-based in a field of fieldSize cars;
        // 0 (or no time set) means there is nothing to judge and pace stays out of it.
        //
        // A sliding scale over the whole field, plus a step at each end: the top eight earn a bonus and the
        // bottom five a penalty. In a field too small for the two groups not to overlap they cancel, and the
        // sliding scale decides on its own.
        public static float PaceMorale(int position, int fieldSize)
        {
            if (position <= 0 || fieldSize <= 1) return 0f;
            position = Mathf.Clamp(position, 1, fieldSize);

            float rank01 = 1f - (position - 1) / (float)(fieldSize - 1);
            float m = Mathf.Lerp(-6f, 6f, rank01);
            if (InTopGroup(position)) m += 2f;
            if (InBottomGroup(position, fieldSize)) m -= 2f;
            return m;
        }

        public static bool InTopGroup(int position) => position >= 1 && position <= TopGroup;

        public static bool InBottomGroup(int position, int fieldSize) =>
            position >= 1 && fieldSize > 1 && position > fieldSize - BottomGroup;

        // The whole session as a weekend outcome. laps = the player's valid laps; position/fieldSize from the
        // session's timing order (position 0 when the player never set a time).
        public static WeekendOutcome Evaluate(int laps, int position, int fieldSize)
        {
            laps = Mathf.Max(0, laps);
            bool timed = laps > 0 && position > 0 && fieldSize > 0;

            float run01 = Mathf.Clamp01(laps / (float)FullRunLaps);
            float rank01 = timed && fieldSize > 1
                ? 1f - (Mathf.Clamp(position, 1, fieldSize) - 1) / (float)(fieldSize - 1)
                : timed ? 1f : 0f;

            var o = WeekendOutcome.Nothing;
            o.setupGain = run01 * 0.28f;
            o.teamMorale = WorkRateMorale(laps) + (timed ? PaceMorale(position, fieldSize) : 0f);
            o.score = timed ? Mathf.Clamp01(run01 * 0.5f + rank01 * 0.5f) : 0f;
            o.statKey = "practicesessions";
            o.statCount = 1;
            o.headline = Headline(laps, timed ? position : 0, fieldSize);
            return o;
        }

        static string Headline(int laps, int position, int fieldSize)
        {
            if (laps <= 0) return "Sat in the car and never turned a lap. The engineers have nothing.";

            string lapText = laps == 1 ? "one lap" : $"{laps} laps";
            string where = $"P{position} of {fieldSize}";
            bool top = InTopGroup(position), bottom = InBottomGroup(position, fieldSize);
            bool shortRun = laps < ProperRunLaps;

            if (position == 1 && !bottom)
                return shortRun
                    ? $"Fastest in practice on {lapText}. The garage loved it - the engineers wanted more of it."
                    : $"Top of the timesheet after {lapText}. The garage is buzzing.";
            if (top && !bottom)
                return shortRun
                    ? $"{where} on just {lapText}. Quick, but the engineers wanted more running."
                    : $"{where} with {lapText} in the book. The crew like what they are seeing.";
            if (bottom && !top)
                return shortRun
                    ? $"{where} on {lapText}. Slow and short - not a lot for anybody to smile about."
                    : $"{lapText} in the book but {where}. The run sheet is full and the mood is not.";
            return shortRun
                ? $"{where} on {lapText}. The engineers wanted more running than that."
                : $"{where} with {lapText} in the book and a run sheet worth reading.";
        }
    }
}
