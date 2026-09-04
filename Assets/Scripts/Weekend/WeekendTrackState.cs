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

    // ------------------------------------------------------------------ a session somebody is watching

    // The stand holds the circuit open.
    //
    // Arriving in the grandstand IS the booking done — the sheet moves on and the clock jumps to the end of
    // the hour the moment the player sits down — so read off the ledger alone, the session they came to
    // watch would end the instant they got there and the field would be cleared out from under them.
    //
    // So the seat takes the session off the clock and holds it: for as long as the player is sat there the
    // circuit belongs to that championship, and GrandstandVisit plays the hour out at speed against its own
    // timer. Released when the compressed session ends or the player gets up, and the track goes cold on
    // whatever the sheet says next.
    static Live _held;
    static bool _holding;

    // Raised when a hold is taken or given back, so the field can be put out or taken in. The weekend
    // clock's own Changed event covers every other case; this is the one that does not move the clock.
    public static event System.Action HoldChanged;

    public static bool Holding => _holding;

    public static void Hold(RacingSeries series, ActivityKind kind, string activityId)
    {
        _held = new Live(series, kind, false, activityId);
        _holding = true;
        HoldChanged?.Invoke();
    }

    public static void Release()
    {
        if (!_holding) return;
        _holding = false;
        _held = default;
        HoldChanged?.Invoke();
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

        // Somebody is sat in the stand watching. Their session outranks the clock, which has already been
        // moved past it.
        if (_holding) return _held;

        // Nobody has put the player in a car, so the only thing that can be on track is somebody else's
        // session — read off the weekend clock.
        var running = WeekendTrackSessions.RunningNow(WeekendDirector.Timetable);
        if (running == null) return default;

        return new Live(running.series, WeekendTrackSessions.SessionKind(running.kind), false, running.id);
    }
}
