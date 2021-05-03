using UnityEngine;
//using UnityEngine.Advertisements;
using System.Collections;
using UnityEngine.SceneManagement;

public class RaceBuildupGUI : MonoBehaviour {

	public static string[] headlines = new string[10];

	public GUISkin eightBitSkin;
	public Texture2D commentatorBG;

	/*
	string outputtext;
	int counter;
	int interval;

	public int positionRand;
	public int positionPoints;
	public string positionDriver;
	public Texture positionTexture;

	public string rivalDriver;
	public string playerName;

	public int nextRound;
	public string nextCircuit;
	public string lastCircuit;

	public int headlineRand;
	*/

	void Awake(){

		//if (Advertisement.isSupported) {
		//	Advertisement.Initialize("48229");
		//}

		SceneManager.LoadScene("ChampionshipHub");

		/*
		positionRand = Random.Range(1,20);
		positionPoints = PlayerPrefs.GetInt("ChampionshipPoints" + positionRand);
		positionDriver = DriverNames.driverNames[positionRand];
		positionTexture = Resources.Load("livery" + positionRand) as Texture;

		playerName = PlayerPrefs.GetString("RacerName");

		rivalDriver = DriverNames.driverNames[positionRand + 1];

		nextRound = PlayerPrefs.GetInt("ChampionshipRound");
		nextCircuit = CircuitNames.circuitNames[nextRound];

		if(nextRound != 1){
			lastCircuit = CircuitNames.circuitNames[nextRound - 1];
		} else {
			lastCircuit = "";
		}

		//headlineRand = Random.Range(1,7);
		headlines[0] = "Welcome to Draftmaster TV and the first round of the new season! " + nextCircuit + " always provides a thrilling start to the year and an early indication on who can take the title. Our pick? " + positionDriver + ". But don't rule out " + playerName + ".";
		headlines[1] = "Round " + nextRound + " of the season brings us to " + nextCircuit + ", a big mental test for all of the drivers. " + positionDriver + " will be looking to build on his " + positionPoints + " points after admitting that he expected more from the first " + (nextRound - 1) + " rounds.";
		headlines[2] = "Welcome to round " + nextRound + " of the season, live from " + nextCircuit + ", an ever popular stop on the season calendar. " + positionDriver + " will be aiming for a strong finish after " + lastCircuit + " left him on a surprising total of " + positionPoints + " points.";
		headlines[3] = "Welcome to Draftmaster TV broadcasting live from " + nextCircuit + " and round " + nextRound + " of the season. The battle between " + positionDriver + " and " + rivalDriver + " grabs the spotlight after the pair collided at the last race at " + lastCircuit + ".";
		headlines[4] = "The season is well underway and " + nextCircuit + " will provide the action in round " + nextRound + ". " + positionDriver + " will be looking for a less eventful race today after a late caution shuffled the results at " + lastCircuit + ". He's on " + positionPoints + " points.";
		headlines[5] = "The Draftmaster circus rolls into town and round " + nextRound + " of the season at " + nextCircuit + ". " + positionDriver + " has been so far fairly inconsistent but brings an improved aero package looking to stabilise both his car and his fortunes.";
		headlines[6] = nextCircuit + ", the home of motorsport in this part of the country, plays host to round " + nextRound + ". A home race for both " + positionDriver + " and " + rivalDriver + ", both drivers come into this round in form after strong races at " + lastCircuit + ".";
		headlines[7] = "This is it. The grand finale at " + nextRound + ". Who will become this season's draft master?";

		headlineRand = 7;

		if(nextRound == 1){
			headlineRand = 0;
		}

		//outputtext = "";
		outputtext = headlines[headlineRand];
		counter = 0;
		interval = 0;
		*/
	}

	void FixedUpdate(){
		/*
		interval++;
		
		if ((interval >=1)&&(counter < headlines[headlineRand].Length)){
			outputtext = outputtext + headlines[headlineRand][counter];
			counter++;
			interval = 0;
		}

		if(interval >= 10){
			if(Input.GetMouseButtonDown(0)){
				SceneManager.LoadScene("ChampionshipHub");
			}
		}

		if(interval >= 600){
			SceneManager.LoadScene("ChampionshipHub");
		}
		*/
	}

	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.white;

		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		/*
		GUI.DrawTexture(new Rect(0, Mathf.Round(Screen.height - (heightblock * 16)),Screen.width, heightblock * 16), commentatorBG, ScaleMode.StretchToFill);

		GUI.DrawTexture(new Rect(widthblock * 11, heightblock * 9, heightblock * 4, heightblock * 8), positionTexture);

		GUI.Label(new Rect(widthblock, 20, widthblock * 18, heightblock * 6), outputtext);

		GUI.Label(new Rect((widthblock * 12) + (heightblock * 4), heightblock * 9, widthblock * 5, heightblock * 3), "DTV's 1 To Watch");

		GUI.Label(new Rect((widthblock * 12) + (heightblock * 4), heightblock * 12, widthblock * 5, heightblock * 2), positionDriver);

		GUI.Label(new Rect((widthblock * 12) + (heightblock * 4), heightblock * 14, widthblock * 5, heightblock * 3), positionPoints + " Points");
		*/
	}
}
