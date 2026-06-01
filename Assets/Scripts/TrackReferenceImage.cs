using UnityEngine;

// Drops a real-track reference photo behind the scene for tracing the spline + barriers.
// Editor helper: scale by a known real-world distance, nudge position, fade opacity.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class TrackReferenceImage : MonoBehaviour
{
    public Sprite image;
    [Tooltip("Metres per pixel of the source image. Set via the calibration fields below, or by hand.")]
    public float metresPerPixel = 1f;
    [Range(0f, 1f)] public float opacity = 0.5f;
    [Tooltip("Sorting order. Keep very negative so the track/cars draw on top.")]
    public int sortingOrder = -1000;
    public Vector2 worldOffset = Vector2.zero;
    public float rotationDeg = 0f;

    [Header("Calibration")]
    [Tooltip("Two pixel coordinates on the image of a feature whose real distance you know.")]
    public Vector2 calibPixelA;
    public Vector2 calibPixelB;
    [Tooltip("Real-world distance (m) between those two pixels. Press the context-menu 'Apply Calibration'.")]
    public float calibRealMetres = 100f;

    SpriteRenderer _sr;

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }

    void Apply()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) return;
        if (image != null) _sr.sprite = image;
        _sr.sortingOrder = sortingOrder;
        _sr.color = new Color(1f, 1f, 1f, opacity);

        // Scale: SpriteRenderer renders sprite at pixelsPerUnit. World metres per pixel = metresPerPixel.
        // localScale = metresPerPixel * (sprite.pixelsPerUnit) so 1 source pixel = metresPerPixel world units.
        if (image != null)
        {
            float s = metresPerPixel * image.pixelsPerUnit;
            transform.localScale = new Vector3(s, s, 1f);
        }
        transform.position = new Vector3(worldOffset.x, worldOffset.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0, 0, rotationDeg);
    }

    [ContextMenu("Apply Calibration")]
    void ApplyCalibration()
    {
        float pixelDist = Vector2.Distance(calibPixelA, calibPixelB);
        if (pixelDist < 1e-3f) return;
        metresPerPixel = calibRealMetres / pixelDist;
        Apply();
    }
}
