using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoreUIFunctions : MonoBehaviour
{
	public int itemPrice;
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
		setPrice();
	}

	public void setPrice(){
		TMPro.TMP_Text btnPrice = this.gameObject.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
		btnPrice.text = itemPrice.ToString();
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
