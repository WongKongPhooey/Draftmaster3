using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardManager : MonoBehaviour
{
	
	public GUISkin buttonSkin;
	public GUISkin redGUI;
	
	public Text leaderboardTitle;
	public string circuitName;
	
    // Start is called before the first frame update
    void Start()
    {
	   circuitName = PlayerPrefs.GetString("CurrentCircuit");
       leaderboardTitle.text = "Fastest Lap - " + circuitName; 
	   
	   Debug.Log("Get leaderboard: " + circuitName);
	   PlayFabManager.GetLeaderboard(circuitName);
	   PlayFabManager.GetLeaderboardAroundPlayer(circuitName);
    }

    // Update is called once per frame
    void Update(){ 
    }
	
	void OnGUI(){
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		CommonGUI.BackButton("RaceResults");
		
		GUI.skin = buttonSkin;
	}
}
