using System.Linq;
using NUnit.Framework;
using UnityEngine;

// The pit box cart wears its car's number as the same Resources/cup20num<n> art the cars carry on the roof,
// not as small text; a number with no art falls back to text. PitBoxStand lives in Assembly-CSharp, so it is
// reached by reflection.
public class PitBoxStandNumberTests
{
    GameObject _root;

    [SetUp] public void SetUp() => _root = new GameObject("PitBoxStandNumberTests");
    [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void Repaint_PutsTheCarNumberSpriteOnTheCanopy()
    {
        var expected = Resources.Load<Sprite>("cup20num89");
        Assert.IsNotNull(expected, "Resources/cup20num89 missing");

        var stand = Build();
        Repaint(stand, 89);

        var sr = stand.GetComponentsInChildren<SpriteRenderer>().SingleOrDefault();
        Assert.IsNotNull(sr, "the stand carries a number sprite");
        Assert.AreSame(expected, sr.sprite);
        Assert.Greater(sr.bounds.size.x, 1f, "the number fills the canopy, not a corner of it");
        Assert.IsFalse(stand.GetComponentsInChildren<TextMesh>().Any(), "no text number alongside the sprite");
    }

    [Test]
    public void Repaint_FallsBackToTextWhenTheNumberHasNoArt()
    {
        Assert.IsNull(Resources.Load<Sprite>("cup20num9999"));

        var stand = Build();
        Repaint(stand, 89);
        Repaint(stand, 9999);

        var tm = stand.GetComponentsInChildren<TextMesh>().SingleOrDefault();
        Assert.IsNotNull(tm, "a text number stands in");
        Assert.AreEqual("9999", tm.text);
        Assert.IsFalse(stand.GetComponentsInChildren<SpriteRenderer>().Any(), "the previous number sprite is hidden");
    }

    Component Build() =>
        (Component)Type().GetMethod("Build").Invoke(null, new object[] { _root.transform, 0, 6.2f, 0f });

    static void Repaint(Component stand, int number) =>
        Type().GetMethod("Repaint").Invoke(stand, new object[] { Color.blue, Color.green, number });

    static System.Type Type()
    {
        var type = System.AppDomain.CurrentDomain.GetAssemblies()
                         .Select(a => a.GetType("PitBoxStand", false))
                         .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, "PitBoxStand not found");
        return type;
    }
}
