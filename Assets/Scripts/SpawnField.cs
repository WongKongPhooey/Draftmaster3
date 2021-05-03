using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;

public class SpawnField : MonoBehaviour {

	public Transform AICarPrefab;

	string carNum;

	public static ArrayList carNumbers = new ArrayList();
	public static ArrayList fastCars = new ArrayList();

	public float paceDistance;

	int gridRows;
	int gridLanes;
	
	bool cautionRestart;
	public static int startLane;

	// Use this for initialization
	void Start () {

		int playerRow;

		gridLanes = 2;
		
		if(PlayerPrefs.GetInt("CircuitLanes") == 4){
			startLane = Random.Range(3,5);
		} else {
			startLane = Random.Range(2,4);
		}
		
		if(PlayerPrefs.GetString("ChallengeType")=="LastToFirstLaps"){
			startLane = 3;
		}
		if(PlayerPrefs.GetString("ChallengeType")=="NoFuel"){
			startLane = 2;
		}
		if(PlayerPrefs.GetString("ChallengeType")=="TrafficJam"){
			startLane = 2;
		}

		spawnCup2020Cars();
		//spawnCup2020Scenario();
		paceDistance = 2.5f;
		string carNumber = PlayerPrefs.GetString("carTexture");
		carNumber = PlayerPrefs.GetString("carTexture");			
		string splitAfter = "livery";
		carNumber = carNumber.Substring(carNumber.IndexOf(splitAfter) + splitAfter.Length);
		fastCars.Remove(carNumber);
		carNumbers.Remove(carNumber);
		gridRows = 10;
		playerRow = 2;

		string challengeName = PlayerPrefs.GetString("ChallengeType");

		switch(challengeName){
		case "NoFuel":
		case "CleanBreak":
		case "PhotoFinish":
			playerRow = 1;
			break;
		case "TeamPlayer":
		case "LatePush":
			playerRow = 5;
			break;
		case "LastToFirstLaps":
		case "TrafficJam":
			playerRow = gridRows - 1;
			break;
		default:
			break;
		}

		int carChoice;

		Object AICarInstance;

		//Cars In Front
		for (int i = playerRow - 1; i >= 1; i--) {
			
			//The Indy Car "Legends 500" Always Starts 3 Wide!
			if((PlayerPrefs.GetString("CurrentCircuit")=="Legends500")&&(PlayerPrefs.GetString("raceSeries")=="IndyCar")){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(1.2f, 0.4f, i * paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					carNumbers.RemoveAt(carChoice);
				}
			}
			
			//Lane 2
			if(PlayerPrefs.GetInt("CircuitLanes") == 3){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0, 0.4f, i * paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					carNumbers.RemoveAt(carChoice);
				}
			}
			
			//Lane 3
			AICarInstance = Instantiate(AICarPrefab, new Vector3(-1.2f, 0.4f, i * paceDistance), Quaternion.identity);
			if(fastCars.Count > 0){
				carChoice = Random.Range(0,fastCars.Count);
				carNum = fastCars[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				fastCars.RemoveAt(carChoice);
			} else {
				carChoice = Random.Range(0,carNumbers.Count);
				carNum = carNumbers[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				carNumbers.RemoveAt(carChoice);
			}
			
			//Lane 4
			if(PlayerPrefs.GetInt("CircuitLanes") == 4){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(-2.4f, 0.4f, i * paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					carNumbers.RemoveAt(carChoice);
				}
			}
		}

		//Position the player car in it's start lane
		GameObject.Find("Player").transform.Translate(-1.2f * (startLane - 1),0f,0f);

		//Lane 1
		if((PlayerPrefs.GetString("CurrentCircuit")=="Legends500")&&(PlayerPrefs.GetString("raceSeries")=="IndyCar")){
			if(startLane != 1){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(1.2f, 0.4f, 0), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					carNumbers.RemoveAt(carChoice);
				}
			}
		}

		//Lane 2
		if(PlayerPrefs.GetInt("CircuitLanes") == 3){
			if(startLane != 2){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0, 0.4f, 0), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					carNumbers.RemoveAt(carChoice);
				}
			}
		}
		
		//Lane 3
		if(startLane != 3){
			AICarInstance = Instantiate(AICarPrefab, new Vector3(-1.2f, 0.4f, 0), Quaternion.identity);
			if(fastCars.Count > 0){
				carChoice = Random.Range(0,fastCars.Count);
				carNum = fastCars[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				fastCars.RemoveAt(carChoice);
			} else {
				carChoice = Random.Range(0,carNumbers.Count);
				carNum = carNumbers[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				carNumbers.RemoveAt(carChoice);
			}
		}

		//Lane 4
		if(PlayerPrefs.GetInt("CircuitLanes") == 4){
			if(startLane != 4){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(-2.4f, 0.4f, 0), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					carNumbers.RemoveAt(carChoice);
				}
			}
		}

		//Cars Behind
		for (int i = 1; i < (gridRows - playerRow); i++) {
			
			//Lane 1
			if((PlayerPrefs.GetString("CurrentCircuit")=="Legends500")&&(PlayerPrefs.GetString("raceSeries")=="IndyCar")){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(1.2f, 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					carNumbers.RemoveAt(carChoice);
				}
			}
			
			//Lane 2
			if(PlayerPrefs.GetInt("CircuitLanes") == 3){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0, 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					carNumbers.RemoveAt(carChoice);
				}
			}
			
			//Lane 3
			AICarInstance = Instantiate(AICarPrefab, new Vector3(-1.2f, 0.4f, i * -paceDistance), Quaternion.identity);
			if(fastCars.Count > 0){
				carChoice = Random.Range(0,fastCars.Count);
				carNum = fastCars[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				fastCars.RemoveAt(carChoice);
			} else {
				carChoice = Random.Range(0,carNumbers.Count);
				carNum = carNumbers[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				carNumbers.RemoveAt(carChoice);
			}

			//Lane 4
			if(PlayerPrefs.GetInt("CircuitLanes") == 4){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(-2.4f, 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,carNumbers.Count);
					carNum = carNumbers[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					carNumbers.RemoveAt(carChoice);
				}
			}
		}
		//Scoreboard.checkPositions();
		Scoreboard.updateScoreboard();
	}

	public static void spawnIndycars(){

		fastCars.Clear();
		carNumbers.Clear();
	}

	public static void spawnCup2020Cars(){
		fastCars.Clear();
		carNumbers.Clear();
		
		fastCars.Add("1");
		fastCars.Add("2");
		fastCars.Add("3");
		fastCars.Add("4");
		fastCars.Add("6");
		fastCars.Add("8");
		fastCars.Add("9");
		fastCars.Add("10");
		fastCars.Add("11");
		fastCars.Add("12");
		fastCars.Add("13");
		fastCars.Add("14");
		fastCars.Add("18");
		fastCars.Add("20");
		fastCars.Add("21");
		fastCars.Add("22");
		fastCars.Add("24");
		fastCars.Add("42");
		fastCars.Add("43");
		fastCars.Add("48");
		fastCars.Add("95");
		carNumbers.Add("0");
		carNumbers.Add("7");
		carNumbers.Add("15");
		carNumbers.Add("16");
		carNumbers.Add("17");
		carNumbers.Add("41");
		
	}
	
	public static void spawnCup2020Scenario(){
		fastCars.Clear();
		carNumbers.Clear();
		
		fastCars.Add("2");
		fastCars.Add("9");
		fastCars.Add("11");
		fastCars.Add("22");
	}

	public static void spawnStockcars(){

		fastCars.Clear();
		carNumbers.Clear();

		fastCars.Add("3");
		fastCars.Add("24");
		fastCars.Add("30");
		fastCars.Add("43");
		fastCars.Add("48");
		fastCars.Add("52");
		fastCars.Add("88");
		
		carNumbers.Add("0");
		carNumbers.Add("1");
		carNumbers.Add("2");
		carNumbers.Add("4");
		carNumbers.Add("5");
		carNumbers.Add("6");
		carNumbers.Add("7");
		carNumbers.Add("8");
		carNumbers.Add("9");
		carNumbers.Add("10");
		carNumbers.Add("11");
		carNumbers.Add("12");
		carNumbers.Add("13");
		carNumbers.Add("14");
		carNumbers.Add("15");
		carNumbers.Add("16");
		carNumbers.Add("17");
		carNumbers.Add("18");
		carNumbers.Add("19");
		carNumbers.Add("20");
		carNumbers.Add("21");
		carNumbers.Add("22");
		carNumbers.Add("23");
		carNumbers.Add("25");
		carNumbers.Add("27");
		carNumbers.Add("28");
		carNumbers.Add("29");
		carNumbers.Add("33");
		carNumbers.Add("37");
		carNumbers.Add("41");
		carNumbers.Add("44");
		carNumbers.Add("46");
		carNumbers.Add("50");
		carNumbers.Add("51");
		carNumbers.Add("55");
		carNumbers.Add("66");
		carNumbers.Add("67");
		
		if(RacePoints.championshipMode == true){
			carNumbers.Add("71");
			carNumbers.Add("77");
			carNumbers.Add("80");
			carNumbers.Add("89");
			carNumbers.Add("91");
			carNumbers.Add("92");
			carNumbers.Add("94");
			carNumbers.Add("99");
		}
	}
}