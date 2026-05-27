using UnityEngine;

[RequireComponent(typeof(SplineDriver))]
public class TireState : MonoBehaviour
{
    [Range(0f, 1f)] public float wear = 0f;

    SplineDriver _spline;

    public float GripMultiplier
    {
        get
        {
            var vi = _spline != null ? _spline.vehicleInfo : null;
            float minGrip = vi != null ? vi.tireMinGrip : 0.88f;
            return Mathf.Lerp(1f, minGrip, Mathf.Clamp01(wear));
        }
    }

    void Awake() { _spline = GetComponent<SplineDriver>(); }

    void FixedUpdate()
    {
        if (_spline == null || _spline.vehicleInfo == null) return;
        if (_spline.CurrentMph < 1f) return;

        // Approximate lateral acceleration from current segment radius and speed.
        float latG = EstimateLateralG();
        float maxG = Mathf.Max(_spline.vehicleInfo.maxLateralG, 0.1f);
        float intensity = Mathf.Clamp01(latG / maxG);
        wear = Mathf.Min(1f, wear + intensity * _spline.vehicleInfo.tireWearRate * Time.fixedDeltaTime);
    }

    float EstimateLateralG()
    {
        var trackInfo = _spline.track != null ? _spline.track.track : null;
        if (trackInfo == null || trackInfo.segments == null) return 0f;
        int segIdx = _spline.CurrentSegmentIndex();
        if (segIdx < 0 || segIdx >= trackInfo.segments.Length) return 0f;
        var seg = trackInfo.segments[segIdx];
        if (seg.type != TrackInfoV2.SegmentType.Turn) return 0f;
        float radius = seg.length / Mathf.Max(Mathf.Abs(seg.angle) * Mathf.Deg2Rad, 1e-4f);
        if (radius <= 0.5f) return 0f;
        float vMps = _spline.CurrentMph * (1f / 2.237f);
        return (vMps * vMps) / (radius * 9.81f);
    }

    public void Reset(float w = 0f) { wear = Mathf.Clamp01(w); }
}
