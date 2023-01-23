using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MomentsCriteria : MonoBehaviour
{
	public static string momentSet;
	public static Dictionary<string, string> momentsCriteria = new Dictionary<string, string>();
	public static Dictionary<string, string> completedCriteria = new Dictionary<string, string>();
    // Start is called before the first frame update
    void Awake(){
		momentSet = null;
        if(PlayerPrefs.HasKey("RaceMoment")){
			momentsCriteria.Clear();
			momentSet = PlayerPrefs.GetString("RaceMoment");
			switch(momentSet){
				case "Daytona79":
					momentsCriteria.Add("WreckStartLocationStraight","3");
					momentsCriteria.Add("WreckEndLocationCorner","3");
					momentsCriteria.Add("WreckEndLocationLessThanX","-4");
					momentsCriteria.Add("CarAvoidsWreck","43");
				break;	
			}
		}
    }

	public static string getMomentsCriteriaName(string criteriaSearchTerm){
		switch(criteriaSearchTerm){
			case "WreckStartLocationStraight":
				return "Wreck On The Back Straight";
				break;
			case "WreckEndLocationCorner":
				return "Stop In Turn - ";
				break;
			case "WreckEndLocationLessThanX":
				return "Stop In The Infield";
				break;
			case "CarAvoidsWreck":
				return " Does Not Wreck";
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
				//If on the correct straight and not on a turn, true
				if((criteriaValue == criteriaCheckA) && 
				  (criteriaCheckB == "False")){
					complete = true;
				}
				break;
			default:
				break;
		}
		if(complete == true){
			Debug.Log("Criteria Complete: " + criteriaSearchTerm + " - " + criteriaValue + "(" + criteriaCheckA + "," + criteriaCheckB + "," + criteriaCheckC + ")");
		} else {
			Debug.Log("Criteria Incomplete: " + criteriaSearchTerm + " - " + criteriaValue + "(" + criteriaCheckA + "," + criteriaCheckB + "," + criteriaCheckC + ")");
		}
		return complete;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
