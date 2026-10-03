using System;
using System.Collections;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Turns a phone to portrait while the player drives with the swing camera, and back to landscape when they
// stop. Why, and the rules for when, are Draftmaster.Controls.DriveOrientation.
//
// Self-installing and kept across scenes, like TouchDriveControls. Only a handheld is ever turned, so on a
// desktop this never writes the orientation at all. Nothing else needs telling: every in-car surface — the
// touch wheel and pedals, the running order, the position and timing readouts, the pause menu — lays itself
// out from the screen's size each frame, and PixelGUI's scale comes from the short side, so they all follow.
//
// The one thing that does not follow on its own is the race camera's zoom. An orthographic size is half the
// screen's HEIGHT in metres, so the same size on an upright screen more than halves the car. The follow
// camera is therefore opened out while it renders, by the screen's long side over its short side, so a metre
// of track covers as many pixels as it did in landscape: the car and the road are as wide in the hand as
// before, and the extra height all goes on road ahead. Done around the render rather than on the camera's
// value, because half a dozen scripts own that value (pit lane zoom, grandstand, co-op) and would fight a
// change to it, and it is put back straight after so none of them ever sees it.
//
// To try it without a phone, open the Device Simulator (Window > General > Device Simulator), pick the swing
// camera from the pause menu and get in the car: the simulated screen turns upright.
[DefaultExecutionOrder(-950)]
public class DriveOrientationController : MonoBehaviour
{
    static DriveOrientationController _instance;

    readonly PortraitLatch _latch = new PortraitLatch();
    ScreenOrientation _home;
    bool _applied;   // the orientation this component last wrote; false = never written, or handed back

    // Whether the screen has been turned upright for the drive.
    public static bool Portrait => _instance != null && _instance._applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("DriveOrientationController");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DriveOrientationController>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        // What to hand back: whatever the game booted in, which is the Player Settings orientation. A phone
        // that booted held upright under auto-rotation reports portrait here, and the game is landscape, so
        // that falls back to landscape rather than locking the whole game upright.
        _home = UnityEngine.Device.Screen.orientation;
        if (_home == ScreenOrientation.Portrait || _home == ScreenOrientation.PortraitUpsideDown)
            _home = ScreenOrientation.LandscapeLeft;
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        SceneManager.sceneLoaded -= SceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        if (_applied) UnityEngine.Device.Screen.orientation = _home;
        _instance = null;
    }

    // ------------------------------------------------------------------ zoom

    Camera _opened;          // the camera whose size is widened for the render in progress
    float _openedFrom;       // and the size it had before

    void BeginCamera(ScriptableRenderContext context, Camera cam)
    {
        _opened = null;
        if (!_applied || cam == null || !cam.orthographic || cam.targetTexture != null) return;
        if (cam.cameraType != CameraType.Game || cam.GetComponent<CameraFollow>() == null) return;

        float factor = DriveOrientation.PortraitZoom(cam.pixelWidth, cam.pixelHeight);
        if (factor <= 1f) return;   // the screen has not finished turning yet

        _opened = cam;
        _openedFrom = cam.orthographicSize;
        cam.orthographicSize = _openedFrom * factor;
    }

    void EndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (_opened == null || cam != _opened) return;
        _opened.orthographicSize = _openedFrom;
        _opened = null;
    }

    // Sat in the car: driving it, or watching the broadcast cut while the AI has it (the TV button must not
    // spin the screen round). Not on foot, and not the crew chief's pit-wall view.
    //
    // And actually handed the car: not while the chief's briefing and the setup panel are still between the
    // driver and the controls (PitLaneStart.GettingIn). Both are landscape screens, and turning upright at
    // the door left the setup panel drawn sideways on a portrait phone. Nor while the setup panel is up for
    // any other reason.
    static bool InCar =>
        !PadInput.OnFoot &&
        !PitLaneStart.GettingIn &&
        !CarSetupPanelUI.IsOpen &&
        (PlayerVehicleController.Human != null ||
         (DriveModeController.Current != null && !DriveModeController.Current.IsDriving));

    // ------------------------------------------------------------------ leaving the scene

    bool _leaving;   // a scene change is waiting on the screen to turn back; nothing turns it upright meanwhile
    int _leaveId;    // which hold the fallback release below belongs to, so a stale one cannot end a newer hold
    bool _goingNow;  // the held change is being run: its own call back in here must go straight through

    // Holds a scene change until an upright screen has turned back to landscape. The next scene was built
    // for landscape and lays itself out on its first frames, so loading it mid-turn — or upright, with the
    // turn still to come — leaves it measured for the wrong screen (quitting to the title from the pause
    // menu was the case that showed it).
    //
    // Returns false when there is nothing to wait for (desktop, landscape, the fixed camera): the caller loads
    // straight away as it always did. Returns true when the screen is upright: it is sent back to landscape,
    // `then` runs once it has got there, and the caller must stop. A second call while one is already waiting
    // is dropped, so a button pressed twice during the turn does not load twice. `then` is free to come back
    // in here: by then the screen is landscape and it is told to go ahead.
    public static bool HoldSceneChangeForLandscape(Action then)
    {
        if (_instance == null || _instance._goingNow) return false;
        if (_instance._leaving) return true;
        if (!_instance._applied) return false;

        _instance.StartCoroutine(_instance.TurnBackThen(then));
        return true;
    }

    IEnumerator TurnBackThen(Action then)
    {
        _leaving = true;
        int id = ++_leaveId;
        _latch.Reset();
        _applied = false;
        UnityEngine.Device.Screen.orientation = _home;

        // Real time: the pause menu has the race stopped while it waits.
        float waited = 0f;
        while (!DriveOrientation.ReadyToLeave(UnityEngine.Device.Screen.width, UnityEngine.Device.Screen.height, waited))
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }
        yield return null;   // one landscape frame, so anything reading the screen's size this frame sees it

        _goingNow = true;
        try { then?.Invoke(); }
        finally
        {
            _goingNow = false;

            // Normally the load lands and SceneLoaded lets go; if `then` changed its mind and loaded nothing,
            // the drive picks back up rather than staying landscape for good.
            StartCoroutine(StopLeavingAfter(id, 1f));
        }
    }

    IEnumerator StopLeavingAfter(int id, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (id == _leaveId) _leaving = false;
    }

    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) _leaving = false;
    }

    void Update()
    {
        if (_leaving) return;

        bool handheld = PixelGUI.Handheld;
        if (!handheld && !_applied) return;

        bool want = DriveOrientation.WantsPortrait(handheld, CameraViewMode.Swinging, InCar, RaceDirector.InResults);
        bool portrait = _latch.Update(want, Time.unscaledDeltaTime);
        if (portrait == _applied) return;

        _applied = portrait;
        UnityEngine.Device.Screen.orientation = portrait ? ScreenOrientation.Portrait : _home;
    }
}
