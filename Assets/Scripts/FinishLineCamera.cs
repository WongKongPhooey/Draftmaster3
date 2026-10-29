using UnityEngine;

// The camera at the chequered flag. Added by RaceDirector.
//
// The moment the player crosses the line to finish the race, RaceEndPitIn hands the car to the AI and asks this
// to hold the camera where the line is. The car rolls away on its cool-down lap and out of shot, and the rest of
// the field comes across the line underneath the results table.
//
// Holding is done by giving CameraFollow a plain transform on the line to follow, which does the rest without
// either of them being told: AudioEars listens from the camera's subject, so the ears stand on the line and hear
// every car that crosses it swell and fall away; and DrivingCameraFeel only swings behind a car, so a swing
// camera unwinds back round to square. The player's own engine is turned into a 3D source like the AI field's,
// or it would go on sounding from inside the car as the car drove off.
//
// Let go the moment anything else points the camera somewhere (END RACE standing the driver beside the car, the
// crew chief's headset): whoever moved it owns it from there.
public class FinishLineCamera : MonoBehaviour
{
    static FinishLineCamera _instance;

    // The camera is parked on the line.
    public static bool Holding => _instance != null && _instance._anchor != null;

    Transform _anchor;
    CameraFollow _follow;
    DriveModeController _drive;

    EngineAudio _engine;
    float _engineBlend, _engineMin, _engineMax, _engineDoppler;

    void Awake() => _instance = this;

    void OnDestroy()
    {
        Release();
        if (_instance == this) _instance = null;
    }

    // Park the camera on the start/finish line, level with where the car crossed it. False when the camera was
    // not on this car to begin with (the crew chief's pit wall, a grandstand seat) and so is left alone.
    public bool Hold(Transform car, TrackBuilder track)
    {
        if (_anchor != null || car == null) return false;
        if (CrewChiefController.IsCrewChief) return false;

        var cam = Camera.main;
        _follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (_follow == null || !_follow.isActiveAndEnabled) return false;

        // On the car, or on the car the player handed to the AI (Auto).
        _drive = DriveModeController.Current;
        bool broadcast = _drive != null && !_drive.IsDriving;
        if (_follow.target != car && !broadcast) return false;

        // Handing the car over would pull the camera back onto it.
        if (_drive != null) _drive.suppressBroadcastCamera = true;

        _anchor = new GameObject("FinishLineCameraAnchor").transform;
        _anchor.position = LinePoint(car.position, track);
        _follow.target = _anchor;

        Spatialise(car);
        return true;
    }

    void Update()
    {
        if (_anchor != null && (_follow == null || _follow.target != _anchor)) Release();
    }

    void Release()
    {
        if (_anchor == null) return;
        Destroy(_anchor.gameObject);
        _anchor = null;

        // Back to the Auto hand-off's own say, once the player has the car again (END RACE puts it back in their
        // hands before parking it). Still watching, it is left as it is: the crew chief may have set it.
        if (_drive != null && _drive.IsDriving) _drive.suppressBroadcastCamera = false;

        if (_engine != null) _engine.SetSpatial(_engineBlend, _engineMin, _engineMax, _engineDoppler);
        _engine = null;
    }

    // Where the car crossed the line: the line's centre, slid across the road to the car's side of it, so the
    // camera stops level with the car rather than jumping sideways to the middle of the track.
    static Vector3 LinePoint(Vector3 carPos, TrackBuilder track)
    {
        if (track == null || track.track == null) return carPos;

        var s = track.SampleAt(track.track.startFinishDistance);
        Vector2 local = track.transform.InverseTransformPoint(carPos);
        Vector2 tangent = s.tangent.sqrMagnitude > 1e-6f ? s.tangent.normalized : Vector2.zero;
        Vector2 onLine = local - tangent * Vector2.Dot(local - s.position, tangent);
        Vector3 world = track.transform.TransformPoint(new Vector3(onLine.x, onLine.y, 0f));

        // The tracker saw the crossing a frame or two late, so the car is a few metres on. Anything further is
        // a car that never came past the line this way (a tow, a teleport): stop where it is instead.
        if (Vector2.Distance(world, carPos) > 30f) return carPos;
        world.z = carPos.z;
        return world;
    }

    // Heard like the rest of the field from here on: positioned, and fading out with distance.
    void Spatialise(Transform car)
    {
        _engine = car.GetComponent<EngineAudio>();
        if (_engine == null) return;
        _engineBlend = _engine.spatialBlend;
        _engineMin = _engine.minDistance;
        _engineMax = _engine.maxDistance;
        _engineDoppler = _engine.dopplerLevel;

        var spawner = FindFirstObjectByType<GridSpawner>();
        if (spawner != null)
            _engine.SetSpatial(1f, spawner.aiEngineMinDistance, spawner.aiEngineMaxDistance, spawner.aiEngineDoppler);
        else
            _engine.SetSpatial(1f, _engine.minDistance, _engine.maxDistance, _engine.dopplerLevel);
    }
}
