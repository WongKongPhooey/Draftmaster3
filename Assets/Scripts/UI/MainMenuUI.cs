using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        loadCurrentChampionshipInfo();
    }

	void loadCurrentChampionshipInfo(){
		
		string subSeriesId;
		string subSeriesName = "No Active Championship";
		
		if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("ChampionshipSubseries").Length > 0)){
			subSeriesId = PlayerPrefs.GetString("ChampionshipSubseries");
			Debug.Log(subSeriesId);
			int seriesInt = int.Parse(subSeriesId.Substring(0,1));
			int subSeriesInt = int.Parse(subSeriesId.Substring(1,2));
			subSeriesName = SeriesData.offlineSeries[seriesInt,subSeriesInt];
		}
		
		GameObject championshipUI = GameObject.Find("ActiveChampionshipName");
		TMPro.TMP_Text championshipUILabel = championshipUI.GetComponent<TMPro.TMP_Text>();
		championshipUILabel.text = subSeriesName;
	}

	void loadCurrentChampionship(){
		if(PlayerPrefs.HasKey("ChampionshipSubseries")){
			PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("ChampionshipCarTexture"));
			PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("ChampionshipCarChoice"));
			PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("ChampionshipCarSeries"));
			PlayerPrefs.SetString("ActivePath","ChampionshipRace");
			//Debug.Log("Championship Car Series is " + PlayerPrefs.GetString("ChampionshipCarSeries"));
			SceneManager.LoadScene("CircuitSelect");
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
