using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomNumbers : MonoBehaviour
{
	
	int carNum;
	string carNumber;
	int customNum;
	string seriesPrefix;
	
    // Start is called before the first frame update
    void Awake()
    {
		/*carNumber = PlayerPrefs.GetString("carTexture");			
		string splitAfter = "livery";
		carNumber = carNumber.Substring(carNumber.IndexOf(splitAfter) + splitAfter.Length);
		
		seriesPrefix = "cup20";
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber)){
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber);
			carNum = int.Parse(carNumber);
			if(customNum == carNum){
				PlayerPrefs.DeleteKey("CustomNumber");
			} else {
				Renderer numRend = this.GetComponent<Renderer>();
				this.GetComponent<Renderer>().material.mainTexture = Resources.Load("cup20livery" + customNum) as Texture;
			}
		} else {
		}*/
    }

    // Update is called once per frame
    void Update()
    {   
		/*carNumber = PlayerPrefs.GetString("carTexture");			
		string splitAfter = "livery";
		carNumber = carNumber.Substring(carNumber.IndexOf(splitAfter) + splitAfter.Length);
		
		seriesPrefix = "cup20";
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber)){
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber);
			carNum = int.Parse(carNumber);
			if(customNum == carNum){
				PlayerPrefs.DeleteKey("CustomNumber");
			} else {
				Renderer numRend = this.GetComponent<Renderer>();
				this.GetComponent<Renderer>().material.mainTexture = Resources.Load("cup20livery" + customNum) as Texture;
			}
		} else {
		}*/
    }
}
