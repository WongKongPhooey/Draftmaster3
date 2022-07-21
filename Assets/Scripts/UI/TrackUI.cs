using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TrackUI : MonoBehaviour
{
	
	public GameObject trackTile;
	public Transform tileFrame;
	public static string[] tracksArray;
	int seriesLength;
	
    // Start is called before the first frame update
    void Start()
    {
		string trackList = PlayerPrefs.GetString("SeriesTrackList");
        loadAllTracks(trackList);
    }

	public void loadAllTracks(string tracks){
		
		foreach (Transform child in tileFrame){
			//Destroy(child.gameObject);
		}
		
		//If there's a track list loaded
		if(tracks != ""){
			tracksArray = tracks.Split(',');
		} else {
			tracks = "1,2,3,4,5";
			tracksArray = tracks.Split(',');
		}
		seriesLength = tracksArray.Length;
		
		for(int i=0;i<seriesLength;i++){
			GameObject tileInst = Instantiate(trackTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.GetComponent<TrackUIFunctions>().trackId = int.Parse(tracksArray[i]);
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			RawImage trackImage = tileInst.transform.GetChild(3).GetComponent<RawImage>();
			trackImage.texture = Resources.Load<Texture2D>(getTrackImage(tracksArray[i]));
		}
	}

	string getTrackImage(string trackId){
		string trackImageName = "";
		switch(trackId){
			case "1":
				trackImageName = "SuperTriOval";
				break;
			case "2":
				trackImageName = "AngledTriOval";
				break;
			case "3":
				trackImageName = "TriOval";
				break;
			case "4":
				trackImageName = "Phoenix";
				break;
			case "5":
				trackImageName = "SuperTriOval";
				break;
			case "6":
				trackImageName = "LongOval";
				break;
			case "7":
				trackImageName = "AngledTriOval";
				break;
			case "8":
				trackImageName = "TinyOval";
				break;
			case "9":
				trackImageName = "TriOval";
				break;
			case "10":
				trackImageName = "Talladega";
				break;
			case "11":
				trackImageName = "SmallOval";
				break;
			case "12":
				trackImageName = "TriOval";
				break;
			case "13":
				trackImageName = "AngledTriOval";
				break;
			case "14":
				trackImageName = "LongPond";
				break;
			case "15":
				trackImageName = "SuperTriOval";
				break;
			case "16":
				trackImageName = "TriOval";
				break;
			case "17":
				trackImageName = "TriOval";
				break;
			case "18":
				trackImageName = "LongOval";
				break;
			case "19":
				trackImageName = "Darlington";
				break;
			case "20":
				trackImageName = "Indianapolis";
				break;
			case "21":
				trackImageName = "BigOval";
				break;
			case "22":
				trackImageName = "Madison";
				break;
			case "23":
				trackImageName = "TriOval";
				break;
			case "30":
				trackImageName = "TinyOval";
				break;
			default:
				break;
		}
		return trackImageName;
		//Debug.Log(circuitChoice + " Loaded");
		//setRaceLaps();
		
		//Testing only
		//PlayerPrefs.SetInt("RaceLaps",2);
		
		//PlayerPrefs.SetString("CurrentTrack","" + order);
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
