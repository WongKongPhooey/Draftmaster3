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
	string seriesPrefix;
	float windowscroll = 2.5f;
	
	public string carMenu;
	
	bool numberPanel;
	
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

		carMenu = "Description";

		numberPanel = false;

		if(PlayerPrefs.HasKey("SeriesFocus")){
			seriesPrefix = PlayerPrefs.GetString("SeriesFocus");
		} else {
			seriesPrefix = "cup20";
		}
		
		if(PlayerPrefs.HasKey("CarFocus")){
			currentCar = PlayerPrefs.GetInt("CarFocus");
		} else {
			currentCar = 1;
		}
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
		
		classCriteria[1] = "1 win at Bristol";
		classCriteria[2] = "1 win at New Hampshire";
		classCriteria[3] = "1 win at Michigan";
		classCriteria[4] = "1 win at California";
		classCriteria[5] = "1 win at Miami";
		classCriteria[6] = "1 win at Ridgeway";
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
		
		GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 1.5f, heightblock * 1f), DriverNames.cup2020Teams[currentCar]);
		Texture2D manufacturerTex = null;
		switch(DriverNames.cup2020Manufacturer[currentCar]){
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
		GUI.DrawTexture(new Rect(cardX + (widthblock * 0.5f), cardY + (heightblock * 2), widthblock * 5f, widthblock * 2.5f), Resources.Load(carLivery) as Texture);
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
		GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + cardH - (heightblock * 2) - 10, widthblock * 2.5f, heightblock * 2), "Intim.");
		GUI.skin.label.alignment = TextAnchor.UpperRight;
		GUI.skin.label.alignment = TextAnchor.LowerRight;
		GUI.Label(new Rect(cardX + (widthblock * 3.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 2.5f) - (widthblock * 0.25f), heightblock * 2), "Class " + carClass);
		GUI.skin.label.normal.textColor = Color.red;
		GUI.Label(new Rect(cardX + (widthblock * 3.5f), cardY + cardH - (heightblock * 2) - 10, (widthblock * 2.5f) - (widthblock * 0.25f), heightblock * 2), "" + carClass);
		GUI.skin.label.normal.textColor = Color.black;
		
		//Right Side Panel
		
		GUI.skin = buttonSkin;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if (GUI.Button(new Rect(widthblock * 7.5f, heightblock * 3.5f, widthblock * 3.5f, heightblock * 1.5f), "Description")){
			carMenu = "Description";
		}
		
		if (GUI.Button(new Rect(widthblock * 11.5f, heightblock * 3.5f, widthblock * 2.5f, heightblock * 1.5f), "Paints")){
			carMenu = "Paints";
		}
		
		if (GUI.Button(new Rect(widthblock * 14.5f, heightblock * 3.5f, widthblock * 2.25f, heightblock * 1.5f), "Links")){
			carMenu = "Links";
		}
		
		if (GUI.Button(new Rect(widthblock * 17.25f, heightblock * 3.5f, widthblock * 2.25f, heightblock * 1.5f), "Stats")){
			carMenu = "Stats";
		}
		
		
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		
		//MENUS
		switch(carMenu){
			case "Description":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 5.5f, widthblock * 11.5f, heightblock * 7), "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Fusce molestie nisl et urna hendrerit, a pellentesque eros pulvinar. Etiam posuere in ex quis auctor. Nunc accumsan, orci eu pharetra fermentum, dui diam elementum nisl, quis tincidunt lorem sem quis magna. Aliquam non vehicula dui. In quis tellus bibendum, viverra ligula gravida, blandit odio.");			
				break;
			case "Paints":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 5.5f, widthblock * 11.5f, heightblock * 7), "Alternate Paints Coming Soon");
				break;
			case "Links":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 5.5f, widthblock * 11.5f, heightblock * 2), "Can Draft With PEN Teammates:");
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 7.5f, widthblock * 11.5f, heightblock * 2), "FRD Cars Will Push You:");
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 9.5f, widthblock * 11.5f, heightblock * 2), "Strategists Change Lane 5% Faster Per Class (15%)");
				
				if (GUI.Button(new Rect(widthblock * 7.5f, heightblock * 15f, widthblock * 4.5f, heightblock * 1.5f), "Change Number")){
					numberPanel = true;
					//PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentCar, 1);
					Debug.Log("Car #" + currentCar + " now uses #1. Var: " + "CustomNumber" + seriesPrefix + currentCar);
				}
				if (GUI.Button(new Rect(widthblock * 15.5f, heightblock * 15f, widthblock * 4f, heightblock * 1.5f), "Change Team")){
				}
				if (GUI.Button(new Rect(widthblock * 7.5f, heightblock * 17f, widthblock * 6f, heightblock * 1.5f), "Change Manufacturer")){
				}
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + currentCar)){
				GUI.skin = redGUI;
				if (GUI.Button(new Rect(widthblock * 15f, heightblock * 17f, widthblock * 4.5f, heightblock * 1.5f), "Reset Changes")){
					PlayerPrefs.DeleteKey("CustomNumber" + seriesPrefix + currentCar);
				}
				}
				GUI.skin = buttonSkin;
				break;
			case "Stats":
				GUI.Label(new Rect(widthblock * 7.5f, heightblock * 5.5f, widthblock * 11.5f, heightblock * 7), "Starts");
				break;
		}
		
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin = buttonSkin;
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();
		
		CommonGUI.BackButton("AllCars");
		
		if(numberPanel == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperLeft;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 3.5f, heightblock * 4f, widthblock * 11f, heightblock * 2f), "Change Number");
			
			for(int i=0;i<8;i++){
				if (GUI.Button(new Rect(widthblock * 4f + (widthblock * i * 1.5f), heightblock * 7f, widthblock * 1f, widthblock * 1f), Resources.Load(seriesPrefix + "num" + i) as Texture)){
					PlayerPrefs.SetInt("CustomNumber" + seriesPrefix + currentCar, i);
					numberPanel = false;
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
}
