using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AllCars : MonoBehaviour {
	
    public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin blueGUI;
	public GUISkin redGUI;
	public GUISkin whiteGUI;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	string filterSeries;
	string seriesPrefix;
	bool seriesPanel;
	
	public Texture BoxTexture;

	int playerLevel;
	int totalMoney;
	int carMoney;
	int moneyCount;
	int premiumTokens;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	public Texture2D frdTex;
	public Texture2D chvTex;
	public Texture2D tytTex;
	
	public Texture2D starOne;
	public Texture2D starTwo;
	public Texture2D starThree;
	public Texture2D starFour;

	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		filterSeries = "";
		
		seriesPrefix = "cup20";
		seriesPanel = false;
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		
		//For returning PlayFab call outputs, reset on Awake
		PlayerPrefs.SetString("SaveLoadOutput","");
		
		//Count Unlocks..
		int totalUnlocks = 0;
		for(int i=0;i<100;i++){
			if(DriverNames.getName(seriesPrefix, i) != null){
				if(PlayerPrefs.HasKey(seriesPrefix + i + "Gears")){
					if(PlayerPrefs.GetInt(seriesPrefix + i + "Unlocked") == 1){
						totalUnlocks++;
					}
				}
			}
		}
		if(PlayerPrefs.HasKey("TotalUnlocks")){
			//If a new car has been unlocked, and you have more than just the starter car..
			if((PlayerPrefs.GetInt("TotalUnlocks") < totalUnlocks)&&(totalUnlocks > 1)){
				PlayerPrefs.SetInt("TotalUnlocks", totalUnlocks);
				//If logged in as someone
				if(PlayerPrefs.HasKey("PlayerUsername")){
					//Then do an autosave
					string progressJSON = JSONifyProgress(seriesPrefix);
					PlayFabManager.AutosavePlayerProgress(progressJSON);
				}
			}
		} else {
			PlayerPrefs.SetInt("TotalUnlocks", totalUnlocks);
		}
	}

    // Update is called once per frame
    void Update(){
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
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
	
	GUISkin classSkin(int carClass){
		GUISkin skinCol;
		switch(carClass){
			case 1:
				skinCol = redGUI;
				break;
		    case 2:
				skinCol = redGUI;
				break;
			case 3:
				skinCol = blueGUI;
				break;
			case 4:
				skinCol = blueGUI;
				break;
			case 5:
				skinCol = blueGUI;
				break;
		    default:
				skinCol = redGUI;
				break;
		}
		return skinCol;
	}
	
	void OnGUI(){
		
		GUI.skin = buttonSkin;
		
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		if (GUI.Button(new Rect(widthblock * 3f, 20, widthblock * 3, heightblock * 1.5f), seriesPrefix)){
			seriesPanel = true;
		}
		
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.skin.button.fontSize = 96 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.UpperRight;
		GUI.Label(new Rect(widthblock * 6.5f, 20, widthblock * 3, heightblock * 2), "Garage");
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;

		int carCount = 0;
		float windowscroll = 4.7f;
		
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		scrollPosition = GUI.BeginScrollView(new Rect(0, heightblock * 2.5f, Screen.width, Screen.height - (heightblock * 2.5f)), scrollPosition, new Rect(0, heightblock * 2.5f, Screen.width - (Screen.width / 20) - 10, Screen.height * windowscroll));

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin = tileSkin;
		
		float cardW = widthblock * 3;
		float cardH = heightblock * 7.5f;
		
		if(filterSeries == ""){
			
			//int maxCars = DriverNames.cup2020Names.Length;
			int maxCars = DriverNames.getFieldSize(seriesPrefix);
			
			//Loop through cars
			for(int rows = 1; rows < 11; rows++){
				for(int columns = 1; columns < 6; columns++){
					
					//If a driver # is not registered
					while(DriverNames.getName(seriesPrefix, carCount) == null){
						if(carCount < 100){
							carCount++;
						} else {
							break;
						}
					}
					
					if(carCount >= 100){
						break;
					}
					
					int carUnlocked = 0;
					int carGears = 0;
					int carClass = 0;
					
					//Initialise (can be used for dev reset)
					if(!PlayerPrefs.HasKey(seriesPrefix + carCount + "Gears")){
						//Debug.Log("#" + carCount + " Not Initialised");
						PlayerPrefs.SetInt(seriesPrefix + carCount + "Unlocked",0);
						PlayerPrefs.SetInt(seriesPrefix + carCount + "Gears",0);
						PlayerPrefs.SetInt(seriesPrefix + carCount + "Class",0);
					}
					carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
					carGears = PlayerPrefs.GetInt(seriesPrefix + carCount + "Gears");
					carClass = PlayerPrefs.GetInt(seriesPrefix + carCount + "Class");
						
					int unlockClass = 1;
					unlockClass = DriverNames.getRarity(seriesPrefix, carCount);
					int unlockGears = GameData.unlockGears(unlockClass);
					
					
					if(carCount < 100){
						string carLivery = seriesPrefix + "livery" + (carCount);
						float cardX = widthblock * (columns * 3.5f) - (widthblock * 3f);
						float cardY = heightblock * (8.5f * rows) - (heightblock * 4.5f);
						
						int classMax = 999;
						classMax = GameData.classMax(carClass);
						if(carClass < unlockClass){
							classMax = unlockGears;
						}
						
						GUI.skin = whiteGUI;
						
						GUI.Box(new Rect(cardX, cardY, cardW, cardH), "");
						
						GUI.skin = tileSkin;
						
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.label.alignment = TextAnchor.UpperLeft;
						GUI.Label(new Rect(cardX + 10, cardY + 10, widthblock * 1.5f, heightblock * 1f), DriverNames.getTeam(seriesPrefix, carCount));
						Texture2D manufacturerTex = null;
						switch(DriverNames.getManufacturer(seriesPrefix, carCount)){
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
						GUI.DrawTexture(new Rect(cardX + cardW - (widthblock * 0.75f) - 10, cardY + 10, widthblock * 0.75f, widthblock * 0.375f), manufacturerTex);
						
						Texture2D rarityStars = null;
						switch(DriverNames.getRarity(seriesPrefix, carCount)){
							case 1:
								rarityStars = starOne;
								break;
							case 2:
								rarityStars = starTwo;
								break;
							case 3:
								rarityStars = starThree;
								break;
							case 4:
								rarityStars = starFour;
								break;
							default:
								break;
						}
						GUI.DrawTexture(new Rect(cardX + (widthblock * 1f), cardY + 10, widthblock * 0.75f, widthblock * 0.375f), rarityStars);
						
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("20liveryblank") as Texture);
						if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carCount)){
							int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carCount);
							if(PlayerPrefs.HasKey(seriesPrefix + carCount + "AltPaint")){
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + carCount + "AltPaint")) as Texture);
							} else {
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "blank") as Texture);
							}
							GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f) + (((widthblock * 2.5f)/64)*34), cardY + (heightblock * 1.25f) + ((widthblock * 1.25f)/4), widthblock * 0.625f, widthblock * 0.625f), Resources.Load("cup20num" + customNum) as Texture);
						} else {
							if(PlayerPrefs.HasKey(seriesPrefix + carCount + "AltPaint")){
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "alt" + PlayerPrefs.GetInt(seriesPrefix + carCount + "AltPaint")) as Texture);
							} else {
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery) as Texture);
							}
						}
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						if(PlayerPrefs.HasKey(seriesPrefix + carCount + "AltDriver")){
							GUI.Label(new Rect(cardX, cardY + (heightblock * 3.5f), widthblock * 3, heightblock * 2), PlayerPrefs.GetString(seriesPrefix + carCount + "AltDriver"));
						} else {
							GUI.Label(new Rect(cardX, cardY + (heightblock * 3.5f), widthblock * 3, heightblock * 2), DriverNames.getName(seriesPrefix, carCount));
						}
						//Progress Bar Box
						GUI.Box(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "");
						//Progress Bar
						if((carGears > classMax)||(carClass == 6)){
							GUI.skin = tileSkin;
							
							GUI.Box(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "");
						} else {
							if(carGears > 0){
								GUI.Box(new Rect(cardX + 10, cardY + (heightblock * 5f), (((cardW - 20)/classMax) * carGears) + 1, heightblock * 1f), "");
							}
						}
						// Gears/Class 
						if(carClass == 6){
							GUI.skin.label.normal.textColor = Color.white;
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "Max Class");
							GUI.skin.label.normal.textColor = Color.black;
						} else {
							if(carClass < DriverNames.getRarity(seriesPrefix, carCount)){
								GUI.skin.label.normal.textColor = Color.white;
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
								GUI.skin.label.normal.textColor = Color.black;
							} else {
								if(carGears >= classMax){
									GUI.skin.label.normal.textColor = new Color32(0,255,0,255);
								} else {
									GUI.skin.label.normal.textColor = Color.white;
								}
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
								GUI.skin.label.normal.textColor = Color.black;
							}
						}
						if((carClass == 0)&&(carGears >= unlockGears)){
							GUI.skin = redGUI;
							GUI.skin.button.fontSize = 48 / FontScale.fontScale;
							if (GUI.Button(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "Unlock")){
								PlayerPrefs.SetInt(seriesPrefix + carCount + "Unlocked", 1);
								PlayerPrefs.SetInt(seriesPrefix + carCount + "Gears", carGears - unlockGears);
								carClass = DriverNames.getRarity(seriesPrefix, carCount);
								PlayerPrefs.SetInt(seriesPrefix + carCount + "Class", carClass);
								classMax = GameData.classMax(carClass);
							}
							GUI.skin = tileSkin;
						} else {
							if((carGears >= classMax)&&(carClass > 0)&&(carClass < 6)){
								GUI.skin = redGUI;
								GUI.skin.button.fontSize = 48 / FontScale.fontScale;
								if (GUI.Button(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "Upgrade")){
									PlayerPrefs.SetInt("CarFocus",carCount);
									PlayerPrefs.SetString("SeriesFocus","cup20");
									Application.LoadLevel("SingleCar");
								}
								GUI.skin = tileSkin;
							}
						}
						GUI.skin.label.alignment = TextAnchor.LowerLeft;
						GUI.Label(new Rect(cardX + 10, cardY + cardH - (heightblock * 2) - 10, widthblock * 1.5f, heightblock * 2), DriverNames.shortenedType(DriverNames.getType(seriesPrefix, carCount)));
						GUI.skin.label.alignment = TextAnchor.LowerRight;
						
						//Locked cars go behind a box layer
						if((carClass == 0)&&(carGears < unlockGears)){
							GUI.Box(new Rect(cardX, cardY, cardW, cardH), "");
						} else {
							GUI.Label(new Rect(cardX + (widthblock * 1.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 1.5f) - 10, heightblock * 2), "Class " + classAbbr(carClass));
							GUI.skin.label.normal.textColor = classColours(carClass);
							GUI.Label(new Rect(cardX + (widthblock * 1.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 1.5f) - 10, heightblock * 2), "" + classAbbr(carClass));
							GUI.skin.label.normal.textColor = Color.black;
							
							if(GUI.Button(new Rect(cardX, cardY, cardW, cardH), "")){
								PlayerPrefs.SetInt("CarFocus",carCount);
								PlayerPrefs.SetString("SeriesFocus",seriesPrefix);
								Application.LoadLevel("SingleCar");
							}
						}
						carCount++;
					}
				}
			}
			
			//Save/Load options
			if(PlayerPrefs.HasKey("PlayerUsername")){
				GUI.skin = redGUI;
				if (GUI.Button(new Rect(widthblock * 3, (heightblock * (8f * 11)) + heightblock * 2f, widthblock * 6, heightblock * 2f), "Save To Server")){
					string progressJSON = JSONifyProgress(seriesPrefix);
					PlayFabManager.SavePlayerProgress(progressJSON);
				}
				if (GUI.Button(new Rect(widthblock * 11, (heightblock * (8f * 11)) + heightblock * 2f, widthblock * 6, heightblock * 2f), "Load From Server")){
					PlayFabManager.GetSavedPlayerProgress();
				}
				GUI.skin = tileSkin;
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(widthblock * 3, (heightblock * (8f * 11)) + heightblock * 5f, widthblock * 14f, heightblock * 4f), "" + PlayerPrefs.GetString("SaveLoadOutput"));
			}
		}
		
		GUI.EndScrollView();
	
		//GUI.skin.button.alignment = TextAnchor.MiddleLeft;

		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		CommonGUI.BackButton("MainMenu");
		
		GUI.skin = buttonSkin;
		
		if(seriesPanel == true){
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if (GUI.Button(new Rect(widthblock * 3f, (heightblock * 2f) + 20, widthblock * 3, heightblock * 1.5f), "cup22")){
				seriesPrefix = "cup22";
				seriesPanel = false;
			}
		}
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
	
	string JSONifyProgress(string seriesPrefix){
		string JSONOutput = "{";
		JSONOutput += "\"playerLevel\": \"" + PlayerPrefs.GetInt("Level").ToString() + "\",";
		JSONOutput += "\"seriesName\": \"" + seriesPrefix + "\",";
		JSONOutput += "\"drivers\": [";
		for(int car = 0; car < 100; car++){
			//Initialise (can be used for dev reset)
			int carUnlocked = 0;
			int carClass = 0;
			int carGears = 0;
			if(PlayerPrefs.HasKey(seriesPrefix + car + "Gears")){
				carUnlocked = PlayerPrefs.GetInt(seriesPrefix + car + "Unlocked");
				carClass = PlayerPrefs.GetInt(seriesPrefix + car + "Class");
				carGears = PlayerPrefs.GetInt(seriesPrefix + car + "Gears");
			}
			if(car > 0){
				JSONOutput += ",";
			}
			JSONOutput += "{";
			JSONOutput += "\"carNo\": \"" + car + "\",";
			JSONOutput += "\"carUnlocked\": \"" + carUnlocked + "\",";
			JSONOutput += "\"carClass\": \"" + carClass + "\",";
			JSONOutput += "\"carGears\": \"" + carGears + "\",";
			JSONOutput += "\"altPaints\": \"0";
			for(int paint=1;paint<10;paint++){
				if(AltPaints.cup2020AltPaintNames[car,paint] != null){
					if(PlayerPrefs.GetInt(seriesPrefix + car + "Alt" + paint + "Unlocked") == 1){
						//Debug.Log("Saved alt: " + AltPaints.cup2020AltPaintNames[car,paint]);
						JSONOutput += "," + paint + "";
					}
				}
			}
			JSONOutput += "\"";
			JSONOutput += "}";		
		}
		JSONOutput += "]}";
		return JSONOutput;
	}
}

[Serializable]
public class Driver {
    public string carNo;
    public string carUnlocked;
    public string carClass;
    public string carGears;
    public string altPaints;
}

[Serializable]
public class Series {
	public string playerLevel;
    public string seriesName;
    public List<Driver> drivers;
}