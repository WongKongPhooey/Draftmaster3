using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StoreUI : MonoBehaviour
{
	public GameObject storeTile;
	
	public Transform weeklyTileFrame;
	public Transform dailyTileFrame;
	public Transform starterTileFrame;
	
	public static ArrayList weeklyPicks = new ArrayList();
	public static ArrayList starterPicks = new ArrayList();
	
    // Start is called before the first frame update
    void Start()
    {
        loadWeeklyPicks();
		loadStarterPicks();
		reloadContentFitters();
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
			TMPro.TMP_Text tilePrice = tileInst.transform.GetChild(3).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			GameObject carClickable = tileInst.transform.GetChild(3).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(4).transform.gameObject;
			
			string carNum = weeklyPicks[i].ToString();
			string carSeries = "cup20";
			string carAlt = null;
			//if series is specified..
			if((carNum.Length > 2)&&(carNum.Length <= 7)){
				//Get series, parse remainder
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
			}
			if(carNum.Length > 7){
				carSeries = carNum.Substring(0, 5);
				carAlt = carNum.Remove(0, carNum.Length-1);
				carNum = carNum.Remove(0, 5);
				carNum = carNum.Remove(carNum.Length-1,-1);
			}
			int carNumInt = int.Parse(carNum);
			
			tileSeries.text = DriverNames.getSeriesNiceName(carSeries);
			tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum);
			tileName.text = DriverNames.getName(carSeries, carNumInt);
			tilePrice.text = DriverNames.getStorePrice(carSeries, carNumInt, false, false).ToString();
		}
	}
	
	public void loadStarterPicks(){
		
		foreach (Transform child in starterTileFrame){
			Destroy(child.gameObject);
		}
		
		starterPicks.Clear();
		
		starterPicks.Add(7);
		starterPicks.Add(8);
		starterPicks.Add(17);
		starterPicks.Add("cup2221");
		starterPicks.Add(37);
		starterPicks.Add("cup2250");
		starterPicks.Add(51);
		starterPicks.Add("cup2262");
		starterPicks.Add("cup2278");
		starterPicks.Add(95);
		
		for(int i=0;i<starterPicks.Count;i++){
			GameObject tileInst = Instantiate(storeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.transform.SetParent(starterTileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			//tileInst.GetComponent<UIAnimate>().setCardDown();
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text tileSeries = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage tilePaint = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text tileName = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text tilePrice = tileInst.transform.GetChild(3).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			GameObject carClickable = tileInst.transform.GetChild(3).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(4).transform.gameObject;
			
			string carNum = starterPicks[i].ToString();
			string carSeries = "cup20";
			string carAlt = null;
			//if series is specified..
			if((carNum.Length > 2)&&(carNum.Length <= 7)){
				//Get series, parse remainder
				carSeries = carNum.Substring(0, 5);
				carNum = carNum.Remove(0, 5);
			}
			if(carNum.Length > 7){
				carSeries = carNum.Substring(0, 5);
				carAlt = carNum.Remove(0, carNum.Length-1);
				carNum = carNum.Remove(0, 5);
				carNum = carNum.Remove(carNum.Length-1,-1);
			}
			int carNumInt = int.Parse(carNum);
			
			tileSeries.text = DriverNames.getSeriesNiceName(carSeries);
			tilePaint.texture = Resources.Load<Texture2D>(carSeries + "livery" + carNum);
			tileName.text = DriverNames.getName(carSeries, carNumInt);
			tilePrice.text = DriverNames.getStorePrice(carSeries, carNumInt, false, false).ToString();
		}
	}
	
	public void reloadContentFitters(){
		
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
