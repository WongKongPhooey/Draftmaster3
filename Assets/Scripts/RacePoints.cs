using UnityEngine;
using System.Collections;

public class RacePoints : MonoBehaviour {

	public static bool championshipMode;
	public static bool challengeMode;

	public static int[] placePoints = new int[43];
	
	// Use this for initialization
	void Start(){
		setCupPoints();
	}
	
	static void setCupPoints(){
		placePoints[0] = 40;
		placePoints[1] = 35;
		placePoints[2] = 34;
		placePoints[3] = 33;
		placePoints[4] = 32;
		placePoints[5] = 31;
		placePoints[6] = 30;
		placePoints[7] = 29;
		placePoints[8] = 28;
		placePoints[9] = 27;
		placePoints[10] = 26;
		placePoints[11] = 25;
		placePoints[12] = 24;
		placePoints[13] = 23;
		placePoints[14] = 22;
		placePoints[15] = 21;
		placePoints[16] = 20;
		placePoints[17] = 19;
		placePoints[18] = 18;
		placePoints[19] = 17;
		placePoints[20] = 16;
		placePoints[21] = 15;
		placePoints[22] = 14;
		placePoints[23] = 13;
		placePoints[24] = 12;
		placePoints[25] = 11;
		placePoints[26] = 10;
		placePoints[27] = 9;
		placePoints[28] = 8;
		placePoints[29] = 7;
		placePoints[30] = 6;
		placePoints[31] = 5;
		placePoints[32] = 4;
		placePoints[33] = 3;
		placePoints[34] = 2;
		placePoints[35] = 1;
		placePoints[36] = 1;
		placePoints[37] = 1;
		placePoints[38] = 1;
		placePoints[39] = 1;
		placePoints[40] = 0;
		placePoints[41] = 0;
		placePoints[42] = 0;
	}
	
	static void setLegacyPoints(){
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
