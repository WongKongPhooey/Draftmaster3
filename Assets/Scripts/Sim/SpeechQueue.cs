using System.Collections.Generic;

namespace Draftmaster.Sim
{
    // Who gets to talk, and what happens to everyone else.
    //
    // The paddock has a lot of mouths in it — a hundred-odd wandering NPCs with ambient one-liners, the
    // driver you are actually in conversation with, a cutscene walking somebody over to you, a fan at the
    // fence, a rival squaring up. Left to themselves they all speak at once and the screen fills with
    // boxes, none of which is the one you were reading.
    //
    // One rule settles it: only one bubble is up at a time, and what happens to a new line depends on what
    // is already being said. This is the rule on its own — no scene, no bubbles — so it can be reasoned
    // about and tested directly.
    public enum SpeechPriority
    {
        // Background flavour. Nobody is waiting on it, so it is dropped the moment anything else wants the
        // screen — a passer-by does not get to interrupt, and does not queue up to interrupt later either.
        Ambient = 0,

        // A conversation the player is actually having. Their own next line always follows the last one.
        Conversation = 1,

        // A scripted beat: a walk-up, a fight, a ceremony. Outranks a conversation because it is the thing
        // that took control of the player to happen.
        Cutscene = 2,
    }

    public enum SpeechVerdict
    {
        Speak,    // nothing in the way, or the incumbent is lower ranked and gets cut off
        Queue,    // same rank, another speaker: wait your turn rather than talking over them
        Drop,     // outranked; this line is not worth saying late
    }

    public static class SpeechQueue
    {
        // What to do with `incoming` when `current` is already being said.
        //
        // `sameSpeaker` is the common case of a conversation advancing: the actor replacing their own line
        // never waits for themselves.
        public static SpeechVerdict Judge(SpeechPriority? current, SpeechPriority incoming, bool sameSpeaker)
        {
            if (current == null || sameSpeaker) return SpeechVerdict.Speak;

            if (incoming > current.Value) return SpeechVerdict.Speak;    // outranks it: cut in
            if (incoming < current.Value) return SpeechVerdict.Drop;     // outranked: not worth saying late

            // Same rank, different speaker. Background chatter is never worth holding onto; anything the
            // player is part of waits and is said when the screen is free.
            return incoming == SpeechPriority.Ambient ? SpeechVerdict.Drop : SpeechVerdict.Queue;
        }

        // A queue that never grows without bound. Dialogue that has been waiting a long time is dialogue
        // about a moment that has passed, so the oldest is dropped rather than the newest refused — the
        // last thing said to you is the one that still makes sense.
        public const int MaxWaiting = 3;

        public static void Enqueue<T>(List<T> waiting, T item)
        {
            if (waiting == null) return;
            waiting.Add(item);
            while (waiting.Count > MaxWaiting) waiting.RemoveAt(0);
        }
    }
}
