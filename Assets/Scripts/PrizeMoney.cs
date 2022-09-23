using UnityEngine;
using System.Collections;

public class PrizeMoney : MonoBehaviour {

	public static int[] cashAmount = new int[43];
	
	// Use this for initialization
	void Awake () {
		setPrizeMoney();
	}
	
	public static void setPrizeMoney(){
		cashAmount[0] = 10000;
		cashAmount[1] = 7500;
		cashAmount[2] = 6000;
		cashAmount[3] = 4750;
		cashAmount[4] = 4250;
		cashAmount[5] = 4000;
		cashAmount[6] = 3800;
		cashAmount[7] = 3600;
		cashAmount[8] = 3500;
		cashAmount[9] = 3400;
		cashAmount[10] = 3300;
		cashAmount[11] = 3200;
		cashAmount[12] = 3100;
		cashAmount[13] = 3000;
		cashAmount[14] = 2900;
		cashAmount[15] = 2800;
		cashAmount[16] = 2700;
		cashAmount[17] = 2600;
		cashAmount[18] = 2500;
		cashAmount[19] = 2400;
		cashAmount[20] = 2300;
		cashAmount[21] = 2200;
		cashAmount[22] = 2100;
		cashAmount[23] = 2000;
		cashAmount[24] = 1900;
		cashAmount[25] = 1800;
		cashAmount[26] = 1700;
		cashAmount[27] = 1600;
		cashAmount[28] = 1500;
		cashAmount[29] = 1400;
		cashAmount[30] = 1300;
		cashAmount[31] = 1200;
		cashAmount[32] = 1100;
		cashAmount[33] = 1000;
		cashAmount[34] = 900;
		cashAmount[35] = 800;
		cashAmount[36] = 700;
		cashAmount[37] = 600;
		cashAmount[38] = 500;
		cashAmount[39] = 400;
		cashAmount[40] = 300;
		cashAmount[41] = 200;
		cashAmount[42] = 100;
	}
	
	public static int getPrizeMoney(int position){
		setPrizeMoney();
		if((position > 0)&&(position < 43)){
			return cashAmount[position];
		} else {
			return cashAmount[42];
		}
	}
}
