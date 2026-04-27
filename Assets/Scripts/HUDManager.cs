using UnityEngine;

public class HUDManager : MonoBehaviour
{

    private static GameObject HUDplayerSpeed, HUDplayerLocation;
    private static TMPro.TMP_Text HUDplayerSpeedLbl, HUDplayerLocationLbl;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HUDplayerSpeed = GameObject.Find("HUDSpeed");
        HUDplayerLocation = GameObject.Find("HUDLocation");
		HUDplayerSpeedLbl = HUDplayerSpeed.GetComponent<TMPro.TMP_Text>();
        HUDplayerLocationLbl = HUDplayerLocation.GetComponent<TMPro.TMP_Text>();
    }

    // Update is called once per frame
    void Update()
    {
        if (HUDplayerLocationLbl != null) {
            HUDplayerLocationLbl.text = RaceManager.playerLocation.ToString("F3") + "m";
        }
    }

    public static void updateHUD(float speed)
    {
        HUDplayerSpeedLbl.text = speed.ToString("F3") + "MpH";
        HUDplayerLocationLbl.text = RaceManager.playerLocation.ToString("F3") + "m";
    }
}
