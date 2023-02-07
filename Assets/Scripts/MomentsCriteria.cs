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
	
	public static GameObject challengeEndState;
	public GameObject challengeItem;
	Transform challengesContainer;
	
	// Start is called before the first frame update
    void Awake(){
		momentSet = null;
		challengeEndState = GameObject.Find("ChallengeEndState");
		challengeEndState.SetActive(false);
		
        if(PlayerPrefs.HasKey("RaceMoment")){
			momentsCriteria.Clear();
			completedCriteria.Clear();
			totalCriteria = 0;
			completeCriteria = 0;
			momentComplete = false;
			momentSet = PlayerPrefs.GetString("RaceMoment");
			switch(momentSet){
				case "Daytona79":
					momentsCriteria.Add("WreckStartLocationStraight","3");
					momentsCriteria.Add("WreckEndLocationCorner","3");
					momentsCriteria.Add("WreckEndLocationLessThanX","-3.5");
					momentsCriteria.Add("CarWrecks","1");
					momentsCriteria.Add("CarAvoidsWreck","43");
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
			case "WreckEndLocationCorner":
				return "Stop In Turn " + criteriaValue;
				break;
			case "WreckEndLocationLessThanX":
				return "Stop In The Infield";
				break;
			case "CarWrecks":
				return "#" + criteriaValue + " Wrecks";
				break;
			case "CarAvoidsWreck":
				return "#" + criteriaValue + " Does Not Wreck";
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
			case "CarWrecks":
				//If wrecked is true, true
				if((GameObject.Find("AICar0" + criteriaValue).GetComponent<AIMovement>().isWrecking == true)
				  ||(GameObject.Find("AICar0" + criteriaValue).GetComponent<AIMovement>().wreckOver == true)){
					complete = true;
				}
				break;
			case "CarAvoidsWreck":
				//If wrecked is false, true
				if((GameObject.Find("AICar0" + criteriaValue).GetComponent<AIMovement>().isWrecking == false)
				  &&(GameObject.Find("AICar0" + criteriaValue).GetComponent<AIMovement>().wreckOver == false)){
					complete = true;
				}
				break;
			default:
				break;
		}
		if(complete == true){
			updateCriteriaCompletion(criteriaSearchTerm, complete);
		} else {
			Debug.Log("Criteria failed: " + criteriaSearchTerm + " - " + criteriaValue + "(" + criteriaCheckA + "," + criteriaCheckB + "," + criteriaCheckC + ")");
		}
		return complete;
	}
	
	public static void updateCriteriaCompletion(string criteriaSearchTerm, bool complete){
		completedCriteria[criteriaSearchTerm] = complete;
		GameObject criteriaObj = GameObject.Find(criteriaSearchTerm);
		RawImage criteriaTick = criteriaObj.transform.GetChild(1).GetComponent<RawImage>();
		criteriaTick.texture = Resources.Load<Texture2D>("Icons/tick");
		completeCriteria++;
		Debug.Log("Criteria Complete: " + criteriaSearchTerm + " - " + complete);
		
		if(completeCriteria >= totalCriteria){
			momentComplete = true;
			challengeEndState.SetActive(true);
			Debug.Log("Moment Complete!");
		}
	}

    // Update is called once per frame
    void Update(){
        
    }
}