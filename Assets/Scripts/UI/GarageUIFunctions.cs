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
    void Start()
    {
        
    }

	public void setSeries(){
		GarageUI.seriesPrefix = seriesPrefix;
		GameObject.Find("Main").GetComponent<GarageUI>().loadAllCars();
		GameObject.Find("Main").GetComponent<GarageUI>().toggleDropdown();
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
