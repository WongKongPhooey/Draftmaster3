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

        var racing = GetComponent<AIRacingBehaviour>();
        if (racing == null) racing = gameObject.AddComponent<AIRacingBehaviour>();

        if (GetComponent<TireState>() == null) gameObject.AddComponent<TireState>();

        if (driver != null)
        {
            float aggression01 = Mathf.Clamp01(driver.Aggression / 100f);
            // Everyone runs the ideal line; aggression only nudges them a little off it (-0.15 = slightly inside, +0.2 = slightly outside).
            _spline.lineFactor = Mathf.Lerp(-0.15f, 0.2f, aggression01);
            racing.aggression01 = aggression01;

            float qualifying01 = Mathf.Clamp01(driver.Qualifying / 100f);
            float consistency01 = Mathf.Clamp01(driver.Consistency / 100f);
            racing.consistency01 = consistency01;

            float pace = Mathf.Lerp(0.93f, 1.04f, qualifying01);
            float jitter = Random.Range(1f - (1f - consistency01) * 0.04f, 1f);
            float basePace = pace * jitter;
            _spline.paceMultiplier = basePace;
            racing.SetBasePace(basePace);

            gameObject.name = $"AI_{driver.LastName}_{driver.Id}";
        }

        _spline.Rebuild();
    }
}
