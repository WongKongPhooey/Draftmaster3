using System.Reflection;
using NUnit.Framework;

// EditMode coverage for the loading card. The load itself is a coroutine over an async scene load and can only
// be watched in play mode; what is pinned here is what decides whether the card reads right:
//   * it draws IN FRONT of ScreenFade's wipe — under it, a caller that faded to black first (the weekend
//     director) would show nothing but black for the whole load, which is the freeze this replaced;
//   * the bar never claims the scene is ready while it is still being read in.
//
// Assembly-CSharp can't be referenced by an asmdef, so the types are reached by reflection.
public class LoadingScreenTests
{
    static readonly System.Type CardType = System.Type.GetType("LoadingScreen, Assembly-CSharp");
    static readonly System.Type FadeType = System.Type.GetType("ScreenFade, Assembly-CSharp");

    static T Const<T>(System.Type type, string name) =>
        (T)type.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    static float Bar(float asyncProgress) => (float)CardType
        .GetMethod("BarForReadProgress", BindingFlags.Public | BindingFlags.Static)
        .Invoke(null, new object[] { asyncProgress });

    [Test]
    public void CardDrawsInFrontOfTheWipe()
    {
        Assert.IsNotNull(CardType, "LoadingScreen not found in Assembly-CSharp.");
        Assert.IsNotNull(FadeType, "ScreenFade not found in Assembly-CSharp.");
        int card = Const<int>(CardType, "CardDepth");
        int wipe = Const<int>(FadeType, "WipeDepth");
        Assert.Less(card, wipe, "Lower GUI.depth draws in front: the loading card must sit under ScreenFade's number.");
    }

    [Test]
    public void BarFillsWithTheReadAndStopsShortOfFull()
    {
        float share = Const<float>(CardType, "ReadShare");
        Assert.AreEqual(0f, Bar(0f), 1e-5f, "Nothing read, nothing shown.");
        Assert.AreEqual(share, Bar(0.9f), 1e-5f, "AsyncOperation.progress tops out at 0.9 — that is the end of the read.");
        Assert.AreEqual(share, Bar(1f), 1e-5f, "Activation reports no more than the read did.");
        Assert.Less(share, 1f, "The last stretch of the bar belongs to the new scene's first frames.");

        float last = -1f;
        for (float p = 0f; p <= 1f; p += 0.05f)
        {
            float b = Bar(p);
            Assert.GreaterOrEqual(b, last, $"Bar went backwards at progress {p:0.00}.");
            last = b;
        }
    }
}
