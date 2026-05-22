using System.Collections.Generic;
using UnityEngine;

public class SplineDriver : MonoBehaviour
{
    public TrackBuilder track;
    [Tooltip("Racing-line variant: -1 = innermost, 0 = ideal, +1 = outermost. Anything in between blends.")]
    [Range(-1f, 1f)]
    public float lineFactor = 0f;
    [Tooltip("Speed in metres per second.")]
    public float speed = 40f;
    [Tooltip("Distance along the spline to spawn at, in metres.")]
    public float startDistance = 0f;
    [Tooltip("Lateral offset from centerline, in metres. Applied on top of any racing-line offset.")]
    public float lateralOffset = 0f;
    [Tooltip("Loop back to the start when the lap completes.")]
    public bool loop = true;
    [Tooltip("Sprite faces +Y by default. Set false if your sprite faces +X.")]
    public bool spriteFacesUp = true;
    [Tooltip("Extra rotation applied to the sprite, in degrees.")]
    public float angleOffsetDeg = 0f;
    [Tooltip("Drive on the pit lane spline instead of the main spline.")]
    public bool usePitLane = false;

    List<TrackBuilder.Sample> _mainSamples;
    List<TrackBuilder.Sample> _pitSamples;
    List<TrackInfoV2.RacingLineAnchor> _anchors;
    float _mainLength;
    float _pitLength;
    float _distance;
    bool _onPit;

    void Awake()
    {
        var vl = GetComponent<VehicleLogic>();
        if (vl != null) vl.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void Start()
    {
        Rebuild();
        _distance = startDistance;
        _onPit = usePitLane;
        Place();
    }

    public void Rebuild()
    {
        if (track == null) return;
        _mainSamples = track.SampleCenterline();
        _mainLength = _mainSamples.Count > 0 ? _mainSamples[_mainSamples.Count - 1].distance : 0f;
        _pitSamples = track.SamplePitCenterline();
        _pitLength = _pitSamples.Count > 0 ? _pitSamples[_pitSamples.Count - 1].distance : 0f;
        _anchors = track.track != null ? track.track.BuildRacingLineAnchors() : null;
    }

    void FixedUpdate()
    {
        if (_mainSamples == null || _mainSamples.Count < 2) return;

        // Handle manual pit-mode switch.
        if (usePitLane != _onPit)
        {
            _onPit = usePitLane;
            _distance = 0f;
        }

        float length = _onPit ? _pitLength : _mainLength;
        if (length <= 0f) return;

        _distance += speed * Time.fixedDeltaTime;
        if (loop && !_onPit)
        {
            while (_distance >= length) _distance -= length;
            while (_distance < 0f) _distance += length;
        }
        else
        {
            _distance = Mathf.Clamp(_distance, 0f, length);
        }
        Place();
    }

    void Place()
    {
        TrackBuilder.Sample sample;
        float length;
        if (_onPit && _pitSamples != null && _pitSamples.Count >= 2)
        {
            sample = track.SamplePitAt(_distance, _pitSamples);
            length = _pitLength;
        }
        else
        {
            sample = track.SampleAt(_distance, _mainSamples);
            length = _mainLength;
        }

        float lineLateral = (!_onPit && _anchors != null && track.track != null) ? track.track.GetLateralAt(_distance, lineFactor, _anchors) : 0f;
        Vector2 right = new Vector2(sample.tangent.y, -sample.tangent.x);
        Vector2 finalPos = sample.position + right * (lateralOffset + lineLateral);
        Vector3 worldPos = track != null ? track.transform.TransformPoint(new Vector3(finalPos.x, finalPos.y, 0)) : new Vector3(finalPos.x, finalPos.y, 0);
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, (spriteFacesUp ? angleDeg - 90f : angleDeg) + angleOffsetDeg);
    }
}
