using Draftmaster.Chatter;
using Draftmaster.Crowd;

// What the circuit is doing right now, in the two vocabularies the paddock cares about: the plain int
// CrowdPolicy sizes a crowd's noise from, and the ChatterTopic its one-liners are drawn from.
//
// This mapping used to live privately inside AmbienceLoop. It is shared now because the same question —
// is a session running, and what kind — decides both how loud the place SOUNDS and what the people in it
// are TALKING ABOUT, and a paddock whose murmur says Friday practice while its crowd talks about the grid
// forming up is worse than one that gets both wrong the same way.
//
// Everything here is a register read, so it is cheap enough to ask per bark and never needs caching.
public static class SessionMood
{
    // The player's own race outranks the weekend sheet. An exhibition race off the title screen, a single
    // race and a multiplayer lobby carry no weekend ledger at all, and would otherwise read as Friday
    // morning — a practice-day crowd murmuring through a race.
    static bool PlayerIsRacing => AppearanceConditions.CurrentSession == RaceWeekend.Session.Race;

    // CrowdPolicy.TrackIdle / TrackPractice / TrackQualifying / TrackRace.
    public static int TrackActivity()
    {
        if (PlayerIsRacing) return CrowdPolicy.TrackRace;

        var live = WeekendTrackState.Now();
        if (!live.any) return CrowdPolicy.TrackIdle;

        return live.kind switch
        {
            Draftmaster.Weekend.ActivityKind.Race => CrowdPolicy.TrackRace,
            Draftmaster.Weekend.ActivityKind.Qualifying => CrowdPolicy.TrackQualifying,
            _ => CrowdPolicy.TrackPractice,
        };
    }

    // Which half-day the crowd is sized and pitched for, 0 = Friday AM .. 5 = Sunday PM. A weekend race
    // that is not the player's — the truck race on Friday night, the National race on Saturday — keeps its
    // own half-day, because those houses genuinely are smaller than Sunday's.
    public static int HalfDaySlot()
        => PlayerIsRacing ? 5 : (int)Draftmaster.Weekend.WeekendLedger.CurrentSlot;

    // What the background crowd should be talking about.
    public static ChatterTopic Topic() => TrackActivity() switch
    {
        CrowdPolicy.TrackRace => ChatterTopic.Race,
        CrowdPolicy.TrackQualifying => ChatterTopic.Qualifying,
        CrowdPolicy.TrackPractice => ChatterTopic.Practice,
        _ => ChatterTopic.Idle,
    };
}
