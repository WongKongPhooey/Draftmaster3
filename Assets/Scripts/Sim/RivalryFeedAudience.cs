namespace Draftmaster.Sim
{
    // Who a rivalry notice is actually for.
    //
    // Rivalry popups — contacts, "now rivals", "furious with", "wants payback" — are pit-wall notices: the crew
    // chief is the one reading the room. In the car they are banners over the racing for things the driver
    // can't act on, so none show while driving or on foot; they show only while the player is crew chief.
    //
    // Even on the pit wall it is only the player's own feuds. Two AI cars falling out somewhere else in the
    // field is a stream of other people's arguments over the screen — noise.
    public static class RivalryFeedAudience
    {
        // playerInvolved: the player is one of the two drivers named in the notice.
        // crewChiefActive: the player is on the pit wall (CrewChiefController.IsCrewChief).
        public static bool ShouldAnnounce(bool playerInvolved, bool crewChiefActive)
            => playerInvolved && crewChiefActive;
    }
}
