using UnityEngine;

// Lightweight holder for a car's display identity (driver surname + car number), set at spawn from
// DriverNames / the chosen carset. Read by the race position counter and any HUD.
public class DriverLabel : MonoBehaviour
{
    public string driverName = "";
    public int carNumber = 0;
    [Tooltip("Carset/livery prefix this car uses, e.g. cup26.")]
    public string carset = "";
}
