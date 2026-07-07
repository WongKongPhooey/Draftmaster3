using UnityEngine;

// Lightweight holder for a car's display identity (driver surname + car number), set at spawn from
// DriverNames / the chosen carset. Read by the race position counter and any HUD.
public class DriverLabel : MonoBehaviour
{
    public string driverName = "";
    public int carNumber = 0;
    [Tooltip("Carset/livery prefix this car uses, e.g. cup26.")]
    public string carset = "";

    [Tooltip("Team this car races for. -1 = unaffiliated. Team 0 is the player's team (TeamSwitchController offers control of its cars).")]
    public int teamId = -1;
    [Tooltip("Display name of the team, e.g. 'Hendrick'.")]
    public string teamName = "";
}
