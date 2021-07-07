using UnityEngine;
using System.Collections;

public class DriverNames : MonoBehaviour {

	public static string[] cup2020Names = new string[101];
	public static string[] cup2020Teams = new string[101];
	public static string[] cup2020Manufacturer = new string[101];
	public static int[] cup2020Rarity = new int[101];
	public static string[] cup2020Types = new string[101];
	
	public static string[] legendsNames = new string[10];
	public static string[] legendsLiveries = new string[10];
	
	public static string[] cup2020AltNames = new string[20];

	// Use this for initialization
	void Start () {
		
		cup2020Names[0] = "Houff";
		cup2020Names[1] = "Ku. Busch";
		cup2020Names[2] = "Keselowski";
		cup2020Names[3] = "A. Dillon";
		cup2020Names[4] = "Harvick";
		cup2020Names[6] = "Newman";
		cup2020Names[7] = "Bilicki";
		cup2020Names[8] = "Reddick";
		cup2020Names[9] = "Elliott";
		cup2020Names[10] = "Almirola";
		cup2020Names[11] = "Hamlin";
		cup2020Names[12] = "Blaney";
		cup2020Names[13] = "T. Dillon";
		cup2020Names[14] = "Bowyer";
		cup2020Names[15] = "Poole";
		cup2020Names[16] = "Haley";
		cup2020Names[17] = "Buescher";
		cup2020Names[18] = "Ky. Busch";
		cup2020Names[19] = "Truex Jr";
		cup2020Names[20] = "Jones";
		cup2020Names[21] = "Dibenedetto";
		cup2020Names[22] = "Logano";
		cup2020Names[24] = "Byron";
		cup2020Names[27] = "Gaulding";
		cup2020Names[32] = "Lajoie";
		cup2020Names[34] = "McDowell";
		cup2020Names[36] = "Ragan";
		cup2020Names[37] = "Preece";
		cup2020Names[38] = "Nemechek";
		cup2020Names[41] = "Custer";
		cup2020Names[42] = "Kenseth";
		cup2020Names[43] = "Wallace";
		cup2020Names[47] = "Stenhouse Jr.";
		cup2020Names[48] = "Johnson";
		cup2020Names[49] = "Finchum";
		cup2020Names[51] = "Gase";
		cup2020Names[52] = "Mcleod";
		cup2020Names[53] = "Davison";
		cup2020Names[54] = "Yeley";
		cup2020Names[62] = "Gaughan";
		cup2020Names[66] = "Hill";
		cup2020Names[74] = "Sorenson"
		cup2020Names[77] = "Chastain";
		cup2020Names[78] = "Smithley";
		cup2020Names[88] = "Bowman";
		cup2020Names[95] = "Bell";
		cup2020Names[96] = "Suarez";
		
		cup2020AltNames[0] = "Larson";
		cup2020AltNames[1] = "Barrett";
		cup2020AltNames[2] = "Grala";
		cup2020AltNames[3] = "Ware";
		cup2020AltNames[4] = "Starr";
		cup2020AltNames[5] = "Allgaier";
		cup2020AltNames[6] = "Currey";
		
		cup2020Teams[0] = "IND";
		cup2020Teams[1] = "CGR";
		cup2020Teams[2] = "PEN";
		cup2020Teams[3] = "RCR";
		cup2020Teams[4] = "SHR";
		cup2020Teams[6] = "RFR";
		cup2020Teams[7] = "IND";
		cup2020Teams[8] = "RCR";
		cup2020Teams[9] = "HEN";
		cup2020Teams[10] = "SHR";
		cup2020Teams[11] = "JGR";
		cup2020Teams[12] = "PEN";
		cup2020Teams[13] = "IND";
		cup2020Teams[14] = "SHR";
		cup2020Teams[15] = "IND";
		cup2020Teams[16] = "IND";
		cup2020Teams[17] = "RFR";
		cup2020Teams[18] = "JGR";
		cup2020Teams[19] = "JGR";
		cup2020Teams[20] = "JGR";
		cup2020Teams[21] = "IND";
		cup2020Teams[22] = "PEN";
		cup2020Teams[24] = "HEN";
		cup2020Teams[27] = "RWR";
		cup2020Teams[32] = "IND";
		cup2020Teams[34] = "FRM";
		cup2020Teams[37] = "JTG";
		cup2020Teams[38] = "FRM";
		cup2020Teams[41] = "SHR";
		cup2020Teams[42] = "CGR";
		cup2020Teams[43] = "IND";
		cup2020Teams[47] = "JTG";
		cup2020Teams[48] = "HEN";
		cup2020Teams[49] = "IND";
		cup2020Teams[51] = "IND";
		cup2020Teams[53] = "RWR";
		cup2020Teams[54] = "IND";
		cup2020Teams[62] = "IND";
		cup2020Teams[66] = "IND";
		cup2020Teams[77] = "IND";
		cup2020Teams[78] = "IND";
		cup2020Teams[88] = "HEN";
		cup2020Teams[95] = "IND";
		cup2020Teams[96] = "IND";
		
		cup2020Manufacturer[0] = "CHV";
		cup2020Manufacturer[1] = "CHV";
		cup2020Manufacturer[2] = "FRD";
		cup2020Manufacturer[3] = "CHV";
		cup2020Manufacturer[4] = "FRD";
		cup2020Manufacturer[6] = "FRD";
		cup2020Manufacturer[7] = "CHV";
		cup2020Manufacturer[8] = "CHV";
		cup2020Manufacturer[9] = "CHV";
		cup2020Manufacturer[10] = "FRD";
		cup2020Manufacturer[11] = "TYT";
		cup2020Manufacturer[12] = "FRD";
		cup2020Manufacturer[13] = "CHV";
		cup2020Manufacturer[14] = "FRD";
		cup2020Manufacturer[15] = "CHV";
		cup2020Manufacturer[16] = "CHV";
		cup2020Manufacturer[17] = "FRD";
		cup2020Manufacturer[18] = "TYT";
		cup2020Manufacturer[19] = "TYT";
		cup2020Manufacturer[20] = "TYT";
		cup2020Manufacturer[21] = "FRD";
		cup2020Manufacturer[22] = "FRD";
		cup2020Manufacturer[24] = "CHV";
		cup2020Manufacturer[27] = "FRD";
		cup2020Manufacturer[32] = "FRD";
		cup2020Manufacturer[34] = "FRD";
		cup2020Manufacturer[37] = "CHV";
		cup2020Manufacturer[38] = "FRD";
		cup2020Manufacturer[41] = "FRD";
		cup2020Manufacturer[42] = "CHV";
		cup2020Manufacturer[43] = "CHV";
		cup2020Manufacturer[47] = "CHV";
		cup2020Manufacturer[48] = "CHV";
		cup2020Manufacturer[49] = "TYT";
		cup2020Manufacturer[51] = "FRD";
		cup2020Manufacturer[52] = "CHV";
		cup2020Manufacturer[53] = "FRD";
		cup2020Manufacturer[62] = "CHV";
		cup2020Manufacturer[66] = "FRD";
		cup2020Manufacturer[77] = "CHV";
		cup2020Manufacturer[78] = "CHV";
		cup2020Manufacturer[88] = "CHV";
		cup2020Manufacturer[95] = "TYT";
		cup2020Manufacturer[96] = "TYT";
		
		cup2020Types[0] = "Rookie";
		cup2020Types[1] = "Intimidator";
		cup2020Types[2] = "Intimidator";
		cup2020Types[3] = "Strategist";
		cup2020Types[4] = "Gentleman";
		cup2020Types[6] = "Closer";
		cup2020Types[7] = "Strategist";
		cup2020Types[8] = "Rookie";
		cup2020Types[9] = "Closer";
		cup2020Types[10] = "Strategist";
		cup2020Types[11] = "Closer";
		cup2020Types[12] = "Strategist";
		cup2020Types[13] = "Closer";
		cup2020Types[14] = "Intimidator";
		cup2020Types[15] = "Rookie";
		cup2020Types[16] = "Strategist";
		cup2020Types[17] = "Gentleman";
		cup2020Types[18] = "Intimidator";
		cup2020Types[19] = "Gentleman";
		cup2020Types[20] = "Closer";
		cup2020Types[21] = "Closer";
		cup2020Types[22] = "Intimidator";
		cup2020Types[24] = "Closer";
		cup2020Types[27] = "Strategist";
		cup2020Types[38] = "Rookie";
		cup2020Types[41] = "Rookie";
		cup2020Types[48] = "Gentleman";
		cup2020Types[95] = "Rookie";
		
		cup2020Rarity[0] = 1;
		cup2020Rarity[1] = 2;
		cup2020Rarity[2] = 3;
		cup2020Rarity[3] = 2;
		cup2020Rarity[4] = 3;
		cup2020Rarity[6] = 2;
		cup2020Rarity[7] = 1;
		cup2020Rarity[8] = 1;
		cup2020Rarity[9] = 3;
		cup2020Rarity[10] = 2;
		cup2020Rarity[11] = 3;
		cup2020Rarity[12] = 2;
		cup2020Rarity[13] = 1;
		cup2020Rarity[14] = 2;
		cup2020Rarity[15] = 1;
		cup2020Rarity[16] = 1;
		cup2020Rarity[17] = 1;
		cup2020Rarity[18] = 3;
		cup2020Rarity[19] = 3;
		cup2020Rarity[20] = 1;
		cup2020Rarity[21] = 1;
		cup2020Rarity[22] = 3;
		cup2020Rarity[24] = 2;
		cup2020Rarity[27] = 1;
		cup2020Rarity[32] = 1;
		cup2020Rarity[34] = 1;
		cup2020Rarity[37] = 1;
		cup2020Rarity[38] = 1;
		cup2020Rarity[41] = 1;
		cup2020Rarity[42] = 2;
		cup2020Rarity[43] = 2;
		cup2020Rarity[47] = 1;
		cup2020Rarity[48] = 3;
		cup2020Rarity[49] = 1;
		cup2020Rarity[51] = 1;
		cup2020Rarity[52] = 1;
		cup2020Rarity[53] = 1;
		cup2020Rarity[62] = 1;
		cup2020Rarity[66] = 1;
		cup2020Rarity[77] = 1;
		cup2020Rarity[78] = 1;
		cup2020Rarity[88] = 2;
		cup2020Rarity[95] = 1;
		cup2020Rarity[96] = 1;
		
		legendsNames[0] = "Petty";
		legendsNames[1] = "Earnhardt";
		legendsNames[2] = "Johnson";
		legendsNames[3] = "Yarborough";
		
		legendsLiveries[0] = "20livery2";
		legendsLiveries[1] = "20livery0";
		legendsLiveries[2] = "20livery1";
		legendsLiveries[3] = "76livery2";
	}
	
	public static string shortenedType(string type){
		switch(type){
			case "Intimidator":
				type="Intim.";
				break;
			case "Strategist":
				type="Strat.";
				break;
			case "Closer":
				type="Close.";
				break;
			case "Gentleman":
				type="Gent.";
				break;
			case "Rookie":
				type="Rook.";
				break;
			default:
				type="";
				break;
		}
	return type;
	}
}
