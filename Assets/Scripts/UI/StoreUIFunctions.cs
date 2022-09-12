using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoreUIFunctions : MonoBehaviour
{
	public int itemPrice;
	public string itemSeries;
	public string itemNum;
	public bool carAlt;
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
			gears -= itemPrice;
			int carGears = 0;
			//Increment the car parts
			if(PlayerPrefs.HasKey(itemSeries + itemNum + "Gears")){
				carGears = PlayerPrefs.GetInt(itemSeries + itemNum + "Gears");
			} else {
				carGears = 0;
			}
			PlayerPrefs.SetInt(itemSeries + itemNum + "Gears", carGears + 3);
			//Pay
			PlayerPrefs.SetInt("Gears",gears);
			Debug.Log("Bought!");
			AlertManager.showPopup("Shop Purchase", DriverNames.getSeriesNiceName(itemSeries) + " " + DriverNames.getName(itemSeries, int.Parse(itemNum)) + "\n" + carGears + " -> " + (carGears + 3) + " Car Parts (+3)",itemSeries + "livery" + itemNum);
		} else {

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
