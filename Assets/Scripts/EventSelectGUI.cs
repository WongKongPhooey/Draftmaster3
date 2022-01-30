using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;

public class EventSelectGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin redGUI;
	public GUISkin whiteGUI;

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;
	
	string minRequirement;
	string levelRequirement;	
	string classRequirement;
	string restrictionValue;
	
	int week;
	string[] eventWeeks;
	
	string seriesMenu;
	int menuIndex;
	int subMenuSize;

	public Vector2 scrollPosition = Vector2.zero;

	void Awake(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		
		week = PlayerPrefs.GetInt("GameWeek");
		
		seriesMenu = "All";
		menuIndex = 0;
	}
	
	void FixedUpdate(){
	}
	
	public static int menuCount(int menu){
		int menuSize = 0;
		for(int i=0;i<10;i++){
			if(EventData.offlineEventChapter[menu,i] != null){
				menuSize = i;
				//Debug.Log("Sub menu larger than  " + i);
			}
		}
		//Debug.Log("This sub menu is size " + menuSize);
		return menuSize;
	}
	
	void OnGUI() {
		
		GUI.skin = eightBitSkin;
		
		GUI.skin.label.fontSize = 144 / FontScale.fontScale;
		GUI.skin.button.fontSize = 128 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect(0, heightblock/2, widthblock * 20, heightblock * 3), "Draftmaster Events");

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		int seriesCount = 0;
		int totalSeries = 7;
		float windowscroll = 2.9f;
		
		GUI.skin = tileSkin;
		
		GUI.skin.horizontalScrollbar.fixedHeight = Screen.height / 10;
		GUI.skin.horizontalScrollbarThumb.fixedHeight = Screen.height / 10;
		scrollPosition = GUI.BeginScrollView(new Rect(0, heightblock * 3, Screen.width, Screen.height - (heightblock * 3)), scrollPosition, new Rect(0, heightblock * 3, Screen.width * windowscroll, Screen.height -(heightblock * 6)));
		
		//Root Level Menu
		if(seriesMenu == "All"){
			for(int rootMenu = 0; rootMenu < 3; rootMenu++){
				string carLivery = "livery" + seriesCount;
				float cardX = widthblock * (rootMenu * 7) + widthblock;
				float cardY = heightblock * 4;
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin = tileSkin;
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 4.5f, heightblock * 2), EventData.offlineEvent[rootMenu]);
				GUI.skin.label.alignment = TextAnchor.UpperRight;
				
				eventWeeks = EventData.offlineEventWeek[rootMenu].Split(',');
				
				if(eventWeeks.Any((week.ToString()).Contains)){
					GUI.Label(new Rect(cardX + (widthblock * 3.75f), cardY + (heightblock * 0.5f), widthblock * 2f, heightblock * 2), "Live!");
				} else {
					//Coming up soon
					if(!eventWeeks.Any(("0").Contains)){
						GUI.Label(new Rect(cardX + (widthblock * 3.75f), cardY + (heightblock * 0.5f), widthblock * 2f, heightblock * 2), "Week " + EventData.offlineEventWeek[rootMenu]);
					}
				}
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2f), widthblock * 5f, heightblock * 5f), Resources.Load(EventData.offlineEventImage[rootMenu]) as Texture, ScaleMode.ScaleToFit);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 8f), widthblock * 5.5f, heightblock * 4), "" + EventData.eventDescriptions[rootMenu] + "");

				//Choose Series
				//Week Cycler
				//week = 4;
				if(eventWeeks.Any((week.ToString()).Contains)){
				//if(week % 13 == EventData.offlineEventWeek[rootMenu]){
					if(GUI.Button(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "")){
						menuIndex = rootMenu;
						seriesMenu = EventData.offlineEvent[menuIndex];
						subMenuSize = menuCount(menuIndex);
						scrollPosition = Vector2.zero;
						windowscroll = 2.5f;
					}
				} else {
					//Grey out the event
					GUI.skin = tileSkin;
					GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
					GUI.skin.label.alignment = TextAnchor.UpperRight;
					if(EventData.offlineEventWeek[rootMenu] == "0"){
						GUI.skin.label.normal.textColor = Color.white;
						GUI.Label(new Rect(cardX + (widthblock * 3.75f), cardY + (heightblock * 0.5f), widthblock * 2f, heightblock * 2), "Coming Soon");
						GUI.skin.label.normal.textColor = Color.black;
					}
					GUI.skin.label.alignment = TextAnchor.UpperLeft;
				}
			}
		} else {
			//If in a sub menu
			for(int subMenu = 0; subMenu <= subMenuSize; subMenu++){
				//string carLivery = "livery" + carCount;
				string carLivery = "livery" + subMenu;
				float cardX = widthblock * (subMenu * 7) + widthblock;
				float cardY = heightblock * 4;
				bool meetsRequirements = true;
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin = tileSkin;
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2), EventData.offlineEventChapter[menuIndex,subMenu]);
				GUI.skin.label.alignment = TextAnchor.UpperRight;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2f), "" + (EventData.offlineAILevel[menuIndex, subMenu] * 10) + "% AI");
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2f), widthblock * 5f, heightblock * 3.5f), Resources.Load(EventData.offlineChapterImage[menuIndex,subMenu]) as Texture, ScaleMode.ScaleToFit);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				
				//Progression Event Checks
				if(EventData.offlineEventType[menuIndex] == "Progression"){
					//If event is already won, lock.
					if(PlayerPrefs.GetInt("BestFinishPosition" + menuIndex + "" + subMenu + "EVENT0") == 1){
						//Reset event for testing
						//PlayerPrefs.SetInt("BestFinishPosition" + menuIndex + "" + subMenu + "EVENT0",0);
						meetsRequirements = false;
					}
					//If previous event isn't won, lock.
					if(subMenu > 0){
						if(PlayerPrefs.GetInt("BestFinishPosition" + menuIndex + "" + (subMenu - 1) + "EVENT0") != 1){
							meetsRequirements = false;
						}
					}
				}
				
				switch(EventData.offlineMinType[menuIndex,subMenu]){
					case "Team":
						minRequirement = "Requires Team " + EventData.offlineMinTeam[menuIndex,subMenu] + "";
						restrictionValue = "" + EventData.offlineMinTeam[menuIndex,subMenu];
					break;
					case "Manufacturer":
						minRequirement = "Requires Manufacturer " + EventData.offlineMinManu[menuIndex,subMenu] + "";
						restrictionValue = "" + EventData.offlineMinManu[menuIndex,subMenu];
					break;
					case "Car":
						minRequirement = "Requires Car #" + EventData.offlineExactCar[menuIndex,subMenu] + "";
						restrictionValue = "" + EventData.offlineExactCar[menuIndex,subMenu];
					break;
					case "Type":
						minRequirement = "Requires Type " + EventData.offlineMinDriverType[menuIndex,subMenu] + "";
						restrictionValue = "" + EventData.offlineMinDriverType[menuIndex,subMenu];
					break;
					case "Rarity":
						minRequirement = "Min Rarity " + EventData.offlineMinRarity[menuIndex,subMenu] + "";
						restrictionValue = "" + EventData.offlineMinRarity[menuIndex,subMenu];
					break;
					default:
					break;
				}
				
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6f), widthblock * 5.5f, heightblock * 6f), minRequirement + "\n" + "Min Class " + SeriesData.classAbbr(EventData.offlineMinClass[menuIndex,subMenu]) + "\n\n" + EventData.eventChapterDescriptions[menuIndex,subMenu] + "");
				
				//Choose Series
				if(meetsRequirements == true){
					if(GUI.Button(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "")){
						PlayerPrefs.SetString("SeriesTrackList",EventData.offlineTracklists[menuIndex,subMenu]);
						PlayerPrefs.SetString("CurrentSeriesIndex", menuIndex + "" + subMenu + "EVENT");
						PlayerPrefs.SetString("CurrentSeriesName",EventData.offlineEventChapter[menuIndex,subMenu]);
						PlayerPrefs.SetInt("CurrentSeries", menuIndex);
						PlayerPrefs.SetInt("CurrentSubseries", subMenu);
						PlayerPrefs.SetInt("SubseriesDailyPlays",999);
						PlayerPrefs.SetInt("SubseriesMinClass", EventData.offlineMinClass[menuIndex,subMenu]);
						PlayerPrefs.SetString("RestrictionType",EventData.offlineMinType[menuIndex,subMenu]);
						PlayerPrefs.SetString("RestrictionValue",restrictionValue);
						PlayerPrefs.SetInt("AIDifficulty", EventData.offlineAILevel[menuIndex,subMenu]);
						PlayerPrefs.SetInt("SeriesFuel",5);
						PlayerPrefs.SetString("SeriesPrize",EventData.offlinePrizes[menuIndex,subMenu]);
						PlayerPrefs.SetString("SeriesPrizeAmt",EventData.offlineSetPrizes[menuIndex,subMenu]);
						PlayerPrefs.SetString("RaceType","Event");
						
						if(EventData.offlineSeries[menuIndex,subMenu] != null){
							PlayerPrefs.SetString("FixedSeries", EventData.offlineSeries[menuIndex,subMenu]);
						}
						if(EventData.offlineCustomField[menuIndex,subMenu] != null){
							PlayerPrefs.SetString("CustomField", EventData.offlineCustomField[menuIndex,subMenu]);
						}
						
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
