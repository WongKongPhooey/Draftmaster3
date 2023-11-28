using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HallOfFame : MonoBehaviour
{	
	public GameObject title;
	public string filterName;
	public string activeFilter;
	
    // Start is called before the first frame update
    void Start(){
	   filterName = "All Time Most Wins";
	   activeFilter = "AllTimeMostWins";
       title.GetComponent<TMPro.TMP_Text>().text = "Hall Of Fame - " + filterName; 
	   
	   PlayFabManager.GetRecordAroundPlayer(activeFilter);
	   PlayFabManager.GetRecordLeaderboard(activeFilter);
    }

    // Update is called once per frame
    void Update(){ 
    }
}
