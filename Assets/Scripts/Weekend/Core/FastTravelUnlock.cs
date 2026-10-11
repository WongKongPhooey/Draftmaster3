using System;
using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // Fast travel is earned, not handed over on the first morning.
    //
    // A new career's first job is the walk across the paddock to the crew chief's strategy briefing — the
    // phone goes off on it (ChiefCheckIn), running is taught on it, and the e-scooter is offered on it. A T
    // that skips the whole walk would skip the lessons with it, and a FAST TRAVEL button nobody has been told
    // about is just a mystery on the objective strip. So it stays locked until the first team meeting is
    // over, and the moment it unlocks a prompt says how: T on a keyboard, the pad's button, the FAST TRAVEL
    // button on the objective on a phone.
    //
    // This is the rule; FastTravelTutorial (Assembly-CSharp) keeps the memory and shows the prompt. Once per
    // save, remembered through AppearanceConditions under SaveKey, so CareerReset and
    // Draftmaster > NPCs > Clear Appearance Flags both lock it again.
    public static class FastTravelUnlock
    {
        public const string SaveKey = "career.fasttravel.unlocked";

        // The ControlHints id of the prompt, once per save like every other taught control.
        public const string HintId = "fasttravel";

        // The bookings that unlock it: the team meetings (TeamMeetingContent). The first of them in a career
        // is always the Friday strategy briefing; the debrief is here as well so a player who slept through
        // the briefing is not left walking until next weekend.
        public static bool Unlocks(ActivityKind kind) =>
            kind == ActivityKind.TeamBriefing || kind == ActivityKind.Debrief;

        // Is fast travel locked?
        //
        //   unlocked   the save has finished (or missed) a team meeting
        //   mode       a career mode with no team meetings on the sheet has nothing to unlock it, so it is
        //              never locked there
        //   coopGuest  the guest is carried along on the host's career and has no meetings of their own
        public static bool Locked(bool unlocked, CareerMode mode, bool coopGuest)
            => !unlocked && !coopGuest && CareerModes.Keeps(mode, ActivityKind.TeamBriefing);

        // Has a team meeting on this weekend's sheet been settled — attended, or gone by without the player?
        // A missed one unlocks as well: the lock is there to make the first walk, not to strand a player who
        // overslept. It also catches a save that was already past its briefing before the lock existed.
        public static bool MeetingSettled(IEnumerable<WeekendActivity> sheet, Func<string, bool> done,
                                          Func<string, bool> missed)
        {
            if (sheet == null) return false;
            foreach (var a in sheet)
                if (a != null && Unlocks(a.kind) && ((done != null && done(a.id)) || (missed != null && missed(a.id))))
                    return true;
            return false;
        }

        // Should the "you can fast travel now" prompt go up?
        //
        //   locked     fast travel is still locked
        //   taught     the save has already had the prompt
        //   canTravel  the objective strip is offering a fast travel right now — the prompt waits for
        //              somewhere to go, so it is never about a button that is not on screen
        //   busy       a conversation, a card, a wipe or a menu has the player
        public static bool ShouldPrompt(bool locked, bool taught, bool canTravel, bool busy)
            => !locked && !taught && canTravel && !busy;
    }
}
