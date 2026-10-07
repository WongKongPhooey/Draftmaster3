using UnityEngine;

namespace Draftmaster.Sim
{
    // How a driver's database stats become the ratings they race on THIS weekend — the NR2003 model.
    //
    // NR2003 never gave an AI driver one fixed number. Each rating was a band (AIParamMean ± AIParamDeviation),
    // kept separately per track type (short track, speedway, superspeedway, road course), and when the event
    // loaded the game rolled a value inside the band that the driver kept for that event only. Two things fall
    // out of that and they are most of why its fields felt alive:
    //   * Specialists — a road-course ace is mid-pack at Talladega, a plate racer is lost at Sonoma. The order
    //     reshuffles track to track instead of the same five cars winning everywhere.
    //   * Form — a narrow band (a consistent driver) gives the same result every week; a wide one has days where
    //     a backmarker runs top ten and days where a star struggles. Consistency is the width of the band.
    //
    // Draftmaster's drivers already carry the track-type aptitudes (ShortTracks, Speedways, ...); before this
    // the AI only ever read Qualifying, so they were shown on the driver card and changed nothing on track.
    //
    // Pure maths with the roll injected, so EditMode tests can pin it down; AIDriverBinding is the caller.
    public struct RaceRatings
    {
        public float strength01;      // raw pace this event: drives paceMultiplier and corner commitment
        public float aggression01;    // propensity to start a pass / hold a line this event
        public float consistency01;   // the stat itself — it is the band width, so it is not rolled
    }

    public static class AIRatings
    {
        // Share of event strength that comes from the track-type aptitude; the rest is raw one-lap speed
        // (Qualifying). Half and half: a specialist gains about a third of the field-wide pace spread at their
        // kind of track, which is enough to reorder a field without a great driver ever looking lost.
        public const float AptitudeWeight = 0.5f;

        // Half-width of the strength band, as a share of the 0-1 scale. NR2003 rosters ran ~5 points wide for
        // the metronomes and ~20 for the journeymen; these are the same on a 0-1 scale.
        public const float MinStrengthDeviation = 0.025f;
        public const float MaxStrengthDeviation = 0.10f;

        // Aggression varies event to event too (a driver with nothing to lose, a driver protecting points), but
        // less than speed does: it is temperament, not form.
        public const float AggressionDeviation = 0.05f;

        // Event strength before the roll: track-type aptitude blended with raw speed.
        public static float StrengthMean(int qualifying, int trackAptitude, int statMax)
        {
            float q = Norm(qualifying, statMax);
            float apt = Norm(trackAptitude, statMax);
            return Mathf.Lerp(q, apt, AptitudeWeight);
        }

        // Half-width of the band. Consistency narrows it: a consistency-max driver is near-certain to run to their
        // rating, a consistency-0 one can be anywhere in a band four times as wide.
        public static float StrengthDeviation(int consistency, int statMax)
            => Mathf.Lerp(MaxStrengthDeviation, MinStrengthDeviation, Norm(consistency, statMax));

        // A uniform roll inside mean ± deviation, as NR2003 picked between the band's min and max.
        // roll01 is a uniform 0..1 draw.
        public static float Roll(float mean, float deviation, float roll01)
            => Mathf.Clamp01(mean + (Mathf.Clamp01(roll01) * 2f - 1f) * deviation);

        // The ratings a driver carries for one event. strengthRoll01 / aggressionRoll01 are independent uniform
        // 0..1 draws, made once per event by the caller.
        public static RaceRatings ForEvent(int qualifying, int trackAptitude, int consistency, int aggression,
                                           int statMax, float strengthRoll01, float aggressionRoll01)
        {
            return new RaceRatings
            {
                strength01 = Roll(StrengthMean(qualifying, trackAptitude, statMax),
                                  StrengthDeviation(consistency, statMax), strengthRoll01),
                aggression01 = Roll(Norm(aggression, statMax), AggressionDeviation, aggressionRoll01),
                consistency01 = Norm(consistency, statMax),
            };
        }

        static float Norm(int stat, int statMax) => statMax > 0 ? Mathf.Clamp01(stat / (float)statMax) : 0f;
    }
}
