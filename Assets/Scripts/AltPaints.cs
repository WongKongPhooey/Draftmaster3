using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AltPaints : MonoBehaviour {
	
	public static string[,] cup2020AltPaintNames = new string[101,10];
	public static string[,] cup22AltPaintNames = new string[101,10];
	public static string[,] cup23AltPaintNames = new string[101,10];
	public static string[,] cup24AltPaintNames = new string[101,10];
	public static string[,] irl23AltPaintNames = new string[101,10];
	public static string[,] irl24AltPaintNames = new string[101,10];
	
	public static string[,] cup2020AltPaintDriver = new string[101,10];
	public static string[,] cup22AltPaintDriver = new string[101,10];
	public static string[,] cup23AltPaintDriver = new string[101,10];
	public static string[,] cup24AltPaintDriver = new string[101,10];
	public static string[,] irl23AltPaintDriver = new string[101,10];
	public static string[,] irl24AltPaintDriver = new string[101,10];
	
	public static int[,] cup2020AltPaintRarity = new int[101,10];
	
	public static string[,] cup2020AltPaintTheme = new string[101,10];
	public static string[,] cup22AltPaintTheme = new string[101,10];
	public static string[,] cup23AltPaintTheme = new string[101,10];
	public static string[,] cup24AltPaintTheme = new string[101,10];
	public static string[,] irl23AltPaintTheme = new string[101,10];
	public static string[,] irl24AltPaintTheme = new string[101,10];
	
	public static bool[,] cup2020AltCanBuy = new bool[101,10];
	public static bool[,] cup22AltCanBuy = new bool[101,10];
	public static bool[,] cup23AltCanBuy = new bool[101,10];
	public static bool[,] cup24AltCanBuy = new bool[101,10];
	public static bool[,] irl23AltCanBuy = new bool[101,10];
	public static bool[,] irl24AltCanBuy = new bool[101,10];
	
	public static bool[,] cup2020AltPaintAISpawning = new bool[101,10];
	public static bool[,] cup22AltPaintAISpawning = new bool[101,10];
	public static bool[,] cup23AltPaintAISpawning = new bool[101,10];
	public static bool[,] cup24AltPaintAISpawning = new bool[101,10];
	public static bool[,] irl23AltPaintAISpawning = new bool[101,10];
	public static bool[,] irl24AltPaintAISpawning = new bool[101,10];
	
    // Start is called before the first frame update
    void Start(){
		loadAlts();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public static void loadAlts(){
		cup2020AltPaintNames[2,1] = "#1 Patriot";
		cup2020AltPaintNames[3,1] = "#1 Halloween";
		cup2020AltPaintNames[3,2] = "#2 Icon";
		cup2020AltPaintNames[3,3] = "#3 Wrangled";
		cup2020AltPaintNames[3,4] = "#4 All Star";
		cup2020AltPaintNames[5,1] = "#1 Dirty";
		cup2020AltPaintNames[5,2] = "#2 Junked";
		cup2020AltPaintNames[6,1] = "#1 Taped";
		cup2020AltPaintNames[7,1] = "#1 Insure";
		cup2020AltPaintNames[9,1] = "#1 Hoot";
		cup2020AltPaintNames[9,2] = "#2 Aussie";
		cup2020AltPaintNames[11,1] = "#1 Pace Car";
		cup2020AltPaintNames[12,1] = "#1 Kobe";
		cup2020AltPaintNames[13,1] = "#1 Military";
		cup2020AltPaintNames[17,1] = "#1 Sunny";
		cup2020AltPaintNames[18,1] = "#1 Halloween";
		cup2020AltPaintNames[19,1] = "#1 USO";
		cup2020AltPaintNames[20,1] = "#1 Clashed";
		cup2020AltPaintNames[22,1] = "#1 Tribute";
		cup2020AltPaintNames[27,1] = "#1 Cup";
		cup2020AltPaintNames[32,1] = "#1 Halloween";
		cup2020AltPaintNames[37,1] = "#1 Seltzer";
		cup2020AltPaintNames[41,1] = "#1 Un Aero";
		cup2020AltPaintNames[42,1] = "#1 Halloween";
		cup2020AltPaintNames[43,1] = "#1 BLM";
		cup2020AltPaintNames[47,1] = "#1 Nitro";
		cup2020AltPaintNames[48,1] = "#1 Champ";
		cup2020AltPaintNames[48,2] = "#2 Cobalt";
		cup2020AltPaintNames[48,3] = "#3 All Star";
		cup2020AltPaintNames[51,1] = "#1 Warhawk";
		cup2020AltPaintNames[66,1] = "#1 Bandit";
		cup2020AltPaintNames[77,1] = "#1 Melon";
		cup2020AltPaintNames[88,1] = "#1 Cup";
		cup2020AltPaintNames[88,2] = "#2 Halloween";
		cup2020AltPaintNames[88,3] = "#3 Duck Num";
		cup2020AltPaintNames[95,1] = "#1 Core";
		
		cup22AltPaintNames[1,1] = "#1 LTT Winner";
		cup22AltPaintNames[1,2] = "#2 Final 4";
		cup22AltPaintNames[7,1] = "#1 Robbins";
		cup22AltPaintNames[9,1] = "#1 Final 4";
		cup22AltPaintNames[15,1] = "Stock (Ragan)";
		cup22AltPaintNames[16,1] = "Stock (Hemric)";
		cup22AltPaintNames[18,1] = "#1 Battery";
		cup22AltPaintNames[18,2] = "#2 Snicki";
		cup22AltPaintNames[18,3] = "#3 Skitl";
		cup22AltPaintNames[18,4] = "#4 Cookie";
		cup22AltPaintNames[20,1] = "#1 Final 4";
		cup22AltPaintNames[21,1] = "#1 Throwback";
		cup22AltPaintNames[22,1] = "#1 Final 4";
		cup22AltPaintNames[24,1] = "#1 Winner";
		cup22AltPaintNames[27,1] = "Stock (Hezemans)";
		cup22AltPaintNames[42,1] = "#1 Throwback";
		cup22AltPaintNames[43,1] = "#1 Throwback";
		cup22AltPaintNames[50,1] = "Stock (Daly)";
		cup22AltPaintNames[66,1] = "Stock (Said)";
		cup22AltPaintNames[77,1] = "Stock (Allgaier)";
		cup22AltPaintNames[78,1] = "Stock (Lally)";
		cup22AltPaintNames[99,1] = "#1 Lawnmower";
		
		cup23AltPaintNames[4,1] = "#1 Throwback";
		cup23AltPaintNames[4,2] = "#2 Champion";
		cup23AltPaintNames[5,1] = "#1 Final 4";
		cup23AltPaintNames[8,1] = "#1 Winner";
		cup23AltPaintNames[9,1] = "Stock (Berry)";
		cup23AltPaintNames[9,2] = "Stock (Taylor)";
		cup23AltPaintNames[12,1] = "#1 Final 4";
		cup23AltPaintNames[14,1] = "#1 Naughton";
		cup23AltPaintNames[15,1] = "Stock (Button)";
		cup23AltPaintNames[20,1] = "#1 Winner";
		cup23AltPaintNames[20,2] = "#2 Final 4";
		cup23AltPaintNames[22,1] = "#1 Winner";
		cup23AltPaintNames[24,1] = "#1 Winner";
		cup23AltPaintNames[24,2] = "#2 Final 4";
		cup23AltPaintNames[34,1] = "#1 McLovin";
		cup23AltPaintNames[41,1] = "#1 Bobby";
		cup23AltPaintNames[91,1] = "#1 Winner";
		
		cup24AltPaintNames[2,1] = "#1 DM2 Crew";
		cup24AltPaintNames[3,1] = "#1 Lion";
		cup24AltPaintNames[4,1] = "#1 Hard Work";
		cup24AltPaintNames[5,1] = "#1 Indy";
		cup24AltPaintNames[5,2] = "#2 40 Years";
		cup24AltPaintNames[6,1] = "#1 Half Off";
		cup24AltPaintNames[9,1] = "#1 40 Years";
		cup24AltPaintNames[11,1] = "#1 Winner";
		cup24AltPaintNames[12,1] = "#1 Body IV";
		cup24AltPaintNames[15,1] = "#1 Parnelli";
		cup24AltPaintNames[16,1] = "#1 MLG 1337";
		cup24AltPaintNames[17,1] = "#1 Submarine";
		cup24AltPaintNames[20,1] = "#1 Yippee";
		cup24AltPaintNames[23,1] = "#1 Money";
		cup24AltPaintNames[24,1] = "#1 40 Years";
		cup24AltPaintNames[31,1] = "#1 Golfer";
		cup24AltPaintNames[34,1] = "#1 Long John";
		cup24AltPaintNames[42,1] = "#1 Dolla";
		cup24AltPaintNames[48,1] = "#1 40 Years";
		cup24AltPaintNames[51,1] = "#1 Cops";
		cup24AltPaintNames[54,1] = "#1 Radio XM";
		cup24AltPaintNames[60,1] = "#1 Waters";
		cup24AltPaintNames[99,1] = "#1 Time Trial";
		cup24AltPaintNames[99,2] = "#2 Worldwide";
		
		irl23AltPaintNames[2,1] = "#1 The 500";
		irl23AltPaintNames[5,1] = "#1 Le Triple";
		irl23AltPaintNames[6,1] = "#1 500 Triple";
		irl23AltPaintNames[7,1] = "#1 Azure Triple";
		irl23AltPaintNames[28,1] = "#1 LGBTQ";
		
		irl24AltPaintNames[3,1] = "#1 Galaga";
		irl24AltPaintNames[3,2] = "#2 Yellow Sub";
		irl24AltPaintNames[14,1] = "#1 Patriot";
		irl24AltPaintNames[41,1] = "#1 Doggo";
		
		cup2020AltPaintDriver[3,2] = "Earnhardt";
		cup2020AltPaintDriver[3,3] = "Earnhardt";
		cup2020AltPaintDriver[3,4] = "Earnhardt";
		cup2020AltPaintDriver[6,1] = "Martin";
		cup2020AltPaintDriver[9,2] = "Ambrose";
		cup2020AltPaintDriver[11,1] = "Bodine";
		cup2020AltPaintDriver[41,1] = "Ku. Busch";
		cup2020AltPaintDriver[88,3] = "Earnhardt Jr";
		
		cup22AltPaintDriver[1,1] = "McMurray";
		cup22AltPaintDriver[15,1] = "Ragan";
		cup22AltPaintDriver[16,1] = "Hemric";
		cup22AltPaintDriver[27,1] = "Hezemans";
		cup22AltPaintDriver[50,1] = "Daly";
		cup22AltPaintDriver[66,1] = "Said";
		cup22AltPaintDriver[77,1] = "Allgaier";
		cup22AltPaintDriver[78,1] = "Lally";
		cup22AltPaintDriver[99,1] = "Edwards";
		
		cup23AltPaintDriver[9,1] = "Berry";
		cup23AltPaintDriver[9,2] = "Taylor";
		cup23AltPaintDriver[15,1] = "Button";
		cup23AltPaintDriver[91,1] = "V. Gisbergen";
		
		cup24AltPaintDriver[99,1] = "Edwards";
		
		cup2020AltPaintTheme[2,1] = "Patriot";
		cup2020AltPaintTheme[3,1] = "Halloween";
		cup2020AltPaintTheme[3,2] = "Earnhardt";
		cup2020AltPaintTheme[3,3] = "Earnhardt";
		cup2020AltPaintTheme[3,4] = "Earnhardt";
		cup2020AltPaintTheme[5,1] = "Wrecked";
		cup2020AltPaintTheme[5,2] = "Wrecked";
		cup2020AltPaintTheme[6,1] = "Wrecked";
		cup2020AltPaintTheme[7,1] = "2020";
		cup2020AltPaintTheme[9,1] = "2020";
		cup2020AltPaintTheme[9,2] = "Throwback";
		cup2020AltPaintTheme[11,1] = "Community";
		cup2020AltPaintTheme[12,1] = "2020";
		cup2020AltPaintTheme[13,1] = "Patriot";
		cup2020AltPaintTheme[17,1] = "2020";
		cup2020AltPaintTheme[18,1] = "Halloween";
		cup2020AltPaintTheme[19,1] = "Patriot";
		cup2020AltPaintTheme[20,1] = "Wrecked";
		cup2020AltPaintTheme[22,1] = "2020";
		cup2020AltPaintTheme[27,1] = "2020";
		cup2020AltPaintTheme[32,1] = "Halloween";
		cup2020AltPaintTheme[37,1] = "2020";
		cup2020AltPaintTheme[41,1] = "Wrecked";
		cup2020AltPaintTheme[42,1] = "Halloween";
		cup2020AltPaintTheme[43,1] = "2020";
		cup2020AltPaintTheme[47,1] = "2020";
		cup2020AltPaintTheme[48,1] = "Johnson";
		cup2020AltPaintTheme[48,2] = "Johnson";
		cup2020AltPaintTheme[48,3] = "Johnson";
		cup2020AltPaintTheme[51,1] = "Patriot";
		cup2020AltPaintTheme[66,1] = "2020";
		cup2020AltPaintTheme[77,1] = "2020";
		cup2020AltPaintTheme[88,1] = "2020";
		cup2020AltPaintTheme[88,2] = "Halloween";
		cup2020AltPaintTheme[88,3] = "Wrecked";
		cup2020AltPaintTheme[95,1] = "2020";
		
		cup22AltPaintTheme[1,1] = "Community";
		cup22AltPaintTheme[1,2] = "Final4";
		cup22AltPaintTheme[7,1] = "Throwback";
		cup22AltPaintTheme[9,1] = "Final4";
		cup22AltPaintTheme[15,1] = "Parttimer";
		cup22AltPaintTheme[16,1] = "Parttimer";
		cup22AltPaintTheme[18,1] = "AOAE";
		cup22AltPaintTheme[18,2] = "AOAE";
		cup22AltPaintTheme[18,3] = "AOAE";
		cup22AltPaintTheme[18,4] = "AOAE";
		cup22AltPaintTheme[20,1] = "Final4";
		cup22AltPaintTheme[21,1] = "Throwback";
		cup22AltPaintTheme[22,1] = "Final4";
		cup22AltPaintTheme[24,1] = "Winners";
		cup22AltPaintTheme[27,1] = "Parttimer";
		cup22AltPaintTheme[42,1] = "Throwback";
		cup22AltPaintTheme[43,1] = "Throwback";
		cup22AltPaintTheme[50,1] = "Parttimer";
		cup22AltPaintTheme[66,1] = "Parttimer";
		cup22AltPaintTheme[77,1] = "Parttimer";
		cup22AltPaintTheme[78,1] = "Parttimer";
		cup22AltPaintTheme[99,1] = "Wrecked";
		
		cup23AltPaintTheme[5,1] = "Final 4";
		cup23AltPaintTheme[8,1] = "Winners";
		cup23AltPaintTheme[9,1] = "Parttimer";
		cup23AltPaintTheme[9,2] = "Parttimer";
		cup23AltPaintTheme[12,1] = "Final 4";
		cup23AltPaintTheme[15,1] = "Parttimer";
		cup23AltPaintTheme[20,1] = "Winners";
		cup23AltPaintTheme[20,2] = "Final 4";
		cup23AltPaintTheme[22,1] = "Winners";
		cup23AltPaintTheme[24,1] = "Winners";
		cup23AltPaintTheme[24,2] = "Final 4";
		cup23AltPaintTheme[91,1] = "Winners";
		
		cup24AltPaintTheme[99,1] = "Community";
		
		irl23AltPaintTheme[2,1] = "Winners";
		
		//True = can't buy.. I know.. I know..
		cup2020AltCanBuy[11,1] = true;
		cup2020AltCanBuy[3,2] = true;
		cup2020AltCanBuy[3,3] = true;
		cup2020AltCanBuy[3,4] = true;
		cup2020AltCanBuy[48,1] = true;
		cup2020AltCanBuy[48,2] = true;
		cup2020AltCanBuy[48,3] = true;
		
		cup22AltCanBuy[1,1] = true;
		
		cup24AltCanBuy[2,1] = true;
		cup24AltCanBuy[99,1] = true;
		cup24AltCanBuy[99,2] = true;
		
		//true = don't spawn. I know this is backwards..
		cup2020AltPaintAISpawning[5,2] = true;
		cup2020AltPaintAISpawning[6,1] = true;
		cup2020AltPaintAISpawning[11,1] = true;
		cup2020AltPaintAISpawning[88,3] = true;
		
		cup22AltPaintAISpawning[1,1] = true;
		cup22AltPaintAISpawning[99,1] = true;
		
		cup24AltPaintAISpawning[2,1] = true;
		cup24AltPaintAISpawning[6,1] = true;
		cup24AltPaintAISpawning[99,1] = true;
	}
	
	public static string getAltPaintName(string seriesPrefix, int carNum, int altNum){
		string altPaintName = null;
		switch(seriesPrefix){
			case "cup20":
				altPaintName = cup2020AltPaintNames[carNum,altNum];
				break;
			case "cup22":
				altPaintName = cup22AltPaintNames[carNum,altNum];
				break;
			case "cup23":
				altPaintName = cup23AltPaintNames[carNum,altNum];
				break;
			case "cup24":
				altPaintName = cup24AltPaintNames[carNum,altNum];
				break;
			case "irl23":
				altPaintName = irl23AltPaintNames[carNum,altNum];
				break;
			case "irl24":
				altPaintName = irl24AltPaintNames[carNum,altNum];
				break;
			default:
				break;
		}
		return altPaintName;
	}
	
	public static string getAltPaintDriver(string seriesPrefix, int carNum, int altNum){
		switch(seriesPrefix){
			case "cup20":
				return cup2020AltPaintDriver[carNum,altNum];
				break;
			case "cup22":
				return cup22AltPaintDriver[carNum,altNum];
				break;
			case "cup23":
				return cup23AltPaintDriver[carNum,altNum];
				break;
			case "cup24":
				return cup24AltPaintDriver[carNum,altNum];
				break;
			case "irl23":
				return irl23AltPaintDriver[carNum,altNum];
				break;
			case "irl24":
				return irl24AltPaintDriver[carNum,altNum];
				break;
			default:
				return null;
				break;
		}
		return null;
	}
	
	public static bool getAltPaintCanBuy(string seriesPrefix, int carNum, int altNum){
		bool canBuy = false;
		switch(seriesPrefix){
			case "cup20":
				if(cup2020AltCanBuy[carNum,altNum] == true){
					canBuy = true;
				}
				break;
			case "cup22":
				if(cup22AltCanBuy[carNum,altNum] == true){
					canBuy = true;
				}
				break;
			case "cup23":
				if(cup23AltCanBuy[carNum,altNum] == true){
					canBuy = true;
				}
				break;
			case "cup24":
				if(cup24AltCanBuy[carNum,altNum] == true){
					canBuy = true;
				}
				break;
			case "irl23":
				if(irl23AltCanBuy[carNum,altNum] == true){
					canBuy = true;
				}
				break;
			case "irl24":
				if(irl24AltCanBuy[carNum,altNum] == true){
					canBuy = true;
				}
				break;
			default:
				break;
		}
		return canBuy;
	}
	
	public static bool getAltPaintAISpawning(string seriesPrefix, int carNum, int altNum){
		bool canSpawn = false;
		switch(seriesPrefix){
			case "cup20":
				if(cup2020AltPaintAISpawning[carNum,altNum] == true){
					canSpawn = true;
				}
				break;
			case "cup22":
				if(cup22AltPaintAISpawning[carNum,altNum] == true){
					canSpawn = true;
				}
				break;
			case "cup23":
				if(cup23AltPaintAISpawning[carNum,altNum] == true){
					canSpawn = true;
				}
				break;
			case "cup24":
				if(cup24AltPaintAISpawning[carNum,altNum] == true){
					canSpawn = true;
				}
				break;
			case "irl23":
				if(irl23AltPaintAISpawning[carNum,altNum] == true){
					canSpawn = true;
				}
				break;
			case "irl24":
				if(irl24AltPaintAISpawning[carNum,altNum] == true){
					canSpawn = true;
				}
				break;
			default:
				break;
		}
		return canSpawn;
	}
	
	public static string getAltPaintTheme(string seriesPrefix, int carNum, int altNum){
		switch(seriesPrefix){
			case "cup20":
				return cup2020AltPaintTheme[carNum,altNum];
				break;
			case "cup22":
				return cup22AltPaintTheme[carNum,altNum];
				break;
			case "cup23":
				return cup23AltPaintTheme[carNum,altNum];
				break;
			case "cup24":
				return cup24AltPaintTheme[carNum,altNum];
				break;
			case "irl23":
				return irl23AltPaintTheme[carNum,altNum];
				break;
			case "irl24":
				return irl24AltPaintTheme[carNum,altNum];
				break;
			default:
				return "Blank";
				break;
		}
		return "Blank";
	}
}