using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -100);
    [Range(0f, 1f)] public float smoothing = 0f;
    [Tooltip("Add lean and impact shake while following a car. Installs DrivingCameraFeel on this camera.")]
    public bool cameraFeel = true;

    DrivingCameraFeel _feel;
    Vector3 _appliedOffset;                            // what the feel added last frame, so the follow maths stays clean
    Quaternion _baseRotation = Quaternion.identity;    // the camera's authored orientation, rolled around
    bool _rolled;

    void Awake()
    {
        _baseRotation = transform.rotation;
        EnsureFeel();
    }

    // The feel component is also what drives the swing camera, so a camera with the lean and shake switched
    // off still needs one the moment the player asks for the swing — including mid-race, which is why this is
    // checked every frame rather than once at Awake. A component installed purely for the swing gets the lean
    // and the shake switched off, so turning the swing on never quietly hands back the feel its author
    // deliberately declined.
    void EnsureFeel()
    {
        if (_feel != null) return;
        bool wanted = cameraFeel || CameraViewMode.Swinging;
        if (!wanted) return;

        _feel = GetComponent<DrivingCameraFeel>();
        if (_feel != null) return;
        _feel = gameObject.AddComponent<DrivingCameraFeel>();
        if (cameraFeel) return;
        _feel.enableLean = false;
        _feel.enableShake = false;
    }

    void LateUpdate()
    {
        if (target == null) return;
        EnsureFeel();

        // Strip last frame's feel offset before following, or the smoothing lerp would chase its own shake.
        Vector3 basePos = transform.position - _appliedOffset;
        Vector3 desired = target.position + offset;
        basePos = smoothing > 0f
            ? Vector3.Lerp(basePos, desired, 1f - Mathf.Pow(1f - smoothing, Time.deltaTime * 60f))
            : desired;

        Vector3 feelOffset = Vector3.zero;
        float roll = 0f;
        if (_feel != null && _feel.isActiveAndEnabled)
        {
            _feel.Tick(target, Time.deltaTime);
            feelOffset = _feel.PositionOffset;
            // Both are rolls about the camera's own axis: the swing turns the view round behind the car, the
            // feel's lean and shake wobble it, and they simply add.
            roll = _feel.RollDegrees + _feel.SwingDegrees;
        }

        _appliedOffset = feelOffset;
        transform.position = basePos + feelOffset;
        ApplyRoll(roll);
    }

    // Roll is layered on the camera's authored orientation, and the rotation is only written while there is
    // roll to apply (plus once more to clear it) — so a camera with the feel switched off is never touched.
    void ApplyRoll(float rollDeg)
    {
        if (rollDeg == 0f && !_rolled) return;
        transform.rotation = _baseRotation * Quaternion.Euler(0f, 0f, rollDeg);
        _rolled = rollDeg != 0f;
    }
}
