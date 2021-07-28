using UnityEngine;
using System.Collections;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Purchasing;

public class Store : MonoBehaviour, IStoreListener{
	
	private static IStoreController m_StoreController;          // The Unity Purchasing system.
	private static IExtensionProvider m_StoreExtensionProvider; // The store-specific Purchasing subsystems.

	// Product identifiers for all products capable of being purchased: 
	// "convenience" general identifiers for use with Purchasing, and their store-specific identifier 
	// counterparts for use with and outside of Unity Purchasing. Define store-specific identifiers 
	// also on each platform's publisher dashboard (iTunes Connect, Google Play Developer Console, etc.)

	// General product identifiers for the consumable, non-consumable, and subscription products.
	// Use these handles in the code to reference which product to purchase. Also use these values 
	// when defining the Product Identifiers on the store. Except, for illustration purposes, the 
	// kProductIDSubscription - it has custom Apple and Google identifiers. We declare their store-
	// specific mapping to Unity Purchasing's AddProduct, below.
	public static string kProductIDConsumable =    "consumable";   
	public static string kProductIDNonConsumable = "nonconsumable";
	public static string kProductIDSubscription =  "subscription"; 

	// Apple App Store-specific product identifier for the subscription product.
	private static string kProductNameAppleSubscription =  "com.unity3d.subscription.new";

	// Google Play Store-specific product identifier subscription product.
	private static string kProductNameGooglePlaySubscription =  "com.unity3d.subscription.original";
	
	public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin blueGUI;
	public GUISkin redGUI;
	public GUISkin whiteGUI;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	string menuCat;
	
	public static ArrayList dailySelects = new ArrayList();
	
	public Texture BoxTexture;

	int playerLevel;
	int totalMoney;
	int carMoney;
	int moneyCount;
	int premiumTokens;
	int gears;
	int carGears;
	
	int dailyCollected;
	int dailySelectsPicked;
	
	public static bool adWindow;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;

	public Vector2 scrollPosition = Vector2.zero;
	
	void Start(){
		// If we haven't set up the Unity Purchasing reference
        if (m_StoreController == null){
            // Begin to configure our connection to Purchasing
            InitializePurchasing();
        }
    }
	
	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		menuCat = "DailySelects";
		
		dailyCollected = PlayerPrefs.GetInt("DailyGarage");
		dailySelectsPicked = PlayerPrefs.GetInt("DailySelects");
		
		adWindow = false;
		
		//if(dailySelectsPicked == 0){
			dailySelects.Add(21);
			dailySelects.Add(77);
			dailySelects.Add(78);
			dailySelects.Add(96);
			dailySelects.Add(6);
			dailySelects.Add(10);
			dailySelects.Add(12);
			dailySelects.Add(88);
			dailySelectsPicked = 1;
			PlayerPrefs.SetInt("DailySelects",1);
		//}
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		
		gears = PlayerPrefs.GetInt("Gears");
	}

    // Update is called once per frame
    void Update(){
    }
	
	void OnGUI(){
		if(adWindow == false){
			GUI.skin = buttonSkin;
			
			GUI.skin.label.fontSize = 96 / FontScale.fontScale;
			GUI.skin.button.fontSize = 96 / FontScale.fontScale;

			GUI.skin.label.normal.textColor = Color.black;

			GUI.skin.label.alignment = TextAnchor.LowerLeft;
			GUI.Label(new Rect(widthblock * 5, heightblock * 1.5f, widthblock * 5, heightblock * 2), "Store");
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;

			int itemCount = 0;
			string seriesPrefix = "cup20";
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

			GUI.skin.label.fontSize = 96 / FontScale.fontScale;
			GUI.skin.button.fontSize = 96 / FontScale.fontScale;
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			
			GUI.skin = tileSkin;
			
			if(menuCat == "Bundles"){
				
				//Free Car Parts
				float cardX = widthblock * 5f;
				float cardY = heightblock * 4;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6f, heightblock * 1.5f), "Daily Junk Spares!");
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "Stop by the back of the garages for 3-10 free spare parts. Available every day!");
				
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(dailyCollected == 0){
					if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "Free")){
						PlayerPrefs.SetString("PrizeType","FreeDaily");
						PlayerPrefs.SetInt("DailyGarage",1);
						dailyCollected = 1;
						Application.LoadLevel("PrizeCollection");
					}
				} else {
					if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "Collected")){
					}
				}
				GUI.skin = tileSkin;
				
				//Premium Bags
				cardX = widthblock * 12f;
				cardY = heightblock * 4;
				
				GUI.skin = whiteGUI;
				
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "3* Rare Auction!");
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "10-50 car parts from a rare 3* car!");
				
				
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "20G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 20){
						gears -= 20;
						PlayerPrefs.SetInt("Gears",gears);
						PlayerPrefs.SetString("PrizeType","PremiumBag");
						Application.LoadLevel("PrizeCollection");
					}
				}
				
				GUI.skin = tileSkin;
				
				
				//Buy Coins
				cardX = widthblock * 5f;
				cardY = heightblock * 12;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "25k Coins");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "10G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 10){
						gears -= 10;
						PlayerPrefs.SetInt("Gears",gears);
						totalMoney = PlayerPrefs.GetInt("PrizeMoney");
						PlayerPrefs.SetInt("PrizeMoney",totalMoney + 25000);
					}
				}
				GUI.skin = tileSkin;
				
				//Premium Bags
				cardX = widthblock * 12f;
				cardY = heightblock * 12;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "250k Coins");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "50G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 50){
						gears -= 50;
						PlayerPrefs.SetInt("Gears",gears);
						totalMoney = PlayerPrefs.GetInt("PrizeMoney");
						PlayerPrefs.SetInt("PrizeMoney",totalMoney + 250000);
					}
				}
				GUI.skin = tileSkin;
			}
			
			if(menuCat == "DailySelects"){
				
				//1* Liveries
				for(int columns = 1; columns < 5; columns++){
					string carNum = dailySelects[columns-1].ToString();
					float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
					float cardY = heightblock * (8.5f * 1) - (heightblock * 4.5f);
					
					int carGears = 0;
					int carClass = 0;
					
					carGears = PlayerPrefs.GetInt(seriesPrefix + carNum + "Gears");
					carClass = PlayerPrefs.GetInt(seriesPrefix + carNum + "Class");
					
					int unlockClass = 1;
					unlockClass = DriverNames.cup2020Rarity[int.Parse(carNum)];
					int unlockGears = GameData.unlockGears(unlockClass);
					
					int classMax = 999;
					classMax = GameData.classMax(carClass);
					if(carClass < unlockClass){
						classMax = unlockGears;
					}
					
					GUI.skin = whiteGUI;
					GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 8f), "");
					GUI.skin = tileSkin;
					
					GUI.skin.label.fontSize = 48 / FontScale.fontScale;
					GUI.skin.button.fontSize = 64 / FontScale.fontScale;
					
					GUI.skin.label.alignment = TextAnchor.UpperCenter;
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), DriverNames.cup2020Names[int.Parse(carNum)] + " (+3)");
					GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("cup20livery" + carNum) as Texture);
					GUI.skin.label.alignment = TextAnchor.MiddleCenter;
					
					//Progress Bar Box
					GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "");
					//Progress Bar
					if((carGears > classMax)||(carClass == 6)){
						GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "");
					} else {
						if(carGears > 0){
							GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), (((widthblock * 2.5f)/classMax) * carGears) + 1, heightblock * 1f), "");
						}
					}
					// Gears/Class 
					if(carClass == 6){
						GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "Max Class");
					} else {
						if(carClass < DriverNames.cup2020Rarity[int.Parse(carNum)]){
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
						} else {
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
						}
					}

					GUI.skin = redGUI;
					if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "5G")){
						gears = PlayerPrefs.GetInt("Gears");
						if(gears >= 5){
							gears -= 5;
							if(PlayerPrefs.HasKey(seriesPrefix + carNum + "Gears")){
								carGears = PlayerPrefs.GetInt(seriesPrefix + carNum + "Gears");
							} else {
								carGears = 0;
							}
							PlayerPrefs.SetInt(seriesPrefix + carNum + "Gears", carGears + 3);
							PlayerPrefs.SetInt("Gears",gears);
						}
					}
					GUI.skin = tileSkin;
				}
				
				//1* Liveries
				for(int columns = 1; columns < 5; columns++){
					string carNum = dailySelects[4 + columns-1].ToString();
					float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
					float cardY = heightblock * (8.5f * 2) - (heightblock * 4f);
					
					GUI.skin = whiteGUI;
					GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 8f), "");
					GUI.skin = tileSkin;
					
					GUI.skin.label.fontSize = 48 / FontScale.fontScale;
					GUI.skin.button.fontSize = 64 / FontScale.fontScale;
					
					GUI.skin.label.alignment = TextAnchor.UpperCenter;
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), DriverNames.cup2020Names[int.Parse(carNum)]);
					GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("cup20livery" + carNum) as Texture);
					GUI.skin.label.alignment = TextAnchor.MiddleCenter;
					//Progress Bar Box
					GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "");
					//Progress Bar
					GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), ((widthblock * 2.5f)/30)*12, heightblock * 1f), "");
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "12/30");

					GUI.skin = redGUI;
					if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "150G")){
					}
					GUI.skin = tileSkin;
				}
				
				//2-3* Liveries
				for(int columns = 1; columns < 5; columns++){
					string carNum = dailySelects[columns-1].ToString();
					float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
					float cardY = heightblock * (8.5f * 3) - (heightblock * 3.5f);
					
					GUI.skin = whiteGUI;
					GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 8f), "");
					GUI.skin = tileSkin;
					
					GUI.skin.label.fontSize = 48 / FontScale.fontScale;
					GUI.skin.button.fontSize = 64 / FontScale.fontScale;
					
					GUI.skin.label.alignment = TextAnchor.UpperCenter;
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), DriverNames.cup2020Names[int.Parse(carNum)]);
					GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("cup20livery" + carNum) as Texture);
					GUI.skin.label.alignment = TextAnchor.MiddleCenter;
					//Progress Bar Box
					GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "");
					//Progress Bar
					GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), ((widthblock * 2.5f)/30)*12, heightblock * 1f), "");
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "12/30");

					GUI.skin = redGUI;
					if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "150G")){
					}
					GUI.skin = tileSkin;
				}
			}
			//End of Daily Selects
			
			if(menuCat == "Fuel"){
				//Fuel
				int columns = 1;
				//string carLivery = "livery" + carCount;
				float cardX = widthblock * (columns * 7f) - (widthblock * 2f);
				float cardY = heightblock * 8 - (heightblock * 4);
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 20, widthblock * 5.5f, heightblock * 4), "Promote a sponsor! Watch an ad to get 5 gallons of fuel to get back on track.");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "Ad")){
					adWindow = true;
					AdManager.ShowRewardedVideo();
				}
				GUI.skin = tileSkin;
				
				columns = 2;
				//string carLivery = "livery" + carCount;
				cardX = widthblock * (columns * 7f) - (widthblock * 2f);
				cardY = heightblock * 8 - (heightblock * 4);
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 20, widthblock * 5.5f, heightblock * 4), "Fuel for sale. A 20 gallon tank that will last the distance.");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "10G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 10){
						gears -= 10;
						GameData.gameFuel+=5;
						PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
						PlayerPrefs.SetInt("Gears",gears);
					}
				}
				GUI.skin = tileSkin;
			}
			//End Of Fuel
			
			if(menuCat == "Gears"){
				
				//20 Gears
				float cardX = widthblock * 5f;
				float cardY = heightblock * 4;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6f, heightblock * 1.5f), "20 Gears!");
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "20 Gears to use in the store");
				
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "$1.99")){
					BuyProductID("com.DuffetyWong.Draftmaster2RollingThunder.gears20");
				}
				GUI.skin = tileSkin;
				
				//Premium Bags
				cardX = widthblock * 12f;
				cardY = heightblock * 4;
				
				GUI.skin = whiteGUI;
				
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "3* Rare Auction!");
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "10-50 car parts from a rare 3* car!");
				
				
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "20G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 20){
						gears -= 20;
						PlayerPrefs.SetInt("Gears",gears);
						PlayerPrefs.SetString("PrizeType","PremiumBag");
						Application.LoadLevel("PrizeCollection");
					}
				}
				
				GUI.skin = tileSkin;
				
				
				//Buy Coins
				cardX = widthblock * 5f;
				cardY = heightblock * 12;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "25k Coins");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "10G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 10){
						gears -= 10;
						PlayerPrefs.SetInt("Gears",gears);
						totalMoney = PlayerPrefs.GetInt("PrizeMoney");
						PlayerPrefs.SetInt("PrizeMoney",totalMoney + 25000);
					}
				}
				GUI.skin = tileSkin;
				
				//Premium Bags
				cardX = widthblock * 12f;
				cardY = heightblock * 12;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "250k Coins");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "50G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 50){
						gears -= 50;
						PlayerPrefs.SetInt("Gears",gears);
						totalMoney = PlayerPrefs.GetInt("PrizeMoney");
						PlayerPrefs.SetInt("PrizeMoney",totalMoney + 250000);
					}
				}
				GUI.skin = tileSkin;
			}
			
			if(menuCat == "Legends"){
				
				//Legends
				for(int columns = 1; columns < 5; columns++){
					float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
					float cardY = heightblock * (8.5f * 1) - (heightblock * 4.5f);
					
					int carGears = 0;
					int carClass = 0;
					
					carGears = PlayerPrefs.GetInt(DriverNames.legendsLiveries[columns-1] + "Gears");
					carClass = PlayerPrefs.GetInt(DriverNames.legendsLiveries[columns-1] + "Class");
					
					int unlockClass = 4;
					int unlockGears = GameData.unlockGears(unlockClass);
					
					int classMax = 999;
					classMax = GameData.classMax(carClass);
					if(carClass < unlockClass){
						classMax = unlockGears;
					}
					
					GUI.skin = whiteGUI;
					GUI.Box(new Rect(cardX, cardY, widthblock * 3, heightblock * 8f), "");
					GUI.skin = tileSkin;
					
					GUI.skin.label.fontSize = 48 / FontScale.fontScale;
					
					GUI.skin.label.alignment = TextAnchor.UpperCenter;
					GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 2.5f, heightblock * 2), DriverNames.legendsNames[columns-1]);
					GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 1.5f), widthblock * 2.5f, widthblock * 1.25f), Resources.Load("cup20livery" + columns) as Texture);
					GUI.skin.label.alignment = TextAnchor.MiddleCenter;
					
					//Progress Bar Box
					GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "");
					//Progress Bar
					if((carGears > classMax)||(carClass == 6)){
						GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "");
					} else {
						if(carGears > 0){
							GUI.Box(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), (((widthblock * 2.5f)/classMax) * carGears) + 1, heightblock * 1f), "");
						}
					}
					// Gears/Class 
					if(carClass == 6){
						GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), "Max Class");
					} else {
						if(carClass < unlockClass){
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
						} else {
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
						}
					}

					GUI.skin = redGUI;
					if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "150G")){
						gears = PlayerPrefs.GetInt("Gears");
						if(gears >= 150){
							gears -= 150;
							if(PlayerPrefs.HasKey(DriverNames.legendsLiveries[columns-1] + "Gears")){
								carGears = PlayerPrefs.GetInt(DriverNames.legendsLiveries[columns-1] + "Gears");
							} else {
								carGears = 0;
							}
							PlayerPrefs.SetInt(DriverNames.legendsLiveries[columns-1] + "Gears", carGears + 3);
							PlayerPrefs.SetInt("Gears",gears);
						}
					}
					GUI.skin = tileSkin;
				}
			}
			
			GUI.EndScrollView();
			
			GUI.skin = redGUI;
			GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
			CommonGUI.BackButton("MainMenu");
			
			GUI.skin = buttonSkin;
			GUI.skin.button.fontSize = 64 / FontScale.fontScale;
			GUI.skin.button.alignment = TextAnchor.MiddleLeft;
			
			CommonGUI.TopBar();

			if (Input.GetKeyDown(KeyCode.Escape)){
				SceneManager.LoadScene("MainMenu");
			}
		}
	}
	
	public void InitializePurchasing(){
		
		// If we have already connected to Purchasing ...
		if (IsInitialized())
		{
			// ... we are done here.
			return;
		}

		// Create a builder, first passing in a suite of Unity provided stores.
		var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

		// Add a product to sell / restore by way of its identifier, associating the general identifier
		// with its store-specific identifiers.
		builder.AddProduct(kProductIDConsumable, ProductType.Consumable);
		// Continue adding the non-consumable product.
		builder.AddProduct(kProductIDNonConsumable, ProductType.NonConsumable);
		// And finish adding the subscription product. Notice this uses store-specific IDs, illustrating
		// if the Product ID was configured differently between Apple and Google stores. Also note that
		// one uses the general kProductIDSubscription handle inside the game - the store-specific IDs 
		// must only be referenced here. 
		builder.AddProduct(kProductIDSubscription, ProductType.Subscription, new IDs(){
			{ kProductNameAppleSubscription, AppleAppStore.Name },
			{ kProductNameGooglePlaySubscription, GooglePlay.Name },
		});

		// Kick off the remainder of the set-up with an asynchrounous call, passing the configuration 
		// and this class' instance. Expect a response either in OnInitialized or OnInitializeFailed.
		UnityPurchasing.Initialize(this, builder);
	}


	private bool IsInitialized()
	{
		// Only say we are initialized if both the Purchasing references are set.
		return m_StoreController != null && m_StoreExtensionProvider != null;
	}


	public void BuyConsumable()
	{
		// Buy the consumable product using its general identifier. Expect a response either 
		// through ProcessPurchase or OnPurchaseFailed asynchronously.
		BuyProductID(kProductIDConsumable);
	}


	public void BuyNonConsumable()
	{
		// Buy the non-consumable product using its general identifier. Expect a response either 
		// through ProcessPurchase or OnPurchaseFailed asynchronously.
		BuyProductID(kProductIDNonConsumable);
	}


	public void BuySubscription()
	{
		// Buy the subscription product using its the general identifier. Expect a response either 
		// through ProcessPurchase or OnPurchaseFailed asynchronously.
		// Notice how we use the general product identifier in spite of this ID being mapped to
		// custom store-specific identifiers above.
		BuyProductID(kProductIDSubscription);
	}


	void BuyProductID(string productId)
	{
		// If Purchasing has been initialized ...
		if (IsInitialized())
		{
			// ... look up the Product reference with the general product identifier and the Purchasing 
			// system's products collection.
			Product product = m_StoreController.products.WithID(productId);

			// If the look up found a product for this device's store and that product is ready to be sold ... 
			if (product != null && product.availableToPurchase)
			{
				Debug.Log(string.Format("Purchasing product asychronously: '{0}'", product.definition.id));
				// ... buy the product. Expect a response either through ProcessPurchase or OnPurchaseFailed 
				// asynchronously.
				m_StoreController.InitiatePurchase(product);
			}
			// Otherwise ...
			else
			{
				// ... report the product look-up failure situation  
				Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
			}
		}
		// Otherwise ...
		else
		{
			// ... report the fact Purchasing has not succeeded initializing yet. Consider waiting longer or 
			// retrying initiailization.
			Debug.Log("BuyProductID FAIL. Not initialized.");
		}
	}


	// Restore purchases previously made by this customer. Some platforms automatically restore purchases, like Google. 
	// Apple currently requires explicit purchase restoration for IAP, conditionally displaying a password prompt.
	public void RestorePurchases()
	{
		// If Purchasing has not yet been set up ...
		if (!IsInitialized())
		{
			// ... report the situation and stop restoring. Consider either waiting longer, or retrying initialization.
			Debug.Log("RestorePurchases FAIL. Not initialized.");
			return;
		}

		// If we are running on an Apple device ... 
		if (Application.platform == RuntimePlatform.IPhonePlayer || 
			Application.platform == RuntimePlatform.OSXPlayer)
		{
			// ... begin restoring purchases
			Debug.Log("RestorePurchases started ...");

			// Fetch the Apple store-specific subsystem.
			var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
			// Begin the asynchronous process of restoring purchases. Expect a confirmation response in 
			// the Action<bool> below, and ProcessPurchase if there are previously purchased products to restore.
			apple.RestoreTransactions((result) => {
				// The first phase of restoration. If no more responses are received on ProcessPurchase then 
				// no purchases are available to be restored.
				Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
			});
		}
		// Otherwise ...
		else
		{
			// We are not running on an Apple device. No work is necessary to restore purchases.
			Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
		}
	}


	//  
	// --- IStoreListener
	//

	public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
	{
		// Purchasing has succeeded initializing. Collect our Purchasing references.
		Debug.Log("OnInitialized: PASS");

		// Overall Purchasing system, configured with products for this application.
		m_StoreController = controller;
		// Store specific subsystem, for accessing device-specific store features.
		m_StoreExtensionProvider = extensions;
	}


	public void OnInitializeFailed(InitializationFailureReason error)
	{
		// Purchasing set-up has not succeeded. Check error for reason. Consider sharing this reason with the user.
		Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
	}


	public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args) 
	{
		// A consumable product has been purchased by this user.
		if (String.Equals(args.purchasedProduct.definition.id, kProductIDConsumable, StringComparison.Ordinal))
		{
			Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", args.purchasedProduct.definition.id));
			// The consumable item has been successfully purchased, add 100 coins to the player's in-game score.
			gears += 20;
			PlayerPrefs.SetInt("Gears",gears);
		}
		// Or ... a non-consumable product has been purchased by this user.
		else if (String.Equals(args.purchasedProduct.definition.id, kProductIDNonConsumable, StringComparison.Ordinal))
		{
			Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", args.purchasedProduct.definition.id));
			// TODO: The non-consumable item has been successfully purchased, grant this item to the player.
		}
		// Or ... a subscription product has been purchased by this user.
		else if (String.Equals(args.purchasedProduct.definition.id, kProductIDSubscription, StringComparison.Ordinal))
		{
			Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", args.purchasedProduct.definition.id));
			// TODO: The subscription item has been successfully purchased, grant this to the player.
		}
		// Or ... an unknown product has been purchased by this user. Fill in additional products here....
		else 
		{
			Debug.Log(string.Format("ProcessPurchase: FAIL. Unrecognized product: '{0}'", args.purchasedProduct.definition.id));
		}

		// Return a flag indicating whether this product has completely been received, or if the application needs 
		// to be reminded of this purchase at next app launch. Use PurchaseProcessingResult.Pending when still 
		// saving purchased products to the cloud, and when that save is delayed. 
		return PurchaseProcessingResult.Complete;
	}


	public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
	{
		// A product purchase attempt did not succeed. Check failureReason for more detail. Consider sharing 
		// this reason with the user to guide their troubleshooting actions.
		Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
	}
}