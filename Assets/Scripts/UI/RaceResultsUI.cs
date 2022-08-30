using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RaceResultsUI : MonoBehaviour
{
	
	public GameObject resultRow;
	public Transform resultsFrame;
	public string seriesPrefix;
	
	
    // Start is called before the first frame update
    void Start(){
		loadResults();
    }

	public void loadResults(){
		
		//Reset results to blank
		foreach (Transform child in resultsFrame){
			Destroy(child.gameObject);
		}
		
		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
			Debug.Log("Series " + seriesPrefix);
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}
		
		int fieldSize = PlayerPrefs.GetInt("FieldSize");
		
		for(int i=0;i<fieldSize;i++){
			
			int carNum = 999;
			float carDist = 999.999f;
			if(!PlayerPrefs.HasKey("FinishPosition" + i + "")){
				break;
			} else {
				carNum = PlayerPrefs.GetInt("FinishPosition" + i + "");
				carDist = PlayerPrefs.GetInt("FinishTime" + i + "");
			}
			
			GameObject resultInst = Instantiate(resultRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform resultObj = resultInst.GetComponent<RectTransform>();
			resultInst.transform.SetParent(resultsFrame, false);
			resultInst.GetComponent<UIAnimate>().animOffset = i+1;
			resultInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text resultPos = resultInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage resultNumber = resultInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text resultDriver = resultInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			RawImage resultManu = resultInst.transform.GetChild(3).GetComponent<RawImage>();
			TMPro.TMP_Text resultTime = resultInst.transform.GetChild(4).GetComponent<TMPro.TMP_Text>();
			
			resultPos.text = (i+1).ToString();
			resultNumber.texture = Resources.Load<Texture2D>(seriesPrefix + "num" + carNum);
			resultDriver.text = DriverNames.getName(seriesPrefix,carNum);
			resultManu.texture = Resources.Load<Texture2D>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, carNum));
			if(i==0){
				resultTime.text = "";
			} else {
				resultTime.text = "+" + (carDist / 1000f).ToString("f3");
			}
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
