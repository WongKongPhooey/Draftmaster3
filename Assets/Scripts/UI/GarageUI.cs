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
		seriesPrefix = "cup22";
		
		for(int i=0;i<100;i++){
			
			//Skip through the non-driver #s
			if(DriverNames.getName(seriesPrefix, i)== null){
				continue;
			}
			
			GameObject tileInst = Instantiate(carTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			Text carTeam = tileInst.transform.GetChild(0).GetComponent<Text>();
			Text carType = tileInst.transform.GetChild(1).GetComponent<Text>();
			Text carClass = tileInst.transform.GetChild(2).GetComponent<Text>();
			Image carManu = tileInst.transform.GetChild(4).GetComponent<Image>();
			Image carPaint = tileInst.transform.GetChild(5).GetComponent<Image>();
			Text carName = tileInst.transform.GetChild(6).GetComponent<Text>();
			
			carTeam.text = DriverNames.getTeam(seriesPrefix, i);
			carType.text = DriverNames.getType(seriesPrefix, i);
			//carClass.text = DriverNames.getClass(seriesPrefix, i);
			carManu.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, i)); 
			carPaint.overrideSprite = Resources.Load<Sprite>(seriesPrefix + "livery" + i); 

			carName.text = DriverNames.getName(seriesPrefix, i);
		}
		//Debug.Log("Tile Drawn");
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
