using UnityEngine;
//using UnityEngine.Advertisements;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class ChampionshipResultsGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public Texture2D yellowBox;

	public static int[] championshipCarNumber = new int[45];
	public static int[] championshipPoints = new int[45];
	
	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	int playerMoney;
	int moneyCount;
	int premiumTokens;

	string raceSeries;
	int fieldSize;
	int nameTrim;
	string namePrefix;

	int championshipRound;
	int currentRound;

	int championshipPos;

	int y;
	
	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		
		//if (Advertisement.isSupported) {
		//	Advertisement.Initialize("48229");
		//}
		
		if(Scoreboard.position == 0){
			SceneManager.LoadScene("MainMenu");
		}

		raceSeries = PlayerPrefs.GetString("raceSeries");
		fieldSize = PlayerPrefs.GetInt("FieldSize");
		nameTrim = PlayerPrefs.GetInt("NameTrim");

		switch(raceSeries){
		case "StockCar":
			namePrefix = "livery";
			break;
		case "IndyCar":
			namePrefix = "indylivery";
			break;
		case "Truck":
			namePrefix = "trucklivery";
			break;
		default:
			namePrefix = "livery";
			break;
		}

		y = 0;

		for( int i=0; i < fieldSize; i++){
			string carNumber;
			if(i == (Scoreboard.position - 1)){
				carNumber = PlayerPrefs.GetString("carTexture");
				carNumber = Regex.Replace(carNumber,"[^\\d]", "");
			} else {
				carNumber = Scoreboard.carNames[i].Remove(0,6);
			}
			int pointsTotal = DriverPoints.pointsTotal[int.Parse(carNumber)] + RacePoints.placePoints[i];
			//Debug.Log("Car" + carNumber + " - " + pointsTotal);
			PlayerPrefs.SetInt("ChampionshipPoints" + carNumber,pointsTotal);
			//Debug.Log("Car " + carNumber + "now has " + PlayerPrefs.GetInt("ChampionshipPoints" + carNumber) + " points");
			DriverPoints.pointsTotal[int.Parse(carNumber)] = PlayerPrefs.GetInt("ChampionshipPoints" + carNumber);
		}

		for( int i=0; i < fieldSize; i++){
			string carNumber;
			if(i == (Scoreboard.position - 1)){
				carNumber = PlayerPrefs.GetString("carTexture");
				carNumber = Regex.Replace(carNumber,"[^\\d]", "");
			} else {
				carNumber = Scoreboard.carNames[i].Remove(0,6);
			}
			championshipPoints[i] = DriverPoints.pointsTotal[int.Parse(carNumber)];
			championshipCarNumber[i] = int.Parse(carNumber);
		}

		int tempPoints;
		int tempNumber;

		for(int repeats = 0; repeats < 45; repeats++){
			for(int i = 0; i < (fieldSize - 1); i++){
				if(championshipPoints[i] <= championshipPoints[i + 1]){
					tempPoints = championshipPoints[i];
					tempNumber = championshipCarNumber[i];
					championshipPoints[i] = championshipPoints[i + 1];
					championshipCarNumber[i] = championshipCarNumber[i + 1];
					championshipPoints[i + 1] = tempPoints;
					championshipCarNumber[i + 1] = tempNumber;
				}
			}
		}

		championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
		currentRound = championshipRound;
		premiumTokens = PlayerPrefs.GetInt("PremiumTokens");
		if(championshipRound < 12){
			championshipRound++;
		} else {
			if(championshipPos == 0){
				premiumTokens+=5;
			}
			championshipRound = 1;
		}
		PlayerPrefs.SetInt("ChampionshipRound", championshipRound);
		PlayerPrefs.SetInt("PremiumTokens", premiumTokens);
		
		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		championshipPos = 1;
	}

	void FixedUpdate(){
		if(currentRound == 12){
			if(moneyCount < (PrizeMoney.cashAmount[championshipPos] * 20)){
				moneyCount += 50000;
			} else {
				moneyCount = (PrizeMoney.cashAmount[championshipPos] * 20);
			}
		}
	}

	void PlayAd(){
		GetComponent<ChampionshipResultsExit>().enabled = true;
		this.enabled = false;
	}

	void OnGUI() {

		GUI.skin = eightBitSkin;
		string carDriver;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		
		GUI.skin.label.normal.textColor = Color.black;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock, Screen.height * 4.5f));
		
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		
		for( int i=0; i < fieldSize; i++){
			Vector2 pivotPoint = new Vector2(widthblock * 3, (heightblock * (i * 2)) + heightblock * 2);
			GUIUtility.RotateAroundPivot(90, pivotPoint);

			string carNumber = PlayerPrefs.GetString("carTexture");
			carNumber = Regex.Replace(carNumber,"[^\\d]", "");
			int carNo = int.Parse(carNumber);
			if(championshipCarNumber[i] == (carNo)){
				PlayerPrefs.SetInt("CurrentChampionshipPos",i+1);
				GUI.DrawTexture(new Rect(widthblock * 3, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 1.5f, heightblock * 3), Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture);
				championshipPos = i;
			} else {
				GUI.DrawTexture(new Rect(widthblock * 3, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 1.5f, heightblock * 3), Resources.Load(namePrefix + championshipCarNumber[i]) as Texture);
			}
			GUIUtility.RotateAroundPivot(-90, pivotPoint);
			
			if(championshipCarNumber[i] == (carNo)){
				GUI.skin.label.normal.textColor = Color.red;
			}
			GUI.Label(new Rect(widthblock * 3.5f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 2, heightblock * 2), "P" + (i + 1));
			if(championshipCarNumber[i] == (carNo)){
				carDriver = PlayerPrefs.GetString("RacerName");
			} else {
				switch(raceSeries){
				case "StockCar":
					carDriver = DriverNames.cup2020Names[championshipCarNumber[i]];
					break;
				default:
					carDriver = DriverNames.cup2020Names[championshipCarNumber[i]];
					break;
				}
			}
			GUI.Label(new Rect(widthblock * 5, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 7, heightblock * 2), "" + carDriver + "(" + championshipCarNumber[i] + ")");
			GUI.Label(new Rect(widthblock * 16, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 4, heightblock * 2), "" + PlayerPrefs.GetInt("ChampionshipPoints" + championshipCarNumber[i]) + "");

			if(championshipCarNumber[i] == (carNo)){
				GUI.skin.label.normal.textColor = Color.black;
			}
		}
		
		GUI.EndScrollView();
		
		GUI.DrawTexture(new Rect(0, 0,Screen.width - widthblock, heightblock * 2), yellowBox, ScaleMode.StretchToFill);
		
		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		
		GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "Championship Standings");
		
		GUI.DrawTexture(new Rect(0, Screen.height - (heightblock * 4),Screen.width - widthblock, heightblock * 6), yellowBox, ScaleMode.StretchToFill);

		if(currentRound == 12){
			GUI.Label(new Rect(widthblock, Screen.height - (heightblock * 3), widthblock * 12, heightblock * 2), "Total Winnings: $" + (playerMoney + moneyCount));
			if((PlayerPrefs.GetInt("CurrentChampionshipPos") == 1) && (y != 1)){
				//CHAMPIONSHIP WIN
				PlayerPrefs.SetInt("TotalChampionships",PlayerPrefs.GetInt("TotalChampionships") + 1);
				y = 1;
			}
		}

		GUI.skin.button.fontSize = 72 / FontScale.fontScale;
		
		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		if(GUI.Button(new Rect(widthblock * 14, Screen.height - (heightblock * 3), widthblock * 4, heightblock * 2), "Next")){
			if(currentRound == 12){
				PlayerPrefs.SetInt("PrizeMoney",PlayerPrefs.GetInt("PrizeMoney") + (PrizeMoney.cashAmount[championshipPos] * 20));
			}
			PlayAd();
		}
	}
}
