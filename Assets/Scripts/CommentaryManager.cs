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
	}

    // Update is called once per frame
    void Update(){
		if(cooldown > 0){
			cooldown--;
		} else {
			cooldown = 0;
		}
		if((Movement.wreckOver == true)||(RaceHUD.raceOver == true)){
			audioSource.Stop();
		}
    }
	
	public void commentate(string phrase){
		Debug.Log("Commentary Triggered.. " + phrase);
		bool canSpeak = checkCommentaryCooldown(cooldown);
		
		//No interrupting
		if(cooldown != 0){
			return;
		}
		//Are we allowed to repeat this phrase?
		//And if not, has it already been said?
		if(checkCommentaryOneTime(phrase) == false){
			//return;
		}
		if(canSpeak == true){
			Debug.Log("Loading Phrase: " + phrase);
			switch(phrase){
				case "Start":
					rand = Random.Range(0,commentaryLines.startClips.Length);
					phraseClip = commentaryLines.startClips[rand];
					break;
				case "GreenFlag":
					rand = Random.Range(0,commentaryLines.greenFlagClips.Length);
					phraseClip = commentaryLines.greenFlagClips[rand];
					break;
				case "RestartFront":
					rand = Random.Range(0,commentaryLines.restartFrontClips.Length);
					phraseClip = commentaryLines.restartFrontClips[rand];
					break;
				case "RestartMiddle":
					rand = Random.Range(0,commentaryLines.restartMiddleClips.Length);
					phraseClip = commentaryLines.restartMiddleClips[rand];
					break;
				case "RestartBack":
					rand = Random.Range(0,commentaryLines.restartBackClips.Length);
					phraseClip = commentaryLines.restartBackClips[rand];
					break;
				case "Wallhit":
					rand = Random.Range(0,commentaryLines.wallHitClips.Length);
					phraseClip = commentaryLines.wallHitClips[rand];
					break;
				case "Caution":
					rand = Random.Range(0,commentaryLines.cautionClips.Length);
					phraseClip = commentaryLines.cautionClips[rand];
					break;
				case "Crash":
					rand = Random.Range(0,commentaryLines.crashClips.Length);
					phraseClip = commentaryLines.crashClips[rand];
					break;
				case "BigCrash":
					rand = Random.Range(0,commentaryLines.bigCrashClips.Length);
					phraseClip = commentaryLines.bigCrashClips[rand];
					break;
				case "LaneChange":
					//Check either turn # or straight #
					rand = Random.Range(0,commentaryLines.moveHighClips.Length);
					phraseClip = commentaryLines.moveHighClips[rand];
					break;
				case "AtTheBack":
					rand = Random.Range(0,commentaryLines.atTheBackClips.Length);
					phraseClip = commentaryLines.atTheBackClips[rand];
					break;
				case "NewLeader":
					rand = Random.Range(0,commentaryLines.newLeaderClips.Length);
					phraseClip = commentaryLines.newLeaderClips[rand];
					break;
				case "BumpDraft":
					rand = Random.Range(0,commentaryLines.bumpDraftClips.Length);
					phraseClip = commentaryLines.bumpDraftClips[rand];
					break;
				case "DraftTrain":
					rand = Random.Range(0,commentaryLines.draftTrainClips.Length);
					phraseClip = commentaryLines.draftTrainClips[rand];
					break;
				case "NewSeason":
					rand = Random.Range(0,commentaryLines.newSeasonClips.Length);
					phraseClip = commentaryLines.newSeasonClips[rand];
					break;
				case "NoDraft":
					rand = Random.Range(0,commentaryLines.noDraftClips.Length);
					phraseClip = commentaryLines.noDraftClips[rand];
					break;
				case "LastLap":
					rand = Random.Range(0,commentaryLines.whiteFlagClips.Length);
					phraseClip = commentaryLines.whiteFlagClips[rand];
					break;
				case "LastLapLeader":
					rand = Random.Range(0,commentaryLines.whiteFlagLeaderClips.Length);
					phraseClip = commentaryLines.whiteFlagLeaderClips[rand];
					break;
				case "Stuck":
					rand = Random.Range(0,commentaryLines.stuckClips.Length);
					phraseClip = commentaryLines.stuckClips[rand];
					break;
				default:
					phraseClip = null;
					break;
			}
			cooldown = ((int)phraseClip.length * 60) + commsFreqGap;
			//Debug.Log("Cooldown: " + cooldown);
			if (!audioSource.isPlaying){
				if(phraseClip != null){
					//Debug.Log("Play Commentary" + phraseClip.name);
					audioSource.PlayOneShot(phraseClip);
				}
			}
		}
	}
	
	public static bool checkCommentaryOneTime(string phrase){
		bool oneTime = false;
		switch(phrase){
			case "Start":
			case "Restart":
			case "Wallhit":
			case "LaneChange":
				oneTime = false;
				break;
			case "AtTheBack":
			case "NewLeader":
			case "BumpDraft":
			case "DraftTrain":
			case "Crash":
			case "Caution":
			case "BigCrash":
			case "NewSeason":
			case "NoDraft":
			case "LastLap":
			case "Stuck":
				oneTime = true;
				break;
			default:
				oneTime = true;
				break;
		}
		return oneTime;
	}
	
	public static bool checkCommentaryCooldown(int cooldown){
		if(cooldown == 0){
			return true;
		} else {
			return false;
		}
	}
}