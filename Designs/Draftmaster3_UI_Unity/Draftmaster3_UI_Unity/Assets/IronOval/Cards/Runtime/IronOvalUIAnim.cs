using UnityEngine;

namespace IronOval.Cards
{
    /// Stepped (whole-pixel / whole-frame) UI animations matching the mockup's CSS keyframes.
    /// Added automatically by the builder wherever the design animates something.
    public class IronOvalUIAnim : MonoBehaviour
    {
        public enum Mode { Blink, Bob, Spin, Pop, Rise }
        public Mode mode = Mode.Blink;
        [Tooltip("Seconds for one full cycle (one-shot length for Pop / Rise).")]
        public float period = 1f;

        RectTransform rt;
        CanvasGroup group;
        Vector2 basePos;
        float t;

        void Awake()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            if (mode == Mode.Blink || mode == Mode.Rise)
            {
                group = GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        void OnEnable() { t = 0f; if (rt != null) Apply(); }

        void Update() { t += Time.unscaledDeltaTime; Apply(); }

        void Apply()
        {
            switch (mode)
            {
                case Mode.Blink:
                    group.alpha = (t % period) < period * 0.5f ? 1f : 0f;
                    break;
                case Mode.Bob: // steps(2): up 4px for half the cycle
                    rt.anchoredPosition = basePos + ((t % period) < period * 0.5f ? Vector2.zero : new Vector2(0, 4));
                    break;
                case Mode.Spin: // steps(64) over the period
                    int step = Mathf.FloorToInt((t % period) / period * 64f);
                    rt.localRotation = Quaternion.Euler(0, 0, -step * (360f / 64f));
                    break;
                case Mode.Pop: // 5 steps: 0.4 -> 1.1 -> 1.0
                {
                    float k = Mathf.Clamp01(t / period); int s = Mathf.Min(4, Mathf.FloorToInt(k * 5f));
                    float[] scale = { 0.4f, 0.75f, 1.1f, 1.05f, 1f };
                    rt.localScale = Vector3.one * (k >= 1f ? 1f : scale[s]);
                    break;
                }
                case Mode.Rise: // 5 steps: 18px below + transparent -> in place
                {
                    float k = Mathf.Clamp01(t / period); float q = Mathf.Floor(k * 5f) / 5f;
                    rt.anchoredPosition = basePos + new Vector2(0, -18f * (1f - q));
                    group.alpha = q;
                    break;
                }
            }
        }
    }
}
