using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random=UnityEngine.Random;

[CreateAssetMenu(fileName = "Data", menuName = "Commentary/Lines", order = 1)]
public class CommentaryLines : ScriptableObject {	

	public string commentator;
	public AudioClip[] startClips, wallHitClips, cautionClips, whiteFlagClips, moveLowClips, moveHighClips;
}
