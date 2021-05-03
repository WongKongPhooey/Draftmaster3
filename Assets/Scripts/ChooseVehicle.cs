using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChooseVehicle : MonoBehaviour {

	void OnMouseDown(){
		/*if(PlayerPrefs.GetInt("Local2Player")==1){
			Debug.Log("2 Playa C'mon Now");
			if(PlayerPrefs.GetInt("PlayerSelector")!=2){
				PlayerPrefs.SetString("carTexture", GetComponent<Renderer>().material.mainTexture.name);
				PlayerPrefs.SetInt("CarChoice",CarSelectGUI.currentCar);
				PlayerPrefs.SetInt("CarChoiceL",CarSelectGUI.currentCarL);
				PlayerPrefs.SetInt("CarChoiceR",CarSelectGUI.currentCarR);
				PlayerPrefs.SetInt("PlayerSelector",2);
				SceneManager.LoadScene(SceneManager.GetActiveScene().name);
			} else {
				PlayerPrefs.SetString("carTexture2", GetComponent<Renderer>().material.mainTexture.name);
				PlayerPrefs.SetInt("CarChoice",CarSelectGUI.currentCar);
				PlayerPrefs.SetInt("CarChoiceL",CarSelectGUI.currentCarL);
				PlayerPrefs.SetInt("CarChoiceR",CarSelectGUI.currentCarR);
				PlayerPrefs.SetInt("PlayerSelector",1);
				SceneManager.LoadScene("CircuitSelect");
			}
		} else {
			PlayerPrefs.SetString("carTexture", GetComponent<Renderer>().material.mainTexture.name);
			PlayerPrefs.SetInt("CarChoice",CarSelectGUI.currentCar);
			PlayerPrefs.SetInt("CarChoiceL",CarSelectGUI.currentCarL);
			PlayerPrefs.SetInt("CarChoiceR",CarSelectGUI.currentCarR);
			
			if(RacePoints.championshipMode == true){
				PlayerPrefs.SetString("ChampionshipCarTexture", GetComponent<Renderer>().material.mainTexture.name);
				PlayerPrefs.SetInt("ChampionshipCarChoice",CarSelectGUI.currentCar);
				PlayerPrefs.SetInt("CurrentChampionshipPos",1);
				SceneManager.LoadScene("ChampionshipHub");
			} else {
				if(ChallengeSelectGUI.challengeMode == true){
					SceneManager.LoadScene("ChallengeSelect");
				} else {
					SceneManager.LoadScene("CircuitSelect");
				}
			}
		}*/
	}
}
