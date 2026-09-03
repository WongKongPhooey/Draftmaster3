namespace Draftmaster.Weekend
{
    // What one completed activity did to the player's weekend. Every activity - the press conference, the
    // signing queue, the sponsor mini-game, the strategy briefing - scores itself into one of these and hands
    // it to the ledger, which is the only thing that touches the persistent meters. That keeps "how well did
    // I do at the mini-game" separate from "what does doing well mean", so the mini-games stay dumb and the
    // economy stays in one file.
    public struct WeekendOutcome
    {
        // Cash in (appearance fee, bonus) or out (a fine). Banked by the ledger via its money hook.
        public int money;

        // Fan appeal, straight onto Draftmaster.Fans.FanAppeal - the same 0-100 meter that decides how many
        // autograph seekers turn up in the pit lane and what the results screen shows.
        public float fanAppeal;

        // How happy the people paying for the hood are, -100..100. Multiplies the sponsor payout at the race.
        public float sponsorMood;

        // How the crew feel about the driver, -100..100. Worth pit-stop speed and a little pace.
        public float teamMorale;

        // How the press are writing about you, -100..100. Sets the tone of later questions and bleeds into
        // fan appeal over a season.
        public float mediaStanding;

        // Setup knowledge banked from practice, briefings and debriefs. 0..1, spent as pace at the race.
        public float setupGain;

        // A driver this activity moved you toward or away from - a jab in a press conference, a shared laugh
        // at a signing. Applied to DriverRelationships by the runtime layer.
        public string rivalName;
        public float rivalDelta;

        // A career counter to bump (PlayerStatsLedger key), e.g. "autographs", "sponsordays".
        public string statKey;
        public int statCount;

        // One line for the weekend wrap-up: "Told the press the car was junk. They printed it."
        public string headline;

        // Grade shown on the activity's result card. 0..1.
        public float score;

        // How much of the obligation's window the player spent, for the ones that are a window with a queue
        // in it rather than a fixed set of questions. Not a meter — it is what closes the signing fence.
        public float minutesSpent;

        public static WeekendOutcome Nothing => new WeekendOutcome { score = 0f };

        public WeekendOutcome WithHeadline(string line) { headline = line; return this; }
    }
}
