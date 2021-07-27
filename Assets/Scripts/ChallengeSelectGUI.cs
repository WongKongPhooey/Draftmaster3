using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChallengeSelectGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GameObject circuit;
	public GameObject vehicle;

	public static bool challengeMode;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	public static int gameDifficulty;

	public static int speedFactor;

	public static string circuitChoice;

	public static string challengeTitle;
	public static string challengeDescription;
	
	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		gameDifficulty = PlayerPrefs.GetInt("Difficulty");
		circuit = GameObject.Find("Circuit");
		vehicle = GameObject.Find("Vehicle");
		LastToFirstLaps(circuit,vehicle);
	}
	
	void FixedUpdate(){
	}

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		GUI.skin.button.fontSize = 72 / FontScale.fontScale;
		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, widthblock * 9, Screen.height), scrollPosition, new Rect(0, 0, widthblock, Screen.height * 1.5f));

		if (GUI.Button(new Rect(widthblock / 2, heightblock, widthblock * 7, heightblock * 2), "Last To First")){
			LastToFirstLaps(circuit,vehicle);
		}

		/*if (GUI.Button(new Rect(widthblock / 2, heightblock * 4, widthblock * 7, heightblock * 2), "No Fuel")){
			NoFuel(circuit,vehicle);
		}

		if (GUI.Button(new Rect(widthblock / 2, heightblock * 7, widthblock * 7, heightblock * 2), "Late Push")){
			LatePush(circuit,vehicle);
		}

		if (GUI.Button(new Rect(widthblock / 2, heightblock * 10, widthblock * 7, heightblock * 2), "Team Player")){
			TeamPlayer(circuit,vehicle);
		}*/

		if (GUI.Button(new Rect(widthblock / 2, heightblock * 4, widthblock * 7, heightblock * 2), "Clean Break")){
			CleanBreak(circuit,vehicle);
		}

		/*if (GUI.Button(new Rect(widthblock / 2, heightblock * 16, widthblock * 7, heightblock * 2), "Traffic Jam")){
			TrafficJam(circuit,vehicle);
		}*/

		if (GUI.Button(new Rect(widthblock / 2, heightblock * 7, widthblock * 7, heightblock * 2), "Photo Finish")){
			PhotoFinish(circuit,vehicle);
		}
		
		/*if (GUI.Button(new Rect(widthblock / 2, heightblock * 22, widthblock * 7, heightblock * 2), "All Wound Up")){
			AllWoundUp(circuit,vehicle);
		}*/

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		GUI.Label(new Rect(widthblock / 2, heightblock * 10, widthblock * 7, heightblock * 3), "More reworked challenges coming soon!");
		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		
		GUI.EndScrollView();

		//GUI.skin.label.fontSize = 24 / FontScale.fontScale;
		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect((widthblock * 10) - 20, 10, (widthblock * 10), heightblock * 2), challengeTitle);
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 10, heightblock * 3, widthblock * 9, heightblock * 8), challengeDescription);
		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		
		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
		
		if (GUI.Button(new Rect(widthblock * 15, Screen.height - (heightblock * 3), widthblock * 3, heightblock * 2), "Race")){
			challengeMode = true;
			PlayerPrefs.SetInt("TotalStarts",PlayerPrefs.GetInt("TotalStarts") + 1);
			SceneManager.LoadScene(circuitChoice);
		}
		if (GUI.Button(new Rect(widthblock * 11, Screen.height - (heightblock * 3), widthblock * 3, heightblock * 2), "Back")){
			SceneManager.LoadScene("MainMenu");
		}
	}
	
	public static void LastToFirstLaps(GameObject circuit,GameObject vehicle){
		circuitChoice = "Talladega";
		PlayerPrefs.SetInt("RaceLaps",188);
		PlayerPrefs.SetInt("StartingLap",163);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",200);
		PlayerPrefs.SetInt("StraightLength2",500);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",150);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",60);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",8);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",2 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Plate");
		PlayerPrefs.SetString("ChallengeType","LastToFirstLaps");
		PlayerPrefs.SetString("carTexture", "cup20livery18");
		PlayerPrefs.SetInt("CarChoice",18);
		challengeTitle = "Back To The Front";
		challengeDescription = "After a puncture and late caution, KB18 has been shuffled to the back of the pack. Reach the front in as few laps/metres as possible to win big. Rewards are given out weekly for the fastest runs!";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("ThunderAlley") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}

	public static void NoFuel(GameObject circuit,GameObject vehicle){
		circuitChoice = "Legends500";
		PlayerPrefs.SetInt("RaceLaps",100);
		PlayerPrefs.SetInt("StartingLap",98);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",400);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",400);
		PlayerPrefs.SetInt("StraightLength4",100);
		PlayerPrefs.SetInt("TurnLength1",90);
		PlayerPrefs.SetInt("TurnLength2",90);
		PlayerPrefs.SetInt("TurnLength3",90);
		PlayerPrefs.SetInt("TurnLength4",90);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",250);
		PlayerPrefs.SetInt("SpeedOffset",0 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("ChallengeType","NoFuel");
		PlayerPrefs.SetString("raceSeries", "StockCar");
		PlayerPrefs.SetString("carTexture", "livery24");
		PlayerPrefs.SetInt("CarChoice",24);
		challengeTitle = "No Fuel";
		challengeDescription = "Gordon missed the last set of stops to hit the front but he's nearly out of gas! Coast to the line as high up the field as possible for a big payout.";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Legends500") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}

	static void LatePush(GameObject circuit,GameObject vehicle){
		circuitChoice = "SunsetSpeedway";
		PlayerPrefs.SetInt("RaceLaps",150);
		PlayerPrefs.SetInt("StartingLap",145);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",80);
		PlayerPrefs.SetInt("StraightLength2",80);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",45 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("ChallengeType","LatePush");
		PlayerPrefs.SetString("raceSeries", "IndyCar");
		PlayerPrefs.SetString("carTexture", "indylivery0");
		PlayerPrefs.SetInt("CarChoice",0);
		challengeTitle = "Late Push";
		challengeDescription = "Andretti has battled through the field all race but with 5 laps to go only a top 3 finish will keep him in the championship hunt. The pressure is on.";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SunsetSpeedway") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}

	static void TeamPlayer(GameObject circuit,GameObject vehicle){
		circuitChoice = "MiracleMile";
		PlayerPrefs.SetInt("RaceLaps",200);
		PlayerPrefs.SetInt("StartingLap",185);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",18 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("ChallengeType","TeamPlayer");
		PlayerPrefs.SetString("raceSeries", "IndyCar");
		PlayerPrefs.SetString("carTexture", "indylivery6");
		PlayerPrefs.SetInt("CarChoice",6);
		PlayerPrefs.SetInt("DraftPercent",0);
		challengeTitle = "Team Player";
		challengeDescription = "Your team mate in the No.2 car can win the title, but only if you push him to the top 3! Stay in his draft for at least 30% of the laps.";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Miracle Mile") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}

	static void CleanBreak(GameObject circuit,GameObject vehicle){
		circuitChoice = "Michigan";
		PlayerPrefs.SetInt("RaceLaps",156);
		PlayerPrefs.SetInt("StartingLap",154);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",10);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",170);
		PlayerPrefs.SetInt("TurnLength4",10);
		PlayerPrefs.SetInt("TurnAngle1",16);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",16);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",3 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
		PlayerPrefs.SetString("ChallengeType","CleanBreak");
		PlayerPrefs.SetString("carTexture", "cup20livery22");
		PlayerPrefs.SetInt("CarChoice",22);
		challengeTitle = "Fresh Air";
		challengeDescription = "Win in Michigan with the fastest car in the field. Slingshot past the rest and win by the biggest margin possible. Rewards are handed out weekly for the biggest winning margins!";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Michigan") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}

	static void TrafficJam(GameObject circuit,GameObject vehicle){
		circuitChoice = "Powerbowl";
		PlayerPrefs.SetInt("RaceLaps",200);
		PlayerPrefs.SetInt("StartingLap",150);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",100);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",65 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("ChallengeType","TrafficJam");
		PlayerPrefs.SetString("raceSeries", "StockCar");
		PlayerPrefs.SetString("carTexture", "livery67");
		PlayerPrefs.SetInt("CarChoice",67);
		challengeTitle = "Traffic Jam";
		challengeDescription = "Battle and barge your way from the back to the front of a packed field on the tightest circuit on the calendar. Within 50 laps.";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Powerbowl") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}

	static void PhotoFinish(GameObject circuit,GameObject vehicle){
		circuitChoice = "Darlington";
		PlayerPrefs.SetInt("RaceLaps",228);
		PlayerPrefs.SetInt("StartingLap",223);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",0);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",250);
		PlayerPrefs.SetInt("TurnLength1",45);
		PlayerPrefs.SetInt("TurnLength2",110);
		PlayerPrefs.SetInt("TurnLength3",45);
		PlayerPrefs.SetInt("TurnLength4",160);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",22 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Alt");
		PlayerPrefs.SetString("ChallengeType","PhotoFinish");
		PlayerPrefs.SetString("raceSeries", "StockCar");
		PlayerPrefs.SetString("carTexture", "cup20livery19");
		PlayerPrefs.SetInt("CarChoice",19);
		challengeTitle = "Photo Finish";
		challengeDescription = "The pace is with you and the win is within reach, but the sponsors want a close finish. Win by no more than a car length.";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Motorland") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}
	
	static void AllWoundUp(GameObject circuit,GameObject vehicle){
		circuitChoice = "SteelCity";
		PlayerPrefs.SetInt("RaceLaps",147);
		PlayerPrefs.SetInt("StartingLap",139);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",0);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",250);
		PlayerPrefs.SetInt("TurnLength1",45);
		PlayerPrefs.SetInt("TurnLength2",110);
		PlayerPrefs.SetInt("TurnLength3",45);
		PlayerPrefs.SetInt("TurnLength4",160);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",22 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("ChallengeType","AllWoundUp");
		PlayerPrefs.SetString("raceSeries", "Truck");
		PlayerPrefs.SetString("carTexture", "trucklivery41");
		PlayerPrefs.SetInt("CarChoice",41);
		challengeTitle = "All Wound Up";
		challengeDescription = "After 147 laps, bitter rivalries will be formed. Make at least 1 rival by hitting an opponent 3 times and still take victory.";
		circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TwinLakes") as Texture;
		vehicle.GetComponent<Renderer>().material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
	}
}
