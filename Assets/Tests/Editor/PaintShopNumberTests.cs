using System.Linq;
using Draftmaster.Sponsors;
using NUnit.Framework;
using UnityEngine;

// The car number is its own layer in the Paint Scheme Designer: the #89's paint layers carry no number, and
// PaintShop stamps Resources/cup20num89 on the roof in the scheme's number colour. PaintShop lives in
// Assembly-CSharp, so the end-to-end checks reach it by reflection.
public class PaintShopNumberTests
{
    const string Livery = "cup26livery89";

    // A digit pixel of cup20num89 (top of the 8's white fill) and where it lands on the livery.
    static readonly Vector2Int kDigitOnLivery = new Vector2Int(32, 20);

    [Test]
    public void StampNumber_TintsTheDigitsAndKeepsTheOutlineDark()
    {
        var px = new[] { new Color32(175, 175, 175, 255), new Color32(175, 175, 175, 255), new Color32(175, 175, 175, 255) };
        var number = new[] { new Color32(255, 255, 255, 255), new Color32(59, 59, 59, 255), new Color32(0, 0, 0, 0) };

        LiveryCompositor.StampNumber(px, 3, 1, number, 3, 1, 0, 0, new Color32(200, 30, 35, 255));

        Assert.AreEqual(new Color32(200, 30, 35, 255), px[0], "White digits take the number colour.");
        Assert.AreEqual(new Color32(46, 7, 8, 255), px[1], "The outline is tinted but stays dark.");
        Assert.AreEqual(new Color32(175, 175, 175, 255), px[2], "Transparent number pixels leave the paint alone.");
    }

    [Test]
    public void StampNumber_DropsPixelsOffTheLivery()
    {
        var px = new[] { new Color32(1, 1, 1, 255) };
        var number = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
        Assert.DoesNotThrow(() => LiveryCompositor.StampNumber(px, 1, 1, number, 2, 1, -1, 0, new Color32(9, 9, 9, 255)));
        Assert.AreEqual(new Color32(9, 9, 9, 255), px[0]);
    }

    [Test]
    public void OldSavesLoadWithAWhiteNumberAndTheNumberIsPartOfTheSignature()
    {
        var old = JsonUtility.FromJson<PaintScheme>("{\"baseColour\":{\"r\":200,\"g\":30,\"b\":35,\"a\":255},\"pattern\":\"\"}");
        Assert.IsFalse(old.factory, "a saved scheme from before the number layer was a repaint");
        Assert.AreEqual(PaintScheme.DefaultNumberColour, old.numberColour);

        var red = old.Clone();
        red.numberColour = new Color32(200, 30, 35, 255);
        Assert.AreNotEqual(old.Signature, red.Signature, "a recoloured number must not hit the old render cache");

        Assert.IsTrue(new PaintScheme { factory = true }.IsStock);
        Assert.IsFalse(new PaintScheme { factory = true, numberColour = red.numberColour }.IsStock);
    }

    [Test]
    public void FactoryArtHasNoBakedNumber()
    {
        var blank = Resources.Load<Texture2D>("PaintShop/" + Livery + "_blank");
        Assert.IsNotNull(blank, $"Resources/PaintShop/{Livery}_blank missing");
        Assert.IsTrue(blank.isReadable);
        Assert.AreEqual(new Color32(175, 175, 175, 255), (Color32)blank.GetPixel(kDigitOnLivery.x, kDigitOnLivery.y),
            "the roof under the number is plain paint");

        var detail = Resources.Load<Texture2D>("PaintShop/" + Livery + "_detail");
        Assert.AreEqual(0, detail.GetPixel(kDigitOnLivery.x, kDigitOnLivery.y).a, 0.001f, "the detail layer no longer draws the number");
    }

    [Test]
    public void Render_StampsTheNumberSpriteInTheChosenColour()
    {
        var original = Resources.Load<Sprite>(Livery);
        Assert.IsNotNull(original, $"Resources/{Livery} missing");
        Assert.IsNotNull(Resources.Load<Texture2D>("cup20num89"), "Resources/cup20num89 missing");

        var factory = Render(original, null);
        Assert.AreNotSame(original, factory, "the #89 has layers, so even factory paint is composed");
        AssertNear(new Color32(255, 255, 255, 255), Pixel(factory, kDigitOnLivery), "factory number is white");

        var red = new Color32(200, 30, 35, 255);
        var redNumber = Render(original, new PaintScheme { factory = true, numberColour = red });
        AssertNear(red, Pixel(redNumber, kDigitOnLivery), "number takes the chosen colour over factory paint");

        var repaint = Render(original, new PaintScheme { baseColour = new Color32(24, 24, 28, 255), numberColour = red });
        AssertNear(red, Pixel(repaint, kDigitOnLivery), "and over a repaint");
        AssertNear(new Color32(24, 24, 28, 255), Pixel(repaint, new Vector2Int(30, 6)), "the roof around it is the base coat");
    }

    // ------------------------------------------------------------------ helpers

    static Color32 Pixel(Sprite sprite, Vector2Int at)
    {
        var r = sprite.textureRect;
        return sprite.texture.GetPixel((int)r.x + at.x, (int)r.y + at.y);
    }

    static void AssertNear(Color32 expected, Color32 actual, string message)
    {
        bool near = Mathf.Abs(expected.r - actual.r) <= 2 && Mathf.Abs(expected.g - actual.g) <= 2
                 && Mathf.Abs(expected.b - actual.b) <= 2 && actual.a == 255;
        Assert.IsTrue(near, $"{message}: expected {expected}, got {actual}");
    }

    static Sprite Render(Sprite original, PaintScheme scheme) =>
        (Sprite)RuntimeType("PaintShop").GetMethod("Render").Invoke(null, new object[] { original, scheme });

    static System.Type RuntimeType(string name)
    {
        var type = System.AppDomain.CurrentDomain.GetAssemblies()
                         .Select(a => a.GetType(name, false))
                         .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, $"{name} not found");
        return type;
    }
}
