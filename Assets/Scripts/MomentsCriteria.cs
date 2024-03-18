using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MomentsCriteria : MonoBehaviour
{
	public static string momentSet;
	public static Dictionary<string, string> momentsCriteria = new Dictionary<string, string>();
	public static Dictionary<string, bool> completedCriteria = new Dictionary<string, bool>();
    
	public static int totalCriteria;
	public static int completeCriteria;
	
	public static bool momentComplete;
	public static bool momentOver;
	
	static GameObject challengeWon;
	static GameObject challengeLost;
	public GameObject challengeItem;
	Transform challengesContainer;
	
	// Start is called before the first frame update
    void Awake(){
		momentSet = null;
		challengeWon = GameObject.Find("ChallengeWon");
		challengeLost = GameObject.Find("ChallengeLost");
		challengeWon.SetActive(false);
		challengeLost.SetActive(false);
		
        if(PlayerPrefs.HasKey("RaceMoment")){
			momentsCriteria.Clear();
			completedCriteria.Clear();
			totalCriteria = 0;
			completeCriteria = 0;
			momentOver = false;
			momentComplete = false;
			momentSet = PlayerPrefs.GetString("RaceMoment");
			switch(momentSet){
				case "Daytona79":
					momentsCriteria.Add("WreckStartLocationStraight","3");
					momentsCriteria.Add("WreckEndLocationCorner","3");
					momentsCriteria.Add("WreckEndLocationLessThanX","-2.2");
					momentsCriteria.Add("CarWrecks","1");
					momentsCriteria.Add("CarAvoidsWreck","43");
				break;
				case "Atlanta01":
					momentsCriteria.Add("FinishPositionLowerThan","1");
					momentsCriteria.Add("CarAvoidsWreck","24");
					momentsCriteria.Add("WinningMargin","0.02");
				break;
				case "ChastainWallride":
					momentsCriteria.Add("WreckStartLocationStraight","2");
					momentsCriteria.Add("WreckStartPositionHigherThan","9");
					momentsCriteria.Add("FinishPositionLowerThan","5");
				break;
				case "WideOpenWheels":
					momentsCriteria.Add("FinishPositionLowerThan","1");
					momentsCriteria.Add("CarAvoidsWreck","5");
				break;
				case "PhotoForThree":
					momentsCriteria.Add("FinishPositionLowerThan","1");
					momentsCriteria.Add("WinningMargin","0.05");
					momentsCriteria.Add("WinToThirdLessThan","0.1");
					momentsCriteria.Add("Top3Finish","12");
					momentsCriteria.Add("Top3FinishAlso","8");
				break;
				case "LiveMoment":
					for(int i=1;i<=5;i++){
						if(PlayerPrefs.GetString("MomentCriteria" + i) != ""){
							string criteriaList = PlayerPrefs.GetString("MomentCriteria" + i);
							string[] criteriaParts = criteriaList.Split(",");
							//Debug.Log("Live Criteria Added: " + criteriaParts[0] + " - " + criteriaParts[1]);
							momentsCriteria.Add(criteriaParts[0],criteriaParts[1]);
						}
					}
				break;
				case "":
				break;
				default:
				break;
			}
		
			challengesContainer = GameObject.Find("ChallengesContainer").transform;
			foreach(KeyValuePair<string, string> crit in momentsCriteria){
				GameObject challengeInst = Instantiate(challengeItem, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
				challengeInst.transform.SetParent(challengesContainer, false);
				challengeInst.name = crit.Key;
				
				TMPro.TMP_Text challengeName = challengeInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
				challengeName.text = getMomentsCriteriaName(crit.Key ,crit.Value);
				
				//Initialise the completion check
				RawImage challengeStatus = challengeInst.transform.GetChild(1).GetComponent<RawImage>();
				challengeStatus.texture = Resources.Load<Texture2D>("Icons/cross"); 
				completedCriteria.Add(crit.Key, false);
				totalCriteria++;
			}
		}
		//Spawn moments into the UI
    }

	public static string getMomentsCriteriaName(string criteriaSearchTerm, string criteriaValue = ""){
		switch(criteriaSearchTerm){
			case "WreckStartLocationStraight":
				return "Wreck On The Back Straight";
				break;
			case "WreckStartLocationCorner":
				return "Wreck In Turn " + criteriaValue;
				break;
			case "WreckStartPositionHigherThan":
				return "Wreck From Position " + criteriaValue + "+";
				break;
			case "WreckEndLocationCorner":
				return "Stop In Turn " + criteriaValue;
				break;
			case "WreckEndLocationLessThanX":
				return "Stop Below The Apron";
				break;
			case "FinishPositionLowerThan":
				if(criteriaValue == "1"){
					return "Win The Race";
				} else {
					return "Finish In The Top " + criteriaValue;
				}
				break;
			case "WinningMargin":
				return "Winning Margin Less Than " + criteriaValue + "s";
				break;
			case "WinToThirdLessThan":
				return "First To Third Less Than " + criteriaValue + "s";
				break;
			case "Top2Finish":
			case "Top2FinishAlso":
				return "Top 2 Finish - # " + criteriaValue;
				break;
			case "Top3Finish":
			case "Top3FinishAlso":
				return "Top 3 Finish - # " + criteriaValue;
				break;
			case "Top5Finish":
			case "Top5FinishAlso":
				return "Top 5 Finish - # " + criteriaValue;
				break;
			case "WreckTotalCars":
				return "At Least " + criteriaValue + " Cars Wreck";
				break;
			case "CarWrecks":
			case "CarWrecksAlso":
				return "#" + criteriaValue + " Wrecks";
				break;
			case "CarAvoidsWreck":
			case "CarAvoidsWreckAlso":
				return "#" + criteriaValue + " Does Not Wreck";
				break;
			case "PlayerWrecks":
				return "You Wreck";
				break;
			case "PlayerAvoidsWreck":
				return "You Do Not Wreck";
				break;
			default:
				return criteriaSearchTerm;
				break;
		}
	}

	public static bool checkMomentsCriteria(string criteriaSearchTerm, string criteriaCheckA, string criteriaCheckB = "", string criteriaCheckC = ""){
		bool complete = false;
		if(momentSet == null){
			return false;
		}
		foreach(KeyValuePair<string, string> criteria in momentsCriteria){
			if(criteria.Key == criteriaSearchTerm){
				complete = checkCriteriaCompletion(criteriaSearchTerm, criteria.Value, criteriaCheckA ,criteriaCheckB, criteriaCheckC);
				//Debug.Log("Checking Critera: " + criteriaSearchTerm);
			}
		}
		//Debug.Log("All criteria checked");
		return complete;
	}
	
	public static void checkEndCriteria(){
		if(momentSet == null){
			return;
		}
		if(momentOver == false){
			if(completeCriteria >= totalCriteria){
				momentComplete = true;
				challengeWon.SetActive(true);
				PlayerPrefs.SetInt("MomentComplete",1);
				//Debug.Log("Criteria Complete! " + completeCriteria + "/" + totalCriteria);
			} else {
				momentComplete = false;
				challengeLost.SetActive(true);
				//Debug.Log("Almost.. " + completeCriteria + "/" + totalCriteria);
				TMPro.TMP_Text challengeLostMessage = GameObject.Find("ChallengeEndMessage").GetComponent<TMPro.TMP_Text>();
				challengeLostMessage.text = "Almost.. (" + completeCriteria + "/" + totalCriteria + ")";
			}
		}
		momentOver = true;
	}
	
	public static bool checkCriteriaCompletion(string criteriaSearchTerm, string criteriaValue, string criteriaCheckA, string criteriaCheckB = "", string criteriaCheckC = ""){
		bool complete = false;
		switch(criteriaSearchTerm){
			case "WreckStartLocationStraight":
				//If on the correct straight (A) and onTurn is false (B), true
				if((criteriaValue == criteriaCheckA) && 
				  (criteriaCheckB == "False")){
					complete = true;
				}
				break;
			case "WreckEndLocationCorner":
				//If on the correct corner (A) and onTurn is true (B), true
				if((criteriaValue == criteriaCheckA) &&
				  (criteriaCheckB == "True")){
					complete = true;
				}
				break;
			case "WreckEndLocationLessThanX":
				//If Z position (A) is lower than the infield line, true
				if(float.Parse(criteriaValue) >= float.Parse(criteriaCheckA)){
					complete = true;
				}
				break;
			case "WreckStartPositionHigherThan":
				//If position at time of wreck (A) is equal or lower, true
				if(int.Parse(criteriaValue) <= int.Parse(criteriaCheckA)){
					complete = true;
				}
				break;
			case "FinishPositionLowerThan":
				//If finish position (A) is lower, true
				if(int.Parse(criteriaValue) > int.Parse(criteriaCheckA)){
					complete = true;
				}
				break;
			case "WinningMargin":
				//If finish gap to leader (A) is lower than the max, true
				//This check only triggers on the 2nd place car
				//Debug.Log("Win By Less Than: " + criteriaValue + "? " + criteriaCheckA);
				if(float.Parse(criteriaValue) >= float.Parse(criteriaCheckA)){
					complete = true;
				}
				break;
			case "WinToThirdLessThan":
				//If finish gap to leader (A) is lower than the max, true
				//This check only triggers on the 3rd place car
				Debug.Log("Beat Third Place By Less Than: " + criteriaValue + "? " + criteriaCheckA);
				if(float.Parse(criteriaValue) >= float.Parse(criteriaCheckA)){
					complete = true;
				}
				break;
			case "WreckTotalCars":
				//If more cars in wreck than the minimum, true
				if(int.Parse(criteriaCheckA) >= int.Parse(criteriaValue)){
					complete = true;
					//Debug.Log(criteriaCheckA + " Cars In Wreck ,More Than " + criteriaValue);
				}
				//Debug.Log(criteriaCheckA + " Cars In Wreck. Needed " + criteriaValue);
				break;
			case "AIMustWin":
				if((int.Parse(criteriaCheckA) == int.Parse(criteriaValue))&&
				  (int.Parse(criteriaCheckB) == 0)){
					complete = true;
				}
				break;
			case "Top2Finish":
			case "Top2FinishAlso":
				if((int.Parse(criteriaCheckA) == int.Parse(criteriaValue))&&
				  (int.Parse(criteriaCheckB) < 2)){
					complete = true;
				}
				break;
			case "Top3Finish":
			case "Top3FinishAlso":
				if((int.Parse(criteriaCheckA) == int.Parse(criteriaValue))&&
				  (int.Parse(criteriaCheckB) < 3)){
					complete = true;
				}
				break;
			case "Top5Finish":
			case "Top5FinishAlso":
				if((int.Parse(criteriaCheckA) == int.Parse(criteriaValue))&&
				  (int.Parse(criteriaCheckB) < 5)){
					complete = true;
				}
				break;
			case "CarWrecks":
			case "CarWrecksAlso":
				GameObject AICar = GameObject.Find("AICar0" + criteriaValue);
				//If wrecked is true, true
				if((AICar != null)&&(AICar.GetComponent<AIMovement>() != null)){
					if((AICar.GetComponent<AIMovement>().isWrecking == true)
					  ||(AICar.GetComponent<AIMovement>().wreckOver == true)){
						complete = true;
					}
				}
				break;
			case "CarAvoidsWreck":
			case "CarAvoidsWreckAlso":
				//If wrecked is false, true
				if((GameObject.Find("AICar0" + criteriaValue).GetComponent<AIMovement>().isWrecking == false)
				  &&(GameObject.Find("AICar0" + criteriaValue).GetComponent<AIMovement>().wreckOver == false)){
					complete = true;
				}
				break;
			case "PlayerWrecks":
				if((Movement.isWrecking == true)
				  ||(Movement.wreckOver == true)){
					complete = true;
				}
				break;
			case "PlayerAvoidsWreck":
				if((Movement.isWrecking == false)
				  &&(Movement.wreckOver == false)){
					complete = true;
				}
				break;
			default:
				break;
		}
		if(complete == true){
			updateCriteriaCompletion(criteriaSearchTerm, complete);
		} else {
			//Debug.Log("Criteria failed: " + criteriaSearchTerm + " - " + criteriaValue + "(" + criteriaCheckA + "," + criteriaCheckB + "," + criteriaCheckC + ")");
		}
		return complete;
	}
	
	public static void updateCriteriaCompletion(string criteriaSearchTerm, bool complete){
		completedCriteria[criteriaSearchTerm] = complete;
		GameObject criteriaObj = GameObject.Find(criteriaSearchTerm);
		RawImage criteriaTick = criteriaObj.transform.GetChild(1).GetComponent<RawImage>();
		criteriaTick.texture = Resources.Load<Texture2D>("Icons/tick");
		completeCriteria++;
		//Debug.Log("Criteria Complete: " + criteriaSearchTerm + " - " + complete + ". Total: " + completeCriteria);
	}

    // Update is called once per frame
    void Update(){
        
    }
}