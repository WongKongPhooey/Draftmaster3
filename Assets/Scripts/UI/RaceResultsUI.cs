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
	
	
    // Start is called before the first frame update
    void Start(){
		loadResults();
    }

	public void loadResults(){
		
		//Reset results to blank
		foreach (Transform child in resultsFrame){
			Destroy(child.gameObject);
		}
		
		for(int i=0;i<30;i++){
			
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
			resultNumber.texture = Resources.Load<Texture2D>("cup20num" + carNum);
			resultDriver.text = DriverNames.getName("cup20",carNum);
			resultManu.texture = Resources.Load<Texture2D>("Icons/manu-frd");
			resultTime.text = "+" + (carDist / 1000f);
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
