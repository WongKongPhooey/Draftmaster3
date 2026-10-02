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
        StringAssert.Contains("PitBoxCars.Car", source,
                              "The crew have to find the car assigned to their box before they can wear it.");
        StringAssert.Contains("CarColours.For", source,
                              "The colours are the car's, from CarColours — the same table the pit box stand reads.");
        StringAssert.Contains("WearTeamColours", source, "...and then the crew have to actually be dressed in them.");
    }

    [Test]
    public void The_players_box_is_found_by_the_human_car_not_by_the_first_controller_in_the_scene()
    {
        // The dynamic AI drive a PlayerVehicleController too, so "the first one Unity lists" is usually an AI
        // car — and the player's crew wore its colours.
        string source = Source("Scripts/AI/PitBoxCars.cs");
        StringAssert.DoesNotContain("FindFirstObjectByType<PlayerVehicleController>", source);
        StringAssert.Contains("PlayerVehicleController.Human", source);
    }

    [Test]
    public void The_player_crew_off_pit_road_wear_the_players_car()
    {
        // The briefing is a meeting: no field is out, so there is no player pit box to ask about. The crew
        // round the chief, and the chief himself, read the car.
        string huddle = Source("Scripts/Weekend/Venues/BriefingHuddle.cs");
        StringAssert.DoesNotContain("PitBoxCars.Label(PitLane.PlayerBox)", huddle);
        StringAssert.Contains("CarColours.For(playerCar", huddle);

        string chief = Source("Scripts/Weekend/Venues/CrewChiefPresence.cs");
        StringAssert.Contains("WearTeamColours", chief, "The crew chief is crew: he wears the car too.");
    }

    [Test]
    public void A_carset_is_read_off_a_livery_name()
    {
        var read = Runtime("CarIdentity").GetMethod("CarsetFromSpriteName");
        Assert.AreEqual("cup26", read.Invoke(null, new object[] { "cup26livery8" }));
        Assert.AreEqual("cts25", read.Invoke(null, new object[] { "cts25livery21alt1" }));
        Assert.IsNull(read.Invoke(null, new object[] { "PlayerCarSprite" }));
        Assert.IsNull(read.Invoke(null, new object[] { "livery8" }), "No carset in front of the token.");
    }

    [Test]
    public void A_cars_colours_come_from_the_paint_it_is_wearing_before_its_label()
    {
        // The player's label is filled from GridSpawner's default carset, not from what they drive. The
        // paintwork is what the player sees, so that is what the crew wear.
        Type coloursType = Runtime("CarColours");
        Type entryType = coloursType.GetNestedType("Entry");
        var table = ScriptableObject.CreateInstance(coloursType);
        var entries = (System.Collections.IList)coloursType.GetField("entries").GetValue(table);
        entries.Add(Entry(entryType, "zz99", 7, Primary, Secondary));
        entries.Add(Entry(entryType, "cup26", 3, Color.green, Color.yellow));

        var car = new GameObject("Car");
        var tex = Sheet("paint");
        var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
        sprite.name = "zz99livery7";
        try
        {
            car.AddComponent<SpriteRenderer>().sprite = sprite;
            Type labelType = Runtime("DriverLabel");
            var label = car.AddComponent(labelType);
            labelType.GetField("carset").SetValue(label, "cup26");
            labelType.GetField("carNumber").SetValue(label, 3);

            SetTable(coloursType, table);
            var forCar = coloursType.GetMethod("For", new[] { typeof(GameObject), typeof(Color).MakeByRefType(), typeof(Color).MakeByRefType() });
            Assert.IsNotNull(forCar, "CarColours.For(GameObject, out, out) reads a car's colours off its paint.");
            var args = new object[] { car, null, null };
            forCar.Invoke(null, args);
            Assert.AreEqual(Primary, (Color)args[1], "The paint says zz99 #7; the label's cup26 #3 is stale.");
            Assert.AreEqual(Secondary, (Color)args[2]);

            // No livery on the car: the label is all there is to go on.
            car.GetComponent<SpriteRenderer>().sprite = null;
            args = new object[] { car, null, null };
            forCar.Invoke(null, args);
            Assert.AreEqual(Color.green, (Color)args[1]);
        }
        finally
        {
            coloursType.GetMethod("Forget").Invoke(null, null);
            UnityEngine.Object.DestroyImmediate(car);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(table);
        }
    }

    static object Entry(Type entryType, string carset, int number, Color primary, Color secondary)
    {
        var e = Activator.CreateInstance(entryType);
        entryType.GetField("carset").SetValue(e, carset);
        entryType.GetField("carNumber").SetValue(e, number);
        entryType.GetField("primary").SetValue(e, primary);
        entryType.GetField("secondary").SetValue(e, secondary);
        return e;
    }

    // CarColours loads its table from Resources; point it at a test table instead.
    static void SetTable(Type coloursType, ScriptableObject table)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var instance = coloursType.GetField("_instance", flags);
        var looked = coloursType.GetField("_looked", flags);
        Assert.IsNotNull(instance, "CarColours._instance has been renamed; this test is written against it.");
        Assert.IsNotNull(looked, "CarColours._looked has been renamed; this test is written against it.");
        instance.SetValue(null, table);
        looked.SetValue(null, true);
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
