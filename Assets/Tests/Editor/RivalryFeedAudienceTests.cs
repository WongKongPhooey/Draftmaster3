using NUnit.Framework;
using Draftmaster.Sim;

// EditMode coverage for who a rivalry notice is shown to. The feed itself is an OnGUI overlay that can only
// be judged by driving, so these pin the rule it asks: the player hears their own feuds and nobody else's.
public class RivalryFeedAudienceTests
{
    [Test]
    public void PlayerInvolved_Announced()
    {
        Assert.IsTrue(RivalryFeedAudience.ShouldAnnounce(playerInvolved: true));
    }

    [Test]
    public void TwoAiDrivers_NeverAnnounced()
    {
        Assert.IsFalse(RivalryFeedAudience.ShouldAnnounce(playerInvolved: false));
    }
}
