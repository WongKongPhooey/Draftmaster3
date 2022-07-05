using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GarageUI : MonoBehaviour
{
	public GameObject carTile;
	public Transform tileFrame;
	string seriesPrefix;
    // Start is called before the first frame update
    void Start(){
		seriesPrefix = "cup20";
		
		for(int i=0;i<100;i++){
			
			//Skip through the non-driver #s
			if(DriverNames.getName(seriesPrefix, i)== null){
				continue;
			}
			
			GameObject tileInst = Instantiate(carTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.GetComponent<GarageTileFunctions>().carSeriesPrefix = seriesPrefix;
			tileInst.GetComponent<GarageTileFunctions>().carNum = i;
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().setCardDown();
			
			Text carTeam = tileInst.transform.GetChild(0).GetComponent<Text>();
			Text carType = tileInst.transform.GetChild(1).GetComponent<Text>();
			Text carClass = tileInst.transform.GetChild(2).GetComponent<Text>();
			Image carManu = tileInst.transform.GetChild(4).GetComponent<Image>();
			RawImage carPaint = tileInst.transform.GetChild(5).GetComponent<RawImage>();
			TMPro.TMP_Text carName = tileInst.transform.GetChild(6).GetComponent<TMPro.TMP_Text>();
			GameObject cardBack = tileInst.transform.GetChild(7).gameObject;
			
			carTeam.text = DriverNames.getTeam(seriesPrefix, i);
			carType.text = DriverNames.shortenedType(DriverNames.getType(seriesPrefix, i));
			//carClass.text = DriverNames.getClass(seriesPrefix, i);
			carManu.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, i)); 
			carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i); 
			//tileInst.transform.GetChild(5).GetComponent<CanvasRenderer>().material.renderQueue = 3001;

			carName.text = DriverNames.getName(seriesPrefix, i);
			
			tileInst.GetComponent<UIAnimate>().flipCardXStart();
			
			cardBack.GetComponent<UIAnimate>().flipCardBacking();
			
			tileInst.GetComponent<UIAnimate>().flipCardXEnd();
		}
		//Debug.Log("Tile Drawn");
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
