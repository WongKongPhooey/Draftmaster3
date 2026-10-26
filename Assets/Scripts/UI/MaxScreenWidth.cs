using UnityEngine;

// Keeps a piece of UI from taking more than a share of the screen's width. It sits at `scale` while that fits
// and shrinks to the cap once it doesn't — a wordmark laid out for a landscape screen still fits a portrait one.
// Runs in edit mode so the scene view shows what a given Game view size will look like.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class MaxScreenWidth : MonoBehaviour
{
    [Tooltip("The scale it sits at whenever that fits.")]
    public float scale = 1f;
    [Tooltip("The most of the root canvas's width it may cover, 0..1.")]
    [Range(0.05f, 1f)] public float maxWidthFraction = 0.6f;

    RectTransform _rt;

    void LateUpdate()
    {
        if (_rt == null) _rt = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var root = canvas.rootCanvas.transform as RectTransform;
        float width = _rt.rect.width;
        if (root == null || width <= 0f) return;

        float s = Mathf.Min(scale, root.rect.width * maxWidthFraction / width);
        if (!Mathf.Approximately(_rt.localScale.x, s)) _rt.localScale = new Vector3(s, s, _rt.localScale.z);
    }
}
