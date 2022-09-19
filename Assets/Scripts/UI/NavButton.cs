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
		SceneManager.LoadScene(sceneName);
	}
	
	public void endChampionship(){
		PlayerPrefs.SetInt("ChampionshipReward",1);
		SceneManager.LoadScene("Menus/RaceRewards");
	}
}
