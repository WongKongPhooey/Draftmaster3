using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommentaryManager : MonoBehaviour
{
	public static int cooldown;
	int commsFreqGap;
	int rand;
	public AudioSource audioSource;
	AudioClip phraseClip;
	public CommentaryLines commentaryLines;
	
    void Awake(){
		cooldown = 0;
		commsFreqGap = 120; //2 seconds
		//Debug.Log("Found" + audioSource);
	}

    // Update is called once per frame
    void Update(){
		if(cooldown > 0){
			cooldown--;
		} else {
			cooldown = 0;
		}
		//Debug.Log("Cooldown: " + cooldown);
    }
	
	public void commentate(string phrase){
		//Debug.Log("Commentary Triggered..");
		bool canSpeak = checkCommentaryCooldown(cooldown);
		//No interrupting
		if(cooldown != 0){
			return;
		}
		if(canSpeak == true){
			switch(phrase){
				case "Start":
					//Debug.Log(commentaryLines.startClips.Length + " choices");
					rand = Random.Range(0,commentaryLines.startClips.Length);
					phraseClip = commentaryLines.startClips[rand];
					break;
				case "Wallhit":
					rand = Random.Range(0,commentaryLines.wallHitClips.Length);
					phraseClip = commentaryLines.wallHitClips[rand];
					break;
				case "Crash":
					rand = Random.Range(0,commentaryLines.cautionClips.Length);
					phraseClip = commentaryLines.cautionClips[rand];
					break;
				case "":
					break;
				default:
					break;
			}
			cooldown = ((int)phraseClip.length * 60) + commsFreqGap;
			Debug.Log("Cooldown: " + cooldown);
			// phraseClip = (AudioClip)Resources.Load("Commentary/caution1");
			if (!audioSource.isPlaying){
				audioSource.PlayOneShot(phraseClip);
			}
		}
	}
	
	public static bool checkCommentaryCooldown(int cooldown){
		if(cooldown == 0){
			return true;
		} else {
			return false;
		}
	}
}