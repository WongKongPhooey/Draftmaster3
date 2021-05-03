using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
	
	public string SceneName = "";
	
	void OnMouseUp(){
		if(PlayerPrefs.HasKey("NewUser")){
			SceneManager.LoadScene(SceneName);
		} else {
			//If new player, they need their first car
			SceneManager.LoadScene("PrizeCollection");
		}
    }
}
