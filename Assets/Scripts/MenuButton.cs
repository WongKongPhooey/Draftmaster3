using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
	
	public string SceneName = "";
	public string URLName = "";
	
	void OnMouseUp(){
		loadScene();
    }
	
	public void loadScene(){
		//if(PlayerPrefs.HasKey("NewUser")){
		LeanTween.reset();
		if(URLName != ""){
			Application.OpenURL(URLName);
		} else {
			SceneManager.LoadScene(SceneName);
		}
	}
}
