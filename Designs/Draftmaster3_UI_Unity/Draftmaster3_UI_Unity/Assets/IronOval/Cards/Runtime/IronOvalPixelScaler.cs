using UnityEngine;
using UnityEngine.UI;

namespace IronOval.Cards
{
    /// Integer UI scale from screen height: floor(h / 360), clamped 1–6 (per the Iron Oval UI kit).
    [RequireComponent(typeof(CanvasScaler))]
    [ExecuteAlways]
    public class IronOvalPixelScaler : MonoBehaviour
    {
        public int referenceHeight = 360;
        public int minScale = 1, maxScale = 6;
        CanvasScaler scaler;
        int lastHeight = -1;

        void OnEnable() { scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; lastHeight = -1; }

        void Update()
        {
            if (Screen.height == lastHeight) return;
            lastHeight = Screen.height;
            scaler.scaleFactor = Mathf.Clamp(Mathf.FloorToInt(Screen.height / (float)referenceHeight), minScale, maxScale);
        }
    }
}
