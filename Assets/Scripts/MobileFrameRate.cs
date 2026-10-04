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

    // Most game time one rendered frame may catch up on, in seconds. The project's Maximum Allowed Timestep is
    // 1.0 s — fifty 50 Hz physics steps in a single frame — so the moment FixedUpdate costs more than its own
    // 20 ms (a 40-car field bunched at a race start on a phone), every slow frame queued more steps for the next
    // one and the game sank to ~2 fps. 0.1 s is five steps: past that the race runs briefly in slow motion
    // instead, and recovers as soon as the pack spreads out.
    public const float MaxCatchUpSeconds = 0.1f;

#if UNITY_ANDROID || UNITY_IOS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (!Application.isMobilePlatform) return;   // editor with the Android target selected
        QualitySettings.vSyncCount = 0;              // vSync overrides targetFrameRate when on
        Application.targetFrameRate = Target;
        Time.maximumDeltaTime = MaxCatchUpSeconds;
    }
#endif
}
