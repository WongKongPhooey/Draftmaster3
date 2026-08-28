namespace Draftmaster.Weekend
{
    // What a booking on the weekend timetable is. Grouped so the schedule screen can colour and sort them
    // without a switch over every single kind.
    public enum ActivityKind
    {
        // --- the player's own car is on track ---
        Practice = 0,
        Qualifying = 1,
        Race = 2,

        // --- the team, behind closed doors ---
        TeamBriefing = 10,    // pre-weekend strategy meeting: where the weekend's plan is set
        Debrief = 11,         // after a session: what the R&D run told us, and what to change
        Orientation = 12,     // first weekend only: the crew chief walks you through the phone you run your life on

        // --- obligations to the sport ---
        DriversMeeting = 20,  // mandatory, two hours before the green flag of the race you are in
        DriverIntros = 21,    // on stage, half an hour before green

        // --- media ---
        PressConference = 30, // sat at the top table answering whatever they ask
        MediaHit = 31,        // a short broadcast interview, one question, no time to think

        // --- fans ---
        Autographs = 40,      // a scheduled signing session with a queue
        HaulerParade = 41,    // walking the haulers in as the gates open

        // --- sponsors ---
        SponsorDuty = 50,     // hospitality suite, meet-and-greet, pit-stop challenge
        PhotoShoot = 51,      // stills for the season's marketing

        // --- somebody else's race ---
        SpectatePractice = 60,
        SpectateQualifying = 61,
        SpectateRace = 62,

        Rest = 90,            // take the window off: nothing gained, nothing lost, the day moves on
    }

    public static class ActivityKinds
    {
        // The player is driving: these hand off to the race scene rather than running as a panel.
        public static bool IsOnTrack(ActivityKind k) =>
            k == ActivityKind.Practice || k == ActivityKind.Qualifying || k == ActivityKind.Race;

        public static bool IsSpectate(ActivityKind k) =>
            k == ActivityKind.SpectatePractice || k == ActivityKind.SpectateQualifying || k == ActivityKind.SpectateRace;

        public static bool IsMedia(ActivityKind k) =>
            k == ActivityKind.PressConference || k == ActivityKind.MediaHit;

        public static bool IsFanDuty(ActivityKind k) =>
            k == ActivityKind.Autographs || k == ActivityKind.HaulerParade;

        public static bool IsSponsorDuty(ActivityKind k) =>
            k == ActivityKind.SponsorDuty || k == ActivityKind.PhotoShoot;

        public static bool IsTeam(ActivityKind k) =>
            k == ActivityKind.TeamBriefing || k == ActivityKind.Debrief || k == ActivityKind.Orientation;

        // Ceremony: no skill in it, but the sport requires you there.
        public static bool IsCeremony(ActivityKind k) =>
            k == ActivityKind.DriversMeeting || k == ActivityKind.DriverIntros;

        // Short tag shown on the left of a timetable row.
        public static string Tag(ActivityKind k)
        {
            if (IsOnTrack(k)) return "TRACK";
            if (IsSpectate(k)) return "WATCH";
            if (IsMedia(k)) return "MEDIA";
            if (IsFanDuty(k)) return "FANS";
            if (IsSponsorDuty(k)) return "SPONSOR";
            if (IsTeam(k)) return "TEAM";
            if (IsCeremony(k)) return "OFFICIAL";
            return "OPEN";
        }
    }

    // One booking on the weekend timetable: what it is, when it starts, how long it takes the player out of
    // circulation, and what happens if they do not turn up.
    //
    // Times are minutes from midnight on that day, so a clash test is plain interval arithmetic.
    public class WeekendActivity
    {
        // Stable within a weekend — the ledger records completion against it, so it must not change between
        // scene loads. Built from kind + slot + start time in the timetable.
        public string id;

        public WeekendSlot slot;
        public ActivityKind kind;

        // Whose session this is. Meaningless for duties; set to the player's series so a caller can always
        // read it without a null check.
        public RacingSeries series;

        public string title;      // "CUP QUALIFYING", "AUTOGRAPH SESSION"
        public string subtitle;   // one line of what it actually is
        public string location;   // "Fan zone stage", "Media centre", "Pit road"

        // Name of the marker GameObject this booking sends the player to, instead of the one the venue for
        // its kind normally uses. Set from a plan file's "markerLocation" field, empty for everything else.
        //
        // This is what lets one circuit put its photo shoot against the transporter and another put it on a
        // podium, without either being a special case in code: the booking names an object, the track has an
        // object by that name, and the objective marker is wherever somebody dragged it to.
        public string markerLocation = "";

        public int startMinute;   // minutes from midnight
        public int minutes;       // how long it blocks the calendar for

        // A booking that has to have happened before this one means anything. A practice debrief with no
        // practice behind it is a meeting about a session nobody ran, so the sheet holds it back until the
        // run it discusses has been made. Empty for everything that stands on its own.
        public string requiresId = "";

        // The sport, the team or the contract says you are there. Skipping applies the penalty below.
        public bool mandatory;

        // What missing it costs, applied when the weekend moves past this activity's window unattended.
        public int skipMoneyPenalty;
        public float skipAppealPenalty;
        // Free-text reason shown on the missed-obligation notice ("the sponsor paid for that hour").
        public string skipReason;

        // Money paid just for turning up, before whatever the activity itself is scored at. Appearance fees
        // are how a lower-series driver actually eats.
        public int appearanceFee;

        public int EndMinute => startMinute + minutes;

        public string Clock => WeekendSlots.ClockRange(startMinute, minutes);

        public bool IsOnTrack => ActivityKinds.IsOnTrack(kind);
        public bool IsSpectate => ActivityKinds.IsSpectate(kind);

        // Two bookings collide when their windows overlap at all. Touching end-to-start is fine: the timetable
        // is built so a session ending at 14:00 and a signing starting at 14:00 are both doable.
        public bool ClashesWith(WeekendActivity other) =>
            other != null && slot == other.slot && startMinute < other.EndMinute && other.startMinute < EndMinute;

        public override string ToString() => $"{WeekendSlots.ShortLabel(slot)} {Clock} {title}";
    }
}
