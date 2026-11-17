using UnityEngine;

namespace IronOval.Cards
{
    /// Shows the Mobile or PC variant of a screen. Auto = Application.isMobilePlatform.
    /// Set IronOvalPlatformSwitch.GlobalOverride (e.g. from an options menu or a debug key) to force one everywhere.
    [ExecuteAlways]
    public class IronOvalPlatformSwitch : MonoBehaviour
    {
        public enum Platform { Auto, Mobile, PC }

        public static Platform GlobalOverride = Platform.Auto;
        public static event System.Action Changed;

        [Tooltip("Per-screen override. Auto defers to GlobalOverride, then to the device.")]
        public Platform force = Platform.Auto;
        public GameObject mobile;
        public GameObject pc;

        public static void SetGlobal(Platform p) { GlobalOverride = p; Changed?.Invoke(); }

        public bool IsMobile
        {
            get
            {
                var p = force != Platform.Auto ? force : GlobalOverride;
                if (p == Platform.Auto) return Application.isMobilePlatform;
                return p == Platform.Mobile;
            }
        }

        void OnEnable() { Changed += Apply; Apply(); }
        void OnDisable() { Changed -= Apply; }
        void OnValidate()
        {
#if UNITY_EDITOR
            // SetActive isn't allowed inside OnValidate; defer it
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif
        }

        public void Apply()
        {
            bool m = IsMobile;
            if (mobile != null && mobile.activeSelf != m) mobile.SetActive(m);
            if (pc != null && pc.activeSelf == m) pc.SetActive(!m);
        }
    }
}
