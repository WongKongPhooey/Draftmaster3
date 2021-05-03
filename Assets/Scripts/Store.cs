using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class Store : MonoBehaviour{
	
	public GUISkin buttonSkin;
	public GUISkin tileSkin;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	string menuCat;
	
	public static string[] goodItems = new string[20];
	public static string[] greatItems = new string[20];
	public static string[] excellentItems = new string[20];
	
	public Texture BoxTexture;

	int playerLevel;
	int totalMoney;
	int carMoney;
	int moneyCount;
	int premiumTokens;
	int gears;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;

	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		menuCat = "DailySelects";
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
	}
	
    // Start is called before the first frame update
    void Start(){
    }

    // Update is called once per frame
    void Update(){
    }
	
	void OnGUI(){
		
		GUI.skin = buttonSkin;
		
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.skin.button.fontSize = 96 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.LowerLeft;
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 5, heightblock * 1.5f, widthblock * 5, heightblock * 2), "Store");
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;

		int itemCount = 0;
		string seriesPrefix = "20";
		int totalItems = 4;
		float windowscroll = 1.5f;
		
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		if (GUI.Button(new Rect(widthblock / 2, heightblock * 4, widthblock * 4, heightblock * 1.5f), "Bundles")){
			menuCat = "Bundles";
		}
		
		if (GUI.Button(new Rect(widthblock / 2, heightblock * 6, widthblock * 4, heightblock * 1.5f), "Daily Selects")){
			menuCat = "DailySelects";
		}
		
		if (GUI.Button(new Rect(widthblock / 2, heightblock * 8, widthblock * 4, heightblock * 1.5f), "Fuel")){
			menuCat = "Fuel";
		}
		
		if (GUI.Button(new Rect(widthblock / 2, heightblock * 10, widthblock * 4, heightblock * 1.5f), "Gears")){
			menuCat = "Gears";
		}
		
		if (GUI.Button(new Rect(widthblock / 2, heightblock * 12, widthblock * 4, heightblock * 1.5f), "Legends")){
			menuCat = "Legends";
		}
		
		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		scrollPosition = GUI.BeginScrollView(new Rect(widthblock * 4, heightblock * 4, Screen.width - (widthblock * 4), Screen.height - (heightblock * 4)), scrollPosition, new Rect(widthblock * 4, heightblock * 4, Screen.width - (widthblock * 5.5f), Screen.height * windowscroll));

		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		GUI.skin = tileSkin;
		
		if(menuCat == "Bundles"){
			
			//Free Car Parts
			float cardX = widthblock * 5f;
			float cardY = heightblock * 4;
			GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "Daily Garage Auction!");
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;

			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "Free")){
				gears = PlayerPrefs.GetInt("Gears");
				PlayerPrefs.SetString("PrizeType","FreeDaily");
				Application.LoadLevel("PrizeCollection");
			}
			
			//Premium Bags
			cardX = widthblock * 12f;
			cardY = heightblock * 4;
			GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "Premium Mega Bag!");
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "10G")){
				gears = PlayerPrefs.GetInt("Gears");
				if(gears >= 10){
					gears -= 10;
					PlayerPrefs.SetInt("Gears",gears);
					PlayerPrefs.SetString("PrizeType","PremiumBag");
					Application.LoadLevel("PrizeCollection");
				}
			}
			
			//Buy Coins
			cardX = widthblock * 5f;
			cardY = heightblock * 12;
			GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "250000 Coins!");
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;

			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "250G")){
				gears = PlayerPrefs.GetInt("Gears");
				if(gears >= 250){
					gears -= 250;
					PlayerPrefs.SetInt("Gears",gears);
					totalMoney = PlayerPrefs.GetInt("PrizeMoney");
					PlayerPrefs.SetInt("PrizeMoney",totalMoney + 250000);
				}
			}
			
			//Premium Bags
			cardX = widthblock * 12f;
			cardY = heightblock * 12;
			GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "Super Rare Parts");
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "50G")){
				gears = PlayerPrefs.GetInt("Gears");
				if(gears >= 500){
					gears -= 500;
					PlayerPrefs.SetInt("Gears",gears);
					PlayerPrefs.SetString("PrizeType","PremiumBag");
					Application.LoadLevel("PrizeCollection");
				}
			}
		}
		
		if(menuCat == "DailySelects"){
			
			//Good Daily Coin Items
			for(int columns = 1; columns < 5; columns++){
				//string carLivery = "livery" + carCount;
				float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
				float cardY = heightblock * 8 - (heightblock * 4);
				GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 7.5f), "");
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), goodItems[columns-1]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 2), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("20livery1") as Texture);
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "You Have 4");

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "1000C")){
					totalMoney-=1000;
				}
			}
			
			//Great Daily Coin Items
			for(int columns = 1; columns < 5; columns++){
				//string carLivery = "livery" + carCount;
				float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
				float cardY = heightblock * (8 * 2) - (heightblock * 3.5f);
				GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 7.5f), "");
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), greatItems[columns-1]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 2), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("20livery1") as Texture);
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "You Have 7");

				//Purchase
				if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "5000C")){
					totalMoney-=5000;
				}
			}
			
			//Premium Liveries
			for(int columns = 1; columns < 5; columns++){
				string carLivery = seriesPrefix + "livery" + (columns-1);
				float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
				float cardY = heightblock * (8 * 3) - (heightblock * 3.5f);
				GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 7.5f), "");
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), DriverNames.cup2020Names[columns-1]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 2), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(carLivery) as Texture);
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				//Progress Bar Box
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "");
				//Progress Bar
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), ((widthblock * 2.5f)/30)*12, heightblock * 1f), "");
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "12/30");

				if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "150G")){
				}
			}
		}
		//End of Daily Selects
		
		if(menuCat == "Fuel"){
			//Fuel
			for(int columns = 1; columns < 3; columns++){
				//string carLivery = "livery" + carCount;
				float cardX = widthblock * (columns * 7f) - (widthblock * 2f);
				float cardY = heightblock * 8 - (heightblock * 4);
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "A full tank of fuel to go racing without needing to wait!");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "5G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears > 5){
						gears-=5;
						GameData.gameFuel+=20;
						PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
						PlayerPrefs.SetInt("Gears",gears);
					}
				}
			}
		}
		//End Of Fuel
		
		if(menuCat == "Legends"){
			//Legends
			for(int columns = 1; columns < 5; columns++){
				//string carLivery = "livery" + carCount;
				float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
				float cardY = heightblock * 8 - (heightblock * 4);
				GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 7.5f), "");
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), DriverNames.legendsNames[columns-1]);
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 2), widthblock * 2.5f, widthblock * 1.25f), Resources.Load(DriverNames.legendsLiveries[columns-1]) as Texture);
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				//Progress Bar Box
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "");
				//Progress Bar
				GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), ((widthblock * 2.5f)/65)*4, heightblock * 1f), "");
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 5f), widthblock * 2.5f, heightblock * 1f), "4/65");

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				if(GUI.Button(new Rect(cardX, cardY + (heightblock * 6), widthblock * 3, heightblock * 1.5f), "450G")){
					totalMoney-=1000;
				}
			}
		}
		
		GUI.EndScrollView();
		
		GUI.skin = buttonSkin;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleLeft;
		
		CommonGUI.BackButton("MainMenu");
		
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		
		CommonGUI.TopBar();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}