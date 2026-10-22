using System.Linq;
using Draftmaster.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Pins the shared type scale (Draftmaster.Controls.UIType) and the satnav screen that prompted it: its
// side panel set the part list at 16px on a 1080-tall canvas, a third of the size the race panels draw
// the same kind of row at, and nothing said that was wrong.
public class TypeScaleTests
{
    const string TravelMapPath = "Assets/Resources/UI/TravelMap.prefab";

    [Test]
    public void Label_is_the_floor_and_nothing_is_under_it()
    {
        foreach (TypeRole role in System.Enum.GetValues(typeof(TypeRole)))
            Assert.GreaterOrEqual(UIType.KitPx(role), UIType.MinimumKitPx, role.ToString());
        Assert.AreEqual(UIType.MinimumKitPx, UIType.KitPx(TypeRole.Label));
        Assert.Greater(UIType.KitPx(TypeRole.Title), UIType.KitPx(TypeRole.Heading));
        Assert.AreEqual(24f, UIType.MinimumScreenPx(1080f));
    }

    [Test]
    public void Reading_text_is_one_size()
    {
        // Body, dialogue and data all read at the objective strip's size (HandheldType): one cell of the
        // data face. A blurb that comes out smaller than the list beside it is the drift this prevents.
        int data = UIType.KitPx(TypeRole.Data);
        Assert.AreEqual(data, UIType.KitPx(TypeRole.Body));
        Assert.AreEqual(data, UIType.KitPx(TypeRole.Dialogue));
        Assert.AreEqual(data, UIType.KitPx(TypeRole.Heading));
    }

    [Test]
    public void Kit_canvas_sizes_match_what_IronOvalUI_and_PixelGUI_were_authored_at()
    {
        // 640x360 canvas, sizes straight through, snapped onto each face.
        Assert.AreEqual(16, UIType.CanvasPx(TypeRole.Heading, 360f, 8));  // Silkscreen header
        Assert.AreEqual(8, UIType.CanvasPx(TypeRole.Label, 360f, 8));     // Silkscreen small header
        Assert.AreEqual(16, UIType.CanvasPx(TypeRole.Data, 360f, 16));    // VT323 / fixedsys
        Assert.AreEqual(20, UIType.CanvasPx(TypeRole.Body, 360f, 20));    // Pixelify's 20 grid
    }

    [Test]
    public void A_1080_canvas_is_three_times_the_kit()
    {
        Assert.AreEqual(48, UIType.CanvasPx(TypeRole.Data, 1080f, 16));
        Assert.AreEqual(48, UIType.CanvasPx(TypeRole.Heading, 1080f, 8));
        Assert.AreEqual(72, UIType.CanvasPx(TypeRole.Title, 1080f, 8));
        Assert.AreEqual(24, UIType.CanvasPx(TypeRole.Label, 1080f, 8));
        Assert.AreEqual(40, UIType.CanvasPx(TypeRole.Body, 1080f, 20));   // 48 onto Pixelify's grid
    }

    [Test]
    public void Snapping_never_drops_under_the_floor()
    {
        // A 24px label in a 20-cell face rounds to 20 — under the floor — so it goes up a step instead.
        Assert.AreEqual(40, UIType.CanvasPx(TypeRole.Label, 1080f, 20));
        Assert.AreEqual(16, UIType.Snap(3f, 16));
        Assert.AreEqual(25, UIType.Snap(24.4f, 0, 25f));
        Assert.IsTrue(UIType.Readable(24f, 1080f));
        Assert.IsFalse(UIType.Readable(16f, 1080f));
        Assert.IsTrue(UIType.Readable(8f, 360f));
    }

    // The satnav: every text on the screen but the map's own node names (which zoom with the board) clears
    // the floor, and sits on its face's pixel cell so the bitmap font stays sharp.
    [Test]
    public void Satnav_text_is_on_the_scale()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(TravelMapPath);
        Assert.IsNotNull(root, TravelMapPath);
        var scaler = root.GetComponent<CanvasScaler>();
        Assert.AreEqual(1080f, scaler.referenceResolution.y, "sizes below assume the 1080-tall canvas");

        var nodes = root.transform.Find("MapPlot/Nodes");
        var texts = root.GetComponentsInChildren<Text>(true)
                        .Where(t => nodes == null || !t.transform.IsChildOf(nodes))
                        .ToList();
        Assert.Greater(texts.Count, 4);

        foreach (var t in texts)
        {
            string where = Path(t.transform, root.transform);
            Assert.IsTrue(UIType.Readable(t.fontSize, 1080f), $"{where} is {t.fontSize}px, under the {UIType.MinimumScreenPx(1080f)}px floor");
            int cell = Cell(t.font);
            if (cell > 0) Assert.AreEqual(0, t.fontSize % cell, $"{where}: {t.fontSize}px is off {t.font.name}'s {cell}px cell");
        }
    }

    // The satnav's map takes the full width: no side panel, no title, no cash, no header icon — the
    // header is one status line, and the way-on buttons sit in the footer beside the notice.
    [Test]
    public void Satnav_map_spans_the_width_with_a_one_line_header()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(TravelMapPath);
        foreach (var gone in new[] { "SidePanel", "TitleLabel", "TitleIcon", "CashLabel", "CashIcon" })
            Assert.IsNull(root.transform.Find(gone), gone + " should have been removed");

        Assert.IsNotNull(root.transform.Find("Footer/ActionButton"), "ActionButton in the footer");
        Assert.IsNotNull(root.transform.Find("Footer/WalkButton"), "WalkButton in the footer");
        Assert.IsNotNull(root.transform.Find("Footer/NoticeLabel"), "NoticeLabel in the footer");

        var plot = (RectTransform)root.transform.Find("MapPlot");
        // Anchored to both edges, so it is the width of whatever screen it is on, not a fixed 1920's worth.
        Assert.AreEqual(0f, plot.anchorMin.x, "the map window should be anchored to the left edge");
        Assert.AreEqual(1f, plot.anchorMax.x, "the map window should be anchored to the right edge");
        Assert.Greater(1920f + plot.sizeDelta.x, 1800f, "the map window should run the width of the canvas");
        Assert.AreEqual(UIType.CanvasPx(TypeRole.Data, 1080f, 16), root.transform.Find("SubLabel").GetComponent<Text>().fontSize);
    }

    static int Cell(Font font)
    {
        if (font == null) return 0;
        string n = font.name;
        if (n.IndexOf("VT323", System.StringComparison.OrdinalIgnoreCase) >= 0) return 16;
        if (n.IndexOf("Silkscreen", System.StringComparison.OrdinalIgnoreCase) >= 0) return 8;
        if (n.IndexOf("Pixelify", System.StringComparison.OrdinalIgnoreCase) >= 0) return 20;
        if (n.IndexOf("fixedsys", System.StringComparison.OrdinalIgnoreCase) >= 0) return 16;
        return 0;
    }

    static string Path(Transform t, Transform root)
    {
        string p = t.name;
        for (var q = t.parent; q != null && q != root; q = q.parent) p = q.name + "/" + p;
        return p;
    }
}
