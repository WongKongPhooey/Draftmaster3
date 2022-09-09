using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackUIFunctions : MonoBehaviour
{
	public int trackId;
	public string trackCodeName;

    // Start is called before the first frame update
    void Start()
    {
        
    }

	public void loadTrack(){
		PlayerPrefs.SetString("TrackLocation", TrackData.trackNames[trackId]);
		TrackUI.trackCodeName = trackCodeName;
		GameObject.Find("Main").GetComponent<TrackData>().loadTrackData(trackCodeName);
		TrackUI.startRace(trackCodeName);
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
