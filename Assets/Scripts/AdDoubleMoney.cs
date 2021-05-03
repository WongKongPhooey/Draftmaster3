using UnityEngine;
//using UnityEngine.Advertisements;
using System.Collections;
using UnityEngine.SceneManagement;

public class AdDoubleMoney : MonoBehaviour {

	public GUISkin eightBitSkin;
		
	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	int playerMoney;
	int moneyCount;
	int raceWinnings;

	public bool proVersion;

	public static bool advertCompleted;
	public static bool advertSkipped;
	public static bool advertFailed;
	public static bool moneyMade;

	public static int sponsorChoice;

	public string[] sponsorMessages = new string[3]; 
	public string[] sponsorLinks = new string[3]; 

	void Awake(){

		proVersion = MiscScripts.proVersion;

		advertCompleted = false;

		sponsorMessages[0] = "The race sponsors want you to promote their Facebook page! Visit the page to double your race winnings. Why not give them a Like too!";
		sponsorMessages[1] = "Enjoying Draftmaster? Why not visit the developer's Twitter page to double your race winnings. Give them a Follow too!";
		sponsorMessages[2] = "Want to share your best Draftmaster experiences? Why not visit the Reddit page and double your race winnings too!";

		sponsorLinks[0] = "https://www.facebook.com/DraftmasterGame/";
		sponsorLinks[1] = "https://twitter.com/DraftmasterGame";
		sponsorLinks[2] = "https://www.reddit.com/r/RacingWithDraftmaster/";

		sponsorChoice = Random.Range(0,3);

		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PlayerPrefs.GetInt("raceWinnings");
	}

	void FixedUpdate(){
		if((advertCompleted == true)||(proVersion == true)){
			if(moneyCount < raceWinnings){
				moneyCount += 1000;
			} else {
				moneyCount = raceWinnings;
			}
		}
	}

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		
		GUI.Label(new Rect(widthblock * 3, heightblock * 4, widthblock * 14, heightblock * 6), sponsorMessages[sponsorChoice]);

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;

		GUI.Label(new Rect(widthblock * 3, heightblock * 15, widthblock * 14, heightblock * 4), "Complete the task to receive an extra $" + PlayerPrefs.GetInt("raceWinnings") + ".");

		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if (GUI.Button(new Rect(widthblock * 2, heightblock * 12, widthblock * 7, heightblock * 2), "Double My Winnings")){
			/*Advertisement.Show(null, new ShowOptions {
				resultCallback = result => {
					//Debug.Log(result.ToString());
					if(result == ShowResult.Finished){
						moneyMade = true;
						PlayerPrefs.SetInt("PrizeMoney", playerMoney + raceWinnings);
						advertSkipped = false;
						advertFailed = false;
					} else {
						moneyMade = false;
						if(result == ShowResult.Failed){
							advertFailed = true;
						}
					}
					advertCompleted = true;
				}
			});*/
			Application.OpenURL(sponsorLinks[sponsorChoice]);
			moneyMade = true;
			PlayerPrefs.SetInt("PrizeMoney", playerMoney + raceWinnings);
			advertSkipped = false;
			advertFailed = false;
			advertCompleted = true;
		}

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 12, widthblock * 7, heightblock * 2), "No Thanks")){
			Application.LoadLevel("MainMenu");
		}
	}
}
