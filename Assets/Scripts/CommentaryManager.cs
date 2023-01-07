using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommentaryManager : MonoBehaviour
{
	public static int cooldown;
	int rand;
	public AudioSource audioSource;
	AudioClip phraseClip;
	public CommentaryLines commentaryLines;
	
    // Start is called before the first frame update
    void Start(){
		cooldown = 0;
		Debug.Log("Found" + audioSource);
	}

    // Update is called once per frame
    void Update(){
		if(cooldown > 0){
			cooldown--;
		} else {
			cooldown = 0;
		}
    }
	
	public void commentate(string phrase){
		Debug.Log("Commentary Triggered..");
		bool canSpeak = checkCommentaryCooldown(cooldown);
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
			// phraseClip = (AudioClip)Resources.Load("Commentary/caution1");
			audioSource.PlayOneShot(phraseClip);
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