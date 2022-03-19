using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClaimReward : MonoBehaviour {

	Challenge challenge;

	public void challengeReward(){
		
		string openChallenge = PlayerPrefs.GetString("OpenChallenge");
		
		//Debug.Log(openChallenge);
		
		challenge = (Challenge)Resources.Load("Data/Challenges/" + openChallenge + "");
		
		switch(challenge.challengeRewardType){
			case "Gears":
				int totalGears = PlayerPrefs.GetInt("Gears");
				PlayerPrefs.SetInt("Gears", totalGears + challenge.challengeRewardInt);
				break;
				
			case "Coins":
				int totalMoney = PlayerPrefs.GetInt("PrizeMoney");
				PlayerPrefs.SetInt("PrizeMoney", totalMoney + challenge.challengeRewardInt);
				break;
			
			case "Parts":
				//PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + multiplier);
				break;
			default:
				break;
		}
		PlayerPrefs.SetInt("Challenge" + openChallenge + "Completed", 1);
		//Debug.Log("Challenge " + openChallenge + "Completed.");
	}
}
