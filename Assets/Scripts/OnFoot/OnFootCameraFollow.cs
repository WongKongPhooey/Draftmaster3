using UnityEngine;

// Lightweight top-down camera follow for the on-foot player. Falls back gracefully if Cinemachine is driving.
public class OnFootCameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothing = 8f;
    public float zDistance = -10f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = new Vector3(target.position.x, target.position.y, zDistance);
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
