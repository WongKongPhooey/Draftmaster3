using System;
using System.Collections.Generic;
using System.Reflection;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// The Master Draft pack opening screen (MasterDraftUI.PackOpening.cs) built in the editor against a test profile
// and a hand-made pack: no session, no backend, no scene — MasterDraftUI.BuildOfflinePack, with the animation
// clock moved on by AdvancePack. Checks the sealed screen (pack, stripes, blinking prompt, bob), that the cards are
// dealt face down and turn over one at a time, and that confirm skips to the opened pack.
//
// Not named MasterDraft*Tests: those compile into Tools/HeadlessTests, and this needs Unity. MasterDraftUI lives in
// Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection (as SeriesScreenTests is).
public class PackOpeningScreenTests
{
    static readonly Type UiType = Type.GetType("MasterDraftUI, Assembly-CSharp", throwOnError: true);

    GameObject _go;
    Component _ui;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("PackOpeningScreenTest");
        _ui = _go.AddComponent(UiType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    object Call(string method, params object[] args) =>
        UiType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(_ui, args);

    T Get<T>(string property) =>
        (T)UiType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(_ui);

    string Stage => Get<string>("PackStageName");
    int FaceUp => Get<int>("PackCardsFaceUp");
    int Crunches => Get<int>("PackCrunches");
    Transform Layer => _go.transform.Find("Canvas/PackOpening");

    Transform Find(string name)
    {
        foreach (var t in Layer.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // Six cards, the third an Epic.
    static List<CardInstance> TestPack()
    {
        var cards = new List<CardInstance>();
        for (int i = 0; i < 6; i++)
        {
            var def = new CardDefinition
            {
                id = "DRV-" + i, type = CardType.Driver, name = "Driver " + i, overall = 60 + i,
                rarity = i == 2 ? Rarity.Epic : Rarity.Common,
            };
            cards.Add(new CardInstance("uid" + i, def));
        }
        return cards;
    }

    static MasterDraftProfile TestProfile() => new MasterDraftProfile
    {
        schemaVersion = MasterDraftProfile.CurrentSchema,
        playerId = "pack-test",
        coins = 5000,
    };

    void Open() => Call("BuildOfflinePack", TestProfile(), TestPack(), PackKind.Standard);

    [Test]
    public void Sealed_PackFloatsOverItsShadowAndThePromptBlinks()
    {
        Open();
        Assert.AreEqual("Sealed", Stage);
        Assert.IsNotNull(Layer, "the pack screen covers the menu");
        Assert.IsNotNull(Find("Stripes"), "striped stage");
        Assert.IsNotNull(Find("Header"), "Open Packs header bar");
        var pack = (RectTransform)Find("Pack");
        var shadow = (RectTransform)Find("PackShadow");
        var prompt = Find("Prompt").GetComponent<CanvasGroup>();
        Assert.IsNotNull(pack);
        Assert.IsNotNull(shadow);
        Assert.IsNull(Find("PackCard0"), "no cards until the pack is opened");

        float restY = pack.anchoredPosition.y;
        float shadowY = shadow.anchoredPosition.y;
        Assert.AreEqual(1f, prompt.alpha, "prompt on");

        Call("AdvancePack", 0.65f);
        Assert.AreEqual(restY + 4f, pack.anchoredPosition.y, 0.01f, "bob: 4 px up for the second half of the cycle");
        Assert.AreEqual(shadowY, shadow.anchoredPosition.y, 0.01f, "the shadow stays put");
        Assert.AreEqual(0f, prompt.alpha, "prompt off for the second half of the blink");

        Call("AdvancePack", 0.6f);
        Assert.AreEqual(restY, pack.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1f, prompt.alpha);
        Assert.AreEqual("Sealed", Stage, "the pack waits for the player");
    }

    [Test]
    public void Opened_CardsAreDealtFaceDownThenFlipOneAtATime()
    {
        Open();
        Call("ConfirmPack");
        Assert.AreEqual("Opening", Stage);

        // Through the tear and into the deal.
        for (int i = 0; i < 20 && Stage == "Opening"; i++) Call("AdvancePack", 0.05f);
        Assert.AreEqual("Dealing", Stage);
        Assert.AreEqual(0, FaceUp);
        for (int i = 0; i < 6; i++)
        {
            var card = Find("PackCard" + i);
            Assert.IsNotNull(card, $"card {i} dealt");
            Assert.IsTrue(card.Find("Back").gameObject.activeSelf, $"card {i} shows its back");
            Assert.IsFalse(card.Find("Card").gameObject.activeSelf, $"card {i} face hidden");
        }

        var flipped = new bool[6];
        int last = 0;
        bool sawEpicCallout = false;
        for (int step = 0; step < 400 && Stage != "Revealed"; step++)
        {
            Call("AdvancePack", 0.02f);
            if (Stage == "Revealed") break;
            int up = FaceUp;
            Assert.AreEqual(up, Crunches, "a crunch as each card's face turns up, the title logo's sound");
            Assert.GreaterOrEqual(up, last, "cards never turn back over");
            Assert.LessOrEqual(up - last, 1, "one card at a time");
            last = up;

            int turning = 0;
            for (int i = 0; i < 6; i++)
            {
                var slot = Find("PackCard" + i);
                if (slot.localScale.x < 0.999f) { turning++; flipped[i] = true; }
                bool faceShown = slot.Find("Card").gameObject.activeSelf;
                Assert.AreNotEqual(faceShown, slot.Find("Back").gameObject.activeSelf, $"card {i}: back or face, never both");
                if (i >= up + 1) Assert.IsFalse(faceShown, $"card {i} still face down while card {up} is next");
            }
            Assert.LessOrEqual(turning, 1, "only one card mid-flip");
            if (Find("Caption").GetComponent<TMPro.TMP_Text>().text.Contains("Epic")) sawEpicCallout = true;
        }

        Assert.AreEqual("Revealed", Stage);
        Assert.AreEqual(6, FaceUp);
        Assert.AreEqual(6, Crunches, "one crunch per card, no more");
        for (int i = 0; i < 6; i++) Assert.IsTrue(flipped[i], $"card {i} turned over (scaled about its centre)");
        Assert.IsTrue(sawEpicCallout, "the Epic gets a call-out");
        Assert.IsNotNull(Find("Button_pack.done"), "Done once the pack is open");
        Assert.IsNotNull(Find("Button_pack.again"), "a bought pack offers another");
    }

    [Test]
    public void Confirm_SkipsTheAnimationToTheOpenedPack()
    {
        Open();
        Call("ConfirmPack");                 // open
        Call("AdvancePack", 0.1f);
        Call("ConfirmPack");                 // skip
        Assert.AreEqual("Revealed", Stage);
        Assert.AreEqual(6, FaceUp);
        Assert.AreEqual(0, Crunches, "a skip turns the cards over silently");
        for (int i = 0; i < 6; i++)
        {
            var slot = Find("PackCard" + i);
            Assert.AreEqual(1f, slot.localScale.x, 0.001f);
            Assert.IsTrue(slot.Find("Card").gameObject.activeSelf);
        }
    }

    // ------------------------------------------------------------------ shiny (alt paint) cars

    static readonly Type ShineType = Type.GetType("MasterDraftCardShine, Assembly-CSharp", throwOnError: true);

    // The test pack with card 4 swapped for a car in the #21 alt paint (a livery that ships in Resources).
    static List<CardInstance> ShinyPack()
    {
        var cards = TestPack();
        cards[4] = new CardInstance("uid4", new CardDefinition
        {
            id = "CAR-CHV-1", type = CardType.Car, name = "Chevrolet Camaro Pro", overall = 74, rarity = Rarity.Rare,
            power = 78, handling = 73, draft = 73, reliability = 78,
            paint = "cup26_21_alt1", paintLivery = "cup26livery21blankalt1", paintName = "No. 21 alt paint",
        });
        return cards;
    }

    [Test]
    public void Shiny_AnAltPaintCarGlittersAndGetsACallout()
    {
        Call("BuildOfflinePack", TestProfile(), ShinyPack(), PackKind.Standard);
        Call("ConfirmPack");
        bool sawCallout = false;
        for (int step = 0; step < 600 && Stage != "Revealed"; step++)
        {
            Call("AdvancePack", 0.02f);
            var caption = Find("Caption");   // none until the cards are dealt
            if (caption != null && caption.GetComponent<TMPro.TMP_Text>().text == "Shiny Alt Paint!") sawCallout = true;
        }
        Assert.AreEqual("Revealed", Stage);
        Assert.IsTrue(sawCallout, "the shiny car gets a call-out as it turns over");
        Assert.AreEqual("Shiny Alt Paint!", Find("Caption").GetComponent<TMPro.TMP_Text>().text, "and over the opened pack");

        // Only the alt paint card shines.
        for (int i = 0; i < 6; i++)
        {
            var face = Find("PackCard" + i).Find("Card");
            Assert.AreEqual(i == 4, face.GetComponent(ShineType) != null, $"card {i}");
        }

        var shiny = Find("PackCard4").Find("Card");
        var shine = shiny.GetComponent(ShineType);
        Assert.IsNotNull(shiny.Find("Inner/Livery"), "the alt paint is shown on the card");
        Assert.AreEqual("ALT", shiny.Find("Inner/Type").GetComponent<TMPro.TMP_Text>().text);
        Assert.IsNotNull(shiny.Find("Inner/Sheen"), "a sheen layer");
        Assert.IsNotNull(shiny.Find("Inner/Sparkle0"), "glitter");

        // Run its clock: the frame steps through the holo colours, the sheen sweeps, the glitter twinkles.
        var tick = ShineType.GetMethod("Tick");
        var frameColour = ShineType.GetProperty("FrameColour");
        var sheenShowing = ShineType.GetProperty("SheenShowing");
        var sparklesShowing = ShineType.GetProperty("SparklesShowing");
        var colours = new HashSet<Color>();
        bool sheenOn = false, sheenOff = false;
        int maxSparkles = 0;
        for (int i = 0; i < 300; i++)
        {
            tick.Invoke(shine, new object[] { 0.02f });
            colours.Add((Color)frameColour.GetValue(shine));
            if ((bool)sheenShowing.GetValue(shine)) sheenOn = true; else sheenOff = true;
            maxSparkles = Math.Max(maxSparkles, (int)sparklesShowing.GetValue(shine));
        }
        Assert.GreaterOrEqual(colours.Count, 4, "holo frame cycles");
        Assert.IsTrue(sheenOn && sheenOff, "the sheen comes and goes");
        Assert.Greater(maxSparkles, 0, "glitter twinkles");
    }
}
