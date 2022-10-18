using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StoreUIFunctions : MonoBehaviour
{
	public int itemPrice;
	public string itemSeries;
	public string itemNum;
	public string itemAlt;
	public bool isAlt;
	public bool staticBundle;
	public string staticBundleName;
	public bool moneyPurchase;
	
	int dailyCollected;
	
	int totalMoney;
	int gears;
	
	public static GameObject alertPopup;
	
    // Start is called before the first frame update
    void Start()
    {
        if(staticBundle == true){
			setPrice();
		}
    }

	public void makePurchase(){
		
		int gears = PlayerPrefs.GetInt("Gears");
		//Do you have enough?
		if(gears >= itemPrice){
			int carGears = 0;
			
            alertPopup = GameObject.Find("Main").GetComponent<StoreUI>().alertPopup;
            
			if(isAlt == true){
				//Unlock the alt
				if(PlayerPrefs.GetInt(itemSeries + itemNum + "Alt" + itemAlt + "Unlocked") != 1){
					PlayerPrefs.SetInt(itemSeries + itemNum + "Alt" + itemAlt + "Unlocked",1);
				} else {
					return;
				}
				string alertContent = "" + DriverNames.getSeriesNiceName(itemSeries);
					   alertContent += " " + DriverNames.getName(itemSeries, int.Parse(itemNum));
					   Debug.Log(itemSeries + " " + itemNum + " " + itemAlt);
					   alertContent += "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt));
					   alertContent += " Alt Paint Unlocked!";
				Debug.Log(alertContent);
				string alertImage = itemSeries + "livery" + itemNum + "alt" + itemAlt + "";
				Debug.Log(alertImage);
				alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)) + " Alt Paint Unlocked!", itemSeries + "livery" + itemNum + "alt" + itemAlt + "");
			} else {
				//Increment the car parts
				if(PlayerPrefs.HasKey(itemSeries + itemNum + "Gears")){
					carGears = PlayerPrefs.GetInt(itemSeries + itemNum + "Gears");
				} else {
					carGears = 0;
				}
				PlayerPrefs.SetInt(itemSeries + itemNum + "Gears", carGears + 3);
				//Pay
				alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + carGears + " -> " + (carGears + 3) + " Car Parts (+3)",itemSeries + "livery" + itemNum);
		
			}
			gears -= itemPrice;
			PlayerPrefs.SetInt("Gears",gears);
			Debug.Log("Bought!");
		} else {
			alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You do not have enough Gears to purchase this.","dm2logo");
		}
	}

	public void makeBundlePurchase(){
		if(staticBundle == true){
			
			alertPopup = GameObject.Find("Main").GetComponent<StoreUI>().alertPopup;
			
			switch(staticBundleName){
				case "DailyJunkSpares":
					dailyCollected = PlayerPrefs.GetInt("DailyGarage");
					if(dailyCollected == 0){
					PlayerPrefs.SetString("PrizeType","FreeDaily");
					PlayerPrefs.SetInt("DailyGarage",1);
					dailyCollected = 1;
					SceneManager.LoadScene("PrizeCollection");
					} else {
						alertPopup.GetComponent<AlertManager>().showPopup("Collected","Check back tomorrow for more spares!","dm2logo");
					}
					break;
				case "MysteryGarage":
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= itemPrice){
						gears -= itemPrice;
						PlayerPrefs.SetInt("Gears",gears);
						PlayerPrefs.SetString("PrizeType","MysteryGarage");
						SceneManager.LoadScene("PrizeCollection");
					} else {
						alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You need more Gears to purchase this bundle.","dm2logo");
					}
					break;
				case "Cash4Gears":
					totalMoney = PlayerPrefs.GetInt("PrizeMoney");
					if(totalMoney >= itemPrice){
						totalMoney -= itemPrice;
						PlayerPrefs.SetInt("PrizeMoney", totalMoney);
						gears = PlayerPrefs.GetInt("Gears");
						PlayerPrefs.SetInt("Gears",gears + 5);
						alertPopup.GetComponent<AlertManager>().showPopup("Cash 4 Gears","You agree a deal to purchase 5 Gears from the broker.","Icons/gear");
					} else {
						alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You need more Money to purchase this bundle.","dm2logo");
					}
					break;
				case "SponsorshipDeal":
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= itemPrice){
						gears -= itemPrice;
						PlayerPrefs.SetInt("Gears",gears);
						totalMoney = PlayerPrefs.GetInt("PrizeMoney");
						PlayerPrefs.SetInt("PrizeMoney",totalMoney + 250000);
						alertPopup.GetComponent<AlertManager>().showPopup("Sponsorship Deal","You strike a deal with a wealthy sponsor, who agrees to give you 250000 coins in exchange for Gears.","Icons/money");
					} else {
						alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You need more Gears to purchase this bundle.","dm2logo");
					}
					break;
				case "FullTank":
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= itemPrice){
						gears -= itemPrice;
						GameData.gameFuel=(GameData.maxFuel);
						PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
						PlayerPrefs.SetInt("Gears",gears);
						alertPopup.GetComponent<AlertManager>().showPopup("Full Tank","You fill the fuel tank and head back to the track!","Icons/gascan");
					} else {
						alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You need more Gears to purchase fuel. Fuel will slowly refill every 5 minutes.","dm2logo");
					}
					break;
				default:
					//Invalid code, do nothing
					break;
			}
		}
	}

	public void setPrice(){
		TMPro.TMP_Text btnPrice = this.gameObject.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
		btnPrice.text = itemPrice.ToString();
		if(itemPrice == 0){
			btnPrice.text = "Free";
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
