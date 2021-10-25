using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Purchasing;
using Random=UnityEngine.Random;

public class Store : MonoBehaviour{
	
	public GUISkin buttonSkin;
	public GUISkin tileSkin;
	public GUISkin blueGUI;
	public GUISkin redGUI;
	public GUISkin whiteGUI;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	string menuCat;
	string storeFocus;
	
	public static ArrayList dailySelects = new ArrayList();
	
	public static ArrayList eventPrizes = new ArrayList();
	public static ArrayList eventDrawPicks = new ArrayList();
	
	string eventRewards;
	
	public Texture BoxTexture;

	int playerLevel;
	int totalMoney;
	int carMoney;
	int moneyCount;
	int premiumTokens;
	int gears;
	int carGears;
	
	public static string fuelOutput;
	
	int dailyCollected;
	int dailySelectsPicked;
	
	string customStoreDailySelects;
	
	int paintsFound;
	int itemsRemaining;
	public bool eventActive;
	public int offset;
	
	public Texture2D eventImage;
	
	public static bool adWindow;
	public GameObject adFallback;
	public static GameObject adFallbackStatic;
	
	public Texture2D gasCanTex;
	public Texture2D gearTex;

	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		playerLevel = 10;
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		menuCat = "DailySelects";
		
		fuelOutput = "";
		
		if(PlayerPrefs.HasKey("StoreFocus")){
			menuCat = PlayerPrefs.GetString("StoreFocus");
			PlayerPrefs.DeleteKey("StoreFocus");
		}
		
		dailyCollected = PlayerPrefs.GetInt("DailyGarage");
		dailySelectsPicked = PlayerPrefs.GetInt("DailySelects");
		
		//Reset Event Picks
		//PlayerPrefs.DeleteKey("PrizePositions");
		
		offset = 0;
		eventActive = false;
		if(PlayerPrefs.GetInt("EventActive") == 1){
			eventActive = true;
			offset = 2;
			menuCat = "Event";
			
			eventRewards = PlayerPrefs.GetString("EventRewards");
		
			if(eventRewards != ""){
				eventPrizes.Clear();
				string[] rewardsArray = eventRewards.Split(',');
				foreach(string item in rewardsArray){
					eventPrizes.Add(item);
					//Debug.Log(item + " added to store");
				}
				Debug.Log("Total event prizes: " + eventPrizes.Count);
				
				//Reset For Testing
				//PlayerPrefs.DeleteKey("PrizePositions");
				//PlayerPrefs.SetInt("EventItemsRemaining", 15);
				
				//Set the random draw
				if(!PlayerPrefs.HasKey("PrizePositions")){
					for(int i=0;i<eventPrizes.Count;i++){
						int rand = Random.Range(1,15);
						int loopBailout = 45;
						while((eventDrawPicks.Contains(rand))&&(loopBailout > 0)){
							rand = Random.Range(1,15);
							loopBailout--;
						}
						eventDrawPicks.Add(rand);
						PlayerPrefs.SetInt("PrizePosition" + i, rand);
						Debug.Log("Draw position #" + rand);
						
						PlayerPrefs.SetInt("PrizePositions",1);
						itemsRemaining = 15;
						PlayerPrefs.SetInt("EventItemsRemaining", itemsRemaining);
						PlayerPrefs.SetInt("EventAltsFound",0);
					}
				} else {
					itemsRemaining = PlayerPrefs.GetInt("EventItemsRemaining");
				}
				CountEventAltsFound();
			}
			//list[Random.Range(0, list.Count)];
		}
		
		adWindow = false;
		adFallback.SetActive(false);
		adFallbackStatic = adFallback;
		
		customStoreDailySelects = "";
		customStoreDailySelects = PlayerPrefs.GetString("StoreDailySelects");
		Debug.Log("Online store: " + customStoreDailySelects);
		
		if(customStoreDailySelects != ""){
			dailySelects.Clear();
			string[] onlineSelects = customStoreDailySelects.Split(',');
			foreach(string item in onlineSelects){
				dailySelects.Add(int.Parse(item));
				//Debug.Log(item + " added to store");
			}
			Debug.Log("Total shop items added: " + dailySelects.Count);
		} else {
			dailySelects.Clear();
			dailySelects.Add(43);
			dailySelects.Add(14);
			dailySelects.Add(95);
			dailySelects.Add(66);
			dailySelects.Add(54);
			dailySelects.Add(52);
			dailySelects.Add(34);
			dailySelects.Add(32);
			dailySelects.Add(17);
			dailySelects.Add(16);
			dailySelects.Add(13);
			dailySelects.Add(0);
		}
		
		dailySelectsPicked = 1;
		PlayerPrefs.SetInt("DailySelects",1);
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		
		gears = PlayerPrefs.GetInt("Gears");
	}

	public void cancelAd(){
		adWindow = false;
		adFallback.SetActive(false);
	}

    // Update is called once per frame
    void Update(){
    }
	
	bool checkEventPickForAlt(int itemNum){
		for(int i=0;i<10;i++){
			if(PlayerPrefs.HasKey("PrizePosition" + i)){
				if(PlayerPrefs.GetInt("PrizePosition" + i) == itemNum){
					//Debug.Log("Item #" + itemNum + " - Ooo a pick!");
					return true;
				}
			}
		}
		//Debug.Log("Item #" + itemNum + " - No alt here..");
		return false;
	}
	
	int getShopPriceByRarity(int rarity){
		int price = 25;
		switch(rarity){
			case 1:
				price = 5;
				break;
		    case 2:
				price = 10;
				break;
			case 3:
				price = 25;
				break;
			case 4:
				price = 50;
				break;
		    default:
				price = 25;
				break;
		}
		return price;
	}
	
	int CountEventAltsFound(){
		int altsFound = 0;
		string eventAlts = PlayerPrefs.GetString("EventRewards");
		string[] allRewards = eventRewards.Split(',');
		foreach(string alt in allRewards){
			string sanitisedAlt = alt.Replace("livery","").Replace("alt","Alt");
			if(PlayerPrefs.GetInt(sanitisedAlt + "Unlocked") == 1){
				Debug.Log("Alt Found: " + sanitisedAlt);
				altsFound++;
			} else {
				Debug.Log("Alt Not Found: " + sanitisedAlt);
			}
		}
		PlayerPrefs.SetInt("EventAltsFound", altsFound);
		return altsFound;
	}
	
	void OnGUI(){
		if(adWindow == false){
			GUI.skin = buttonSkin;
			
			GUI.skin.label.fontSize = 96 / FontScale.fontScale;
			GUI.skin.button.fontSize = 96 / FontScale.fontScale;

			GUI.skin.label.normal.textColor = Color.black;

			GUI.skin.label.alignment = TextAnchor.UpperLeft;
			GUI.Label(new Rect(widthblock * 5.5f, 20, widthblock * 5, heightblock * 2), "Store");
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;

			int itemCount = 0;
			string seriesPrefix = "cup20";
			int totalItems = 4;
			float windowscroll = 1.5f;
			
			GUI.skin.button.fontSize = 64 / FontScale.fontScale;
			
			if(eventActive == true){
				GUI.skin = redGUI;
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				if (GUI.Button(new Rect(widthblock / 2, heightblock * 4, widthblock * 4, heightblock * 1.5f), "Event")){
					menuCat = "Event";
				}
				GUI.skin = buttonSkin;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				GUI.skin.label.fontSize = 96 / FontScale.fontScale;
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			}
			
			if (GUI.Button(new Rect(widthblock / 2, heightblock * (4 + offset), widthblock * 4, heightblock * 1.5f), "Bundles")){
				menuCat = "Bundles";
			}
			
			if (GUI.Button(new Rect(widthblock / 2, heightblock * (6 + offset), widthblock * 4, heightblock * 1.5f), "Weekly Selects")){
				menuCat = "DailySelects";
			}
			
			if (GUI.Button(new Rect(widthblock / 2, heightblock * (8 + offset), widthblock * 4, heightblock * 1.5f), "Fuel")){
				menuCat = "Fuel";
			}
			
			if (GUI.Button(new Rect(widthblock / 2, heightblock * (10 + offset), widthblock * 4, heightblock * 1.5f), "Premium")){
				Application.LoadLevel("GearsStore");
			}
			
			if(menuCat == "DailySelects"){
				GUI.skin.label.fontSize = 40 / FontScale.fontScale;
				if(PlayerPrefs.HasKey("PlayerUsername")){
				} else {
					GUI.Label(new Rect(widthblock * 5f, heightblock * 3f, widthblock * 12f, heightblock * 1), "Register with an account to see the online store that updates weekly!");
				}
			}
			
			GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
			GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
			scrollPosition = GUI.BeginScrollView(new Rect(widthblock * 4, heightblock * 4, Screen.width - (widthblock * 4), Screen.height - (heightblock * 4)), scrollPosition, new Rect(widthblock * 4, heightblock * 4, Screen.width - (widthblock * 5.5f), Screen.height * windowscroll));

			GUI.skin.label.fontSize = 96 / FontScale.fontScale;
			GUI.skin.button.fontSize = 96 / FontScale.fontScale;
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			
			GUI.skin = tileSkin;
			
			if(menuCat == "Event"){
				//Special Event Garage
				float cardX = widthblock * 5f;
				float cardY = heightblock * 4;
				
				GUI.skin = whiteGUI;
				GUI.Box(new Rect(cardX, cardY, widthblock * 13.5f, heightblock * 12f), "");
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 13f, heightblock * 1.5f), "" + PlayerPrefs.GetString("EventName") + "");
				
				GUI.DrawTexture(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 2.5f), widthblock * 5.5f, heightblock * 8.5f), eventImage, ScaleMode.ScaleToFit);
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				GUI.Label(new Rect(cardX + (widthblock * 6f), cardY + 10 + (heightblock * 2.5f), widthblock * 7f, heightblock * 9), "They say this abandoned garage is haunted, proceed with caution! 15 random prizes inside, including 5 Halloween themed alternate paint schemes and 10 sets of parts for those cars!");
				
				GUI.skin.label.alignment = TextAnchor.LowerLeft;
				
				GUI.Label(new Rect(cardX + (widthblock * 6f), cardY + (heightblock * 9f), widthblock * 4f, heightblock * 1f), PlayerPrefs.GetInt("EventAltsFound") + "/5 paints found");
				
				GUI.Label(new Rect(cardX + (widthblock * 6f), cardY + (heightblock * 10f), widthblock * 4f, heightblock * 1f), itemsRemaining + "/15 items left");
				
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;

				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(itemsRemaining > 0){
					if(GUI.Button(new Rect(cardX + (widthblock * 10f), cardY + (heightblock * 9.5f), widthblock * 3, heightblock * 1.5f), "10G")){
						gears = PlayerPrefs.GetInt("Gears");
						if(gears >= 10){
							gears -= 10;
							bool isPick = checkEventPickForAlt(itemsRemaining);
							itemsRemaining--;
							PlayerPrefs.SetInt("Gears",gears);
							PlayerPrefs.SetInt("EventItemsRemaining",itemsRemaining);
							if(isPick == true){
								int failover = CountEventAltsFound();
								if(failover >= 5){
									Application.LoadLevel("MainMenu");
								}
								PlayerPrefs.SetString("PrizeType","EventAlt");
								PlayerPrefs.SetInt("EventAltsFound",PlayerPrefs.GetInt("EventAltsFound") + 1);
							} else {
								PlayerPrefs.SetString("PrizeType","EventGarage");
							}
							Application.LoadLevel("PrizeCollection");
						}
					}
				}
				GUI.skin = tileSkin;
			}
			
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
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "Stop by the back of the garages for 3-10 free spare parts. Available once per day!");
				
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
				
				//Premium Garage Auctions
				cardX = widthblock * 12f;
				cardY = heightblock * 4;
				
				GUI.skin = whiteGUI;
				
				GUI.Box(new Rect(cardX, cardY, widthblock * 6.5f, heightblock * 7.5f), "");
				
				GUI.skin = tileSkin;
				
				GUI.skin.label.fontSize = 64 / FontScale.fontScale;
				GUI.skin.button.fontSize = 64 / FontScale.fontScale;
				
				GUI.skin.label.alignment = TextAnchor.UpperCenter;
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "Mystery Garage Auction!");
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
				
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
				
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "10-50 car parts from a random car!");
				
				
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "35G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 35){
						gears -= 35;
						PlayerPrefs.SetInt("Gears",gears);
						PlayerPrefs.SetString("PrizeType","MysteryGarage");
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
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "Cash 4 Gears");
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
								
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
								
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "Spend your spare cash on 5 salvaged gears from the scrapyard.");
								
								
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "$50000")){
					totalMoney = PlayerPrefs.GetInt("PrizeMoney");
					if(totalMoney >= 50000){
						totalMoney -= 50000;
						PlayerPrefs.SetInt("PrizeMoney", totalMoney);
						gears = PlayerPrefs.GetInt("Gears");
						PlayerPrefs.SetInt("Gears",gears + 5);
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
				GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + 10, widthblock * 6.5f, heightblock * 4), "Sponsorship Deal");
				
				GUI.skin.label.alignment = TextAnchor.UpperLeft;
								
				GUI.skin.label.fontSize = 48 / FontScale.fontScale;
								
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 10 + (heightblock * 1.5f), widthblock * 5.5f, heightblock * 3), "A large 250k investment for car upgrades.");
								
								
				GUI.skin.button.alignment = TextAnchor.MiddleCenter;
				
				GUI.skin = redGUI;
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "40G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 40){
						gears -= 40;
						PlayerPrefs.SetInt("Gears",gears);
						totalMoney = PlayerPrefs.GetInt("PrizeMoney");
						PlayerPrefs.SetInt("PrizeMoney",totalMoney + 250000);
					}
				}
				GUI.skin = tileSkin;
			}
			
			if(menuCat == "DailySelects"){
				
				//1st Row Offline
				for(int columns = 1; columns < 5; columns++){
					string carNum = dailySelects[columns-1].ToString();
					int carNumInt = int.Parse(carNum);
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
						GUI.skin.label.normal.textColor = Color.white;
						if(carClass < DriverNames.cup2020Rarity[int.Parse(carNum)]){
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
						} else {
							GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
						}
						GUI.skin.label.normal.textColor = Color.black;
					}

					GUI.skin = redGUI;
					
					int itemPrice = getShopPriceByRarity(DriverNames.cup2020Rarity[carNumInt]);
					
					if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "" + itemPrice + "G")){
						
						gears = PlayerPrefs.GetInt("Gears");
						if(gears >= itemPrice){
							gears -= itemPrice;
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
				
				if(dailySelects.Count >= 8){
				
					//2nd Row Offline
					for(int columns = 1; columns < 5; columns++){
						string carNum = dailySelects[4 + columns-1].ToString();
						int carNumInt = int.Parse(carNum);
						float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
						float cardY = heightblock * (8.5f * 2) - (heightblock * 4f);
						
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
							GUI.skin.label.normal.textColor = Color.white;
							if(carClass < DriverNames.cup2020Rarity[int.Parse(carNum)]){
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
							} else {
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
							}
							GUI.skin.label.normal.textColor = Color.black;
						}

						GUI.skin = redGUI;
						
						int itemPrice = getShopPriceByRarity(DriverNames.cup2020Rarity[carNumInt]);
						if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "" + itemPrice + "G")){
							
							gears = PlayerPrefs.GetInt("Gears");
							if(gears >= itemPrice){
								gears -= itemPrice;
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
				}
				
				if(dailySelects.Count >= 12){
				
					//3rd Row Offline
					for(int columns = 1; columns < 5; columns++){
						string carNum = dailySelects[8 + columns-1].ToString();
						int carNumInt = int.Parse(carNum);
						float cardX = widthblock * (columns * 3.5f) + (widthblock * 1.5f);
						float cardY = heightblock * (8.5f * 3) - (heightblock * 4f);
						
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
							GUI.skin.label.normal.textColor = Color.white;
							if(carClass < DriverNames.cup2020Rarity[int.Parse(carNum)]){
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + unlockGears);
							} else {
								GUI.Label(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 4.5f), widthblock * 2.5f, heightblock * 1f), carGears + "/" + classMax);
							}
							GUI.skin.label.normal.textColor = Color.black;
						}

						GUI.skin = redGUI;
						
						int itemPrice = getShopPriceByRarity(DriverNames.cup2020Rarity[carNumInt]);
						if(GUI.Button(new Rect(cardX + (widthblock * 0.25f), cardY + (heightblock * 6), widthblock * 2.5f, heightblock * 1.5f), "" + itemPrice + "G")){
							
							gears = PlayerPrefs.GetInt("Gears");
							if(gears >= itemPrice){
								gears -= itemPrice;
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
				GUI.Label(new Rect(cardX + (widthblock * 0.5f), cardY + 20, widthblock * 5.5f, heightblock * 4), "Promote a sponsor! Watch an ad to get 10 gallons of fuel to get back on track.");
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.Label(new Rect(cardX + (widthblock * 3.5f), cardY + (heightblock * 5.5f), widthblock * 3f, heightblock * 1.5f), fuelOutput);
				
				GUI.skin = redGUI;
				
				if(GameData.gameFuel < GameData.maxFuel){
					if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "Ad")){
						adWindow = true;
						adFallback.SetActive(true);
						AdManager.ShowRewardedVideo();
					}
				} else {
					if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "Full")){
					}
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
				
				if(GUI.Button(new Rect(cardX + (heightblock * 0.5f), cardY + (heightblock * 5.5f), widthblock * 3, heightblock * 1.5f), "5G")){
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= 5){
						gears -= 5;
						GameData.gameFuel+=20;
						PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
						PlayerPrefs.SetInt("Gears",gears);
					}
				}
				GUI.skin = tileSkin;
			}
			//End Of Fuel
			
			if(menuCat == "Gears"){
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
}