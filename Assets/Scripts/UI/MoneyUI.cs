using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoneyUI : MonoBehaviour
{
	int money;
	int moneyVisual;
	int ticker;
    // Start is called before the first frame update
    void Start()
    {
		money = PlayerPrefs.GetInt("PrizeMoney");
		moneyVisual = money;
        updateMoney();
    }

	void updateMoney(){
		money = PlayerPrefs.GetInt("PrizeMoney");
		this.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = moneyVisual.ToString();
	}
	
    // Update is called once per frame
    void Update()
    {
		ticker++;
		if(ticker%10 == 0){
			updateMoney();
		}
		if(moneyVisual > money){
			if((moneyVisual - money) > 1000){
				moneyVisual-= 1000;
			} else {
				moneyVisual--;
			}
			updateMoney();
		}
		if(moneyVisual < money){
			if((money - moneyVisual) > 1000){
			moneyVisual+=1000;
			} else {
				moneyVisual++;
			}
			updateMoney();
		}
    }
}

