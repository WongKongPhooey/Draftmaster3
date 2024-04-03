using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDFunctions : MonoBehaviour
{
	public GameObject HUD;
	public GameObject HUDOpener;
	public GameObject HUDScreen1;
	public GameObject HUDScreen2;
	GameObject Ticker;
	GameObject ShowTickerBtn;
	GameObject HideTickerBtn;
	
    // Start is called before the first frame update
    void Start()
    {
		Ticker = GameObject.Find("Scoreboard");
		ShowTickerBtn = GameObject.Find("ShowScoreboard");
		HideTickerBtn = GameObject.Find("HideScoreboard");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public void HUDScreenCycler(){
		if(HUDScreen1.activeSelf){
			HUDScreen1.SetActive(false);
			HUDScreen2.SetActive(true);
		} else {
			HUDScreen2.SetActive(false);
			HUDScreen1.SetActive(true);
		}
	}
	
	public void HUDToggle(){
		if(HUD.activeSelf){
			HUD.SetActive(false);
			HUDOpener.SetActive(true);
		} else {
			HUD.SetActive(true);
			HUDOpener.SetActive(false);
		}
	}
	
	public void HideTicker(){
		Ticker.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f,120f);
		ShowTickerBtn.GetComponent<UIAnimate>().show();
		HideTickerBtn.GetComponent<UIAnimate>().hide();
	}
	public void ShowTicker(){
		Ticker.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f,0f);
		HideTickerBtn.GetComponent<UIAnimate>().show();
		ShowTickerBtn.GetComponent<UIAnimate>().hide();
	}
}
