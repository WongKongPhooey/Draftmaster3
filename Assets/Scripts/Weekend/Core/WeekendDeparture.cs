namespace Draftmaster.Weekend
{
    // Whether the player may leave the circuit yet.
    //
    // The travel map is the way to the next round, and it is reachable from the RV's satnav from the moment
    // the paddock is walkable. Nothing stopped a new career walking straight into the motorhome on Friday
    // morning and driving off to Daytona with the whole weekend at Watkins Glen unplayed. The road opens only
    // once the weekend is spent — Sunday afternoon over, or the player's race ended from the results screen
    // (WeekendObjectiveHUD.RoadCalled) — the same two conditions that put HIT THE ROAD on the objective strip.
    public static class WeekendDeparture
    {
        // currentWeekendId is RaceWeekend.WeekendId. A ledger still holding some other weekend's sheet has not
        // been started for this one yet (WeekendDirector does that), so its WeekendOver says nothing about it.
        public static bool WeekendDone(int currentWeekendId, bool roadCalled) =>
            roadCalled || (WeekendLedger.WeekendId == currentWeekendId && WeekendLedger.WeekendOver);

        public const string HeldReason =
            "The weekend isn't over yet. Practice, qualifying and the race come first - the road opens after Sunday.";
    }
}
