using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
	public AudioSource source;
	AudioClip sfxClip;
	public SfxClips sfxClips;
	int rand;
	
    // Start is called before the first frame update
    void Start()
    { 
    }

	public void playSfx(string phrase){
		
		switch(phrase){
			case "Skid":
				//Debug.Log(sfxClips.skidClips.Length + " choices");
				rand = Random.Range(0,sfxClips.skidClips.Length);
				sfxClip = sfxClips.skidClips[rand];
				break;
			case "Scrape":
				rand = Random.Range(0,sfxClips.scrapeClips.Length);
				sfxClip = sfxClips.scrapeClips[rand];
				break;
			case "Impact":
				rand = Random.Range(0,sfxClips.impactClips.Length);
				sfxClip = sfxClips.impactClips[rand];
				break;
			case "GearShift":
				rand = Random.Range(0,sfxClips.gearShiftClips.Length);
				sfxClip = sfxClips.gearShiftClips[rand];
				break;
			case "Grass":
				rand = Random.Range(0,sfxClips.grassClips.Length);
				sfxClip = sfxClips.grassClips[rand];
				break;
			default:
				sfxClip = null;
				break;
		}
		if(sfxClip != null){
			source.PlayOneShot(sfxClip);
		}
	}

    // Update is called once per frame
    void Update(){ 
		if((Movement.wreckOver == true)||(RaceHUD.raceOver == true)){
			source.Stop();
		}
    }
}
