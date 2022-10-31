using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceModifiers : MonoBehaviour
{
	
	public static string modifier;
	public string activeModifier;
	
    // Start is called before the first frame update
    void Awake(){
		checkModifiers();
    }

	public static void checkModifiers(){
		if(PlayerPrefs.HasKey("RaceModifier")){
			modifier = PlayerPrefs.GetString("RaceModifier");
			//Debug.Log("Modifier Active: " + modifier);
		}
		
        switch(modifier){
			case "delicate":
				Movement.delicateMod = true;
				break;
			case "invincible":
				Movement.invincibleMod = true;
				break;
			case "wallride":
				Movement.wallrideMod = true;
				break;
			default:
				break;
		}
	}

    // Update is called once per frame
    void Update(){
        //activeModifier = modifier;
    }
}
