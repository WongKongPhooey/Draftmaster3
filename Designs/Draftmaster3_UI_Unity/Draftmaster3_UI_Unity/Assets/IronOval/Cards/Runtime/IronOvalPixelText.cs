using TMPro;
using UnityEngine;

namespace IronOval.Cards
{
    /// Keeps a pixel font crisp at sizes between its native multiples.
    ///
    /// A pixel font is sharp when every font pixel covers a whole number of screen pixels:
    ///     screenPxPerFontPx = (fontSize / nativePx) * canvasScale   must be an integer.
    /// Instead of a fixed size, each text gets a *step*: how many screen pixels one font pixel covers,
    /// relative to the canvas scale. Step 0 = the font's native size (16px for monogram). Step +1 = one
    /// screen pixel bigger per font pixel, e.g. at 3x (1080p) monogram renders at 21.33px design size,
    /// which is exactly 4 screen px per font pixel: larger, and still perfectly sharp.
    ///
    ///     fontSize = nativePx * (scale + step) / scale
    ///
    /// Requirements: integer canvas scale (IronOvalPixelScaler), TMP atlas sampled at nativePx with
    /// RASTER_HINT + Point filter (the builder does this), no Auto Size, no fractional parent scale.
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class IronOvalPixelText : MonoBehaviour
    {
        public enum Role { Body, Row, Data, Name, Header, Title, Menu, Button, Label }

        /// Project-wide default step per role. Edit here (or at runtime) to change the type hierarchy everywhere.
        public static int DefaultStep(Role r)
        {
            switch (r)
            {
                case Role.Menu: return 1;   // title menu: one step up (1.33x at 1080p, 1.25x at 1440p)
                default: return 0;          // everything else at native size, exactly as designed
            }
        }

        public Role role = Role.Body;
        [Tooltip("Native pixel size of the font's grid (monogram = 16, Silkscreen = 8).")]
        public int nativePx = 16;
        [Tooltip("Use the role's default step. Untick to set a step for this text only.")]
        public bool useRoleDefault = true;
        [Tooltip("Extra screen pixels per font pixel. 0 = native size.")]
        public int step = 0;

        TMP_Text text;
        Canvas canvas;
        float lastScale = -1f;
        int lastStep = int.MinValue;

        void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            canvas = GetComponentInParent<Canvas>();
            lastScale = -1f;
            Apply();
        }

        void LateUpdate() { Apply(); }

        void OnValidate() { lastScale = -1f; }

        public int EffectiveStep => useRoleDefault ? DefaultStep(role) : step;

        public void Apply()
        {
            if (text == null) return;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            float s = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            int scale = Mathf.Max(1, Mathf.RoundToInt(s));
            int st = EffectiveStep;
            if (Mathf.Approximately(scale, lastScale) && st == lastStep) return;
            lastScale = scale; lastStep = st;
            text.enableAutoSizing = false;
            text.fontSize = nativePx * (float)Mathf.Max(1, scale + st) / scale;
        }
    }
}
