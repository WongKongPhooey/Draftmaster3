using NUnit.Framework;
using TMPro;
using UnityEngine;

// Pins down how TextMeshPro's fontSize relates to world size, which is what SpeechBubble gets wrong
// if anyone "simplifies" it back to assigning metres straight into fontSize.
//
// The bug this guards: setting fontSize to a metre value (0.22) rendered dialogue about 0.02 world units
// tall -- invisible -- because TMP's fontSize is a point size scaled by the component's own factor, not a
// world measurement. SpeechBubble.FitToMetres instead measures a probe line and derives a transform scale.
//
// SpeechBubble itself lives in Assembly-CSharp, which this test assembly cannot reference, so these tests
// verify the TECHNIQUE against TMP directly.
public class TmpWorldSizingTests
{
    GameObject _go;

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    TextMeshPro MakeLabel()
    {
        _go = new GameObject("TmpProbe");
        var label = _go.AddComponent<TextMeshPro>();
        label.enableWordWrapping = false;
        return label;
    }

    [Test]
    public void FontSize_IsNotWorldMetres()
    {
        var label = MakeLabel();
        label.fontSize = 0.22f;          // the broken assumption: "0.22 metres"
        label.text = "Ag";
        label.ForceMeshUpdate();

        float height = label.GetPreferredValues().y;

        // If fontSize were metres this would be ~0.22. It is far smaller, which is why the text vanished.
        Assert.That(height, Is.LessThan(0.1f),
            $"Expected fontSize 0.22 to render far below 0.22 world units, got {height}. " +
            "If this ever fails, TMP changed and SpeechBubble's scaling can be revisited.");
    }

    [Test]
    public void MeasuredScale_ProducesRequestedWorldHeight()
    {
        var label = MakeLabel();
        const float target = 0.22f;

        // The technique SpeechBubble uses: render at the font's native point size, measure, derive scale.
        float pointSize = label.font != null && label.font.faceInfo.pointSize > 0
            ? label.font.faceInfo.pointSize
            : 16f;
        label.fontSize = pointSize;
        label.text = "Ag";
        label.ForceMeshUpdate();

        float measured = label.GetPreferredValues().y;
        Assert.That(measured, Is.GreaterThan(0f), "probe line measured as zero height");

        float scale = target / measured;
        label.transform.localScale = new Vector3(scale, scale, 1f);

        float world = measured * scale;
        Assert.That(world, Is.EqualTo(target).Within(0.001f),
            "scaled line height should equal the requested metres");
    }

    [Test]
    public void PreferredValues_GrowWithLongerText()
    {
        var label = MakeLabel();
        label.fontSize = 16f;

        label.text = "Short";
        label.ForceMeshUpdate();
        float narrow = label.GetPreferredValues().x;

        label.text = "A considerably longer line of dialogue";
        label.ForceMeshUpdate();
        float wide = label.GetPreferredValues().x;

        // SpeechBubble sizes its plate from this, so a stuck value would give every line the same box.
        Assert.That(wide, Is.GreaterThan(narrow),
            "preferred width must track the text, or the dialogue plate cannot size itself");
    }

    [Test]
    public void RectTransform_DefaultsLargerThanADialogueBubble()
    {
        var label = MakeLabel();

        // The second bug: TMP aligns text inside this rect, so TopLeft alignment against the default rect
        // put the text metres away from the bubble. SpeechBubble now fits the rect to the text.
        Vector2 size = label.rectTransform.sizeDelta;
        Assert.That(size.x, Is.GreaterThan(1f),
            $"expected TMP's default rect to dwarf a ~0.5m bubble, got {size}");
    }
}
