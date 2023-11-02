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
	
	public void endChampionship(string seriesPrefix){
		PlayerPrefs.SetInt("ChampionshipReward",1);
		if(ModData.isModSeries(seriesPrefix) == true){
			SceneManager.LoadScene("Menus/MainMenu");
		} else {
			SceneManager.LoadScene("Menus/RaceRewards");
		}
	}
}
