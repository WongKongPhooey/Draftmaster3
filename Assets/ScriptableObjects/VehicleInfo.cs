using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Vehicle/New Vehicle", order = 1)]
public class VehicleInfo : ScriptableObject
{
    [Header("Identity")]
    public string displayName;

    [Header("Top Speed (mph)")]
    public int topSpeed = 200;
    public float zeroToSixty = 3.4f;

    [Header("Curves")]
    [Tooltip("Legacy curve kept for the older VehicleLogic player physics. Free-form mapping.")]
    public AnimationCurve speedCurve;

    [Tooltip("Acceleration in m/s² as a function of current speed in mph. Should DECREASE with speed (engine power drops off relative to drag). Cup-typical: ~8 at 0 mph, ~4 at 100 mph, ~1.5 at 180 mph, ~0.2 near top speed.")]
    public AnimationCurve accelerationCurve;

    [Tooltip("Deceleration in m/s² as a function of current speed in mph when braking. Cup-typical: ~14 at 150+ mph (carbon brakes + downforce), ~10 below 80 mph.")]
    public AnimationCurve decelerationCurve;

    [Tooltip("Optional designer override. Corner speed in mph as a function of corner radius in metres on a FLAT corner. If empty, the lateral-G physics formula is used instead. Cup-typical anchors if set: 30m→55, 100m→90, 200m→115, 500m→155, 1000m→180.")]
    public AnimationCurve corneringSpeedCurve;

    [Tooltip("Max sustained lateral acceleration in g. Used when corneringSpeedCurve is empty. Cup-typical 1.4-1.6 with downforce.")]
    public float maxLateralG = 1.5f;

    [Tooltip("Extra corner speed (mph) added per degree of banking. ~2.5 mph/deg.")]
    public float bankingMphPerDegree = 2.5f;

    [Header("Drafting")]
    [Tooltip("Max speed bonus (mph) when fully in another car's slipstream.")]
    public float draftingMaxBonus = 7f;
    [Tooltip("Maximum gap (m) at which drafting effect applies. Falls off linearly to zero at this range.")]
    public float draftingMaxGap = 18f;
    [Tooltip("Drafting only kicks in above this speed (mph). Slow corners shouldn't benefit.")]
    public float draftingMinSpeed = 130f;

    [Header("Tire Wear")]
    [Tooltip("Wear added per second at maximum lateral G. Cup stint: full wear over ~30 mins of hard cornering.")]
    public float tireWearRate = 0.00055f;
    [Tooltip("Grip multiplier at full wear. 0.88 means 12% slower in corners on shot tires.")]
    [Range(0.5f, 1f)] public float tireMinGrip = 0.88f;

    [Header("Bicycle Dynamics (Tier 3, optional)")]
    [Tooltip("Distance between front and rear axles (m). Cup car ~2.8m.")]
    public float wheelbase = 2.8f;
    [Tooltip("Max front-wheel steering angle in degrees. Real Cup ~28°.")]
    public float maxSteeringAngle = 28f;
    [Tooltip("How quickly steering can change (deg/sec). Limits steering rate.")]
    public float steeringRate = 240f;
}
