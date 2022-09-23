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
	
	int unlockClass;
	int unlockGears;
	
	int currentCar;
	
	string currentCarManu;
	string currentCarTeam;
	string currentCarType;
	int currentCarRarity;
	
	List<int> teamMates = new List<int>();
	int[] myTeamMates;
	
	string seriesPrefix;
	float windowscroll = 2.5f;
	
	public string carMenu;
	
	bool driverPanel;
	bool numberPanel;
	
	List<int> availableNumbers = new List<int>();
	int[] availNums;
	int numsInd;
	int customNumX;
	
	List<string> freeAgents = new List<string>();
	List<string> availableDrivers = new List<string>();
	string[] availFreeAgents;
	int freeAgentsInd;
	
	int transfersLeft;
	int transfersMax;
	
	string transferError;
	int transferContracts;
	int contractsUsed;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	public Texture2D frdTex;
	public Texture2D chvTex;
	public Texture2D tytTex;
	
	public Texture2D starOne;
	public Texture2D starTwo;
	public Texture2D starThree;
	
	public Texture2D contracts;
	
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
		Debug.Log("Single Car Series Prefix: " + seriesPrefix);
		
		if(PlayerPrefs.HasKey("CarFocus")){
			currentCar = PlayerPrefs.GetInt("CarFocus");
			PlayerPrefs.DeleteKey("CarFocus");
		} else {
			currentCar = 1;
		}
		
		currentCarManu = DriverNames.getManufacturer(seriesPrefix, currentCar);
		currentCarTeam = DriverNames.getTeam(seriesPrefix, currentCar);
		currentCarType = DriverNames.getType(seriesPrefix, currentCar);
		currentCarRarity = DriverNames.getRarity(seriesPrefix, currentCar);
		
		teamMates.Clear();
		for(int car=0;car<100;car++){
			if((DriverNames.getTeam(seriesPrefix, car) == currentCarTeam)
				&&(car != currentCar)){
				teamMates.Add(car);
			}
		}
		myTeamMates = teamMates.ToArray();
		
		windowscroll = 2.5f;
		
		carGears = 0;
		carClass = 0;
		
		unlockClass = 1;
		unlockClass = DriverNames.getRarity(seriesPrefix, currentCar);
		unlockGears = GameData.unlockGears(unlockClass);
		
		if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "Gears")){
			carGears = PlayerPrefs.GetInt(seriesPrefix + currentCar + "Gears");
			carClass = PlayerPrefs.GetInt(seriesPrefix + currentCar + "Class");
		}
		
		if(PlayerPrefs.GetInt(seriesPrefix + currentCar + "Unlocked") != 1){
			PlayerPrefs.SetInt(seriesPrefix + currentCar + "Unlocked", 1);
			PlayerPrefs.SetInt(seriesPrefix + currentCar + "Gears", carGears - unlockGears);
			PlayerPrefs.SetInt(seriesPrefix + currentCar + "Class", DriverNames.getRarity(seriesPrefix, currentCar));
			carGears = PlayerPrefs.GetInt(seriesPrefix + currentCar + "Gears");
			carClass = DriverNames.getRarity(seriesPrefix, currentCar);
			classMax = GameData.classMax(carClass);
		}
		
        classMax = GameData.classMax(carClass);
		
		availableNumbers.Clear();
		
		for(int car=0;car<100;car++){
			if(Resources.Load("cup20num" + car) != null){
				if(PlayerPrefs.GetInt(seriesPrefix + car + "Unlocked") == 1){
					if(car != currentCar){
						//Debug.Log("Can swap with car: " + car);
						availableNumbers.Add(car);
					}
				} else {
					//Debug.Log("Cannot swap with car: " + car + ". Unlocked = " + PlayerPrefs.GetInt(seriesPrefix + car + "Unlocked"));
				}
			} else {
				//Debug.Log("No number resource for: " + car);
			}
		}
		availNums = availableNumbers.ToArray();
		
		customNumX = 34;
		if(seriesPrefix == "cup22"){
			customNumX = 30;
		}
		
		freeAgents.Clear();
		
		if(!PlayerPrefs.HasKey("TransfersLeft")){
			PlayerPrefs.SetInt("TransfersLeft",1);
		}
		transfersLeft = PlayerPrefs.GetInt("TransfersLeft");
		
		if(!PlayerPrefs.HasKey("TransferTokens")){
			PlayerPrefs.SetInt("TransferTokens",1);
		}
		transfersMax = PlayerPrefs.GetInt("TransferTokens");
		
		transferError = "";
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
		GUI.DrawTexture(new Rect(cardX + cardW - (widthblock * 1.25f), cardY + 10, widthblock * 1f, widthblock * 0.5f), manufacturerTex);
		
		Texture2D rarityStars = null;
		switch(DriverNames.getRarity(seriesPrefix, currentCar)){
			case 1:
				rarityStars = starOne;
				break;
			case 2:
				rarityStars = starTwo;
				break;
			case 3:
				rarityStars = starThree;
				break;
			default:
				break;
		}
		GUI.DrawTexture(new Rect(cardX + (widthblock * 2.5f), cardY + 10, widthblock * 1f, widthblock * 0.5f), rarityStars);
		
		GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load("20liveryblank") as Texture);
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + currentCar)){
			int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar);
			if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "AltPaint")){
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + currentCar + "AltPaint")) as Texture);
			} else {
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery + "blank") as Texture);
			}
			GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f) + (((widthblock * 5f)/64)*customNumX), cardY + (heightblock * 2) + ((widthblock * 2.5f)/4), widthblock * 1.25f, widthblock * 1.25f), Resources.Load("cup20num" + customNum) as Texture);
		} else {
			if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "AltPaint")){
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery + "alt" + PlayerPrefs.GetInt(seriesPrefix + currentCar + "AltPaint")) as Texture);
			} else {
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery) as Texture);
			}
		}
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "AltDriver")){
			GUI.Label(new Rect(cardX, cardY + (heightblock * 7.5f), widthblock * 6, heightblock * 2), PlayerPrefs.GetString(seriesPrefix + currentCar + "AltDriver"));
		} else {
			GUI.Label(new Rect(cardX, cardY + (heightblock * 7.5f), widthblock * 6, heightblock * 2), DriverNames.getName(seriesPrefix, currentCar));
		}
		
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
			GUI.skin.label.normal.textColor = Color.black;
		}
		
		if(carClass == 6){
			GUI.skin.label.normal.textColor = Color.white;
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), widthblock * 5.5f, heightblock * 1.5f), "Max Class");
		} else {
			GUI.skin.label.normal.textColor = Color.white;
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 9.5f), widthblock * 5.5f, heightblock * 1.5f), carGears + "/" + classMax);
		}
		GUI.skin.label.normal.textColor = Color.black;
		
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		//Upgrade
		GUI.skin.label.normal.textColor = Color.white;
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
				
				//Initial first unlocks
				if(GameData.upgradeCost(carClass) == 0){
					if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "Gears")){
						PlayerPrefs.SetInt(seriesPrefix + currentCar + "Gears", carGears - unlockGears);
						PlayerPrefs.SetInt(seriesPrefix + currentCar + "Class", DriverNames.getRarity(seriesPrefix, currentCar));
						carClass = DriverNames.getRarity(seriesPrefix, currentCar);
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
		GUI.skin.label.normal.textColor = Color.black;
		
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
		
				for(int t=0;t<6;t++){
					if(t < myTeamMates.Length){
						GUI.DrawTexture(new Rect((widthblock * 7.5f) + ((heightblock * 2.5f) * t), heightblock * 8.5f, heightblock * 2f, heightblock * 1), Resources.Load(seriesPrefix + "livery" + myTeamMates[t]) as Texture);
					}
				}
				
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
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Strategists Change Lane Faster Per Class");
						break;
					case "Rookie":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Rookies Are Blocked Less Often");
						break;
					case "Closer":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Closers Pick Up A Draft From Further Away");
						break;
					case "Dominator":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Dominators Lose Less Speed In Clean Air");
						break;
					case "Blocker":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Blockers Hold More Speed When Bump Drafted");
						break;
					case "Gentleman":
						GUI.Label(new Rect(widthblock * 7.5f, heightblock * 13f, widthblock * 11.5f, heightblock * 2), "Everyone Will Work With Gentleman Drivers");
						break;
				}
				GUI.skin = buttonSkin;
				break;
			case "Paints":
				
				//Stock Paint
				cardX = widthblock * 7.5f;
				cardY = heightblock * 6f;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 3.5f, heightblock * 6f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 3f, heightblock * 2), "Stock");
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 3f, heightblock * 2f), Resources.Load(seriesPrefix + "livery" + currentCar) as Texture, ScaleMode.ScaleToFit);
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin = redGUI;
				
				if(PlayerPrefs.HasKey(seriesPrefix + currentCar + "AltPaint")){
					if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4), widthblock * 3f, heightblock * 1.5f), "Select")){
						PlayerPrefs.DeleteKey(seriesPrefix + currentCar + "AltPaint");
						PlayerPrefs.DeleteKey(seriesPrefix + currentCar + "AltDriver");
					}
				} else {
					GUI.skin.label.alignment = TextAnchor.MiddleCenter;
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4), widthblock * 3f, heightblock * 1.5f), "Selected");
				}
				GUI.skin = tileSkin;
				
				//Alt Paints Row
				for(int columns = 1; columns < 3; columns++){
					if(AltPaints.getAltPaintName(seriesPrefix, currentCar,columns) != null){
					
						cardX = widthblock * (columns * 4.25f) + (widthblock * 7.25f);
						cardY = heightblock * 6f;
						
						GUI.skin = whiteGUI;
						GUI.Box(new Rect(cardX, cardY, widthblock * 3.5f, heightblock * 6f), "");
						GUI.skin = tileSkin;
						
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.button.fontSize = 64 / FontScale.fontScale;
						
						GUI.skin.label.alignment = TextAnchor.UpperCenter;
						GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 3f, heightblock * 2), AltPaints.getAltPaintName(seriesPrefix, currentCar,columns));
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 3f, heightblock * 2f), Resources.Load(seriesPrefix + "livery" + currentCar + "alt" + columns) as Texture, ScaleMode.ScaleToFit);
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;

						GUI.skin = redGUI;
						
						if(PlayerPrefs.GetInt(seriesPrefix + currentCar + "Alt" + columns + "Unlocked") == 1){
							if(PlayerPrefs.GetInt(seriesPrefix + currentCar + "AltPaint") != columns){
								if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4), widthblock * 3f, heightblock * 1.5f), "Select")){
									PlayerPrefs.SetInt(seriesPrefix + currentCar + "AltPaint", columns);
									if(AltPaints.getAltPaintDriver(seriesPrefix,currentCar,columns) != null){
										PlayerPrefs.SetString(seriesPrefix + currentCar + "AltDriver", AltPaints.getAltPaintDriver(seriesPrefix,currentCar,columns));
										Debug.Log("Driver Name set: " + AltPaints.getAltPaintDriver(seriesPrefix,currentCar,columns));
									}
								}
							} else {
								GUI.skin.label.alignment = TextAnchor.MiddleCenter;
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4), widthblock * 3f, heightblock * 1.5f), "Selected");
							}
						} else {
							GUI.skin = tileSkin;
							GUI.Box(new Rect(cardX, cardY, widthblock * 3.5f, heightblock * 6f), "");
						}
						GUI.skin = tileSkin;
					}
				}
				
				//Alt Paints 2nd Row
				int offset = 2;
				for(int columns = 1; columns < 4; columns++){

					if(AltPaints.getAltPaintName(seriesPrefix, currentCar,columns+offset) != null){
					
						cardX = widthblock * (columns * 4.25f) + (widthblock * 3.25f);
						cardY = heightblock * 12.5f;
						
						GUI.skin = whiteGUI;
						GUI.Box(new Rect(cardX, cardY, widthblock * 3.5f, heightblock * 6f), "");
						GUI.skin = tileSkin;
						
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.button.fontSize = 64 / FontScale.fontScale;
						
						GUI.skin.label.alignment = TextAnchor.UpperCenter;
						GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 3f, heightblock * 2), AltPaints.getAltPaintName(seriesPrefix, currentCar,columns+offset));
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 3f, heightblock * 2f), Resources.Load(seriesPrefix + "livery" + currentCar + "alt" + (columns + offset)) as Texture, ScaleMode.ScaleToFit);
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;

						GUI.skin = redGUI;
						
						if(PlayerPrefs.GetInt(seriesPrefix + currentCar + "Alt" + (columns + offset) + "Unlocked") == 1){
							if(PlayerPrefs.GetInt(seriesPrefix + currentCar + "AltPaint") != (columns+offset)){
								if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4f), widthblock * 3f, heightblock * 1.5f), "Select")){
									PlayerPrefs.SetInt(seriesPrefix + currentCar + "AltPaint", columns+offset);
									if(AltPaints.getAltPaintDriver(seriesPrefix,currentCar,columns+offset) != null){
										PlayerPrefs.SetString(seriesPrefix + currentCar + "AltDriver", AltPaints.getAltPaintDriver(seriesPrefix,currentCar,columns+offset));
										Debug.Log("Driver Name set: " + AltPaints.getAltPaintDriver(seriesPrefix,currentCar,columns+offset));
									}
								}
							} else {
								GUI.skin.label.alignment = TextAnchor.MiddleCenter;
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4f), widthblock * 3f, heightblock * 1.5f), "Selected");
							}
						} else {
							GUI.skin = tileSkin;
							GUI.Box(new Rect(cardX, cardY, widthblock * 3.5f, heightblock * 6f), "");
						}
						GUI.skin = tileSkin;
					}
				}
				
				break;
			case "Stats":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 7f, widthblock * 4f, heightblock * 2), "Starts");
				GUI.Label(new Rect(widthblock * 10.5f, heightblock * 7f, widthblock * 4f, heightblock * 2), "" + PlayerPrefs.GetInt("TotalStarts" + seriesPrefix + currentCar));
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 8.5f, widthblock * 4f, heightblock * 2), "Wins");
				GUI.Label(new Rect(widthblock * 10.5f, heightblock * 8.5f, widthblock * 4f, heightblock * 2), "" + PlayerPrefs.GetInt("TotalWins" + seriesPrefix + currentCar));
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 10f, widthblock * 4f, heightblock * 2), "Top 5s");
				GUI.Label(new Rect(widthblock * 10.5f, heightblock * 10f, widthblock * 4f, heightblock * 2), "" + PlayerPrefs.GetInt("TotalTop5s" + seriesPrefix + currentCar));
				break;
			case "Transfers":
			
				GUI.DrawTexture(new Rect(widthblock * 7.5f, heightblock * 7f, heightblock * 1f, heightblock * 1), contracts);
				GUI.Label(new Rect(widthblock * 8.5f, heightblock * 7f, widthblock * 11.75f, heightblock * 3f), "" + transfersLeft + "/" + transfersMax + " Transfer Contracts Available");
				
				GUI.skin.label.normal.textColor = Color.red;
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 11f, widthblock * 6.25f, heightblock * 6f), transferError);
				GUI.skin.label.normal.textColor = Color.black;
				
				/*if(!PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + currentCar)){
					if (GUI.Button(new Rect(widthblock * 14.75f, heightblock * 9f, widthblock * 4.5f, heightblock * 1.5f), "Driver Swap")){
						if(transfersLeft > 0){
							driverPanel = true;
						} else {
							transferError = "All transfer contracts are in use. Gain more by leveling up or purchase the Negotiator pack in store.";
							//Debug.Log("No transfer contracts left");
						}
					}
				} else {
					GUI.skin.label.alignment = TextAnchor.MiddleRight;
					GUI.Label(new Rect(widthblock * 14.75f, heightblock * 11f, widthblock * 4.5f, heightblock * 1.5f), "Custom Number: #" + PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar));
				}*/
				
				if(!PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + currentCar)){
					if (GUI.Button(new Rect(widthblock * 14.75f, heightblock * 11f, widthblock * 4.5f, heightblock * 1.5f), "Number Swap")){
						if(transfersLeft > 0){
							numberPanel = true;
						} else {
							transferError = "All transfer contracts are in use. Gain more by leveling up or purchase the Negotiator pack in the Store.";
							//Debug.Log("No transfer contracts left");
						}
					}
				} else {
					GUI.skin.label.alignment = TextAnchor.MiddleRight;
					GUI.Label(new Rect(widthblock * 14.75f, heightblock * 11f, widthblock * 4.5f, heightblock * 1.5f), "Custom Number: #" + PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar));
				}
				
				if (GUI.Button(new Rect(widthblock * 15.25f, heightblock * 13f, widthblock * 4f, heightblock * 1.5f), "Change Team")){
					transferError = "Team changes coming soon";
				}
				if (GUI.Button(new Rect(widthblock * 13.25f, heightblock * 15f, widthblock * 6f, heightblock * 1.5f), "Change Manufacturer")){
					transferError = "Manufacturer changes coming soon";
				}
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + currentCar)){
					GUI.skin = redGUI;
					if (GUI.Button(new Rect(widthblock * 14.75f, heightblock * 17f, widthblock * 4.5f, heightblock * 1.5f), "Reset Car")){
						PlayerPrefs.DeleteKey("CustomNumber" + seriesPrefix + currentCar);
						checkNumberDupes(currentCar, seriesPrefix, currentCar);
						transfersLeft+=1;
						if(transfersLeft > transfersMax){
							transfersLeft = transfersMax;
						}
						PlayerPrefs.SetInt("TransfersLeft",transfersLeft);
					}
				}
				//At least one transfer to reset
				if(transfersLeft != transfersMax){
					GUI.skin = redGUI;
					if (GUI.Button(new Rect(widthblock * 13.25f, heightblock * 17f, widthblock * 6f, heightblock * 1.5f), "Reset All Transfers")){
						resetAllTransfers(seriesPrefix);
						transfersLeft = transfersMax;
						PlayerPrefs.SetInt("TransfersLeft",transfersLeft);
					}
				}
				break;
		}
		
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin = buttonSkin;
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();
		
		if(driverPanel == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 2f, heightblock * 3f, widthblock * 16f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperLeft;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 2.5f, heightblock * 4f, widthblock * 7.5f, heightblock * 2f), "Change Driver");

			numsInd = 0;
			
			for(int i=0;i<4;i++){
				if(numsInd < availNums.Length){
					if (GUI.Button(new Rect(widthblock * 2.5f, heightblock * 6f, widthblock * 1f, widthblock * 1f), "")){
						
						//Example: I swap car #4 with car #10..
						//Set car #4 to become #10
						PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentCar, availNums[numsInd]);
						
						//Does car #10 actually has number 10 to swap with, or do they also have a custom number..
						if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + availNums[numsInd])){
							//Find who has number 10.. in this example, it is the #18
							int currentNumHolder = findCustomNum(seriesPrefix, availNums[numsInd]);
							//Set #18 as #4 instead, to preserve the numbers to and allow 3-way swaps
							PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentNumHolder, currentCar);
							
							//Remove any dupes
							checkNumberDupes(currentNumHolder, seriesPrefix, currentCar);
							checkNumberDupes(currentCar, seriesPrefix, currentNumHolder);
							
						} else {
							//If not, set #10 as #4
							PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + availNums[numsInd], currentCar);
							
							//Remove any dupes
							checkNumberDupes(availNums[numsInd], seriesPrefix, currentCar);
							checkNumberDupes(currentCar, seriesPrefix, availNums[numsInd]);
						}
						if(PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar) == currentCar){
							PlayerPrefs.DeleteKey("CustomNumber" + seriesPrefix + currentCar);
							transferError = "Transfer Rejected \n(#" + availNums[numsInd] + " Unavailable)";
						} else {
							transfersLeft-=1;
							PlayerPrefs.SetInt("TransfersLeft",transfersLeft);
						}
						
						Debug.Log("Car #" + currentCar + " now uses #" + PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar) + ". Var: " + "CustomNumber" + seriesPrefix + availNums[numsInd]);
						
						driverPanel = false;
					}
					GUI.DrawTexture(new Rect(widthblock * 2.5f, heightblock * 6f, widthblock * 1f - 10, widthblock * 1f - 10), Resources.Load(seriesPrefix + "num" + availNums[numsInd]) as Texture);
					numsInd++;
				}
			}
		}
		
		if(numberPanel == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 2f, heightblock * 3f, widthblock * 16f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperLeft;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 2.5f, heightblock * 4f, widthblock * 7.5f, heightblock * 2f), "Change Number");

			numsInd = 0;
			
			if(availNums.Length == 0){
				GUI.Label(new Rect(widthblock * 2.5f, heightblock * 6f, widthblock * 15f, heightblock * 2f), "You need at to own at least 2 cars to swap numbers!");
			}
			
			for(int i=0;i<4;i++){
				for(int j=0;j<12;j++){
					if(numsInd < availNums.Length){
						if (GUI.Button(new Rect(widthblock * 2.5f + (widthblock * j * 1.25f), (heightblock * 6f) + (heightblock * i * 2f), widthblock * 1f, widthblock * 1f), "")){
							
							//Example: I swap car #4 with car #10..
							//Set car #4 to become #10
							PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentCar, availNums[numsInd]);
							
							//Does car #10 actually has number 10 to swap with, or do they also have a custom number..
							if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + availNums[numsInd])){
								//Find who has number 10.. in this example, it is the #18
								int currentNumHolder = findCustomNum(seriesPrefix, availNums[numsInd]);
								//Set #18 as #4 instead, to preserve the numbers to and allow 3-way swaps
								PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentNumHolder, currentCar);
								
								//Remove any dupes
								checkNumberDupes(currentNumHolder, seriesPrefix, currentCar);
								checkNumberDupes(currentCar, seriesPrefix, currentNumHolder);
								
							} else {
								//If not, set #10 as #4
								PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + availNums[numsInd], currentCar);
								
								//Remove any dupes
								checkNumberDupes(availNums[numsInd], seriesPrefix, currentCar);
								checkNumberDupes(currentCar, seriesPrefix, availNums[numsInd]);
							}
							if(PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar) == currentCar){
								PlayerPrefs.DeleteKey("CustomNumber" + seriesPrefix + currentCar);
								transferError = "Transfer Rejected \n(#" + availNums[numsInd] + " Unavailable)";
							} else {
								transfersLeft-=1;
								PlayerPrefs.SetInt("TransfersLeft",transfersLeft);
							}
							
							//Debug.Log("Car #" + currentCar + " now uses #" + PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + currentCar) + ". Var: " + "CustomNumber" + seriesPrefix + availNums[numsInd]);
							
							numberPanel = false;
						}
						GUI.DrawTexture(new Rect(widthblock * 2.5f + (widthblock * j * 1.25f) + 5, (heightblock * 6f) + (heightblock * i * 2f) + 5, widthblock * 1f - 10, widthblock * 1f - 10), Resources.Load("cup20num" + availNums[numsInd]) as Texture);
						numsInd++;
					}
				}
			}
		}

		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		if((numberPanel == true)||(driverPanel == true)){
			if (GUI.Button(new Rect(widthblock * 0.5f, 20, widthblock * 2f, heightblock * 1.5f), "Back")){
				driverPanel = false;
				numberPanel = false;
			}
		} else {
			CommonGUI.BackButton("Menus/MainMenu");
		}
		GUI.skin = buttonSkin;

		if (Input.GetKeyDown(KeyCode.Escape)){
			if((numberPanel == true)||(driverPanel == true)){
				driverPanel = false;
				numberPanel = false;
			} else {
				SceneManager.LoadScene("AllCars");
			}
		}
	}
	
	void resetAllTransfers(string prefix){
		for(int i=0;i<99;i++){
			if(PlayerPrefs.HasKey("CustomNumber" + prefix + i)){
				PlayerPrefs.DeleteKey("CustomNumber" + prefix + i);
				Debug.Log("Reset transfer #" + i + "");
			}
		}
	}
	
	void checkNumberDupes(int change, string prefix, int number){
		for(int i=0;i<99;i++){
			if(i != change){
				if(PlayerPrefs.HasKey("CustomNumber" + prefix + i)){
					if(PlayerPrefs.GetInt("CustomNumber" + prefix + i) == number){
						PlayerPrefs.DeleteKey("CustomNumber" + prefix + i);
						Debug.Log("Removed dupe " + i + "(#" + number + ")");
					}
				}
			}
		}
	}
	
	int findCustomNum(string prefix, int number){
		for(int i=0;i<99;i++){
			if(PlayerPrefs.HasKey("CustomNumber" + prefix + i)){
				if(PlayerPrefs.GetInt("CustomNumber" + prefix + i) == number){
					return i;
				}
			}
		}
		return 9999;
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
				classColour = new Color32(164,6,6,255);
				break;
		    case 2:
				classColour = new Color32(255,165,0,255);
				break;
			case 3:
				classColour = new Color32(238,130,238,255);
				break;
			case 4:
				classColour = new Color32(0,128,0,255);
				break;
			case 5:
				classColour = new Color32(0,0,255,255);
				break;
			case 6:
				classColour = new Color32(75,0,130,255);
				break;
		    default:
				classColour = new Color32(164,6,6,255);
				break;
		}
		return classColour;
	}
}
