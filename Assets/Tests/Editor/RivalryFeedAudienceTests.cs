using NUnit.Framework;
using Draftmaster.Sim;

// EditMode coverage for who a rivalry notice is shown to. The feed itself is an OnGUI overlay that can only
// be judged by driving, so these pin the rule it asks: the driver hears their own feuds, and the rest of the
// field's arguments belong to the crew chief.
public class RivalryFeedAudienceTests
{
    [Test]
    public void PlayerInvolved_AlwaysAnnounced()
    {
        Assert.IsTrue(RivalryFeedAudience.ShouldAnnounce(playerInvolved: true, crewChiefActive: false));
        Assert.IsTrue(RivalryFeedAudience.ShouldAnnounce(playerInvolved: true, crewChiefActive: true));
    }

    [Test]
    public void TwoAiDrivers_HiddenFromTheDriver()
    {
        Assert.IsFalse(RivalryFeedAudience.ShouldAnnounce(playerInvolved: false, crewChiefActive: false));
    }

    [Test]
    public void TwoAiDrivers_ShownToTheCrewChief()
    {
        Assert.IsTrue(RivalryFeedAudience.ShouldAnnounce(playerInvolved: false, crewChiefActive: true));
    }
}
