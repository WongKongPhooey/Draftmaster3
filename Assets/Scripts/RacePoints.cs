using UnityEngine;
using System.Collections;

public class RacePoints : MonoBehaviour {

	public static bool championshipMode;
	public static bool challengeMode;

	public static int[] placePoints = new int[43];
	
	// Use this for initialization
	void Start () {
		placePoints[0] = 185;
		placePoints[1] = 170;
		placePoints[2] = 165;
		placePoints[3] = 160;
		placePoints[4] = 155;
		placePoints[5] = 150;
		placePoints[6] = 146;
		placePoints[7] = 142;
		placePoints[8] = 138;
		placePoints[9] = 134;
		placePoints[10] = 130;
		placePoints[11] = 127;
		placePoints[12] = 124;
		placePoints[13] = 121;
		placePoints[14] = 118;
		placePoints[15] = 115;
		placePoints[16] = 112;
		placePoints[17] = 109;
		placePoints[18] = 106;
		placePoints[19] = 103;
		placePoints[20] = 100;
		placePoints[21] = 97;
		placePoints[22] = 94;
		placePoints[23] = 91;
		placePoints[24] = 88;
		placePoints[25] = 85;
		placePoints[26] = 82;
		placePoints[27] = 79;
		placePoints[28] = 76;
		placePoints[29] = 73;
		placePoints[30] = 70;
		placePoints[31] = 67;
		placePoints[32] = 64;
		placePoints[33] = 61;
		placePoints[34] = 58;
		placePoints[35] = 55;
		placePoints[36] = 52;
		placePoints[37] = 49;
		placePoints[38] = 46;
		placePoints[39] = 43;
		placePoints[40] = 40;
		placePoints[41] = 37;
		placePoints[42] = 34;
	}
}
