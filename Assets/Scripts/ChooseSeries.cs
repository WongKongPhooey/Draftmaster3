using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChooseSeries : MonoBehaviour {

	void OnMouseDown(){
		PlayerPrefs.SetString("raceSeries", this.name);
		if(RacePoints.championshipMode == true){
			PlayerPrefs.SetString("championshipRaceSeries", this.name);
			switch(this.name){
			case "StockCar":
				PlayerPrefs.SetInt("FieldSize",40);
				PlayerPrefs.SetInt("NameTrim",6);
				break;
			case "IndyCar":
				PlayerPrefs.SetInt("FieldSize",24);
				PlayerPrefs.SetInt("NameTrim",10);
				break;
			case "Truck":
				PlayerPrefs.SetInt("FieldSize",24);
				PlayerPrefs.SetInt("NameTrim",11);
				break;
			}
		}
		if(RacePoints.championshipMode == true){
			if((MiscScripts.proVersion == false)&&(this.name != "StockCar")){
				MiscScripts.championshipSelector = false;
				SceneManager.LoadScene("BuyProVersion");
			} else {
				MiscScripts.championshipSelector = false;
				SceneManager.LoadScene("CarSelect");
			}
		} else {
			MiscScripts.championshipSelector = false;
			SceneManager.LoadScene("CarSelect");
		}
	}
}
