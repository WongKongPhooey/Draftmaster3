using UnityEngine;

// Glowing rectangle over the player's reserved pit box (PitLane.PlayerBox), showing where to stop the
// car. Built from the shared PitLane geometry, centred on the wall-side park lane, long axis along the
// lane. PlayerPitService toggles visibility (shown while the player is in the pit lane) and uses
// CarFullyInside as the gate for starting the stop.
//
// The sprite is a unit (1×1 m) rounded glow rect scaled to (boxWidth, boxLength), so marker-local
// coordinates are normalized: a point is inside the box iff |x| and |y| ≤ 0.5.
public class PlayerPitBoxMarker : MonoBehaviour
{
    [Tooltip("Box length (m), along the pit lane. Must comfortably exceed the car length (~4.8).")]
    public float boxLength = 8.5f;
    [Tooltip("Box width (m), across the lane. Must comfortably exceed the car width (~2.0).")]
    public float boxWidth = 4.6f;
    public Color color = new Color(0.35f, 1f, 0.55f);
    [Tooltip("Glow pulses per second.")]
    public float pulseHz = 1.1f;
    [Range(0f, 1f)] public float minAlpha = 0.35f;
    [Tooltip("Sorting order — under the cars (~5) and the box props (4).")]
    public int sortingOrder = 3;

    public bool IsBuilt => _builtBox >= 0;

    SpriteRenderer _sr;
    int _builtBox = -1;
    bool _visible;

    public static PlayerPitBoxMarker Ensure()
    {
        var existing = FindFirstObjectByType<PlayerPitBoxMarker>();
        if (existing != null) return existing;
        var go = new GameObject("PlayerPitBoxMarker");
        return go.AddComponent<PlayerPitBoxMarker>();
    }

    // (Re)position over the player's current box. Cheap when nothing changed; call every frame.
    public void UpdateFor(TrackBuilder track)
    {
        int box = PitLane.Configured ? PitLane.PlayerBox : -1;
        if (box == _builtBox) return;
        _builtBox = -1;
        if (box < 0 || track == null) { Apply(); return; }

        var pit = track.SamplePitCenterline();
        if (pit.Count < 2) { Apply(); return; }
        float pitLen = pit[pit.Count - 1].distance;
        if (pitLen <= 0f) { Apply(); return; }

        var s = track.SamplePitAt(PitLane.BoxDistance(box, pitLen), pit);
        Vector2 centre = s.position + s.normal * PitLane.ParkLateral;
        Vector3 wp = track.transform.TransformPoint(new Vector3(centre.x, centre.y, 0f));
        Vector3 wt = track.transform.TransformDirection(new Vector3(s.tangent.x, s.tangent.y, 0f));

        transform.position = new Vector3(wp.x, wp.y, -0.05f);
        // Local +Y = along the lane (same frame as the crew boxes), +X = lateral.
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(wt.y, wt.x) * Mathf.Rad2Deg - 90f);
        transform.localScale = new Vector3(boxWidth, boxLength, 1f);

        _builtBox = box;
        Apply();
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        Apply();
    }

    // True when the car's whole footprint sits inside the box rectangle.
    // halfExtents: x = half-width, y = half-length (VehicleCollision convention; car forward = local +X).
    public bool CarFullyInside(Transform car, Vector2 halfExtents)
    {
        if (!IsBuilt || car == null) return false;
        Vector3 f = car.right * halfExtents.y;   // long axis
        Vector3 s = car.up * halfExtents.x;      // side axis
        Vector3 p = car.position;
        return Inside(p + f + s) && Inside(p + f - s) && Inside(p - f + s) && Inside(p - f - s);
    }

    bool Inside(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world);   // unit-sprite space: box = ±0.5
        return Mathf.Abs(l.x) <= 0.5f && Mathf.Abs(l.y) <= 0.5f;
    }

    void Awake()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = BuildSprite();
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _sr.sharedMaterial = new Material(sh);
        _sr.sortingLayerName = "Vehicles";
        _sr.sortingOrder = sortingOrder;
        Apply();
    }

    void Update()
    {
        if (!_visible || !IsBuilt) return;
        float pulse = Mathf.Lerp(minAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * 2f * Mathf.PI));
        var c = color;
        c.a = color.a * pulse;
        _sr.color = c;
    }

    void Apply()
    {
        if (_sr != null) _sr.enabled = _visible && IsBuilt;
    }

    // Unit glow rect: bright border, faint fill, alpha falling off toward the inside of the border.
    static Sprite BuildSprite()
    {
        const int size = 64;
        const int border = 6;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float a = edge < border
                    ? Mathf.Lerp(1f, 0.35f, edge / (float)border)   // glowing border
                    : 0.18f;                                        // faint fill
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
