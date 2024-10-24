using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;
using FitMode = UnityEngine.UI.ContentSizeFitter.FitMode;

public class StoreUI : MonoBehaviour
{
	
	public static int gears;
	public static int transfersMax;
	public static int transfersLeft;
	
	private const string gears60 = "com.duffetywong.draftmaster2rollingthunder.gears60";
	private const string gears125 = "com.duffetywong.draftmaster2rollingthunder.gears125";
	private const string gears200 = "com.duffetywong.draftmaster2rollingthunder.gears200";
	private const string gears500 = "com.duffetywong.draftmaster2rollingthunder.gears500";
	
	private const string smallgears = "com.duffetywong.draftmaster2rollingthunder.smallgears";
	private const string mediumgears = "com.duffetywong.draftmaster2rollingthunder.mediumgears";
	private const string largegears = "com.duffetywong.draftmaster2rollingthunder.largegears";
	private const string extralargegears = "com.duffetywong.draftmaster2rollingthunder.extralargegears";
	
	private const string negotiator = "com.duffetywong.draftmaster2rollingthunder.negotiator";
	private const string negotiatorios = "com.duffetywong.draftmaster2rollingthunder.negotiatorios";
	
	public GameObject restorePurchaseBtn;
	
	public GameObject storeTile;
	
	public GameObject alertPopup;
	
	public Texture2D altButton;
	
	public Transform weeklyTileFrame;
	public GameObject weeklyPicksMessage;
	public Transform dailyTileFrame;
	public Transform starterTileFrame;
	
	public static ArrayList weeklyPicks = new ArrayList();
	public static ArrayList dailyRandoms = new ArrayList();
	public static ArrayList starterPicks = new ArrayList();
	
	public int fitHack;
	public int loadHack;
	
    // Start is called before the first frame update
    void Awake(){
        //Drag define this manually in the scene
        weeklyPicksMessage = GameObject.Find("WeeklyPicksMsg");
		weeklyPicksMessage.SetActive(false);
    }
    
    void Start()
    {
        loadWeeklyPicks();
		loadDailyPicks();
		loadStarterPicks();
		DisableRestorePurchase();
		//this.gameObject.GetComponent<AdsInitializer>().InitializeAds();
    }
	
	public void reloadStore(){
        loadWeeklyPicks();
		loadDailyPicks();
		loadStarterPicks();
	}

	public void loadWeeklyPicks(){
		
		foreach (Transform child in weeklyTileFrame){
			Destroy(child.gameObject);
		}
		
		string customStoreWeeklyPicks = PlayerPrefs.GetString("StoreDailySelects");
		Debug.Log("Online store: " + customStoreWeeklyPicks);
		
		if(customStoreWeeklyPicks != ""){
			weeklyPicks.Clear();
			string[] onlineSelects = customStoreWeeklyPicks.Split(',');
			foreach(string item in onlineSelects){
				weeklyPicks.Add(item);
			}
			Debug.Log("Total shop items added: " + weeklyPicks.Count);
		} else {
			weeklyPicksMessage.SetActive(true);
		}
		
		for(int i=0;i<weeklyPicks.Count;i++){
			GameObject tileInst = Instantiate(storeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.transform.SetParent(weeklyTileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			//tileInst.GetComponent<UIAnimate>().setCardDown();
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text tileSeries = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage tilePaint = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text tileName = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text tileQuantity = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			Transform tilePrice = tileInst.transform.GetChild(4);
			GameObject carClickable = tileInst.transform.GetChild(4).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(5).transform.gameObject;
			
			string carNum = weeklyPicks[i].ToString();
			string carSeries = "cup20";
			string carAlt = null;
			int carAltInt = 0;
			bool isAlt = false;
			//if series is specified..
			if((carNum.Length > 2)&&(carNum.Length <= 7)){
				//Get series, parse remainder
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
			}
			//This is an alt
			if(carNum.Length > 7){
				isAlt = true;
				//Extract series from front
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
				//Debug.Log("Series " + carSeries);
				
				//Extract alt from end
				carAlt = carNum.Substring(carNum.Length-1);
				carAltInt = int.Parse(carAlt);
				carNum = carNum.Remove(carNum.Length-1);
				
				//Remove the remaining chars (livery/alt)
				carNum = Regex.Replace(carNum, "[A-Za-z ]", "");
			}
			int carNumInt = int.Parse(carNum);
			
			tileSeries.text = DriverNames.getSeriesNiceName(carSeries);
			int itemPrice = 999999;
			int itemRarity = DriverNames.getRarity(carSeries, carNumInt);
			tilePrice.GetComponent<StoreUIFunctions>().itemRarity = itemRarity;
			if(isAlt == true){
				tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum + "alt" + carAlt);
				tileName.text = AltPaints.getAltPaintName(carSeries, carNumInt, carAltInt);
				tileQuantity.text = "Paint Scheme";
				tilePrice.GetComponent<RawImage>().texture = altButton;
				tilePrice.GetComponent<StoreUIFunctions>().itemRarity = itemRarity;
				tilePrice.GetComponent<StoreUIFunctions>().isAlt = true;
				tilePrice.GetComponent<StoreUIFunctions>().itemAlt = carAlt;
				itemPrice = DriverNames.getStorePrice(carSeries, carNumInt, true, false, itemRarity);
				tilePrice.GetComponent<StoreUIFunctions>().itemPrice = itemPrice;
				
				//Check we don't already have it..
				if(PlayerPrefs.GetInt(carSeries + carNum + "Alt" + carAlt + "Unlocked") == 1){
					tilePrice.transform.gameObject.SetActive(false);
				}
			} else {
				tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum);
				tileName.text = DriverNames.getName(carSeries, carNumInt) + "";
				tileQuantity.text = "+3 Parts";
				itemPrice = DriverNames.getStorePrice(carSeries, carNumInt, false, false, itemRarity);
				tilePrice.GetComponent<StoreUIFunctions>().isAlt = false;
			}
			tilePrice.GetComponent<StoreUIFunctions>().itemPrice = itemPrice;
			tilePrice.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = itemPrice.ToString();
			tilePrice.transform.GetChild(1).GetComponent<RawImage>().texture = DriverNames.getStoreBtnIcon(itemRarity);
			tilePrice.GetComponent<StoreUIFunctions>().itemSeries = carSeries;
			tilePrice.GetComponent<StoreUIFunctions>().itemNum = carNum;
		}
	}
	
	public void loadDailyPicks(){
		
		//PlayerPrefs.DeleteKey("DailyRandoms");
		
		if(!PlayerPrefs.HasKey("DailyRandoms")){
			//Generate Daily Random Picks
			string dailyRandomsList = "";
			string randSeries = DriverNames.getRandomWinnableSeries();
			
			for(int i=0;i<20;i++){
				//Starts on an invalid number so the while runs at least once
				int rand = 100;
				while((DriverNames.getRarity(randSeries,rand) == 0) || 
					  (DriverNames.getRarity(randSeries,rand) > 3) ||
					  (dailyRandoms.Contains("" + randSeries + rand + "") == true)){
					randSeries = DriverNames.getRandomWinnableSeries();
					rand = Mathf.FloorToInt(Random.Range(0,100));
				}
				string randAlt = DriverNames.getRandomAltPaint(randSeries,rand,true,10);
				if(randAlt != null){
					if(AltPaints.getAltPaintCanBuy(randSeries,rand,int.Parse(randAlt.Substring(randAlt.Length - 1))) != true){
						dailyRandoms.Add(randAlt);
						dailyRandomsList += randAlt + ",";
					} else {
						dailyRandoms.Add("" + randSeries + "" + rand + "");
						dailyRandomsList += "" + randSeries + "" + rand + ",";
					}
				} else {	
					dailyRandoms.Add("" + randSeries + "" + rand + "");
					dailyRandomsList += "" + randSeries + "" + rand + ",";
				}
			}
			//Debug.Log("Daily Picks: " + dailyRandomsList);
			PlayerPrefs.SetString("DailyRandoms",dailyRandomsList);
		} else {
			//Retrieve Daily Random Picks
			List<string> dailyRandomsList = PlayerPrefs.GetString("DailyRandoms").Split(',').ToList<string>();
			dailyRandoms.Clear();
			foreach(string dailyRand in dailyRandomsList){
				dailyRandoms.Add(dailyRand);
				//Debug.Log(dailyRand);
			}
		}
		
		foreach (Transform child in dailyTileFrame){
			Destroy(child.gameObject);
		}
		
		for(int i=0;i<dailyRandoms.Count;i++){
			string carNum = dailyRandoms[i].ToString();
			if((carNum == null)||(carNum == "")){
				continue;
			}
			GameObject tileInst = Instantiate(storeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.transform.SetParent(dailyTileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			//tileInst.GetComponent<UIAnimate>().setCardDown();
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text tileSeries = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage tilePaint = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text tileName = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text tileQuantity = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			Transform tilePrice = tileInst.transform.GetChild(4);
			GameObject carClickable = tileInst.transform.GetChild(4).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(5).transform.gameObject;
			
			string carSeries = "cup20";
			string carAlt = null;
			int carAltInt = 0;
			bool isAlt = false;
			//if series is specified..
			if((carNum.Length > 2)&&(carNum.Length <= 7)){
				//Get series, parse remainder
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
			}
			
			//This is an alt
			if(carNum.Length > 7){
				isAlt = true;
				//Extract series from front
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
				
				//Extract alt from end
				carAlt = carNum.Substring(carNum.Length-1);
				carAltInt = int.Parse(carAlt);
				carNum = carNum.Remove(carNum.Length-1);
				//Debug.Log("Alt " + carAlt);
				
				//Remove the remaining chars (livery/alt)
				carNum = Regex.Replace(carNum, "[A-Za-z ]", "");
			}
			int carNumInt = int.Parse(carNum);
			
			tileSeries.text = DriverNames.getSeriesNiceName(carSeries);
			int itemPrice = 999999;
			int itemRarity = DriverNames.getRarity(carSeries, carNumInt);
			tilePrice.GetComponent<StoreUIFunctions>().itemRarity = itemRarity;
			if(isAlt == true){
				tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum + "alt" + carAlt);
				tileName.text = AltPaints.getAltPaintName(carSeries, carNumInt, carAltInt);
				tileQuantity.text = "Paint Scheme";
				tilePrice.GetComponent<RawImage>().texture = altButton;
				tilePrice.GetComponent<StoreUIFunctions>().itemRarity = itemRarity;
				tilePrice.GetComponent<StoreUIFunctions>().isAlt = true;
				tilePrice.GetComponent<StoreUIFunctions>().itemAlt = carAlt;
				itemPrice = DriverNames.getStorePrice(carSeries, carNumInt, true, false, itemRarity);
				tilePrice.GetComponent<StoreUIFunctions>().itemPrice = itemPrice;
				
				//Check we don't already have it..
				if(PlayerPrefs.GetInt(carSeries + carNum + "Alt" + carAlt + "Unlocked") == 1){
					tilePrice.transform.gameObject.SetActive(false);
				}
			} else {
				tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum);
				tileName.text = DriverNames.getName(carSeries, carNumInt) + "";
				tileQuantity.text = "+3 Parts";
				itemPrice = DriverNames.getStorePrice(carSeries, carNumInt, false, false, itemRarity);
				tilePrice.GetComponent<StoreUIFunctions>().isAlt = false;
			}
			tilePrice.GetComponent<StoreUIFunctions>().itemPrice = itemPrice;
			tilePrice.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = itemPrice.ToString();
			tilePrice.transform.GetChild(1).GetComponent<RawImage>().texture = DriverNames.getStoreBtnIcon(itemRarity);
			tilePrice.GetComponent<StoreUIFunctions>().itemSeries = carSeries;
			tilePrice.GetComponent<StoreUIFunctions>().itemNum = carNum;
		}
	}
	
	public void loadStarterPicks(){
		
		foreach (Transform child in starterTileFrame){
			Destroy(child.gameObject);
		}
		
		starterPicks.Clear();
		
		starterPicks.Add("irl2420");
		starterPicks.Add("irl2466");
		starterPicks.Add("cup2433");
		starterPicks.Add("cup2451");
		starterPicks.Add("cup2471");
		starterPicks.Add("irl2344");
		starterPicks.Add("cup2362");
		starterPicks.Add("cup2291");
		starterPicks.Add(77);
		starterPicks.Add("dmc1528");
		
		for(int i=0;i<starterPicks.Count;i++){
			GameObject tileInst = Instantiate(storeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.transform.SetParent(starterTileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text tileSeries = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage tilePaint = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text tileName = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text tileQuantity = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			Transform tilePrice = tileInst.transform.GetChild(4);
			GameObject carClickable = tileInst.transform.GetChild(4).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(5).transform.gameObject;
			
			string carNum = starterPicks[i].ToString();
			string carSeries = "cup20";
			string carAlt = null;
			int carAltInt = 0;
			bool isAlt = false;
			//if series is specified..
			if((carNum.Length > 2)&&(carNum.Length <= 7)){
				//Get series, parse remainder
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
			}
			
			//This is an alt
			if(carNum.Length > 7){
				isAlt = true;
				//Extract series from front
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
				Debug.Log("Series " + carSeries);
				
				//Extract alt from end
				carAlt = carNum.Substring(carNum.Length-1);
				Debug.Log("Alt " + carAlt);
				carAltInt = int.Parse(carAlt);
				carNum = carNum.Remove(carNum.Length-1);
				Debug.Log("Alt " + carAlt);
				
				//Remove the remaining chars (livery/alt)
				carNum = Regex.Replace(carNum, "[A-Za-z ]", "");
			}
			int carNumInt = int.Parse(carNum);
			
			tileSeries.text = DriverNames.getSeriesNiceName(carSeries);
			int itemPrice = 999999;
			int itemRarity = DriverNames.getRarity(carSeries, carNumInt);
			tilePrice.GetComponent<StoreUIFunctions>().itemRarity = itemRarity;
			if(isAlt == true){
				tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum + "alt" + carAlt);
				tileName.text = AltPaints.getAltPaintName(carSeries, carNumInt, carAltInt);
				tileQuantity.text = "Paint Scheme";
				tilePrice.GetComponent<RawImage>().texture = altButton;
				tilePrice.GetComponent<StoreUIFunctions>().itemRarity = itemRarity;
				tilePrice.GetComponent<StoreUIFunctions>().isAlt = true;
				tilePrice.GetComponent<StoreUIFunctions>().itemAlt = carAlt;
				itemPrice = DriverNames.getStorePrice(carSeries, carNumInt, true, false, itemRarity);
				tilePrice.GetComponent<StoreUIFunctions>().itemPrice = itemPrice;
				
				//Check we don't already have it..
				if(PlayerPrefs.GetInt(carSeries + carNum + "Alt" + carAlt + "Unlocked") == 1){
					tilePrice.transform.gameObject.SetActive(false);
				}
			} else {
				tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum);
				tileName.text = DriverNames.getName(carSeries, carNumInt) + "";
				tileQuantity.text = "+3 Parts";
				itemPrice = DriverNames.getStorePrice(carSeries, carNumInt, false, false, itemRarity);
				tilePrice.GetComponent<StoreUIFunctions>().isAlt = false;
			}
			tilePrice.GetComponent<StoreUIFunctions>().itemPrice = itemPrice;
			tilePrice.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = itemPrice.ToString();
			tilePrice.transform.GetChild(1).GetComponent<RawImage>().texture = DriverNames.getStoreBtnIcon(itemRarity);
			tilePrice.GetComponent<StoreUIFunctions>().itemSeries = carSeries;
			tilePrice.GetComponent<StoreUIFunctions>().itemNum = carNum;
		}
	}
	
	public void reloadContentFitters(int fitHack){
		if(fitHack == 1){
			this.gameObject.GetComponent<ContentSizeFitter>().verticalFit = (UnityEngine.UI.ContentSizeFitter.FitMode)1;
		} else {
			this.gameObject.GetComponent<ContentSizeFitter>().verticalFit = (UnityEngine.UI.ContentSizeFitter.FitMode)2;
		}
	}

	public void OnPurchaseComplete(Product product){
		
		gears = PlayerPrefs.GetInt("Gears");
		transfersMax = PlayerPrefs.GetInt("TransferTokens");
		transfersLeft = PlayerPrefs.GetInt("TransfersLeft");
		
		switch(product.definition.id){
			case gears60:
				Debug.Log("Added 150 gears");
				gears+=150;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","150 Gears have been added!","dm2logo");
				break;
			case gears125:
				Debug.Log("Added 450 gears");
				gears+=450;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","450 Gears have been added!","dm2logo");
				break;
			case gears200:
				Debug.Log("Added 1100 gears");
				gears+=1100;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","1100 Gears have been added!","dm2logo");
				break;
			case gears500:
				Debug.Log("Added 2500 gears");
				gears+=2500;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","2500 Gears have been added!","dm2logo");
				break;
			case smallgears:
				Debug.Log("Added 80 gears");
				gears+=80;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","80 Gears have been added!","dm2logo");
				break;
			case mediumgears:
				Debug.Log("Added 250 gears");
				gears+=250;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","250 Gears have been added!","dm2logo");
				break;
			case largegears:
				Debug.Log("Added 600 gears");
				gears+=600;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","600 Gears have been added!","dm2logo");
				break;
			case extralargegears:
				Debug.Log("Added 1500 gears");
				gears+=1500;
				PlayerPrefs.SetInt("Gears",gears);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","1500 Gears have been added!","dm2logo");
				break;
			case negotiator:
			case negotiatorios:
				Debug.Log("Added 999 contracts");
				transfersMax=999;
				transfersLeft=999;
				PlayerPrefs.SetInt("TransferTokens",transfersMax);
				PlayerPrefs.SetInt("TransfersLeft",transfersLeft);
				PlayerPrefs.SetInt("ModAccess",1);
				alertPopup.GetComponent<AlertManager>().showPopup("Purchase Successful","Mod Support Unlocked!\n999 Transfer Tokens have been added!","dm2logo");
				break;
			default:
				break;
		}
	}
	
	public void OnPurchaseFailed(Product product, PurchaseFailureReason reason){
		Debug.Log("Purchase of " + product.definition.id + " failed. Reason: " + reason);
	}
	
	private void DisableRestorePurchase(){
		if(Application.platform != RuntimePlatform.IPhonePlayer){
			restorePurchaseBtn.SetActive(false);
		}
	}

    // Update is called once per frame
    void Update(){
		if(loadHack < 10){
			if(fitHack == 1){
				fitHack = 2;
			} else {
				fitHack = 1;
			}
			loadHack++;
		} else {
			return;
		}
		reloadContentFitters(fitHack);
    }
}