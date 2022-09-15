using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoreUIFunctions : MonoBehaviour
{
	public int itemPrice;
	public string itemSeries;
	public string itemNum;
	public string itemAlt;
	public bool isAlt;
	public bool staticBundle;
	public bool moneyPurchase;
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
				AlertManager.showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + AltPaints.getAltPaintName(itemSeries,int.Parse(itemNum),int.Parse(itemAlt)) + " Alt Paint Unlocked!", itemSeries + "livery" + itemNum + "alt" + itemAlt + "");
			} else {
				//Increment the car parts
				if(PlayerPrefs.HasKey(itemSeries + itemNum + "Gears")){
					carGears = PlayerPrefs.GetInt(itemSeries + itemNum + "Gears");
				} else {
					carGears = 0;
				}
				PlayerPrefs.SetInt(itemSeries + itemNum + "Gears", carGears + 3);
				//Pay
				AlertManager.showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + carGears + " -> " + (carGears + 3) + " Car Parts (+3)",itemSeries + "livery" + itemNum);
		
			}
			gears -= itemPrice;
			PlayerPrefs.SetInt("Gears",gears);
			Debug.Log("Bought!");
		} else {
			AlertManager.showPopup("Oh no..","You do not have enough Gears to purchase this.","dm2logo");
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
