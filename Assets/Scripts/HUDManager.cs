using UnityEngine;

public class HUDManager : MonoBehaviour
{

    private static GameObject HUDplayerSpeed;
    private static TMPro.TMP_Text HUDplayerSpeedLbl;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HUDplayerSpeed = GameObject.Find("HUDSpeed");
		HUDplayerSpeedLbl = HUDplayerSpeed.GetComponent<TMPro.TMP_Text>();
    }

    // Update is called once per frame
    void Update()
    {
    }

    public static void updateHUD(float speed)
    {
        HUDplayerSpeedLbl.text = speed.ToString() + "MpH";
    }
}
