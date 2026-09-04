using Draftmaster.Weekend;

// Who is on the circuit right now, and whether the player is one of them.
//
// The weekend's schedule owns the track. Cars are only out for a designated practice, qualifying or race,
// and for the rest of the three days the circuit is empty because every driver is doing what the player is
// doing: a strategy meeting, a signing session, an hour under the sponsor's awning.
//
// Two answers come out of here:
//
//   * the player's own hour in the car — the sheet has already routed them into the race scene and set
//     RaceWeekend.SessionLive, and the scene runs its full session flow (grid, formation, directors);
//   * one of the other two championships running while the player is on foot — those cars are on track and
//     the player cannot take part, because there is no way into a car they are not entered in.
//
// This is the runtime half of WeekendTrackSessions: the rule lives in the core assembly where it is
// testable, and this is the bit that knows about RaceWeekend, the director and multiplayer.
public static class WeekendTrackState
{
    // What the circuit is doing. `any` false = nothing on it.
    public readonly struct Live
    {
        public readonly bool any;
        public readonly RacingSeries series;
        public readonly ActivityKind kind;   // Practice / Qualifying / Race — never a Spectate kind
        public readonly bool playerDriving;

        // The booking these cars are out for, so a field already on track can be told apart from the one
        // the next hour wants. Empty for an exhibition race started outside a weekend.
        public readonly string activityId;

        public Live(RacingSeries series, ActivityKind kind, bool playerDriving, string activityId)
        {
            any = true;
            this.series = series;
            this.kind = kind;
            this.playerDriving = playerDriving;
            this.activityId = activityId ?? "";
        }
    }

    public static Live Now()
    {
        // The player's own session. Multiplayer lands here too: a lobby that has loaded the track is a
        // race, with no weekend around it to ask.
        if (RaceWeekend.SessionLive)
        {
            var kind = RaceWeekend.Current switch
            {
                RaceWeekend.Session.Qualifying => ActivityKind.Qualifying,
                RaceWeekend.Session.Race => ActivityKind.Race,
                _ => ActivityKind.Practice,
            };
            return new Live(SeriesCatalog.PlayerSeries, kind, true, WeekendDirector.PendingRouteId);
        }

        // Nobody has put the player in a car, so the only thing that can be on track is somebody else's
        // session — read off the weekend clock.
        var running = WeekendTrackSessions.RunningNow(WeekendDirector.Timetable);
        if (running == null) return default;

        return new Live(running.series, WeekendTrackSessions.SessionKind(running.kind), false, running.id);
    }
}
