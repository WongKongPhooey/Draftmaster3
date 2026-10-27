using UnityEngine;

namespace Draftmaster.Weekend
{
    // The sponsor's pit-stop challenge: one shot, against the clock. The car starts stopped at the pit-lane
    // entry with the limiter off, the driver runs the lane flat out, stops fully inside the box, the crew
    // fit four fresh tyres, and the clock stops the moment the car is all the way back out of the box.
    //
    // A bit of fun rather than an obligation - nobody is fined for skipping it - but a quick time is worth
    // fan support and a happy sponsor, because both of them were watching.
    //
    // Pure, so what a time is worth is testable without a car. PitChallengeRun (runtime) drives the run and
    // hands the finished time here.
    public static class PitChallenge
    {
        // How long the crew take to change four tyres, seconds. The clock keeps running through it.
        public const float ServiceSeconds = 4f;

        // The give-up: a run still going after this long has gone wrong somewhere (spun, parked, lost).
        public const float TimeLimitSeconds = 75f;

        // A clean run by a decent driver, used as the par the grade is measured from. Flat out to the box on
        // a car that accelerates at `accel` and brakes at `brake`, the stop, and the pull out of the box.
        public static float Par(float runInMetres, float exitMetres)
        {
            const float accel = 6f, brake = 8f, topSpeed = 50f;   // m/s^2, m/s^2, m/s
            float run = FlatOutAndStop(Mathf.Max(0f, runInMetres), accel, brake, topSpeed);
            float exit = Mathf.Sqrt(2f * Mathf.Max(0f, exitMetres) / accel);
            // A human reaction off the start and a moment to settle the car in the box.
            return run + ServiceSeconds + exit + 1.5f;
        }

        // Time to cover `d` metres from a standstill to a standstill: accelerate, maybe sit at top speed, brake.
        static float FlatOutAndStop(float d, float a, float b, float vMax)
        {
            if (d <= 0f) return 0f;
            float peak = Mathf.Sqrt(2f * d * a * b / (a + b));
            if (peak <= vMax) return peak / a + peak / b;
            float cruise = d - vMax * vMax / (2f * a) - vMax * vMax / (2f * b);
            return vMax / a + vMax / b + cruise / vMax;
        }

        public enum Grade { Missed, Slow, Decent, Quick, Brilliant }

        // How far under or over par the run came in.
        public static Grade GradeFor(float seconds, float par, bool completed)
        {
            if (!completed) return Grade.Missed;
            float over = seconds - par;
            if (over <= 0f) return Grade.Brilliant;
            if (over <= 2.5f) return Grade.Quick;
            if (over <= 6f) return Grade.Decent;
            return Grade.Slow;
        }

        public static string Label(Grade g) => g switch
        {
            Grade.Brilliant => "Brilliant",
            Grade.Quick => "Quick",
            Grade.Decent => "Decent",
            Grade.Slow => "Slow",
            _ => "No Time",
        };

        // What the run did for the weekend. A good time is the fans and the sponsor both cheering; a slow one
        // still got the brand's name on a stopwatch; a run that never stopped in the box is a laugh at your
        // expense and a rep who wishes you had.
        public static WeekendOutcome Outcome(float seconds, float par, bool completed, bool personalBest)
        {
            var g = GradeFor(seconds, par, completed);
            var o = new WeekendOutcome { statKey = "pitchallenges", statCount = completed ? 1 : 0 };
            switch (g)
            {
                case Grade.Brilliant:
                    o.fanAppeal = 3f; o.sponsorMood = 5f; o.money = 1500; o.score = 1f;
                    o.headline = $"{seconds:0.00}s in the pit-stop challenge. The sponsor's guests were on their feet.";
                    break;
                case Grade.Quick:
                    o.fanAppeal = 2f; o.sponsorMood = 3f; o.money = 750; o.score = 0.8f;
                    o.headline = $"{seconds:0.00}s down pit road and out of the box. Quick, and they knew it.";
                    break;
                case Grade.Decent:
                    o.fanAppeal = 1f; o.sponsorMood = 1.5f; o.score = 0.55f;
                    o.headline = $"{seconds:0.00}s in the pit-stop challenge. Tidy, nothing to post about.";
                    break;
                case Grade.Slow:
                    o.fanAppeal = 0.3f; o.sponsorMood = 0.5f; o.score = 0.3f;
                    o.headline = $"{seconds:0.00}s. The crew were quicker changing the tyres than you were getting there.";
                    break;
                default:
                    o.fanAppeal = 0f; o.sponsorMood = -0.5f; o.score = 0.1f;
                    o.headline = "Never stopped in the box. The stopwatch is still running somewhere.";
                    break;
            }
            if (completed && personalBest && g != Grade.Missed)
            {
                o.fanAppeal += 0.5f;
                o.headline += " A new best here.";
            }
            return o;
        }

        // The crew chief's intro, at the team's garage. One beat: say the word and the car is brought round.
        public static WeekendConversation Intro(WeekendActivity a)
        {
            var c = new WeekendConversation
            {
                statKey = "",
                statCount = 0,
                greeting = new[]
                {
                    "Sponsor's pit-stop challenge. One shot, on the clock.",
                    "We'll put you at the top of pit road. Limiter's off - flat out down the lane.",
                    "Stop dead inside the box, we bolt on four new tyres, and the watch stops when the car's clean out of the box.",
                },
                farewell = new[] { "Car's coming round now. Don't stall it." },
            };
            c.Add(new WeekendBeat
            {
                speaker = "Crew Chief",
                line = "Ready when you are.",
                choices = { WeekendConversation.Say("Strap me in.", "That's what I like to hear.", score: 1f) },
            });
            return c;
        }
    }
}
