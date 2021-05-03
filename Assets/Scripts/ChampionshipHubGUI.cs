using UnityEngine;
using System.Collections;

public class ChampionshipHubGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GameObject circuit;
	
	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	public static string circuitChoice;
	public static string circuitName;

	void Awake(){

		if(PlayerPrefs.HasKey("ChampionshipRound")){
			PlayerPrefs.GetInt("ChampionshipRound");
		} else {
			PlayerPrefs.SetInt("ChampionshipRound", 1);
			DriverPoints.resetPoints();
		}

		if(!PlayerPrefs.HasKey("raceSeries")){
			PlayerPrefs.SetString("raceSeries", "StockCar");
			PlayerPrefs.SetInt("FieldSize",40);
			PlayerPrefs.SetInt("NameTrim",6);
		}

		string championshipCarTex = PlayerPrefs.GetString("ChampionshipCarTexture");
		int championshipCar = PlayerPrefs.GetInt("ChampionshipCarChoice");
		PlayerPrefs.SetString("carTexture", championshipCarTex);
		PlayerPrefs.SetInt("CarChoice",championshipCar);

		switch(PlayerPrefs.GetInt("ChampionshipRound")){
		case 1:
			DriverPoints.resetPoints();
			ThunderAlley();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("ThunderAlley") as Texture;
			break;
		case 2:
			MiracleMile();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Miracle Mile") as Texture;
			break;
		case 3:
			ThePowerbowl();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("PowerBowl") as Texture;
			break;
		case 4:
			Legends500();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Legends500") as Texture;
			break;
		case 5:
			TwinLakes();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TwinLakes") as Texture;
			break;
		case 6:
			FreedomPark();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("FreedomPark") as Texture;
			break;
		case 7:
			Motorland();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Motorland") as Texture;
			break;
		case 8:
			SunsetSpeedway();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SunsetSpeedway") as Texture;
			break;
		case 9:
			SteelCity();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TwinLakes") as Texture;
			break;
		case 10:
			MountPower();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("MountPower") as Texture;
			break;
		case 11:
			UnionSpeedway();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("UnionSpeedway") as Texture;
			break;
		case 12:
			PalmBeach();
			circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("PalmBeach") as Texture;
			break;
		}

	}

	void FixedUpdate(){
		circuit.transform.Rotate(0,1.0f,0);
	}
	
	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		GUI.skin.button.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		int maxSpeed = PlayerPrefs.GetInt("SpeedOffset");
		GUI.Label(new Rect(0, 0, Screen.width, heightblock * 1.5f), "Round " + PlayerPrefs.GetInt("ChampionshipRound") + " - " + circuitName);

		string racerName = PlayerPrefs.GetString("RacerName");
		string carNumber = PlayerPrefs.GetString("carTexture").Remove(0,PlayerPrefs.GetInt("NameTrim"));
		int championshipPlace = PlayerPrefs.GetInt("CurrentChampionshipPos");
		GUI.Label(new Rect((widthblock * 10) + 20, heightblock * 2,(widthblock * 10) - 20, heightblock * 3), racerName + " - " + championshipPlace + MiscScripts.PositionPostfix(championshipPlace) + "\n" + PlayerPrefs.GetInt("ChampionshipPoints" + carNumber) + " Points");

		Vector2 pivotPoint = new Vector2(widthblock * 18, heightblock * 6);
		GUIUtility.RotateAroundPivot(90, pivotPoint);
		GUI.DrawTexture(new Rect(widthblock * 18, heightblock * 6, heightblock * 7, heightblock * 14), Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture);
		GUIUtility.RotateAroundPivot(-90, pivotPoint);

		GUI.skin.label.alignment = TextAnchor.LowerLeft;
		GUI.Label(new Rect(20, heightblock * 15.5f, widthblock * 5, heightblock * 1.5f), "Laps: " + PlayerPrefs.GetInt("RaceLaps"));
		GUI.Label(new Rect(20, heightblock * 17, widthblock * 5, heightblock * 1.5f), "Lanes: " + PlayerPrefs.GetInt("CircuitLanes"));
		GUI.Label(new Rect(20, heightblock * 18.5f, widthblock * 8, heightblock * 1.5f), "Avg Speed: " + (202 - maxSpeed) + "MpH");
		
		//if (Input.GetKeyDown(KeyCode.Escape)){
			//Application.LoadLevel("MainMenu");
		//}
		
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 17, widthblock * 6, heightblock * 2), "Next Race")){
			PlayerPrefs.SetInt("TotalStarts",PlayerPrefs.GetInt("TotalStarts") + 1);
			PlayerPrefs.SetString("nextCircuit",circuitChoice);
			PlayerPrefs.SetString("CurrentCircuit",circuitChoice);
			GetComponent<ChampionshipHubExit>().enabled = true;
			this.enabled = false;
		}
	}

	static void ThunderAlley(){
		circuitName = "Thunder Alley";
		circuitChoice = "ThunderAlley";
		PlayerPrefs.SetInt("RaceLaps",5);
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
		PlayerPrefs.SetInt("SpeedOffset",0);
		PlayerPrefs.SetInt("TotalTurns",3);
	}
	
	static void MiracleMile(){
		circuitName = "Miracle Mile";
		circuitChoice = "MiracleMile";
		PlayerPrefs.SetInt("RaceLaps",12);
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
		PlayerPrefs.SetInt("SpeedOffset",18);
		PlayerPrefs.SetInt("TotalTurns",4);
	}
	
	static void ThePowerbowl(){
		circuitName = "The Powerbowl";
		circuitChoice = "Powerbowl";
		PlayerPrefs.SetInt("RaceLaps",18);
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
		PlayerPrefs.SetInt("SpeedOffset",65);
		PlayerPrefs.SetInt("TotalTurns",2);
	}
	
	static void Legends500(){
		circuitName = "Legends 500";
		circuitChoice = "Legends500";
		PlayerPrefs.SetInt("RaceLaps",8);
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
		PlayerPrefs.SetInt("SpeedOffset",5);
		PlayerPrefs.SetInt("TotalTurns",4);
	}
	
	static void TwinLakes(){
		circuitName = "Twin Lakes";
		circuitChoice = "TwinLakes";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",300);
		PlayerPrefs.SetInt("StraightLength2",300);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",200);
		PlayerPrefs.SetInt("TurnLength2",160);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",200);
		PlayerPrefs.SetInt("SpeedOffset",25);
		PlayerPrefs.SetInt("TotalTurns",2);
	}
	
	static void FreedomPark(){
		circuitName = "Freedom Park";
		circuitChoice = "TriOval";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",350);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",300);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",110);
		PlayerPrefs.SetInt("TurnLength2",100);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",200);
		PlayerPrefs.SetInt("SpeedOffset",8);
		PlayerPrefs.SetInt("TotalTurns",3);

	}
	
	static void Motorland(){
		circuitName = "Motorland";
		circuitChoice = "Motorland";
		PlayerPrefs.SetInt("RaceLaps",7);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",30);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",30);
		PlayerPrefs.SetInt("TurnAngle1",8);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",8);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",3);
		PlayerPrefs.SetInt("TotalTurns",4);
	}
	
	static void SunsetSpeedway(){
		circuitName = "Sunset Speedway";
		circuitChoice = "SunsetSpeedway";
		PlayerPrefs.SetInt("RaceLaps",12);
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
		PlayerPrefs.SetInt("SpeedOffset",45);
		PlayerPrefs.SetInt("TotalTurns",2);
	}
	
	static void MountPower(){
		circuitName = "Mount Power";
		circuitChoice = "MountPower";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",80);
		PlayerPrefs.SetInt("StraightLength2",75);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",75);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",22);
		PlayerPrefs.SetInt("TotalTurns",4);
	}
	
	static void Route67(){
		circuitName = "Route 67";
		circuitChoice = "Route67";
		PlayerPrefs.SetInt("RaceLaps",7);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",500);
		PlayerPrefs.SetInt("StraightLength3",400);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",160);
		PlayerPrefs.SetInt("TurnLength3",20);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",6);
		PlayerPrefs.SetInt("TotalTurns",3);
	}

	static void SteelCity(){
		circuitName = "Steel City";
		circuitChoice = "SteelCity";
		PlayerPrefs.SetInt("RaceLaps",8);
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
		PlayerPrefs.SetInt("SpeedOffset",22);
		PlayerPrefs.SetInt("TotalTurns",4);
	}

	static void PalmBeach(){
		circuitName = "Palm Beach";
		circuitChoice = "PalmBeach";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",30);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",30);
		PlayerPrefs.SetInt("TurnAngle1",8);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",8);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",8);
		PlayerPrefs.SetInt("TotalTurns",4);
	}
	
	static void UnionSpeedway(){
		circuitName = "Union Speedway";
		circuitChoice = "UnionSpeedway";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",200);
		PlayerPrefs.SetInt("StraightLength2",200);
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
		PlayerPrefs.SetInt("SpeedOffset",49);
		PlayerPrefs.SetInt("TotalTurns",2);
	}
}
