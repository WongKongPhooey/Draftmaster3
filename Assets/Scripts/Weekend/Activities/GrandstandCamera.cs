using Draftmaster.Weekend;
using UnityEngine;

// The shot from the grandstand.
//
// Arriving in the stand is a cut, not a walk — the gate wipes the screen and puts the player in the seat —
// so the picture comes back tight on them, exactly as it was in the paddock, and then pulls back over the
// next couple of seconds to the view the track was authored for: a fixed vantage over a length of circuit,
// at whatever zoom the marker asks for (WeekendMarker.cameraView / cameraZoom). The pan is the difference
// between being teleported and sitting down.
//
// A venue with no authored vantage still gets a shot: the camera goes partway from the seat toward the
// nearest piece of racing surface and picks a zoom off the distance (GrandstandWatch), which is a workable
// view of the road at all 38 tracks without anybody opening a package.
//
// GET UP AND IT GIVES THE CAMERA BACK. The player is not frozen in the seat, and a fixed wide frame with
// the player walking out of the bottom of it is worse than no shot at all — so a few metres out of the seat
// the camera hands itself back to the ordinary on-foot follow and this stands down.
public class GrandstandCamera : MonoBehaviour
{
    public static GrandstandCamera Active { get; private set; }

    // How far the player can get out of their seat before the camera goes back to following them.
    const float ReleaseDistance = 3.5f;

    Camera _cam;
    CameraFollow _follow;
    OnFootCameraFollow _walkFollow;   // the other follow some scenes fit; parked while the shot is up
    PitLaneStart _zoomOwner;          // owns the scene's orthographic-size lerp
    Transform _rig;                   // what the follow chases while the camera is panning out
    Transform _restoreTarget;         // the player, to give the camera back to
    Transform _player;

    Vector3 _from, _to;
    float _elapsed, _panSeconds;
    float _zoomFrom, _zoomTo;
    Vector3 _seat;
    bool _done;

    // Pull back from `seat` to `view`, ending at `zoom` metres of half-height. `hasView` false means nobody
    // authored a vantage and one is worked out from the circuit; `zoom` <= 0 means the same about the zoom.
    public static GrandstandCamera Begin(Vector3 seat, Vector3 view, bool hasView, float zoom, float panSeconds)
    {
        // Ended rather than destroyed: Destroy runs OnDestroy at the END of the frame, so a torn-down shot
        // would give the camera back to the player after the new one had taken it.
        if (Active != null) Active.End();

        var cam = Camera.main;
        if (cam == null) return null;

        var go = new GameObject("GrandstandCamera");
        var shot = go.AddComponent<GrandstandCamera>();
        shot.Build(cam, seat, view, hasView, zoom, panSeconds);
        return shot;
    }

    void OnEnable() { Active = this; }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        Restore();
    }

    void Build(Camera cam, Vector3 seat, Vector3 view, bool hasView, float zoom, float panSeconds)
    {
        _cam = cam;
        _seat = seat;
        _follow = cam.GetComponent<CameraFollow>();
        _zoomOwner = FindFirstObjectByType<PitLaneStart>();

        // A scene can be running the simpler walking follow instead (or as well). It chases the player
        // every LateUpdate, so it has to stand down or it drags the camera straight back out of the shot.
        _walkFollow = cam.GetComponent<OnFootCameraFollow>();
        if (_walkFollow != null && _walkFollow.enabled) _walkFollow.enabled = false;
        else _walkFollow = null;

        _player = WeekendVenueAnchor.OnFootPlayer();

        // No vantage authored: aim at the road. Partway from the seat to the nearest point on the
        // centreline, wide enough to hold both.
        float seatToTrack;
        if (!hasView)
        {
            if (NearestTrackPoint(seat, out Vector2 road))
            {
                seatToTrack = Vector2.Distance(seat, road);
                view = GrandstandWatch.Vantage(seat, road);
            }
            else
            {
                seatToTrack = 0f;
                view = seat;
            }
        }
        else seatToTrack = Vector2.Distance(seat, view) * 2f;

        _from = new Vector3(seat.x, seat.y, 0f);
        _to = new Vector3(view.x, view.y, 0f);
        _panSeconds = Mathf.Max(0.2f, panSeconds);

        _zoomFrom = cam.orthographic ? cam.orthographicSize
                  : _zoomOwner != null ? _zoomOwner.OnFootZoom : 3.5f;
        _zoomTo = zoom > 0f ? zoom : GrandstandWatch.ZoomFor(seatToTrack);

        // The follow chases a rig rather than being switched off, so everything else about the camera —
        // the offset, the z plane, the pit-lane zoom arbiter — carries on working exactly as it does when
        // it is following the player.
        if (_follow != null)
        {
            _restoreTarget = _follow.target;
            _rig = new GameObject("GrandstandCameraRig").transform;
            _rig.position = _from;
            _follow.target = _rig;
        }

        if (_zoomOwner != null) _zoomOwner.SetZoomTarget(_zoomTo);
    }

    void LateUpdate()
    {
        if (_done) return;

        _elapsed += Time.deltaTime;
        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / _panSeconds));

        if (_rig != null) _rig.position = Vector3.Lerp(_from, _to, u);

        // Only when nothing else owns the zoom. PitLaneStart lerps toward its own target every frame and
        // writing the size here as well would be two hands on the same dial.
        if (_zoomOwner == null && _cam != null && _cam.orthographic)
            _cam.orthographicSize = Mathf.Lerp(_zoomFrom, _zoomTo, u);

        // Out of the seat: the shot is over.
        if (_player == null) _player = WeekendVenueAnchor.OnFootPlayer();
        if (_player != null && Vector2.Distance(_player.position, _seat) > ReleaseDistance) End();
    }

    // Give the camera back and stand down. Safe to call twice.
    public void End()
    {
        if (_done) return;
        Restore();
        Destroy(gameObject);
    }

    void Restore()
    {
        if (_done) return;
        _done = true;

        if (_follow != null)
        {
            // Whatever the camera was following before, unless it has been destroyed since — then the
            // walking body, which is what it would have been.
            var back = _restoreTarget != null ? _restoreTarget : WeekendVenueAnchor.OnFootPlayer();
            if (back != null) _follow.target = back;
        }

        if (_zoomOwner != null) _zoomOwner.SetZoomTarget(_zoomOwner.OnFootZoom);
        if (_walkFollow != null) { _walkFollow.enabled = true; _walkFollow = null; }
        if (_rig != null) Destroy(_rig.gameObject);
        _rig = null;
    }

    // Nearest point on the racing surface to the seat, off the loaded track's centreline.
    static bool NearestTrackPoint(Vector3 seat, out Vector2 point)
    {
        point = default;

        var builder = TrackPackage.ActiveTrack != null ? TrackPackage.ActiveTrack
                                                       : FindFirstObjectByType<TrackBuilder>();
        if (builder == null) return false;

        var samples = builder.SampleCenterline();
        if (samples == null || samples.Count == 0) return false;

        // Centreline samples are in the track's own space (SplineDriver transforms every one of them), and
        // the seat is a world position — so the search happens in the track's space and the answer comes
        // back out of it.
        Vector2 local = builder.transform.InverseTransformPoint(seat);

        float best = float.MaxValue;
        Vector2 nearest = default;
        for (int i = 0; i < samples.Count; i++)
        {
            Vector2 p = samples[i].position;
            float d = (local - p).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            nearest = p;
        }
        if (best == float.MaxValue) return false;

        point = builder.transform.TransformPoint(new Vector3(nearest.x, nearest.y, 0f));
        return true;
    }
}
