using System.Collections.Generic;
using System.Reflection;
using Draftmaster.Crowd;
using NUnit.Framework;
using UnityEngine;

// Paddock walkers who arrive in twos, threes and fours: the wiring that holds a group together.
//
// A crowd of individuals is a crowd of strangers, so some of PaddockSpawner's wanderers are handed to a
// leader and keep the slot it holds for them (CrowdGrouping decides the shapes -- CrowdGroupingTests
// covers those). What is checked here is the part that can go wrong in a scene rather than on paper: that
// the slots are handed out in order, that somebody peeling off does not leave a hole in the numbering,
// and above all that a leader carried across the paddock by the crowd recycler takes its company with it.
// Leave that last one out and the recycler quietly strings three people out over a hundred metres.
//
// Assembly-CSharp cannot be referenced from an asmdef, so PaddockWalker is reached by reflection the same
// way PaddockObstacleTests reaches the router.
public class PaddockGroupTests
{
    static readonly System.Type WalkerType = System.Type.GetType("PaddockWalker, Assembly-CSharp");

    const float Spacing = 0.85f;

    readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp() => Assert.NotNull(WalkerType, "PaddockWalker is missing from Assembly-CSharp.");

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
        _spawned.Clear();
        Physics2D.SyncTransforms();
    }

    // --- fixtures ---------------------------------------------------------------------------------

    // A walker stood in an empty paddock, configured with a rectangle to wander the way the spawner
    // configures one. No Rigidbody2D and no CrowdActor: neither has had its Awake run in edit mode, and
    // the walker's own fallbacks cover both.
    Component Walker(Vector2 at)
    {
        var go = new GameObject("PaddockNPC_Walk_Test");
        go.transform.position = new Vector3(at.x, at.y, -0.1f);
        var w = go.AddComponent(WalkerType);
        _spawned.Add(go);

        Set(w, "groupSpacing", Spacing);
        WalkerType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
                  .Invoke(w, new object[] { Vector3.zero, Vector3.right, Vector3.up, 200f, 15f });
        return w;
    }

    // A leader with `size - 1` people in its company, all stood where the spawner would have put them.
    Component Group(int size, Vector2 at, out List<Component> followers)
    {
        var leader = Walker(at);
        FaceInstantly(leader, Vector2.up);
        followers = new List<Component>();

        for (int i = 1; i < size; i++)
        {
            Vector2 slot = (Vector2)at + CrowdGrouping.HuddleSlot(i, size, Spacing)
                                       - CrowdGrouping.HuddleSlot(0, size, Spacing);
            var f = Walker(slot);
            AddFollower(leader, f);
            followers.Add(f);
        }
        return leader;
    }

    // --- reflection wrappers ----------------------------------------------------------------------

    static void Set(object walker, string field, object value) =>
        WalkerType.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                  .SetValue(walker, value);

    static T Get<T>(object walker, string field) =>
        (T)WalkerType.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     .GetValue(walker);

    static T Prop<T>(object walker, string name) =>
        (T)WalkerType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(walker);

    static void Call(object walker, string method, params object[] args) =>
        WalkerType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(walker, args);

    static void AddFollower(object leader, object follower) =>
        WalkerType.GetMethod("AddFollower", BindingFlags.Public | BindingFlags.Instance)
                  .Invoke(leader, new[] { follower });

    static void FaceInstantly(object walker, Vector2 dir) =>
        WalkerType.GetMethod("FaceInstantly", BindingFlags.Public | BindingFlags.Instance)
                  .Invoke(walker, new object[] { dir });

    static Vector2 SlotWorld(object leader, int slot) =>
        (Vector2)WalkerType.GetMethod("SlotWorld", BindingFlags.Public | BindingFlags.Instance)
                           .Invoke(leader, new object[] { slot });

    static Vector2 PositionOf(Component c) => c.transform.position;

    // --- who is with whom -------------------------------------------------------------------------

    [Test]
    public void SlotsAreHandedOutInOrderBehindTheLeader()
    {
        var leader = Group(4, Vector2.zero, out var followers);

        Assert.AreEqual(4, Prop<int>(leader, "GroupSize"));
        Assert.IsTrue(Prop<bool>(leader, "HasCompany"));
        Assert.IsFalse(Prop<bool>(leader, "IsFollower"), "The leader cannot be following itself.");
        Assert.AreEqual(0, Get<int>(leader, "groupSlot"), "The leader holds slot 0.");

        for (int i = 0; i < followers.Count; i++)
        {
            Assert.AreEqual(i + 1, Get<int>(followers[i], "groupSlot"));
            Assert.IsTrue(Prop<bool>(followers[i], "IsFollower"));
            Assert.AreSame(leader, Get<object>(followers[i], "groupLeader"));
            Assert.AreEqual(Spacing, Get<float>(followers[i], "groupSpacing"), 0.001f,
                            "A follower measures its slot with a different spacing to its leader.");
        }
    }

    [Test]
    public void NobodyIsTakenIntoTheirOwnCompanyTwice()
    {
        var leader = Group(3, Vector2.zero, out var followers);

        AddFollower(leader, followers[0]);      // already in
        AddFollower(leader, leader);            // itself

        Assert.AreEqual(3, Prop<int>(leader, "GroupSize"), "The group grew by adding people already in it.");
    }

    [Test]
    public void SomebodyPeelingOffLeavesTheRestNumberedFromOne()
    {
        // A follower walled off from its slot gives up and wanders alone rather than freezing the group.
        // The gap it leaves has to close, or the last member aims at a slot on a ring of the wrong size.
        var leader = Group(4, Vector2.zero, out var followers);

        Call(followers[0], "LeaveGroup");

        Assert.AreEqual(3, Prop<int>(leader, "GroupSize"));
        Assert.IsFalse(Prop<bool>(followers[0], "IsFollower"), "The leaver is still following.");
        Assert.AreEqual(0, Get<int>(followers[0], "groupSlot"));
        Assert.AreEqual(1, Get<int>(followers[1], "groupSlot"), "Slots were left with a hole in them.");
        Assert.AreEqual(2, Get<int>(followers[2], "groupSlot"));
    }

    [Test]
    public void ALoneWalkerIsUntouchedByAnyOfIt()
    {
        var solo = Walker(new Vector2(20f, 3f));

        Assert.AreEqual(1, Prop<int>(solo, "GroupSize"));
        Assert.IsFalse(Prop<bool>(solo, "HasCompany"));
        Assert.IsFalse(Prop<bool>(solo, "IsFollower"));

        // Its own slot is wherever it is stood, so nothing can drag it anywhere.
        Assert.AreEqual(PositionOf(solo), SlotWorld(solo, 0));

        var path = Get<List<Vector3>>(solo, "_path");
        Call(solo, "OnRecycled");
        Assert.Greater(path.Count, 0, "A recycled lone walker was left with no route.");
    }

    // --- where the group stands -------------------------------------------------------------------

    [Test]
    public void AStoppedGroupStandsInARingWithinReachOfEachOther()
    {
        var leader = Group(4, new Vector2(60f, -8f), out var followers);
        Set(leader, "_moving", false);

        var stood = new List<Vector2> { PositionOf((Component)leader) };
        for (int i = 0; i < followers.Count; i++) stood.Add(SlotWorld(leader, i + 1));

        for (int i = 0; i < stood.Count; i++)
            for (int j = i + 1; j < stood.Count; j++)
            {
                float d = Vector2.Distance(stood[i], stood[j]);
                Assert.Greater(d, Spacing * 0.5f, $"Members {i} and {j} stand on top of each other.");
                Assert.Less(d, 3f, $"Members {i} and {j} are {d:F2}m apart — that is not a conversation.");
            }
    }

    [Test]
    public void AMovingGroupStringsOutBehindItsLeader()
    {
        var leader = Group(3, Vector2.zero, out _);
        FaceInstantly(leader, Vector2.up);        // walking north up the paddock
        Set(leader, "_moving", true);

        for (int slot = 1; slot <= 2; slot++)
            Assert.Less(SlotWorld(leader, slot).y, PositionOf((Component)leader).y,
                        $"Slot {slot} of a moving three is in front of the leader.");
    }

    [Test]
    public void TheGroupTurnsWithItsLeader()
    {
        // Slots are held in the leader's own frame, so a group that turns a corner swings round with it
        // rather than dragging its followers through the motorhome it just walked past.
        var leader = Group(3, Vector2.zero, out _);
        Set(leader, "_moving", true);

        FaceInstantly(leader, Vector2.up);
        Vector2 north = SlotWorld(leader, 1);

        FaceInstantly(leader, Vector2.right);
        Vector2 east = SlotWorld(leader, 1);

        Assert.Greater(Vector2.Distance(north, east), Spacing * 0.5f,
                       "The slot did not move when the leader turned ninety degrees.");
        Assert.AreEqual(Vector2.Distance(north, Vector2.zero), Vector2.Distance(east, Vector2.zero), 0.01f,
                        "Turning changed how far the follower stands from the leader.");
    }

    // --- the one that matters ---------------------------------------------------------------------

    [Test]
    public void RecyclingALeaderCarriesItsCompanyAcrossThePaddockWithIt()
    {
        // The crowd director picks filler up at the far end of the paddock and puts it back down just out
        // of shot near the player. Only the leader is recyclable, so if it did not gather its group on the
        // way, two people would be left a hundred metres behind — and the leash would have the leader
        // stood waiting for them for the rest of the session.
        var leader = Group(3, Vector2.zero, out var followers);

        var t = ((Component)leader).transform;
        t.position = new Vector3(140f, -60f, t.position.z);
        Call(leader, "OnRecycled");

        Vector2 lead = PositionOf((Component)leader);
        for (int i = 0; i < followers.Count; i++)
        {
            float d = Vector2.Distance(PositionOf(followers[i]), lead);
            Assert.Less(d, Spacing * 3f,
                        $"Follower {i} was left {d:F1}m behind when its leader was recycled.");
            Assert.AreEqual(-0.1f, PositionOf3(followers[i]).z, 0.001f,
                            "A follower was moved out of its own sorting plane.");
        }

        // And they are still a group afterwards, not three strangers who happen to be stood together.
        Assert.AreEqual(3, Prop<int>(leader, "GroupSize"));
    }

    [Test]
    public void AFollowerBeingMovedDoesNotDragTheWholeGroupRoundInCircles()
    {
        // PlaceAt calls OnRecycled on whoever it moved, and OnRecycled gathers that walker's company.
        // A follower has none, so this has to terminate rather than bouncing back up to its leader.
        var leader = Group(2, Vector2.zero, out var followers);

        Call(followers[0], "PlaceAt", new Vector2(5f, 5f));

        Assert.AreEqual(new Vector2(5f, 5f), PositionOf(followers[0]));
        Assert.AreEqual(Vector2.zero, PositionOf((Component)leader), "Moving a follower moved its leader.");
    }

    static Vector3 PositionOf3(Component c) => c.transform.position;
}
