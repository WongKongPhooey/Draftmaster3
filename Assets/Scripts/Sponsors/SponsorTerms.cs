using UnityEngine;

namespace Draftmaster.Sponsors
{
    // The negotiation maths, kept pure so it can be unit-tested and so the NPC that speaks the lines owns
    // no rules of its own.
    //
    // Money is sized against the LIVE wallet economy (PlayerWallet: $5,000 start, a race win pays $12,000,
    // parts cost hundreds to low thousands), not the six-figure numbers in the SQLite Sponsors table —
    // those belong to the unbuilt team-budget economy. A big-name hood deal lands around $5-6k a race,
    // a small one around $2k, so a full car roughly doubles race-day income without dwarfing it.
    public static class SponsorTerms
    {
        // Standing (0-100) the player is judged on. FanAppeal is the live equivalent of "prestige" — it is
        // the only 0-100 reputation the running game maintains, it moves on results and autographs, and it
        // already gates how many fans turn up. Sponsor.MinPrestige is compared against it directly.
        public const float StandingMin = 0f;
        public const float StandingMax = 100f;

        // Per-race money a sponsor of this wealth pays for a hood, before haggling. Quadratic so the gap
        // between a corner-shop sponsor and a national brand is felt.
        public static int BaseValue(int wealth)
        {
            float w = Mathf.Clamp(wealth, 0, 100);
            return Mathf.RoundToInt(300f + w * w * 0.6f);
        }

        // How far past a sponsor's floor the player stands, 0..1. Barely qualified opens low; a star gets
        // their best number straight away.
        public static float Surplus(float standing, int minPrestige)
        {
            return Mathf.Clamp01((standing - minPrestige) / 40f);
        }

        public static bool CanApproach(float standing, int minPrestige) => standing >= minPrestige;

        // The number they lead with.
        public static int OpeningValue(int wealth, float standing, int minPrestige)
        {
            float surplus = Surplus(standing, minPrestige);
            return Mathf.RoundToInt(BaseValue(wealth) * Mathf.Lerp(0.75f, 1.10f, surplus));
        }

        // The most they will ever pay. Push past this and they walk.
        public static int CeilingValue(int wealth, float standing, int minPrestige)
        {
            float surplus = Surplus(standing, minPrestige);
            return Mathf.RoundToInt(BaseValue(wealth) * Mathf.Lerp(1.15f, 1.50f, surplus));
        }

        // Deal length in races. Wealthier brands commit for longer; nobody signs for less than four.
        public static int OpeningRaces(int wealth) => Mathf.Clamp(6 + Mathf.RoundToInt(wealth / 12f), 4, 16);

        // Performance clause. Prestigious sponsors expect results and pay a bonus for them; the small
        // fry just want the exposure and set no clause at all.
        public static int ClausePosition(int prestige)
        {
            if (prestige >= 85) return 5;
            if (prestige >= 70) return 10;
            if (prestige >= 55) return 15;
            return 0;
        }

        public static int ClauseBonus(int perRace, int clausePosition) =>
            clausePosition <= 0 ? 0 : Mathf.RoundToInt(perRace * 0.6f);

        // ---------------------------------------------------------------- haggling

        // What the player can say back. Deliberately a short list of canned moves rather than a number
        // entry: the conversation runs through DialogueChoiceUI like every other on-foot exchange.
        public enum Move
        {
            Accept,
            PushGentle,   // ask for ~15% more
            PushHard,     // ask for ~35% more
            Shorten,      // fewer races for a bit more per race — good when you expect to outgrow them
            Walk,
        }

        public struct Offer
        {
            public int perRace;
            public int races;
            public int clausePosition;
            public int clauseBonus;
        }

        public struct Response
        {
            public bool signed;      // they accepted the terms in `offer`
            public bool walked;      // negotiation over, nothing signed
            public Offer offer;      // the terms now on the table (their counter, or what was agreed)
            public string reply;     // what the rep says
        }

        public static Offer Open(int wealth, int prestige, float standing, int minPrestige)
        {
            int value = OpeningValue(wealth, standing, minPrestige);
            int clause = ClausePosition(prestige);
            return new Offer
            {
                perRace = value,
                races = OpeningRaces(wealth),
                clausePosition = clause,
                clauseBonus = ClauseBonus(value, clause),
            };
        }

        // Resolve one round of haggling. `round` counts pushes already made (0 on the first push).
        // The rule of thumb: they concede once, hold firm at their ceiling, and walk if pushed past it twice.
        public static Response Respond(Move move, Offer current, int ceiling, int round)
        {
            switch (move)
            {
                case Move.Accept:
                    return new Response { signed = true, offer = current, reply = "Deal. We'll get the decals cut tonight." };

                case Move.Walk:
                    return new Response { walked = true, offer = current, reply = "Your call. We'll be at the next one if you change your mind." };

                case Move.Shorten:
                {
                    // Shorter term, better rate: they'll trade length for money once, and only down to 4 races.
                    var shorter = current;
                    shorter.races = Mathf.Max(4, current.races - 3);
                    if (shorter.races == current.races)
                        return new Response { offer = current, reply = "Any shorter and it isn't worth printing the decals." };

                    shorter.perRace = Mathf.Min(ceiling, Mathf.RoundToInt(current.perRace * 1.12f));
                    shorter.clauseBonus = ClauseBonus(shorter.perRace, shorter.clausePosition);
                    return new Response
                    {
                        offer = shorter,
                        reply = $"Shorter deal, then — {shorter.races} races at ${shorter.perRace:N0} a race. That works for us.",
                    };
                }

                case Move.PushGentle:
                case Move.PushHard:
                {
                    float factor = move == Move.PushHard ? 1.35f : 1.15f;
                    int asked = Mathf.RoundToInt(current.perRace * factor);

                    if (asked <= ceiling)
                    {
                        // Inside what they can justify. They concede fully on a second ask, and split the
                        // difference on the first — so pushing twice is how you actually get paid.
                        int agreed = round >= 1 ? asked : Mathf.RoundToInt((current.perRace + asked) * 0.5f);
                        var next = current;
                        next.perRace = agreed;
                        next.clauseBonus = ClauseBonus(agreed, next.clausePosition);
                        return new Response
                        {
                            offer = next,
                            reply = agreed >= asked
                                ? $"You drive a hard bargain. ${agreed:N0} a race, and that's the budget gone."
                                : $"I can meet you part way — ${agreed:N0} a race.",
                        };
                    }

                    if (asked <= Mathf.RoundToInt(ceiling * 1.25f) && round < 1)
                    {
                        var next = current;
                        next.perRace = ceiling;
                        next.clauseBonus = ClauseBonus(ceiling, next.clausePosition);
                        return new Response
                        {
                            offer = next,
                            reply = $"That's over my head. ${ceiling:N0} a race is the ceiling — take it or leave it.",
                        };
                    }

                    return new Response
                    {
                        walked = true,
                        offer = current,
                        reply = "You're not serious. Come find me when you've a car worth that.",
                    };
                }
            }

            return new Response { offer = current, reply = "" };
        }
    }
}
