using System;
using System.Reflection;
using Draftmaster.Controls;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Every button in the game is one height with one caption, taken from the pause menu (UIButton). The IMGUI
// panels get it from PixelGUI.MenuButtonH and ButtonStyle, the canvases from IronOvalUI — these pin that the
// two agree, so a new screen built through either comes out the same.
//
// PixelGUI and IronOvalUI live in Assembly-CSharp, which a test assembly cannot reference, so they are
// reached by reflection.
public class ButtonStandardTests
{
    static Type Kit(string name) => Type.GetType(name + ", Assembly-CSharp", throwOnError: true);
    static object Get(string type, string member) =>
        Kit(type).GetProperty(member, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    static object Call(string type, string method, Type[] signature, params object[] args) =>
        Kit(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, signature, null).Invoke(null, args);

    static bool Handheld => (bool)Get("PixelGUI", "Handheld");

    static Button KitButton(Transform parent, string caption, Vector2 size) => (Button)Call("IronOvalUI", "Button",
        new[] { typeof(Transform), typeof(string), typeof(string), typeof(Vector2) }, parent, "B", caption, size);

    static Button KitTab(Transform parent, string caption, Vector2 size) => (Button)Call("IronOvalUI", "TabButton",
        new[] { typeof(Transform), typeof(string), typeof(string), typeof(Vector2), typeof(bool) }, parent, "T", caption, size, false);

    [Test]
    public void TheStandardIsTwentySixKitPixels()
    {
        Assert.AreEqual(26, UIButton.KitPx);
        Assert.AreEqual(26f, UIButton.CanvasPx(360f));
        Assert.AreEqual(78f, UIButton.CanvasPx(1080f));
        Assert.AreEqual(78f, UIButton.ScreenPx(1080f));
        Assert.AreEqual(48f, UIButton.CaptionScreenPx(1080f), "caption is the Data size: 16 kit px");
    }

    // The pause menu is the reference; its plate is MenuButtonH, which on a desktop is UIButton at the
    // kit's scale.
    [Test]
    public void ImguiButtonsAreTheStandardHeight()
    {
        if (Handheld) Assert.Ignore("handheld plates grow to fit the bigger face");
        Assert.AreEqual(UIButton.KitPx * (int)Get("PixelGUI", "Scale"), (float)Get("PixelGUI", "MenuButtonH"), 0.01f);
    }

    [Test]
    public void CanvasButtonsAreTheStandardHeightWhateverTheyAskFor()
    {
        var root = new GameObject("Root", typeof(RectTransform));
        try
        {
            var button = KitButton(root.transform, "GO", new Vector2(60f, 33f));
            Assert.AreEqual(UIButton.KitPx, ((RectTransform)button.transform).sizeDelta.y);
            Assert.AreEqual(60f, ((RectTransform)button.transform).sizeDelta.x, "width stays the caller's");

            var tab = KitTab(root.transform, "Back", new Vector2(56f, 12f));
            Assert.AreEqual(UIButton.KitPx, ((RectTransform)tab.transform.parent).sizeDelta.y);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void CanvasCaptionsMatchThePauseMenu()
    {
        var root = new GameObject("Root", typeof(RectTransform));
        try
        {
            var a = KitButton(root.transform, "Go", new Vector2(60f, 20f)).GetComponentInChildren<TMP_Text>();
            var b = KitTab(root.transform, "Back", new Vector2(56f, 20f)).GetComponentInChildren<TMP_Text>();
            var theme = Kit("PixelUITheme").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            foreach (var label in new[] { a, b })
            {
                Assert.AreEqual(UIType.KitPx(UIButton.CaptionRole), label.fontSize, label.name);
                Assert.AreEqual(0f, label.characterSpacing, "no extra tracking, as IMGUI");
                Assert.AreEqual(FontStyles.Normal, label.fontStyle);
                if (theme != null) Assert.AreSame(theme.GetType().GetField("data").GetValue(theme), label.font);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // A button authored in a prefab is brought onto the standard by its binder.
    [Test]
    public void StandardiseButtonFixesAnAuthoredOne()
    {
        if (Handheld) Assert.Ignore("desktop sizes");
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 52f);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(go.transform, false);
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.cyan;

            Call("IronOvalUI", "StandardiseButton", new[] { typeof(Button), typeof(float) }, go.GetComponent<Button>(), 0f);

            Assert.AreEqual(78f, ((RectTransform)go.transform).sizeDelta.y);
            Assert.AreEqual(200f, ((RectTransform)go.transform).sizeDelta.x);
            Assert.AreEqual(48, text.fontSize);
            Assert.AreEqual(FontStyle.Normal, text.fontStyle);
            Assert.AreEqual(Color.cyan, text.color, "colour carries state and is kept");
            Assert.AreSame(Get("BrandFonts", "Body"), text.font);
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }
}
