namespace Draftmaster.Sim
{
    // Who a rivalry notice is actually for.
    //
    // Only the player's own feuds: the popup that says another car is furious with you, or that you have just
    // traded paint, is information a driver has (they felt the hit, their spotter is shouting). Two AI cars
    // falling out somewhere else in the field is not, and a stream of other people's arguments over the
    // screen is noise — in every session, and on the pit wall as much as in the car.
    public static class RivalryFeedAudience
    {
        // playerInvolved: the player is one of the two drivers named in the notice.
        public static bool ShouldAnnounce(bool playerInvolved) => playerInvolved;
    }
}
