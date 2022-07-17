using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GarageUI : MonoBehaviour
{
	public GameObject carTile;
	public Transform tileFrame;
	public static string seriesPrefix;
	public List<RectTransform> shuffleArray;
    // Start is called before the first frame update
    void Start(){
		seriesPrefix = "cup20";
		loadAllCars();
	}
	
	public void loadAllCars(){
		
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		for(int i=0;i<100;i++){
			
			//Skip through the non-driver #s
			if(DriverNames.getName(seriesPrefix, i)== null){
				continue;
			}
			
			GameObject tileInst = Instantiate(carTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.GetComponent<GarageUIFunctions>().seriesPrefix = seriesPrefix;
			tileInst.GetComponent<GarageUIFunctions>().carNum = i;
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			//tileInst.GetComponent<UIAnimate>().setCardDown();
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			Text carTeamUI = tileInst.transform.GetChild(0).GetComponent<Text>();
			Text carTypeUI = tileInst.transform.GetChild(1).GetComponent<Text>();
			Text carClassUI = tileInst.transform.GetChild(2).GetComponent<Text>();
			Image carManuUI = tileInst.transform.GetChild(4).GetComponent<Image>();
			RawImage carPaint = tileInst.transform.GetChild(5).GetComponent<RawImage>();
			TMPro.TMP_Text carName = tileInst.transform.GetChild(6).GetComponent<TMPro.TMP_Text>();
			GameObject cardBack = tileInst.transform.GetChild(7).gameObject;
			GameObject carClickable = tileInst.transform.GetChild(8).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(9).transform.gameObject;
			
			int carUnlocked = PlayerPrefs.GetInt(seriesPrefix + i + "Unlocked");
			int carGears = PlayerPrefs.GetInt(seriesPrefix + i + "Gears");
			int carClass = PlayerPrefs.GetInt(seriesPrefix + i + "Class");
			
			string carTeam = DriverNames.getTeam(seriesPrefix, i);
			string carType = DriverNames.getType(seriesPrefix, i);
			string carManu = DriverNames.getManufacturer(seriesPrefix, i);
			
			carTeamUI.text = carTeam;
			carTypeUI.text = DriverNames.shortenedType(DriverNames.getType(seriesPrefix, i));
			carClassUI.text = "Class " + classAbbr(carClass);
			carClassUI.color = classColours(carClass);
			carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + carManu); 
			carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i); 

			carName.text = DriverNames.getName(seriesPrefix, i);
			
			int minClass = PlayerPrefs.GetInt("SubseriesMinClass");
			string restrictionType = PlayerPrefs.GetString("RestrictionType");
			string restrictionValue = PlayerPrefs.GetString("RestrictionValue");
			
			if(minClass > carClass){
				carClickable.SetActive(false);
				carDisabled.SetActive(true);
			}
			
			//tileInst.GetComponent<UIAnimate>().flipCardXStart();
			//cardBack.GetComponent<UIAnimate>().flipCardBacking();
			//tileInst.GetComponent<UIAnimate>().flipCardXEnd();
		}
		
		int sortCounter=0;
		foreach (Transform child in tileFrame){
			GameObject eligibleEntry = child.transform.GetChild(9).transform.gameObject;

			if(eligibleEntry.activeSelf == true){
				RectTransform tileObj = child.GetComponent<RectTransform>();
				//Debug.Log(child);
				if(tileObj != null){
					shuffleArray.Add(tileObj);
				}
			}
			sortCounter++;
		}
		
		sortTiles();
	}

	public void sortTiles(){
		if (shuffleArray.Any()){
			RectTransform tile = shuffleArray.First();
			tile.SetAsLastSibling();
			shuffleArray.Remove(tile);
			sortTiles();
		}
	}

	public void backButton(){
		SceneManager.LoadScene("Menus/SeriesSelect");
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
	
	Color classColours(int carClass){
		Color classColour;
		switch(carClass){
			case 1:
				classColour = new Color32(164,6,6,255);
				break;
		    case 2:
				classColour = new Color32(255,165,0,255);
				break;
			case 3:
				classColour = new Color32(238,130,238,255);
				break;
			case 4:
				classColour = new Color32(0,128,0,255);
				break;
			case 5:
				classColour = new Color32(0,0,255,255);
				break;
			case 6:
				classColour = new Color32(75,0,130,255);
				break;
		    default:
				classColour = new Color32(164,6,6,255);
				break;
		}
		return classColour;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
