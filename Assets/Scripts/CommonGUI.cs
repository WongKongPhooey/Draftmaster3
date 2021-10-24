﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommonGUI : MonoBehaviour {
	
	public static float widthblock; 
	public static float heightblock;
	
	public static int day;
	public static int week;
	public static int level;
	public static int exp;
	public static int totalMoney;
	public static int totalGears;
	
	public Texture2D moneyTex;
	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	public static Texture2D moneyTexInst;
	public static Texture2D gasCanTexInst;
	public static Texture2D gearTexInst;

	
    // Start is called before the first frame update
    void Awake(){
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		
		exp = PlayerPrefs.GetInt("Exp");
		level = PlayerPrefs.GetInt("Level");
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		totalGears = PlayerPrefs.GetInt("Gears");
		
		moneyTexInst = moneyTex;
		gasCanTexInst = gasCanTex;
		gearTexInst = gearTex;
    }

    // Update is called once per frame
    void Update(){
    }
	
	public static void BackButton(string backTo){
		
		if(backTo == ""){
			backTo = "MainMenu";
		}
		
		if (GUI.Button(new Rect(widthblock * 0.5f, 20, widthblock * 2f, heightblock * 1.5f), "Back")){
			SceneManager.LoadScene(backTo);
		}
	}
	
	public static void FuelUI(){
		//Fuel
		if (GUI.Button(new Rect(widthblock * 17f, 20, widthblock * 2.5f, heightblock * 1.5f), GameData.gameFuel + "/" + GameData.maxFuel)){
		}
		GUI.DrawTexture(new Rect((widthblock * 17f) + 10, 27, (heightblock * 1.5f) - 20, (heightblock * 1.5f) - 20), gasCanTexInst);
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
	}
	
	public static void TopBar(){
		
		day = PlayerPrefs.GetInt("GameDay");
		week = PlayerPrefs.GetInt("GameWeek");
		level = PlayerPrefs.GetInt("Level");
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		totalGears = PlayerPrefs.GetInt("Gears");
		
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
        Scene currentScene = SceneManager.GetActiveScene ();
        string sceneName = currentScene.name;
 
        if (sceneName == "MainMenu"){		
			//Level
			if (GUI.Button(new Rect(widthblock * 0.5f, 20, widthblock * 2f, heightblock * 1.5f), "Lvl " + level)){
				if (sceneName == "MainMenu"){
					MainMenuGUI.showInfoBox = true;
					MainMenuGUI.infoBox = "This is your player level. A higher player level unlocks series with faster opponents, and higher gear rewards. Level ups contain prizes!";
				}
			}
			
			//Day/Week
			if (GUI.Button(new Rect(widthblock * 3f, 20, widthblock * 3f, heightblock * 1.5f), "W" + week + " / D" + day)){
				if (sceneName == "MainMenu"){
					MainMenuGUI.showInfoBox = true;
					MainMenuGUI.infoBox = "This is the Week and Day of the 13 week game cycle. The blue bar underneath shows how far through the current day you are, before daily events reset.";
				}
			}
		}
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		//Coins
		if (GUI.Button(new Rect(widthblock * 10.5f, 20, widthblock * 3f, heightblock * 1.5f), "" + totalMoney)){
			if (sceneName == "MainMenu"){
				MainMenuGUI.showInfoBox = true;
				MainMenuGUI.infoBox = "Money is used for upgrading a car's class in the Garage. Any spare money can be spent on Gears in the Store Junkyard, but the exchange rate is poor.";
			}
		}
		GUI.DrawTexture(new Rect((widthblock * 10.5f) + 10, 27, (heightblock * 1.5f) - 20, (heightblock * 1.5f) - 20), moneyTexInst);
		
		//Gears
		if (GUI.Button(new Rect(widthblock * 14f, 20, widthblock * 2.5f, heightblock * 1.5f), "" + totalGears)){
			if (sceneName == "MainMenu"){
				MainMenuGUI.showInfoBox = true;
				MainMenuGUI.infoBox = "Gears are used to purchase cars in the Store. Gears can be won in races by placing in the Top 3, or bought in the Premium Store.";
			}
		}
		GUI.DrawTexture(new Rect((widthblock * 14f) + 10, 27, (heightblock * 1.5f) - 20, (heightblock * 1.5f) - 20), gearTexInst);
		
		//Fuel
		if (GUI.Button(new Rect(widthblock * 17f, 20, widthblock * 2.5f, heightblock * 1.5f), GameData.gameFuel + "/" + GameData.maxFuel)){
			if (sceneName == "MainMenu"){
				MainMenuGUI.showInfoBox = true;
				MainMenuGUI.infoBox = "Fuel is used whenever you race. Higher level races use more fuel. Fuel can be regained in the Store by watching ads, or by simply waiting some time.";
			}
		}
		GUI.DrawTexture(new Rect((widthblock * 17f) + 10, 27, (heightblock * 1.5f) - 20, (heightblock * 1.5f) - 20), gasCanTexInst);
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
	}
	
	public static void ExamplePopup(){
		GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
		//GUI.skin = whiteGUI;
		GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
		
		//GUI.skin = buttonSkin;
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 3.5f, heightblock * 4f, widthblock * 11f, heightblock * 2f), "Change Number");
		
		//GUI.skin = redGUI;
		if (GUI.Button(new Rect(widthblock * 14f, heightblock * 3.5f, widthblock * 2.5f, heightblock * 1.5f), "Back")){
		}
	}	
}
