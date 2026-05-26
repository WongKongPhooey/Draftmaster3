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

    [Tooltip("Corner speed in mph as a function of corner radius in metres on a FLAT corner. Banking is added on top via bankingMphPerDegree. Cup-typical anchor points: 30m→55, 100m→90, 200m→115, 500m→155, 1000m→180.")]
    public AnimationCurve corneringSpeedCurve;

    [Tooltip("Extra corner speed (mph) added per degree of banking. ~2.5 mph/deg is a reasonable fudge for Cup cars.")]
    public float bankingMphPerDegree = 2.5f;
}
