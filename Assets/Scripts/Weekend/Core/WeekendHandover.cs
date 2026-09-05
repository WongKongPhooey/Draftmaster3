namespace Draftmaster.Weekend
{
    // Handing the circuit from one championship to the next, without the player watching it happen.
    //
    // A weekend gives pit road away several times inside one scene load — the Trucks come in, the paddock
    // goes quiet, the Cup cars go out — and every handover is a lot of things moving at once: a field
    // cleared off the lap, another one stood up around it, the box ladder refitted, and a crew (and a pit
    // stand, in that car's colours) rebuilt on every box. None of that is meant to be a scene. Watched from
    // the paddock it reads as the world glitching rather than as a session starting.
    //
    // So it goes behind a wipe: fade down, hand the circuit over while nobody can see it, fade back up with
    // everything already stood where it belongs — and only then does the objective for the session go up.
    //
    // The rules are here, pure, because the runtime half (WeekendTrackChangeover) is all coroutines and
    // scene lookups and cannot be tested. This is the part with decisions in it.
    public static class WeekendHandover
    {
        // How long to fade down, sit black and come back up. Short: this is punctuation, not a cutscene.
        public const float FadeOutSeconds = 0.28f;
        public const float HoldSeconds = 0.06f;
        public const float FadeInSeconds = 0.34f;

        // How long to wait for the player to be free — a result card up, a panel open, mid-conversation —
        // before giving up on the wipe and letting the handover through in the open. A blackout that lands
        // over the card telling you what you just earned is worse than the glitch it was hiding.
        public const float WaitLimitSeconds = 15f;

        // How long to sit black waiting for the field and its crews to finish standing up. Generous enough
        // for a first-launch database open, short enough that a spawner destroyed mid-handover cannot leave
        // the screen black.
        public const float StageLimitSeconds = 6f;

        // Is this handover one anybody is going to see?
        //
        // Only the walk-around half of the weekend: the player on foot in the paddock, with the circuit
        // changing hands beside them. Driving their own session, sat in a grandstand (which owns the camera
        // and runs its own wipes), or in a multiplayer lobby, there is either nothing to hide or somebody
        // else already hiding it — and a second fade over the top of one already running is a flicker.
        public static bool ShouldWipe(bool onFootInThePaddock, bool multiplayer,
                                      bool watchingFromAStand, bool screenAlreadyWiping)
        {
            if (multiplayer) return false;
            if (!onFootInThePaddock) return false;
            if (watchingFromAStand) return false;
            if (screenAlreadyWiping) return false;
            return true;
        }

        // Is the player free to have the lights go out on them? Anything with their attention in it — the
        // card settling the obligation they just finished, an open panel, the person they are still talking
        // to — holds the wipe rather than being blacked out mid-sentence.
        public static bool ReadyToWipe(bool cardUp, bool panelUp, bool conversationUp)
            => !cardUp && !panelUp && !conversationUp;

        // Waited long enough for that. Past this the handover happens in the open, which is exactly what it
        // did before any of this existed.
        public static bool GaveUpWaiting(float waitedSeconds) => waitedSeconds >= WaitLimitSeconds;

        // Sat in the dark long enough. The field is either up or it is never coming; either way the screen
        // comes back.
        public static bool StagedLongEnough(float blackSeconds) => blackSeconds >= StageLimitSeconds;
    }
}
