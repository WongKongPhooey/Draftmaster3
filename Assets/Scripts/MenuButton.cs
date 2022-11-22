using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButton : MonoBehaviour
{
	
	public string SceneName = "";
	public string URLName = "";
	
	void OnMouseUp(){
		//loadScene();
    }
	
	public void loadScene(){
		LeanTween.reset();
		if(URLName != ""){
			Application.OpenURL(URLName);
		} else {
			SceneManager.LoadScene(SceneName);
		}
	}
	
	public void loadTimeTrial(){
		LeanTween.reset();
		if(PlayerPrefs.GetString("LiveTimeTrial") != ""){
			SceneManager.LoadScene("Levels/HallOfFame");
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
