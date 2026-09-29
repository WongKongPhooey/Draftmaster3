using Draftmaster.Data;
using UnityEngine;

[RequireComponent(typeof(SplineDriver))]
public class AIDriverBinding : MonoBehaviour
{
    public Driver driver;
    public VehicleInfo vehicleInfo;

    // Share of the grip limit the lowest-rated driver corners at (the best take all of it).
    public const float MinCornerCommitment = 0.93f;

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
            float aggression01 = Mathf.Clamp01(driver.Aggression / (float)Driver.StatMax);
            // Everyone runs the ideal line; aggression only nudges them fractionally off it (-0.05 = a touch
            // inside, +0.08 = a touch outside). Kept tight so the field visibly follows the ideal line.
            _spline.lineFactor = Mathf.Lerp(-0.05f, 0.08f, aggression01);
            racing.aggression01 = aggression01;

            float qualifying01 = Mathf.Clamp01(driver.Qualifying / (float)Driver.StatMax);
            float consistency01 = Mathf.Clamp01(driver.Consistency / (float)Driver.StatMax);
            racing.consistency01 = consistency01;

            float pace = Mathf.Lerp(0.93f, 1.04f, qualifying01);
            float jitter = Random.Range(1f - (1f - consistency01) * 0.04f, 1f);
            float basePace = pace * jitter;
            _spline.paceMultiplier = basePace;
            racing.SetBasePace(basePace);

            // How close to the grip limit they corner. Pace can't do this: it is capped at the limit, and every
            // car sits above it, so without it the whole field cornered identically, strung out ~5 lengths
            // apart and never passed. The best drivers use all of it; the weakest rated give up ~4%, a second
            // or two a lap at Watkins Glen. Consistency adds a little spread on top so equal-rated drivers differ.
            float commitJitter = Random.Range(-(1f - consistency01) * 0.012f, (1f - consistency01) * 0.004f);
            _spline.cornerCommitment = Mathf.Clamp(Mathf.Lerp(MinCornerCommitment, 1f, qualifying01) + commitJitter,
                                                   MinCornerCommitment - 0.01f, 1f);

            gameObject.name = $"AI_{driver.LastName}_{driver.Id}";
        }

        _spline.Rebuild();
    }
}
