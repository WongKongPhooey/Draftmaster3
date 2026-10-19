using Draftmaster.Sim;
using NUnit.Framework;

// When a stopped player car is offered the tow back to the pits. The damage gate stays for races (grid holds and
// caution queues stop undamaged cars on purpose); practice and qualifying offer it to any car stuck on the
// circuit, after a longer wait so a recoverable spin does not flash the prompt.
public class StrandedTowRuleTests
{
    const float DamageAtLeast = 0.12f, DamagedWait = 3f, UndamagedWait = 6f;

    static bool Offer(float stoppedFor, float damage, bool practiceLike, bool requireDamage = true) =>
        StrandedTowRule.Offer(stoppedFor, damage, practiceLike, requireDamage, DamageAtLeast, DamagedWait, UndamagedWait);

    [Test]
    public void WreckedCar_IsOffered_AfterTheShortWait_InAnySession()
    {
        Assert.IsFalse(Offer(2.9f, 0.3f, practiceLike: false));
        Assert.IsTrue(Offer(3f, 0.3f, practiceLike: false));
        Assert.IsTrue(Offer(3f, 0.3f, practiceLike: true));
    }

    [Test]
    public void LightlyDamagedCar_StoppedInQualifying_IsOffered_AfterTheLongerWait()
    {
        // One moderate hit (~0.05) used to leave a stopped car with no tow at all.
        Assert.IsFalse(Offer(5f, 0.05f, practiceLike: true));
        Assert.IsTrue(Offer(6f, 0.05f, practiceLike: true));
        Assert.IsTrue(Offer(6f, 0f, practiceLike: true));
    }

    [Test]
    public void UndamagedCar_StoppedInARace_IsNeverOffered()
    {
        Assert.IsFalse(Offer(600f, 0.05f, practiceLike: false));
        Assert.IsFalse(Offer(600f, 0f, practiceLike: false));
    }

    [Test]
    public void RequireDamageOff_OffersAnyStoppedCar_AfterTheShortWait()
    {
        Assert.IsTrue(Offer(3f, 0f, practiceLike: false, requireDamage: false));
    }

    [Test]
    public void NotStopped_IsNeverOffered()
    {
        Assert.IsFalse(Offer(-1f, 1f, practiceLike: true));
    }
}
