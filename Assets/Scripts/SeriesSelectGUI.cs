using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class SeriesSelectGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GUISkin buttonSkin;
	public GUISkin tileSkin;

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;
	
	string seriesMenu;
	int menuIndex;
	int subMenuSize;
	string minRequirement;
	string restrictionValue;
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
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
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
				bool meetsRequirements = false;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2), SeriesData.offlineSeries[menuIndex,subMenu]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2f), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery) as Texture);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.MiddleLeft;
				
				switch(SeriesData.offlineMinType[menuIndex,subMenu]){
					case "Level":
						minRequirement = "Min Level " + SeriesData.offlineMinLevel[menuIndex,subMenu] + "\n";
						restrictionValue = "" + SeriesData.offlineMinLevel[menuIndex,subMenu];
						if(level >= SeriesData.offlineMinLevel[menuIndex,subMenu]){
							meetsRequirements = true;
						}
					break;
					case "Class":
						minRequirement = "Min Class " + SeriesData.classAbbr(SeriesData.offlineMinClass[menuIndex,subMenu]) + "\n";
						restrictionValue = "" + SeriesData.offlineMinClass[menuIndex,subMenu];
						meetsRequirements = true;
					break;
					case "Team":
						minRequirement = "Requires Team " + SeriesData.offlineMinTeam[menuIndex,subMenu] + "\n";
						restrictionValue = "" + SeriesData.offlineMinTeam[menuIndex,subMenu];
						meetsRequirements = true;
					break;
					case "Manufacturer":
						minRequirement = "Requires Manufacturer " + SeriesData.offlineMinManu[menuIndex,subMenu] + "\n";
						restrictionValue = "" + SeriesData.offlineMinManu[menuIndex,subMenu];
						meetsRequirements = true;
					break;
					case "Rarity":
						minRequirement = "Min Rarity " + SeriesData.offlineMinRarity[menuIndex,subMenu] + "\n";
						restrictionValue = "" + SeriesData.offlineMinRarity[menuIndex,subMenu];
						meetsRequirements = true;
					break;
					default:
					break;
				}
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6f), widthblock * 5.5f, heightblock * 6), minRequirement + SeriesData.offlineDescriptions[menuIndex, subMenu] + "");
				
				
				//Choose Subseries
				if(meetsRequirements == true){
					if(GUI.Button(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "")){
						PlayerPrefs.SetString("carTexture", carLivery);
						PlayerPrefs.SetString("SeriesTrackList",SeriesData.offlineTracklists[menuIndex,subMenu]);
						PlayerPrefs.SetString("CurrentSubseries", menuIndex + "" + subMenu);
						PlayerPrefs.SetString("CurrentSeries",SeriesData.offlineSeries[menuIndex,subMenu]);
						PlayerPrefs.SetInt("SubseriesDailyPlays",SeriesData.offlineDailyPlays[menuIndex,subMenu]);
						PlayerPrefs.SetString("RestrictionType",SeriesData.offlineMinType[menuIndex,subMenu]);
						PlayerPrefs.SetString("RestrictionValue",restrictionValue);
						PlayerPrefs.SetInt("SeriesFuel",SeriesData.offlineFuel[menuIndex,subMenu]);
						PlayerPrefs.SetString("SeriesPrize",SeriesData.offlinePrizes[menuIndex,subMenu]);
						SceneManager.LoadScene("CarSelect");
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

		CommonGUI.BackButton("MainMenu");
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}