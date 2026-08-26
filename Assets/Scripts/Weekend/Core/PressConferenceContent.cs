using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // What a reporter asks and what saying one thing rather than another costs you.
    //
    // The press conference is the one activity with no skill in it - there is no timing bar to hit. What it
    // has instead is that every answer is good for something and bad for something else. Backing your crew
    // in public buys morale and sells nothing; taking a swing at the driver who wrecked you sells papers and
    // costs you the sponsor's afternoon. There is no answer that wins every meter, which is what makes it a
    // conference rather than a quiz.
    public enum PressTone
    {
        TeamFirst = 0,   // credit the crew, say nothing quotable
        Confident = 1,   // back yourself; the fans and the press both like it
        Fiery = 2,       // name a name; huge with fans and press, poison with sponsors
        Corporate = 3,   // hit the talking points; the brand loves it, nobody else does
        Candid = 4,      // tell the truth about a bad car; the press respect it, the shop does not
    }

    public class PressAnswer
    {
        public string text;
        public PressTone tone;
        // Overrides the tone's default reaction line when this answer needs its own.
        public string reaction;
        // Set when the answer is aimed at a specific driver, so the relationship hit lands on them.
        public bool aimedAtRival;

        public PressAnswer(string text, PressTone tone, string reaction = null, bool aimedAtRival = false)
        {
            this.text = text; this.tone = tone; this.reaction = reaction; this.aimedAtRival = aimedAtRival;
        }
    }

    public class PressQuestion
    {
        public string reporter;
        public string outlet;
        public string text;
        public List<PressAnswer> answers = new();
    }

    // Everything the question bank needs to know about where the player is standing when they get asked.
    public struct PressContext
    {
        public RacingSeries series;
        public string trackName;
        public string rivalName;      // "" when nobody is feuding with you
        public string sponsorName;    // "" when the car is unpainted
        public int weekendId;
        public bool raceDay;          // Sunday-morning tone rather than Friday tone
        public float mediaStanding;   // -100..100, so a hostile room asks harder questions
        public int lastFinish;        // last race's finishing position, 0 = no history
        public bool qualifiedWell;    // inside the top ten on the current grid
        public bool ranPractice;      // whether the player actually did their own practice session
    }

    public static class PressConferenceContent
    {
        static readonly (string name, string outlet)[] Reporters =
        {
            ("Dana Whitlow", "Circuit Weekly"),
            ("Terry Applebaum", "The Draft"),
            ("Marisol Vega", "Speedway Now"),
            ("Ed Kinsella", "Pit Lane Radio"),
            ("Priya Raman", "National Motorsport"),
            ("Buck Hollifield", "Trackside TV"),
            ("Naomi Bright", "The Infield Post"),
        };

        // Three or four questions is what an availability actually is - long enough to have a bad one in it,
        // short enough that nobody is answering the same question twice.
        public static List<PressQuestion> Build(PressContext ctx, string activityId, int count = 3)
        {
            var rng = WeekendRandom.For(ctx.weekendId, (int)ctx.series, activityId != null ? activityId.GetHashCode() : 0);
            var pool = Pool(ctx);
            var chosen = new List<PressQuestion>();

            // Shuffle the pool rather than picking with replacement, so the same room never asks the same
            // thing twice in one availability.
            var order = new List<int>(pool.Count);
            for (int i = 0; i < pool.Count; i++) order.Add(i);
            rng.Shuffle(order);

            int want = Mathf.Clamp(count, 1, pool.Count);
            for (int i = 0; i < want; i++)
            {
                var q = pool[order[i]];
                var r = Reporters[rng.Range(0, Reporters.Length)];
                q.reporter = r.name;
                q.outlet = r.outlet;
                chosen.Add(q);
            }
            return chosen;
        }

        // Questions that fit the situation. Situational ones go in first so the shuffle is picking between
        // relevant questions rather than filler.
        static List<PressQuestion> Pool(PressContext ctx)
        {
            var list = new List<PressQuestion>();
            string track = string.IsNullOrEmpty(ctx.trackName) ? "this place" : ctx.trackName;
            string rival = string.IsNullOrEmpty(ctx.rivalName) ? "" : ctx.rivalName;
            string sponsor = string.IsNullOrEmpty(ctx.sponsorName) ? "the sponsor" : ctx.sponsorName;
            string nick = SeriesCatalog.Nickname(ctx.series);

            // ---- situational -------------------------------------------------------------------------
            if (!string.IsNullOrEmpty(rival))
            {
                list.Add(Q($"You and {rival} have had a few run-ins now. Is that a problem you need to fix?",
                    A($"We'll sort it between us. I'm not going to race it out in the paper.", PressTone.TeamFirst),
                    A($"He knows where I am. If he wants it, he can have it.", PressTone.Fiery, null, true),
                    A($"I've got a car to drive. That's the whole answer.", PressTone.Corporate)));

                list.Add(Q($"{rival} said this week that you race people harder than you get raced. Fair?",
                    A("He's entitled to think that. I'd rather be hard to pass than easy.", PressTone.Confident),
                    A("If he's talking about me in December he's not thinking about his own car.", PressTone.Fiery, null, true),
                    A("I don't read it, honestly. Ask him.", PressTone.Corporate)));
            }

            if (ctx.lastFinish > 0 && ctx.lastFinish <= 5)
            {
                list.Add(Q($"You came out of the last one P{ctx.lastFinish}. Does that change what you expect here?",
                    A("The crew found something real. I just have to not waste it.", PressTone.TeamFirst),
                    A("It changes what I'll accept. I'm not here for another top five.", PressTone.Confident),
                    A($"{sponsor} have backed us all year and results like that are what they signed up for.", PressTone.Corporate)));
            }
            else if (ctx.lastFinish >= 20)
            {
                list.Add(Q($"P{ctx.lastFinish} last time out. What went wrong?",
                    A("That one's on me. The car was better than where I finished it.", PressTone.TeamFirst),
                    A("Nothing that keeps me up. We were quick and we got caught out.", PressTone.Confident),
                    A("We unloaded off and never fixed it. That's the honest version.", PressTone.Candid)));
            }

            if (!ctx.ranPractice)
            {
                list.Add(Q("You weren't in the car for practice. How much does that hurt you today?",
                    A("The engineers gave me a sheet I trust. We'll be fine.", PressTone.TeamFirst),
                    A("I've run enough laps here in my life. I don't need the practice.", PressTone.Confident),
                    A("It hurts. I'd rather have been in it. That's a straight answer.", PressTone.Candid)));
            }

            if (ctx.qualifiedWell)
            {
                list.Add(Q("Good lap in qualifying. Is the car that good or was that you?",
                    A("That's the shop. I turned the wheel, they built the speed.", PressTone.TeamFirst),
                    A("Bit of both, and I'll take the bit that was me.", PressTone.Confident),
                    A("One lap is one lap. Ask me after we run five hundred of them.", PressTone.Candid)));
            }

            if (ctx.raceDay)
            {
                list.Add(Q("Race morning. What's the plan?",
                    A("Do what the crew chief says and be there at the end.", PressTone.TeamFirst),
                    A("Lead laps. I didn't drive here to ride around.", PressTone.Confident),
                    A($"Give {sponsor} a good day. That's what the whole thing is for.", PressTone.Corporate)));
            }

            // ---- always available --------------------------------------------------------------------
            list.Add(Q($"How do you rate your chances at {track} this weekend?",
                A("The crew have given me something to work with. Ask me Sunday night.", PressTone.TeamFirst),
                A("I like this place and this place likes me. We're going to be there.", PressTone.Confident),
                A($"{sponsor} put a lot into this weekend. We intend to pay it back.", PressTone.Corporate)));

            list.Add(Q($"There's talk that the {nick} field is getting harder to pass in. Do you feel that?",
                A("Everybody's got the same problem. We'll figure it out quicker than most.", PressTone.Confident),
                A("It's brutal. If you don't qualify you don't race, and that's the truth of it.", PressTone.Candid),
                A("I'd rather talk about our car than the rulebook.", PressTone.Corporate)));

            list.Add(Q("What's the hardest part of a weekend like this one, honestly?",
                A("Everything that isn't the driving. The driving's the easy bit.", PressTone.Candid),
                A("Nothing's hard when the car's good. Make the car good.", PressTone.Confident),
                A("Keeping thirty people all pulling the same way. That's the crew chief's job and he's good at it.", PressTone.TeamFirst)));

            list.Add(Q("A lot of young drivers say the sponsor days wear them out. Do they wear you out?",
                A("They pay for the tyres. I'll shake every hand they want me to shake.", PressTone.Corporate),
                A("Some weeks. But there's a thousand people who'd take my week.", PressTone.TeamFirst),
                A("They do, and I think we all pretend they don't.", PressTone.Candid)));

            list.Add(Q($"What would a good weekend look like for you here?",
                A("Car in one piece, everybody happy, points in the bag.", PressTone.TeamFirst),
                A("Trophy. I'm not going to pretend otherwise.", PressTone.Confident),
                A("Anything that gets us out of the hole we're in.", PressTone.Candid)));

            return list;
        }

        static PressQuestion Q(string text, params PressAnswer[] answers)
        {
            var q = new PressQuestion { text = text };
            q.answers.AddRange(answers);
            return q;
        }

        static PressAnswer A(string text, PressTone tone, string reaction = null, bool aimedAtRival = false)
            => new PressAnswer(text, tone, reaction, aimedAtRival);

        // ------------------------------------------------------------------ scoring

        // What one answer does. Nothing here is free: every tone pays one meter out of another's pocket.
        public static WeekendOutcome Score(PressAnswer answer, PressContext ctx)
        {
            if (answer == null) return WeekendOutcome.Nothing;
            var o = new WeekendOutcome { score = 0.5f };

            switch (answer.tone)
            {
                case PressTone.TeamFirst:
                    o.teamMorale = 9f;
                    o.sponsorMood = 3f;
                    o.mediaStanding = 2f;
                    o.fanAppeal = 0.4f;
                    o.score = 0.65f;
                    break;

                case PressTone.Confident:
                    o.mediaStanding = 7f;
                    o.fanAppeal = 1.6f;
                    o.teamMorale = -1f;
                    o.score = 0.7f;
                    break;

                case PressTone.Fiery:
                    o.mediaStanding = 12f;
                    o.fanAppeal = 3.2f;
                    o.sponsorMood = -14f;
                    o.teamMorale = -4f;
                    o.score = 0.8f;
                    if (answer.aimedAtRival && !string.IsNullOrEmpty(ctx.rivalName))
                    {
                        o.rivalName = ctx.rivalName;
                        o.rivalDelta = -12f;
                    }
                    break;

                case PressTone.Corporate:
                    o.sponsorMood = 12f;
                    o.mediaStanding = -6f;
                    o.fanAppeal = -0.6f;
                    o.score = 0.45f;
                    break;

                case PressTone.Candid:
                    o.mediaStanding = 9f;
                    o.fanAppeal = 1.2f;
                    o.teamMorale = -8f;
                    o.sponsorMood = -3f;
                    o.score = 0.6f;
                    break;
            }

            // A room that already likes you rewards a bold line more and punishes a dull one harder.
            float warmth = Mathf.Clamp(ctx.mediaStanding / 100f, -1f, 1f);
            o.mediaStanding *= 1f + warmth * 0.25f;
            return o;
        }

        // The line the room comes back with. Written per tone rather than per answer so a new question does
        // not need five more strings to be readable.
        public static string Reaction(PressAnswer answer)
        {
            if (answer == null) return "";
            if (!string.IsNullOrEmpty(answer.reaction)) return answer.reaction;

            return answer.tone switch
            {
                PressTone.TeamFirst => "Pens move. Nobody in the room writes a headline out of it, and the crew chief hears about it by lunchtime.",
                PressTone.Confident => "That gets a laugh and three people typing at once. It will be the clip.",
                PressTone.Fiery => "The room goes very quiet and then very loud. Somebody in a branded polo shirt closes their eyes.",
                PressTone.Corporate => "Two reporters stop writing. The sponsor's PR lead, standing at the back, does not.",
                PressTone.Candid => "An honest answer lands harder than a good one. They write it down word for word.",
            };
        }

        public static string ToneLabel(PressTone t) => t switch
        {
            PressTone.TeamFirst => "TEAM",
            PressTone.Confident => "BOLD",
            PressTone.Fiery => "FIRED UP",
            PressTone.Corporate => "ON MESSAGE",
            _ => "CANDID",
        };
    }
}
