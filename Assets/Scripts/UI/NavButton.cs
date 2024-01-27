using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NavButton : MonoBehaviour
{
	
	public string sceneName;
    // Start is called before the first frame update
    void Start()
    {  
    }

    // Update is called once per frame
    void Update()
    {
    }
	
	public void loadScene(){
		Time.timeScale = 1.0f;
		LeanTween.reset();
		SceneManager.LoadScene(sceneName);
	}
	
	public void endChampionship(){
		Debug.Log("Championship Ended");
		PlayerPrefs.SetInt("ChampionshipReward",1);
		string currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
		string seriesPrefix = PlayerPrefs.GetString("SeriesChampionship" + currentSeriesIndex + "CarSeries");
		if(ModData.isModSeries(seriesPrefix) == true){
			//Pref deletes from the RaceRewards screen that's being skipped
			PlayerPrefs.DeleteKey("ChampionshipSubseries");
			PlayerPrefs.DeleteKey("SeriesPrize");
			PlayerPrefs.DeleteKey("RaceType");
			PlayerPrefs.DeleteKey("MomentComplete");
			PlayerPrefs.DeleteKey("SeriesPrizeAmt");
			PlayerPrefs.SetInt("ChampionshipReward",0);
			SceneManager.LoadScene("Menus/MainMenu");
		} else {
			Debug.Log("To Race Rewards");
			SceneManager.LoadScene("Menus/RaceRewards");
		}
	}
}
