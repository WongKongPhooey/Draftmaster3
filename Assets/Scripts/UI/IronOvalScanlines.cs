using UnityEngine;
using UnityEngine.UI;

// The Iron Oval sheet's TV line: one full-screen tiled Image sitting on top of everything, gameplay
// included. Self-installing so no scene has to carry it — call IronOvalScanlines.Ensure() (or leave the
// component on any object) and it builds its own overlay canvas.
//
// Sheet spec: Image Type Tiled, top of sort order, Raycast Target off, no tint. The source tile is 1x3px
// with one row of black at 42% alpha, so the gap reads as 3 UI px — 9 screen px at 3x.
// Runs in edit mode as well, so a screen being authored is drawn through the same line the player sees.
// The overlay it builds is DontSave — it never lands in the scene file, however many times you look at it.
[ExecuteAlways]
[DisallowMultipleComponent]
public class IronOvalScanlines : MonoBehaviour
{
    [Tooltip("Sort order for the overlay canvas. Has to beat every other UI canvas or the lines sit under it.")]
    public int sortingOrder = 32000;
    [Tooltip("Overall strength. The 42% alpha is baked into the tile, so this scales it down further.")]
    [Range(0f, 1f)] public float opacity = 1f;

    static IronOvalScanlines _instance;
    Image _image;

    public static IronOvalScanlines Ensure()
    {
        if (_instance != null) return _instance;
        _instance = FindFirstObjectByType<IronOvalScanlines>();
        if (_instance == null)
            _instance = new GameObject("IronOvalScanlines").AddComponent<IronOvalScanlines>();
        return _instance;
    }

    void Awake()
    {
        if (Application.isPlaying && _instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
    }

    void OnEnable()
    {
        _instance = this;
        if (_image == null) Build();   // edit mode: rebuild after a domain reload or scene open
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_image != null && !Application.isPlaying)
        {
            var canvas = _image.canvas;
            if (canvas != null) DestroyImmediate(canvas.gameObject);
            _image = null;
        }
    }

    void Build()
    {
        var theme = PixelUITheme.Instance;
        if (theme == null || theme.scanline == null)
        {
            Debug.LogWarning("[IronOvalScanlines] no scanline sprite in the theme — run " +
                             "Draftmaster/Art/Set Up Iron Oval Kit.");
            enabled = false;
            return;
        }

        var canvas = PixelUI.CreateCanvas("ScanlineOverlay", sortingOrder);
        canvas.transform.SetParent(transform, false);
        // Generated furniture, not scene content: hidden from the hierarchy and never serialised.
        canvas.gameObject.hideFlags = HideFlags.HideAndDontSave;
        // Nothing on this canvas is interactive, and a raycaster over the whole screen would swallow
        // every click underneath it.
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) DestroyImmediate(raycaster);

        var go = new GameObject("Scanlines", typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _image = go.GetComponent<Image>();
        _image.sprite = theme.scanline;
        _image.type = Image.Type.Tiled;   // never Stretched: a stretched 1x3 tile is a flat grey wash
        _image.raycastTarget = false;
        Apply();
    }

    void OnValidate() { if (_image != null) Apply(); }

    void Apply()
    {
        if (_image != null) _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
    }
}
