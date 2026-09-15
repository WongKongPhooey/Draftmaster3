namespace Draftmaster.Sim
{
    // Who a rivalry notice is actually for.
    //
    // Driving, you only hear about your own feuds: the popup that says another car is furious with you, or
    // that you have just traded paint, is information a driver has (they felt the hit, their spotter is
    // shouting). Two AI cars falling out somewhere else in the field is not — the driver never sees it,
    // and a stream of other people's arguments over the windscreen is noise.
    //
    // On the pit wall it is the opposite: the crew chief's job is the whole race, so field-wide notices
    // belong there alongside the fuel-and-stops telemetry the driver doesn't get.
    public static class RivalryFeedAudience
    {
        // playerInvolved: the player is one of the two drivers named in the notice.
        // crewChiefActive: the player is currently acting as crew chief rather than driving/walking.
        public static bool ShouldAnnounce(bool playerInvolved, bool crewChiefActive)
            => playerInvolved || crewChiefActive;
    }
}
