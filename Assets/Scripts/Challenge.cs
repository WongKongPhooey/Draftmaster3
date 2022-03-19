using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Challenge", menuName = "Challenge")] 
public class Challenge : ScriptableObject {
	
	public string challengeCustomText;
	public string challengeType;
	public int challengeValue;
	public string challengeCircuit;
	public int challengeSeries;
	public int challengeSubseries;
	public string challengeCarset;
	public int challengeCar;
	public string challengeAlt;
	public int challengeAILevel;
	public int challengeCustomValue;
	
	public string challengeRewardType;
	public int challengeRewardInt;
	public string challengeRewardString;
}
