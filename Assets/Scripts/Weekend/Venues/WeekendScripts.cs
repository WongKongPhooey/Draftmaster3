using Draftmaster.Weekend;
using UnityEngine;

// Which conversation a booking is, and everything about the player's situation that the content needs but
// the core assembly cannot see: who is paying for the hood, who they are feuding with, how the last race
// finished.
//
// The content itself lives in Core/Conversations — pure and testable. This is the seam where it is handed
// the runtime's facts and turned into the script the venue's host plays.
public static class WeekendScripts
{
    public static WeekendConversation For(WeekendActivity a)
    {
        if (a == null) return null;

        switch (a.kind)
        {
            case ActivityKind.TeamBriefing:
            case ActivityKind.Debrief:
                return TeamMeetingContent.Build(a);

            case ActivityKind.PressConference:
            case ActivityKind.MediaHit:
                return Press(a);

            case ActivityKind.DriversMeeting:
            case ActivityKind.DriverIntros:
                return CeremonyContent.Build(a);

            case ActivityKind.SponsorDuty:
            case ActivityKind.PhotoShoot:
                return SponsorContent.Build(a, LeadSponsor());

            case ActivityKind.Autographs:
            case ActivityKind.HaulerParade:
                return SigningContent.Build(a);

            default:
                return null;   // on-track sessions and spectating are not conversations
        }
    }

    // ------------------------------------------------------------------ the press

    // The question bank is already pure and already scores its own answers, so a press conference becomes a
    // conversation by asking it for its questions and pricing each answer through the same Score() the
    // panel used. Nothing about what an answer is worth changed when the room became a room.
    static WeekendConversation Press(WeekendActivity a)
    {
        var ctx = BuildContext(a);
        bool hit = a.kind == ActivityKind.MediaHit;
        var questions = PressConferenceContent.Build(ctx, a.id, hit ? 1 : 3);

        var c = new WeekendConversation
        {
            statKey = "pressconferences",
            statCount = 1,
            greeting = hit
                ? new[] { "Two minutes, live in thirty seconds. One question, that is all they want." }
                : new[]
                {
                    "Take the middle chair. Water is in front of you.",
                    "Room is about half full and they have been here since the trucks ran.",
                },
            farewell = hit
                ? new[] { "And we are clear. Thanks." }
                : new[] { "That is time. Thank you, everybody." },
        };

        foreach (var q in questions)
        {
            if (q == null || q.answers == null || q.answers.Count == 0) continue;

            var beat = new WeekendBeat
            {
                speaker = string.IsNullOrEmpty(q.outlet) ? q.reporter : $"{q.reporter}, {q.outlet}",
                line = q.text,
                question = q.text,
            };

            foreach (var answer in q.answers)
            {
                var scored = PressConferenceContent.Score(answer, ctx);
                beat.choices.Add(new WeekendChoice
                {
                    text = answer.text,
                    response = PressConferenceContent.Reaction(answer),
                    money = scored.money,
                    appeal = scored.fanAppeal,
                    sponsor = scored.sponsorMood,
                    morale = scored.teamMorale,
                    media = scored.mediaStanding,
                    setup = scored.setupGain,
                    rivalName = scored.rivalName,
                    rivalDelta = scored.rivalDelta,
                    score = Mathf.Clamp01(scored.score),
                });
            }

            c.Add(beat);
        }

        c.headline = o =>
            o.mediaStanding >= 8f ? "You gave them something to write, and they wrote it."
            : o.mediaStanding <= -6f ? "A short room, a shorter answer, and nobody left with a story."
            : o.sponsorMood < 0f ? "Honest. The people paying for the hood will have watched that."
            : "Questions taken, nothing broken. That is a press conference.";
        return c;
    }

    // Everything the question bank needs about where the player is standing.
    public static PressContext BuildContext(WeekendActivity a)
    {
        return new PressContext
        {
            series = SeriesCatalog.PlayerSeries,
            trackName = TrackSelection.CurrentDisplayName,
            rivalName = WorstRival(),
            sponsorName = LeadSponsor(),
            weekendId = RaceWeekend.WeekendId,
            raceDay = a != null && WeekendSlots.Day(a.slot) == WeekendSlots.Day(WeekendTimetable.RaceTime(SeriesCatalog.PlayerSeries).slot),
            mediaStanding = WeekendLedger.MediaStanding,
            lastFinish = PlayerStatsLedger.Get("lastfinish"),
            qualifiedWell = QualifiedWell(),
            ranPractice = TeamMeetingContent.RanOwnPractice(),
        };
    }

    // The driver the player is on worst terms with. Negative values are bad blood; anything above the
    // threshold is not a feud worth a question.
    static string WorstRival()
    {
        string me = DriverRelationships.PlayerName;
        string worst = "";
        float lowest = -15f;
        foreach (var (a, b, value) in DriverRelationships.AllPairs())
        {
            string other = a == me ? b : (b == me ? a : null);
            if (other == null) continue;
            if (value < lowest) { lowest = value; worst = other; }
        }
        return worst;
    }

    public static string LeadSponsor()
    {
        var deals = Draftmaster.Sponsors.SponsorBook.Deals;
        for (int i = 0; i < deals.Count; i++)
            if (deals[i] != null && deals[i].IsPlaced && deals[i].IsActive) return deals[i].sponsorName;
        for (int i = 0; i < deals.Count; i++)
            if (deals[i] != null && deals[i].IsActive) return deals[i].sponsorName;
        return "";
    }

    static bool QualifiedWell()
    {
        var grid = RaceWeekend.GridOrder;
        if (grid == null) return false;
        for (int i = 0; i < grid.Count && i < 10; i++)
            if (grid[i] != null && grid[i].isPlayer) return true;
        return false;
    }
}
