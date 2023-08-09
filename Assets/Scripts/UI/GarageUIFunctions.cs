using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GarageUIFunctions : MonoBehaviour
{
	public string seriesPrefix;
	public string carLivery;
	public int carNum;
    // Start is called before the first frame update
    void Start(){
    }

	public void setSeries(){
		GarageUI.seriesPrefix = seriesPrefix;
		GameObject.Find("Main").GetComponent<GarageUI>().loadAllCars();
		GameObject.Find("Main").GetComponent<GarageUI>().toggleDropdown(false);
	}

	public void carAction(){
		//If user heading to a race..
		if(PlayerPrefs.HasKey("ActivePath")){
			selectCar();
		} else {
			openCarInfo();
		}
	}

	public void selectCar(){
		PlayerPrefs.SetString("carSeries", seriesPrefix);
		PlayerPrefs.SetInt("CarChoice",carNum);
		PlayerPrefs.SetString("carTexture", seriesPrefix + "livery" + carNum);
		SceneManager.LoadScene("Menus/TrackSelect");
	}

	public void openCarInfo(){
		PlayerPrefs.SetInt("CarFocus",carNum);
		PlayerPrefs.SetString("SeriesFocus",seriesPrefix);
		SceneManager.LoadScene("Levels/SingleCar");
	}

	public void classUp(){
		Debug.Log("Class Me Up Scotty!");
		int carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carNum + "Unlocked");
		int carClass = PlayerPrefs.GetInt(seriesPrefix + carNum + "Class");
		int carGears = PlayerPrefs.GetInt(seriesPrefix + carNum + "Gears");
		
		if(carUnlocked == 0){
			int unlockClass = DriverNames.getRarity(seriesPrefix, carNum);
			int unlockGears = GameData.unlockGears(unlockClass);
			if(carGears >= unlockGears){
				carUnlocked = 1;
				carGears-= unlockGears;
				carClass = unlockClass;
				PlayerPrefs.SetInt(seriesPrefix + carNum + "Unlocked",1);
				PlayerPrefs.SetInt(seriesPrefix + carNum + "Gears",carGears);
				PlayerPrefs.SetInt(seriesPrefix + carNum + "Class",carClass);
			} else {
				Debug.Log("Not Enough To Unlock! Need " + unlockGears + " But Have " + carGears);
			}
		} else {
			int classMax = getClassMax(carClass);
			if(carGears >= classMax){
				carGears-= classMax;
				carClass++;
				PlayerPrefs.SetInt(seriesPrefix + carNum + "Gears",carGears);
				PlayerPrefs.SetInt(seriesPrefix + carNum + "Class",carClass);
			} else {
				Debug.Log("Not Enough!");
			}
		}
		reloadGarage();
	}

	public void reloadGarage(){
		GameObject.Find("Main").GetComponent<GarageUI>().loadAllCars();
	}

	public int getClassMax(int carClass){
		switch(carClass){
			case 0:
				return 10;
				break;
			case 1:
				return 20;
				break;
		    case 2:
				return 35;
				break;
			case 3:
				return 50;
				break;
			case 4:
				return 70;
				break;
			case 5:
				return 100;
				break;
			case 6:
				return 150;
				break;
		    default:
				return 999;
				break;
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
