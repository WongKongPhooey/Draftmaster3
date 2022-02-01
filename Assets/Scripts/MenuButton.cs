using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
	
	public string SceneName = "";
	public string URLName = "";
	
	void OnMouseUp(){
		if(PlayerPrefs.HasKey("NewUser")){
			if(URLName != ""){
				Application.OpenURL(URLName);
			} else {
				SceneManager.LoadScene(SceneName);
			}
		} else {
			//If new player, they need their first car
			SceneManager.LoadScene("PrizeCollection");
		}
    }
}
