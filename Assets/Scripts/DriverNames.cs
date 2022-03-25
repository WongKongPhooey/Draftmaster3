using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DriverNames : MonoBehaviour {

	public static ArrayList series = new ArrayList();

	public static string[] names = new string[101];
	public static string[] teams = new string[101];
	public static string[] manufacturer = new string[101];
	public static int[] rarity = new int[101];
	public static string[] types = new string[101];

	public static Dictionary<string, string[]> allNames = new Dictionary<string, string[]>();
	public static Dictionary<string, string[]> allTeams = new Dictionary<string, string[]>();
	public static Dictionary<string, string[]> allManufacturer = new Dictionary<string, string[]>();
	public static Dictionary<string, int[]> allRarity = new Dictionary<string, int[]>();
	public static Dictionary<string, string[]> allTypes = new Dictionary<string, string[]>();
	public static Dictionary<string, string> allCarsetNames = new Dictionary<string, string>();

	public static string[] cup2020Names = new string[101];
	public static string[] cup2020Teams = new string[101];
	public static string[] cup2020Manufacturer = new string[101];
	public static int[] cup2020Rarity = new int[101];
	public static string[] cup2020Types = new string[101];
	
	public static string[] cup2022Names = new string[101];
	public static string[] cup2022Teams = new string[101];
	public static string[] cup2022Manufacturer = new string[101];
	public static int[] cup2022Rarity = new int[101];
	public static string[] cup2022Types = new string[101];
	
	public static string[] legendsNames = new string[10];
	public static string[] legendsLiveries = new string[10];
	
	public static string[] cup2020AltNames = new string[20];

	// Use this for initialization
	void Start () {
		
		cup20();
		cup22();
		carsetNames();
		
		series.Clear();
		series.Add("cup20");
		series.Add("cup22");
		
		if(allNames.ContainsKey("cup20") == false){
			allNames.Add("cup20", cup2020Names);
			allTeams.Add("cup20", cup2020Teams);
			allManufacturer.Add("cup20", cup2020Manufacturer);
			allRarity.Add("cup20", cup2020Rarity);
			allTypes.Add("cup20", cup2020Types);
			
			allNames.Add("cup22", cup2022Names);
			allTeams.Add("cup22", cup2022Teams);
			allManufacturer.Add("cup22", cup2022Manufacturer);
			allRarity.Add("cup22", cup2022Rarity);
			allTypes.Add("cup22", cup2022Types);
		}
	}
	
	public static string getName(string seriesPrefix, int index){
		string[] names = allNames[seriesPrefix];
		return names[index];
	}
	
	public static string getTeam(string seriesPrefix, int index){
		string[] teams = allTeams[seriesPrefix];
		return teams[index];
	}
	
	public static string getManufacturer(string seriesPrefix, int index){
		string[] manufacturer = allManufacturer[seriesPrefix];
		return manufacturer[index];
	}
	
	public static int getRarity(string seriesPrefix, int index){
		int[] rarities = allRarity[seriesPrefix];
		return rarities[index];
	}
	
	public static string getType(string seriesPrefix, int index){
		string[] types = allTypes[seriesPrefix];
		return types[index];
	}
	
	public static int getFieldSize(string seriesPrefix){
		string[] nameSet = allNames[seriesPrefix];
		return nameSet.Length;
	}
	
	public static string getSeriesNiceName(string seriesPrefix){
		return allCarsetNames[seriesPrefix];
	}
	
	public static string getRandomSeries(){
		return series[Mathf.FloorToInt(Random.Range(0,series.Count))].ToString();
	}
	
	public static void carsetNames(){
		if(allNames.ContainsKey("cup20") == false){
			allCarsetNames.Add("cup20", "Cup '20");
			allCarsetNames.Add("cup22", "Cup '22");
		}
	}
	
	public static void cup20(){
		cup2020Names[0] = "Houff";
		cup2020Names[1] = "Ku. Busch";
		cup2020Names[2] = "Keselowski";
		cup2020Names[3] = "A. Dillon";
		cup2020Names[4] = "Harvick";
		cup2020Names[5] = "Larson";
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
		cup2020Names[74] = "Sorenson";
		cup2020Names[77] = "Chastain";
		cup2020Names[78] = "Smithley";
		cup2020Names[88] = "Bowman";
		cup2020Names[95] = "Bell";
		cup2020Names[96] = "Suarez";
		
		cup2020AltNames[0] = "Barrett";
		cup2020AltNames[1] = "Grala";
		cup2020AltNames[2] = "Ware";
		cup2020AltNames[3] = "Starr";
		cup2020AltNames[4] = "Allgaier";
		cup2020AltNames[5] = "Currey";
		cup2020AltNames[6] = "Hemric";
		cup2020AltNames[7] = "McMurray";
		cup2020AltNames[8] = "Mears";
		cup2020AltNames[9] = "Allmendinger";
		
		cup2020Teams[0] = "IND";
		cup2020Teams[1] = "CGR";
		cup2020Teams[2] = "PEN";
		cup2020Teams[3] = "RCR";
		cup2020Teams[4] = "SHR";
		cup2020Teams[5] = "CGR";
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
		cup2020Teams[36] = "FRM";
		cup2020Teams[37] = "JTG";
		cup2020Teams[38] = "FRM";
		cup2020Teams[41] = "SHR";
		cup2020Teams[42] = "CGR";
		cup2020Teams[43] = "IND";
		cup2020Teams[47] = "JTG";
		cup2020Teams[48] = "HEN";
		cup2020Teams[49] = "IND";
		cup2020Teams[51] = "RWR";
		cup2020Teams[52] = "RWR";
		cup2020Teams[53] = "RWR";
		cup2020Teams[54] = "RWR";
		cup2020Teams[62] = "IND";
		cup2020Teams[66] = "IND";
		cup2020Teams[74] = "IND";
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
		cup2020Manufacturer[5] = "CHV";
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
		cup2020Manufacturer[36] = "FRD";
		cup2020Manufacturer[37] = "CHV";
		cup2020Manufacturer[38] = "FRD";
		cup2020Manufacturer[41] = "FRD";
		cup2020Manufacturer[42] = "CHV";
		cup2020Manufacturer[43] = "CHV";
		cup2020Manufacturer[47] = "CHV";
		cup2020Manufacturer[48] = "CHV";
		cup2020Manufacturer[49] = "TYT";
		cup2020Manufacturer[51] = "FRD";
		cup2020Manufacturer[52] = "FRD";
		cup2020Manufacturer[53] = "FRD";
		cup2020Manufacturer[54] = "FRD";
		cup2020Manufacturer[62] = "CHV";
		cup2020Manufacturer[66] = "TYT";
		cup2020Manufacturer[74] = "CHV";
		cup2020Manufacturer[77] = "CHV";
		cup2020Manufacturer[78] = "CHV";
		cup2020Manufacturer[88] = "CHV";
		cup2020Manufacturer[95] = "TYT";
		cup2020Manufacturer[96] = "TYT";
		
		cup2020Types[0] = "Rookie";
		cup2020Types[1] = "Intimidator";
		cup2020Types[2] = "Intimidator";
		cup2020Types[3] = "Strategist";
		cup2020Types[4] = "Dominator";
		cup2020Types[5] = "Closer";
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
		cup2020Types[16] = "Rookie";
		cup2020Types[17] = "Strategist";
		cup2020Types[18] = "Intimidator";
		cup2020Types[19] = "Dominator";
		cup2020Types[20] = "Closer";
		cup2020Types[21] = "Closer";
		cup2020Types[22] = "Intimidator";
		cup2020Types[24] = "Closer";
		cup2020Types[27] = "Strategist";
		cup2020Types[32] = "Strategist";
		cup2020Types[34] = "Intimidator";
		cup2020Types[36] = "Closer";
		cup2020Types[37] = "Rookie";
		cup2020Types[38] = "Rookie";
		cup2020Types[41] = "Rookie";
		cup2020Types[42] = "Strategist";
		cup2020Types[43] = "Strategist";
		cup2020Types[47] = "Intimidator";
		cup2020Types[48] = "Legend";
		cup2020Types[49] = "Rookie";
		cup2020Types[51] = "Strategist";
		cup2020Types[52] = "Closer";
		cup2020Types[53] = "Strategist";
		cup2020Types[54] = "Closer";
		cup2020Types[62] = "Intimidator";
		cup2020Types[66] = "Closer";
		cup2020Types[74] = "Strategist";
		cup2020Types[77] = "Intimidator";
		cup2020Types[78] = "Closer";
		cup2020Types[88] = "Strategist";
		cup2020Types[95] = "Rookie";
		cup2020Types[96] = "Closer";
		
		cup2020Rarity[0] = 1;
		cup2020Rarity[1] = 2;
		cup2020Rarity[2] = 3;
		cup2020Rarity[3] = 2;
		cup2020Rarity[4] = 3;
		cup2020Rarity[5] = 3;
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
		cup2020Rarity[36] = 2;
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
		cup2020Rarity[54] = 1;
		cup2020Rarity[62] = 1;
		cup2020Rarity[66] = 1;
		cup2020Rarity[74] = 1;
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
	
	public static void cup22(){
		cup2022Names[1] = "Chastain";
		cup2022Names[2] = "Cindric";
		cup2022Names[3] = "A. Dillon";
		cup2022Names[4] = "Harvick";
		cup2022Names[5] = "Larson";
		cup2022Names[6] = "Keselowski";
		cup2022Names[7] = "LaJoie";
		cup2022Names[8] = "Reddick";
		cup2022Names[9] = "Elliott";
		cup2022Names[10] = "Almirola";
		cup2022Names[11] = "Hamlin";
		cup2022Names[12] = "Blaney";
		cup2022Names[14] = "Briscoe";
		cup2022Names[15] = "Davison";
		cup2022Names[16] = "Allmendinger";
		cup2022Names[17] = "Buescher";
		cup2022Names[18] = "Ky. Busch";
		cup2022Names[19] = "Truex Jr";
		cup2022Names[20] = "Bell";
		cup2022Names[21] = "Burton";
		cup2022Names[22] = "Logano";
		cup2022Names[23] = "Wallace";
		cup2022Names[24] = "Byron";
		cup2022Names[27] = "Villeneuve";
		cup2022Names[31] = "Haley";
		cup2022Names[34] = "McDowell";
		cup2022Names[38] = "Gilliland";
		cup2022Names[41] = "Custer";
		cup2022Names[42] = "T. Dillon";
		cup2022Names[43] = "Jones";
		cup2022Names[44] = "Biffle";
		cup2022Names[45] = "Ku. Busch";
		cup2022Names[47] = "Stenhouse Jr.";
		cup2022Names[48] = "Bowman";
		cup2022Names[50] = "Grala";
		cup2022Names[51] = "Ware";
		cup2022Names[52] = "Bilicki";
		cup2022Names[53] = "Smithley";
		cup2022Names[55] = "Yeley";
		cup2022Names[62] = "Gragson";
		cup2022Names[66] = "Hill";
		cup2022Names[77] = "Cassill";
		cup2022Names[78] = "McLeod";
		cup2022Names[99] = "Suarez";
		
		cup2022Teams[1] = "TRK";
		cup2022Teams[2] = "PEN";
		cup2022Teams[3] = "RCR";
		cup2022Teams[4] = "SHR";
		cup2022Teams[5] = "HEN";
		cup2022Teams[6] = "RFK";
		cup2022Teams[7] = "SPI";
		cup2022Teams[8] = "RCR";
		cup2022Teams[9] = "HEN";
		cup2022Teams[10] = "SHR";
		cup2022Teams[11] = "JGR";
		cup2022Teams[12] = "PEN";
		cup2022Teams[14] = "SHR";
		cup2022Teams[15] = "RWR";
		cup2022Teams[16] = "KAU";
		cup2022Teams[17] = "RFK";
		cup2022Teams[18] = "JGR";
		cup2022Teams[19] = "JGR";
		cup2022Teams[20] = "JGR";
		cup2022Teams[21] = "IND";
		cup2022Teams[22] = "PEN";
		cup2022Teams[23] = "23X";
		cup2022Teams[24] = "HEN";
		cup2022Teams[27] = "IND";
		cup2022Teams[31] = "KAU";
		cup2022Teams[34] = "FRM";
		cup2022Teams[38] = "FRM";
		cup2022Teams[41] = "SHR";
		cup2022Teams[42] = "GMS";
		cup2022Teams[43] = "GMS";
		cup2022Teams[44] = "IND";
		cup2022Teams[45] = "23X";
		cup2022Teams[47] = "IND";
		cup2022Teams[48] = "HEN";
		cup2022Teams[50] = "IND";
		cup2022Teams[51] = "RWR";
		cup2022Teams[52] = "RWR";
		cup2022Teams[53] = "RWR";
		cup2022Teams[55] = "IND";
		cup2022Teams[62] = "IND";
		cup2022Teams[66] = "IND";
		cup2022Teams[77] = "SPI";
		cup2022Teams[78] = "IND";
		cup2022Teams[99] = "TRK";
		
		cup2022Manufacturer[1] = "CHV";
		cup2022Manufacturer[2] = "FRD";
		cup2022Manufacturer[3] = "CHV";
		cup2022Manufacturer[4] = "FRD";
		cup2022Manufacturer[5] = "CHV";
		cup2022Manufacturer[6] = "FRD";
		cup2022Manufacturer[7] = "CHV";
		cup2022Manufacturer[8] = "CHV";
		cup2022Manufacturer[9] = "CHV";
		cup2022Manufacturer[10] = "FRD";
		cup2022Manufacturer[11] = "TYT";
		cup2022Manufacturer[12] = "FRD";
		cup2022Manufacturer[14] = "FRD";
		cup2022Manufacturer[15] = "FRD";
		cup2022Manufacturer[16] = "CHV";
		cup2022Manufacturer[17] = "FRD";
		cup2022Manufacturer[18] = "TYT";
		cup2022Manufacturer[19] = "TYT";
		cup2022Manufacturer[20] = "TYT";
		cup2022Manufacturer[21] = "FRD";
		cup2022Manufacturer[22] = "FRD";
		cup2022Manufacturer[23] = "TYT";
		cup2022Manufacturer[24] = "CHV";
		cup2022Manufacturer[27] = "FRD";
		cup2022Manufacturer[31] = "CHV";
		cup2022Manufacturer[34] = "FRD";
		cup2022Manufacturer[38] = "FRD";
		cup2022Manufacturer[41] = "FRD";
		cup2022Manufacturer[42] = "CHV";
		cup2022Manufacturer[43] = "CHV";
		cup2022Manufacturer[44] = "CHV";
		cup2022Manufacturer[45] = "TYT";
		cup2022Manufacturer[47] = "CHV";
		cup2022Manufacturer[48] = "CHV";
		cup2022Manufacturer[50] = "CHV";
		cup2022Manufacturer[51] = "FRD";
		cup2022Manufacturer[52] = "FRD";
		cup2022Manufacturer[53] = "FRD";
		cup2022Manufacturer[55] = "FRD";
		cup2022Manufacturer[62] = "CHV";
		cup2022Manufacturer[66] = "TYT";
		cup2022Manufacturer[77] = "CHV";
		cup2022Manufacturer[78] = "FRD";
		cup2022Manufacturer[99] = "CHV";
		
		cup2022Types[1] = "Strategist";
		cup2022Types[2] = "Rookie";
		cup2022Types[3] = "Strategist";
		cup2022Types[4] = "Dominator";
		cup2022Types[5] = "Dominator";
		cup2022Types[6] = "Intimidator";
		cup2022Types[7] = "Strategist";
		cup2022Types[8] = "Closer";
		cup2022Types[9] = "Closer";
		cup2022Types[10] = "Strategist";
		cup2022Types[11] = "Closer";
		cup2022Types[12] = "Strategist";
		cup2022Types[13] = "Closer";
		cup2022Types[14] = "Intimidator";
		cup2022Types[15] = "Blocker";
		cup2022Types[16] = "Rookie";
		cup2022Types[17] = "Blocker";
		cup2022Types[18] = "Intimidator";
		cup2022Types[19] = "Dominator";
		cup2022Types[20] = "Closer";
		cup2022Types[21] = "Rookie";
		cup2022Types[22] = "Intimidator";
		cup2022Types[23] = "Strategist";
		cup2022Types[24] = "Closer";
		cup2022Types[27] = "Intimidator";
		cup2022Types[31] = "Strategist";
		cup2022Types[34] = "Blocker";
		cup2022Types[38] = "Rookie";
		cup2022Types[41] = "Closer";
		cup2022Types[42] = "Blocker";
		cup2022Types[43] = "Strategist";
		cup2022Types[44] = "Dominator";
		cup2022Types[45] = "Intimidator";
		cup2022Types[47] = "Intimidator";
		cup2022Types[48] = "Closer";
		cup2022Types[50] = "Rookie";
		cup2022Types[51] = "Blocker";
		cup2022Types[52] = "Closer";
		cup2022Types[53] = "Closer";
		cup2022Types[55] = "Blocker";
		cup2022Types[62] = "Intimidator";
		cup2022Types[66] = "Closer";
		cup2022Types[77] = "Strategist";
		cup2022Types[78] = "Blocker";
		cup2022Types[99] = "Closer";
		
		cup2022Rarity[1] = 1;
		cup2022Rarity[2] = 2;
		cup2022Rarity[3] = 2;
		cup2022Rarity[4] = 4;
		cup2022Rarity[5] = 4;
		cup2022Rarity[6] = 4;
		cup2022Rarity[7] = 1;
		cup2022Rarity[8] = 1;
		cup2022Rarity[9] = 4;
		cup2022Rarity[10] = 2;
		cup2022Rarity[11] = 4;
		cup2022Rarity[12] = 3;
		cup2022Rarity[14] = 2;
		cup2022Rarity[15] = 1;
		cup2022Rarity[16] = 1;
		cup2022Rarity[17] = 1;
		cup2022Rarity[18] = 4;
		cup2022Rarity[19] = 3;
		cup2022Rarity[20] = 2;
		cup2022Rarity[21] = 1;
		cup2022Rarity[22] = 3;
		cup2022Rarity[23] = 2;
		cup2022Rarity[24] = 2;
		cup2022Rarity[27] = 1;
		cup2022Rarity[31] = 1;
		cup2022Rarity[34] = 2;
		cup2022Rarity[38] = 1;
		cup2022Rarity[41] = 2;
		cup2022Rarity[42] = 1;
		cup2022Rarity[43] = 2;
		cup2022Rarity[44] = 2;
		cup2022Rarity[45] = 3;
		cup2022Rarity[47] = 2;
		cup2022Rarity[48] = 3;
		cup2022Rarity[50] = 1;
		cup2022Rarity[51] = 1;
		cup2022Rarity[52] = 1;
		cup2022Rarity[53] = 1;
		cup2022Rarity[55] = 1;
		cup2022Rarity[62] = 1;
		cup2022Rarity[66] = 1;
		cup2022Rarity[77] = 1;
		cup2022Rarity[78] = 1;
		cup2022Rarity[99] = 1;
		
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
			case "Dominator":
				type="Domin.";
				break;
			case "Intimidator":
				type="Intim.";
				break;
			case "Strategist":
				type="Strat.";
				break;
			case "Closer":
				type="Close.";
				break;
			case "Blocker":
				type="Block.";
				break;
			case "Legend":
				type="Legen.";
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
