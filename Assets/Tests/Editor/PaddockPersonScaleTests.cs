using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

// How big a person is.
//
// Every body in this game is drawn to one figure: an 8px walk frame at the project's 12.8 px/m, so 0.625m.
// The world is metric for cars but the people are deliberately drawn smaller than 1:1, and the paper-doll
// layers are built through NPCPartLibrary.pixelsPerUnit rather than the PNG's import setting — so the only
// way to get the size right is to normalise to that one standard, and the only way to get it wrong is to
// type a real human height into a spawn call.
//
// That is exactly what had happened at the weekend venues: the motorhome engineer, the hospitality rep and
// every driver sat in the drivers' room were spawned at 1.45–1.75m, which at this project's standard is
// nearly three times the size of the player stood next to them. These tests pin the standard, measure a
// body actually built from it, and fail if anybody hand-sets a metric height in the venue builder again.
//
// Read through reflection because this assembly can't reference Assembly-CSharp, where the venue runtime
// lives — same approach as TitleCrashTests and TitleScreenWiringTests.
public class PaddockPersonScaleTests
{
    const string VenueSitesSource = "Scripts/Weekend/Venues/WeekendVenueSites.cs";

    static System.Type Runtime(string name)
    {
        var type = System.Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    static float Constant(System.Type type, string field)
    {
        var info = type.GetField(field, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        Assert.IsNotNull(info, $"{type.Name}.{field} is gone; this test is written against it.");
        return (float)info.GetValue(null);
    }

    static float StandingHeight => Constant(Runtime("PaddockPerson"), "HeightM");
    static float SeatedHeight => Constant(Runtime("PaddockPerson"), "SeatedHeightM");
    static float OnFootStandard => Constant(Runtime("PitCrewSpawner"), "OnFootPersonHeight");

    // ------------------------------------------------------------------ the standard

    [Test]
    public void AVenueBodyIsTheSameSizeAsEveryoneElseOnFoot()
    {
        Assert.AreEqual(OnFootStandard, StandingHeight, 1e-5f,
                        "A person stood in a venue has to be the size of the player stood next to them. " +
                        "Take the height from PitCrewSpawner.OnFootPersonHeight, never a real human one.");
    }

    [Test]
    public void TheStandardIsTheProjectsPixelGrid()
    {
        // 8px of walk frame at 12.8 px/m. Restated here rather than read off the same constant, so a
        // change to the pixel standard has to be a deliberate one and not a silent drift.
        Assert.AreEqual(0.625f, StandingHeight, 1e-4f,
                        "The on-foot figure is an 8px frame at PixelArt.PixelsPerMetre (12.8), i.e. 0.625m.");
    }

    [Test]
    public void ASeatedBodyIsShorterThanAStandingOneButStillTheSamePerson()
    {
        Assert.Less(SeatedHeight, StandingHeight, "Sitting down should not make somebody taller.");
        Assert.Greater(SeatedHeight, StandingHeight * 0.7f,
                       "A seated driver is folded up, not a different species — anything under about " +
                       "three-quarters of standing reads as a child in the chair.");
    }

    // ------------------------------------------------------------------ a body actually built

    [Test]
    public void ASpawnedBodyMeasuresAPersonInTheWorld()
    {
        AssertSpawnedHeight(StandingHeight);
    }

    [Test]
    public void ASpawnedSeatedBodyMeasuresItsSeatedHeight()
    {
        AssertSpawnedHeight(SeatedHeight);
    }

    // Build one for real and measure what the camera would see. This is the check that survives a change
    // to the part library's frame size or pixels-per-unit: whatever those are, the figure comes out at the
    // height it was asked for.
    static void AssertSpawnedHeight(float expected)
    {
        var type = Runtime("PaddockPerson");
        var spawn = type.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(spawn, "PaddockPerson.Spawn is gone; this test builds a body through it.");

        var body = (GameObject)spawn.Invoke(null, new object[]
        {
            null, Vector3.zero, "PaddockPersonUnderTest", 4242, expected, null,
        });
        Assert.IsNotNull(body, "PaddockPerson.Spawn returned nothing.");

        try
        {
            var renderers = body.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.Greater(renderers.Length, 0,
                           "The body has nothing to draw — neither a paper-doll layer nor the fallback blob.");

            // Measured off the sprite's own rect rather than renderer.bounds: a tight-meshed sprite reports
            // only the pixels it drew, and a hat with three transparent rows would read as a short person.
            float tallest = 0f;
            foreach (var sr in renderers)
            {
                if (sr == null || sr.sprite == null) continue;
                float ppu = Mathf.Max(0.0001f, sr.sprite.pixelsPerUnit);
                tallest = Mathf.Max(tallest, sr.sprite.rect.height / ppu * Mathf.Abs(sr.transform.lossyScale.y));
            }

            Assert.AreEqual(expected, tallest, expected * 0.02f,
                            $"A body asked for {expected:0.###}m came out {tallest:0.###}m tall. " +
                            "The height has to be normalised through the part library's own frame size.");
        }
        finally
        {
            Object.DestroyImmediate(body);
        }
    }

    // ------------------------------------------------------------------ the way it went wrong

    [Test]
    public void NoVenueHandSetsAPersonsHeightInMetres()
    {
        string path = Path.Combine(Application.dataPath, VenueSitesSource);
        Assert.IsTrue(File.Exists(path), $"{VenueSitesSource} has moved; this test reads it as source.");

        var offenders = new List<string>();
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"heightM\s*:\s*(-?[0-9]*\.?[0-9]+f?)");
            if (match.Success) offenders.Add($"line {i + 1}: heightM: {match.Groups[1].Value}");
        }

        Assert.IsEmpty(offenders,
                       "A person's height was typed in as a number instead of taken from the standard:\n  " +
                       string.Join("\n  ", offenders) +
                       "\nUse PaddockPerson.HeightM or PaddockPerson.SeatedHeightM — a literal here is how " +
                       "the drivers' room ended up full of giants.");
    }
}
