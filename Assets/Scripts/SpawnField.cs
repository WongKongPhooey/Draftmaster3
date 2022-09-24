using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

public class SpawnField : MonoBehaviour {

	public Transform AICarPrefab;

	string carNum;

	public static ArrayList fastCars = new ArrayList();
	public static ArrayList midCars = new ArrayList();
	public static ArrayList slowCars = new ArrayList();

	public float paceDistance;

	int gridRows;
	int gridLanes;
	int circuitLanes;
	int AILevel;
	
	int fieldSize;
	
	int currentSeries;
	int currentSubseries;
	
	string seriesPrefix;
	
	bool cautionRestart;
	public static int startLane;
	
	static Dictionary<int, int> championshipPoints = new Dictionary<int, int>();

	// Use this for initialization
	void Start () {

		int playerRow;
		
		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}
		//Debug.Log("seriesPrefix on spawn is: " + seriesPrefix);

		gridLanes = 2;
		
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		currentSeries = PlayerPrefs.GetInt("CurrentSeries");
		currentSubseries = PlayerPrefs.GetInt("CurrentSubseries");
		
		startLane = Random.Range(1,3); //1 or 2
		
		if(PlayerPrefs.GetString("RaceType") == "Event"){
			AILevel = EventData.offlineAILevel[int.Parse(currentSeries.ToString()),int.Parse(currentSubseries.ToString())];
		} else {
			AILevel = SeriesData.offlineAILevel[int.Parse(currentSeries.ToString()),int.Parse(currentSubseries.ToString())];
		}
		if(AILevel>15){
			AILevel = 15;
		}

		if(PlayerPrefs.HasKey("CustomField")){
			spawnCustomField(PlayerPrefs.GetString("CustomField"));
			//Debug.Log("Spawn custom field");
		} else {
			//Debug.Log("Spawn standard field: " + seriesPrefix);
			if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("ChampionshipSubseries") == PlayerPrefs.GetString("CurrentSeriesIndex"))){
				if(PlayerPrefs.GetInt("ChampionshipRound") > 7){
					//Debug.Log("Points Adjusted Field");
					spawnCarsPointsAdjusted(seriesPrefix);
				} else {
					//Debug.Log("Too early in the season for points adjusting");
					spawnCars(seriesPrefix);
				}
			} else {
				//Debug.Log("Not a championship race");
				spawnCars(seriesPrefix);
			}
			//spawnCup2020Scenario();
		}
		//spawnCup2020Scenario();
		paceDistance = 2.5f;
		string carNumber = PlayerPrefs.GetString("carTexture");
		carNumber = PlayerPrefs.GetString("carTexture");			
		string splitAfter = "livery";
		carNumber = carNumber.Substring(carNumber.IndexOf(splitAfter) + splitAfter.Length);
		
		fastCars.Remove(carNumber);
		midCars.Remove(carNumber);
		slowCars.Remove(carNumber);
		
		// +1 for the Player's car
		if(!PlayerPrefs.HasKey("SpawnFromCaution")){
			fieldSize = fastCars.Count + midCars.Count + slowCars.Count + 1;
		} else {
			//Just remember count from the initial race start
			fieldSize = PlayerPrefs.GetInt("FieldSize");
		}
		
		//Max field size
		if(fieldSize > 45){
			fieldSize = 45;
		}
		
		if(!PlayerPrefs.HasKey("SpawnFromCaution")){
			PlayerPrefs.SetInt("FieldSize", fieldSize);
		}
		float gridRowsCalc = fieldSize * 0.5f;
		
		gridRows = Mathf.CeilToInt(gridRowsCalc);
		
		Debug.Log("Field size: " + fieldSize + ", Grid rows: " + gridRows);
		
		if(gridRows > (AILevel+5)){
			playerRow = Random.Range(AILevel,AILevel+5);
			//Debug.Log("Full row selection: " + playerRow);
		} else {
			if(gridRows > AILevel){
				playerRow = Random.Range(AILevel,gridRows);
				//Debug.Log("Shortened row selection: " + playerRow);
			} else {
				playerRow = gridRows;
				//Debug.Log("Back row: " + playerRow);
			}
		}
		
		//If race is restarting..
		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			float playerRowCalc = PlayerPrefs.GetInt("PlayerCautionPosition") / 2;
			//If I don't add the +1, you get a dupe car spawn in 0_o
			playerRow = Mathf.CeilToInt(playerRowCalc) + 1;
		}

		int carChoice;

		Object AICarInstance;

		//In-race restart, set field from caution order
		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			Debug.Log("Set Field From Caution");
			int fieldIndex = 0;
			
			Debug.Log("Player starting row: " + playerRow);
			
			//Set field after caution
			//Cars In Front
			for (int i = playerRow - 1; i >= 1; i--) {
				Debug.Log("Field Row In Front: " + i);
				for(int j=1;j<=gridLanes;j++){
					AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * paceDistance), Quaternion.identity);
					carNum = PlayerPrefs.GetInt("CautionPosition" + fieldIndex + "").ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
					fieldIndex++;
				}
			}

			//Position the player car in it's start lane
			GameObject.Find("Player").transform.Translate(0-(1.2f * startLane),0f,0f);

			for(int j=1;j<=gridLanes;j++){
				if(j != startLane){
					AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, 0f), Quaternion.identity);
					carNum = PlayerPrefs.GetInt("CautionPosition" + fieldIndex + "").ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
				}
				fieldIndex++;
			}

			//Cars Behind
			for (int i = 1; i < (gridRows - playerRow); i++) {
				Debug.Log("Field Row Behind: " + i);
				for(int j=1;j<=gridLanes;j++){
					AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * -paceDistance), Quaternion.identity);
					carNum = PlayerPrefs.GetInt("CautionPosition" + fieldIndex + "").ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
					fieldIndex++;
				}
			}
			Debug.Log(fieldIndex + " Cars Positioned After Caution");
			PlayerPrefs.DeleteKey("SpawnFromCaution");
			
		} else {
			
			//Debug.Log("Set Starting Field");
			//Randomised field for start of race
			//Cars In Front
			for (int i = playerRow - 1; i >= 1; i--) {
				for(int j=1;j<=gridLanes;j++){
					if(fastCars.Count > 0){
						AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * paceDistance), Quaternion.identity);
						carChoice = Random.Range(0,fastCars.Count);
						carNum = fastCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
						fastCars.RemoveAt(carChoice);
					} else {
						if(midCars.Count > 0){
							AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * paceDistance), Quaternion.identity);
							carChoice = Random.Range(0,midCars.Count);
							carNum = midCars[carChoice].ToString();
							AICarInstance.name = ("AICar0" + carNum);
							GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
							midCars.RemoveAt(carChoice);
						} else {
							if(slowCars.Count > 0){
								AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * paceDistance), Quaternion.identity);
								carChoice = Random.Range(0,slowCars.Count);
								carNum = slowCars[carChoice].ToString();
								AICarInstance.name = ("AICar0" + carNum);
								GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
								slowCars.RemoveAt(carChoice);
							}
						}
					}
				}
			}

			//Position the player car in it's start lane
			GameObject.Find("Player").transform.Translate(0-(1.2f * startLane),0f,0f);

			for(int j=1;j<=gridLanes;j++){
				if(j != startLane){
					if(fastCars.Count > 0){
						AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, 0f), Quaternion.identity);
						carChoice = Random.Range(0,fastCars.Count);
						carNum = fastCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
						fastCars.RemoveAt(carChoice);
					} else {
						if(midCars.Count > 0){
							AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, 0f), Quaternion.identity);
							carChoice = Random.Range(0,midCars.Count);
							carNum = midCars[carChoice].ToString();
							AICarInstance.name = ("AICar0" + carNum);
							GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
							midCars.RemoveAt(carChoice);
						} else {
							if(slowCars.Count > 0){
								AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, 0f), Quaternion.identity);
								carChoice = Random.Range(0,slowCars.Count);
								carNum = slowCars[carChoice].ToString();
								AICarInstance.name = ("AICar0" + carNum);
								GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
								slowCars.RemoveAt(carChoice);
							}
						}
					}
				}
			}

			//Cars Behind
			for (int i = 1; i < (gridRows - playerRow); i++) {
				for(int j=1;j<=gridLanes;j++){
					if(fastCars.Count > 0){
						AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * -paceDistance), Quaternion.identity);
						carChoice = Random.Range(0,fastCars.Count);
						carNum = fastCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
						fastCars.RemoveAt(carChoice);
					} else {
						if(midCars.Count > 0){
							AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * -paceDistance), Quaternion.identity);
							carChoice = Random.Range(0,midCars.Count);
							carNum = midCars[carChoice].ToString();
							AICarInstance.name = ("AICar0" + carNum);
							GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
							midCars.RemoveAt(carChoice);
						} else {
							if(slowCars.Count > 0){
								AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * -paceDistance), Quaternion.identity);
								carChoice = Random.Range(0,slowCars.Count);
								carNum = slowCars[carChoice].ToString();
								AICarInstance.name = ("AICar0" + carNum);
								GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
								slowCars.RemoveAt(carChoice);
							}
						}
					}
				}
			}
			//Debug.Log("Cars Spawned: " + slowCars.Count);
		}
			
		Ticker.updateTicker();
	}

	public static void spawnIndycars(){
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
	}

	public static void spawnCars(string seriesPref){
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
		
		for(int i=0;i<100;i++){
			switch(DriverNames.getRarity(seriesPref, i)){
				case 4:
					fastCars.Add("" + i + "");
					break;
				case 3:
					fastCars.Add("" + i + "");
					break;
				case 2:
					midCars.Add("" + i + "");
					break;
				case 1:
					slowCars.Add("" + i + "");
					break;
				default:
					break;
			}
		}
	}
	
	public static void spawnCarsPointsAdjusted(string seriesPref){
		
		//Spawned field based on Championship points
		championshipPoints.Clear();
		int pointsInd = 0;
		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("ChampionshipPoints" + i)){
				championshipPoints.Add(i, PlayerPrefs.GetInt("ChampionshipPoints" + i));
				pointsInd++;
			}
		}
		
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
		
		List<KeyValuePair<int, int>> pointsTable = new List<KeyValuePair<int, int>>(championshipPoints);
		
		pointsTable.Sort(
			delegate(KeyValuePair<int, int> firstPair,
			KeyValuePair<int, int> nextPair)
			{
				return nextPair.Value.CompareTo(firstPair.Value);
			}
		);
		
		int pointsTableInd = 0;
		foreach(var pointsRow in pointsTable){
			if(pointsRow.Value > 0){
				if(pointsTableInd < 5){
					Debug.Log("Added fast car: #" + pointsRow.Key);
					fastCars.Add("" + pointsRow.Key + "");
				} else {
					if(pointsTableInd > 20){
						slowCars.Add("" + pointsRow.Key + "");
					} else {
						midCars.Add("" + pointsRow.Key + "");
					}
				}
			}
			pointsTableInd++;
		}
	}
	
	public static void spawnCustomField(string customField){
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
		
		switch(customField){
			case "cup22AllStar":
				fastCars.Add("4");
				fastCars.Add("5");
				fastCars.Add("6");
				fastCars.Add("9");
				fastCars.Add("11");
				fastCars.Add("18");
				fastCars.Add("19");
				fastCars.Add("22");
				midCars.Add("12");
				midCars.Add("20");
				midCars.Add("24");
				midCars.Add("45");
				midCars.Add("48");
				slowCars.Add("10");
				slowCars.Add("16");
				slowCars.Add("23");
				slowCars.Add("34");
				slowCars.Add("41");
				break;
			case "cup01HarvickGordon":
				fastCars.Add("24");
				break;
			case "cup79MomentsDaytona":
				fastCars.Add("1");
				fastCars.Add("11");
				slowCars.Add("43");
				break;
			default:
				break;
		}
	}
	
	public static void spawnCup2020Scenario(){
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
		
		fastCars.Add("11");
		fastCars.Add("6");
		fastCars.Add("3");
		fastCars.Add("4");
	}

	public static void spawnStockcars(){

		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();

		fastCars.Add("3");
		fastCars.Add("24");
		fastCars.Add("30");
		fastCars.Add("43");
		fastCars.Add("48");
		fastCars.Add("52");
		fastCars.Add("88");
		
		slowCars.Add("0");
		slowCars.Add("1");
		slowCars.Add("2");
		slowCars.Add("4");
		slowCars.Add("5");
		slowCars.Add("6");
		slowCars.Add("7");
		slowCars.Add("8");
		slowCars.Add("9");
		slowCars.Add("10");
		slowCars.Add("11");
		slowCars.Add("12");
		slowCars.Add("13");
		slowCars.Add("14");
		slowCars.Add("15");
		slowCars.Add("16");
		slowCars.Add("17");
		slowCars.Add("18");
		slowCars.Add("19");
		slowCars.Add("20");
		slowCars.Add("21");
		slowCars.Add("22");
		slowCars.Add("23");
		slowCars.Add("25");
		slowCars.Add("27");
		slowCars.Add("28");
		slowCars.Add("29");
		slowCars.Add("33");
		slowCars.Add("37");
		slowCars.Add("41");
		slowCars.Add("44");
		slowCars.Add("46");
		slowCars.Add("50");
		slowCars.Add("51");
		slowCars.Add("55");
		slowCars.Add("66");
		slowCars.Add("67");
		
		if(RacePoints.championshipMode == true){
			slowCars.Add("71");
			slowCars.Add("77");
			slowCars.Add("80");
			slowCars.Add("89");
			slowCars.Add("91");
			slowCars.Add("92");
			slowCars.Add("94");
			slowCars.Add("99");
		}
	}
}