using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
	
	public string SceneName = "";
	public string URLName = "";
	public bool hasLogic = false;
	public bool abortAction = false;
	public GameObject alertPopup;
	
	void OnMouseUp(){
		//loadScene();
    }
	
	public void loadScene(){
		LeanTween.reset();
		abortAction = false;
		if(hasLogic == true){
			SceneName = checkLogic(SceneName);
		}
		if(URLName != ""){
			Application.OpenURL(URLName);
		} else {
			if((SceneName != "")&&(abortAction == false)){
				SceneManager.LoadScene(SceneName);
			}
		}
	}
	
	public string checkLogic(string link){
		Debug.Log("Check button logic");
		switch(link){
			case "Menus/Mods":
				if((PlayerPrefs.GetInt("FreeModding") != 1)
				  &&(PlayerPrefs.GetInt("TransferTokens") < 999)){
					Debug.Log("You Don't Have Mod Access");
					abortAction = true;
					alertPopup.GetComponent<AlertManager>().showPopup("No Mods Access","The free beta for modding has now ended. You need to purchase the Editor pack in the Store to use mods.", "cup22livery1alt1");
				}
				break;
			default:
				break;
		}
		return link;
	}
	
	public void loadTimeTrial(){
		LeanTween.reset();
		if(PlayerPrefs.GetString("LiveTimeTrial") != ""){
			SceneManager.LoadScene("Menus/TimeTrial");
		}
	}
	
	public void loadInProgressChampionship(){
		LeanTween.reset();
		if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("ChampionshipSubseries").Length > 0)){
			PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("ChampionshipCarTexture"));
			PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("ChampionshipCarChoice"));
			PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("ChampionshipCarSeries"));
			PlayerPrefs.SetString("ActivePath","ChampionshipRace");
			//Debug.Log("Championship Car Series is " + PlayerPrefs.GetString("ChampionshipCarSeries"));
			SceneManager.LoadScene("Menus/ChampionshipHub");
		} else {
			SceneManager.LoadScene("Menus/SeriesSelect");
		}
	}
}
