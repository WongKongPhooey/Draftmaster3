using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The walk to the Friday strategy briefing is where the phone first goes off: 200 m short of the pit box
// the crew chief texts to ask where the driver is, and a prompt says which key opens the phone.
//
// Pinned here: when that text is allowed to arrive (ChiefCheckIn, pure), what it says, and the phone it
// arrives on — MESSAGES in DrivR's old bay reading "1 unread message", and the form guide moved in with the
// championships under STATS rather than lost. The phone is Assembly-CSharp, so it is reached by reflection
// the same way WakeUpOpeningTests reaches the wake-up sequence.
public class ChiefCheckInTests
{
    // ------------------------------------------------------------------ when it fires

    const float Near = ChiefCheckIn.TriggerMetres - 1f;
    const float Far = ChiefCheckIn.TriggerMetres + 1f;

    static bool Fires(bool fired = false, ActivityKind booked = ChiefCheckIn.Booking, float metres = Near,
                      bool arrived = false, bool busy = false)
        => ChiefCheckIn.ShouldFire(fired, booked, metres, arrived, busy);

    [Test]
    public void TheTrigger_IsTwoHundredMetres()
    {
        Assert.AreEqual(200f, ChiefCheckIn.TriggerMetres);
    }

    [Test]
    public void WalkingToTheBriefing_FiresInsideTheTrigger_AndNotBeforeIt()
    {
        Assert.IsFalse(Fires(metres: Far), "The chief texted while the player was still over 200 m away.");
        Assert.IsTrue(Fires(metres: Near), "Inside 200 m of the briefing and the phone never went off.");
        Assert.IsTrue(Fires(metres: ChiefCheckIn.TriggerMetres), "Exactly 200 m out should count as inside.");
    }

    [Test]
    public void ItOnlyHappensOnce()
    {
        Assert.IsFalse(Fires(fired: true), "The chief asked where you were a second time.");
    }

    [Test]
    public void OnlyTheBriefingWalk_GetsTheText()
    {
        foreach (ActivityKind kind in System.Enum.GetValues(typeof(ActivityKind)))
        {
            if (kind == ActivityKind.TeamBriefing) continue;
            Assert.IsFalse(Fires(booked: kind), $"The chief's 'where are you' fired on the way to {kind}.");
        }
        Assert.AreEqual(ActivityKind.TeamBriefing, ChiefCheckIn.Booking);
    }

    [Test]
    public void NothingToWalkTo_NeverFires()
    {
        // DistanceRemaining reports -1 when there is no target or the player is not on foot (in the car).
        Assert.IsFalse(Fires(metres: -1f));
    }

    [Test]
    public void AlreadyStoodThere_DoesNotGetAskedWhereTheyAre()
    {
        // T-travel lands the player on the venue's mark in one wipe: zero metres, and arrived.
        Assert.IsFalse(Fires(metres: 0f, arrived: true));
    }

    [Test]
    public void ItWaitsForAConversationOrWipeToClear()
    {
        Assert.IsFalse(Fires(busy: true));
    }

    // ------------------------------------------------------------------ the run hint comes after the phone

    [Test]
    public void OnTheBriefingWalk_RunningWaitsForThePhone()
    {
        Assert.IsTrue(ChiefCheckIn.HoldsRunHint(alreadyFired: false, briefingBooked: true, phoneLessonLive: false),
                      "'Hold to run' went up before the phone had gone off on the walk to the briefing.");
    }

    [Test]
    public void WhileThePhoneIsOut_RunningStillWaits()
    {
        // Fired, so the save remembers it — but the player has not put the phone away yet.
        Assert.IsTrue(ChiefCheckIn.HoldsRunHint(alreadyFired: true, briefingBooked: true, phoneLessonLive: true));
        Assert.IsTrue(ChiefCheckIn.HoldsRunHint(alreadyFired: true, briefingBooked: false, phoneLessonLive: true));
    }

    [Test]
    public void OnceThePhoneIsPutAway_RunningIsTaught()
    {
        Assert.IsFalse(ChiefCheckIn.HoldsRunHint(alreadyFired: true, briefingBooked: true, phoneLessonLive: false),
                       "The phone lesson is over and the run hint is still being held back.");
    }

    [Test]
    public void AnyOtherWalk_DoesNotHoldRunningBack()
    {
        // No briefing booked (a later weekend, a scene with no liaison): nothing is coming, so nothing waits.
        Assert.IsFalse(ChiefCheckIn.HoldsRunHint(alreadyFired: false, briefingBooked: false, phoneLessonLive: false));
    }

    // An urgent prompt jumps the queue: the player is stood still until they read it, so a hint already on
    // screen — possibly a sticky one that never expires — must not keep it waiting.
    [Test]
    public void AnUrgentPrompt_GoesStraightToTheFront_AndTheOneItBumpedComesBack()
    {
        var uiType = System.Type.GetType("ControlHintUI, Assembly-CSharp");
        Assert.IsNotNull(uiType, "ControlHintUI not found in Assembly-CSharp.");
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var go = new GameObject("ControlHintUI (test)");
        try
        {
            var ui = go.AddComponent(uiType);
            var push = uiType.GetMethod("Push", Any);
            var update = uiType.GetMethod("Update", Any);
            var currentField = uiType.GetField("_current", Any);
            var queue = (IList)uiType.GetField("_queue", Any).GetValue(ui);
            System.Type hintType = uiType.GetNestedType("Hint", BindingFlags.NonPublic);
            string IdOf(object h) => (string)hintType.GetField("id").GetValue(h);
            float LeftOf(object h) => (float)hintType.GetField("secondsLeft").GetValue(h);

            push.Invoke(ui, new object[] { "tow", "Y", "Y", "Call a tow", Mathf.Infinity, false });
            update.Invoke(ui, null);                      // the sticky one is now on screen
            Assert.AreEqual("tow", IdOf(currentField.GetValue(ui)));

            push.Invoke(ui, new object[] { "phone", "P", "VIEW", "Check your phone", Mathf.Infinity, true });

            Assert.AreEqual(0f, LeftOf(currentField.GetValue(ui)), "The hint on screen should be fading out.");
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual("phone", IdOf(queue[0]), "The urgent prompt should be next up.");
            Assert.AreEqual("tow", IdOf(queue[1]), "The bumped hint should go back in line, not be lost.");
            Assert.IsTrue(float.IsInfinity(LeftOf(queue[1])), "The bumped hint should keep the time it had left.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------------ what it says

    [Test]
    public void TheMessage_AsksWhereTheyAre_NamesThemAndTheTime()
    {
        string text = ChiefCheckIn.Message("8:00 AM");
        StringAssert.Contains("where are you", text.ToLowerInvariant());
        StringAssert.Contains("{playerfirst}", text, "The text should greet the driver by name when it lands.");
        StringAssert.Contains("8:00 AM", text);
        StringAssert.Contains("pit box", text, "It should say where the crew is, since that is where the marker points.");
    }

    [Test]
    public void TheMessage_StillReads_WithoutATime()
    {
        string text = ChiefCheckIn.Message("");
        StringAssert.DoesNotContain(" at  ", text);
        StringAssert.Contains("where are you", text.ToLowerInvariant());
    }

    // ------------------------------------------------------------------ the phone it lands on

    static readonly System.Type PhoneType = System.Type.GetType("PhoneUI, Assembly-CSharp");
    static readonly System.Type AppType = System.Type.GetType("PhoneApp, Assembly-CSharp");
    static readonly System.Type MessagesAppType = System.Type.GetType("PhoneMessagesApp, Assembly-CSharp");
    static readonly System.Type StatsAppType = System.Type.GetType("PhoneStatsApp, Assembly-CSharp");

    // The home grid as the phone builds it, in tile order.
    static List<string> TileIds(out List<string> names)
    {
        Assert.IsNotNull(PhoneType, "PhoneUI not found in Assembly-CSharp.");
        var instance = PhoneType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNull(instance.GetValue(null), "A PhoneUI is already alive in edit mode; this test needs a clean one.");

        var go = new GameObject("PhoneUI (test)");
        try
        {
            var phone = go.AddComponent(PhoneType);
            // Edit mode does not run Awake; it is what claims Instance and builds the tiles.
            PhoneType.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(phone, null);

            var apps = (IList)PhoneType.GetField("_apps", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(phone);
            var ids = new List<string>();
            names = new List<string>();
            foreach (var app in apps)
            {
                ids.Add((string)AppType.GetProperty("Id").GetValue(app));
                names.Add((string)AppType.GetProperty("TileName").GetValue(app));
            }
            return ids;
        }
        finally
        {
            instance.GetSetMethod(true).Invoke(null, new object[] { null });
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void MessagesTakesDrivRsBay_AndStatsTakesPoints()
    {
        var ids = TileIds(out var names);
        int slots = (int)PhoneType.GetField("TileSlots", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();

        Assert.AreEqual(slots, ids.Count, "Every bay on the home grid should hold an app, and none past it.");
        Assert.AreEqual(4, ids.IndexOf("messages"), "MESSAGES belongs in the bay DrivR used to have. Tiles: " + string.Join(", ", ids));
        Assert.AreEqual(5, ids.IndexOf("stats"), "STATS belongs in the bay POINTS used to have. Tiles: " + string.Join(", ", ids));
        CollectionAssert.DoesNotContain(ids, "drivr", "DrivR is a tab under STATS now, not a tile.");
        CollectionAssert.DoesNotContain(ids, "championship", "POINTS is a tab under STATS now, not a tile.");
        Assert.AreEqual("MESSAGES", names[4]);
        Assert.AreEqual("STATS", names[5]);
    }

    [Test]
    public void TheMessagesTile_CountsWhatIsWaiting()
    {
        Assert.IsNotNull(MessagesAppType, "PhoneMessagesApp not found in Assembly-CSharp.");
        var label = MessagesAppType.GetMethod("UnreadLabel", BindingFlags.Public | BindingFlags.Static);
        string Say(int n) => (string)label.Invoke(null, new object[] { n });

        Assert.AreEqual("1 unread message", Say(1));
        Assert.AreEqual("3 unread messages", Say(3));
        Assert.AreEqual("No new messages", Say(0));
    }

    [Test]
    public void TheFormGuide_LivesUnderStats_BesideThePoints()
    {
        Assert.IsNotNull(StatsAppType, "PhoneStatsApp not found in Assembly-CSharp.");
        var stats = System.Activator.CreateInstance(StatsAppType);
        var pages = (System.Array)StatsAppType.GetField("_pages", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stats);

        var pageIds = new List<string>();
        foreach (var page in pages) pageIds.Add((string)AppType.GetProperty("Id").GetValue(page));

        CollectionAssert.AreEqual(new[] { "championship", "drivr" }, pageIds,
                                  "STATS should open on the championships, with the form guide as its second tab.");
    }

    // ------------------------------------------------------------------ the bleep

    [Test]
    public void TheBleep_IsAudible_AndShort()
    {
        var beat = System.Type.GetType("ChiefCheckInBeat, Assembly-CSharp");
        Assert.IsNotNull(beat, "ChiefCheckInBeat not found in Assembly-CSharp.");
        var clip = (AudioClip)beat.GetMethod("TextTone", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

        Assert.IsNotNull(clip, "The text tone did not build, so the phone goes off in silence.");
        Assert.Less(clip.length, 1f, "A text alert, not a ringtone.");

        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);
        float peak = 0f;
        foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        Assert.Greater(peak, 0.1f, "The text tone is silent.");
    }
}
