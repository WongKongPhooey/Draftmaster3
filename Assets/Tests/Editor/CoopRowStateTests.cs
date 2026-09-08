using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// The PLAY WITH A FRIEND row on the pause menu.
//
// The rule being pinned here is the first one: a press has to change the row on the very next repaint.
// Opening a co-op session is several seconds of UGS sign-in and Relay allocation before anything about the
// game changes, and the row used to be driven off the session mode alone — which flips at the far END of
// sign-in — so the tab sat there still reading PLAY WITH A FRIEND and the press read as a press that had
// missed. The launcher's Busy flag is set inside the click, so it is what the row leads on.
//
// As with CoopNameTagTests, nothing here names a type from Assembly-CSharp: this assembly cannot reference
// the predefined assemblies, so RacePauseMenu is reached by reflection.
public class CoopRowStateTests
{
    static MethodInfo _rowState;

    [OneTimeSetUp]
    public void FindMethod()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType("RacePauseMenu", false))
                            .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, "No RacePauseMenu type — the pause menu has moved or been renamed.");

        _rowState = type.GetMethod("CoopRowState", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(_rowState, "RacePauseMenu has no static CoopRowState — the co-op row's states are " +
                                    "no longer a rule that can be asserted.");
    }

    static string State(bool busy, bool active, bool isGuest, bool guestPresent, bool hasCode, bool failed)
        => _rowState.Invoke(null, new object[] { busy, active, isGuest, guestPresent, hasCode, failed })
                    .ToString();

    [Test]
    public void ClickIsAcknowledgedBeforeTheModeFlips()
    {
        // The frame after the press: the launcher is busy signing in, and NOTHING about the session has
        // happened yet — no co-op mode, no code. The row must already say it is working.
        Assert.AreEqual("Opening",
                        State(busy: true, active: false, isGuest: false, guestPresent: false,
                              hasCode: false, failed: false));
    }

    [Test]
    public void IdleRowOffersToOpen()
    {
        Assert.AreEqual("Offer",
                        State(busy: false, active: false, isGuest: false, guestPresent: false,
                              hasCode: false, failed: false));
    }

    [Test]
    public void FailedAttemptSaysSoInsteadOfSilentlyReverting()
    {
        Assert.AreEqual("Retry",
                        State(busy: false, active: false, isGuest: false, guestPresent: false,
                              hasCode: false, failed: true));
    }

    [Test]
    public void RetryingOverridesAStaleFailure()
    {
        // The failure notice is on a timer, so a second press happens while it is still up. Busy wins.
        Assert.AreEqual("Opening",
                        State(busy: true, active: false, isGuest: false, guestPresent: false,
                              hasCode: false, failed: true));
    }

    [Test]
    public void HostWithASessionShowsTheCode()
    {
        Assert.AreEqual("Code",
                        State(busy: false, active: true, isGuest: false, guestPresent: false,
                              hasCode: true, failed: false));
    }

    [Test]
    public void HostStillWaitingOnASessionKeepsSayingOpening()
    {
        Assert.AreEqual("Opening",
                        State(busy: false, active: true, isGuest: false, guestPresent: false,
                              hasCode: false, failed: false));
    }

    [Test]
    public void ConnectedGuestReplacesTheCode()
    {
        Assert.AreEqual("GuestConnected",
                        State(busy: false, active: true, isGuest: false, guestPresent: true,
                              hasCode: true, failed: false));
    }

    [Test]
    public void GuestSeesWhoseWeekendItIs()
    {
        Assert.AreEqual("Guest",
                        State(busy: false, active: true, isGuest: true, guestPresent: false,
                              hasCode: false, failed: false));
    }
}
