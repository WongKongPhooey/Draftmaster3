using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimeTrial : MonoBehaviour
{
    public GameObject leaderboardTitle;
	
    // Start is called before the first frame update
    void Start()
    {
       leaderboardTitle.GetComponent<TMPro.TMP_Text>().text = "Live Time Trial - " + PlayerPrefs.GetString("LiveTimeTrial"); 
	   
	   PlayFabManager.GetLiveTimeTrialAroundPlayer();
	   PlayFabManager.GetLiveTimeTrialLeaderboard();
    }

    // Update is called once per frame
    void Update(){ 
    }
}
