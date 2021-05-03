using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class EventSelectGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GUISkin buttonSkin;
	public GUISkin tileSkin;

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;
	
	int week;
	
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
		float windowscroll = 2.2f;
		
		GUI.skin = tileSkin;
		
		GUI.skin.horizontalScrollbar.fixedHeight = Screen.height / 10;
		GUI.skin.horizontalScrollbarThumb.fixedHeight = Screen.height / 10;
		scrollPosition = GUI.BeginScrollView(new Rect(0, heightblock * 3, Screen.width, Screen.height - (heightblock * 3)), scrollPosition, new Rect(0, heightblock * 3, Screen.width * windowscroll, Screen.height -(heightblock * 6)));
		
		//Root Level Menu
		if(seriesMenu == "All"){
			for(int rootMenu = 0; rootMenu < 4; rootMenu++){
				string carLivery = "livery" + seriesCount;
				float cardX = widthblock * (rootMenu * 7) + widthblock;
				float cardY = heightblock * 4;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 4.5f, heightblock * 2), EventData.offlineEvent[rootMenu]);
				GUI.skin.label.alignment = TextAnchor.UpperRight;
				if((EventData.offlineEventWeek[rootMenu] - week) == 0){
					GUI.Label(new Rect(cardX + (widthblock * 3.75f), cardY + (heightblock * 0.5f), widthblock * 2f, heightblock * 2), "Live!");
				} else {
					//Coming up soon
					GUI.Label(new Rect(cardX + (widthblock * 3.75f), cardY + (heightblock * 0.5f), widthblock * 2f, heightblock * 2), "Week " + EventData.offlineEventWeek[rootMenu]);
				}
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2f), widthblock * 5f, widthblock * 2.5f), Resources.Load(EventData.offlineEventImage[rootMenu]) as Texture);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 8f), widthblock * 5.5f, heightblock * 4), "" + EventData.eventDescriptions[rootMenu] + "");

				//Choose Series
				//Week Cycler
				if(week % 13 == EventData.offlineEventWeek[rootMenu]){
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
				}
			}
		} else {
			//If in a sub menu
			for(int subMenu = 0; subMenu <= subMenuSize; subMenu++){
				//string carLivery = "livery" + carCount;
				string carLivery = "livery" + subMenu;
				float cardX = widthblock * (subMenu * 7) + widthblock;
				float cardY = heightblock * 4;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "");
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 0.5f), widthblock * 5.5f, heightblock * 2), EventData.offlineEventChapter[menuIndex,subMenu]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2f), widthblock * 5f, widthblock * 2.5f), Resources.Load(EventData.offlineChapterImage[menuIndex,subMenu]) as Texture);
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 7f), widthblock * 5.5f, heightblock * 5), "Min Class " + SeriesData.classAbbr(EventData.offlineMinClass[menuIndex,subMenu]) + "\n\n" + EventData.eventChapterDescriptions[menuIndex,subMenu] + "");
				
				//Choose Series
				if(GUI.Button(new Rect(cardX, cardY, widthblock * 6, heightblock * 12), "")){
					PlayerPrefs.SetString("carTexture", carLivery);
					PlayerPrefs.SetString("SeriesTrackList",SeriesData.offlineTracklists[menuIndex,subMenu]);
					//PlayerPrefs.SetInt("MinClass",SeriesData.offlineMinClass[subMenu]);
					PlayerPrefs.SetString("CurrentSeries",EventData.offlineEventChapter[menuIndex,subMenu]);
					SceneManager.LoadScene("CarSelect");
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
