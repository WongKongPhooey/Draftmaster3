using UnityEngine;

namespace IronOval.Cards
{
    /// Keeps a full-screen RectTransform inside Screen.safeArea (notch / home indicator).
    /// The builder puts this on every Mobile variant; the screen's background stays full-bleed behind it.
    [RequireComponent(typeof(RectTransform))]
    public class IronOvalSafeArea : MonoBehaviour
    {
        RectTransform rt;
        Rect last;
        Vector2Int lastSize;

        void OnEnable() { rt = (RectTransform)transform; last = default; Apply(); }
        void Update() { Apply(); }

        void Apply()
        {
            var sa = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (sa == last && size == lastSize) return;
            last = sa; lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;
            var min = sa.position; var max = sa.position + sa.size;
            rt.anchorMin = new Vector2(min.x / size.x, min.y / size.y);
            rt.anchorMax = new Vector2(max.x / size.x, max.y / size.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
