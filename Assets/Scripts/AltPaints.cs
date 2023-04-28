using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AltPaints : MonoBehaviour {
	
	public static string[,] cup2020AltPaintNames = new string[101,10];
	public static string[,] cup22AltPaintNames = new string[101,10];
	public static string[,] cup23AltPaintNames = new string[101,10];
	public static string[,] cup2020AltPaintDriver = new string[101,10];
	public static string[,] cup22AltPaintDriver = new string[101,10];
	public static string[,] cup23AltPaintDriver = new string[101,10];
	public static int[,] cup2020AltPaintRarity = new int[101,10];
	public static string[,] cup2020AltPaintTheme = new string[101,10];
	public static string[,] cup22AltPaintTheme = new string[101,10];
	public static string[,] cup23AltPaintTheme = new string[101,10];
	
	public static bool[,] cup2020AltPaintAISpawning = new bool[101,10];
	public static bool[,] cup22AltPaintAISpawning = new bool[101,10];
	public static bool[,] cup23AltPaintAISpawning = new bool[101,10];
	
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
		
		cup23AltPaintNames[8,1] = "#1 Winner";
		cup23AltPaintNames[9,1] = "Stock (Berry)";
		cup23AltPaintNames[9,2] = "Stock (Taylor)";
		cup23AltPaintNames[15,1] = "Stock (Button)";
		cup23AltPaintNames[20,1] = "#1 Winner";
		cup23AltPaintNames[22,1] = "#1 Winner";
		cup23AltPaintNames[24,1] = "#1 Winner";
		
		cup2020AltPaintDriver[3,2] = "Earnhardt";
		cup2020AltPaintDriver[3,3] = "Earnhardt";
		cup2020AltPaintDriver[3,4] = "Earnhardt";
		cup2020AltPaintDriver[6,1] = "Martin";
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
		
		//true = don't spawn. I know this is backwards..
		cup2020AltPaintAISpawning[5,2] = true;
		cup2020AltPaintAISpawning[6,1] = true;
		cup2020AltPaintAISpawning[11,1] = true;
		cup2020AltPaintAISpawning[88,3] = true;
		
		cup22AltPaintAISpawning[1,1] = true;
		cup22AltPaintAISpawning[99,1] = true;
		
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
		
		cup23AltPaintTheme[8,1] = "Winners";
		cup23AltPaintTheme[9,1] = "Parttimer";
		cup23AltPaintTheme[9,2] = "Parttimer";
		cup23AltPaintTheme[15,1] = "Parttimer";
		cup23AltPaintTheme[20,1] = "Winners";
		cup23AltPaintTheme[22,1] = "Winners";
		cup23AltPaintTheme[24,1] = "Winners";
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
			default:
				return null;
				break;
		}
		return null;
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
			default:
				return "Blank";
				break;
		}
		return "Blank";
	}
}