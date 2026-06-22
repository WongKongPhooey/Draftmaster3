using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Wires a spawned networked car's scene-dependent refs (the track) that a prefab can't serialize, and —
// for the owning client — drops the car onto the start grid and frames the camera for driving.
//
// The host's player car spawns the instant StartHost runs, which is while the MENU scene is still active
// (before the race scene loads). At that point there's no track and no race camera, so setup is deferred:
// TrySetup() no-ops until a TrackBuilder exists, retrying on sceneLoaded. Clients spawn after they've been
// synced into the race scene, so their setup succeeds immediately. Brain gating lives in NetworkCarOwnerGate.
[RequireComponent(typeof(NetworkObject))]
public class NetworkedCarBindings : NetworkBehaviour
{
    [Tooltip("Spacing between grid slots along the track (m).")]
    public float gridSpacing = 8f;
    [Tooltip("Lateral stagger between alternating slots (m). 0 = single file.")]
    public float gridStagger = 3.5f;
    [Tooltip("Lateral offset off the centerline for the whole grid (m).")]
    public float gridLateral = 0f;
    [Tooltip("Orthographic camera size while driving (matches the single-player driving zoom).")]
    public float drivingOrthoSize = 20f;

    bool _setupDone;

    public override void OnNetworkSpawn()
    {
        if (!TrySetup())
            SceneManager.sceneLoaded += OnSceneLoaded; // host car: spawned before the race scene existed
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (TrySetup()) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // True once the track is present and the car has been wired (and, for the owner, placed + camera framed).
    bool TrySetup()
    {
        if (_setupDone) return true;
        var track = FindFirstObjectByType<TrackBuilder>();
        if (track == null) return false;

        var pvc = GetComponent<PlayerVehicleController>();
        var spline = GetComponent<SplineDriver>();
        if (pvc != null) pvc.track = track;
        if (spline != null) spline.track = track;

        if (IsOwner)
        {
            PlaceOnGrid(track, (int)OwnerClientId, pvc);
            // Start may have already run (host car, menu scene); re-seed heading/track from the placed pose.
            if (pvc != null) pvc.ReinitializeAtCurrentPose();
            WireCamera();
        }

        _setupDone = true;
        return true;
    }

    // Mirrors PitLaneStart's parked-car placement: sample the track behind the start line for this slot,
    // offset laterally, and set the transform so the car faces down-track.
    void PlaceOnGrid(TrackBuilder track, int slot, PlayerVehicleController pvc)
    {
        float sf = track.track != null ? track.track.startFinishDistance : 0f;
        float dist = sf - slot * gridSpacing;
        var s = track.SampleAt(dist);

        float lateral = gridLateral + ((slot % 2 == 0) ? gridStagger * 0.5f : -gridStagger * 0.5f);
        Vector2 off = s.position + s.normal * lateral;
        Vector3 worldPos = track.transform.TransformPoint(new Vector3(off.x, off.y, 0f));

        bool facesUp = pvc != null && pvc.spriteFacesUp;
        float angleOffset = pvc != null ? pvc.angleOffsetDeg : 180f;

        float headingDeg = Mathf.Atan2(s.tangent.y, s.tangent.x) * Mathf.Rad2Deg;
        float zRot = headingDeg - ((facesUp ? 90f : 0f) - angleOffset);

        transform.SetPositionAndRotation(
            new Vector3(worldPos.x, worldPos.y, transform.position.z),
            Quaternion.Euler(0f, 0f, zRot));
    }

    void WireCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = transform;
        if (cam.orthographic) cam.orthographicSize = drivingOrthoSize;
    }
}
