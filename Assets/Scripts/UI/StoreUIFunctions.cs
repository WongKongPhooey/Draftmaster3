using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StoreUIFunctions : MonoBehaviour
{
	public int itemPrice;
	public string itemSeries;
	public string itemNum;
	public int itemRarity;
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
		
		gears = PlayerPrefs.GetInt("Gears");
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		alertPopup = GameObject.Find("Main").GetComponent<StoreUI>().alertPopup;
		
		if(itemRarity == 1){
			//Money purchase
			if(totalMoney >= itemPrice){
				int carGears = 0;

				if(isAlt == true){
					//Unlock the alt
					if(PlayerPrefs.GetInt(itemSeries + itemNum + "Alt" + itemAlt + "Unlocked") != 1){
						PlayerPrefs.SetInt(itemSeries + itemNum + "Alt" + itemAlt + "Unlocked",1);
						GameObject.Find("Main").GetComponent<StoreUI>().reloadStore();
					} else {
						return;
					}
					string alertContent = "" + DriverNames.getSeriesNiceName(itemSeries);
						   alertContent += " " + DriverNames.getName(itemSeries, int.Parse(itemNum));
						   //Debug.Log(itemSeries + " " + itemNum + " " + itemAlt);
						   alertContent += "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt));
						   alertContent += " Alt Paint Unlocked!";
					//Debug.Log(alertContent);
					string alertImage = itemSeries + "livery" + itemNum + "alt" + itemAlt + "";
					
					totalMoney -= itemPrice;
					PlayerPrefs.SetInt("PrizeMoney",totalMoney);
					
					alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)) + " Alt Paint Unlocked!", itemSeries + "livery" + itemNum + "alt" + itemAlt + "");
				
					PlayerPrefs.SetInt(itemSeries + itemNum + "AltPaint",int.Parse(itemAlt));
					if(AltPaints.getAltPaintDriver(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)) != null){
						PlayerPrefs.SetString(itemSeries + itemNum + "AltDriver", AltPaints.getAltPaintDriver(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)));
						//Debug.Log("Driver Name set: " + AltPaints.getAltPaintDriver(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)));
					}
				
				} else {
					//Increment the car parts
					if(PlayerPrefs.HasKey(itemSeries + itemNum + "Gears")){
						carGears = PlayerPrefs.GetInt(itemSeries + itemNum + "Gears");
					} else {
						carGears = 0;
					}
					PlayerPrefs.SetInt(itemSeries + itemNum + "Gears", carGears + 3);
					
					int carUnlocked = PlayerPrefs.GetInt(itemSeries + itemNum + "Unlocked");
					int carClass = PlayerPrefs.GetInt(itemSeries + itemNum + "Class");
					int progressTarget = 0;
					
					if(carUnlocked == 1){
						int classMax = getClassMax(carClass);
						progressTarget = classMax;
					} else {
						int unlockClass = DriverNames.getRarity(itemSeries,int.Parse(itemNum));
						int unlockGears = GameData.unlockGears(unlockClass);
						progressTarget = unlockGears;
					}
					
					totalMoney -= itemPrice;
					PlayerPrefs.SetInt("PrizeMoney",totalMoney);
					
					//If beyond or at the target..
					if((carGears + 3) >= progressTarget){
						classUp(carGears + 3, progressTarget);
					} else {
						//Just pay and go
						if(carUnlocked == 1){
							alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n+3 Car Parts", itemSeries + "livery" + itemNum, true, (carGears + 3), progressTarget);
						} else {
							alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n+3 Car Parts. Unlocks at " + progressTarget, itemSeries + "livery" + itemNum, true, (carGears + 3), progressTarget);
						}
					}
				}
			} else {
				alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You do not have enough money to purchase this.","dm2logo");
			}
		} else {
			//Gears purchase
			if(gears >= itemPrice){
				int carGears = 0;

				if(isAlt == true){
					//Unlock the alt
					if(PlayerPrefs.GetInt(itemSeries + itemNum + "Alt" + itemAlt + "Unlocked") != 1){
						PlayerPrefs.SetInt(itemSeries + itemNum + "Alt" + itemAlt + "Unlocked",1);
						GameObject.Find("Main").GetComponent<StoreUI>().reloadStore();
					} else {
						return;
					}
					string alertContent = "" + DriverNames.getSeriesNiceName(itemSeries);
						   alertContent += " " + DriverNames.getName(itemSeries, int.Parse(itemNum));
						   //Debug.Log(itemSeries + " " + itemNum + " " + itemAlt);
						   alertContent += "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt));
						   alertContent += " Alt Paint Unlocked!";
					//Debug.Log(alertContent);
					string alertImage = itemSeries + "livery" + itemNum + "alt" + itemAlt + "";
					
					gears -= itemPrice;
					PlayerPrefs.SetInt("Gears",gears);
					
					alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)) + " Alt Paint Unlocked!", itemSeries + "livery" + itemNum + "alt" + itemAlt + "");
				
					PlayerPrefs.SetInt(itemSeries + itemNum + "AltPaint",int.Parse(itemAlt));
					
					if(AltPaints.getAltPaintDriver(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)) != null){
						PlayerPrefs.SetString(itemSeries + itemNum + "AltDriver", AltPaints.getAltPaintDriver(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)));
						Debug.Log("Driver Name set: " + AltPaints.getAltPaintDriver(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)));
					}
				
				} else {
					//Increment the car parts
					if(PlayerPrefs.HasKey(itemSeries + itemNum + "Gears")){
						carGears = PlayerPrefs.GetInt(itemSeries + itemNum + "Gears");
					} else {
						carGears = 0;
					}
					PlayerPrefs.SetInt(itemSeries + itemNum + "Gears", carGears + 3);
					
					int carUnlocked = PlayerPrefs.GetInt(itemSeries + itemNum + "Unlocked");
					int carClass = PlayerPrefs.GetInt(itemSeries + itemNum + "Class");
					int progressTarget = 0;
					
					if(carUnlocked == 1){
						int classMax = getClassMax(carClass);
						progressTarget = classMax;
					} else {
						int unlockClass = DriverNames.getRarity(itemSeries,int.Parse(itemNum));
						int unlockGears = GameData.unlockGears(unlockClass);
						progressTarget = unlockGears;
					}
					
					gears -= itemPrice;
					PlayerPrefs.SetInt("Gears",gears);
					
					//If beyond or at the target..
					if((carGears + 3) >= progressTarget){
						classUp(carGears + 3, progressTarget);
					} else {
						//Just pay and go
						if(carUnlocked == 1){
							alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n+3 Car Parts", itemSeries + "livery" + itemNum, true, (carGears + 3), progressTarget);
						} else {
							alertPopup.GetComponent<AlertManager>().showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n+3 Car Parts. Unlocks at " + progressTarget, itemSeries + "livery" + itemNum, true, (carGears + 3), progressTarget);
						}
					}
				}
			} else {
				alertPopup.GetComponent<AlertManager>().showPopup("Oh no..","You do not have enough Gears to purchase this.","dm2logo");
			}
		}
	}

	public void classUp(int carGears, int targetGears){
		Debug.Log("Class Me Up Scotty!");
		int carUnlocked = PlayerPrefs.GetInt(itemSeries + itemNum + "Unlocked");
		int carClass = PlayerPrefs.GetInt(itemSeries + itemNum + "Class");
		
		if(carUnlocked == 0){
			int unlockClass = DriverNames.getRarity(itemSeries, int.Parse(itemNum));
			int unlockGears = GameData.unlockGears(unlockClass);
			if(carGears >= unlockGears){
				carUnlocked = 1;
				carGears-= unlockGears;
				carClass = unlockClass;
				PlayerPrefs.SetInt(itemSeries + itemNum + "Unlocked",1);
				PlayerPrefs.SetInt(itemSeries + itemNum + "Gears",carGears);
				PlayerPrefs.SetInt(itemSeries + itemNum + "Class",carClass);
				
				//Update the 'class up' Gears value
				int classUpGears = GameData.classMax(carClass);
				
				alertPopup.GetComponent<AlertManager>().showPopup("Car Unlocked!", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\nCar Unlocked!", itemSeries + "livery" + itemNum, true, carGears, classUpGears);
			}
		} else {
			int classMax = getClassMax(carClass);
			if(carGears >= classMax){
				carGears-= classMax;
				carClass++;
				PlayerPrefs.SetInt(itemSeries + itemNum + "Gears",carGears);
				PlayerPrefs.SetInt(itemSeries + itemNum + "Class",carClass);
				
				//Update the 'class up' Gears value
				int classUpGears = GameData.classMax(carClass);
				
				alertPopup.GetComponent<AlertManager>().showPopup("Car Leveled Up!", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\nClass Upgrade! -> Class " + classAbbr(carClass), itemSeries + "livery" + itemNum, true, carGears, classUpGears);
			}
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
				case "MysteryPaint":
					gears = PlayerPrefs.GetInt("Gears");
					if(gears >= itemPrice){
						gears -= itemPrice;
						PlayerPrefs.SetInt("Gears",gears);
						PlayerPrefs.SetString("PrizeType","MysteryPaint");
						SceneManager.LoadScene("PrizeCollection");
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

	public static int getClassMax(int carClass){
		switch(carClass){
			case 0:
				return 10;
				break;
			case 1:
				return 20;
				break;
		    case 2:
				return 35;
				break;
			case 3:
				return 50;
				break;
			case 4:
				return 70;
				break;
			case 5:
				return 100;
				break;
			case 6:
				return 150;
				break;
		    default:
				return 999;
				break;
		}
	}

	string classAbbr(int carClass){
		string classLetter;
		switch(carClass){
			case 1:
				classLetter = "R";
				break;
		    case 2:
				classLetter = "D";
				break;
			case 3:
				classLetter = "C";
				break;
			case 4:
				classLetter = "B";
				break;
			case 5:
				classLetter = "A";
				break;
			case 6:
				classLetter = "S";
				break;
		    default:
				classLetter = "R";
				break;
		}
		return classLetter;
	}
}
