using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HallOfFame : MonoBehaviour
{	
	public GUISkin buttonSkin;
	public GUISkin redGUI;
	
	public Text leaderboardTitle;
	
    // Start is called before the first frame update
    void Start()
    {
       leaderboardTitle.text = "Hall Of Fame - All Time Starts"; 
	   
	   PlayFabManager.GetHallOfFameLeaderboards();
    }

    // Update is called once per frame
    void Update(){ 
    }
	
	void OnGUI(){
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		CommonGUI.BackButton("MainMenu");
		
		GUI.skin = buttonSkin;
	}
}
