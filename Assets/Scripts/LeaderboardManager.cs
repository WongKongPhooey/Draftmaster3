using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardManager : MonoBehaviour
{
	
	public Text leaderboardTitle;
	public string circuitName;
	
    // Start is called before the first frame update
    void Start()
    {
	   circuitName = PlayerPrefs.GetString("CurrentCircuit");
       leaderboardTitle.text = "Fastest Lap - " + circuitName; 
	   
	   PlayFabManager.GetLeaderboard(circuitName);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
