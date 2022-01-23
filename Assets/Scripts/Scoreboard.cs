using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

public class Scoreboard : MonoBehaviour {

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;

	public static string seriesPrefix;

	public string circuitName;

	public static int fieldSize;

	public static GameObject playerCar;
	public static int playerCarNum;
	public static GameObject playerTwoCar;
	public static float playerPosition;
	public static float playerTwoZPosition;
	public static int position;
	public static int playerTwoPosition;
	public static int championshipPosition;
	public static int playerPoints;
	
	public static bool carsTagged;
	
	public static int playerOneNum;
	public static int playerTwoNum;
	
	public static bool multiplayer;
	public static string liveryName;

	public static float leaderDist;
	public static float twoPlayerDist;

	public int speedOffset;

	public static GameObject[] carsArray = new GameObject[45];
	public static float[] carPositions = new float[45];
	public static string[] carNames = new string[45];
	public static string[] carNumber = new string[45];
	public static string[] driverNames = new string[45];
	public static float[] carDist = new float[45];
	public static List<GameObject> entrantList = new List<GameObject>();

	public static GameObject[] cautionCarsArray = new GameObject[45];
	public static float[] cautionCarPositions = new float[45];
	public static string[] cautionCarNames = new string[45];

	public static string[] carChampNames = new string[45];
	public static int[] carChampPoints = new int[45];
	
	public static float[] orderedPositions = new float[45];
	public static string[] orderedNames = new string[45];

	public static int totalCarWins;
	public static int totalCarTopFives;

	public GUISkin eightBitSkin;
	public GUISkin scoreboardGUI;
	
	void Awake (){
		
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		
		seriesPrefix = PlayerPrefs.GetString("carSeries");
		
		circuitName = PlayerPrefs.GetString("CurrentCircuit");
		
		int i=0;
		carsTagged = false;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		fieldSize = carsArray.Length;
		playerCarNum = PlayerPrefs.GetInt("CarChoice");
		
		speedOffset = PlayerPrefs.GetInt("SpeedOffset");
		
		if(carsArray.Length > 0){
			foreach (GameObject car in carsArray) {
				carPositions[i] = car.transform.position.z;
				carNames[i] = car.transform.name;
				carNumber[i] = Regex.Replace(carNames[i], "[^0-9]+$", "");
				driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				entrantList.Add(car);
				i++;
			}
			carsTagged = true;
		}
		entrantList.Add(playerCar);

		updateScoreboard();
	}
	
	void Update(){
		
		//Will run until the cars have instantiated
		if(carsTagged == false){
			
			int i=0;
			
			carsArray = GameObject.FindGameObjectsWithTag("AICar");
			
			if(carsArray.Length > 0){
				foreach (GameObject car in carsArray) {
					carPositions[i] = car.transform.position.z;
					carNames[i] = car.transform.name;
					carNumber[i] = Regex.Replace(carNames[i], "[^0-9]", "");
					driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
					entrantList.Add(car);
					i++;
					//Debug.Log("Added car " + i);
				}
				carsTagged = true;
			}
			entrantList.Add(playerCar);
		}
	}
	
	public static void updateScoreboard(){
		playerPosition = playerCar.transform.position.z;
		
		entrantList.Clear();
		
		int j=0;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		if(carsArray.Length > 0){
			foreach (GameObject car in carsArray) {
				entrantList.Add(car);
				j++;
			}
			carsTagged = true;
		}
		entrantList.Add(playerCar);
		
		//Sort by z axis position
        entrantList.Sort(delegate(GameObject a, GameObject b) {
		  return (a.transform.position.z).CompareTo(b.transform.position.z);
		});
		//Reverse the sort
		entrantList.Reverse(); 
		
		//Debug.Log(entrantList.Count + " cars to sort.");
		
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				position = i;
				carNames[i] = playerCar.transform.name;
				carNumber[i] = "" + playerCarNum + "";
				driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
			} else {
				carNames[i] = "" + entrantList[i].name;
				carNumber[i] = Regex.Replace(carNames[i], "[^0-9]", "");
				driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				carDist[i] = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				carDist[i] = carDist[i] / 25;
			}
			//Debug.Log(i + ": " + driverNames[i]);
		}
	}

	public static int checkPlayerPosition(){
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				return i;
			}
		}
		return 0;
	}

	public static int checkSingleCarPosition(string thisCarName){
		if(thisCarName == ""){
			return 9999;
		}
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == thisCarName){
				//Debug.Log("Car #" + thisCarName + " is in pos " + i);
				return i;
			}
		}
		return 9999;
	}

	public static void checkFinishPositions(){
		playerPosition = playerCar.transform.position.z;
		
		entrantList.Clear();
		
		int j=0;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		if(carsArray.Length > 0){
			foreach (GameObject car in carsArray) {
				entrantList.Add(car);
				j++;
			}
			carsTagged = true;
		}
		entrantList.Add(playerCar);
		
		//Sort by z axis position
        entrantList.Sort(delegate(GameObject a, GameObject b) {
		  return (a.transform.position.z).CompareTo(b.transform.position.z);
		});
		//Reverse the sort
		entrantList.Reverse(); 
		
		//Debug.Log(entrantList.Count + " cars to sort.");
		
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				position = i;
				carNames[i] = playerCar.transform.name;
				carNumber[i] = "" + playerCarNum + "";
				driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
			} else {
				carNames[i] = "" + entrantList[i].name;
				carNumber[i] = Regex.Replace(carNames[i], "[^0-9]", "");
				driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				carDist[i] = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				carDist[i] = carDist[i] / 25;
			}
			//Debug.Log(i + ": " + driverNames[i]);
		}
		
		if(position == 0){
			PlayerPrefs.SetInt("TotalWins",PlayerPrefs.GetInt("TotalWins") + 1);
			if(PlayerPrefs.HasKey("TotalWins" + seriesPrefix + playerCarNum)){
				totalCarWins = PlayerPrefs.GetInt("TotalWins" + seriesPrefix + playerCarNum);
				PlayerPrefs.SetInt("TotalWins" + seriesPrefix + playerCarNum,totalCarWins + 1);
			} else {
				PlayerPrefs.SetInt("TotalWins" + seriesPrefix + playerCarNum, 1);
			}
		}
		if(position <= 4){
			PlayerPrefs.SetInt("TotalTop5s",PlayerPrefs.GetInt("TotalTop5s") + 1);
			if(PlayerPrefs.HasKey("TotalWins" + seriesPrefix + playerCarNum)){
				totalCarTopFives = PlayerPrefs.GetInt("TotalTop5s" + seriesPrefix + playerCarNum);
				PlayerPrefs.SetInt("TotalTop5s" + seriesPrefix + playerCarNum,totalCarTopFives + 1);
			} else {
				PlayerPrefs.SetInt("TotalTop5s" + seriesPrefix + playerCarNum, 1);
			}
		}
	}

	void OnGUI(){
		GUI.skin = eightBitSkin;

		//Timing screen
		GUI.Box(new Rect(0,0,Screen.width, heightblock * 2), "");
		
		GUI.skin.label.normal.textColor = Color.white;
		GUI.skin.label.fontSize = 40 / FontScale.fontScale;
	
		GUI.DrawTexture(new Rect((widthblock * 1) + 20, 10, (widthblock * 3f) - 40, (heightblock * 2f) - 20), Resources.Load("SeriesSponsor") as Texture, ScaleMode.ScaleToFit);
	
		//Laps and Info
		GUI.Label(new Rect(widthblock * 4, 0, widthblock * 3, heightblock * 1f), "LAP " + CameraRotate.lap + " of " + PlayerPrefs.GetInt("RaceLaps"));	
		GUI.Label(new Rect(widthblock * 8, 0, widthblock * 3, heightblock * 1f), circuitName.ToUpper());
				
		//Drivers loop start
		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 4, heightblock * 1f, widthblock * 4, heightblock * 1), 1 + " " + driverNames[0] + "");
		
		//Note* Pos starts at index 0, not 1
		if(position <= 2){
			//Just show the top 4
			GUI.Label(new Rect(widthblock * 8, heightblock * 1f, widthblock * 4, heightblock * 1), "2" + " " + driverNames[1] + "(+" + carDist[1].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 12, heightblock * 1f, widthblock * 4, heightblock * 1), "3" + " " + driverNames[2] + "(+" + carDist[2].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 16, heightblock * 1f, widthblock * 4, heightblock * 1), "4" + " " + driverNames[3] + "(+" + carDist[3].ToString("F3") + ")");
		} else {
			//Player Position
			GUI.Label(new Rect(widthblock * 8, heightblock * 1f, widthblock * 6, heightblock * 1), position + " " + driverNames[position - 1] + "(+" + carDist[position - 1].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 12, heightblock * 1f, widthblock * 6, heightblock * 1), (position + 1) + " " + driverNames[position] + "(+" + carDist[position].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 16, heightblock * 1f, widthblock * 6, heightblock * 1), (position + 2) + " " + driverNames[position + 1] + "(+" + carDist[position + 1].ToString("F3") + ")");
		}
		
		//Lap timer
		GUI.Box(new Rect(Screen.width - (widthblock * 4),heightblock * 3,widthblock * 3.5f, heightblock * 3.5f), "");
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 3.25f, widthblock * 3f, heightblock * 1f), "Spd:" + (Movement.playerSpeed - speedOffset).ToString("F2") + "MpH");
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 4.25f, widthblock * 3f, heightblock * 1f), "This:" + (CameraRotate.averageSpeed - speedOffset).ToString("F2") + "MpH");
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 5.25f, widthblock * 3f, heightblock * 1f), "Best:" + (CameraRotate.lapRecord - speedOffset).ToString("F2") + "MpH");
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;
		
		//Testing - Corner Speeds
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 6.75f, widthblock * 3f, heightblock * 1f), "Offset:" + (CameraRotate.carSpeedOffset).ToString("F2") + "MpH");
	}
}
