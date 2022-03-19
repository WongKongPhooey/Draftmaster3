using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChallengeDisplay : MonoBehaviour
{
	
	public Challenge challenge;
	
    // Start is called before the first frame update
    void Start()
    {
        //Debug.Log(challenge.challengeType);
    }
	
	public void openChallenge(){
		
		challenge = (Challenge)Resources.Load("Data/Challenges/" + this.name + "");
		
		//Debug.Log(challenge.challengeType);
		GameObject challengeDescription = GameObject.Find("ChallengeDesc");
		Text challengeDesc = challengeDescription.GetComponent<Text>();
		
		if(challenge.challengeCustomText != ""){
			challengeDesc.text = challenge.challengeCustomText;
		} else {
			challengeDesc.text = "Achieve " + challenge.challengeValue + " " + challenge.challengeType;
		}
		challengeDesc.text += "\n\nRewards " + challengeReward(challenge);
		
		GameObject challengeRewardBtn = GameObject.Find("ClaimReward");
		challengeRewardBtn.GetComponent<UIAnimate>().show();
		
		int isCompleted = 0;
		if(PlayerPrefs.HasKey("Challenge" + this.name + "Completed")){
			isCompleted = PlayerPrefs.GetInt("Challenge" + this.name + "Completed");
			//Debug.Log("Challenge" + this.name + "Completed: " + isCompleted);
			if(isCompleted == 1){
				challengeRewardBtn.GetComponent<UIAnimate>().hide();
			}
		} else {
			PlayerPrefs.SetInt("Challenge" + this.name + "Completed", 0);
			isCompleted = 0;
		}
		
		PlayerPrefs.SetString("OpenChallenge", this.name);
		
		GameObject challengePanel = GameObject.Find("ChallengePanel");
		challengePanel.GetComponent<UIAnimate>().scaleIn();
	}
	
	public string challengeReward(Challenge challenge){
		string rewardOut = "";
		switch(challenge.challengeRewardType){
			case "Gears":
				rewardOut += challenge.challengeRewardInt + " gears.";
				break;
			case "Coins":
				rewardOut += challenge.challengeRewardInt + " coins.";
				break;
			case "Parts":
				rewardOut += challenge.challengeRewardInt + " " + challenge.challengeRewardString + " parts.";
				break;
			default:
				break;
		}
		return rewardOut;
	}
}
