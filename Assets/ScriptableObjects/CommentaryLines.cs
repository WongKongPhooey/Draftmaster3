using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random=UnityEngine.Random;

[CreateAssetMenu(fileName = "Data", menuName = "Commentary/Lines", order = 1)]
public class CommentaryLines : ScriptableObject {	

	public string commentator;
	public AudioClip[] startClips, greenFlagClips, restartFrontClips, restartMiddleClips, restartBackClips, wallHitClips, crashClips, bigCrashClips, cautionClips, cautionTAClips, cautionTBClips, cautionTCClips, cautionTDClips, cautionSAClips, cautionSBClips, whiteFlagClips, whiteFlagLeaderClips, moveLowClips, moveMiddleClips, moveHighClips, atTheBackClips, newLeaderClips, bumpDraftClips, draftTrainClips, newSeasonClips, seasonFinaleClips, noDraftClips, stuckClips;
}
