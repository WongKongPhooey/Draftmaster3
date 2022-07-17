using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class CarSelectGUI : MonoBehaviour {

    public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin blueGUI;
	public GUISkin redGUI;
	public GUISkin whiteGUI;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	public Texture BoxTexture;

	int playerLevel;
	int totalMoney;
	int carMoney;
	int moneyCount;
	int premiumTokens;
	int maxCars;
	
	string seriesPrefix;
	string filterSeries;
	bool seriesPanel;
	int customNumX;
	
	string restrictionType;
	string restrictionValue;
	int seriesMinClass;
	string seriesTeam;
	string seriesManu;
	int seriesCar;
	string seriesDriverType;
	int seriesRarity;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	public Texture2D frdTex;
	public Texture2D chvTex;
	public Texture2D tytTex;

	public Texture2D starOne;
	public Texture2D starTwo;
	public Texture2D starThree;

	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		
		filterSeries = "";
		seriesPanel = false;		
		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = "cup20";
		}
		if(PlayerPrefs.HasKey("LastSeriesPrefix")){
			seriesPrefix = PlayerPrefs.GetString("LastSeriesPrefix");
		}
		//seriesPrefix = "dmc15";
		
		customNumX = 34;
		if(seriesPrefix == "cup22"){
			customNumX = 30;
		} else {
			customNumX = 34;
		}
		
		//Give a free McLeod for Cup '22
		if(PlayerPrefs.GetInt("cup2278Unlocked") == 0){
			PlayerPrefs.SetInt("cup2278Unlocked",1);
			PlayerPrefs.SetInt("cup2278Gears",0);
			PlayerPrefs.SetInt("cup2278Class",1);
		}
		
		//Give free Starters for DM1 '15
		if(PlayerPrefs.GetInt("dmc150Unlocked") == 0){
			PlayerPrefs.SetInt("dmc150Unlocked",1);
			PlayerPrefs.SetInt("dmc150Gears",0);
			PlayerPrefs.SetInt("dmc150Class",1);
		}
		if(PlayerPrefs.GetInt("dmc151Unlocked") == 0){
			PlayerPrefs.SetInt("dmc151Unlocked",1);
			PlayerPrefs.SetInt("dmc151Gears",0);
			PlayerPrefs.SetInt("dmc151Class",1);
		}
		if(PlayerPrefs.GetInt("dmc152Unlocked") == 0){
			PlayerPrefs.SetInt("dmc152Unlocked",1);
			PlayerPrefs.SetInt("dmc152Gears",0);
			PlayerPrefs.SetInt("dmc152Class",1);
		}
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		
		maxCars = DriverNames.cup2020Names.Length;
			
		//Filter out cars below series restriction
		
		restrictionType = PlayerPrefs.GetString("RestrictionType");
		restrictionValue = PlayerPrefs.GetString("RestrictionValue");
		
		seriesTeam = "";
		seriesManu = "";
		seriesCar = 999;
		seriesDriverType = "";
		seriesRarity = 0;
		
		switch(restrictionType){
			case "Team":
				seriesTeam = restrictionValue;
				break;
			case "Manufacturer":
				seriesManu = restrictionValue;
				break;
			case "Car":
				seriesCar = int.Parse(restrictionValue);
				break;
			case "Rarity":
				seriesRarity = int.Parse(restrictionValue);
				break;
			case "Type":
				seriesDriverType = restrictionValue;
				Debug.Log("Driver Type: " + seriesDriverType);
				break;
			default:
				break;
		}
		
		seriesMinClass = PlayerPrefs.GetInt("SubseriesMinClass");
	}

    // Update is called once per frame
    void Update(){
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		
		if(seriesPrefix == "cup22"){
			customNumX = 30;
		} else {
			customNumX = 34;
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
		if (GUI.Button(new Rect(widthblock * 3f, 20, widthblock * 3, heightblock * 1.5f), DriverNames.getSeriesNiceName(seriesPrefix))){
			seriesPanel = true;
		}
		
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.skin.button.fontSize = 96 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.UpperRight;
		GUI.Label(new Rect(widthblock * 6.5f, 20, widthblock * 3, heightblock * 2), "Pick Car");
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;

		int carCount = 0;
		float windowscroll = 4.8f;
		int validCars = 0;
		
		int carUnlocked;
		int carClass;
		
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
			
			//Loop through unlocked cars
			for(int rows = 1; rows < 11; rows++){
				for(int columns = 1; columns < 6; columns++){
					
					carUnlocked = 0;
					carClass = 0;
					carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
					carClass = PlayerPrefs.GetInt(seriesPrefix + carCount + "Class");
					
					//Only valid drivers, unlocked and leveled up, and filtered
					while((
					(DriverNames.getName(seriesPrefix, carCount) == null)
					||(carUnlocked == 0)
					||(carClass < seriesMinClass)
					||((seriesTeam != "")&&(DriverNames.getTeam(seriesPrefix, carCount) != seriesTeam))
					||((seriesManu != "")&&(DriverNames.getManufacturer(seriesPrefix, carCount) != seriesManu))
					||((seriesCar != 999)&&(carCount != seriesCar))
					||((seriesDriverType != "")&&(DriverNames.getType(seriesPrefix, carCount) != seriesDriverType))
					||((seriesRarity != 0)&&(DriverNames.getRarity(seriesPrefix, carCount) != seriesRarity))
					)&&(carCount < 100)){
						//Skip if necessary
						if(carCount < 100){
							carCount++;
							carUnlocked = 0;
							carClass = 0;
							carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
							carClass = PlayerPrefs.GetInt(seriesPrefix + carCount + "Class");
						} else {
							break;
						}
					}
					
					if(DriverNames.getName(seriesPrefix, carCount) != null){
						string carLivery = seriesPrefix + "livery" + (carCount);
						float cardX = widthblock * (columns * 3.5f) - (widthblock * 3f);
						float cardY = heightblock * (8.5f * rows) - (heightblock * 4.5f);
						
						validCars++;
						
						carUnlocked = 0;
						int carGears = 0;
						carClass = 0;
						
						//Initiate new cars
						if(!PlayerPrefs.HasKey(seriesPrefix + carCount + "Gears")){
							PlayerPrefs.SetInt(seriesPrefix + carCount + "Unlocked", 0);
							PlayerPrefs.SetInt(seriesPrefix + carCount + "Gears",0);
							PlayerPrefs.SetInt(seriesPrefix + carCount + "Class",0);
						}
						carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
						carGears = PlayerPrefs.GetInt(seriesPrefix + carCount + "Gears");
						carClass = PlayerPrefs.GetInt(seriesPrefix + carCount + "Class");
						
						int classMax = GameData.classMax(carClass);
						
						GUI.skin = classSkin(carClass);
						
						GUI.Box(new Rect(cardX, cardY-4, cardW, cardH), "");
						
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
							default:
								break;
						}
						GUI.DrawTexture(new Rect(cardX + (widthblock * 1f), cardY + 10, widthblock * 0.75f, widthblock * 0.375f), rarityStars);
						
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("20liveryblank") as Texture);
						if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carCount)){
							int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carCount);
							if(PlayerPrefs.HasKey(seriesPrefix + carCount + "AltPaint")){
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + carCount + "AltPaint")) as Texture);
							} else {
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "blank") as Texture);
							}
							GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f) + (((widthblock * 2.5f)/64)*customNumX), cardY + (heightblock * 1.75f) + ((widthblock * 1.25f)/4), widthblock * 0.625f, widthblock * 0.625f), Resources.Load("cup20num" + customNum) as Texture);
						} else {
							if(PlayerPrefs.HasKey(seriesPrefix + carCount + "AltPaint")){
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "alt" + PlayerPrefs.GetInt(seriesPrefix + carCount + "AltPaint")) as Texture);
							} else {
								GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery) as Texture);
							}
						}
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						if(PlayerPrefs.HasKey(seriesPrefix + carCount + "AltDriver")){
							GUI.Label(new Rect(cardX, cardY + (heightblock * 4f), widthblock * 3, heightblock * 2), PlayerPrefs.GetString(seriesPrefix + carCount + "AltDriver"));
						} else {
							GUI.Label(new Rect(cardX, cardY + (heightblock * 4f), widthblock * 3, heightblock * 2), DriverNames.getName(seriesPrefix, carCount));
						}
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						
						GUI.skin.label.alignment = TextAnchor.LowerLeft;
						GUI.Label(new Rect(cardX + 10, cardY + cardH - (heightblock * 2) - 10, widthblock * 1.5f, heightblock * 2), DriverNames.shortenedType(DriverNames.getType(seriesPrefix, carCount)));
						GUI.skin.label.alignment = TextAnchor.LowerRight;
						GUI.Label(new Rect(cardX + (widthblock * 1.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 1.5f) - 10, heightblock * 2), "Class " + classAbbr(carClass));
						GUI.skin.label.normal.textColor = classColours(carClass);
						GUI.Label(new Rect(cardX + (widthblock * 1.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 1.5f) - 10, heightblock * 2), "" + classAbbr(carClass));
						GUI.skin.label.normal.textColor = Color.black;
						
						//If not unlocked or below race restrictions..
						if((carUnlocked == 0)||(carClass < seriesMinClass)){
							GUI.Box(new Rect(cardX, cardY, cardW, cardH), "");
							//Debug.Log("#" + carCount + " isn't unlocked");
						} else {
							if(GUI.Button(new Rect(cardX, cardY, cardW, cardH), "")){
								PlayerPrefs.SetString("carTexture", carLivery);
								PlayerPrefs.SetInt("CarChoice",carCount);
								PlayerPrefs.SetString("carSeries", seriesPrefix);
								//PlayerPrefs.SetString("CarSeries", seriesPrefix);
								//Debug.Log("Race Car Series is " + PlayerPrefs.GetString("carSeries"));
								Application.LoadLevel("CircuitSelect");
							}
						}
						carCount++;
					}
				}
			}
			
			if(validCars == 0){
				GUI.skin.label.fontSize = 72 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(widthblock * 2f, heightblock * 5, widthblock * 14f, heightblock * 4), "You have no cars that are eligible for this series.");
			}
			
			carCount = 0;
			
			//Loop through all cars
			for(int rows = 1; rows < 10; rows++){
				for(int columns = 1; columns < 6; columns++){
					
					carUnlocked = 0;
					carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
					
					//If a driver # is not registered
					while(((DriverNames.getName(seriesPrefix, carCount) == null)||(carUnlocked == 1))&&(carCount < 99)){
						if(carCount < 100){
							carCount++;
							carUnlocked = 0;
							carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
						} else {
							break;
						}
					}
					if(DriverNames.getName(seriesPrefix, carCount) != null){
						string carLivery = seriesPrefix + "livery" + (carCount);
						float cardX = widthblock * (columns * 3.5f) - (widthblock * 3f);
						float cardY = heightblock * (8.5f * rows) - (heightblock * 3.5f) + cardH + (heightblock * Mathf.Floor(validCars / 5)) + (cardH * Mathf.Floor(validCars / 5));
						
						carUnlocked = 0;
						int carGears = 0;
						carClass = 0;
						
						//Initiate new cars
						if(!PlayerPrefs.HasKey(seriesPrefix + carCount + "Gears")){
							PlayerPrefs.SetInt(seriesPrefix + carCount + "Unlocked", 0);
							PlayerPrefs.SetInt(seriesPrefix + carCount + "Gears",0);
							PlayerPrefs.SetInt(seriesPrefix + carCount + "Class",0);
						}
						carUnlocked = PlayerPrefs.GetInt(seriesPrefix + carCount + "Unlocked");
						carGears = PlayerPrefs.GetInt(seriesPrefix + carCount + "Gears");
						carClass = PlayerPrefs.GetInt(seriesPrefix + carCount + "Class");
						
						int classMax = GameData.classMax(carClass);
						
						GUI.skin = classSkin(carClass);
						
						GUI.Box(new Rect(cardX, cardY-4, cardW, cardH), "");
						
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
						GUI.DrawTexture(new Rect(cardX + cardW - (widthblock * 1f), cardY + 10, widthblock * 0.75f, widthblock * 0.375f), manufacturerTex);
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("20liveryblank") as Texture);
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.75f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery) as Texture);
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						GUI.Label(new Rect(cardX, cardY + (heightblock * 4f), widthblock * 3, heightblock * 2), DriverNames.getName(seriesPrefix, carCount));
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						
						GUI.skin.label.alignment = TextAnchor.LowerLeft;
						GUI.Label(new Rect(cardX + 10, cardY + cardH - (heightblock * 2) - 10, widthblock * 1.5f, heightblock * 2), DriverNames.shortenedType(DriverNames.getType(seriesPrefix, carCount)));
						GUI.skin.label.alignment = TextAnchor.LowerRight;
						GUI.Label(new Rect(cardX + (widthblock * 1.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 1.5f) - 10, heightblock * 2), "Class " + classAbbr(carClass));
						GUI.skin.label.normal.textColor = classColours(carClass);
						GUI.Label(new Rect(cardX + (widthblock * 1.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 1.5f) - 10, heightblock * 2), "" + classAbbr(carClass));
						GUI.skin.label.normal.textColor = Color.black;
						
						//Locked cars go behind a box layer
						int seriesMinClass = PlayerPrefs.GetInt("MinClass");
						
						//If not unlocked or below race restrictions..
						if((carUnlocked == 0)||(carClass < seriesMinClass)){
							GUI.Box(new Rect(cardX, cardY, cardW, cardH), "");
							//Debug.Log("#" + carCount + " isn't unlocked");
						}
						carCount++;
					}
				}
			}
		}
		
		GUI.EndScrollView();
	
		//GUI.skin.button.alignment = TextAnchor.MiddleLeft;

		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		CommonGUI.BackButton("SeriesSelect");
		
		GUI.skin = buttonSkin;
		
		if(seriesPanel == true){
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if(seriesPrefix == "cup22"){
				if (GUI.Button(new Rect(widthblock * 3f, (heightblock * 2f) + 20, widthblock * 3, heightblock * 1.5f), "Cup '20")){
					seriesPrefix = "cup20";
					PlayerPrefs.SetString("LastSeriesPrefix", seriesPrefix);
					seriesPanel = false;
				}
			} else {
				if (GUI.Button(new Rect(widthblock * 3f, (heightblock * 2f) + 20, widthblock * 3, heightblock * 1.5f), "Cup '22")){
					seriesPrefix = "cup22";
					PlayerPrefs.SetString("LastSeriesPrefix", seriesPrefix);
					seriesPanel = false;
				}
				
				if (GUI.Button(new Rect(widthblock * 3f, (heightblock * 4f) + 20, widthblock * 3, heightblock * 1.5f), "DM1 '15")){
					seriesPrefix = "dmc15";
					PlayerPrefs.SetString("LastSeriesPrefix", seriesPrefix);
					seriesPanel = false;
				}
			}
		}
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("SeriesSelect");
		}
	}
}
