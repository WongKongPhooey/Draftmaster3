using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

// The name tag over the other player's head in co-op.
//
// Two things are pinned. First, the label itself: it is whatever that player typed on the OPTIONS screen,
// and a player who has never been through OPTIONS still gets a tag saying which of the two they are rather
// than a blank plate. Second, the tag is a free-standing world object sat above the body and pulled toward
// the camera — not a child of it, because the body rotates to face where it is walking and a parented tag
// would turn upside down with it.
//
// As with OptionsScreenTests, nothing here names a type from Assembly-CSharp: this assembly cannot
// reference the predefined assemblies, so CoopNameTag and CoopBodies are reached by reflection. Nothing
// here writes PlayerPrefs either, so running the tests cannot rename whoever is mid-career.
public class CoopNameTagTests
{
    GameObject _body;
    GameObject _tagGo;
    object _hierarchyEnabledWas;

    static Type Find(string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType(name, false))
                            .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, $"No {name} type — the co-op name tag has moved or been renamed.");
        return type;
    }

    static Type NameTag() => Find("CoopNameTag");
    static Type Bodies() => Find("CoopBodies");

    [SetUp]
    public void SetUp()
    {
        // RuntimeHierarchy.Adopt would file the tag under a "UI" bucket it creates in whatever scene the
        // editor happens to have open. Off for the duration, so the tag stays a root object this test can
        // destroy outright and the open scene is left exactly as it was found.
        var flag = Find("RuntimeHierarchy").GetField("Enabled", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(flag, "RuntimeHierarchy.Enabled has gone.");
        _hierarchyEnabledWas = flag.GetValue(null);
        flag.SetValue(null, false);
    }

    [TearDown]
    public void TearDown()
    {
        if (_tagGo != null) UnityEngine.Object.DestroyImmediate(_tagGo);
        if (_body != null) UnityEngine.Object.DestroyImmediate(_body);

        var flag = Find("RuntimeHierarchy").GetField("Enabled", BindingFlags.Public | BindingFlags.Static);
        if (flag != null && _hierarchyEnabledWas != null) flag.SetValue(null, _hierarchyEnabledWas);
    }

    Component Attach(string label)
    {
        _body = new GameObject("CoopPuppetProbe");
        _body.transform.position = new Vector3(4f, 2f, 0f);

        var attach = NameTag().GetMethod("Attach", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(attach, "CoopNameTag.Attach has gone.");

        var tag = attach.Invoke(null, new object[] { _body.transform, label }) as Component;
        Assert.IsNotNull(tag, "CoopNameTag.Attach returned nothing.");
        _tagGo = tag.gameObject;
        return tag;
    }

    static TextMeshPro LabelOf(Component tag)
    {
        var label = tag.GetComponentInChildren<TextMeshPro>();
        Assert.IsNotNull(label, "The tag has no TextMeshPro — nothing would be drawn.");
        return label;
    }

    // ------------------------------------------------------------------ the label

    [Test]
    public void TheTagShowsTheNameTheOtherPlayerSent()
    {
        var tag = Attach("Josh Wong");
        Assert.AreEqual("Josh Wong", LabelOf(tag).text);
    }

    [Test]
    public void ARenameOnTheOtherMachineChangesTheTag()
    {
        var tag = Attach("Josh Wong");

        var setName = NameTag().GetMethod("SetName", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(setName, "CoopNameTag.SetName has gone.");
        setName.Invoke(tag, new object[] { "Kyle Larson" });

        Assert.AreEqual("Kyle Larson", LabelOf(tag).text);
    }

    [Test]
    public void AnUnnamedPlayerStillGetsATagSayingWhichPlayerTheyAre()
    {
        var type = NameTag();
        var labelFor = type.GetMethod("LabelFor", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(labelFor, "CoopNameTag.LabelFor has gone.");

        string host = (string)type.GetField("HostFallback", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        string guest = (string)type.GetField("GuestFallback", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Assert.AreNotEqual(host, guest, "Both players falling back to the same label tells you nothing.");

        Assert.AreEqual(host, labelFor.Invoke(null, new object[] { "", true }));
        Assert.AreEqual(guest, labelFor.Invoke(null, new object[] { "", false }));
        Assert.AreEqual(guest, labelFor.Invoke(null, new object[] { null, false }));
        Assert.AreEqual(host, labelFor.Invoke(null, new object[] { "   ", true }),
                        "A name of nothing but spaces is not a name.");
    }

    [Test]
    public void ANameIsTrimmedRatherThanDrawnWithItsPadding()
    {
        var labelFor = NameTag().GetMethod("LabelFor", BindingFlags.Public | BindingFlags.Static);
        Assert.AreEqual("Josh Wong", labelFor.Invoke(null, new object[] { "  Josh Wong  ", false }));
    }

    // ------------------------------------------------------------------ where it sits

    [Test]
    public void TheTagFloatsAboveTheBodyAndInFrontOfTheGround()
    {
        var tag = Attach("Josh Wong");

        Vector3 body = _body.transform.position;
        Vector3 at = tag.transform.position;

        Assert.Greater(at.y, body.y, "The tag is not above the head.");
        Assert.AreEqual(body.x, at.x, 0.001f, "The tag should sit centred over the body.");
        Assert.Less(at.z, body.z,
                    "The tag must be pulled toward the camera or the opaque ground plane depth-culls it.");
    }

    [Test]
    public void TheTagIsNotParentedToTheBodyItFollows()
    {
        var tag = Attach("Josh Wong");
        Assert.IsFalse(tag.transform.IsChildOf(_body.transform),
                       "A parented tag inherits the body's facing rotation and turns upside down with it.");
    }

    [Test]
    public void TheTagDoesNotInheritTheBodysFacingSpin()
    {
        var tag = Attach("Josh Wong");
        _body.transform.rotation = Quaternion.Euler(0f, 0f, 180f);

        // Attach positions it once; the follow that runs every frame is what keeps it upright, and it is
        // the same call. Re-running it against a spun body must still leave the tag level.
        var follow = NameTag().GetMethod("Follow", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(follow, "CoopNameTag.Follow has gone — the tag no longer tracks its body.");
        follow.Invoke(tag, null);

        Assert.AreEqual(0f, Quaternion.Angle(tag.transform.rotation, Quaternion.identity), 0.01f,
                        "The tag rotated with the body.");
        Assert.Greater(tag.transform.position.y, _body.transform.position.y,
                       "The tag stopped tracking the body's head.");
    }

    // ------------------------------------------------------------------ where the name comes from

    [Test]
    public void TheNameOnTheWireIsTheOneTheOptionsScreenSaves()
    {
        var localName = Bodies().GetMethod("LocalDisplayName", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(localName,
                         "CoopBodies.LocalDisplayName has gone — nothing feeds the other player's tag.");

        var driver = Find("PlayerDriver");
        string first = (string)driver.GetProperty("FirstName", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        string last = (string)driver.GetProperty("LastName", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        // Read-only: whatever this machine's career name happens to be, the name put on the wire must be
        // the two halves the OPTIONS boxes write, and nothing else.
        Assert.AreEqual((first + " " + last).Trim(), (string)localName.Invoke(null, null));
    }

    [Test]
    public void NameTagsAreOnByDefault()
    {
        var field = Bodies().GetField("showNameTags", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, "CoopBodies.showNameTags has gone.");

        var go = new GameObject("CoopBodiesProbe");
        try
        {
            var bodies = go.AddComponent(Bodies());
            Assert.IsTrue((bool)field.GetValue(bodies),
                          "Name tags default off, so co-op ships without them unless someone ticks a box.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
