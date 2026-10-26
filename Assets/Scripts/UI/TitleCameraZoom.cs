using Draftmaster.Sim;
using UnityEngine;

// Pulls the title camera back on a narrow screen. The crash and the track backdrop are laid out in reference
// pixels off the screen's height, so on a portrait screen the crash's slot alone is wider than the view and
// nothing either side of the track shows. This widens the orthographic size until at least
// `minVisibleWidthPx` reference pixels fit across, and TitleCrashScene / TitleTrackBackdrop divide the zoom
// back out (Factor) so they stay the same size in the world — the camera sees more of it instead.
// The title canvas is ScreenSpace-Camera, so the UI is unaffected.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class TitleCameraZoom : MonoBehaviour
{
    [Tooltip("Orthographic size at 16:9 and wider — the size the layout is authored at.")]
    public float baseOrthoSize = 1.8f;
    [Tooltip("The least width, in reference pixels (the layout is 640 x 360), the camera may show. A screen " +
             "narrower than this zooms out until it fits.")]
    public float minVisibleWidthPx = 600f;

    Camera _camera;

    void OnEnable() => Apply();
    void Update() => Apply();

    void OnDisable()
    {
        if (_camera != null) _camera.orthographicSize = baseOrthoSize;
    }

    void Apply()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null || baseOrthoSize <= 0f || _camera.aspect <= 0f) return;

        float visiblePx = TitleCrash.CanvasHeight * _camera.aspect;
        float size = baseOrthoSize * Mathf.Max(1f, minVisibleWidthPx / visiblePx);
        if (!Mathf.Approximately(_camera.orthographicSize, size)) _camera.orthographicSize = size;
    }

    // How far this camera is zoomed out from the authored size: 1 without the component. Checks `enabled`, not
    // isActiveAndEnabled: a backdrop or crash building in its own OnEnable can run before this one's, while
    // isActiveAndEnabled still reads false, and would lay out at 1 against an already zoomed camera.
    public static float Factor(Camera camera)
    {
        if (camera == null || !camera.TryGetComponent(out TitleCameraZoom zoom) || !zoom.enabled ||
            zoom.baseOrthoSize <= 0f)
            return 1f;
        return Mathf.Max(1f, camera.orthographicSize / zoom.baseOrthoSize);
    }
}
