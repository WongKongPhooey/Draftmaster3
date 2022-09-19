using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class MiscScripts : MonoBehaviour {

	public static bool proVersion = false;
	public static bool championshipSelector;

	void Awake(){
		if(PlayerPrefs.GetInt("ProForFree")==1){
			proVersion = true;
		}
	}

	// Use this for initialization
	public static string PositionPostfix(int position){
		
		string postfix;
		
		switch(position){
		case 1:
		case 21:
		case 31:
		case 41:
			postfix = "st";
			break;
		case 2:
		case 22:
		case 32:
		case 42:
			postfix = "nd";
			break;
		case 3:
		case 23:
		case 33:
		case 43:
			postfix = "rd";
			break;
		default:
			postfix = "th";
			break;
		}
		return position.ToString() + postfix;
	}

	public static void CheatCodes(string code){
		if(code=="AllTheCars"){
			//CarUnlocks.unlockAll();
			Debug.Log("All Cars Unlocked");
		}
		if(code=="TheJackpot"){
			if(PlayerPrefs.GetInt("JackpotUsed")!=1){
				PlayerPrefs.SetInt("PrizeMoney",PlayerPrefs.GetInt("PrizeMoney") + 10000000);
				SceneManager.LoadScene("MainMenu");
				Debug.Log("10000000 Dollars Added");
			} else {
				PlayerPrefs.SetInt("JackpotUsed",1);
				Debug.Log("Jackpot Already Used");
			}
		}
		if(code=="Billionaire"){
			PlayerPrefs.SetInt("PrizeMoney", 10000000);
			Debug.Log("10000000 Dollar Winnings");
			SceneManager.LoadScene("MainMenu");
		}
		if(code=="LetsGoPro"){
			PlayerPrefs.SetInt("ProForFree",1);
			Debug.Log("Pro Features Enabled");
			SceneManager.LoadScene("MainMenu");
		}
		if(code=="RemovePro"){
			PlayerPrefs.SetInt("ProForFree",0);
			proVersion = false;
			SceneManager.LoadScene("MainMenu");
			Debug.Log("Pro Features Disabled");
		}
		if(code=="ResetAll"){
			//CarUnlocks.resetUnlocks();
			SceneManager.LoadScene("MainMenu");
			Debug.Log("All Vehicles Reset");
		}
		if((code.Length > 2)&&(code.Substring(0,2) == "RM")){
			PlayerPrefs.DeleteKey(code.Substring(2));
			Debug.Log("Removed " + code.Substring(2) + " - Exists?: " + PlayerPrefs.HasKey(code.Substring(2)));
		}
	}
}
