using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class SeriesSelectGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin whiteGUI;
	public GUISkin redGUI;

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;
	
	string seriesMenu;
	int menuIndex;
	int subMenuSize;
	string minRequirement;
	string levelRequirement;	
	string classRequirement;
	string restrictionValue;
	int minClass;
	int level;

	public Vector2 scrollPosition = Vector2.zero;

	void Awake(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		
		level = PlayerPrefs.GetInt("Level");
		
		PlayerPrefs.SetString("SeriesPrize","");
		
		seriesMenu = "All";
		menuIndex = 0;
	}
	
	void FixedUpdate(){
	}
	
	public static int menuCount(int menu){
		int menuSize = 0;
		for(int i=0;i<10;i++){
			if(SeriesData.offlineSeries[menu,i] != null){
				menuSize = i;
			}
		}
		return menuSize;
	}
	
	void OnGUI() {
		
		GUI.skin = eightBitSkin;
		
		GUI.skin.label.fontSize = 144 / FontScale.fontScale;
		GUI.skin.button.fontSize = 128 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect(0, heightblock/2, widthblock * 20, heightblock * 3), "Choose a series");

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		int seriesCount = 0;
		int totalSeries = 7;
		float windowscroll = 3.55f;
		
		GUI.skin = tileSkin;
		
		GUI.skin.horizontalScrollbar.fixedHeight = Screen.height / 10;
		GUI.skin.horizontalScrollbarThumb.fixedHeight = Screen.height / 10;
		scrollPosition = GUI.BeginScrollView(new Rect(0, heightblock * 3, Screen.width, Screen.height - (heightblock * 3)), scrollPosition, new Rect(0, heightblock * 3, Screen.width * windowscroll, Screen.height -(heightblock * 6)));
		
		//Root Level Menu
		if(seriesMenu == "All"){
			for(int rootMenu = 0; rootMenu <= 9; rootMenu++){
				string carLivery = "livery" + seriesCount;
				float cardX = widthblock * (rootMenu * 7) + widthblock;
				float cardY = heightblock * 4;
				GUI.skin = whiteGUI;
				
				if(PlayerPrefs.HasKey("ChampionshipSubseries")){
					if(PlayerPrefs.GetString("ChampionshipSubseries")[0].ToString() == rootMenu + ""){
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.label.normal.textColor = Color.red;
						GUI.skin.label.alignment = TextAnchor.LowerCenter;
						GUI.Label(new Rect(cardX, cardY - (heightblock * 1f), widthblock * 6f, heightblock * 1), "Active Championship");
					}
				}
				
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin = tileSkin;
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2), SeriesData.offlineMenu[rootMenu]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2f), widthblock * 5f, widthblock * 2.5f), Resources.Load(SeriesData.offlineImage[rootMenu]) as Texture);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.MiddleLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6f), widthblock * 5.5f, heightblock * 6), "" + SeriesData.seriesDescriptions[rootMenu] + "");

				//Choose Series
				if(GUI.Button(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "")){
					menuIndex = rootMenu;
					seriesMenu = SeriesData.offlineMenu[menuIndex];
					subMenuSize = menuCount(menuIndex);
					scrollPosition = Vector2.zero;
					Debug.Log("We are now in menu #" + menuIndex);
				}
			}
		} else {
			//If in a sub menu
			for(int subMenu = 0; subMenu <= subMenuSize; subMenu++){
				//string carLivery = "livery" + carCount;
				string carLivery = SeriesData.offlineSeriesImage[menuIndex,subMenu];
				float cardX = widthblock * (subMenu * 7) + widthblock;
				float cardY = heightblock * 4;
				bool meetsRequirements = true;
				GUI.skin = whiteGUI;
				
				if(PlayerPrefs.HasKey("ChampionshipSubseries")){
					if(PlayerPrefs.GetString("ChampionshipSubseries") == menuIndex + "" + subMenu){
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.label.normal.textColor = Color.red;
						GUI.skin.label.alignment = TextAnchor.LowerCenter;
						GUI.Label(new Rect(cardX, cardY - (heightblock * 1f), widthblock * 6f, heightblock * 1), "Active Championship");
					}
				}
				
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin = tileSkin;
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2), SeriesData.offlineSeries[menuIndex,subMenu]);
				GUI.skin.label.alignment = TextAnchor.UpperRight;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2f), "" + (SeriesData.offlineAILevel[menuIndex, subMenu] * 10) + "% AI");
				GUI.DrawTexture(new Rect(cardX + (widthblock * 3) - (heightblock * 3.5f), cardY + (heightblock * 2f), heightblock * 7f, heightblock * 3.5f), Resources.Load(carLivery) as Texture);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.MiddleLeft;
				
				//Level Check
				levelRequirement = "Min Level " + SeriesData.offlineMinLevel[menuIndex,subMenu] + "\n";
				restrictionValue = "" + SeriesData.offlineMinLevel[menuIndex,subMenu];
				if(level < SeriesData.offlineMinLevel[menuIndex,subMenu]){
					meetsRequirements = false;
				}
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 7f), widthblock * 2.75f, heightblock * 1.5f), "Min Level " + (SeriesData.offlineMinLevel[menuIndex, subMenu]) + "");
				
				//Class Check
				GUI.skin.label.alignment = TextAnchor.MiddleRight;
				classRequirement = "Min Class " + SeriesData.offlineMinClass[menuIndex,subMenu] + "\n";
				minClass = SeriesData.offlineMinClass[menuIndex,subMenu];
				GUI.Label(new Rect(cardX + (widthblock * 3f), cardY + (heightblock * 7f), widthblock * 2.75f, heightblock * 1.5f), "Min Class " + classAbbr(SeriesData.offlineMinClass[menuIndex, subMenu]) + "");
				GUI.skin.label.normal.textColor = classColours(SeriesData.offlineMinClass[menuIndex, subMenu]);
				GUI.Label(new Rect(cardX + (widthblock * 3f), cardY + (heightblock * 7f), widthblock * 2.75f, heightblock * 1.5f), "" + classAbbr(SeriesData.offlineMinClass[menuIndex, subMenu]) + "");
				GUI.skin.label.normal.textColor = Color.black;
				
				GUI.skin.label.alignment = TextAnchor.MiddleLeft;
				switch(SeriesData.offlineMinType[menuIndex,subMenu]){
					case "Team":
						minRequirement = "Requires Team " + SeriesData.offlineMinTeam[menuIndex,subMenu] + "";
						restrictionValue = "" + SeriesData.offlineMinTeam[menuIndex,subMenu];
					break;
					case "Manufacturer":
						minRequirement = "Requires Manufacturer " + SeriesData.offlineMinManu[menuIndex,subMenu] + "";
						restrictionValue = "" + SeriesData.offlineMinManu[menuIndex,subMenu];
					break;
					case "Car":
						minRequirement = "Requires Car #" + SeriesData.offlineExactCar[menuIndex,subMenu] + "";
						restrictionValue = "" + SeriesData.offlineExactCar[menuIndex,subMenu];
					break;
					case "Type":
						minRequirement = "Requires Type " + SeriesData.offlineMinDriverType[menuIndex,subMenu] + "";
						restrictionValue = "" + SeriesData.offlineMinDriverType[menuIndex,subMenu];
					break;
					case "Rarity":
						minRequirement = "Min Rarity " + SeriesData.offlineMinRarity[menuIndex,subMenu] + "";
						restrictionValue = "" + SeriesData.offlineMinRarity[menuIndex,subMenu];
					break;
					default:
					break;
				}
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6.5f), widthblock * 5.5f, heightblock * 4.5f), "" + minRequirement + "");
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 8f), widthblock * 5.5f, heightblock * 4.5f), "" + SeriesData.offlineDescriptions[menuIndex, subMenu] + "");
				
				//Choose Subseries
				if(meetsRequirements == true){
					//if(PlayerPrefs.GetInt("DailyPlays" + menuIndex + subMenu + "") > 0){
						if(GUI.Button(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "")){
							PlayerPrefs.SetString("carTexture", carLivery);
							PlayerPrefs.SetString("SeriesTrackList",SeriesData.offlineTracklists[menuIndex,subMenu]);
							PlayerPrefs.SetString("CurrentSeriesIndex", menuIndex + "" + subMenu);
							PlayerPrefs.SetString("CurrentSeriesName",SeriesData.offlineSeries[menuIndex,subMenu]);
							PlayerPrefs.SetInt("CurrentSeries", menuIndex);
							PlayerPrefs.SetInt("CurrentSubseries", subMenu);
							PlayerPrefs.SetInt("SubseriesDailyPlays",SeriesData.offlineDailyPlays[menuIndex,subMenu]);
							PlayerPrefs.SetInt("SubseriesMinClass", minClass);
							PlayerPrefs.SetString("RestrictionType",SeriesData.offlineMinType[menuIndex,subMenu]);
							PlayerPrefs.SetString("RestrictionValue",restrictionValue);
							PlayerPrefs.SetInt("AIDifficulty", SeriesData.offlineAILevel[menuIndex,subMenu]);
							PlayerPrefs.SetInt("SeriesFuel",SeriesData.offlineFuel[menuIndex,subMenu]);
							Debug.Log("Fuel cost: " + SeriesData.offlineFuel[menuIndex,subMenu]);
							PlayerPrefs.SetString("SeriesPrize",SeriesData.offlinePrizes[menuIndex,subMenu]);
							
							if(PlayerPrefs.HasKey("ChampionshipSubseries")){
								if(PlayerPrefs.GetString("ChampionshipSubseries") == menuIndex + "" + subMenu){
									PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("ChampionshipCarTexture"));
									PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("ChampionshipCarChoice"));
									PlayerPrefs.SetInt("CarSeries", PlayerPrefs.GetInt("ChampionshipCarSeries"));
									SceneManager.LoadScene("CircuitSelect");
								} else {
									SceneManager.LoadScene("CarSelect");
								}
							} else {
								SceneManager.LoadScene("CarSelect");
							}
						}
				} else {
					//Grey out the event
					GUI.skin = tileSkin;
					GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				}
			}
		}
		
		GUI.EndScrollView();
		
		GUI.skin = buttonSkin;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleLeft;

		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		if(seriesMenu == "All"){
			CommonGUI.BackButton("MainMenu");
		} else {		
			if (GUI.Button(new Rect(widthblock * 0.5f, 20, widthblock * 2f, heightblock * 1.5f), "Back")){
				seriesMenu = "All";
			}
		}
		
		GUI.skin = buttonSkin;
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.FuelUI();
		
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