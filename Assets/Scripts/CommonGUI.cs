using System.Collections;
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
	
	public Texture2D fbTex;
	public Texture2D twitterTex;
	public Texture2D redditTex;
	
	public static Texture2D moneyTexInst;
	public static Texture2D gasCanTexInst;
	public static Texture2D gearTexInst;
	
	public static Texture2D fbTexInst;
	public static Texture2D twitterTexInst;
	public static Texture2D redditTexInst;

	
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
		
		fbTexInst = fbTex;
		twitterTexInst = twitterTex;
		redditTexInst = redditTex;
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
