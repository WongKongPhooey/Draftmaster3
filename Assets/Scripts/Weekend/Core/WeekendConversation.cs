using System;
using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // What a weekend obligation is, now that they are things that happen to you in the paddock rather than
    // panels: somebody in front of you says a line, you pick what to say back, they answer it, and each of
    // those exchanges moves the weekend's meters.
    //
    // The content lives here, in the pure assembly, and knows nothing about how it is shown. The runtime
    // layer plays a conversation through the same speech bubbles and choice list the rest of the paddock
    // uses (WeekendVenueHost), so a press conference reads like talking to a person rather than reading a
    // form. It is also why the content is testable: a conversation is data, and what any given
    // answer is worth is arithmetic on that data.
    public class WeekendChoice
    {
        public string text;        // what the player says
        public string response;    // what they say back to it

        // What picking this is worth. Same units as WeekendOutcome.
        public float setup;        // 0..1 of car knowledge
        public float morale;       // -100..100
        public float media;        // -100..100
        public float sponsor;      // -100..100
        public float appeal;       // fan appeal points
        public int money;          // straight cash, before the appearance fee

        // A driver this answer moved you toward or away from.
        public string rivalName;
        public float rivalDelta;

        // How well this counted as an answer, 0..1. Averaged across the beats into the outcome's grade.
        public float score = 0.5f;

        // How long this answer takes out of the obligation's window. Only means anything to a conversation
        // that is on a clock (see minuteBudget): a queue at a fence is as long as the hour lets it be, so
        // stopping to talk to one person is time the next one does not get. Left at zero by everything that
        // is a fixed set of questions rather than a queue.
        public float minutes;

        // What this answer adds to the obligation's career counter — one signature, one photo. Left at zero
        // by anything that is counted once for the whole obligation rather than per answer.
        public int statCount;

        // Set when picking this ends the conversation early — walking off, or a fan you waved away.
        public bool ends;
    }

    // One exchange: who is speaking, what they said, and what can be said back. Lines before the question
    // (a crew chief walking you to the car, an official reading a note) go in `preamble`, which is spoken
    // first and needs no answer.
    public class WeekendBeat
    {
        public string speaker = "";
        public string[] preamble;
        public string line = "";
        public List<WeekendChoice> choices = new();

        // Shown as the choice-list header. Falls back to the spoken line.
        public string question;

        public string Question => string.IsNullOrEmpty(question) ? line : question;
    }

    // A whole obligation, start to finish.
    public class WeekendConversation
    {
        public List<WeekendBeat> beats = new();

        // Career counter this obligation bumps once it has been seen through (PlayerStatsLedger key).
        public string statKey;
        public int statCount = 1;

        // The wrap-up line, decided from what the answers added up to. Set by the content so a meeting that
        // went badly can say so in its own words.
        public Func<WeekendOutcome, string> headline;

        // Spoken by the host as the player walks up, before the first beat.
        public string[] greeting;
        // Spoken as the player leaves, after the last answer has landed.
        public string[] farewell;
        // Spoken instead of the farewell when the window ran out with beats still queued up. Falls back to
        // the farewell when the content has not written one.
        public string[] timeUpFarewell;

        // ------------------------------------------------------------------ the clock
        //
        // Some obligations are a fixed set of questions and end when the last one is answered. Others are a
        // window with a queue in it, and the decision is what to spend the window on: the signing fence is
        // as long as the hour allows, so a driver who takes a minute each meets fewer people than one who
        // signs and moves. Set minuteBudget to the length of the window and give each answer its `minutes`,
        // and the conversation closes itself when there is no time left to bring the next person forward.

        public float minuteBudget;   // 0 = untimed: the conversation runs to its last beat
        public float minuteStep;     // the cheapest answer, i.e. the least time the next person could take

        // Applied to the settled outcome before the headline is written, given how many beats were actually
        // answered. This is where a timed obligation prices the whole hour rather than each answer — the
        // queue that never reached the front, the queue that did.
        public Func<WeekendOutcome, int, WeekendOutcome> epilogue;

        // Is there room in the window for one more person at the front?
        public bool OutOfTime(float minutesSpent) =>
            minuteBudget > 0f && minutesSpent + minuteStep > minuteBudget;

        // Was answering `choice` at beat `index` the last thing this conversation had in it — because the
        // answer ended it, because the beats ran out, or because the window did. Kept here so the venue host
        // and the tests agree on when the queue closes.
        public bool Ends(int index, WeekendChoice choice, float minutesSpent) =>
            (choice != null && choice.ends) || index >= beats.Count - 1 || OutOfTime(minutesSpent);

        public WeekendBeat Add(WeekendBeat beat)
        {
            beats.Add(beat);
            return beat;
        }

        // Fold one answer into a running outcome. Kept here rather than in the player so the same
        // arithmetic covers every obligation and can be tested without a scene.
        public static void Accumulate(ref WeekendOutcome outcome, WeekendChoice choice)
        {
            if (choice == null) return;

            outcome.setupGain += choice.setup;
            outcome.teamMorale += choice.morale;
            outcome.mediaStanding += choice.media;
            outcome.sponsorMood += choice.sponsor;
            outcome.fanAppeal += choice.appeal;
            outcome.money += choice.money;
            outcome.score += choice.score;
            outcome.statCount += choice.statCount;
            outcome.minutesSpent += choice.minutes;

            // Last answer to name a driver wins: an obligation that keeps mentioning one person is about
            // that person, and two half-strength deltas at different drivers would read as neither.
            if (!string.IsNullOrEmpty(choice.rivalName) && choice.rivalDelta != 0f)
            {
                outcome.rivalName = choice.rivalName;
                outcome.rivalDelta += choice.rivalDelta;
            }
        }

        // Turn the running total into the finished thing: average the grade over however many answers were
        // actually given, price the obligation as a whole, stamp the career counter, and let the content
        // write the headline.
        public WeekendOutcome Settle(WeekendOutcome running, int answered)
        {
            var o = running;
            o.score = Clamp01(o.score / Math.Max(1, answered));
            // After the grade, so the epilogue can read how the hour was worked as well as how much of it
            // got done, and before the headline, so the headline describes what was actually banked.
            if (epilogue != null) o = epilogue(o, answered);
            if (!string.IsNullOrEmpty(statKey))
            {
                o.statKey = statKey;
                // Answers that count themselves (a signature each) have already added up; anything else is
                // counted once for the whole obligation.
                if (o.statCount <= 0) o.statCount = statCount;
            }
            if (headline != null) o.headline = headline(o);
            return o;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ------------------------------------------------------------------ content helpers

        // Terse builder so the content files read as script rather than as object initialisers.
        public static WeekendChoice Say(string text, string response, float setup = 0f, float morale = 0f,
                                        float media = 0f, float sponsor = 0f, float appeal = 0f,
                                        int money = 0, float score = 0.5f, int statCount = 0,
                                        string rivalName = null, float rivalDelta = 0f, bool ends = false,
                                        float minutes = 0f)
            => new WeekendChoice
            {
                text = text, response = response,
                setup = setup, morale = morale, media = media, sponsor = sponsor, appeal = appeal,
                money = money, score = score, statCount = statCount,
                rivalName = rivalName, rivalDelta = rivalDelta, ends = ends, minutes = minutes,
            };
    }
}
