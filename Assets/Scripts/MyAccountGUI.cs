using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MyAccountGUI : MonoBehaviour
{
	
	public GUISkin redGUI;
	public TMPro.TMP_Text playerName;
    public TMPro.TMP_Text totalStarts;
    public TMPro.TMP_Text totalWins;
    public TMPro.TMP_Text garageValue;
	
    // Start is called before the first frame update
    void Start()
    {
        playerName.text = PlayerPrefs.GetString("PlayerUsername");
        totalStarts.text = "Total Starts - " + PlayerPrefs.GetInt("TotalStarts");
        totalWins.text = "Total Wins - " + PlayerPrefs.GetInt("TotalWins");
        garageValue.text = "Garage Value - " + PlayerPrefs.GetInt("GarageValue");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
