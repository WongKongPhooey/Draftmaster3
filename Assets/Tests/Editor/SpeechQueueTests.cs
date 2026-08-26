using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;

// One screen, one voice — the rules on their own, without a scene.
//
// The paddock has a hundred NPCs with something to say and a conversation the player is actually having.
// These are the decisions that stop those turning into three boxes at once.
public class SpeechQueueTests
{
    [Test]
    public void AnEmptyScreenIsAlwaysGranted()
    {
        foreach (SpeechPriority p in System.Enum.GetValues(typeof(SpeechPriority)))
            Assert.AreEqual(SpeechVerdict.Speak, SpeechQueue.Judge(null, p, sameSpeaker: false),
                            $"{p} should be able to speak when nothing else is.");
    }

    // A conversation advancing is the same voice continuing, whichever bubble it comes out of — the reply
    // must never be queued behind the line it answers.
    [Test]
    public void AVoiceNeverWaitsForItself()
    {
        Assert.AreEqual(SpeechVerdict.Speak,
                        SpeechQueue.Judge(SpeechPriority.Conversation, SpeechPriority.Conversation, sameSpeaker: true));
        Assert.AreEqual(SpeechVerdict.Speak,
                        SpeechQueue.Judge(SpeechPriority.Cutscene, SpeechPriority.Cutscene, sameSpeaker: true));
    }

    // Background flavour is exactly that: it never talks over anything, and it is never held to be said
    // later, because a passer-by's remark about a moment gone is worse than silence.
    [Test]
    public void AmbientChatterNeverInterruptsAndNeverQueues()
    {
        Assert.AreEqual(SpeechVerdict.Drop,
                        SpeechQueue.Judge(SpeechPriority.Conversation, SpeechPriority.Ambient, false),
                        "A passer-by talked over a conversation.");
        Assert.AreEqual(SpeechVerdict.Drop,
                        SpeechQueue.Judge(SpeechPriority.Cutscene, SpeechPriority.Ambient, false),
                        "A passer-by talked over a cutscene.");
        Assert.AreEqual(SpeechVerdict.Drop,
                        SpeechQueue.Judge(SpeechPriority.Ambient, SpeechPriority.Ambient, false),
                        "Two passers-by spoke at once.");
    }

    // A cutscene has taken control of the player to happen, so it gets the screen.
    [Test]
    public void AScriptedBeatCutsIn()
    {
        Assert.AreEqual(SpeechVerdict.Speak,
                        SpeechQueue.Judge(SpeechPriority.Conversation, SpeechPriority.Cutscene, false));
        Assert.AreEqual(SpeechVerdict.Speak,
                        SpeechQueue.Judge(SpeechPriority.Ambient, SpeechPriority.Cutscene, false));
    }

    // Two separate conversations at once is the bug this exists to stop: the second waits.
    [Test]
    public void TwoConversationsTakeTurns()
    {
        Assert.AreEqual(SpeechVerdict.Queue,
                        SpeechQueue.Judge(SpeechPriority.Conversation, SpeechPriority.Conversation, false));
        Assert.AreEqual(SpeechVerdict.Queue,
                        SpeechQueue.Judge(SpeechPriority.Cutscene, SpeechPriority.Cutscene, false));
    }

    // A queue that grows forever is a queue that says things minutes after they stopped making sense. The
    // oldest goes, not the newest — the last thing said to you is the one still worth hearing.
    [Test]
    public void TheQueueForgetsTheOldestRatherThanRefusingTheNewest()
    {
        var waiting = new List<string>();
        for (int i = 0; i < SpeechQueue.MaxWaiting + 3; i++)
            SpeechQueue.Enqueue(waiting, "line " + i);

        Assert.AreEqual(SpeechQueue.MaxWaiting, waiting.Count);
        Assert.AreEqual("line " + (SpeechQueue.MaxWaiting + 2), waiting[waiting.Count - 1],
                        "The newest line should still be in the queue.");
        Assert.IsFalse(waiting.Contains("line 0"), "The oldest line should have been dropped.");
    }
}
