using System;
using System.IO;
using System.Reflection;
using Draftmaster.Crowd;
using NUnit.Framework;
using UnityEngine;

// The pit crew wear the car.
//
// Five people over the wall in five different sets of clothes read as five bystanders. On a real pit road
// the crew are the loudest statement of whose box you are looking at, because they are in the car's paint —
// so the crew of a box take the same two colours (CarColours' primary and secondary) that the pit box stand
// behind them is painted in.
//
// These tests pin the two halves of that: the rule for which worn layer takes which colour (TeamUniform),
// and that a built paper-doll outfit actually repaints when it is handed a team's colours. The crew runtime
// itself lives in Assembly-CSharp, which this assembly cannot reference, so it is reached by reflection —
// the same approach as PaddockPersonScaleTests.
public class PitCrewUniformTests
{
    static readonly Color Primary = new(0.90f, 0.15f, 0.10f);
    static readonly Color Secondary = new(0.05f, 0.25f, 0.85f);

    static Type Runtime(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is missing from Assembly-CSharp.");
        return type;
    }

    // ---- the rule ---------------------------------------------------------------------------------

    [Test]
    public void The_uniform_puts_the_cars_two_colours_on_the_clothes()
    {
        Assert.IsTrue(TeamUniform.TryColour(TeamUniform.Top, Primary, Secondary, out Color top));
        Assert.AreEqual(Primary, top, "The top is the car's primary — the colour you read from the stand.");

        Assert.IsTrue(TeamUniform.TryColour(TeamUniform.Bottoms, Primary, Secondary, out Color bottoms));
        Assert.AreEqual(Secondary, bottoms, "The bottoms carry the secondary, so the kit is two-tone like the car.");

        Assert.IsTrue(TeamUniform.TryColour(TeamUniform.Hat, Primary, Secondary, out Color hat));
        Assert.AreEqual(Primary, hat, "The cap matches the top.");
    }

    [Test]
    public void The_uniform_leaves_the_person_alone()
    {
        foreach (string notClothes in new[] { "Base", "Hair", "Shoes", "", "Sunglasses" })
            Assert.IsFalse(TeamUniform.TryColour(notClothes, Primary, Secondary, out _),
                           $"'{notClothes}' is not team kit — recolouring it would repaint the person, not the uniform.");
    }

    [Test]
    public void The_uniform_does_not_care_how_a_library_capitalises_its_layers()
    {
        Assert.IsTrue(TeamUniform.TryColour("top", Primary, Secondary, out Color top));
        Assert.AreEqual(Primary, top);
    }

    // ---- the outfit -------------------------------------------------------------------------------

    [Test]
    public void A_built_outfit_repaints_into_team_colours()
    {
        var library = BuildLibrary("Base", "Bottoms", "Top", "Hat");
        var go = new GameObject("CrewMember");
        try
        {
            Type appearanceType = Runtime("NPCLayeredAppearance");
            var appearance = go.AddComponent(appearanceType);
            appearanceType.GetField("library").SetValue(appearance, library);

            bool built = (bool)appearanceType.GetMethod("Build").Invoke(appearance, new object[] { (int?)7 });
            Assert.IsTrue(built, "The outfit did not build, so there is nothing to dress.");
            Assert.AreEqual(Color.white, LayerColour(go, "Top"), "An untinted layer should start as drawn.");

            int changed = (int)appearanceType.GetMethod("WearTeamColours")
                                             .Invoke(appearance, new object[] { Primary, Secondary });

            Assert.AreEqual(3, changed, "Top, bottoms and hat are the kit — three layers should have taken a colour.");
            Assert.AreEqual(Primary, LayerColour(go, "Top"));
            Assert.AreEqual(Secondary, LayerColour(go, "Bottoms"));
            Assert.AreEqual(Primary, LayerColour(go, "Hat"));
            Assert.AreEqual(Color.white, LayerColour(go, "Base"), "The body is not part of the uniform.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(library);
            NPCSpriteCache.Clear();
        }
    }

    [Test]
    public void An_outfit_with_no_kit_layers_reports_that_it_could_not_be_dressed()
    {
        var library = BuildLibrary("Base", "Hair");
        var go = new GameObject("Bystander");
        try
        {
            Type appearanceType = Runtime("NPCLayeredAppearance");
            var appearance = go.AddComponent(appearanceType);
            appearanceType.GetField("library").SetValue(appearance, library);
            appearanceType.GetMethod("Build").Invoke(appearance, new object[] { (int?)7 });

            int changed = (int)appearanceType.GetMethod("WearTeamColours")
                                             .Invoke(appearance, new object[] { Primary, Secondary });

            Assert.AreEqual(0, changed, "Nothing worn is team kit, so the caller must be able to fall back.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(library);
            NPCSpriteCache.Clear();
        }
    }

    // ---- the wiring -------------------------------------------------------------------------------

    [Test]
    public void A_crew_member_can_be_told_what_to_wear()
    {
        var member = Runtime("PitCrewMember").GetMethod("WearTeamColours", new[] { typeof(Color), typeof(Color) });
        Assert.IsNotNull(member, "PitCrewMember.WearTeamColours(primary, secondary) is how a crew is painted.");
    }

    [Test]
    public void A_crew_box_paints_itself_from_the_car_in_it()
    {
        string source = Source("Scripts/AI/PitCrew.cs");
        StringAssert.Contains("PitBoxCars.Label", source,
                              "The crew have to find the car assigned to their box before they can wear it.");
        StringAssert.Contains("CarColours.For", source,
                              "The colours are the car's, from CarColours — the same table the pit box stand reads.");
        StringAssert.Contains("WearTeamColours", source, "...and then the crew have to actually be dressed in them.");
    }

    static string Source(string relative)
    {
        string path = Path.Combine(Application.dataPath, relative);
        Assert.IsTrue(File.Exists(path), $"{relative} has moved; this test is written against it.");
        return File.ReadAllText(path);
    }

    static Color LayerColour(GameObject root, string layerName)
    {
        var layer = root.transform.Find(layerName);
        Assert.IsNotNull(layer, $"The outfit has no '{layerName}' layer.");
        var sr = layer.GetComponent<SpriteRenderer>();
        Assert.IsNotNull(sr, $"The '{layerName}' layer has no renderer.");
        return sr.color;
    }

    // A throwaway part library: one plain white 8x8 sheet per named category, which is all the outfit
    // builder needs to produce one renderer per layer.
    static ScriptableObject BuildLibrary(params string[] categoryNames)
    {
        Type libraryType = Runtime("NPCPartLibrary");
        var library = ScriptableObject.CreateInstance(libraryType);
        libraryType.GetField("frameWidth").SetValue(library, 8);
        libraryType.GetField("frameHeight").SetValue(library, 8);
        libraryType.GetField("pixelsPerUnit").SetValue(library, 100f);
        libraryType.GetField("pivot").SetValue(library, new Vector2(0.5f, 0.5f));

        Type categoryType = libraryType.GetNestedType("PartCategory");
        Assert.IsNotNull(categoryType, "NPCPartLibrary.PartCategory is gone; this test is written against it.");

        var categories = Array.CreateInstance(categoryType, categoryNames.Length);
        for (int i = 0; i < categoryNames.Length; i++)
        {
            var category = Activator.CreateInstance(categoryType);
            categoryType.GetField("name").SetValue(category, categoryNames[i]);
            categoryType.GetField("optional").SetValue(category, false);
            categoryType.GetField("options").SetValue(category, new[] { Sheet(categoryNames[i]) });
            categories.SetValue(category, i);
        }
        libraryType.GetField("categories").SetValue(library, categories);
        return library;
    }

    static Texture2D Sheet(string name)
    {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[8 * 8];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
