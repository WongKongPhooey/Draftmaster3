using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class DriverEditorGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	
	float widthblock; 
	float heightblock;

	string driverSelect;

	string lastChange;

	public Vector2 scrollPosition = Vector2.zero;
	public float hSliderValue = 0.0F;

	int seriesNum;
	string[] seriesChoice = new string[] {"Stock Cars","Indycars","Trucks"};
	string[] seriesKey = new string[] {"StockCar","Indycar","Truck"};

	string[][] allDriverNames = new string[3][];

	int driverNum;

	public string cheatCode = "";

	void Awake(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;

		allDriverNames[0] = DriverNames.cup2020Names;

		seriesNum = 0;
		driverSelect = "";

		lastChange = "";

		driverNum = 0;
		driverSelect = allDriverNames[seriesNum][driverNum];
	}
	
	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.textField.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock-10, Screen.height * 1.0f));

		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		
		GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "Driver Editor");

		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		if (GUI.Button(new Rect(widthblock * 16.5f, heightblock * 0.5f, widthblock * 1.5f, heightblock * 1.5f), "Back")){
			SceneManager.LoadScene("MainMenu");
		}
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 4, widthblock * 14, heightblock * 2), "Series: " + seriesChoice[seriesNum]);

		if (GUI.Button(new Rect(widthblock * 16, heightblock * 4, widthblock * 1, heightblock * 2), "<")){
			driverNum = 0;
			if(seriesNum <= 0){
				seriesNum = 2;
			} else {
				seriesNum--;
			}
			driverSelect = allDriverNames[seriesNum][driverNum];
		}
		if (GUI.Button(new Rect(widthblock * 17, heightblock * 4, widthblock * 1, heightblock * 2), ">")){
			driverNum = 0;
			if(seriesNum >= 2){
				seriesNum = 0;
			} else {
				seriesNum++;
			}
			driverSelect = allDriverNames[seriesNum][driverNum];
		}

		GUI.Label(new Rect(widthblock * 2, heightblock * 8, widthblock * 5, heightblock * 2), "Driver: #" + driverNum);

		driverSelect = GUI.TextField(new Rect(widthblock * 7, heightblock * 8, widthblock * 7, heightblock * 2),driverSelect, 12);

		if (GUI.Button(new Rect(widthblock * 16, heightblock * 8, widthblock * 1, heightblock * 2), "<")){
			if(driverNum <=0){
				driverNum = 100;
			}
			while(allDriverNames[seriesNum][driverNum - 1] == null){
				driverNum--;
			}
			driverNum--;
			driverSelect = allDriverNames[seriesNum][driverNum];

		}
		if (GUI.Button(new Rect(widthblock * 17, heightblock * 8, widthblock * 1, heightblock * 2), ">")){
			if(driverNum >=99){
				driverNum = -1;
			}
			while(allDriverNames[seriesNum][driverNum + 1] == null){
				driverNum++;
			}
			driverNum++;
			driverSelect = allDriverNames[seriesNum][driverNum];
		}

		if (GUI.Button(new Rect(widthblock * 7, heightblock * 11, widthblock * 3, heightblock * 2), "Save")){
			if(driverSelect != ""){
				lastChange = "#" + driverNum + " " + allDriverNames[seriesNum][driverNum] + " -> " + driverSelect;
				PlayerPrefs.SetString(seriesKey[seriesNum] + "Driver" + driverNum,driverSelect);
				//DriverNames.reloadNames();
				driverSelect = allDriverNames[seriesNum][driverNum];
			}
		}

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 11, widthblock * 3, heightblock * 2), "Reset")){
			string prevName = allDriverNames[seriesNum][driverNum];
			PlayerPrefs.DeleteKey(seriesKey[seriesNum] + "Driver" + driverNum);
			//DriverNames.reloadNames();
			driverSelect = allDriverNames[seriesNum][driverNum];
			lastChange = "#" + driverNum + " " + prevName + " -> " + allDriverNames[seriesNum][driverNum];
		}

		GUI.Label(new Rect(widthblock * 2, heightblock * 15, widthblock * 16, heightblock * 2), lastChange);

		GUI.EndScrollView();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
