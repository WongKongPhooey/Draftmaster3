using UnityEngine;

// Android and iOS run at 30 fps unless Application.targetFrameRate is set -- Unity's mobile default,
// chosen for battery. The editor is uncapped, so the game felt smooth there and choppy on the phone:
// measured on device (SurfaceFlinger present times) it was pinned at exactly 33.2 ms a frame with
// headroom to spare. Only the legacy scrolling scenes ever set a rate (CameraManager, MainMenuUI), so
// every spline scene inherited the 30 cap.
//
// 60 rather than the panel's 120: a steady 60 is the smoothness win, and doubling it again roughly
// doubles the GPU heat on a long race for a difference most people will not see in pixel art.
static class MobileFrameRate
{
    public const int Target = 60;

#if UNITY_ANDROID || UNITY_IOS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (!Application.isMobilePlatform) return;   // editor with the Android target selected
        QualitySettings.vSyncCount = 0;              // vSync overrides targetFrameRate when on
        Application.targetFrameRate = Target;
    }
#endif
}
