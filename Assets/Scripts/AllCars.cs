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

	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		filterSeries = "";
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
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
		
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.skin.button.fontSize = 96 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 5, 20, widthblock * 5, heightblock * 2), "My Garage");
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;

		int carCount = 0;
		string seriesPrefix = "cup20";
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
			
			int maxCars = DriverNames.cup2020Names.Length;
			
			//Loop through cars
			for(int rows = 1; rows < 11; rows++){
				for(int columns = 1; columns < 6; columns++){
					
					//If a driver # is not registered
					while(DriverNames.cup2020Names[carCount] == null){
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
					
					//Reset Everything
					/*PlayerPrefs.SetInt(seriesPrefix + carCount + "Unlocked",0);
					PlayerPrefs.SetInt(seriesPrefix + carCount + "Gears",0);
					PlayerPrefs.SetInt(seriesPrefix + carCount + "Class",0);*/
					
					//Initialise
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
					unlockClass = DriverNames.cup2020Rarity[carCount];
					int unlockGears = GameData.unlockGears(unlockClass);
					
					
					if(carCount < 99){
						string carLivery = seriesPrefix + "livery" + (carCount);
						float cardX = widthblock * (columns * 3.5f) - (widthblock * 3f);
						float cardY = heightblock * (8.5f * rows) - (heightblock * 4.5f);
						
						int classMax = 999;
						classMax = GameData.classMax(carClass);
						if(carClass < unlockClass){
							classMax = unlockGears;
						}
						
						GUI.skin = classSkin(carClass);
						
						GUI.Box(new Rect(cardX, cardY-4, cardW, cardH), "");
						
						GUI.skin = whiteGUI;
						
						GUI.Box(new Rect(cardX, cardY, cardW, cardH), "");
						
						GUI.skin = tileSkin;
						
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.label.alignment = TextAnchor.UpperLeft;
						GUI.Label(new Rect(cardX + 10, cardY + 10, widthblock * 1.5f, heightblock * 1f), DriverNames.cup2020Teams[carCount]);
						Texture2D manufacturerTex = null;
						switch(DriverNames.cup2020Manufacturer[carCount]){
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
						
						Texture2D rarityStars = null;
						switch(DriverNames.cup2020Rarity[carCount]){
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
						
						GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("20liveryblank") as Texture);
						if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carCount)){
							//GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery) as Texture);
							int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carCount);
							GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery + "blank") as Texture);
							GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f) + (((widthblock * 2.5f)/64)*34), cardY + (heightblock * 1.25f) + ((widthblock * 1.25f)/4), widthblock * 0.625f, widthblock * 0.625f), Resources.Load("cup20num" + customNum) as Texture);
						} else {
							GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.25f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery) as Texture);
						}
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						GUI.Label(new Rect(cardX, cardY + (heightblock * 3.5f), widthblock * 3, heightblock * 2), DriverNames.cup2020Names[carCount]);
						GUI.skin.label.alignment = TextAnchor.MiddleCenter;
						//Progress Bar Box
						GUI.Box(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "");
						//Progress Bar
						if((carGears > classMax)||(carClass == 6)){
							GUI.Box(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "");
						} else {
							if(carGears > 0){
								GUI.Box(new Rect(cardX + 10, cardY + (heightblock * 5f), (((cardW - 20)/classMax) * carGears) + 1, heightblock * 1f), "");
							}
						}
						// Gears/Class 
						if(carClass == 6){
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "Max Class");
						} else {
							if(carClass < DriverNames.cup2020Rarity[carCount]){
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
							} else {
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
							}
						}
						if((carClass == 0)&&(carGears >= unlockGears)){
							GUI.skin = redGUI;
							GUI.skin.button.fontSize = 48 / FontScale.fontScale;
							if (GUI.Button(new Rect(cardX + 10, cardY + (heightblock * 5f), cardW - 20, heightblock * 1f), "Unlock")){
								totalMoney -= GameData.upgradeCost(carClass);
								PlayerPrefs.SetInt(seriesPrefix + carCount + "Unlocked", 1);
								PlayerPrefs.SetInt(seriesPrefix + carCount + "Gears", carGears - unlockGears);
								PlayerPrefs.SetInt(seriesPrefix + carCount + "Class", DriverNames.cup2020Rarity[carCount]);
								carClass = DriverNames.cup2020Rarity[carCount];
								classMax = GameData.classMax(carClass);
							}
							GUI.skin = tileSkin;
						}
						GUI.skin.label.alignment = TextAnchor.LowerLeft;
						GUI.Label(new Rect(cardX + 10, cardY + cardH - (heightblock * 2) - 10, widthblock * 1.5f, heightblock * 2), DriverNames.shortenedType(DriverNames.cup2020Types[carCount]));
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
								PlayerPrefs.SetString("SeriesFocus","cup20");
								Application.LoadLevel("SingleCar");
							}
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
		
		CommonGUI.BackButton("MainMenu");
		
		GUI.skin = buttonSkin;
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
