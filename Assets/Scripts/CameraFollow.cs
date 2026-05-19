using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -100);
    [Range(0f, 1f)] public float smoothing = 0f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = smoothing > 0f
            ? Vector3.Lerp(transform.position, desired, 1f - Mathf.Pow(1f - smoothing, Time.deltaTime * 60f))
            : desired;
    }
}
