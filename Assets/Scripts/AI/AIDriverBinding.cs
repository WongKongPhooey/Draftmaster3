using Draftmaster.Data;
using UnityEngine;

[RequireComponent(typeof(SplineDriver))]
public class AIDriverBinding : MonoBehaviour
{
    public Driver driver;

    [Tooltip("Base speed in m/s before driver-stat modifiers are applied.")]
    public float baseSpeed = 35f;

    SplineDriver _spline;

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
    }

    public void Apply()
    {
        if (driver == null || _spline == null) return;

        // Aggression skews preferred line outward (more aggressive drivers ride the max line; cautious drivers tuck to the min).
        float aggression01 = Mathf.Clamp01(driver.Aggression / 100f);
        _spline.lineFactor = Mathf.Lerp(-0.6f, 1f, aggression01);

        // Qualifying boosts straight-line pace; consistency tightens the random jitter band.
        float qualifying01 = Mathf.Clamp01(driver.Qualifying / 100f);
        float consistency01 = Mathf.Clamp01(driver.Consistency / 100f);
        float paceMultiplier = Mathf.Lerp(0.92f, 1.05f, qualifying01);
        float jitter = Random.Range(1f - (1f - consistency01) * 0.08f, 1f);
        _spline.speed = baseSpeed * paceMultiplier * jitter;

        gameObject.name = $"AI_{driver.LastName}_{driver.Id}";
    }
}
