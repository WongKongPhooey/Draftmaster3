using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

public class Scoreboard : MonoBehaviour {

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;

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

	public GUISkin eightBitSkin;
	public GUISkin scoreboardGUI;
	
	void Awake (){
		
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		
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
				driverNames[i] = DriverNames.cup2020Names[int.Parse(carNumber[i])];
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
					driverNames[i] = DriverNames.cup2020Names[int.Parse(carNumber[i])];
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
				//carPositions[j] = car.transform.position.z;
				//carNames[j] = car.transform.name;
				//carNumber[j] = Regex.Replace(carNames[j], "[^0-9]", "");
				//driverNames[j] = DriverNames.cup2020Names[int.Parse(carNumber[j])];
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
				driverNames[i] = DriverNames.cup2020Names[int.Parse(carNumber[i])];
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
			} else {
				carNames[i] = "" + entrantList[i].name;
				carNumber[i] = Regex.Replace(carNames[i], "[^0-9]", "");
				driverNames[i] = DriverNames.cup2020Names[int.Parse(carNumber[i])];
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

	public static int checkSingleCarPosition(string carName){
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == carName){
				return i;
			}
		}
		return 0;
	}

	public static void checkFinishPositions(){
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		playerPosition = playerCar.transform.position.z;
		float tempPosition = 0;
		string tempName = "";
		int i = 0;
		position = 1;
		foreach (GameObject car in carsArray) {
			carPositions[i] = car.transform.position.z;
			carNames[i] = car.transform.name;
			
			if(carPositions[i] > playerPosition){
				position++;
			}
			i++;
		}
		for(int repeats = 0; repeats < 45; repeats++){
			for(i = 0; i < (carsArray.Length - 1); i++){
				if(carPositions[i] <= carPositions[i+1]){
					tempPosition = carPositions[i];
					tempName = carNames[i];
					carPositions[i] = carPositions[i + 1];
					carNames[i] = carNames[i + 1];
					carPositions[i + 1] = tempPosition;
					carNames[i + 1] = tempName;
				}
			}
		}
		for(i = carsArray.Length; i >=0; i--){
			if(carPositions[i] < playerPosition){
				carPositions[i + 1] = carPositions[i];
				carNames[i + 1] = carNames[i];
				carPositions[i] = 0;
				carNames[i] = "";
			}
		}
		carPositions[position - 1] = playerPosition;
		carNames[position - 1] = PlayerPrefs.GetString("RacerName");
		if(position == 1){
			PlayerPrefs.SetInt("TotalWins",PlayerPrefs.GetInt("TotalWins") + 1);
		}
		if(position <= 5){
			PlayerPrefs.SetInt("TotalTop5s",PlayerPrefs.GetInt("TotalTop5s") + 1);
		}
	}
	
	void OnGUI(){
		GUI.skin = eightBitSkin;

		//Timing screen
		GUI.Box(new Rect(0,0,Screen.width, heightblock * 2), "");
		
		GUI.skin.label.normal.textColor = Color.white;
		GUI.skin.label.fontSize = 40 / FontScale.fontScale;
		
		//Laps and Info
		GUI.Label(new Rect(widthblock * 4, heightblock * 0f, widthblock * 2, heightblock * 1f), "STAGE 1");
		GUI.Label(new Rect(widthblock * 6, heightblock * 0f, widthblock * 3, heightblock * 1f), "LAP " + CameraRotate.lap + " of " + PlayerPrefs.GetInt("RaceLaps"));
		
		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 4, heightblock * 1f, widthblock * 4, heightblock * 1), 1 + " " + driverNames[0] + "");
		
		if(position <= 2){
			//Just show the top 4
			GUI.Label(new Rect(widthblock * 8, heightblock * 1f, widthblock * 4, heightblock * 1), "2" + " " + driverNames[1] + "(+" + carDist[1].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 12, heightblock * 1f, widthblock * 4, heightblock * 1), "3" + " " + driverNames[2] + "(+" + carDist[2].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 16, heightblock * 1f, widthblock * 4, heightblock * 1), "4" + " " + driverNames[3] + "(+" + carDist[3].ToString("F3") + ")");
		} else {
			//Player Position
			GUI.Label(new Rect(widthblock * 8, heightblock * 1f, widthblock * 6, heightblock * 1), (position - 1) + " " + driverNames[position - 1] + "(+" + carDist[position - 1].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 12, heightblock * 1f, widthblock * 6, heightblock * 1), position + " " + driverNames[position] + "(+" + carDist[position].ToString("F3") + ")");
			GUI.Label(new Rect(widthblock * 16, heightblock * 1f, widthblock * 6, heightblock * 1), (position + 1) + " " + driverNames[position + 1] + "(+" + carDist[position + 1].ToString("F3") + ")");
		}
		
		//Lap timer
		GUI.Box(new Rect(Screen.width - (widthblock * 4),heightblock * 3,widthblock * 3.5f, heightblock * 3.5f), "");
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 3.25f, widthblock * 3f, heightblock * 1f), "Spd:" + (Movement.playerSpeed - speedOffset).ToString("F2") + "MpH");
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 4.25f, widthblock * 3f, heightblock * 1f), "This:" + (CameraRotate.averageSpeed - speedOffset).ToString("F2") + "MpH");
		GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 5.25f, widthblock * 3f, heightblock * 1f), "Best:" + (CameraRotate.lapRecord - speedOffset).ToString("F2") + "MpH");
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;
	}
}
