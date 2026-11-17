using NUnit.Framework;
using Draftmaster.Sim;

// EditMode coverage for who a rivalry notice is shown to. The feed itself is an OnGUI overlay that can only
// be judged by driving, so these pin the rule it asks: rivalry popups are crew-chief only, and even then only
// the player's own feuds.
public class RivalryFeedAudienceTests
{
    [Test]
    public void PlayerInvolved_OnPitWall_Announced()
    {
        Assert.IsTrue(RivalryFeedAudience.ShouldAnnounce(playerInvolved: true, crewChiefActive: true));
    }

    [Test]
    public void PlayerInvolved_WhileDriving_NeverAnnounced()
    {
        Assert.IsFalse(RivalryFeedAudience.ShouldAnnounce(playerInvolved: true, crewChiefActive: false));
    }

    [Test]
    public void TwoAiDrivers_OnPitWall_NotAnnounced()
    {
        Assert.IsFalse(RivalryFeedAudience.ShouldAnnounce(playerInvolved: false, crewChiefActive: true));
    }

    [Test]
    public void TwoAiDrivers_WhileDriving_NotAnnounced()
    {
        Assert.IsFalse(RivalryFeedAudience.ShouldAnnounce(playerInvolved: false, crewChiefActive: false));
    }
}
