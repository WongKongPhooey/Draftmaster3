using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SingleCar : MonoBehaviour {
    
	public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin blueGUI;
	public GUISkin redGUI;
	public GUISkin whiteGUI;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	int playerLevel;
	int totalMoney;
	
	int carGears;
	int carClass;
	int classMax;
	
	int currentCar;
	
	string currentCarManu;
	string currentCarTeam;
	string currentCarType;
	int currentCarRarity;
	
	string seriesPrefix;
	float windowscroll = 2.5f;
	
	public string carMenu;
	
	bool numberPanel;
	
	List<int> availableNumbers = new List<int>();
	int[] availNums;
	int numsInd;
	
	int transferContracts;
	int contractsUsed;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	public Texture2D frdTex;
	public Texture2D chvTex;
	public Texture2D tytTex;
	
	public static string[] classCriteria = new string[7];

	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);

		carMenu = "Links";

		numberPanel = false;

		if(PlayerPrefs.HasKey("SeriesFocus")){
			seriesPrefix = PlayerPrefs.GetString("SeriesFocus");
		} else {
			seriesPrefix = "cup20";
		}
		
		if(PlayerPrefs.HasKey("CarFocus")){
			currentCar = PlayerPrefs.GetInt("CarFocus");
			PlayerPrefs.DeleteKey("CarFocus");
		} else {
			currentCar = 1;
		}
		
		currentCarManu = DriverNames.cup2020Manufacturer[currentCar];
		currentCarTeam = DriverNames.cup2020Teams[currentCar];
		currentCarType = DriverNames.cup2020Types[currentCar];
		currentCarRarity = DriverNames.cup2020Rarity[currentCar];
		
		seriesPrefix = "cup20";
		windowscroll = 2.5f;
		
		carGears = 0;
		carClass = 0;
		
		if(PlayerPrefs.GetInt(seriesPrefix + currentCar + "Unlocked") != 1){
			PlayerPrefs.SetInt(seriesPrefix + currentCar + "Unlocked", 1);
		}
		
		if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "Gears")){
			carGears = PlayerPrefs.GetInt(seriesPrefix + currentCar + "Gears");
			carClass = PlayerPrefs.GetInt(seriesPrefix + currentCar + "Class");
		}
		
        classMax = GameData.classMax(carClass);
		
		availableNumbers.Clear();
		
		for(int car=0;car<100;car++){
			if((Resources.Load(seriesPrefix + "num" + car) != null)
			&&(PlayerPrefs.GetInt(seriesPrefix + car + "Unlocked") == 1)){
				availableNumbers.Add(car);
			}
		}
		availNums = availableNumbers.ToArray();
		
		if(!PlayerPrefs.HasKey("TransferContracts")){
			PlayerPrefs.SetInt("TransferContracts",0);
		}
		transferContracts = PlayerPrefs.GetInt("TransferContracts");
		
		if(!PlayerPrefs.HasKey("ContractsUsed")){
			PlayerPrefs.SetInt("ContractsUsed",0);
		}
		contractsUsed = PlayerPrefs.GetInt("ContractsUsed");
	}

    // Update is called once per frame
    void Update()
    {
        totalMoney = PlayerPrefs.GetInt("PrizeMoney");
    }
	
	void OnGUI(){
		
		GUI.skin = buttonSkin;
		
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.skin.button.fontSize = 96 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 5, 20, widthblock * 5, heightblock * 2), "My Garage");
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin = tileSkin;
		
		float cardW = widthblock * 6;
		float cardH = heightblock * 15;
			
		string carLivery = seriesPrefix + "livery" + (currentCar);
		float cardX = widthblock * 0.5f;
		float cardY = heightblock * 3.5f;
			
		GUI.skin = whiteGUI;
			
		GUI.Box(new Rect(cardX, cardY, cardW, cardH), "");
		
		GUI.skin = tileSkin;
		
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		
		GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 1.5f, heightblock * 1f), currentCarTeam);
		Texture2D manufacturerTex = null;
		switch(currentCarManu){
			case "FRD":
				manufacturerTex = frdTex;
				break;
			case "CHV":
				manufacturerTex = chvTex;
				break;
			case "TYT":
				manufacturerTex = tytTex;
				break;
			default:
				break;
		}
		GUI.DrawTexture(new Rect(cardX + cardW - (widthblock * 1f), cardY + 10, widthblock * 0.75f, widthblock * 0.375f), manufacturerTex);
		
		GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load("20liveryblank") as Texture);
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + currentCar)){
			int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar);
			GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery + "blank") as Texture);
			GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f) + (((widthblock * 5f)/64)*34), cardY + (heightblock * 2) + ((widthblock * 2.5f)/4), widthblock * 1.25f, widthblock * 1.25f), Resources.Load("cup20num" + customNum) as Texture);
		} else {
			GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery) as Texture);
		}
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect(cardX, cardY + (heightblock * 7.5f), widthblock * 6, heightblock * 2), DriverNames.cup2020Names[currentCar]);
		
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		//Progress Bar Box
		GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f), "");
		//Progress Bar
		if(carClass == 6){
			//Maxed Out
			GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f), "");
		} else {
			if(carGears > classMax){
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f), "");
			} else {
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), ((cardW - (widthblock * 0.5f))/classMax) * carGears, heightblock * 1.5f), "");
			}
		}
		
		if(carClass == 6){
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), widthblock * 5.5f, heightblock * 1.5f), "Max Class");
		} else {
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), widthblock * 5.5f, heightblock * 1.5f), carGears + "/" + classMax);
		}
		
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		//Upgrade
		if((carGears >= classMax)&&(carClass < 6)){
			if(totalMoney >= GameData.upgradeCost(carClass)){
				if (GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 11.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f), "Upgrade ($" + GameData.upgradeCost(carClass) + ")")){
					totalMoney -= GameData.upgradeCost(carClass);
					PlayerPrefs.SetInt("PrizeMoney",totalMoney);
					if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "Gears")){
						PlayerPrefs.SetInt(seriesPrefix + currentCar + "Gears", carGears - classMax);
						PlayerPrefs.SetInt(seriesPrefix + currentCar + "Class", carClass+1);
						carGears -= classMax;
						carClass++;
						classMax = GameData.classMax(carClass);
					}
				}
			} else {
				if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 11.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f), "$" + (GameData.upgradeCost(carClass) - totalMoney) + " More Required")){
				}
			}
		} else {
			if(carClass != 6){
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 11.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f),"");
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 11.5f), cardW - (widthblock * 0.5f), heightblock * 1.5f), "" + (classMax - carGears) + " More Required");
			
				if(carGears < 0){
					carGears = 0;
					PlayerPrefs.SetInt(seriesPrefix + currentCar + "Gears", 0);
				}
			}
		}
		
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.LowerLeft;
		GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + cardH - (heightblock * 2) - 10, widthblock * 2.5f, heightblock * 2), currentCarType);
		GUI.skin.label.alignment = TextAnchor.UpperRight;
		GUI.skin.label.alignment = TextAnchor.LowerRight;
		GUI.Label(new Rect(cardX + (widthblock * 3.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 2.5f) - (widthblock * 0.25f), heightblock * 2), "Class " + classAbbr(carClass));
		GUI.skin.label.normal.textColor = classColours(carClass);
		GUI.Label(new Rect(cardX + (widthblock * 3.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 2.5f) - (widthblock * 0.25f), heightblock * 2), "" + classAbbr(carClass));
		GUI.skin.label.normal.textColor = Color.black;
		
		//Right Side Panel
		
		GUI.skin = buttonSkin;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if (GUI.Button(new Rect(widthblock * 7.5f, heightblock * 3.5f, widthblock * 2.5f, heightblock * 1.5f), "Links")){
			carMenu = "Links";
		}
		
		if (GUI.Button(new Rect(widthblock * 10.25f, heightblock * 3.5f, widthblock * 2.5f, heightblock * 1.5f), "Paints")){
			carMenu = "Paints";
		}
		
		if (GUI.Button(new Rect(widthblock * 13f, heightblock * 3.5f, widthblock * 2.5f, heightblock * 1.5f), "Stats")){
			carMenu = "Stats";
		}
		
		if (GUI.Button(new Rect(widthblock * 15.75f, heightblock * 3.5f, widthblock * 3.5f, heightblock * 1.5f), "Transfers")){
			carMenu = "Transfers";
		}
		
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		
		//MENUS
		switch(carMenu){
			case "Links":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 7f, widthblock * 11.5f, heightblock * 2), currentCarTeam + " Teammates Will Help You More");
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 10f, widthblock * 11.5f, heightblock * 2), "Other " + currentCarManu + " Cars Will Block You Less");
				GUI.DrawTexture(new Rect(widthblock * 7.5f, heightblock * 11.5f, heightblock * 2f, heightblock * 1), manufacturerTex);
				
				switch(currentCarType){
					case "Intimidator":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Intimidators Push Cars Out Of Lanes More Often");
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 14.5f, widthblock * 11.5f, heightblock * 2), "Base (25%)");
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 16f, widthblock * 11.5f, heightblock * 2), "Class " + classAbbr(carClass) + " (+" + carClass * 2 + "%)");
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 17.5f, widthblock * 11.5f, heightblock * 2), "" + currentCarRarity + "* Rarity (+" + (currentCarRarity * 10) + "%)");
						break;
					case "Strategist":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 14.5f, widthblock * 11.5f, heightblock * 2), "Strategists Change Lane Faster Per Class");
						break;
					case "Rookie":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 14.5f, widthblock * 11.5f, heightblock * 2), "Rookies Are Blocked Less Often");
						break;
					case "Closer":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 14.5f, widthblock * 11.5f, heightblock * 2), "Closers Pick Up A Draft From Further Away");
						break;
					case "Gentleman":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 14.5f, widthblock * 11.5f, heightblock * 2), "Everyone Will Work With Gentleman Drivers");
						break;
				}
				GUI.skin = buttonSkin;
				break;
			case "Paints":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 5.5f, widthblock * 11.5f, heightblock * 7), "Alternate Paints Coming Soon");
				break;
			case "Stats":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 5.5f, widthblock * 11.5f, heightblock * 7), "Starts");
				break;
			case "Transfers":
				if (GUI.Button(new Rect(widthblock * 9f, heightblock * 10f, widthblock * 4.5f, heightblock * 1.5f), "Change Number")){
					numberPanel = true;
				}
				if (GUI.Button(new Rect(widthblock * 9.5f, heightblock * 12f, widthblock * 4f, heightblock * 1.5f), "Change Team")){
				}
				if (GUI.Button(new Rect(widthblock * 7.5f, heightblock * 14f, widthblock * 6f, heightblock * 1.5f), "Change Manufacturer")){
				}
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + currentCar)){
					GUI.skin = redGUI;
					if (GUI.Button(new Rect(widthblock * 9f, heightblock * 16f, widthblock * 4.5f, heightblock * 1.5f), "Reset Changes")){
						int currentNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar);
						PlayerPrefs.DeleteKey("CustomNumber" + seriesPrefix + currentNum);
						PlayerPrefs.DeleteKey("CustomNumber" + seriesPrefix + currentCar);
					}
				}
				
				break;
		}
		
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin = buttonSkin;
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		CommonGUI.BackButton("AllCars");
		
		GUI.skin = buttonSkin;
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();
		
		if(numberPanel == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperLeft;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 3.5f, heightblock * 4f, widthblock * 11f, heightblock * 2f), "Change Number");

			numsInd = 0;
			
			for(int i=0;i<4;i++){
				for(int j=0;j<10;j++){
					if(numsInd < availNums.Length){
						if (GUI.Button(new Rect(widthblock * 3.75f + (widthblock * j * 1.25f), (heightblock * 6f) + (heightblock * i * 2f), widthblock * 1f, widthblock * 1f), "")){
							PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentCar, availNums[numsInd]);
							PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + availNums[numsInd], currentCar);
							Debug.Log("Car #" + currentCar + " now uses #" + PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar) + ". Var: " + "CustomNumber" + seriesPrefix + availNums[numsInd]);
							numberPanel = false;
						}
						GUI.DrawTexture(new Rect(widthblock * 3.75f + (widthblock * j * 1.25f) + 5, (heightblock * 6f) + (heightblock * i * 2f) + 5, widthblock * 1f - 10, widthblock * 1f - 10), Resources.Load(seriesPrefix + "num" + availNums[numsInd]) as Texture);
						numsInd++;
					}
				}
			}
			
			GUI.skin = redGUI;
			if (GUI.Button(new Rect(widthblock * 14f, heightblock * 3.5f, widthblock * 2.5f, heightblock * 1.5f), "Back")){
				numberPanel = false;
			}
		}

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
	
	string classAbbr(int carClass){
		string classLetter;
		switch(carClass){
			case 1:
				classLetter = "R";
				break;
		    case 2:
				classLetter = "D";
				break;
			case 3:
				classLetter = "C";
				break;
			case 4:
				classLetter = "B";
				break;
			case 5:
				classLetter = "A";
				break;
			case 6:
				classLetter = "S";
				break;
		    default:
				classLetter = "R";
				break;
		}
		return classLetter;
	}
	
	Color classColours(int carClass){
		Color classColour;
		switch(carClass){
			case 1:
				classColour = Color.red;
				break;
		    case 2:
				classColour = Color.yellow;
				break;
			case 3:
				classColour = Color.green;
				break;
			case 4:
				classColour = Color.cyan;
				break;
			case 5:
				classColour = Color.blue;
				break;
			case 6:
				classColour = Color.red;
				break;
		    default:
				classColour = Color.red;
				break;
		}
		return classColour;
	}
}
