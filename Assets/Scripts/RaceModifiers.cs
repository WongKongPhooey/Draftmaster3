using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceModifiers : MonoBehaviour
{
	
	public static string modifier;
	
    // Start is called before the first frame update
    void Start(){
		modifier = "";
		checkModifiers();
    }

	public static void resetModifiers(){
		modifier = "";
		Movement.delicateMod = false;
		Movement.invincibleMod = false;
		Movement.bulldozerMod = false;
		Movement.suddenshowerMod = false;
		Movement.wallrideMod = false;
	}

	public static void checkModifiers(){
		resetModifiers();
		
		if(PlayerPrefs.HasKey("RaceModifier")){
			modifier = PlayerPrefs.GetString("RaceModifier");
			//Debug.Log("Modifier Active: " + modifier);
		}
		
		//Live Modifier overwrites if necessary
		if(PlayerPrefs.HasKey("LiveMomentMods")){
			//If current event matches the Live Moment ID
			if(PlayerPrefs.GetString("CurrentSeriesIndex") == "49EVENT"){
				modifier = PlayerPrefs.GetString("LiveMomentMods");
				//Debug.Log("Live Modifier Active: " + modifier);
			}
			//Debug.Log("Live Modifier Not A Match");
		}
		
        switch(modifier){
			case "delicate":
				Movement.delicateMod = true;
				break;
			case "invincible":
				Movement.invincibleMod = true;
				break;
			case "bulldozer":
				Movement.bulldozerMod = true;
				break;
			case "suddenshower":
				Movement.suddenshowerMod = true;
				break;
			case "wallride":
				Movement.wallrideMod = true;
				break;
			default:
				Movement.delicateMod = false;
				Movement.invincibleMod = false;
				Movement.bulldozerMod = false;
				Movement.suddenshowerMod = false;
				Movement.wallrideMod = false;
				break;
		}
	}
}
