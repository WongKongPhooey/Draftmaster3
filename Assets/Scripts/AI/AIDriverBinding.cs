using Draftmaster.Data;
using UnityEngine;

[RequireComponent(typeof(SplineDriver))]
public class AIDriverBinding : MonoBehaviour
{
    public Driver driver;
    public VehicleInfo vehicleInfo;

    SplineDriver _spline;

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
    }

    public void Apply()
    {
        if (_spline == null) return;

        if (vehicleInfo != null) _spline.vehicleInfo = vehicleInfo;

        if (driver != null)
        {
            // Aggression skews preferred line toward the right; cautious drivers tuck to the left. Replace later with proper turn-relative logic (inside vs outside) once the AI plans corner-by-corner.
            float aggression01 = Mathf.Clamp01(driver.Aggression / 100f);
            _spline.lineFactor = Mathf.Lerp(-0.6f, 1f, aggression01);

            // Qualifying lifts overall pace; consistency tightens random variation.
            float qualifying01 = Mathf.Clamp01(driver.Qualifying / 100f);
            float consistency01 = Mathf.Clamp01(driver.Consistency / 100f);
            float pace = Mathf.Lerp(0.93f, 1.04f, qualifying01);
            float jitter = Random.Range(1f - (1f - consistency01) * 0.04f, 1f);
            _spline.paceMultiplier = pace * jitter;

            gameObject.name = $"AI_{driver.LastName}_{driver.Id}";
        }

        _spline.Rebuild();
    }
}
