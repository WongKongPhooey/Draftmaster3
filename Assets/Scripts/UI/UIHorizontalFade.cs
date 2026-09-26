using UnityEngine;
using UnityEngine.UI;

// A left-to-right fade drawn as UI: solid `color` at the left edge of its rect, running out to nothing at the
// right. Used on the title screen to take the grass down to black under the menu so the rows read.
//
// Every knob redraws live in the editor (Graphic.OnValidate dirties the mesh), so the fade can be tuned by eye
// with the scene open. Positions are fractions of the rect's width.
[RequireComponent(typeof(CanvasRenderer))]
public class UIHorizontalFade : MaskableGraphic
{
    [Tooltip("Opacity at the left edge.")]
    [Range(0f, 1f)] public float leftAlpha = 1f;
    [Tooltip("Opacity at the right edge.")]
    [Range(0f, 1f)] public float rightAlpha = 0f;
    [Tooltip("Fully leftAlpha up to here, as a fraction of the width from the left.")]
    [Range(0f, 1f)] public float fadeStart = 0.25f;
    [Tooltip("Fully rightAlpha from here on, as a fraction of the width from the left.")]
    [Range(0f, 1f)] public float fadeEnd = 1f;
    [Tooltip("Shape of the ramp between fadeStart and fadeEnd. 1 = linear; above 1 holds the dark longer and " +
             "drops off late; below 1 clears quickly then tails off.")]
    [Range(0.2f, 5f)] public float falloff = 1f;
    [Tooltip("0 = smooth. Otherwise the ramp is cut into this many flat bands, for a pixel-art look.")]
    [Range(0, 64)] public int bands = 0;

    // Enough strips that a curved ramp doesn't show its facets.
    const int SmoothStrips = 48;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        float a = Mathf.Min(fadeStart, fadeEnd), b = Mathf.Max(fadeStart, fadeEnd);

        // Strip edges as fractions of the width: the solid run, the ramp, the clear run.
        int steps = bands > 0 ? bands : SmoothStrips;
        var xs = new System.Collections.Generic.List<float> { 0f };
        if (a > 0f) xs.Add(a);
        for (int i = 1; i < steps; i++) xs.Add(Mathf.Lerp(a, b, i / (float)steps));
        if (b < 1f) xs.Add(b);
        xs.Add(1f);

        for (int i = 0; i < xs.Count - 1; i++)
        {
            float x0 = xs[i], x1 = xs[i + 1];
            // Banded: one flat alpha per strip, taken at its middle. Smooth: alpha at each edge, interpolated.
            float a0 = bands > 0 ? AlphaAt((x0 + x1) * 0.5f, a, b) : AlphaAt(x0, a, b);
            float a1 = bands > 0 ? a0 : AlphaAt(x1, a, b);
            Strip(vh, r, x0, x1, a0, a1);
        }
    }

    float AlphaAt(float x, float a, float b)
    {
        float t = b > a ? Mathf.Clamp01((x - a) / (b - a)) : (x < a ? 0f : 1f);
        t = Mathf.Pow(t, Mathf.Max(0.01f, falloff));
        return Mathf.Lerp(leftAlpha, rightAlpha, t);
    }

    void Strip(VertexHelper vh, Rect r, float x0, float x1, float a0, float a1)
    {
        float l = Mathf.Lerp(r.xMin, r.xMax, x0), rr = Mathf.Lerp(r.xMin, r.xMax, x1);
        Color32 c0 = WithAlpha(a0), c1 = WithAlpha(a1);
        int v = vh.currentVertCount;
        vh.AddVert(new Vector3(l, r.yMin), c0, Vector2.zero);
        vh.AddVert(new Vector3(l, r.yMax), c0, Vector2.zero);
        vh.AddVert(new Vector3(rr, r.yMax), c1, Vector2.zero);
        vh.AddVert(new Vector3(rr, r.yMin), c1, Vector2.zero);
        vh.AddTriangle(v, v + 1, v + 2);
        vh.AddTriangle(v + 2, v + 3, v);
    }

    Color32 WithAlpha(float alpha)
    {
        var c = color;
        c.a *= alpha;
        return c;
    }
}
