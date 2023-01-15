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
        if(PlayerPrefs.HasKey("RaceMoment")){
			momentSet = PlayerPrefs.GetString("RaceMoment");
			switch(momentSet){
				case "Daytona79":
					momentsCriteria.Add("WreckStartLocationStraight","2");
					momentsCriteria.Add("WreckEndLocationCorner","3");
					momentsCriteria.Add("WreckEndLocationLessThanX","-4");
				break;	
			}
		}
    }

	public static void checkMomentsCriteria(string criteriaSearchTerm, string criteriaValue){
		if(momentSet == null){
			return;
		}
		foreach(KeyValuePair<string, string> criteria in momentsCriteria){
			if(criteria.Key == criteriaSearchTerm){
				checkCriteriaCompletion(criteriaSearchTerm, criteriaValue);
			}
		}
		Debug.Log("All criteria checked");
	}
	
	public static bool checkCriteriaCompletion(string criteriaSearchTerm, string criteriaValue){
		Debug.Log("Is " + criteriaSearchTerm + " criteria met?");
		return false;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
